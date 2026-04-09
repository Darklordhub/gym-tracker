namespace backend.Models;

public class GoalSettings
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public AppUser? User { get; set; }
    public decimal? TargetBodyWeightKg { get; set; }
    public int? WeeklyWorkoutTarget { get; set; }
    public string FitnessPhase { get; set; } = "maintain";
}
