namespace backend.Configuration;

public class ExerciseMediaEnrichmentOptions
{
    public const string SectionName = "ExerciseMediaEnrichment";

    public int MaxItems { get; set; } = 250;
    public ExerciseMediaProviderOptions Wger { get; set; } = new();
    public ExerciseDbMediaProviderOptions ExerciseDb { get; set; } = new();
}

public class ExerciseMediaProviderOptions
{
    public bool Enabled { get; set; } = true;
}

public class ExerciseDbMediaProviderOptions : ExerciseMediaProviderOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ExercisePath { get; set; } = "/exercises";
    public string? AssetBaseUrl { get; set; }
    public int MaxDatasetItems { get; set; } = 2500;
    public Dictionary<string, string> RequestHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
