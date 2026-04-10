using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class UpdateUserRoleRequest
{
    [Required]
    [StringLength(20)]
    public string Role { get; set; } = string.Empty;
}
