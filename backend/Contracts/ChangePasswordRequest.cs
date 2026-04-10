using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class ChangePasswordRequest
{
    [Required]
    [StringLength(200, MinimumLength = 8)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
