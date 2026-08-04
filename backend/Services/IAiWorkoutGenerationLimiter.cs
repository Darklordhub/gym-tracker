namespace backend.Services;

public sealed class AiWorkoutGenerationReservationRequest
{
    public int UserId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string? Model { get; init; }
    public string RequestHash { get; init; } = string.Empty;
    public int CandidateExerciseCount { get; init; }
    public string PromptVersion { get; init; } = string.Empty;
}

public sealed class AiWorkoutGenerationReservation
{
    public int AttemptId { get; init; }
}

public sealed class AiWorkoutGenerationReservationResult
{
    public bool IsReserved { get; init; }
    public AiWorkoutGenerationReservation? Reservation { get; init; }
    public string SafeReason { get; init; } = string.Empty;
}

public interface IAiWorkoutGenerationLimiter
{
    Task<AiWorkoutGenerationReservationResult> ReserveAsync(
        AiWorkoutGenerationReservationRequest request,
        CancellationToken cancellationToken = default);

    Task MarkSucceededAsync(
        AiWorkoutGenerationReservation reservation,
        int selectedExerciseCount,
        CancellationToken cancellationToken = default);

    Task MarkFallbackSucceededAsync(
        AiWorkoutGenerationReservation reservation,
        string errorCategory,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        AiWorkoutGenerationReservation reservation,
        string errorCategory,
        CancellationToken cancellationToken = default);
}
