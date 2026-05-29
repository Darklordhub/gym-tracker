namespace backend.Configuration;

public class AppOptions
{
    public const string SectionName = "App";

    public bool AllowStartupWithMigrationFailure { get; set; }
}
