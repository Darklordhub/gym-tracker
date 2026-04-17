using backend.Contracts;
using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ProgressiveOverloadService
{
    private readonly AppDbContext _dbContext;

    public ProgressiveOverloadService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProgressiveOverloadRecommendationResponse> GetRecommendationAsync(
        int userId,
        string? exerciseName)
    {
        var normalizedExerciseName = NormalizeExerciseName(exerciseName);

        if (string.IsNullOrWhiteSpace(normalizedExerciseName))
        {
            return BuildInsufficientDataResponse(exerciseName, "Choose an exercise to generate a progression target.");
        }

        var displayExerciseName = exerciseName!.Trim();

        var entries = await _dbContext.ExerciseEntries
            .AsNoTracking()
            .Where(entry =>
                entry.Workout != null &&
                entry.Workout.UserId == userId &&
                entry.Workout.WorkoutType != "cardio" &&
                entry.ExerciseName.ToUpper() == normalizedExerciseName)
            .Include(entry => entry.Workout)
            .Include(entry => entry.Sets)
            .OrderByDescending(entry => entry.Workout!.Date)
            .ThenByDescending(entry => entry.WorkoutId)
            .Take(5)
            .ToListAsync();

        var sessions = entries
            .Where(entry => entry.Workout is not null && entry.Sets.Count > 0)
            .Select(entry => new ExerciseSession(
                entry.WorkoutId,
                entry.Workout!.Date,
                entry.Sets.Max(set => set.WeightKg),
                entry.Sets
                    .Where(set => set.WeightKg == entry.Sets.Max(currentSet => currentSet.WeightKg))
                    .Max(set => set.Reps),
                entry.Sets.Sum(set => set.Reps * set.WeightKg)))
            .OrderByDescending(session => session.Date)
            .ThenByDescending(session => session.WorkoutId)
            .ToList();

        if (sessions.Count == 0)
        {
            return BuildInsufficientDataResponse(exerciseName, "Log this exercise with sets before using progressive overload guidance.");
        }

        var latest = sessions[0];
        var recentBestWeight = sessions.Max(session => session.TopWeightKg);

        if (sessions.Count < 3)
        {
            return new ProgressiveOverloadRecommendationResponse
            {
                ExerciseName = displayExerciseName,
                RecommendedWeightKg = latest.TopWeightKg,
                RecommendedRepTarget = BuildRepTarget(latest.TopReps),
                ProgressionStatus = "hold",
                ShortReason = "Need at least 3 recent sessions before increasing or deloading. Hold the current working weight and build cleaner data.",
                RelevantSessionCount = sessions.Count,
                LatestWorkingWeightKg = latest.TopWeightKg,
                RecentBestWeightKg = recentBestWeight,
            };
        }

        var recentThree = sessions.Take(3).ToList();
        var previous = recentThree.Skip(1).ToList();
        var previousAverageVolume = previous.Average(session => session.Volume);
        var repeatedRegression =
            latest.TopWeightKg < previous.Min(session => session.TopWeightKg) * 0.95m ||
            latest.Volume < previousAverageVolume * 0.85m;
        var consistentlySuccessful =
            latest.TopWeightKg >= previous.Max(session => session.TopWeightKg) &&
            latest.TopReps >= 8 &&
            recentThree.Count(session => session.TopReps >= 8) >= 2;

        if (repeatedRegression)
        {
            var deloadWeight = RoundToNearestIncrement(latest.TopWeightKg * 0.9m);

            return new ProgressiveOverloadRecommendationResponse
            {
                ExerciseName = displayExerciseName,
                RecommendedWeightKg = deloadWeight,
                RecommendedRepTarget = "6-8 reps",
                ProgressionStatus = "deload",
                ShortReason = "Recent performance has dropped against your prior sessions. Reduce load slightly and rebuild stable reps.",
                RelevantSessionCount = sessions.Count,
                LatestWorkingWeightKg = latest.TopWeightKg,
                RecentBestWeightKg = recentBestWeight,
            };
        }

        if (consistentlySuccessful)
        {
            var increasedWeight = RoundToNearestIncrement(latest.TopWeightKg + GetConservativeIncrement(latest.TopWeightKg));

            return new ProgressiveOverloadRecommendationResponse
            {
                ExerciseName = displayExerciseName,
                RecommendedWeightKg = increasedWeight,
                RecommendedRepTarget = "6-8 reps",
                ProgressionStatus = "increase",
                ShortReason = "Recent top sets are stable enough to justify a small increase. Keep the rep target conservative for the next session.",
                RelevantSessionCount = sessions.Count,
                LatestWorkingWeightKg = latest.TopWeightKg,
                RecentBestWeightKg = recentBestWeight,
            };
        }

        return new ProgressiveOverloadRecommendationResponse
        {
            ExerciseName = displayExerciseName,
            RecommendedWeightKg = latest.TopWeightKg,
            RecommendedRepTarget = BuildRepTarget(latest.TopReps),
            ProgressionStatus = "hold",
            ShortReason = "Recent sessions are mixed. Hold the current working weight and aim to add cleaner reps before increasing load.",
            RelevantSessionCount = sessions.Count,
            LatestWorkingWeightKg = latest.TopWeightKg,
            RecentBestWeightKg = recentBestWeight,
        };
    }

    private static ProgressiveOverloadRecommendationResponse BuildInsufficientDataResponse(
        string? exerciseName,
        string reason)
    {
        return new ProgressiveOverloadRecommendationResponse
        {
            ExerciseName = exerciseName?.Trim() ?? string.Empty,
            RecommendedWeightKg = null,
            RecommendedRepTarget = "6-10 reps",
            ProgressionStatus = "hold",
            ShortReason = reason,
            RelevantSessionCount = 0,
            LatestWorkingWeightKg = null,
            RecentBestWeightKg = null,
        };
    }

    private static string BuildRepTarget(int latestTopReps)
    {
        return latestTopReps switch
        {
            >= 10 => "8-10 reps",
            >= 8 => "8-12 reps",
            _ => "6-8 reps",
        };
    }

    private static decimal GetConservativeIncrement(decimal weightKg)
    {
        return weightKg >= 100m ? 5m : 2.5m;
    }

    private static decimal RoundToNearestIncrement(decimal weightKg)
    {
        return decimal.Round(weightKg / 2.5m, 0, MidpointRounding.AwayFromZero) * 2.5m;
    }

    private static string NormalizeExerciseName(string? exerciseName)
    {
        return exerciseName?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private sealed record ExerciseSession(
        int WorkoutId,
        DateTime Date,
        decimal TopWeightKg,
        int TopReps,
        decimal Volume);
}
