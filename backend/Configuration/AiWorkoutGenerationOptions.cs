namespace backend.Configuration;

public class AiWorkoutGenerationOptions
{
    public const string SectionName = "AiWorkoutGeneration";
    public const int DefaultTimeoutSeconds = 45;
    public const int DefaultMaxCandidateExercises = 60;
    public const int MinCandidateExercises = 10;
    public const int MaxCandidateExercisesLimit = 100;

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public int MaxCandidateExercises { get; set; } = DefaultMaxCandidateExercises;
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    public int GetEffectiveMaxCandidateExercises()
    {
        return Math.Clamp(MaxCandidateExercises, MinCandidateExercises, MaxCandidateExercisesLimit);
    }
}
