namespace backend.Services;

public sealed class AiWorkoutPlanProviderRequest
{
    public string Goal { get; init; } = string.Empty;
    public string WorkoutType { get; init; } = string.Empty;
    public int DurationMinutes { get; init; }
    public int RecommendedMainExerciseCount { get; init; }
    public int MaximumMainExerciseCount { get; init; }
    public string FitnessLevel { get; init; } = string.Empty;
    public IReadOnlyList<string> TargetMuscles { get; init; } = [];
    public IReadOnlyList<string> ExcludedExercises { get; init; } = [];
    public bool IncludeWarmup { get; init; } = true;
    public bool IncludeCooldown { get; init; } = true;
    public IReadOnlyList<AiWorkoutCandidateExercise> CandidateExercises { get; init; } = [];
}

public sealed class AiWorkoutCandidateExercise
{
    public int ExerciseCatalogItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? PrimaryMuscle { get; init; }
    public IReadOnlyList<string> SecondaryMuscles { get; init; } = [];
    public string? Equipment { get; init; }
    public string? Difficulty { get; init; }
}

public sealed class AiWorkoutPlanProviderResult
{
    public IReadOnlyList<AiWorkoutPlanProviderSection> Sections { get; init; } = [];
}

public sealed class AiWorkoutPlanProviderSection
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<AiWorkoutPlanProviderExercise> Exercises { get; init; } = [];
}

public sealed class AiWorkoutPlanProviderExercise
{
    public int ExerciseCatalogItemId { get; init; }
    public int Sets { get; init; }
    public string Reps { get; init; } = string.Empty;
    public int RestSeconds { get; init; }
    public string? SuggestedWeight { get; init; }
    public string? Rationale { get; init; }
}

public sealed class AiWorkoutPlanProviderException : Exception
{
    public AiWorkoutPlanProviderException(
        string message,
        string errorCategory = AiWorkoutProviderFailureCategories.ProviderFailure)
        : base(message)
    {
        ErrorCategory = errorCategory;
    }

    public string ErrorCategory { get; }
}

public interface IAiWorkoutPlanProvider
{
    string ProviderName { get; }
    string? ModelName { get; }

    void ValidateConfiguration();

    Task<AiWorkoutPlanProviderResult> GeneratePlanAsync(
        AiWorkoutPlanProviderRequest request,
        CancellationToken cancellationToken = default);
}
