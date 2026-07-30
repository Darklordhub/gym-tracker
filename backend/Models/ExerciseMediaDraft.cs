namespace backend.Models;

public class ExerciseMediaDraft
{
    public int Id { get; set; }
    public int ExerciseCatalogItemId { get; set; }
    public ExerciseCatalogItem? ExerciseCatalogItem { get; set; }
    public string Status { get; set; } = ExerciseMediaDraftStatuses.Queued;
    public string MediaType { get; set; } = ExerciseMediaDraftMediaTypes.Video;
    public string PromptText { get; set; } = string.Empty;
    public string? PromptVersion { get; set; }
    public string? SourceSnapshotJson { get; set; }
    public string? GeneratedThumbnailUrl { get; set; }
    public string? GeneratedVideoUrl { get; set; }
    public string? GenerationProvider { get; set; }
    public string? GenerationModel { get; set; }
    public string? ProviderJobId { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public int? PublishedByUserId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<ExerciseMediaGenerationAttempt> GenerationAttempts { get; set; } = new();
}
