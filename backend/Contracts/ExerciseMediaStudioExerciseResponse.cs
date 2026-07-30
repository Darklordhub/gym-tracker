namespace backend.Contracts;

public class ExerciseMediaStudioExerciseResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? Instructions { get; set; }
    public string? PrimaryMuscle { get; set; }
    public string[] SecondaryMuscles { get; set; } = [];
    public string? Equipment { get; set; }
    public string? Difficulty { get; set; }
    public string? VideoUrl { get; set; }
    public string? ProviderVideoUrl { get; set; }
    public string? LocalVideoUrlOverride { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ProviderThumbnailUrl { get; set; }
    public string? LocalThumbnailUrlOverride { get; set; }
    public string? LocalMediaPath { get; set; }
    public bool IsActive { get; set; }
    public bool IsManuallyEdited { get; set; }
    public IReadOnlyList<ExerciseMediaDraftResponse> LatestDrafts { get; set; } = [];
}
