namespace backend.Models;

public class ExerciseCatalogItem
{
    public int Id { get; set; }
    public string Source { get; set; } = "local";
    public string? ExternalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public string? PrimaryMuscle { get; set; }
    public string SecondaryMuscles { get; set; } = string.Empty;
    public string? Equipment { get; set; }
    public string? Difficulty { get; set; }
    public string? VideoUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? LocalMediaPath { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
