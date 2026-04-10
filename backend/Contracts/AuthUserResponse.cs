namespace backend.Contracts;

public class AuthUserResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? HeightCm { get; set; }
    public string? Gender { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
