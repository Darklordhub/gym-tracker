namespace backend.Configuration;

public class ExerciseMediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    // Relative paths are resolved from the API content root.
    public string RootPath { get; set; } = "media";
    public string PublicBaseUrl { get; set; } = "http://localhost:8080/media";
    public int MaxFileSizeMb { get; set; } = 100;
}
