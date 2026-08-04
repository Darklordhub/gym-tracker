using System.Data;
using backend.Configuration;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class AiWorkoutGenerationLimiter : IAiWorkoutGenerationLimiter
{
    private const string RateLimitCategory = "RateLimit";
    private const string RateLimitMessage = "AI workout generation is temporarily limited. The local planner was used instead.";

    private readonly AppDbContext _dbContext;
    private readonly AiWorkoutGenerationOptions _options;
    private readonly TimeProvider _timeProvider;

    public AiWorkoutGenerationLimiter(
        AppDbContext dbContext,
        IOptions<AiWorkoutGenerationOptions> options,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<AiWorkoutGenerationReservationResult> ReserveAsync(
        AiWorkoutGenerationReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateReservationRequest(request);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var hourStart = now.AddHours(-1);
        var dayStart = now.Date;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var countableAttempts = _dbContext.AiWorkoutGenerationAttempts
                .Where(attempt => attempt.Status != AiWorkoutGenerationAttemptStatuses.RateLimited);

            var userHourlyCount = await countableAttempts
                .Where(attempt => attempt.UserId == request.UserId && attempt.StartedAtUtc >= hourStart)
                .CountAsync(cancellationToken);
            var userDailyCount = await countableAttempts
                .Where(attempt => attempt.UserId == request.UserId && attempt.StartedAtUtc >= dayStart)
                .CountAsync(cancellationToken);
            var globalDailyCount = await countableAttempts
                .Where(attempt => attempt.StartedAtUtc >= dayStart)
                .CountAsync(cancellationToken);
            var lastUserAttemptAtUtc = await countableAttempts
                .Where(attempt => attempt.UserId == request.UserId)
                .OrderByDescending(attempt => attempt.StartedAtUtc)
                .Select(attempt => (DateTime?)attempt.StartedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var isRateLimited = userHourlyCount >= _options.GetEffectiveMaxGenerationsPerUserPerHour()
                || userDailyCount >= _options.GetEffectiveMaxGenerationsPerUserPerDay()
                || globalDailyCount >= _options.GetEffectiveMaxGlobalGenerationsPerDay()
                || (lastUserAttemptAtUtc.HasValue
                    && lastUserAttemptAtUtc.Value.AddSeconds(_options.GetEffectiveCooldownSeconds()) > now);

            if (isRateLimited)
            {
                _dbContext.AiWorkoutGenerationAttempts.Add(CreateAttempt(
                    request,
                    now,
                    AiWorkoutGenerationAttemptStatuses.RateLimited,
                    RateLimitCategory,
                    RateLimitMessage));
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new AiWorkoutGenerationReservationResult { SafeReason = RateLimitMessage };
            }

            var attempt = CreateAttempt(request, now, AiWorkoutGenerationAttemptStatuses.Reserved, null, null);
            _dbContext.AiWorkoutGenerationAttempts.Add(attempt);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AiWorkoutGenerationReservationResult
            {
                IsReserved = true,
                Reservation = new AiWorkoutGenerationReservation { AttemptId = attempt.Id },
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // The reservation remains denied when the database cannot complete the transaction.
            }
            return new AiWorkoutGenerationReservationResult
            {
                SafeReason = "AI workout generation is temporarily unavailable. The local planner was used instead.",
            };
        }
    }

    public Task MarkSucceededAsync(
        AiWorkoutGenerationReservation reservation,
        int selectedExerciseCount,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            reservation,
            AiWorkoutGenerationAttemptStatuses.Succeeded,
            selectedExerciseCount,
            null,
            cancellationToken);

    public Task MarkFallbackSucceededAsync(
        AiWorkoutGenerationReservation reservation,
        string errorCategory,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            reservation,
            AiWorkoutGenerationAttemptStatuses.FallbackSucceeded,
            null,
            errorCategory,
            cancellationToken);

    public Task MarkFailedAsync(
        AiWorkoutGenerationReservation reservation,
        string errorCategory,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            reservation,
            AiWorkoutGenerationAttemptStatuses.Failed,
            null,
            errorCategory,
            cancellationToken);

    private async Task CompleteAsync(
        AiWorkoutGenerationReservation reservation,
        string status,
        int? selectedExerciseCount,
        string? errorCategory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var attempt = await _dbContext.AiWorkoutGenerationAttempts
            .FirstOrDefaultAsync(item => item.Id == reservation.AttemptId, cancellationToken);
        if (attempt is null || attempt.Status != AiWorkoutGenerationAttemptStatuses.Reserved)
        {
            return;
        }

        attempt.Status = status;
        attempt.SelectedExerciseCount = selectedExerciseCount;
        attempt.ErrorCategory = NormalizeOptional(errorCategory, 80);
        attempt.SafeErrorMessage = GetSafeErrorMessage(errorCategory);
        attempt.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AiWorkoutGenerationAttempt CreateAttempt(
        AiWorkoutGenerationReservationRequest request,
        DateTime now,
        string status,
        string? errorCategory,
        string? safeErrorMessage) => new()
    {
        UserId = request.UserId,
        Provider = request.Provider.Trim(),
        Model = NormalizeOptional(request.Model, 160),
        Status = status,
        RequestHash = request.RequestHash.Trim(),
        CandidateExerciseCount = request.CandidateExerciseCount,
        PromptVersion = request.PromptVersion.Trim(),
        StartedAtUtc = now,
        CreatedAtUtc = now,
        ErrorCategory = NormalizeOptional(errorCategory, 80),
        SafeErrorMessage = NormalizeOptional(safeErrorMessage, 400),
    };

    private static void ValidateReservationRequest(AiWorkoutGenerationReservationRequest request)
    {
        if (request.UserId <= 0
            || string.IsNullOrWhiteSpace(request.Provider)
            || request.Provider.Length > 80
            || string.IsNullOrWhiteSpace(request.RequestHash)
            || request.RequestHash.Length != 64
            || request.CandidateExerciseCount is < 1 or > AiWorkoutGenerationOptions.MaxCandidateExercisesLimit
            || string.IsNullOrWhiteSpace(request.PromptVersion)
            || request.PromptVersion.Length > 80)
        {
            throw new ArgumentException("AI workout generation reservation input is invalid.", nameof(request));
        }
    }

    private static string? NormalizeOptional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    private static string? GetSafeErrorMessage(string? errorCategory) => errorCategory switch
    {
        "Cancelled" => "The AI workout provider request was cancelled.",
        "LocalFallbackFailure" => "The AI workout provider and local planner could not complete the workout plan.",
        "ProviderFailure" => "The AI workout provider did not produce a usable workout plan.",
        _ => "The AI workout generation attempt did not complete.",
    };
}
