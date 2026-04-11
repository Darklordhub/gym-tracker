using backend.Contracts;
using backend.Models;

namespace backend.Services;

public static class CycleGuidanceService
{
    public static CycleGuidanceResponse Build(
        UserCycleSettings? settings,
        IReadOnlyCollection<UserCycleEntry> entries,
        IReadOnlyCollection<Workout> workouts,
        DateOnly? today = null)
    {
        var currentDate = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var loadSummary = BuildRecentLoadSummary(workouts, currentDate);

        if (settings is null || !settings.IsEnabled)
        {
            return new CycleGuidanceResponse
            {
                IsEnabled = false,
                RecentLoadLabel = loadSummary.Label,
                RecentWorkoutCount = loadSummary.WorkoutCount,
                RecentSetCount = loadSummary.SetCount,
                RecentTrainingLoad = loadSummary.TrainingLoad,
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
        var guidance = BuildGuidance(estimatedPhase, loadSummary);

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

        var ovulationDay = Math.Clamp((int)Math.Round(cycleLength / 2d, MidpointRounding.AwayFromZero), periodLength + 4, cycleLength - 8);
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

    private static (string Headline, string Message) BuildGuidance(string estimatedPhase, RecentLoadSummary loadSummary)
    {
        var highLoad = loadSummary.WorkoutCount >= 4 || loadSummary.SetCount >= 36 || loadSummary.TrainingLoad >= 6000m;
        var moderateLoad = loadSummary.WorkoutCount >= 3 || loadSummary.SetCount >= 24 || loadSummary.TrainingLoad >= 3500m;

        return estimatedPhase switch
        {
            "Menstrual" or "Late luteal" when highLoad => (
                "Consider a lighter session",
                "You appear to be in a higher-fatigue phase and your recent training load is high. Consider reducing intensity or volume today, or shifting toward easier recovery work."),
            "Menstrual" or "Late luteal" => (
                "Prioritize recovery-aware training",
                "This looks like a higher-fatigue phase. A lighter session, steadier pacing, or reduced volume may feel better if recovery is not matching effort."),
            "Follicular" or "Ovulatory" when highLoad => (
                "Good phase, but recent load is already high",
                "This may be a better-tolerated phase for harder work, but you have already trained hard several times recently. Keep quality high without automatically adding more volume."),
            "Follicular" or "Ovulatory" => (
                "Potentially stronger training window",
                "This may be a better time for heavier work if recovery feels good. Use it for quality top sets or a more demanding session without forcing it."),
            _ when moderateLoad => (
                "Autoregulate and watch fatigue",
                "Recent training load is building. Keep the next session controlled and adjust volume if recovery feels slower than usual."),
            _ => (
                "Train steadily and adjust by feel",
                "Your estimated phase does not point to a strong caution flag. Keep the plan practical and adjust effort based on recovery and session quality.")
        };
    }

    private static RecentLoadSummary BuildRecentLoadSummary(IReadOnlyCollection<Workout> workouts, DateOnly currentDate)
    {
        var windowStart = currentDate.AddDays(-6).ToDateTime(TimeOnly.MinValue);
        var recentWorkouts = workouts.Where(workout => workout.Date >= windowStart).ToList();
        var setCount = recentWorkouts.Sum(workout => workout.ExerciseEntries.Sum(entry => entry.Sets.Count));
        var trainingLoad = recentWorkouts.Sum(workout => workout.ExerciseEntries.Sum(entry => entry.Sets.Sum(set => set.WeightKg * set.Reps)));

        var label = trainingLoad >= 6000m || setCount >= 36 || recentWorkouts.Count >= 4
            ? "High"
            : trainingLoad >= 3500m || setCount >= 24 || recentWorkouts.Count >= 3
                ? "Moderate"
                : "Low";

        return new RecentLoadSummary(recentWorkouts.Count, setCount, trainingLoad, label);
    }

    private static string NormalizeRegularity(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private sealed record RecentLoadSummary(int WorkoutCount, int SetCount, decimal TrainingLoad, string Label);
}
