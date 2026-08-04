using System.Net;
using System.Text;
using System.Text.Json;
using backend.Configuration;
using backend.Services;
using Microsoft.Extensions.Options;

namespace backend.Tests;

public class AiWorkoutPlanProviderTests
{
    [Fact]
    public async Task DisabledConfiguration_DoesNotRequireApiKeyOrMakeHttpRequest()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called while disabled."));
        var provider = CreateProvider(handler, enabled: false, apiKey: string.Empty);

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));

        Assert.Equal("AI workout generation is disabled.", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task EnabledConfiguration_RejectsMissingApiKeyWithoutMakingHttpRequest()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called without an API key."));
        var provider = CreateProvider(handler, enabled: true, apiKey: string.Empty);

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));

        Assert.Equal("The OpenAI API key is not configured.", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task UnsupportedModel_IsRejectedWithoutMakingHttpRequest()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called with an unsupported model."));
        var provider = CreateProvider(
            handler,
            enabled: true,
            apiKey: "test-key",
            workoutModel: "unsupported-model");

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));

        Assert.Equal("The configured OpenAI workout model is not supported.", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task UndocumentedPlaceholderModel_IsRejectedWithoutMakingHttpRequest()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called with an unsupported placeholder model."));
        var provider = CreateProvider(
            handler,
            enabled: true,
            apiKey: "test-key",
            workoutModel: "gpt-5.5-mini");

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));

        Assert.Equal("The configured OpenAI workout model is not supported.", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task StructuredResponseOutsideWorkoutSafetyBounds_IsRejected()
    {
        var providerOutput = JsonSerializer.Serialize(new
        {
            sections = new[]
            {
                new
                {
                    name = "Main workout",
                    exercises = new[]
                    {
                        new
                        {
                            exerciseCatalogItemId = 42,
                            sets = 9,
                            reps = "6-8",
                            restSeconds = 120,
                            suggestedWeight = (string?)null,
                            rationale = (string?)null,
                        },
                    },
                },
            },
        });
        var providerEnvelope = JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[] { new { type = "output_text", text = providerOutput } },
                },
            },
        });
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(providerEnvelope, Encoding.UTF8, "application/json"),
        }));
        var provider = CreateProvider(handler, enabled: true, apiKey: "test-key");

        await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));
    }

    [Fact]
    public async Task InvalidCandidateConfiguration_IsRejectedBeforeMakingHttpRequest()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called with too many candidates."));
        var provider = CreateProvider(
            handler,
            enabled: true,
            apiKey: "test-key",
            maxCandidateExercises: 1);
        var request = CreateValidRequest(
        [
            CreateCandidate(42, "Barbell back squat"),
            CreateCandidate(43, "Romanian deadlift"),
        ]);

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(request));

        Assert.Equal("AI workout candidate limit must be between 10 and 100.", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ProviderError_DoesNotLeakRawResponseContent()
    {
        const string rawProviderContent = "raw-provider-diagnostic sk-sensitive-value";
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(rawProviderContent, Encoding.UTF8, "application/json"),
        }));
        var provider = CreateProvider(handler, enabled: true, apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));

        Assert.Equal("The AI workout provider rejected the workout request.", exception.Message);
        Assert.DoesNotContain(rawProviderContent, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sk-sensitive-value", exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ValidStructuredResponse_ReturnsInternalPlanContract()
    {
        string? observedApiKey = null;
        string? observedRequestBody = null;
        Uri? observedUri = null;
        var providerOutput = JsonSerializer.Serialize(new
        {
            sections = new[]
            {
                new
                {
                    name = "Main workout",
                    exercises = new[]
                    {
                        new
                        {
                            exerciseCatalogItemId = 42,
                            sets = 4,
                            reps = "6-8",
                            restSeconds = 120,
                            suggestedWeight = (string?)null,
                            rationale = "Primary lower-body strength movement.",
                        },
                    },
                },
            },
        });
        var providerEnvelope = JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[]
                    {
                        new { type = "output_text", text = providerOutput },
                    },
                },
            },
        });
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            observedApiKey = request.Headers.Authorization?.Parameter;
            observedRequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            observedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(providerEnvelope, Encoding.UTF8, "application/json"),
            };
        });
        var provider = CreateProvider(handler, enabled: true, apiKey: "backend-only-test-key");

        var result = await provider.GeneratePlanAsync(CreateValidRequest());

        var section = Assert.Single(result.Sections);
        var exercise = Assert.Single(section.Exercises);
        Assert.Equal("Main workout", section.Name);
        Assert.Equal(42, exercise.ExerciseCatalogItemId);
        Assert.Equal("backend-only-test-key", observedApiKey);
        Assert.Equal(new Uri("https://example.test/v1/responses"), observedUri);
        Assert.Contains("\"max_output_tokens\":2000", observedRequestBody, StringComparison.Ordinal);
        using var requestDocument = JsonDocument.Parse(observedRequestBody!);
        var providerInput = requestDocument.RootElement
            .GetProperty("input")[1]
            .GetProperty("content")
            .GetString();
        Assert.Contains("\"exerciseCatalogItemId\":42", providerInput, StringComparison.Ordinal);
        Assert.Equal(1, handler.CallCount);
    }

    private static OpenAiWorkoutPlanProvider CreateProvider(
        HttpMessageHandler handler,
        bool enabled,
        string apiKey,
        string workoutModel = "gpt-5-mini",
        int maxCandidateExercises = 60)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/v1/"),
            Timeout = TimeSpan.FromSeconds(45),
        };

        return new OpenAiWorkoutPlanProvider(
            httpClient,
            Options.Create(new AiWorkoutGenerationOptions
            {
                Enabled = enabled,
                Provider = OpenAiWorkoutPlanProvider.Name,
                MaxCandidateExercises = maxCandidateExercises,
                TimeoutSeconds = 45,
            }),
            Options.Create(new OpenAiWorkoutGenerationOptions
            {
                ApiKey = apiKey,
                WorkoutModel = workoutModel,
                WorkoutMaxOutputTokens = 2000,
            }));
    }

    private static AiWorkoutPlanProviderRequest CreateValidRequest(
        IReadOnlyList<AiWorkoutCandidateExercise>? candidates = null)
    {
        return new AiWorkoutPlanProviderRequest
        {
            Goal = "Build strength",
            WorkoutType = "Full body",
            DurationMinutes = 45,
            FitnessLevel = "Intermediate",
            TargetMuscles = ["Quadriceps", "Glutes"],
            ExcludedExercises = ["Leg press"],
            CandidateExercises = candidates ?? [CreateCandidate(42, "Barbell back squat")],
        };
    }

    private static AiWorkoutCandidateExercise CreateCandidate(int id, string name)
    {
        return new AiWorkoutCandidateExercise
        {
            ExerciseCatalogItemId = id,
            Name = name,
            PrimaryMuscle = "Quadriceps",
            SecondaryMuscles = ["Glutes", "Hamstrings"],
            Equipment = "Barbell",
            Difficulty = "Intermediate",
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _handler(request, cancellationToken);
        }
    }
}
