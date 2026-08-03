namespace backend.Configuration;

public class OpenAiWorkoutGenerationOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string WorkoutModel { get; set; } = "gpt-5-mini";
    public int WorkoutMaxOutputTokens { get; set; } = 2000;
}
