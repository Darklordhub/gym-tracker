namespace backend.Services;

internal sealed class WorkoutGenerationContext
{
    public string Goal { get; init; } = "general fitness";
    public string WorkoutType { get; init; } = "full-body";
    public string FitnessLevel { get; init; } = "intermediate";
    public int DurationMinutes { get; init; } = 45;
    public IReadOnlyList<string> TargetMuscles { get; init; } = [];
    public HashSet<string> ExcludedExercises { get; init; } = new(StringComparer.Ordinal);
    public bool IncludeWarmup { get; init; }
    public bool IncludeCooldown { get; init; }
    public int SetTarget { get; init; }
    public string RepTarget { get; init; } = "8-12 reps";
    public int RestSeconds { get; init; }
}

internal sealed class CatalogExerciseCandidate
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NameNormalized { get; init; } = string.Empty;
    public string FamilyKey { get; init; } = string.Empty;
    public string? Instructions { get; init; }
    public string? PrimaryMuscle { get; init; }
    public string? PrimaryMuscleNormalized { get; init; }
    public IReadOnlyList<string> Muscles { get; init; } = [];
    public string? Equipment { get; init; }
    public string? Difficulty { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? VideoUrl { get; init; }
    public string FocusGroup { get; init; } = "full-body";
    public string Category { get; init; } = "compound";
    public string MovementPattern { get; init; } = "full body";
    public bool IsBodyweight { get; init; }
    public bool IsAdvanced { get; init; }
}

internal sealed class WorkoutSlot
{
    public string FocusGroup { get; init; } = "full-body";
    public string? TargetMuscle { get; init; }
    public IReadOnlyList<string> PreferredCategories { get; init; } = [];
    public IReadOnlyList<string> PreferredPatterns { get; init; } = [];
}
