using backend.Models;
using backend.Services;
using backend.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public class ExerciseMediaGenerationLimitTests
{
    [Fact]
    public async Task StartGeneration_BlocksAfterDailyLimitWithoutCallingProvider()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        await factory.CreateGenerationAttemptAsync(context, draft, DateTime.UtcNow.AddHours(-2));
        var provider = new FakeExerciseMediaGenerationProvider();
        var service = factory.CreateDraftService(
            context,
            [provider],
            generationEnabled: true,
            maxJobsPerDay: 1,
            maxJobsPerHour: 3,
            maxJobsPerDraftPerDay: 2,
            cooldownSeconds: 0);

        var exception = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.StartGenerationAsync(draft.Id, requestedByUserId: 42));

        Assert.Equal("Generation limit reached. Try again later.", exception.Message);
        Assert.Equal(0, provider.StartCalls);
        Assert.Equal(1, await context.ExerciseMediaGenerationAttempts.CountAsync(
            attempt => attempt.Status == ExerciseMediaGenerationAttemptStatuses.Blocked));
    }

    [Fact]
    public async Task StartGeneration_BlocksAfterHourlyLimit()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        await factory.CreateGenerationAttemptAsync(context, draft, DateTime.UtcNow.AddMinutes(-10));
        var provider = new FakeExerciseMediaGenerationProvider();
        var service = factory.CreateDraftService(
            context,
            [provider],
            generationEnabled: true,
            maxJobsPerDay: 10,
            maxJobsPerHour: 1,
            maxJobsPerDraftPerDay: 2,
            cooldownSeconds: 0);

        var exception = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.StartGenerationAsync(draft.Id));

        Assert.Equal("Generation limit reached. Try again later.", exception.Message);
        Assert.Equal(0, provider.StartCalls);
    }

    [Fact]
    public async Task StartGeneration_BlocksAfterPerDraftDailyLimit()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        await factory.CreateGenerationAttemptAsync(context, draft, DateTime.UtcNow.AddHours(-3));
        var provider = new FakeExerciseMediaGenerationProvider();
        var service = factory.CreateDraftService(
            context,
            [provider],
            generationEnabled: true,
            maxJobsPerDay: 10,
            maxJobsPerHour: 10,
            maxJobsPerDraftPerDay: 1,
            cooldownSeconds: 0);

        var exception = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.StartGenerationAsync(draft.Id));

        Assert.Equal("Generation limit reached. Try again later.", exception.Message);
        Assert.Equal(0, provider.StartCalls);
    }

    [Fact]
    public async Task StartGeneration_BlocksDuringDraftCooldown()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        await factory.CreateGenerationAttemptAsync(context, draft, DateTime.UtcNow.AddSeconds(-30));
        var provider = new FakeExerciseMediaGenerationProvider();
        var service = factory.CreateDraftService(
            context,
            [provider],
            generationEnabled: true,
            maxJobsPerDay: 10,
            maxJobsPerHour: 10,
            maxJobsPerDraftPerDay: 10,
            cooldownSeconds: 120);

        var exception = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.StartGenerationAsync(draft.Id));

        Assert.Equal("Generation limit reached. Try again later.", exception.Message);
        Assert.Equal(0, provider.StartCalls);
    }

    [Fact]
    public async Task StartGeneration_AllowsProviderCallAndRecordsStartedAttempt()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        var provider = new FakeExerciseMediaGenerationProvider();
        var service = factory.CreateDraftService(
            context,
            [provider],
            generationEnabled: true,
            maxJobsPerDay: 10,
            maxJobsPerHour: 10,
            maxJobsPerDraftPerDay: 10,
            cooldownSeconds: 0);

        var result = await service.StartGenerationAsync(draft.Id, requestedByUserId: 42);

        Assert.NotNull(result);
        Assert.Equal(1, provider.StartCalls);
        var attempt = await context.ExerciseMediaGenerationAttempts.SingleAsync();
        Assert.Equal(ExerciseMediaGenerationAttemptStatuses.Started, attempt.Status);
        Assert.Equal(42, attempt.RequestedByUserId);
        Assert.Equal("Fake", attempt.Provider);
        Assert.Equal("test-model", attempt.Model);
        Assert.Equal("test-job", attempt.ProviderJobId);
    }

    [Fact]
    public async Task StartGeneration_RecordsFailedAttemptWhenProviderStartFails()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        var provider = new FailingStartProvider();
        var service = factory.CreateDraftService(
            context,
            [provider],
            generationEnabled: true,
            maxJobsPerDay: 10,
            maxJobsPerHour: 10,
            maxJobsPerDraftPerDay: 10,
            cooldownSeconds: 0);

        var exception = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.StartGenerationAsync(draft.Id));

        Assert.Equal("Provider start failed.", exception.Message);
        var attempt = await context.ExerciseMediaGenerationAttempts.SingleAsync();
        Assert.Equal(ExerciseMediaGenerationAttemptStatuses.Failed, attempt.Status);
        Assert.Equal("Provider start failed.", attempt.ErrorMessage);
        Assert.NotNull(attempt.CompletedAt);
    }

    [Fact]
    public async Task RefreshGenerationStatus_RecordsCompletedAttempt()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise);
        var provider = new CompletedRefreshProvider();
        var service = factory.CreateDraftService(
            context,
            [provider],
            generationEnabled: true,
            maxJobsPerDay: 10,
            maxJobsPerHour: 10,
            maxJobsPerDraftPerDay: 10,
            cooldownSeconds: 0);

        await service.StartGenerationAsync(draft.Id);
        var refreshed = await service.RefreshGenerationStatusAsync(draft.Id);

        Assert.NotNull(refreshed);
        Assert.Equal(ExerciseMediaDraftStatuses.NeedsReview, refreshed.Status);
        var attempt = await context.ExerciseMediaGenerationAttempts.SingleAsync();
        Assert.Equal(ExerciseMediaGenerationAttemptStatuses.Completed, attempt.Status);
        Assert.NotNull(attempt.CompletedAt);
    }

    private sealed class FailingStartProvider : FakeExerciseMediaGenerationProvider
    {
        public override async Task<ExerciseMediaGenerationStartResult> StartGenerationAsync(
            ExerciseMediaDraft draft,
            CancellationToken cancellationToken = default)
        {
            await base.StartGenerationAsync(draft, cancellationToken);
            throw new ExerciseMediaGenerationException("Provider start failed.");
        }
    }

    private sealed class CompletedRefreshProvider : FakeExerciseMediaGenerationProvider
    {
        public override Task<ExerciseMediaGenerationRefreshResult> RefreshGenerationStatusAsync(
            ExerciseMediaDraft draft,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExerciseMediaGenerationRefreshResult
            {
                State = ExerciseMediaGenerationState.Completed,
                GeneratedVideoUrl = "/api/admin/exercise-catalog/media-studio/1/video",
            });
        }
    }
}
