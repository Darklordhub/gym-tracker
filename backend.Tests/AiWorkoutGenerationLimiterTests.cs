using backend.Configuration;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Tests;

public class AiWorkoutGenerationLimiterTests
{
    [Fact]
    public async Task ReserveAsync_EnforcesPerUserHourlyLimit()
    {
        await using var database = await LimiterTestDatabase.CreateAsync();
        var now = DateTime.UtcNow;
        database.Context.AiWorkoutGenerationAttempts.Add(CreateAttempt(userId: 1, now.AddMinutes(-10)));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateLimiter(maxPerUserHour: 1).ReserveAsync(RequestFor(1));

        Assert.False(result.IsReserved);
        var attempts = await database.Context.AiWorkoutGenerationAttempts.ToListAsync();
        Assert.Equal(2, attempts.Count);
        Assert.Equal(AiWorkoutGenerationAttemptStatuses.RateLimited, attempts.Single(attempt => attempt.Status == AiWorkoutGenerationAttemptStatuses.RateLimited).Status);
    }

    [Fact]
    public async Task ReserveAsync_EnforcesPerUserDailyLimit()
    {
        await using var database = await LimiterTestDatabase.CreateAsync();
        database.Context.AiWorkoutGenerationAttempts.Add(CreateAttempt(userId: 1, DateTime.UtcNow.AddHours(-3)));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateLimiter(maxPerUserDay: 1).ReserveAsync(RequestFor(1));

        Assert.False(result.IsReserved);
        Assert.Contains("local planner", result.SafeReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReserveAsync_EnforcesGlobalDailyLimit()
    {
        await using var database = await LimiterTestDatabase.CreateAsync();
        database.Context.AiWorkoutGenerationAttempts.Add(CreateAttempt(userId: 2, DateTime.UtcNow.AddHours(-3)));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateLimiter(maxGlobalPerDay: 1).ReserveAsync(RequestFor(1));

        Assert.False(result.IsReserved);
    }

    [Fact]
    public async Task ReserveAsync_EnforcesPerUserCooldown()
    {
        await using var database = await LimiterTestDatabase.CreateAsync();
        database.Context.AiWorkoutGenerationAttempts.Add(CreateAttempt(userId: 1, DateTime.UtcNow.AddSeconds(-30)));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateLimiter(cooldownSeconds: 120).ReserveAsync(RequestFor(1));

        Assert.False(result.IsReserved);
    }

    [Fact]
    public async Task CompletedAttempt_StoresOnlySafeAuditFields()
    {
        await using var database = await LimiterTestDatabase.CreateAsync();
        var limiter = database.CreateLimiter();
        var reservation = Assert.IsType<AiWorkoutGenerationReservation>((await limiter.ReserveAsync(RequestFor(1))).Reservation);

        await limiter.MarkFallbackSucceededAsync(
            reservation,
            "ProviderFailure");

        var attempt = Assert.Single(await database.Context.AiWorkoutGenerationAttempts.ToListAsync());
        Assert.Equal(AiWorkoutGenerationAttemptStatuses.FallbackSucceeded, attempt.Status);
        Assert.Equal(64, attempt.RequestHash.Length);
        Assert.Null(attempt.InputTokens);
        Assert.Null(attempt.OutputTokens);
        Assert.Null(attempt.TotalTokens);
        Assert.Null(attempt.EstimatedCost);
        Assert.DoesNotContain("raw-provider-response", attempt.SafeErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    private static AiWorkoutGenerationReservationRequest RequestFor(int userId) => new()
    {
        UserId = userId,
        Provider = "OpenAI",
        Model = "gpt-5-mini",
        RequestHash = new string('a', 64),
        CandidateExerciseCount = 12,
        PromptVersion = "workout-plan-v1",
    };

    private static AiWorkoutGenerationAttempt CreateAttempt(int userId, DateTime startedAtUtc) => new()
    {
        UserId = userId,
        Provider = "OpenAI",
        Model = "gpt-5-mini",
        RequestHash = new string('b', 64),
        CandidateExerciseCount = 12,
        PromptVersion = "workout-plan-v1",
        Status = AiWorkoutGenerationAttemptStatuses.Succeeded,
        StartedAtUtc = startedAtUtc,
        CompletedAtUtc = startedAtUtc.AddSeconds(2),
        CreatedAtUtc = startedAtUtc,
    };

    private sealed class LimiterTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private LimiterTestDatabase(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<LimiterTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            return new LimiterTestDatabase(connection, context);
        }

        public AiWorkoutGenerationLimiter CreateLimiter(
            int maxPerUserDay = 5,
            int maxPerUserHour = 10,
            int maxGlobalPerDay = 50,
            int cooldownSeconds = 0) => new(
                Context,
                Options.Create(new AiWorkoutGenerationOptions
                {
                    Enabled = true,
                    MaxGenerationsPerUserPerDay = maxPerUserDay,
                    MaxGenerationsPerUserPerHour = maxPerUserHour,
                    MaxGlobalGenerationsPerDay = maxGlobalPerDay,
                    CooldownSeconds = cooldownSeconds,
                }),
                TimeProvider.System);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
