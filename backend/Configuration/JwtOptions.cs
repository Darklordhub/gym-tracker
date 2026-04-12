namespace backend.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "gym-tracker-api";
    public string Audience { get; set; } = "gym-tracker-app";
    public int ExpirationHours { get; set; } = 24;

}
