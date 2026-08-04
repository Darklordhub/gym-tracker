namespace backend.Configuration;

public class AiWorkoutGenerationOptions
{
    public const string SectionName = "AiWorkoutGeneration";
    public const int DefaultTimeoutSeconds = 45;
    public const int DefaultMaxCandidateExercises = 60;
    public const int DefaultMaxGenerationsPerUserPerDay = 5;
    public const int DefaultMaxGenerationsPerUserPerHour = 2;
    public const int DefaultMaxGlobalGenerationsPerDay = 50;
    public const int DefaultCooldownSeconds = 120;
    public const int MinCandidateExercises = 10;
    public const int MaxCandidateExercisesLimit = 100;

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public int MaxCandidateExercises { get; set; } = DefaultMaxCandidateExercises;
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;
    public int MaxGenerationsPerUserPerDay { get; set; } = DefaultMaxGenerationsPerUserPerDay;
    public int MaxGenerationsPerUserPerHour { get; set; } = DefaultMaxGenerationsPerUserPerHour;
    public int MaxGlobalGenerationsPerDay { get; set; } = DefaultMaxGlobalGenerationsPerDay;
    public int CooldownSeconds { get; set; } = DefaultCooldownSeconds;

    public int GetEffectiveMaxCandidateExercises()
    {
        return Math.Clamp(MaxCandidateExercises, MinCandidateExercises, MaxCandidateExercisesLimit);
    }

    public int GetEffectiveMaxGenerationsPerUserPerDay() => Math.Clamp(MaxGenerationsPerUserPerDay, 1, 1000);

    public int GetEffectiveMaxGenerationsPerUserPerHour() => Math.Clamp(MaxGenerationsPerUserPerHour, 1, 1000);

    public int GetEffectiveMaxGlobalGenerationsPerDay() => Math.Clamp(MaxGlobalGenerationsPerDay, 1, 100000);

    public int GetEffectiveCooldownSeconds() => Math.Clamp(CooldownSeconds, 0, 86400);
}
