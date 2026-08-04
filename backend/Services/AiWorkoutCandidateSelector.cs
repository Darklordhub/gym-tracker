using backend.Configuration;
using backend.Data;
using backend.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class AiWorkoutCandidateSelector : IAiWorkoutCandidateSelector
{
    private const int MinimumSuitabilityScore = 14;

    private static readonly IReadOnlyDictionary<string, int> EmptyMovementPatternUsage =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private readonly AppDbContext _dbContext;
    private readonly AiWorkoutGenerationOptions _options;

    public AiWorkoutCandidateSelector(
        AppDbContext dbContext,
        IOptions<AiWorkoutGenerationOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<AiWorkoutCandidate>> SelectCandidatesAsync(
        string userId,
        AiWorkoutGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!int.TryParse(userId, out var parsedUserId) || parsedUserId <= 0)
        {
            throw new InvalidOperationException("Authenticated user identifier is invalid.");
        }

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

        if (catalogItems.Count == 0)
        {
            return [];
        }

        var context = AiWorkoutGeneratorService.BuildContext(request, goals, recentWorkouts.Count);
        var catalogCandidates = AiWorkoutGeneratorService.BuildCatalogCandidates(
            catalogItems,
            context.ExcludedExercises);
        if (catalogCandidates.Count == 0)
        {
            return [];
        }

        var slots = AiWorkoutGeneratorService.BuildMainSlots(context, new Random(1729));
        if (slots.Count == 0)
        {
            return [];
        }

        var recentExerciseCounts = AiWorkoutGeneratorService.BuildRecentExerciseCounts(recentWorkouts.Take(2));
        var recentExercisePenalties = AiWorkoutGeneratorService.BuildRecentExercisePenaltyLookup(recentWorkouts.Take(6));
        var recentFamilyPenalties = AiWorkoutGeneratorService.BuildRecentExerciseFamilyPenaltyLookup(recentWorkouts.Take(6));
        var recentPatternPenalties = AiWorkoutGeneratorService.BuildRecentMovementPatternPenaltyLookup(recentWorkouts.Take(6));

        var rankedCandidates = catalogCandidates
            .Select(candidate => RankCandidate(
                candidate,
                context,
                slots,
                recentExerciseCounts,
                recentExercisePenalties,
                recentFamilyPenalties,
                recentPatternPenalties))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.WasRecentlyUsed)
            .ThenBy(candidate => candidate.Candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Candidate.Id)
            .Take(_options.GetEffectiveMaxCandidateExercises())
            .ToList();

        return rankedCandidates
            .Select((candidate, index) => MapCandidate(candidate, context, index + 1))
            .ToList();
    }

    private static RankedCatalogCandidate RankCandidate(
        CatalogExerciseCandidate candidate,
        WorkoutGenerationContext context,
        IReadOnlyList<WorkoutSlot> slots,
        IReadOnlyDictionary<string, int> recentExerciseCounts,
        IReadOnlyDictionary<string, int> recentExercisePenalties,
        IReadOnlyDictionary<string, int> recentFamilyPenalties,
        IReadOnlyDictionary<string, int> recentPatternPenalties)
    {
        var recentCount = recentExerciseCounts.TryGetValue(candidate.NameNormalized, out var count) ? count : 0;
        var bestScore = int.MinValue;
        var bestSlot = slots[0];

        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            var slot = slots[slotIndex];
            var randomSeed = unchecked((candidate.Id * 397) ^ (slotIndex * 7919) ^ 1729);
            var score = AiWorkoutGeneratorService.ScoreCandidate(
                candidate,
                slot,
                context,
                recentCount,
                recentExercisePenalties,
                recentFamilyPenalties,
                recentPatternPenalties,
                EmptyMovementPatternUsage,
                new Random(randomSeed));

            if (score > bestScore)
            {
                bestScore = score;
                bestSlot = slot;
            }
        }

        return new RankedCatalogCandidate
        {
            Candidate = candidate,
            BestSlot = bestSlot,
            Score = bestScore,
            WasRecentlyUsed = recentCount > 0,
        };
    }

    private static AiWorkoutCandidate MapCandidate(
        RankedCatalogCandidate rankedCandidate,
        WorkoutGenerationContext context,
        int rank)
    {
        var candidate = rankedCandidate.Candidate;
        return new AiWorkoutCandidate
        {
            ExerciseCatalogItemId = candidate.Id,
            Name = candidate.Name,
            Category = candidate.Category,
            TargetMuscle = NullIfWhiteSpace(candidate.PrimaryMuscle),
            SecondaryMuscles = candidate.Muscles
                .Where(muscle => !string.Equals(
                    muscle,
                    candidate.PrimaryMuscleNormalized,
                    StringComparison.Ordinal))
                .ToList(),
            Equipment = NullIfWhiteSpace(candidate.Equipment),
            Difficulty = NullIfWhiteSpace(candidate.Difficulty),
            FocusGroup = candidate.FocusGroup,
            MovementPattern = candidate.MovementPattern,
            Score = rankedCandidate.Score,
            Rank = rank,
            MatchReasons = BuildMatchReasons(rankedCandidate, context),
            IsBodyweight = candidate.IsBodyweight,
            IsAdvanced = candidate.IsAdvanced,
            WasRecentlyUsed = rankedCandidate.WasRecentlyUsed,
            MeetsMinimumSuitability = rankedCandidate.Score >= MinimumSuitabilityScore,
        };
    }

    private static IReadOnlyList<string> BuildMatchReasons(
        RankedCatalogCandidate rankedCandidate,
        WorkoutGenerationContext context)
    {
        var candidate = rankedCandidate.Candidate;
        var slot = rankedCandidate.BestSlot;
        var reasons = new List<string>();

        if (context.TargetMuscles.Count > 0 &&
            candidate.Muscles.Intersect(context.TargetMuscles, StringComparer.Ordinal).Any())
        {
            reasons.Add("target muscle match");
        }

        if (string.Equals(candidate.FocusGroup, slot.FocusGroup, StringComparison.Ordinal) ||
            (slot.FocusGroup == "upper" && candidate.FocusGroup is "push" or "pull"))
        {
            reasons.Add("workout focus match");
        }

        if (slot.PreferredCategories.Contains(candidate.Category, StringComparer.Ordinal))
        {
            reasons.Add("exercise category match");
        }

        if (slot.PreferredPatterns.Contains(candidate.MovementPattern, StringComparer.Ordinal))
        {
            reasons.Add("movement pattern match");
        }

        if (context.Goal == "strength" && candidate.Category == "compound")
        {
            reasons.Add("strength goal match");
        }
        else if (context.Goal == "muscle gain" && candidate.Category == "isolation")
        {
            reasons.Add("muscle gain goal match");
        }

        if (rankedCandidate.WasRecentlyUsed)
        {
            reasons.Add("recent-use penalty applied");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("general workout suitability");
        }

        return reasons;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed class RankedCatalogCandidate
    {
        public CatalogExerciseCandidate Candidate { get; init; } = null!;
        public WorkoutSlot BestSlot { get; init; } = null!;
        public int Score { get; init; }
        public bool WasRecentlyUsed { get; init; }
    }
}
