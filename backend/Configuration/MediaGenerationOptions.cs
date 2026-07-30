namespace backend.Configuration;

public class MediaGenerationOptions
{
    public const string SectionName = "MediaGeneration";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public int MaxJobsPerDay { get; set; } = 10;
    public int MaxJobsPerHour { get; set; } = 3;
    public int MaxJobsPerDraftPerDay { get; set; } = 2;
    public int CooldownSeconds { get; set; } = 120;
}
