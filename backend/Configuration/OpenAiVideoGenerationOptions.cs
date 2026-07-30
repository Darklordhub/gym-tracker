namespace backend.Configuration;

public class OpenAiVideoGenerationOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string VideoModel { get; set; } = "sora-2";
    public int VideoSeconds { get; set; } = 4;
    public string VideoSize { get; set; } = "1280x720";
    public int TimeoutSeconds { get; set; } = 60;
}
