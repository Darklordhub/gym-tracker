namespace backend.Configuration;

public class MediaGenerationOptions
{
    public const string SectionName = "MediaGeneration";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
}
