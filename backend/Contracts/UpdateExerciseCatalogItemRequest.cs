using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class UpdateExerciseCatalogItemRequest
{
    [Required]
    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(6000)]
    public string? Instructions { get; set; }

    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    public bool IsActive { get; set; }
}
