using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class TrainingIntelligenceService
{
    private readonly AppDbContext _dbContext;

    public TrainingIntelligenceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TrainingRecommendationResponse> GetDailyRecommendationAsync(int userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var todayStart = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var recentStart = todayStart.AddDays(-6);

        var recentWorkouts = await _dbContext.Workouts
            .AsNoTracking()
            .Where(workout => workout.UserId == userId && workout.Date >= recentStart)
            .Include(workout => workout.ExerciseEntries)
            .ThenInclude(exercise => exercise.Sets)
            .OrderByDescending(workout => workout.Date)
            .ThenByDescending(workout => workout.Id)
            .ToListAsync();

        var readinessLogs = await _dbContext.UserReadinessLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId && log.Date >= today.AddDays(-6))
            .OrderByDescending(log => log.Date)
            .ThenByDescending(log => log.UpdatedAt)
            .ToListAsync();

        var calorieLogs = await _dbContext.UserCalorieLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId && log.Date >= today.AddDays(-6))
            .OrderByDescending(log => log.Date)
            .ThenByDescending(log => log.UpdatedAt)
            .ToListAsync();

        var goals = await _dbContext.GoalSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(goalSettings => goalSettings.UserId == userId);

        return BuildRecommendation(today, recentWorkouts, readinessLogs, calorieLogs, goals);
    }

    private static TrainingRecommendationResponse BuildRecommendation(
        DateOnly today,
        IReadOnlyCollection<Workout> recentWorkouts,
        IReadOnlyCollection<UserReadinessLog> readinessLogs,
        IReadOnlyCollection<UserCalorieLog> calorieLogs,
        GoalSettings? goals)
    {
        var strengthWorkouts = recentWorkouts
            .Where(workout => !string.Equals(workout.WorkoutType, "cardio", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var cardioWorkouts = recentWorkouts
            .Where(workout => string.Equals(workout.WorkoutType, "cardio", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var weeklyLoadScore = CalculateWeeklyLoadScore(strengthWorkouts, cardioWorkouts);
        var fatigueLevel = CalculateFatigueLevel(weeklyLoadScore, readinessLogs);
        var todayReadiness = readinessLogs.FirstOrDefault(log => log.Date == today);
        decimal? readinessScore = todayReadiness is null ? null : CalculateReadinessScore(todayReadiness);
        var netCaloriesToday = CalculateNetCaloriesToday(today, recentWorkouts, calorieLogs, goals);
        var goalPhase = NormalizeGoalPhase(goals?.FitnessPhase);
        var weeklyTarget = goals?.WeeklyWorkoutTarget;
        var weeklyProgress = weeklyTarget.HasValue && weeklyTarget.Value > 0
            ? recentWorkouts.Count / (decimal)weeklyTarget.Value
            : (decimal?)null;

        var recommendation = ChooseRecommendation(
            goalPhase,
            weeklyLoadScore,
            fatigueLevel,
            readinessScore,
            netCaloriesToday,
            recentWorkouts.Count,
            strengthWorkouts.Count,
            cardioWorkouts.Count,
            weeklyProgress);

        return new TrainingRecommendationResponse
        {
            Date = today,
            RecommendedSessionType = recommendation.SessionType,
            Intensity = recommendation.Intensity,
            FatigueLevel = fatigueLevel,
            ShortReason = recommendation.ShortReason,
            GoalContext = BuildGoalContext(goalPhase, goals, weeklyProgress, netCaloriesToday),
            RecentWorkoutCount = recentWorkouts.Count,
            RecentStrengthWorkoutCount = strengthWorkouts.Count,
            RecentCardioWorkoutCount = cardioWorkouts.Count,
            WeeklyLoadScore = weeklyLoadScore,
            ReadinessScore = readinessScore,
            NetCaloriesToday = netCaloriesToday,
        };
    }

    private static RecommendationChoice ChooseRecommendation(
        string goalPhase,
        int weeklyLoadScore,
        string fatigueLevel,
        decimal? readinessScore,
        int? netCaloriesToday,
        int recentWorkoutCount,
        int recentStrengthWorkoutCount,
        int recentCardioWorkoutCount,
        decimal? weeklyProgress)
    {
        var readinessIsLow = readinessScore.HasValue && readinessScore.Value < 1.9m;
        var readinessIsHigh = readinessScore.HasValue && readinessScore.Value >= 2.5m;
        var underFueled = netCaloriesToday.HasValue && netCaloriesToday.Value <= -400;
        var highSurplus = netCaloriesToday.HasValue && netCaloriesToday.Value >= 500;
        var targetAlreadyCovered = weeklyProgress.HasValue && weeklyProgress.Value >= 1m;

        if (fatigueLevel == "high" && (readinessIsLow || underFueled))
        {
            return new RecommendationChoice(
                "rest",
                "low",
                "Recovery should lead today. Recent training load is high and your recovery or fueling signal is not supporting another hard session.");
        }

        if (readinessIsLow)
        {
            return new RecommendationChoice(
                "cardio",
                "low",
                "Readiness is low today. Keep movement easy with a walk, light ride, or short recovery session instead of forcing intensity.");
        }

        if (underFueled && weeklyLoadScore >= 55)
        {
            return new RecommendationChoice(
                "cardio",
                "low",
                "Fueling is running low against recent workload. Low-intensity cardio is a better fit than a demanding strength session.");
        }

        if (fatigueLevel == "high")
        {
            return new RecommendationChoice(
                "rest",
                "low",
                "Recent weekly load is high. A rest day or very easy movement will protect recovery and keep the next hard session higher quality.");
        }

        if (goalPhase == "cut")
        {
            if (targetAlreadyCovered)
            {
                return new RecommendationChoice(
                    "cardio",
                    "low",
                    "Your weekly target is already covered. Easy cardio supports fat loss without adding much recovery cost.");
            }

            return new RecommendationChoice(
                recentStrengthWorkoutCount <= recentCardioWorkoutCount ? "strength" : "cardio",
                readinessIsHigh && !underFueled ? "moderate" : "low",
                "Fat-loss goals benefit from consistent training while managing fatigue. Keep today controlled rather than chasing maximum intensity.");
        }

        if (goalPhase == "bulk")
        {
            if (readinessIsHigh && !underFueled && weeklyLoadScore < 70)
            {
                return new RecommendationChoice(
                    "strength",
                    highSurplus ? "high" : "moderate",
                    "Recovery and fueling look supportive for muscle gain. A focused strength session is the best use of today.");
            }

            return new RecommendationChoice(
                recentWorkoutCount == 0 ? "strength" : "cardio",
                "moderate",
                "Muscle-gain goals still need recovery quality. Train, but keep the session controlled unless warm-ups feel strong.");
        }

        if (targetAlreadyCovered && fatigueLevel == "moderate")
        {
            return new RecommendationChoice(
                "cardio",
                "low",
                "You are on pace for the week. Easy cardio or mobility keeps momentum without creating unnecessary fatigue.");
        }

        if (readinessIsHigh && weeklyLoadScore < 65)
        {
            return new RecommendationChoice(
                "strength",
                "moderate",
                "Readiness is strong and weekly load is manageable. A quality strength session fits well today.");
        }

        if (recentWorkoutCount == 0)
        {
            return new RecommendationChoice(
                "strength",
                "moderate",
                "There is not much recent training data yet. Start with a controlled strength session and log how it feels.");
        }

        return new RecommendationChoice(
            "cardio",
            "moderate",
            "Recent load is manageable but not fresh. Moderate cardio or a shorter mixed session keeps progress moving without overreaching.");
    }

    private static int CalculateWeeklyLoadScore(
        IEnumerable<Workout> strengthWorkouts,
        IEnumerable<Workout> cardioWorkouts)
    {
        var strengthScore = strengthWorkouts.Sum(workout =>
        {
            var setCount = workout.ExerciseEntries.Sum(exercise => exercise.Sets.Count);
            var totalLoad = workout.ExerciseEntries
                .SelectMany(exercise => exercise.Sets)
                .Sum(set => set.Reps * set.WeightKg);

            return 12 + (setCount * 2) + (int)Math.Min(25, Math.Floor(totalLoad / 1200m));
        });

        var cardioScore = cardioWorkouts.Sum(workout =>
        {
            var intensityMultiplier = NormalizeIntensity(workout.CardioIntensity) switch
            {
                "high" => 1.4m,
                "moderate" => 1.0m,
                _ => 0.65m,
            };
            return (int)Math.Round((workout.CardioDurationMinutes ?? 0) * intensityMultiplier, MidpointRounding.AwayFromZero);
        });

        return Math.Clamp(strengthScore + cardioScore, 0, 100);
    }

    private static string CalculateFatigueLevel(
        int weeklyLoadScore,
        IReadOnlyCollection<UserReadinessLog> readinessLogs)
    {
        var latestReadiness = readinessLogs.FirstOrDefault();
        decimal? readinessScore = latestReadiness is null ? null : CalculateReadinessScore(latestReadiness);
        var lowReadinessCount = readinessLogs.Count(log => CalculateReadinessScore(log) < 1.9m);

        if (weeklyLoadScore >= 75 || (readinessScore.HasValue && readinessScore.Value < 1.7m) || lowReadinessCount >= 2)
        {
            return "high";
        }

        if (weeklyLoadScore >= 45 || (readinessScore.HasValue && readinessScore.Value < 2.4m))
        {
            return "moderate";
        }

        return "low";
    }

    private static decimal CalculateReadinessScore(UserReadinessLog log)
    {
        var sorenessRecovery = 4 - log.SorenessLevel;
        return decimal.Round(
            (log.EnergyLevel + sorenessRecovery + log.SleepQuality + log.MotivationLevel) / 4m,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static int? CalculateNetCaloriesToday(
        DateOnly today,
        IEnumerable<Workout> recentWorkouts,
        IReadOnlyCollection<UserCalorieLog> calorieLogs,
        GoalSettings? goals)
    {
        var todayLog = calorieLogs.FirstOrDefault(log => log.Date == today);
        var targetCalories = ResolveDailyCalorieTarget(goals);

        if (todayLog is null || targetCalories is null)
        {
            return null;
        }

        var todayStart = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var todayEnd = todayStart.AddDays(1);
        var caloriesBurned = recentWorkouts
            .Where(workout => workout.Date >= todayStart && workout.Date < todayEnd)
            .Sum(EstimateWorkoutCaloriesBurned);

        return todayLog.CaloriesConsumed - caloriesBurned - targetCalories.Value;
    }

    private static int? ResolveDailyCalorieTarget(GoalSettings? goals)
    {
        if (goals is null)
        {
            return null;
        }

        if (string.Equals(goals.CalorieTargetMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            return goals.DailyCalorieTarget;
        }

        if (goals.TargetBodyWeightKg is null)
        {
            return null;
        }

        var multiplier = NormalizeGoalPhase(goals.FitnessPhase) switch
        {
            "cut" => 27m,
            "bulk" => 34m,
            _ => 31m,
        };

        return (int)Math.Round(goals.TargetBodyWeightKg.Value * multiplier, MidpointRounding.AwayFromZero);
    }

    private static int EstimateWorkoutCaloriesBurned(Workout workout)
    {
        if (string.Equals(workout.WorkoutType, "cardio", StringComparison.OrdinalIgnoreCase))
        {
            var kcalPerMinute = NormalizeIntensity(workout.CardioIntensity) switch
            {
                "high" => 11,
                "moderate" => 8,
                _ => 5,
            };

            return (workout.CardioDurationMinutes ?? 0) * kcalPerMinute;
        }

        var exerciseCount = workout.ExerciseEntries.Count;
        var setCount = workout.ExerciseEntries.Sum(exercise => exercise.Sets.Count);
        return (exerciseCount * 12) + (setCount * 7);
    }

    private static string BuildGoalContext(
        string goalPhase,
        GoalSettings? goals,
        decimal? weeklyProgress,
        int? netCaloriesToday)
    {
        var phaseContext = goalPhase switch
        {
            "cut" => "Fat-loss phase: protect consistency and recovery while keeping intensity controlled.",
            "bulk" => "Muscle-gain phase: prioritize quality strength work when recovery and fueling support it.",
            _ => "Maintenance phase: balance strength, cardio, and recovery without chasing fatigue.",
        };

        var weeklyContext = goals?.WeeklyWorkoutTarget is null
            ? " Set a weekly workout target for sharper consistency guidance."
            : weeklyProgress.HasValue && weeklyProgress.Value >= 1m
                ? " Your weekly workout target is already covered."
                : $" You are at {Math.Round((weeklyProgress ?? 0m) * 100)}% of this week’s workout target.";

        var calorieContext = netCaloriesToday switch
        {
            <= -400 => " Fueling is low for the current workload.",
            >= 500 => " You are running a larger surplus today.",
            null => " Log calories today for sharper fueling context.",
            _ => " Fueling is close enough to target for normal training decisions.",
        };

        return $"{phaseContext}{weeklyContext}{calorieContext}";
    }

    private static string NormalizeGoalPhase(string? fitnessPhase)
    {
        return string.IsNullOrWhiteSpace(fitnessPhase)
            ? "maintain"
            : fitnessPhase.Trim().ToLowerInvariant() switch
            {
                "cut" => "cut",
                "bulk" => "bulk",
                _ => "maintain",
            };
    }

    private static string NormalizeIntensity(string? intensity)
    {
        return string.IsNullOrWhiteSpace(intensity)
            ? "low"
            : intensity.Trim().ToLowerInvariant() switch
            {
                "high" => "high",
                "moderate" => "moderate",
                _ => "low",
            };
    }

    private sealed record RecommendationChoice(
        string SessionType,
        string Intensity,
        string ShortReason);
}
