namespace backend.Contracts;

public class TrainingRecommendationResponse
{
    public DateOnly Date { get; set; }
    public string RecommendedSessionType { get; set; } = "rest";
    public string Intensity { get; set; } = "low";
    public string FatigueLevel { get; set; } = "moderate";
    public string ShortReason { get; set; } = string.Empty;
    public string GoalContext { get; set; } = string.Empty;
    public int RecentWorkoutCount { get; set; }
    public int RecentStrengthWorkoutCount { get; set; }
    public int RecentCardioWorkoutCount { get; set; }
    public int WeeklyLoadScore { get; set; }
    public decimal? ReadinessScore { get; set; }
    public int? NetCaloriesToday { get; set; }
}
