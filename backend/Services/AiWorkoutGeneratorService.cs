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

    private static readonly HashSet<string> ExerciseFamilyStopWords = new(StringComparer.Ordinal)
    {
        "barbell",
        "dumbbell",
        "cable",
        "machine",
        "smith",
        "bodyweight",
        "band",
        "trx",
        "standing",
        "seated",
        "single",
        "one",
        "arm",
        "alternating",
        "lever",
        "plate",
        "weighted",
        "assisted",
        "reverse",
        "incline",
        "decline",
        "mp",
    };

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
        var goals = await _dbContext.GoalSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(goal => goal.UserId == parsedUserId, cancellationToken);

        var recentWorkouts = await _dbContext.Workouts
            .AsNoTracking()
            .Where(workout => workout.UserId == parsedUserId)
            .Include(workout => workout.ExerciseEntries)
            .ThenInclude(entry => entry.Sets)
            .OrderByDescending(workout => workout.Date)
            .ThenByDescending(workout => workout.Id)
            .Take(12)
            .ToListAsync(cancellationToken);

        var catalogItems = await _dbContext.ExerciseCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var context = BuildContext(request, goals, recentWorkouts.Count);
        var random = Random.Shared;
        var variation = BuildVariationProfile(context, random);
        var recentExerciseCounts = BuildRecentExerciseCounts(recentWorkouts.Take(2));
        var recentExerciseWeights = BuildRecentExerciseWeightLookup(recentWorkouts);
        var recentExercisePenalties = BuildRecentExercisePenaltyLookup(recentWorkouts.Take(6));
        var recentFamilyPenalties = BuildRecentExerciseFamilyPenaltyLookup(recentWorkouts.Take(6));
        var recentPatternPenalties = BuildRecentMovementPatternPenaltyLookup(recentWorkouts.Take(6));
        var catalogCandidates = BuildCatalogCandidates(catalogItems, context.ExcludedExercises);
        var mainWorkout = BuildMainWorkoutExercises(
            context,
            catalogCandidates,
            recentExerciseCounts,
            recentExerciseWeights,
            recentExercisePenalties,
            recentFamilyPenalties,
            recentPatternPenalties,
            variation,
            random);

        var sections = new List<AiWorkoutSectionDto>();

        if (context.IncludeWarmup)
        {
            sections.Add(new AiWorkoutSectionDto
            {
                Name = "Warm-up",
                Exercises = BuildWarmupExercises(context),
            });
        }

        sections.Add(new AiWorkoutSectionDto
        {
            Name = GetMainSectionName(context.WorkoutType),
            Exercises = mainWorkout.Exercises,
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
            Notes = BuildPlanNotes(context, mainWorkout, recentWorkouts.Count, variation),
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
            FamilyKey = BuildExerciseFamilyKey(name),
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
            MovementPattern = ResolveMovementPattern(name, muscles, focusGroup, category),
            IsBodyweight = string.Equals(NormalizeOptionalText(item.Equipment), "bodyweight", StringComparison.OrdinalIgnoreCase)
                           || category == "bodyweight",
            IsAdvanced = string.Equals(NormalizeOptionalText(item.Difficulty), "advanced", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static GeneratedMainWorkoutResult BuildMainWorkoutExercises(
        WorkoutGenerationContext context,
        IReadOnlyList<CatalogExerciseCandidate> catalogCandidates,
        IReadOnlyDictionary<string, int> recentExerciseCounts,
        IReadOnlyDictionary<string, decimal> recentExerciseWeights,
        IReadOnlyDictionary<string, int> recentExercisePenalties,
        IReadOnlyDictionary<string, int> recentFamilyPenalties,
        IReadOnlyDictionary<string, int> recentPatternPenalties,
        GenerationVariationProfile variation,
        Random random)
    {
        var slots = BuildMainSlots(context, random);
        var selectedExercises = new List<AiWorkoutExerciseDto>(slots.Count);
        var usedCatalogIds = new HashSet<int>();
        var usedExerciseNames = new HashSet<string>(StringComparer.Ordinal);
        var usedFamilyKeys = new HashSet<string>(StringComparer.Ordinal);
        var usedMovementPatterns = new Dictionary<string, int>(StringComparer.Ordinal);
        var summary = new WorkoutSelectionSummary();

        foreach (var slot in slots)
        {
            var selection = SelectCandidate(
                catalogCandidates,
                slot,
                context,
                recentExerciseCounts,
                recentExercisePenalties,
                recentFamilyPenalties,
                recentPatternPenalties,
                usedCatalogIds,
                usedExerciseNames,
                usedFamilyKeys,
                usedMovementPatterns,
                allowRecent: false);

            selection ??= SelectCandidate(
                catalogCandidates,
                slot,
                context,
                recentExerciseCounts,
                recentExercisePenalties,
                recentFamilyPenalties,
                recentPatternPenalties,
                usedCatalogIds,
                usedExerciseNames,
                usedFamilyKeys,
                usedMovementPatterns,
                allowRecent: true);

            if (selection?.Candidate is not null)
            {
                var candidate = selection.Candidate;
                usedCatalogIds.Add(candidate.Id);
                usedExerciseNames.Add(candidate.NameNormalized);
                usedFamilyKeys.Add(candidate.FamilyKey);
                if (!string.IsNullOrWhiteSpace(candidate.MovementPattern))
                {
                    usedMovementPatterns[candidate.MovementPattern] =
                        usedMovementPatterns.TryGetValue(candidate.MovementPattern, out var usedCount) ? usedCount + 1 : 1;
                    summary.RecordMovementPattern(candidate.MovementPattern);
                }

                summary.CatalogExerciseCount++;
                if (selection.AvoidedRecentAlternative)
                {
                    summary.RotatedAwayFromRecentRepeatsCount++;
                }

                if (!string.IsNullOrWhiteSpace(candidate.ThumbnailUrl) || !string.IsNullOrWhiteSpace(candidate.VideoUrl))
                {
                    summary.MediaBackedExerciseCount++;
                }

                selectedExercises.Add(MapSelectedCatalogExercise(candidate, slot, context, recentExerciseWeights, variation));
                continue;
            }

            var fallbackExercise = BuildFallbackExercise(slot, context, variation);
            usedExerciseNames.Add(NormalizeText(fallbackExercise.Name));
            summary.FallbackExerciseCount++;
            var fallbackPattern = ResolveMovementPattern(
                fallbackExercise.Name,
                [],
                slot.FocusGroup,
                NormalizeText(fallbackExercise.Category));
            usedMovementPatterns[fallbackPattern] =
                usedMovementPatterns.TryGetValue(fallbackPattern, out var fallbackPatternCount) ? fallbackPatternCount + 1 : 1;
            summary.RecordMovementPattern(fallbackPattern);
            selectedExercises.Add(fallbackExercise);
        }

        return new GeneratedMainWorkoutResult
        {
            Exercises = selectedExercises,
            Summary = summary,
        };
    }

    private static CandidateSelectionResult? SelectCandidate(
        IReadOnlyList<CatalogExerciseCandidate> candidates,
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        IReadOnlyDictionary<string, int> recentExerciseCounts,
        IReadOnlyDictionary<string, int> recentExercisePenalties,
        IReadOnlyDictionary<string, int> recentFamilyPenalties,
        IReadOnlyDictionary<string, int> recentPatternPenalties,
        IReadOnlySet<int> usedCatalogIds,
        IReadOnlySet<string> usedExerciseNames,
        IReadOnlySet<string> usedFamilyKeys,
        IReadOnlyDictionary<string, int> usedMovementPatterns,
        bool allowRecent)
    {
        var scoredCandidates = new List<ScoredCandidate>();

        foreach (var candidate in candidates)
        {
            if (usedCatalogIds.Contains(candidate.Id) || usedExerciseNames.Contains(candidate.NameNormalized))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(candidate.FamilyKey) && usedFamilyKeys.Contains(candidate.FamilyKey))
            {
                continue;
            }

            var recentCount = recentExerciseCounts.TryGetValue(candidate.NameNormalized, out var value) ? value : 0;
            if (!allowRecent && recentCount > 0)
            {
                continue;
            }

            var score = ScoreCandidate(
                candidate,
                slot,
                context,
                recentCount,
                recentExercisePenalties,
                recentFamilyPenalties,
                recentPatternPenalties,
                usedMovementPatterns,
                Random.Shared);

            if (score >= 14)
            {
                scoredCandidates.Add(new ScoredCandidate
                {
                    Candidate = candidate,
                    Score = score,
                    RecentCount = recentCount,
                });
            }
        }

        if (scoredCandidates.Count == 0)
        {
            return null;
        }

        scoredCandidates.Sort((left, right) => right.Score.CompareTo(left.Score));
        var bestScore = scoredCandidates[0].Score;
        var selectionPool = scoredCandidates
            .Where(entry => entry.Score >= bestScore - 6)
            .Take(5)
            .ToList();

        var totalWeight = selectionPool.Sum(entry => Math.Max(1, entry.Score - bestScore + 8));
        var pick = Random.Shared.Next(totalWeight);
        var runningWeight = 0;

        foreach (var option in selectionPool)
        {
            runningWeight += Math.Max(1, option.Score - bestScore + 8);
            if (pick < runningWeight)
            {
                return new CandidateSelectionResult
                {
                    Candidate = option.Candidate,
                    AvoidedRecentAlternative = option.RecentCount == 0
                        && scoredCandidates.Any(entry =>
                            entry.RecentCount > 0
                            && entry.Score >= bestScore - 4),
                };
            }
        }

        var chosen = selectionPool[0];
        return new CandidateSelectionResult
        {
            Candidate = chosen.Candidate,
            AvoidedRecentAlternative = chosen.RecentCount == 0
                && scoredCandidates.Any(entry => entry.RecentCount > 0 && entry.Score >= bestScore - 4),
        };
    }

    private static int ScoreCandidate(
        CatalogExerciseCandidate candidate,
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        int recentCount,
        IReadOnlyDictionary<string, int> recentExercisePenalties,
        IReadOnlyDictionary<string, int> recentFamilyPenalties,
        IReadOnlyDictionary<string, int> recentPatternPenalties,
        IReadOnlyDictionary<string, int> usedMovementPatterns,
        Random random)
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

        var movementPatternScore = ScoreMovementPattern(candidate.MovementPattern, slot.PreferredPatterns);
        score += movementPatternScore;

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

        if (recentExercisePenalties.TryGetValue(candidate.NameNormalized, out var recentExercisePenalty))
        {
            score -= recentExercisePenalty * 2;
        }

        if (!string.IsNullOrWhiteSpace(candidate.FamilyKey)
            && recentFamilyPenalties.TryGetValue(candidate.FamilyKey, out var familyPenalty))
        {
            score -= familyPenalty * 2;
        }

        if (!string.IsNullOrWhiteSpace(candidate.MovementPattern)
            && recentPatternPenalties.TryGetValue(candidate.MovementPattern, out var patternPenalty))
        {
            score -= patternPenalty;
        }

        if (!string.IsNullOrWhiteSpace(candidate.MovementPattern)
            && usedMovementPatterns.TryGetValue(candidate.MovementPattern, out var sameWorkoutPatternCount))
        {
            score -= 16 * sameWorkoutPatternCount;
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

        score += random.Next(-2, 3);
        return score;
    }

    private static AiWorkoutExerciseDto MapSelectedCatalogExercise(
        CatalogExerciseCandidate candidate,
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        IReadOnlyDictionary<string, decimal> recentExerciseWeights,
        GenerationVariationProfile variation)
    {
        return new AiWorkoutExerciseDto
        {
            ExerciseCatalogItemId = candidate.Id,
            Name = candidate.Name,
            Category = FormatLabel(candidate.Category),
            TargetMuscle = candidate.PrimaryMuscle ?? FormatLabel(slot.TargetMuscle ?? candidate.FocusGroup),
            Sets = GetSetCountForSlot(slot, context, variation),
            Reps = GetRepTargetForSlot(slot, context, variation),
            SuggestedWeight = BuildSuggestedWeight(candidate, context, recentExerciseWeights),
            RestSeconds = GetRestSecondsForSlot(slot, context, variation),
            Instructions = string.IsNullOrWhiteSpace(candidate.Instructions)
                ? BuildFallbackInstruction(candidate.FocusGroup, candidate.Category)
                : candidate.Instructions!,
            ThumbnailUrl = candidate.ThumbnailUrl,
            VideoUrl = candidate.VideoUrl,
        };
    }

    private static List<WorkoutSlot> BuildMainSlots(WorkoutGenerationContext context, Random random)
    {
        var slotCount = GetMainExerciseCount(context.WorkoutType, context.DurationMinutes);
        if (context.TargetMuscles.Count > 0)
        {
            var slots = new List<WorkoutSlot>(slotCount);
            for (var index = 0; index < slotCount; index++)
            {
                var targetMuscle = context.TargetMuscles[index % context.TargetMuscles.Count];
                var compoundBias = index < Math.Min(2, slotCount);
                slots.Add(BuildSlot(
                    ResolveFocusGroupForTargetMuscle(targetMuscle),
                    compoundBias ? ["compound", "bodyweight"] : ["compound", "isolation", "bodyweight", "core"],
                    GetPreferredPatternsForTargetMuscle(targetMuscle, compoundBias, random),
                    targetMuscle));
            }

            return slots;
        }

        List<WorkoutSlot> baseSlots = context.WorkoutType switch
        {
            "upper" => Shuffle(new[]
                {
                    BuildSlot("push", ["compound", "bodyweight"], DistinctShuffled(["horizontal press", "vertical press", "angled press"], random, 3)),
                    BuildSlot("pull", ["compound", "bodyweight"], DistinctShuffled(["horizontal pull", "vertical pull"], random, 2)),
                }, random)
                .Concat(Shuffle(new[]
                {
                    BuildSlot("push", ["isolation", "bodyweight"], DistinctShuffled(["lateral raise", "triceps extension", "chest fly", "push bodyweight"], random, 3)),
                    BuildSlot("pull", ["isolation", "bodyweight"], DistinctShuffled(["curl", "rear delt", "horizontal pull"], random, 3)),
                    BuildSlot("upper", ["compound", "isolation", "bodyweight"], DistinctShuffled(["vertical press", "horizontal pull", "curl", "lateral raise"], random, 3)),
                }, random))
                .Concat(new[] { BuildSlot("core", "core", "bodyweight") })
                .ToList(),
            "lower" or "legs" => Shuffle(new[]
                {
                    BuildSlot("lower", ["compound", "bodyweight"], DistinctShuffled(["squat", "hinge", "lunge"], random, 3)),
                    BuildSlot("lower", ["compound", "bodyweight"], DistinctShuffled(["hinge", "squat", "hip thrust"], random, 3)),
                }, random)
                .Concat(Shuffle(new[]
                {
                    BuildSlot("lower", ["isolation", "bodyweight"], DistinctShuffled(["lunge", "hamstring curl", "hip thrust", "knee extension"], random, 3)),
                    BuildSlot("lower", ["isolation", "bodyweight"], DistinctShuffled(["calf raise", "hamstring curl", "knee extension", "lunge"], random, 3)),
                    BuildSlot("full-body", ["compound", "bodyweight"], DistinctShuffled(["lunge", "squat", "hinge"], random, 3)),
                }, random))
                .Concat(new[] { BuildSlot("core", "core", "bodyweight") })
                .ToList(),
            "push" => Shuffle(new[]
                {
                    BuildSlot("push", ["compound", "bodyweight"], DistinctShuffled(["horizontal press", "angled press", "push bodyweight"], random, 3)),
                    BuildSlot("push", ["compound", "bodyweight"], DistinctShuffled(["vertical press", "horizontal press", "dip"], random, 3)),
                }, random)
                .Concat(Shuffle(new[]
                {
                    BuildSlot("push", ["isolation", "bodyweight"], DistinctShuffled(["lateral raise", "triceps extension", "chest fly"], random, 3)),
                    BuildSlot("push", ["isolation", "bodyweight"], DistinctShuffled(["triceps extension", "lateral raise", "push bodyweight"], random, 3)),
                }, random))
                .Concat(new[] { BuildSlot("core", "core", "bodyweight") })
                .ToList(),
            "pull" => Shuffle(new[]
                {
                    BuildSlot("pull", ["compound", "bodyweight"], DistinctShuffled(["horizontal pull", "vertical pull"], random, 2)),
                    BuildSlot("pull", ["compound", "bodyweight"], DistinctShuffled(["vertical pull", "horizontal pull"], random, 2)),
                }, random)
                .Concat(Shuffle(new[]
                {
                    BuildSlot("pull", ["isolation", "bodyweight"], DistinctShuffled(["curl", "rear delt", "shrug"], random, 3)),
                    BuildSlot("pull", ["isolation", "bodyweight"], DistinctShuffled(["rear delt", "curl", "vertical pull"], random, 3)),
                }, random))
                .Concat(new[] { BuildSlot("core", "core", "bodyweight") })
                .ToList(),
            "core" => Shuffle(new[]
            {
                BuildSlot("core", ["core", "bodyweight"], DistinctShuffled(["plank", "anti rotation", "core flexion"], random, 3)),
                BuildSlot("core", ["core", "bodyweight"], DistinctShuffled(["core flexion", "rotation", "plank"], random, 3)),
                BuildSlot("lower", ["bodyweight", "compound"], DistinctShuffled(["lunge", "squat", "hinge"], random, 3)),
                BuildSlot("full-body", ["bodyweight", "compound"], DistinctShuffled(["push bodyweight", "horizontal pull", "lunge"], random, 3)),
                BuildSlot("core", ["core", "bodyweight"], DistinctShuffled(["rotation", "plank", "anti rotation"], random, 3)),
            }, random).ToList(),
            _ => Shuffle(new[]
                {
                    BuildSlot("lower", ["compound", "bodyweight"], DistinctShuffled(["squat", "hinge", "lunge"], random, 3)),
                    BuildSlot("push", ["compound", "bodyweight"], DistinctShuffled(["horizontal press", "vertical press", "angled press"], random, 3)),
                    BuildSlot("pull", ["compound", "bodyweight"], DistinctShuffled(["horizontal pull", "vertical pull"], random, 2)),
                }, random)
                .Concat(Shuffle(new[]
                {
                    BuildSlot("lower", ["isolation", "bodyweight"], DistinctShuffled(["lunge", "hamstring curl", "calf raise"], random, 3)),
                    BuildSlot("push", ["isolation", "bodyweight"], DistinctShuffled(["lateral raise", "triceps extension", "chest fly"], random, 3)),
                    BuildSlot("pull", ["isolation", "bodyweight"], DistinctShuffled(["curl", "rear delt", "shrug"], random, 3)),
                }, random))
                .Concat(new[] { BuildSlot("core", "core", "bodyweight") })
                .ToList(),
        };

        return baseSlots.Take(slotCount).ToList();
    }

    private static WorkoutSlot BuildSlot(
        string focusGroup,
        IReadOnlyList<string> preferredCategories,
        IReadOnlyList<string>? preferredPatterns = null,
        string? targetMuscle = null)
    {
        return new WorkoutSlot
        {
            FocusGroup = focusGroup,
            TargetMuscle = targetMuscle,
            PreferredCategories = preferredCategories.ToList(),
            PreferredPatterns = preferredPatterns?.ToList() ?? [],
        };
    }

    private static WorkoutSlot BuildSlot(string focusGroup, params string[] preferredCategories)
    {
        return BuildSlot(focusGroup, preferredCategories, null, null);
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

    private static AiWorkoutExerciseDto BuildFallbackExercise(
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        GenerationVariationProfile variation)
    {
        return slot.FocusGroup switch
        {
            "push" => new AiWorkoutExerciseDto
            {
                Name = "Push-up",
                Category = "Bodyweight",
                TargetMuscle = "Chest",
                Sets = GetSetCountForSlot(slot, context, variation),
                Reps = GetRepTargetForSlot(slot, context, variation),
                SuggestedWeight = "Bodyweight only.",
                RestSeconds = GetRestSecondsForSlot(slot, context, variation),
                Instructions = "Keep your ribs down, move through a full range, and stop 1-2 reps before form breaks.",
            },
            "pull" => new AiWorkoutExerciseDto
            {
                Name = "Band row or inverted row",
                Category = "Bodyweight",
                TargetMuscle = "Back",
                Sets = GetSetCountForSlot(slot, context, variation),
                Reps = GetRepTargetForSlot(slot, context, variation),
                SuggestedWeight = "Use a band or setup that leaves 1-2 reps in reserve.",
                RestSeconds = GetRestSecondsForSlot(slot, context, variation),
                Instructions = "Keep the ribcage stacked, pull elbows back, and pause briefly when the shoulder blades retract.",
            },
            "lower" => new AiWorkoutExerciseDto
            {
                Name = "Goblet squat or split squat",
                Category = "Compound",
                TargetMuscle = "Quadriceps",
                Sets = GetSetCountForSlot(slot, context, variation),
                Reps = GetRepTargetForSlot(slot, context, variation),
                SuggestedWeight = "Choose a controlled load that leaves 1-2 reps in reserve.",
                RestSeconds = GetRestSecondsForSlot(slot, context, variation),
                Instructions = "Move with full depth you can control, keep the feet planted, and maintain a steady torso position.",
            },
            "core" => new AiWorkoutExerciseDto
            {
                Name = "Front plank",
                Category = "Core",
                TargetMuscle = "Core",
                Sets = GetSetCountForSlot(slot, context, variation),
                Reps = context.Goal == "strength" ? "20-30 sec" : "30-45 sec",
                SuggestedWeight = "Bodyweight only.",
                RestSeconds = Math.Min(60, GetRestSecondsForSlot(slot, context, variation)),
                Instructions = "Brace the trunk, squeeze glutes lightly, and hold a straight line without letting the low back sag.",
            },
            _ => new AiWorkoutExerciseDto
            {
                Name = "Walking lunge",
                Category = "Bodyweight",
                TargetMuscle = "Lower body",
                Sets = GetSetCountForSlot(slot, context, variation),
                Reps = GetRepTargetForSlot(slot, context, variation),
                SuggestedWeight = "Bodyweight or light dumbbells.",
                RestSeconds = GetRestSecondsForSlot(slot, context, variation),
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

    private static IReadOnlyDictionary<string, int> BuildRecentExercisePenaltyLookup(IEnumerable<Workout> workouts)
    {
        return BuildWeightedRecentLookup(workouts, entry => NormalizeText(entry.ExerciseName));
    }

    private static IReadOnlyDictionary<string, int> BuildRecentExerciseFamilyPenaltyLookup(IEnumerable<Workout> workouts)
    {
        return BuildWeightedRecentLookup(workouts, entry => BuildExerciseFamilyKey(entry.ExerciseName));
    }

    private static IReadOnlyDictionary<string, int> BuildRecentMovementPatternPenaltyLookup(IEnumerable<Workout> workouts)
    {
        return BuildWeightedRecentLookup(workouts, entry => ResolveMovementPattern(
            entry.ExerciseName,
            [],
            ResolveFocusGroup(entry.ExerciseName, []),
            ResolveCategory(entry.ExerciseName, null, [])));
    }

    private static IReadOnlyDictionary<string, int> BuildWeightedRecentLookup(
        IEnumerable<Workout> workouts,
        Func<ExerciseEntry, string> keySelector)
    {
        var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
        var recentWorkoutList = workouts.ToList();

        for (var workoutIndex = 0; workoutIndex < recentWorkoutList.Count; workoutIndex++)
        {
            var weight = Math.Max(1, 7 - workoutIndex);
            foreach (var entry in recentWorkoutList[workoutIndex].ExerciseEntries)
            {
                var key = keySelector(entry);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                lookup[key] = lookup.TryGetValue(key, out var current) ? current + weight : weight;
            }
        }

        return lookup;
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

    private static int GetSetCountForSlot(
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        GenerationVariationProfile variation)
    {
        var baseSetTarget = slot.FocusGroup == "core"
            ? Math.Max(2, context.SetTarget - 1)
            : slot.PreferredCategories.Contains("compound", StringComparer.Ordinal)
                ? context.SetTarget
                : Math.Max(2, context.SetTarget - 1);

        var adjustment = slot.PreferredCategories.Contains("compound", StringComparer.Ordinal)
            ? variation.CompoundSetAdjustment
            : variation.AccessorySetAdjustment;
        var minSets = slot.FocusGroup == "core"
            ? 2
            : context.FitnessLevel switch
            {
                "beginner" => 2,
                "advanced" => 4,
                _ => 3,
            };
        var maxSets = slot.FocusGroup == "core"
            ? 4
            : context.FitnessLevel switch
            {
                "beginner" => 3,
                "advanced" => 5,
                _ => 4,
            };

        if (slot.FocusGroup == "core" && context.Goal == "endurance")
        {
            maxSets = Math.Max(maxSets, 4);
        }

        return Math.Clamp(baseSetTarget + adjustment, minSets, maxSets);
    }

    private static string GetRepTargetForSlot(
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        GenerationVariationProfile variation)
    {
        if (slot.FocusGroup == "core")
        {
            return context.Goal == "strength"
                ? "8-12 controlled reps"
                : variation.RepStyle switch
                {
                    RepVariationStyle.Lower => "10-12 reps",
                    RepVariationStyle.Higher => "12-15 reps",
                    _ => "10-15 reps",
                };
        }

        var compoundBias = slot.PreferredCategories.Contains("compound", StringComparer.Ordinal);
        return context.Goal switch
        {
            "strength" => compoundBias
                ? variation.RepStyle switch
                {
                    RepVariationStyle.Lower => "5-6 reps",
                    RepVariationStyle.Higher => "6-8 reps",
                    _ => "5-8 reps",
                }
                : variation.RepStyle switch
                {
                    RepVariationStyle.Lower => "6-8 reps",
                    RepVariationStyle.Higher => "8-10 reps",
                    _ => "8-10 reps",
                },
            "muscle gain" => compoundBias
                ? variation.RepStyle switch
                {
                    RepVariationStyle.Lower => "6-8 reps",
                    RepVariationStyle.Higher => "10-12 reps",
                    _ => "8-10 reps",
                }
                : variation.RepStyle switch
                {
                    RepVariationStyle.Lower => "8-10 reps",
                    RepVariationStyle.Higher => "12-15 reps",
                    _ => "10-12 reps",
                },
            "endurance" => variation.RepStyle switch
            {
                RepVariationStyle.Lower => "12-15 reps",
                RepVariationStyle.Higher => "15-20 reps",
                _ => "12-18 reps",
            },
            _ => variation.RepStyle switch
            {
                RepVariationStyle.Lower => "8-10 reps",
                RepVariationStyle.Higher => "12-15 reps",
                _ => "10-12 reps",
            },
        };
    }

    private static int GetRestSecondsForSlot(
        WorkoutSlot slot,
        WorkoutGenerationContext context,
        GenerationVariationProfile variation)
    {
        var baseRestSeconds = context.RestSeconds;
        if (slot.FocusGroup == "core")
        {
            return Math.Clamp(Math.Min(baseRestSeconds, 60) + variation.RestAdjustment, 30, 60);
        }

        if (context.Goal == "strength" && slot.PreferredCategories.Contains("compound", StringComparer.Ordinal))
        {
            return Math.Clamp(Math.Max(baseRestSeconds, 120) + variation.RestAdjustment, 90, 150);
        }

        return Math.Clamp(baseRestSeconds + variation.RestAdjustment, 45, 120);
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
        GeneratedMainWorkoutResult mainWorkout,
        int recentWorkoutCount,
        GenerationVariationProfile variation)
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

        if (mainWorkout.Exercises.Any(exercise => exercise.ExerciseCatalogItemId is null))
        {
            notes.Add("Some main-session movements fell back to safe generic exercises because the local catalog did not have a strong enough match.");
        }

        if (context.TargetMuscles.Count > 0)
        {
            notes.Add($"Target muscles were prioritized in this order: {string.Join(", ", context.TargetMuscles.Select(FormatLabel))}.");
        }

        if (recentWorkoutCount >= 2)
        {
            notes.Add("Exercises from your last two workouts were deprioritized first, then similar movement families from the broader recent history were penalized to reduce repetition.");
        }

        if (mainWorkout.Summary.RotatedAwayFromRecentRepeatsCount > 0)
        {
            notes.Add($"This version actively rotated away from {mainWorkout.Summary.RotatedAwayFromRecentRepeatsCount} recently repeated slot candidate(s) where a strong alternative was available.");
        }

        if (mainWorkout.Summary.HighlightPatterns.Count > 0)
        {
            notes.Add($"This pass emphasized {string.Join(", ", mainWorkout.Summary.HighlightPatterns.Select(FormatLabel))} while keeping the split balanced.");
        }

        notes.Add(variation.RepStyle switch
        {
            RepVariationStyle.Lower => "Rep targets leaned slightly heavier inside the safe range to keep the session from repeating the exact same feel.",
            RepVariationStyle.Higher => "Rep targets leaned slightly higher inside the safe range to add variation without changing the training goal.",
            _ => "Rep targets stayed near the middle of the safe range, with small order and rest changes to keep repeated generations from looking identical.",
        });

        if (mainWorkout.Summary.MediaBackedExerciseCount > 0)
        {
            notes.Add("When scores were close, catalog exercises with stored media were slightly preferred so the plan stays easier to preview and coach from.");
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

    private static GenerationVariationProfile BuildVariationProfile(
        WorkoutGenerationContext context,
        Random random)
    {
        var compoundSetAdjustment = random.Next(2) == 0 ? 0 : -1;
        var accessorySetAdjustment = random.Next(3) switch
        {
            0 => -1,
            _ => 0,
        };
        var repStyle = (RepVariationStyle)random.Next(0, 3);
        var restAdjustment = context.Goal switch
        {
            "strength" => random.Next(2) == 0 ? 0 : 15,
            "muscle gain" => random.Next(3) switch
            {
                0 => -15,
                1 => 0,
                _ => 15,
            },
            _ => random.Next(2) == 0 ? -15 : 0,
        };

        return new GenerationVariationProfile
        {
            CompoundSetAdjustment = compoundSetAdjustment,
            AccessorySetAdjustment = accessorySetAdjustment,
            RepStyle = repStyle,
            RestAdjustment = restAdjustment,
        };
    }

    private static IReadOnlyList<string> GetPreferredPatternsForTargetMuscle(
        string targetMuscle,
        bool compoundBias,
        Random random)
    {
        string[] primaryPatterns = targetMuscle switch
        {
            "chest" or "pectorals" => ["horizontal press", "angled press", "push bodyweight"],
            "shoulders" or "deltoids" or "front delts" or "side delts" => ["vertical press", "lateral raise", "horizontal press"],
            "triceps" => ["vertical press", "dip", "triceps extension"],
            "back" or "lats" or "latissimus dorsi" or "rhomboids" or "traps" or "trapezius" => ["horizontal pull", "vertical pull", "rear delt"],
            "biceps" or "forearms" => ["vertical pull", "horizontal pull", "curl"],
            "glutes" or "gluteus" => ["hinge", "hip thrust", "lunge"],
            "hamstrings" => ["hinge", "hamstring curl", "lunge"],
            "quads" or "quadriceps" => ["squat", "lunge", "knee extension"],
            "calves" => ["calf raise", "lunge", "squat"],
            "abs" or "abdominals" or "obliques" or "core" or "lower back" or "erectors" => ["plank", "anti rotation", "core flexion"],
            _ => ["squat", "horizontal press", "horizontal pull"],
        };

        string[] accessoryPatterns = targetMuscle switch
        {
            "chest" or "pectorals" => ["chest fly", "push bodyweight", "horizontal press"],
            "shoulders" or "deltoids" or "front delts" or "side delts" => ["lateral raise", "vertical press", "rear delt"],
            "triceps" => ["triceps extension", "dip", "push bodyweight"],
            "back" or "lats" or "latissimus dorsi" or "rhomboids" or "traps" or "trapezius" => ["rear delt", "horizontal pull", "vertical pull"],
            "biceps" or "forearms" => ["curl", "vertical pull", "horizontal pull"],
            "glutes" or "gluteus" => ["hip thrust", "lunge", "hinge"],
            "hamstrings" => ["hamstring curl", "hinge", "lunge"],
            "quads" or "quadriceps" => ["knee extension", "lunge", "squat"],
            "calves" => ["calf raise", "lunge", "squat"],
            "abs" or "abdominals" or "obliques" or "core" or "lower back" or "erectors" => ["plank", "rotation", "anti rotation"],
            _ => ["lunge", "push bodyweight", "horizontal pull"],
        };

        var pool = compoundBias
            ? primaryPatterns.Concat(accessoryPatterns.Take(1))
            : accessoryPatterns.Concat(primaryPatterns);

        return DistinctShuffled(pool, random, 3);
    }

    private static IReadOnlyList<string> DistinctShuffled(
        IEnumerable<string> values,
        Random random,
        int takeCount)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(_ => random.Next())
            .Take(takeCount)
            .ToList();
    }

    private static List<T> Shuffle<T>(IEnumerable<T> items, Random random)
    {
        return items
            .OrderBy(_ => random.Next())
            .ToList();
    }

    private static int ScoreMovementPattern(string movementPattern, IReadOnlyList<string> preferredPatterns)
    {
        if (string.IsNullOrWhiteSpace(movementPattern) || preferredPatterns.Count == 0)
        {
            return 0;
        }

        for (var index = 0; index < preferredPatterns.Count; index++)
        {
            if (string.Equals(preferredPatterns[index], movementPattern, StringComparison.Ordinal))
            {
                return 18 - (index * 4);
            }
        }

        return -4;
    }

    private static string BuildExerciseFamilyKey(string name)
    {
        var filteredTokens = NormalizeText(name)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !ExerciseFamilyStopWords.Contains(token))
            .Take(4)
            .ToList();

        return filteredTokens.Count == 0
            ? NormalizeText(name)
            : string.Join(' ', filteredTokens);
    }

    private static string ResolveMovementPattern(
        string name,
        IReadOnlyCollection<string> muscles,
        string focusGroup,
        string category)
    {
        var normalizedName = NormalizeText(name);

        if (normalizedName.Contains("incline", StringComparison.Ordinal)
            && normalizedName.Contains("press", StringComparison.Ordinal))
        {
            return "angled press";
        }

        if (normalizedName.Contains("bench", StringComparison.Ordinal)
            || normalizedName.Contains("chest press", StringComparison.Ordinal)
            || normalizedName.Contains("push up", StringComparison.Ordinal)
            || normalizedName.Contains("pushup", StringComparison.Ordinal))
        {
            return "horizontal press";
        }

        if (normalizedName.Contains("overhead", StringComparison.Ordinal)
            || normalizedName.Contains("shoulder press", StringComparison.Ordinal)
            || normalizedName.Contains("arnold", StringComparison.Ordinal))
        {
            return "vertical press";
        }

        if (normalizedName.Contains("dip", StringComparison.Ordinal))
        {
            return "dip";
        }

        if (normalizedName.Contains("row", StringComparison.Ordinal))
        {
            return "horizontal pull";
        }

        if (normalizedName.Contains("pull up", StringComparison.Ordinal)
            || normalizedName.Contains("pullup", StringComparison.Ordinal)
            || normalizedName.Contains("chin up", StringComparison.Ordinal)
            || normalizedName.Contains("pulldown", StringComparison.Ordinal)
            || normalizedName.Contains("lat pull", StringComparison.Ordinal))
        {
            return "vertical pull";
        }

        if (normalizedName.Contains("curl", StringComparison.Ordinal))
        {
            return normalizedName.Contains("hamstring", StringComparison.Ordinal) || normalizedName.Contains("leg", StringComparison.Ordinal)
                ? "hamstring curl"
                : "curl";
        }

        if (normalizedName.Contains("face pull", StringComparison.Ordinal)
            || normalizedName.Contains("rear delt", StringComparison.Ordinal))
        {
            return "rear delt";
        }

        if (normalizedName.Contains("shrug", StringComparison.Ordinal))
        {
            return "shrug";
        }

        if (normalizedName.Contains("squat", StringComparison.Ordinal))
        {
            return normalizedName.Contains("split", StringComparison.Ordinal) ? "lunge" : "squat";
        }

        if (normalizedName.Contains("deadlift", StringComparison.Ordinal)
            || normalizedName.Contains("hinge", StringComparison.Ordinal)
            || normalizedName.Contains("romanian", StringComparison.Ordinal)
            || normalizedName.Contains("good morning", StringComparison.Ordinal))
        {
            return "hinge";
        }

        if (normalizedName.Contains("lunge", StringComparison.Ordinal)
            || normalizedName.Contains("step up", StringComparison.Ordinal)
            || normalizedName.Contains("stepup", StringComparison.Ordinal))
        {
            return "lunge";
        }

        if (normalizedName.Contains("thrust", StringComparison.Ordinal)
            || normalizedName.Contains("bridge", StringComparison.Ordinal))
        {
            return "hip thrust";
        }

        if (normalizedName.Contains("extension", StringComparison.Ordinal)
            && (normalizedName.Contains("leg", StringComparison.Ordinal) || muscles.Contains("quadriceps")))
        {
            return "knee extension";
        }

        if (normalizedName.Contains("calf", StringComparison.Ordinal))
        {
            return "calf raise";
        }

        if (normalizedName.Contains("lateral raise", StringComparison.Ordinal))
        {
            return "lateral raise";
        }

        if (normalizedName.Contains("fly", StringComparison.Ordinal) || normalizedName.Contains("pec deck", StringComparison.Ordinal))
        {
            return "chest fly";
        }

        if (normalizedName.Contains("pushdown", StringComparison.Ordinal)
            || normalizedName.Contains("kickback", StringComparison.Ordinal)
            || normalizedName.Contains("triceps", StringComparison.Ordinal))
        {
            return "triceps extension";
        }

        if (normalizedName.Contains("plank", StringComparison.Ordinal))
        {
            return "plank";
        }

        if (normalizedName.Contains("twist", StringComparison.Ordinal)
            || normalizedName.Contains("rotation", StringComparison.Ordinal))
        {
            return "rotation";
        }

        if (normalizedName.Contains("pallof", StringComparison.Ordinal)
            || normalizedName.Contains("dead bug", StringComparison.Ordinal)
            || normalizedName.Contains("bird dog", StringComparison.Ordinal))
        {
            return "anti rotation";
        }

        if (normalizedName.Contains("crunch", StringComparison.Ordinal)
            || normalizedName.Contains("sit up", StringComparison.Ordinal)
            || normalizedName.Contains("situp", StringComparison.Ordinal)
            || normalizedName.Contains("leg raise", StringComparison.Ordinal))
        {
            return "core flexion";
        }

        return focusGroup switch
        {
            "push" when category == "bodyweight" => "push bodyweight",
            "push" => "horizontal press",
            "pull" => "horizontal pull",
            "lower" => "squat",
            "core" => "plank",
            _ => "full body",
        };
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
        public string FamilyKey { get; init; } = string.Empty;
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
        public string MovementPattern { get; init; } = "full body";
        public bool IsBodyweight { get; init; }
        public bool IsAdvanced { get; init; }
    }

    private sealed class WorkoutSlot
    {
        public string FocusGroup { get; init; } = "full-body";
        public string? TargetMuscle { get; init; }
        public IReadOnlyList<string> PreferredCategories { get; init; } = [];
        public IReadOnlyList<string> PreferredPatterns { get; init; } = [];
    }

    private sealed class GeneratedMainWorkoutResult
    {
        public List<AiWorkoutExerciseDto> Exercises { get; init; } = [];
        public WorkoutSelectionSummary Summary { get; init; } = new();
    }

    private sealed class WorkoutSelectionSummary
    {
        public int CatalogExerciseCount { get; set; }
        public int FallbackExerciseCount { get; set; }
        public int MediaBackedExerciseCount { get; set; }
        public int RotatedAwayFromRecentRepeatsCount { get; set; }
        public List<string> HighlightPatterns { get; } = [];

        public void RecordMovementPattern(string movementPattern)
        {
            if (string.IsNullOrWhiteSpace(movementPattern)
                || movementPattern == "full body"
                || HighlightPatterns.Any(pattern => string.Equals(pattern, movementPattern, StringComparison.Ordinal)))
            {
                return;
            }

            if (HighlightPatterns.Count < 3)
            {
                HighlightPatterns.Add(movementPattern);
            }
        }
    }

    private sealed class CandidateSelectionResult
    {
        public CatalogExerciseCandidate Candidate { get; init; } = null!;
        public bool AvoidedRecentAlternative { get; init; }
    }

    private sealed class ScoredCandidate
    {
        public CatalogExerciseCandidate Candidate { get; init; } = null!;
        public int Score { get; init; }
        public int RecentCount { get; init; }
    }

    private sealed class GenerationVariationProfile
    {
        public int CompoundSetAdjustment { get; init; }
        public int AccessorySetAdjustment { get; init; }
        public RepVariationStyle RepStyle { get; init; } = RepVariationStyle.Standard;
        public int RestAdjustment { get; init; }
    }

    private enum RepVariationStyle
    {
        Lower = 0,
        Standard = 1,
        Higher = 2,
    }
}
