namespace backend.Contracts;

public class GoalSettingsResponse
{
    public decimal? TargetBodyWeightKg { get; set; }
    public int? WeeklyWorkoutTarget { get; set; }
    public string FitnessPhase { get; set; } = "maintain";
    public int? DailyCalorieTarget { get; set; }
    public string CalorieTargetMode { get; set; } = "manual";
}
