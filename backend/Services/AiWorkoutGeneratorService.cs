using backend.Data;
using backend.Dtos;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class AiWorkoutGeneratorService : IAiWorkoutGeneratorService
{
    private static readonly HashSet<string> PushMuscles = new(StringComparer.Ordinal)
    {
        "chest",
        "pectorals",
        "shoulders",
        "deltoids",
        "triceps",
    };

    private static readonly HashSet<string> PullMuscles = new(StringComparer.Ordinal)
    {
        "back",
        "lats",
        "latissimus dorsi",
        "traps",
        "trapezius",
        "rhomboids",
        "rear delts",
        "biceps",
        "forearms",
    };

    private static readonly HashSet<string> LowerMuscles = new(StringComparer.Ordinal)
    {
        "glutes",
        "gluteus",
        "hamstrings",
        "quadriceps",
        "quads",
        "calves",
        "adductors",
        "abductors",
        "hip flexors",
    };

    private static readonly HashSet<string> CoreMuscles = new(StringComparer.Ordinal)
    {
        "abs",
        "abdominals",
        "obliques",
        "core",
        "lower back",
        "erectors",
    };

    private static readonly string[] CompoundKeywords =
    [
        "bench",
        "press",
        "row",
        "pull up",
        "pullup",
        "pulldown",
        "deadlift",
        "squat",
        "lunge",
        "thrust",
        "dip",
    ];

    private static readonly string[] IsolationKeywords =
    [
        "curl",
        "extension",
        "raise",
        "fly",
        "kickback",
        "pushdown",
        "adduction",
        "abduction",
        "pullover",
    ];

    private static readonly string[] CoreKeywords =
    [
        "plank",
        "crunch",
        "sit up",
        "sit-up",
        "leg raise",
        "dead bug",
        "hollow",
        "twist",
        "bird dog",
    ];

    private readonly AppDbContext _dbContext;

    public AiWorkoutGeneratorService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AiWorkoutPlanDto> GenerateAsync(
        string userId,
        AiWorkoutGenerateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!int.TryParse(userId, out var parsedUserId) || parsedUserId <= 0)
        {
            throw new InvalidOperationException("Authenticated user identifier is invalid.");
        }

        // TODO: Replace this deterministic planner with a provider-backed LLM implementation behind the same interface when external AI integration is introduced.
        var goalsTask = _dbContext.GoalSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(goal => goal.UserId == parsedUserId, cancellationToken);
        var recentWorkoutsTask = _dbContext.Workouts
            .AsNoTracking()
            .Where(workout => workout.UserId == parsedUserId)
            .Include(workout => workout.ExerciseEntries)
            .ThenInclude(entry => entry.Sets)
            .OrderByDescending(workout => workout.Date)
            .ThenByDescending(workout => workout.Id)
            .Take(12)
            .ToListAsync(cancellationToken);
        var catalogTask = _dbContext.ExerciseCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(goalsTask, recentWorkoutsTask, catalogTask);

        var goals = goalsTask.Result;
        var recentWorkouts = recentWorkoutsTask.Result;
        var catalogItems = catalogTask.Result;

        var context = BuildContext(request, goals, recentWorkouts.Count);
        var recentExerciseCounts = BuildRecentExerciseCounts(recentWorkouts.Take(2));
        var recentExerciseWeights = BuildRecentExerciseWeightLookup(recentWorkouts);
        var catalogCandidates = BuildCatalogCandidates(catalogItems, context.ExcludedExercises);

        var sections = new List<AiWorkoutSectionDto>();

        if (context.IncludeWarmup)
        {
            sections.Add(new AiWorkoutSectionDto
            {
                Name = "Warm-up",
                Exercises = BuildWarmupExercises(context),
            });
        }

        var mainSectionExercises = BuildMainWorkoutExercises(
            context,
            catalogCandidates,
            recentExerciseCounts,
            recentExerciseWeights);

        sections.Add(new AiWorkoutSectionDto
        {
            Name = GetMainSectionName(context.WorkoutType),
            Exercises = mainSectionExercises,
        });

        if (context.IncludeCooldown)
        {
            sections.Add(new AiWorkoutSectionDto
            {
                Name = "Cooldown",
                Exercises = BuildCooldownExercises(context),
            });
        }

        return new AiWorkoutPlanDto
        {
            Title = $"{FormatLabel(context.WorkoutType)} {FormatLabel(context.Goal)} Plan",
            Goal = FormatLabel(context.Goal),
            WorkoutType = FormatLabel(context.WorkoutType),
            EstimatedDurationMinutes = EstimateDurationMinutes(sections, context.DurationMinutes),
            Difficulty = FormatLabel(context.FitnessLevel),
            Sections = sections,
            Notes = BuildPlanNotes(context, mainSectionExercises, recentWorkouts.Count),
        };
    }

    private static WorkoutGenerationContext BuildContext(
        AiWorkoutGenerateRequest request,
        GoalSettings? goals,
        int recentWorkoutCount)
    {
        var goal = NormalizeGoal(request.Goal, goals?.FitnessPhase);
        var requestedTargetMuscles = NormalizeDistinctList(request.TargetMuscles);
        var workoutType = NormalizeWorkoutType(request.PreferredWorkoutType, requestedTargetMuscles, goals?.WeeklyWorkoutTarget);
        var fitnessLevel = NormalizeFitnessLevel(request.FitnessLevel, recentWorkoutCount);
        var durationMinutes = Math.Clamp(request.DurationMinutes ?? GetDefaultDurationMinutes(fitnessLevel), 20, 180);

        return new WorkoutGenerationContext
        {
            Goal = goal,
            WorkoutType = workoutType,
            FitnessLevel = fitnessLevel,
            DurationMinutes = durationMinutes,
            TargetMuscles = requestedTargetMuscles,
            ExcludedExercises = NormalizeDistinctList(request.ExcludedExercises).ToHashSet(StringComparer.Ordinal),
            IncludeWarmup = request.IncludeWarmup,
            IncludeCooldown = request.IncludeCooldown,
            SetTarget = GetSetTarget(fitnessLevel),
            RepTarget = GetRepTarget(goal),
            RestSeconds = GetRestSeconds(goal),
        };
    }

    private static List<CatalogExerciseCandidate> BuildCatalogCandidates(
        IEnumerable<ExerciseCatalogItem> items,
        HashSet<string> excludedExercises)
    {
        return items
            .Select(item => MapCatalogCandidate(item))
            .Where(candidate => candidate is not null)
            .Cast<CatalogExerciseCandidate>()
            .Where(candidate => !excludedExercises.Contains(NormalizeText(candidate.Name)))
            .ToList();
    }

    private static CatalogExerciseCandidate? MapCatalogCandidate(ExerciseCatalogItem item)
    {
        var name = GetEffectiveName(item);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var muscles = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.PrimaryMuscle))
        {
            muscles.Add(NormalizeMuscle(item.PrimaryMuscle));
        }

        muscles.AddRange((item.SecondaryMuscles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeMuscle)
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        muscles = muscles
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var focusGroup = ResolveFocusGroup(name, muscles);
        var category = ResolveCategory(name, item.Equipment, muscles);

        return new CatalogExerciseCandidate
        {
            Id = item.Id,
            Name = name,
            NameNormalized = NormalizeText(name),
            Instructions = GetEffectiveInstructions(item),
            PrimaryMuscle = string.IsNullOrWhiteSpace(item.PrimaryMuscle) ? null : item.PrimaryMuscle.Trim(),
            PrimaryMuscleNormalized = string.IsNullOrWhiteSpace(item.PrimaryMuscle) ? null : NormalizeMuscle(item.PrimaryMuscle),
            Muscles = muscles,
            Equipment = NormalizeOptionalText(item.Equipment),
            Difficulty = NormalizeOptionalText(item.Difficulty),
            ThumbnailUrl = GetEffectiveThumbnailUrl(item),
            VideoUrl = GetEffectiveVideoUrl(item),
            FocusGroup = focusGroup,
            Category = category,
            IsBodyweight = string.Equals(NormalizeOptionalText(item.Equipment), "bodyweight", StringComparison.OrdinalIgnoreCase)
                           || category == "bodyweight",
            IsAdvanced = string.Equals(NormalizeOptionalText(item.Difficulty), "advanced", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static List<AiWorkoutExerciseDto> BuildMainWorkoutExercises(
        WorkoutGenerationContext context,
        IReadOnlyList<CatalogExerciseCandidate> catalogCandidates,
        IReadOnlyDictionary<string, int> recentExerciseCounts,
        IReadOnlyDictionary<string, decimal> recentExerciseWeights)
    {
        var slots = BuildMainSlots(context);
        var selectedExercises = new List<AiWorkoutExerciseDto>(slots.Count);
        var usedCatalogIds = new HashSet<int>();
        var usedExerciseNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            var candidate = SelectBestCandidate(
                catalogCandidates,
                slot,
                context,
                recentExerciseCounts,
                usedCatalogIds,
                usedExerciseNames,
                allowRecent: false);

            candidate ??= SelectBestCandidate(
                catalogCandidates,
                slot,
                context,
                recentExerciseCounts,
                usedCatalogIds,
                usedExerciseNames,
                allowRecent: true);

            if (candidate is not null)
            {
                usedCatalogIds.Add(candidate.Id);
                usedExerciseNames.Add(candidate.NameNormalized);
                selectedExercises.Add(MapSelectedCatalogExercise(candidate, slot, context, recentExerciseWeights));
                continue;
            }

            var fallbackExercise = BuildFallbackExercise(slot, context);
            usedExerciseNames.Add(NormalizeText(fallbackExercise.Name));
            selectedExercises.Add(fallbackExercise);
        }

        return selectedExercises;
    }

    private static CatalogExerciseCandidate? SelectBestCandidate(
        IReadOnlyList<CatalogExerciseCandidate> candidates,
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        IReadOnlyDictionary<string, int> recentExerciseCounts,
        IReadOnlySet<int> usedCatalogIds,
        IReadOnlySet<string> usedExerciseNames,
        bool allowRecent)
    {
        CatalogExerciseCandidate? bestCandidate = null;
        var bestScore = int.MinValue;

        foreach (var candidate in candidates)
        {
            if (usedCatalogIds.Contains(candidate.Id) || usedExerciseNames.Contains(candidate.NameNormalized))
            {
                continue;
            }

            var recentCount = recentExerciseCounts.TryGetValue(candidate.NameNormalized, out var value) ? value : 0;
            if (!allowRecent && recentCount > 0)
            {
                continue;
            }

            var score = ScoreCandidate(candidate, slot, context, recentCount);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        return bestScore >= 18 ? bestCandidate : null;
    }

    private static int ScoreCandidate(
        CatalogExerciseCandidate candidate,
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        int recentCount)
    {
        var score = 0;

        if (string.Equals(candidate.FocusGroup, slot.FocusGroup, StringComparison.Ordinal))
        {
            score += 40;
        }
        else if (string.Equals(slot.FocusGroup, "upper", StringComparison.Ordinal)
                 && (candidate.FocusGroup == "push" || candidate.FocusGroup == "pull"))
        {
            score += 28;
        }
        else if (string.Equals(slot.FocusGroup, "full-body", StringComparison.Ordinal)
                 && candidate.FocusGroup != "mobility")
        {
            score += 22;
        }
        else if (slot.FocusGroup == "lower" && candidate.FocusGroup == "core")
        {
            score += 6;
        }
        else if (slot.FocusGroup == "core" && candidate.FocusGroup == "lower")
        {
            score += 6;
        }

        if (slot.PreferredCategories.Contains(candidate.Category, StringComparer.Ordinal))
        {
            score += 14;
        }

        if (!string.IsNullOrWhiteSpace(slot.TargetMuscle))
        {
            if (string.Equals(candidate.PrimaryMuscleNormalized, slot.TargetMuscle, StringComparison.Ordinal))
            {
                score += 30;
            }
            else if (candidate.Muscles.Contains(slot.TargetMuscle, StringComparer.Ordinal))
            {
                score += 16;
            }
            else
            {
                score -= 10;
            }
        }

        if (context.TargetMuscles.Count > 0)
        {
            if (candidate.Muscles.Intersect(context.TargetMuscles, StringComparer.Ordinal).Any())
            {
                score += 18;
            }
            else
            {
                score -= 8;
            }
        }

        if (recentCount > 0)
        {
            score -= 18 * recentCount;
        }

        if (!string.IsNullOrWhiteSpace(candidate.Instructions))
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(candidate.ThumbnailUrl))
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(candidate.VideoUrl))
        {
            score += 2;
        }

        if (context.Goal == "strength" && candidate.Category == "compound")
        {
            score += 8;
        }

        if (context.Goal == "muscle gain" && candidate.Category == "isolation")
        {
            score += 6;
        }

        if ((context.Goal == "fat loss" || context.Goal == "endurance" || context.Goal == "general fitness") && candidate.IsBodyweight)
        {
            score += 5;
        }

        if (context.FitnessLevel == "beginner" && candidate.IsAdvanced)
        {
            score -= 16;
        }

        if (context.FitnessLevel == "advanced" && candidate.Category == "compound")
        {
            score += 3;
        }

        return score;
    }

    private static AiWorkoutExerciseDto MapSelectedCatalogExercise(
        CatalogExerciseCandidate candidate,
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        IReadOnlyDictionary<string, decimal> recentExerciseWeights)
    {
        return new AiWorkoutExerciseDto
        {
            ExerciseCatalogItemId = candidate.Id,
            Name = candidate.Name,
            Category = FormatLabel(candidate.Category),
            TargetMuscle = candidate.PrimaryMuscle ?? FormatLabel(slot.TargetMuscle ?? candidate.FocusGroup),
            Sets = GetSetCountForSlot(slot, context.SetTarget),
            Reps = GetRepTargetForSlot(slot, context.RepTarget, context.Goal),
            SuggestedWeight = BuildSuggestedWeight(candidate, context, recentExerciseWeights),
            RestSeconds = GetRestSecondsForSlot(slot, context.RestSeconds, context.Goal),
            Instructions = string.IsNullOrWhiteSpace(candidate.Instructions)
                ? BuildFallbackInstruction(candidate.FocusGroup, candidate.Category)
                : candidate.Instructions!,
            ThumbnailUrl = candidate.ThumbnailUrl,
            VideoUrl = candidate.VideoUrl,
        };
    }

    private static List<WorkoutSlot> BuildMainSlots(WorkoutGenerationContext context)
    {
        var slotCount = GetMainExerciseCount(context.WorkoutType, context.DurationMinutes);
        if (context.TargetMuscles.Count > 0)
        {
            var slots = new List<WorkoutSlot>(slotCount);
            for (var index = 0; index < slotCount; index++)
            {
                var targetMuscle = context.TargetMuscles[index % context.TargetMuscles.Count];
                slots.Add(new WorkoutSlot
                {
                    FocusGroup = ResolveFocusGroupForTargetMuscle(targetMuscle),
                    TargetMuscle = targetMuscle,
                    PreferredCategories = index < 2 ? ["compound", "bodyweight"] : ["compound", "isolation", "bodyweight", "core"],
                });
            }

            return slots;
        }

        List<WorkoutSlot> baseSlots = context.WorkoutType switch
        {
            "upper" => new List<WorkoutSlot>
            {
                BuildSlot("push", "compound"),
                BuildSlot("pull", "compound"),
                BuildSlot("push", "isolation", "bodyweight"),
                BuildSlot("pull", "isolation", "bodyweight"),
                BuildSlot("core", "core", "bodyweight"),
                BuildSlot("upper", "compound", "isolation"),
            },
            "lower" or "legs" => new List<WorkoutSlot>
            {
                BuildSlot("lower", "compound"),
                BuildSlot("lower", "compound"),
                BuildSlot("lower", "isolation", "bodyweight"),
                BuildSlot("core", "core", "bodyweight"),
                BuildSlot("lower", "isolation", "bodyweight"),
                BuildSlot("full-body", "compound", "bodyweight"),
            },
            "push" => new List<WorkoutSlot>
            {
                BuildSlot("push", "compound"),
                BuildSlot("push", "compound", "bodyweight"),
                BuildSlot("push", "isolation", "bodyweight"),
                BuildSlot("core", "core", "bodyweight"),
                BuildSlot("push", "isolation"),
            },
            "pull" => new List<WorkoutSlot>
            {
                BuildSlot("pull", "compound"),
                BuildSlot("pull", "compound", "bodyweight"),
                BuildSlot("pull", "isolation", "bodyweight"),
                BuildSlot("core", "core", "bodyweight"),
                BuildSlot("pull", "isolation"),
            },
            "core" => new List<WorkoutSlot>
            {
                BuildSlot("core", "core", "bodyweight"),
                BuildSlot("core", "core", "bodyweight"),
                BuildSlot("lower", "bodyweight", "compound"),
                BuildSlot("full-body", "bodyweight", "compound"),
                BuildSlot("core", "core"),
            },
            _ => new List<WorkoutSlot>
            {
                BuildSlot("lower", "compound"),
                BuildSlot("push", "compound"),
                BuildSlot("pull", "compound"),
                BuildSlot("core", "core", "bodyweight"),
                BuildSlot("lower", "isolation", "bodyweight"),
                BuildSlot("push", "isolation", "bodyweight"),
                BuildSlot("pull", "isolation", "bodyweight"),
            },
        };

        return baseSlots.Take(slotCount).ToList();
    }

    private static WorkoutSlot BuildSlot(string focusGroup, params string[] preferredCategories)
    {
        return new WorkoutSlot
        {
            FocusGroup = focusGroup,
            PreferredCategories = preferredCategories.ToList(),
        };
    }

    private static List<AiWorkoutExerciseDto> BuildWarmupExercises(WorkoutGenerationContext context)
    {
        var movementPrep = context.WorkoutType switch
        {
            "upper" or "push" or "pull" => new AiWorkoutExerciseDto
            {
                Name = "Shoulder and upper-back activation",
                Category = "Warmup",
                TargetMuscle = "Shoulders",
                Sets = 2,
                Reps = "10-12 reps",
                RestSeconds = 30,
                Instructions = "Use controlled shoulder circles, band pull-aparts, or scapular wall slides to raise temperature and open the upper body.",
                SuggestedWeight = "Very light band or bodyweight only.",
            },
            "lower" or "legs" => new AiWorkoutExerciseDto
            {
                Name = "Hip and lower-body primer",
                Category = "Warmup",
                TargetMuscle = "Lower body",
                Sets = 2,
                Reps = "8-10 reps",
                RestSeconds = 30,
                Instructions = "Move through bodyweight squats, hip hinges, and lateral lunges to open the hips and knees before loading.",
                SuggestedWeight = "Bodyweight only.",
            },
            _ => new AiWorkoutExerciseDto
            {
                Name = "Dynamic full-body prep",
                Category = "Warmup",
                TargetMuscle = "Full body",
                Sets = 2,
                Reps = "6-8 reps per side",
                RestSeconds = 30,
                Instructions = "Use a walking knee hug, inchworm, and world's-greatest-stretch sequence to prepare the whole body for the session.",
                SuggestedWeight = "Bodyweight only.",
            },
        };

        return
        [
            new AiWorkoutExerciseDto
            {
                Name = "Easy cardio ramp-up",
                Category = "Warmup",
                TargetMuscle = "Cardiovascular",
                Sets = 1,
                Reps = "3-5 min",
                RestSeconds = 20,
                Instructions = "Use a brisk walk, easy bike, or light row to bring your heart rate up gradually before the working sets begin.",
                SuggestedWeight = "Keep intensity conversational.",
            },
            movementPrep,
        ];
    }

    private static List<AiWorkoutExerciseDto> BuildCooldownExercises(WorkoutGenerationContext context)
    {
        var stretchTarget = context.WorkoutType switch
        {
            "upper" or "push" => "Chest and shoulder stretch",
            "pull" => "Lat and biceps stretch",
            "lower" or "legs" => "Hip flexor and hamstring stretch",
            "core" => "Breathing and trunk reset",
            _ => "Full-body stretch",
        };

        return
        [
            new AiWorkoutExerciseDto
            {
                Name = "Breathing downshift",
                Category = "Cooldown",
                TargetMuscle = "Recovery",
                Sets = 1,
                Reps = "2-3 min",
                RestSeconds = 20,
                Instructions = "Walk slowly or lie on your back and bring your breathing down with long exhales before finishing the session.",
                SuggestedWeight = null,
            },
            new AiWorkoutExerciseDto
            {
                Name = stretchTarget,
                Category = "Cooldown",
                TargetMuscle = FormatLabel(context.WorkoutType == "full-body" ? "full body" : context.WorkoutType),
                Sets = 1,
                Reps = "30-45 sec per side",
                RestSeconds = 20,
                Instructions = "Choose the tightest muscles from the session and stretch them gently without forcing end ranges.",
                SuggestedWeight = null,
            },
        ];
    }

    private static AiWorkoutExerciseDto BuildFallbackExercise(WorkoutSlot slot, WorkoutGenerationContext context)
    {
        return slot.FocusGroup switch
        {
            "push" => new AiWorkoutExerciseDto
            {
                Name = "Push-up",
                Category = "Bodyweight",
                TargetMuscle = "Chest",
                Sets = GetSetCountForSlot(slot, context.SetTarget),
                Reps = GetRepTargetForSlot(slot, context.RepTarget, context.Goal),
                SuggestedWeight = "Bodyweight only.",
                RestSeconds = GetRestSecondsForSlot(slot, context.RestSeconds, context.Goal),
                Instructions = "Keep your ribs down, move through a full range, and stop 1-2 reps before form breaks.",
            },
            "pull" => new AiWorkoutExerciseDto
            {
                Name = "Band row or inverted row",
                Category = "Bodyweight",
                TargetMuscle = "Back",
                Sets = GetSetCountForSlot(slot, context.SetTarget),
                Reps = GetRepTargetForSlot(slot, context.RepTarget, context.Goal),
                SuggestedWeight = "Use a band or setup that leaves 1-2 reps in reserve.",
                RestSeconds = GetRestSecondsForSlot(slot, context.RestSeconds, context.Goal),
                Instructions = "Keep the ribcage stacked, pull elbows back, and pause briefly when the shoulder blades retract.",
            },
            "lower" => new AiWorkoutExerciseDto
            {
                Name = "Goblet squat or split squat",
                Category = "Compound",
                TargetMuscle = "Quadriceps",
                Sets = GetSetCountForSlot(slot, context.SetTarget),
                Reps = GetRepTargetForSlot(slot, context.RepTarget, context.Goal),
                SuggestedWeight = "Choose a controlled load that leaves 1-2 reps in reserve.",
                RestSeconds = GetRestSecondsForSlot(slot, context.RestSeconds, context.Goal),
                Instructions = "Move with full depth you can control, keep the feet planted, and maintain a steady torso position.",
            },
            "core" => new AiWorkoutExerciseDto
            {
                Name = "Front plank",
                Category = "Core",
                TargetMuscle = "Core",
                Sets = Math.Max(2, context.SetTarget - 1),
                Reps = context.Goal == "strength" ? "20-30 sec" : "30-45 sec",
                SuggestedWeight = "Bodyweight only.",
                RestSeconds = 45,
                Instructions = "Brace the trunk, squeeze glutes lightly, and hold a straight line without letting the low back sag.",
            },
            _ => new AiWorkoutExerciseDto
            {
                Name = "Walking lunge",
                Category = "Bodyweight",
                TargetMuscle = "Lower body",
                Sets = GetSetCountForSlot(slot, context.SetTarget),
                Reps = GetRepTargetForSlot(slot, context.RepTarget, context.Goal),
                SuggestedWeight = "Bodyweight or light dumbbells.",
                RestSeconds = GetRestSecondsForSlot(slot, context.RestSeconds, context.Goal),
                Instructions = "Step long enough to keep the front heel grounded and control each rep instead of rushing through the set.",
            },
        };
    }

    private static IReadOnlyDictionary<string, int> BuildRecentExerciseCounts(IEnumerable<Workout> workouts)
    {
        return workouts
            .SelectMany(workout => workout.ExerciseEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ExerciseName))
            .GroupBy(entry => NormalizeText(entry.ExerciseName))
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, decimal> BuildRecentExerciseWeightLookup(IEnumerable<Workout> workouts)
    {
        return workouts
            .SelectMany(workout => workout.ExerciseEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ExerciseName) && entry.Sets.Count > 0)
            .GroupBy(entry => NormalizeText(entry.ExerciseName))
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(entry => entry.Sets).Max(set => set.WeightKg),
                StringComparer.Ordinal);
    }

    private static string? BuildSuggestedWeight(
        CatalogExerciseCandidate candidate,
        WorkoutGenerationContext context,
        IReadOnlyDictionary<string, decimal> recentExerciseWeights)
    {
        if (candidate.IsBodyweight)
        {
            return "Bodyweight only. Leave 1-2 reps in reserve.";
        }

        if (recentExerciseWeights.TryGetValue(candidate.NameNormalized, out var recentWeight) && recentWeight > 0)
        {
            return $"Around {recentWeight:0.#} kg based on a recent top set.";
        }

        return context.FitnessLevel switch
        {
            "beginner" => "Start light and finish each set with 2-3 reps still in reserve.",
            "advanced" => "Use a challenging but repeatable load that keeps the target reps clean.",
            _ => "Choose a moderate load that leaves 1-2 reps in reserve.",
        };
    }

    private static int GetSetCountForSlot(WorkoutSlot slot, int baseSetTarget)
    {
        if (slot.FocusGroup == "core")
        {
            return Math.Max(2, baseSetTarget - 1);
        }

        if (slot.PreferredCategories.Contains("compound", StringComparer.Ordinal))
        {
            return baseSetTarget;
        }

        return Math.Max(2, baseSetTarget - 1);
    }

    private static string GetRepTargetForSlot(WorkoutSlot slot, string baseRepTarget, string goal)
    {
        if (slot.FocusGroup == "core")
        {
            return goal == "strength" ? "8-12 controlled reps" : "12-15 reps";
        }

        return baseRepTarget;
    }

    private static int GetRestSecondsForSlot(WorkoutSlot slot, int baseRestSeconds, string goal)
    {
        if (slot.FocusGroup == "core")
        {
            return Math.Min(baseRestSeconds, 60);
        }

        if (goal == "strength" && slot.PreferredCategories.Contains("compound", StringComparer.Ordinal))
        {
            return Math.Max(baseRestSeconds, 120);
        }

        return baseRestSeconds;
    }

    private static int GetMainExerciseCount(string workoutType, int durationMinutes)
    {
        var baseCount = durationMinutes switch
        {
            <= 30 => 4,
            <= 45 => 5,
            <= 60 => 6,
            _ => 7,
        };

        return workoutType switch
        {
            "push" or "pull" or "upper" or "lower" or "legs" => Math.Max(4, baseCount - 1),
            "core" => Math.Max(3, baseCount - 2),
            _ => baseCount,
        };
    }

    private static int EstimateDurationMinutes(IReadOnlyCollection<AiWorkoutSectionDto> sections, int requestedDurationMinutes)
    {
        var totalMinutes = 0m;

        foreach (var exercise in sections.SelectMany(section => section.Exercises))
        {
            totalMinutes += EstimateExerciseMinutes(exercise);
        }

        var computed = (int)Math.Ceiling(totalMinutes);
        return Math.Clamp(Math.Max(computed, requestedDurationMinutes - 5), 20, 180);
    }

    private static decimal EstimateExerciseMinutes(AiWorkoutExerciseDto exercise)
    {
        if (exercise.Reps.Contains("min", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseLeadingNumber(exercise.Reps) ?? 3m;
        }

        if (exercise.Reps.Contains("sec", StringComparison.OrdinalIgnoreCase))
        {
            var seconds = TryParseLeadingNumber(exercise.Reps) ?? 30m;
            return seconds / 60m + 0.5m;
        }

        var workSecondsPerSet = exercise.Category?.Equals("Core", StringComparison.OrdinalIgnoreCase) == true ? 40m : 50m;
        return exercise.Sets * ((workSecondsPerSet + exercise.RestSeconds) / 60m) + 0.4m;
    }

    private static decimal? TryParseLeadingNumber(string value)
    {
        var digits = new string(value.TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
        return decimal.TryParse(digits, out var parsed) ? parsed : null;
    }

    private static List<string> BuildPlanNotes(
        WorkoutGenerationContext context,
        IReadOnlyCollection<AiWorkoutExerciseDto> mainExercises,
        int recentWorkoutCount)
    {
        var notes = new List<string>
        {
            "This plan is read-only for the MVP and is not saved to your workout log automatically.",
            "Swap out any movement that causes pain, and keep 1-3 reps in reserve on early work sets unless you intentionally want a hard top set.",
        };

        if (recentWorkoutCount < 2)
        {
            notes.Add("Recent workout history is limited, so exercise rotation and weight guidance defaulted to conservative assumptions.");
        }

        if (mainExercises.Any(exercise => exercise.ExerciseCatalogItemId is null))
        {
            notes.Add("Some main-session movements fell back to safe generic exercises because the local catalog did not have a strong enough match.");
        }

        if (context.TargetMuscles.Count > 0)
        {
            notes.Add($"Target muscles were prioritized in this order: {string.Join(", ", context.TargetMuscles.Select(FormatLabel))}.");
        }

        if (context.Goal == "strength")
        {
            notes.Add("Longer rest periods and lower rep targets were chosen to protect bar speed and output quality.");
        }
        else if (context.Goal == "muscle gain")
        {
            notes.Add("The plan leans slightly toward moderate rep accumulation and accessory coverage to support hypertrophy.");
        }
        else if (context.Goal == "fat loss" || context.Goal == "general fitness")
        {
            notes.Add("Rest times are shorter to keep the session moving while still preserving controlled exercise quality.");
        }

        return notes;
    }

    private static string NormalizeGoal(string? goal, string? goalPhase)
    {
        var normalizedGoal = NormalizeText(goal);
        if (!string.IsNullOrWhiteSpace(normalizedGoal))
        {
            if (normalizedGoal.Contains("strength", StringComparison.Ordinal))
            {
                return "strength";
            }

            if (normalizedGoal.Contains("hypertrophy", StringComparison.Ordinal)
                || normalizedGoal.Contains("muscle", StringComparison.Ordinal)
                || normalizedGoal.Contains("gain", StringComparison.Ordinal)
                || normalizedGoal.Contains("bulk", StringComparison.Ordinal))
            {
                return "muscle gain";
            }

            if (normalizedGoal.Contains("fat", StringComparison.Ordinal)
                || normalizedGoal.Contains("cut", StringComparison.Ordinal)
                || normalizedGoal.Contains("loss", StringComparison.Ordinal))
            {
                return "fat loss";
            }

            if (normalizedGoal.Contains("endurance", StringComparison.Ordinal))
            {
                return "endurance";
            }
        }

        return NormalizeText(goalPhase) switch
        {
            "bulk" => "muscle gain",
            "cut" => "fat loss",
            _ => "general fitness",
        };
    }

    private static string NormalizeWorkoutType(string? workoutType, IReadOnlyList<string> targetMuscles, int? weeklyWorkoutTarget)
    {
        var normalizedWorkoutType = NormalizeText(workoutType);
        if (!string.IsNullOrWhiteSpace(normalizedWorkoutType))
        {
            if (normalizedWorkoutType.Contains("upper", StringComparison.Ordinal))
            {
                return "upper";
            }

            if (normalizedWorkoutType.Contains("lower", StringComparison.Ordinal)
                || normalizedWorkoutType.Contains("leg", StringComparison.Ordinal))
            {
                return "lower";
            }

            if (normalizedWorkoutType.Contains("push", StringComparison.Ordinal))
            {
                return "push";
            }

            if (normalizedWorkoutType.Contains("pull", StringComparison.Ordinal))
            {
                return "pull";
            }

            if (normalizedWorkoutType.Contains("core", StringComparison.Ordinal)
                || normalizedWorkoutType.Contains("abs", StringComparison.Ordinal))
            {
                return "core";
            }

            if (normalizedWorkoutType.Contains("full", StringComparison.Ordinal))
            {
                return "full-body";
            }
        }

        if (targetMuscles.Count > 0)
        {
            var focusGroups = targetMuscles
                .Select(ResolveFocusGroupForTargetMuscle)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (focusGroups.Count == 1)
            {
                return focusGroups[0] switch
                {
                    "push" => "push",
                    "pull" => "pull",
                    "lower" => "lower",
                    "core" => "core",
                    _ => "full-body",
                };
            }
        }

        return weeklyWorkoutTarget.HasValue && weeklyWorkoutTarget.Value >= 5 ? "upper" : "full-body";
    }

    private static string NormalizeFitnessLevel(string? fitnessLevel, int recentWorkoutCount)
    {
        var normalizedLevel = NormalizeText(fitnessLevel);
        if (!string.IsNullOrWhiteSpace(normalizedLevel))
        {
            if (normalizedLevel.Contains("beginner", StringComparison.Ordinal))
            {
                return "beginner";
            }

            if (normalizedLevel.Contains("advanced", StringComparison.Ordinal))
            {
                return "advanced";
            }

            if (normalizedLevel.Contains("intermediate", StringComparison.Ordinal))
            {
                return "intermediate";
            }
        }

        return recentWorkoutCount switch
        {
            < 12 => "beginner",
            >= 60 => "advanced",
            _ => "intermediate",
        };
    }

    private static int GetDefaultDurationMinutes(string fitnessLevel)
    {
        return fitnessLevel switch
        {
            "beginner" => 40,
            "advanced" => 60,
            _ => 50,
        };
    }

    private static int GetSetTarget(string fitnessLevel)
    {
        return fitnessLevel switch
        {
            "beginner" => 3,
            "advanced" => 5,
            _ => 4,
        };
    }

    private static string GetRepTarget(string goal)
    {
        return goal switch
        {
            "strength" => "5-8 reps",
            "muscle gain" => "8-12 reps",
            "endurance" => "12-20 reps",
            _ => "10-15 reps",
        };
    }

    private static int GetRestSeconds(string goal)
    {
        return goal switch
        {
            "strength" => 120,
            "muscle gain" => 90,
            _ => 60,
        };
    }

    private static string GetMainSectionName(string workoutType)
    {
        return workoutType switch
        {
            "upper" => "Upper-body block",
            "lower" => "Lower-body block",
            "push" => "Push block",
            "pull" => "Pull block",
            "core" => "Core block",
            _ => "Main workout",
        };
    }

    private static string ResolveCategory(string name, string? equipment, IReadOnlyCollection<string> muscles)
    {
        var normalizedName = NormalizeText(name);
        var normalizedEquipment = NormalizeText(equipment);

        if (CoreKeywords.Any(keyword => normalizedName.Contains(keyword, StringComparison.Ordinal)))
        {
            return "core";
        }

        if (normalizedName.Contains("stretch", StringComparison.Ordinal)
            || normalizedName.Contains("mobility", StringComparison.Ordinal))
        {
            return "mobility";
        }

        if (CompoundKeywords.Any(keyword => normalizedName.Contains(keyword, StringComparison.Ordinal)))
        {
            return "compound";
        }

        if (IsolationKeywords.Any(keyword => normalizedName.Contains(keyword, StringComparison.Ordinal)))
        {
            return "isolation";
        }

        if (normalizedEquipment == "bodyweight")
        {
            return muscles.Any(muscle => CoreMuscles.Contains(muscle)) ? "core" : "bodyweight";
        }

        return muscles.Any(muscle => CoreMuscles.Contains(muscle)) ? "core" : "compound";
    }

    private static string ResolveFocusGroup(string name, IReadOnlyCollection<string> muscles)
    {
        var normalizedName = NormalizeText(name);

        if (CoreKeywords.Any(keyword => normalizedName.Contains(keyword, StringComparison.Ordinal))
            || muscles.Any(muscle => CoreMuscles.Contains(muscle)))
        {
            return "core";
        }

        if (normalizedName.Contains("squat", StringComparison.Ordinal)
            || normalizedName.Contains("deadlift", StringComparison.Ordinal)
            || normalizedName.Contains("lunge", StringComparison.Ordinal)
            || normalizedName.Contains("hinge", StringComparison.Ordinal)
            || muscles.Any(muscle => LowerMuscles.Contains(muscle)))
        {
            return "lower";
        }

        if (normalizedName.Contains("row", StringComparison.Ordinal)
            || normalizedName.Contains("pull", StringComparison.Ordinal)
            || normalizedName.Contains("curl", StringComparison.Ordinal)
            || muscles.Any(muscle => PullMuscles.Contains(muscle)))
        {
            return "pull";
        }

        if (normalizedName.Contains("press", StringComparison.Ordinal)
            || normalizedName.Contains("push", StringComparison.Ordinal)
            || normalizedName.Contains("dip", StringComparison.Ordinal)
            || muscles.Any(muscle => PushMuscles.Contains(muscle)))
        {
            return "push";
        }

        return "full-body";
    }

    private static string ResolveFocusGroupForTargetMuscle(string targetMuscle)
    {
        return PushMuscles.Contains(targetMuscle) ? "push"
            : PullMuscles.Contains(targetMuscle) ? "pull"
            : LowerMuscles.Contains(targetMuscle) ? "lower"
            : CoreMuscles.Contains(targetMuscle) ? "core"
            : "full-body";
    }

    private static string BuildFallbackInstruction(string focusGroup, string category)
    {
        if (focusGroup == "core" || category == "core")
        {
            return "Move slowly, keep the ribcage stacked over the pelvis, and stop the set as soon as you lose trunk control.";
        }

        if (focusGroup == "lower")
        {
            return "Own the full range you can control, keep your feet planted, and maintain a steady torso instead of chasing speed.";
        }

        if (focusGroup == "push")
        {
            return "Set the shoulders first, keep the ribcage down, and finish each rep only as fast as you can stay technically clean.";
        }

        if (focusGroup == "pull")
        {
            return "Initiate by moving the shoulder blades, then finish with the elbows without throwing momentum into the top of the rep.";
        }

        return "Use a controlled tempo, stop each set with clean reps in reserve, and prioritize repeatable technique over fatigue chasing.";
    }

    private static string GetEffectiveName(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalNameOverride) ? item.Name.Trim() : item.LocalNameOverride.Trim();
    }

    private static string? GetEffectiveInstructions(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalInstructionsOverride)
            ? NormalizeOptionalText(item.Instructions)
            : item.LocalInstructionsOverride.Trim();
    }

    private static string? GetEffectiveThumbnailUrl(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride)
            ? NormalizeOptionalText(item.ThumbnailUrl)
            : item.LocalThumbnailUrlOverride.Trim();
    }

    private static string? GetEffectiveVideoUrl(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride)
            ? NormalizeOptionalText(item.VideoUrl)
            : item.LocalVideoUrlOverride.Trim();
    }

    private static string NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray());

        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeMuscle(string? value)
    {
        var normalized = NormalizeText(value);
        return normalized switch
        {
            "quad" => "quads",
            "quadricep" => "quadriceps",
            "glute" => "glutes",
            "abdominals" => "abs",
            "trapezius" => "traps",
            "latissimus" => "lats",
            _ => normalized,
        };
    }

    private static List<string> NormalizeDistinctList(IEnumerable<string>? values)
    {
        return values?
            .Select(NormalizeText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? [];
    }

    private static string FormatLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ',
            value.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Length == 0 ? string.Empty : char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private sealed class WorkoutGenerationContext
    {
        public string Goal { get; init; } = "general fitness";
        public string WorkoutType { get; init; } = "full-body";
        public string FitnessLevel { get; init; } = "intermediate";
        public int DurationMinutes { get; init; } = 45;
        public IReadOnlyList<string> TargetMuscles { get; init; } = [];
        public HashSet<string> ExcludedExercises { get; init; } = new(StringComparer.Ordinal);
        public bool IncludeWarmup { get; init; }
        public bool IncludeCooldown { get; init; }
        public int SetTarget { get; init; }
        public string RepTarget { get; init; } = "8-12 reps";
        public int RestSeconds { get; init; }
    }

    private sealed class CatalogExerciseCandidate
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string NameNormalized { get; init; } = string.Empty;
        public string? Instructions { get; init; }
        public string? PrimaryMuscle { get; init; }
        public string? PrimaryMuscleNormalized { get; init; }
        public IReadOnlyList<string> Muscles { get; init; } = [];
        public string? Equipment { get; init; }
        public string? Difficulty { get; init; }
        public string? ThumbnailUrl { get; init; }
        public string? VideoUrl { get; init; }
        public string FocusGroup { get; init; } = "full-body";
        public string Category { get; init; } = "compound";
        public bool IsBodyweight { get; init; }
        public bool IsAdvanced { get; init; }
    }

    private sealed class WorkoutSlot
    {
        public string FocusGroup { get; init; } = "full-body";
        public string? TargetMuscle { get; init; }
        public IReadOnlyList<string> PreferredCategories { get; init; } = [];
    }
}
