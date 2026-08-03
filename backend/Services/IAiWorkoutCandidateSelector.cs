using backend.Dtos;

namespace backend.Services;

internal sealed class AiWorkoutCandidate
{
    public int ExerciseCatalogItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? TargetMuscle { get; init; }
    public IReadOnlyList<string> SecondaryMuscles { get; init; } = [];
    public string? Equipment { get; init; }
    public string? Difficulty { get; init; }
    public string FocusGroup { get; init; } = string.Empty;
    public string MovementPattern { get; init; } = string.Empty;
    public int Score { get; init; }
    public int Rank { get; init; }
    public IReadOnlyList<string> MatchReasons { get; init; } = [];
    public bool IsBodyweight { get; init; }
    public bool IsAdvanced { get; init; }
    public bool WasRecentlyUsed { get; init; }
    public bool MeetsMinimumSuitability { get; init; }
}

internal interface IAiWorkoutCandidateSelector
{
    Task<IReadOnlyList<AiWorkoutCandidate>> SelectCandidatesAsync(
        string userId,
        AiWorkoutGenerateRequest request,
        CancellationToken cancellationToken = default);
}
