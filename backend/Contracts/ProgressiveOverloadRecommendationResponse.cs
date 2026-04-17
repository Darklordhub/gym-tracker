namespace backend.Contracts;

public class ProgressiveOverloadRecommendationResponse
{
    public string ExerciseName { get; set; } = string.Empty;
    public decimal? RecommendedWeightKg { get; set; }
    public string RecommendedRepTarget { get; set; } = "6-10 reps";
    public string ProgressionStatus { get; set; } = "hold";
    public string ShortReason { get; set; } = string.Empty;
    public int RelevantSessionCount { get; set; }
    public decimal? LatestWorkingWeightKg { get; set; }
    public decimal? RecentBestWeightKg { get; set; }
}
