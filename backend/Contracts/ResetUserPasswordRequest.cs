using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class ResetUserPasswordRequest
{
    [Required]
    [StringLength(200, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
