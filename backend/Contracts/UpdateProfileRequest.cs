using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class UpdateProfileRequest
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(80)]
    public string? DisplayName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [Range(50, 300)]
    public int? HeightCm { get; set; }

    [StringLength(50)]
    public string? Gender { get; set; }
}
