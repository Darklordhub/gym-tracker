using backend.Configuration;
using backend.Contracts;
using backend.Models;
using backend.Services;
using backend.Tests.TestInfrastructure;
using Microsoft.Extensions.Options;

namespace backend.Tests;

public class ExerciseMediaGenerationSafetyTests
{
    [Fact]
    public void OpenAiProvider_RejectsDisabledGenerationWithoutMakingRequests()
    {
        using var factory = new ExerciseMediaTestFactory();
        var provider = CreateOpenAiProvider(factory, generationEnabled: false, apiKey: "not-used");

        var exception = Assert.Throws<ExerciseMediaGenerationException>(provider.ValidateConfiguration);

        Assert.Equal("Media generation is disabled.", exception.Message);
    }

    [Fact]
    public void OpenAiProvider_RejectsMissingApiKey()
    {
        using var factory = new ExerciseMediaTestFactory();
        var provider = CreateOpenAiProvider(factory, generationEnabled: true, apiKey: string.Empty);

        var exception = Assert.Throws<ExerciseMediaGenerationException>(provider.ValidateConfiguration);

        Assert.Equal("The OpenAI API key is not configured.", exception.Message);
    }

    [Fact]
    public async Task StartGeneration_RejectsPublishedDraftBeforeCallingProvider()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var context = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(context);
        var draft = await factory.CreateDraftAsync(context, exercise, ExerciseMediaDraftStatuses.Published);
        var provider = new FakeExerciseMediaGenerationProvider();
        var service = factory.CreateDraftService(context, [provider], generationEnabled: true);

        var exception = await Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            service.StartGenerationAsync(draft.Id));

        Assert.Contains("Only queued", exception.Message);
        Assert.Equal(0, provider.StartCalls);
    }

    [Fact]
    public async Task StartGeneration_ClaimsDraftSoConcurrentRequestsCannotBothStart()
    {
        using var factory = new ExerciseMediaTestFactory();
        await using var setupContext = factory.CreateDbContext();
        var exercise = await factory.CreateExerciseAsync(setupContext);
        var draft = await factory.CreateDraftAsync(setupContext, exercise);
        var provider = new BlockingExerciseMediaGenerationProvider();

        await using var firstContext = factory.CreateDbContext();
        var firstService = factory.CreateDraftService(firstContext, [provider], generationEnabled: true);
        var firstStart = firstService.StartGenerationAsync(draft.Id);
        await provider.StartEntered.WaitAsync(TimeSpan.FromSeconds(5));

        await using var secondContext = factory.CreateDbContext();
        var secondService = factory.CreateDraftService(secondContext, [provider], generationEnabled: true);
        var secondAttempt = Assert.ThrowsAsync<ExerciseMediaDraftWorkflowException>(() =>
            secondService.StartGenerationAsync(draft.Id));

        provider.Release();
        await firstStart;
        await secondAttempt;

        Assert.Equal(1, provider.StartCalls);
    }

    private static OpenAiExerciseMediaGenerationProvider CreateOpenAiProvider(
        ExerciseMediaTestFactory factory,
        bool generationEnabled,
        string apiKey)
    {
        return new OpenAiExerciseMediaGenerationProvider(
            new HttpClient { BaseAddress = new Uri("https://example.test/") },
            factory.CreateStorageService(),
            Options.Create(new MediaGenerationOptions
            {
                Enabled = generationEnabled,
                Provider = OpenAiExerciseMediaGenerationProvider.Name,
            }),
            Options.Create(new OpenAiVideoGenerationOptions
            {
                ApiKey = apiKey,
                VideoModel = "sora-2",
                VideoSeconds = 4,
                VideoSize = "1280x720",
                TimeoutSeconds = 60,
            }));
    }
}
