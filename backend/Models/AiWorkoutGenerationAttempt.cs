namespace backend.Models;

public class AiWorkoutGenerationAttempt
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Status { get; set; } = AiWorkoutGenerationAttemptStatuses.Reserved;
    public string Provider { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public int CandidateExerciseCount { get; set; }
    public int? SelectedExerciseCount { get; set; }
    public string PromptVersion { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorCategory { get; set; }
    public string? SafeErrorMessage { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
