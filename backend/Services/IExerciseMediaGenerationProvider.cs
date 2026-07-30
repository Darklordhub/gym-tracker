using backend.Models;

namespace backend.Services;

public enum ExerciseMediaGenerationState
{
    Pending,
    Completed,
    Failed,
}

public sealed class ExerciseMediaGenerationStartResult
{
    public string ProviderJobId { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
}

public sealed class ExerciseMediaGenerationRefreshResult
{
    public ExerciseMediaGenerationState State { get; init; }
    public string? GeneratedVideoUrl { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ExerciseMediaGenerationException : Exception
{
    public ExerciseMediaGenerationException(string message)
        : base(message)
    {
    }
}

public interface IExerciseMediaGenerationProvider
{
    string ProviderName { get; }

    Task<ExerciseMediaGenerationStartResult> StartGenerationAsync(
        ExerciseMediaDraft draft,
        CancellationToken cancellationToken = default);

    Task<ExerciseMediaGenerationRefreshResult> RefreshGenerationStatusAsync(
        ExerciseMediaDraft draft,
        CancellationToken cancellationToken = default);
}
