using backend.Contracts;
using backend.Models;

namespace backend.Services;

public static class CycleGuidanceService
{
    public static CycleGuidanceResponse Build(
        UserCycleSettings? settings,
        IReadOnlyCollection<UserCycleEntry> entries,
        IReadOnlyCollection<UserCycleSymptomLog> symptomLogs,
        IReadOnlyCollection<Workout> workouts,
        DateOnly? today = null)
    {
        var currentDate = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var loadSummary = BuildRecentLoadSummary(workouts, currentDate);
        var symptomSummary = BuildSymptomSummary(symptomLogs, currentDate);

        if (settings is null || !settings.IsEnabled)
        {
            return new CycleGuidanceResponse
            {
                IsEnabled = false,
                RecentLoadLabel = loadSummary.Label,
                RecentWorkoutCount = loadSummary.WorkoutCount,
                RecentSetCount = loadSummary.SetCount,
                RecentTrainingLoad = loadSummary.TrainingLoad,
                LatestSymptomLogDate = symptomSummary.LatestLogDate,
                SymptomLoadLabel = symptomSummary.Label,
                RecentFatigueLevel = symptomSummary.FatigueLevel,
                RecentCrampsLevel = symptomSummary.CrampsLevel,
                RecentSleepQuality = symptomSummary.SleepQuality,
                RecentRecoveryFeeling = symptomSummary.RecoveryFeeling,
            };
        }

        var knownStarts = entries
            .Select(entry => (DateOnly?)entry.PeriodStartDate)
            .Concat([settings.LastPeriodStartDate])
            .OfType<DateOnly>()
            .Distinct()
            .Where(date => date <= currentDate)
            .OrderBy(date => date)
            .ToList();

        var lastPeriodStart = knownStarts.LastOrDefault();
        if (lastPeriodStart == default)
        {
            return new CycleGuidanceResponse
            {
                IsEnabled = true,
                PredictionConfidence = "Needs data",
                GuidanceHeadline = "Add more cycle data",
                GuidanceMessage = "Log your last period start date to begin estimating cycle phase and recovery-aware training guidance.",
                RecentLoadLabel = loadSummary.Label,
                RecentWorkoutCount = loadSummary.WorkoutCount,
                RecentSetCount = loadSummary.SetCount,
                RecentTrainingLoad = loadSummary.TrainingLoad,
                LatestSymptomLogDate = symptomSummary.LatestLogDate,
                SymptomLoadLabel = symptomSummary.Label,
                RecentFatigueLevel = symptomSummary.FatigueLevel,
                RecentCrampsLevel = symptomSummary.CrampsLevel,
                RecentSleepQuality = symptomSummary.SleepQuality,
                RecentRecoveryFeeling = symptomSummary.RecoveryFeeling,
                Insights = BuildInsights(null, loadSummary, symptomSummary, null),
            };
        }

        var recordedCycleLengths = knownStarts
            .Zip(knownStarts.Skip(1), (left, right) => right.DayNumber - left.DayNumber)
            .Where(length => length is >= 20 and <= 45)
            .ToList();

        var recordedPeriodLengths = entries
            .Where(entry => entry.PeriodEndDate >= entry.PeriodStartDate)
            .Select(entry => entry.PeriodEndDate.DayNumber - entry.PeriodStartDate.DayNumber + 1)
            .Where(length => length is >= 2 and <= 10)
            .ToList();

        var cycleLength = ResolveCycleLength(settings, recordedCycleLengths);
        var periodLength = ResolvePeriodLength(settings, recordedPeriodLengths);
        var currentCycleDay = Math.Max(1, currentDate.DayNumber - lastPeriodStart.DayNumber + 1);
        var nextPeriodStart = lastPeriodStart.AddDays(cycleLength);
        var estimatedPhase = EstimatePhase(currentCycleDay, cycleLength, periodLength, nextPeriodStart, currentDate);
        var confidence = EstimateConfidence(settings, recordedCycleLengths.Count());
        var guidance = BuildGuidance(estimatedPhase, loadSummary, symptomSummary);

        return new CycleGuidanceResponse
        {
            IsEnabled = true,
            CurrentCycleDay = currentCycleDay,
            EstimatedCurrentPhase = estimatedPhase,
            EstimatedNextPeriodStartDate = nextPeriodStart,
            PredictionConfidence = confidence,
            GuidanceHeadline = guidance.Headline,
            GuidanceMessage = guidance.Message,
            RecentLoadLabel = loadSummary.Label,
            RecentWorkoutCount = loadSummary.WorkoutCount,
            RecentSetCount = loadSummary.SetCount,
            RecentTrainingLoad = decimal.Round(loadSummary.TrainingLoad, 1, MidpointRounding.AwayFromZero),
            IsHigherFatiguePhase = estimatedPhase is "Menstrual" or "Late luteal",
            LatestSymptomLogDate = symptomSummary.LatestLogDate,
            SymptomLoadLabel = symptomSummary.Label,
            RecentFatigueLevel = symptomSummary.FatigueLevel,
            RecentCrampsLevel = symptomSummary.CrampsLevel,
            RecentSleepQuality = symptomSummary.SleepQuality,
            RecentRecoveryFeeling = symptomSummary.RecoveryFeeling,
            Insights = BuildInsights(estimatedPhase, loadSummary, symptomSummary, nextPeriodStart),
        };
    }

    private static int ResolveCycleLength(UserCycleSettings settings, IReadOnlyCollection<int> recordedCycleLengths)
    {
        if (recordedCycleLengths.Count > 0)
        {
            return (int)Math.Round(recordedCycleLengths.Average(), MidpointRounding.AwayFromZero);
        }

        return settings.AverageCycleLengthDays ?? 28;
    }

    private static int ResolvePeriodLength(UserCycleSettings settings, IReadOnlyCollection<int> recordedPeriodLengths)
    {
        if (recordedPeriodLengths.Count > 0)
        {
            return (int)Math.Round(recordedPeriodLengths.Average(), MidpointRounding.AwayFromZero);
        }

        return settings.AveragePeriodLengthDays ?? 5;
    }

    private static string EstimatePhase(
        int currentCycleDay,
        int cycleLength,
        int periodLength,
        DateOnly nextPeriodStart,
        DateOnly today)
    {
        if (currentCycleDay <= periodLength)
        {
            return "Menstrual";
        }

        var daysUntilNextPeriod = nextPeriodStart.DayNumber - today.DayNumber;
        if (daysUntilNextPeriod is >= 0 and <= 4)
        {
            return "Late luteal";
        }

        var ovulationDay = Math.Clamp(
            (int)Math.Round(cycleLength / 2d, MidpointRounding.AwayFromZero),
            periodLength + 4,
            cycleLength - 8);

        if (Math.Abs(currentCycleDay - ovulationDay) <= 2)
        {
            return "Ovulatory";
        }

        return currentCycleDay < ovulationDay ? "Follicular" : "Luteal";
    }

    private static string EstimateConfidence(UserCycleSettings settings, int recordedCycleLengthCount)
    {
        var regularity = NormalizeRegularity(settings.CycleRegularity);

        if (settings.UsesHormonalContraception == true || settings.IsNaturallyCycling == false)
        {
            return "Lower confidence";
        }

        if (recordedCycleLengthCount >= 3 && regularity == "regular")
        {
            return "Higher confidence";
        }

        if (recordedCycleLengthCount >= 1 && regularity != "irregular")
        {
            return "Moderate confidence";
        }

        if (settings.LastPeriodStartDate.HasValue)
        {
            return "Lower confidence";
        }

        return "Needs data";
    }

    private static (string Headline, string Message) BuildGuidance(
        string estimatedPhase,
        RecentLoadSummary loadSummary,
        SymptomSummary symptomSummary)
    {
        var highLoad = loadSummary.WorkoutCount >= 4 || loadSummary.SetCount >= 36 || loadSummary.TrainingLoad >= 6000m;
        var moderateLoad = loadSummary.WorkoutCount >= 3 || loadSummary.SetCount >= 24 || loadSummary.TrainingLoad >= 3500m;
        var strongSymptoms = symptomSummary.BurdenScore >= 4.1m || symptomSummary.FatigueLevel >= 4 || symptomSummary.CrampsLevel >= 4;
        var poorRecovery = symptomSummary.RecoveryFeeling > 0 && symptomSummary.RecoveryFeeling <= 2;
        var poorSleep = symptomSummary.SleepQuality > 0 && symptomSummary.SleepQuality <= 2;
        var symptomPressure = strongSymptoms || poorRecovery || poorSleep;

        return estimatedPhase switch
        {
            "Menstrual" or "Late luteal" when highLoad && symptomPressure => (
                "Back off today if recovery feels strained",
                "You appear to be in a higher-fatigue phase, recent training load is high, and your recent symptoms suggest recovery may be lagging. Reducing intensity or volume today would be sensible."),
            "Menstrual" or "Late luteal" when highLoad => (
                "Consider a lighter session",
                "You appear to be in a higher-fatigue phase and your recent training load is high. Consider reducing intensity or volume today, or shifting toward easier recovery work."),
            "Menstrual" or "Late luteal" when symptomPressure => (
                "Keep today's training conservative",
                "Your recent symptom and recovery logs suggest this phase may feel heavier than usual. A shorter session, easier pacing, or lower volume may support recovery better."),
            "Menstrual" or "Late luteal" => (
                "Prioritize recovery-aware training",
                "This looks like a higher-fatigue phase. A lighter session, steadier pacing, or reduced volume may feel better if recovery is not matching effort."),
            "Follicular" or "Ovulatory" when highLoad && symptomPressure => (
                "Potentially strong phase, but recovery signals are mixed",
                "This phase often tolerates harder work better, but your recent symptom or recovery logs are not fully supporting another big push. Keep the next session high quality without forcing extra volume."),
            "Follicular" or "Ovulatory" when highLoad => (
                "Good phase, but recent load is already high",
                "This may be a better-tolerated phase for harder work, but you have already trained hard several times recently. Keep quality high without automatically adding more volume."),
            "Follicular" or "Ovulatory" when !symptomPressure => (
                "Potentially stronger training window",
                "This may be a better time for heavier work if recovery feels good. Use it for quality top sets or a more demanding session without forcing it."),
            _ when moderateLoad && symptomPressure => (
                "Watch fatigue closely",
                "Recent training load is building and your symptom or recovery logs suggest some strain. Keep the next session controlled and be willing to trim volume."),
            _ when moderateLoad => (
                "Autoregulate and watch fatigue",
                "Recent training load is building. Keep the next session controlled and adjust volume if recovery feels slower than usual."),
            _ => (
                "Train steadily and adjust by feel",
                "Your estimated phase and recent recovery signals do not point to a strong caution flag. Keep the plan practical and adjust effort based on session quality.")
        };
    }

    private static RecentLoadSummary BuildRecentLoadSummary(IReadOnlyCollection<Workout> workouts, DateOnly currentDate)
    {
        var windowStart = currentDate.AddDays(-6).ToDateTime(TimeOnly.MinValue);
        var recentWorkouts = workouts.Where(workout => workout.Date >= windowStart).ToList();
        var setCount = recentWorkouts.Sum(workout =>
            workout.WorkoutType == "cardio"
                ? 0
                : workout.ExerciseEntries.Sum(entry => entry.Sets.Count));
        var strengthTrainingLoad = recentWorkouts.Sum(workout =>
            workout.WorkoutType == "cardio"
                ? 0
                : workout.ExerciseEntries.Sum(entry => entry.Sets.Sum(set => set.WeightKg * set.Reps)));
        var cardioLoad = recentWorkouts
            .Where(workout => workout.WorkoutType == "cardio")
            .Sum(workout => (workout.CardioDurationMinutes ?? 0) * GetCardioIntensityFactor(workout.CardioIntensity));
        var trainingLoad = strengthTrainingLoad + cardioLoad;

        var label = trainingLoad >= 6000m || setCount >= 36 || recentWorkouts.Count >= 4
            ? "High"
            : trainingLoad >= 3500m || setCount >= 24 || recentWorkouts.Count >= 3
                ? "Moderate"
                : "Low";

        return new RecentLoadSummary(recentWorkouts.Count, setCount, trainingLoad, label);
    }

    private static SymptomSummary BuildSymptomSummary(IReadOnlyCollection<UserCycleSymptomLog> symptomLogs, DateOnly currentDate)
    {
        if (symptomLogs.Count == 0)
        {
            return SymptomSummary.Empty;
        }

        var recentLogs = symptomLogs
            .Where(log => log.Date >= currentDate.AddDays(-4) && log.Date <= currentDate)
            .OrderByDescending(log => log.Date)
            .ThenByDescending(log => log.UpdatedAt)
            .ToList();

        var latestLog = recentLogs.FirstOrDefault() ?? symptomLogs
            .OrderByDescending(log => log.Date)
            .ThenByDescending(log => log.UpdatedAt)
            .First();

        var sample = recentLogs.Count > 0 ? recentLogs : [latestLog];
        var baselineLogs = symptomLogs.OrderByDescending(log => log.Date).Take(8).ToList();
        var recentBurden = sample.Average(GetBurdenScore);
        var baselineBurden = baselineLogs.Count > 0 ? baselineLogs.Average(GetBurdenScore) : recentBurden;
        var adjustedBurden = recentBurden - baselineBurden;

        var label = recentBurden >= 4.1 || adjustedBurden >= 0.6
            ? "High"
            : recentBurden >= 3.1 || adjustedBurden >= 0.2
                ? "Moderate"
                : "Low";

        return new SymptomSummary(
            latestLog.Date,
            label,
            latestLog.FatigueLevel,
            latestLog.CrampsLevel,
            latestLog.SleepQuality,
            latestLog.RecoveryFeeling,
            decimal.Round((decimal)recentBurden, 2, MidpointRounding.AwayFromZero));
    }

    private static List<string> BuildInsights(
        string? estimatedPhase,
        RecentLoadSummary loadSummary,
        SymptomSummary symptomSummary,
        DateOnly? nextPeriodStart)
    {
        var insights = new List<string>();

        if (estimatedPhase is not null)
        {
            insights.Add($"Estimated phase: {estimatedPhase}.");
        }

        if (nextPeriodStart.HasValue)
        {
            insights.Add($"Next period is currently estimated around {nextPeriodStart:yyyy-MM-dd}.");
        }

        insights.Add($"Recent training load is {loadSummary.Label.ToLowerInvariant()} from {loadSummary.WorkoutCount} workout{(loadSummary.WorkoutCount == 1 ? string.Empty : "s")} in the last 7 days.");

        if (symptomSummary.LatestLogDate.HasValue)
        {
            insights.Add($"Recent symptom pressure looks {symptomSummary.Label.ToLowerInvariant()} based on your latest fatigue, cramps, sleep, and recovery log.");
        }

        return insights;
    }

    private static double GetBurdenScore(UserCycleSymptomLog log)
    {
        var strain = log.FatigueLevel + log.CrampsLevel + log.BloatingLevel + (6 - log.SleepQuality) + (6 - log.RecoveryFeeling);
        return strain / 5d;
    }

    private static decimal GetCardioIntensityFactor(string? cardioIntensity)
    {
        return cardioIntensity?.Trim().ToLowerInvariant() switch
        {
            "high" => 30m,
            "moderate" => 20m,
            _ => 12m,
        };
    }

    private static string NormalizeRegularity(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private sealed record RecentLoadSummary(int WorkoutCount, int SetCount, decimal TrainingLoad, string Label);

    private sealed record SymptomSummary(
        DateOnly? LatestLogDate,
        string Label,
        int? FatigueLevel,
        int? CrampsLevel,
        int? SleepQuality,
        int? RecoveryFeeling,
        decimal BurdenScore)
    {
        public static SymptomSummary Empty { get; } = new(null, "Unknown", null, null, null, null, 0m);
    }
}
