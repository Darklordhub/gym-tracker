namespace backend.Configuration;

public class AiWorkoutGenerationOptions
{
    public const string SectionName = "AiWorkoutGeneration";
    public const int DefaultTimeoutSeconds = 45;

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public int MaxCandidateExercises { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;
}
