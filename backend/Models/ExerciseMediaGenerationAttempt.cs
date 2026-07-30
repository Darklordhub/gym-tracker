namespace backend.Models;

public class ExerciseMediaGenerationAttempt
{
    public int Id { get; set; }
    public int ExerciseMediaDraftId { get; set; }
    public ExerciseMediaDraft? ExerciseMediaDraft { get; set; }
    public int ExerciseCatalogItemId { get; set; }
    public int? RequestedByUserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string Status { get; set; } = ExerciseMediaGenerationAttemptStatuses.Started;
    public string? ProviderJobId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
