namespace backend.Contracts;

public class CycleGuidanceResponse
{
    public bool IsEnabled { get; set; }
    public int? CurrentCycleDay { get; set; }
    public string? EstimatedCurrentPhase { get; set; }
    public DateOnly? EstimatedNextPeriodStartDate { get; set; }
    public string PredictionConfidence { get; set; } = "Needs data";
    public string GuidanceHeadline { get; set; } = "Cycle-aware guidance is off.";
    public string GuidanceMessage { get; set; } = "Enable cycle-aware guidance in profile to receive optional training context.";
    public string RecentLoadLabel { get; set; } = "Unknown";
    public int RecentWorkoutCount { get; set; }
    public int RecentSetCount { get; set; }
    public decimal RecentTrainingLoad { get; set; }
    public bool IsHigherFatiguePhase { get; set; }
}
