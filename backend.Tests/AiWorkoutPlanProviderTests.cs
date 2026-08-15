using System.Net;
using System.Text;
using System.Text.Json;
using backend.Configuration;
using backend.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));

        Assert.Equal(AiWorkoutProviderFailureCategories.OpenAiInvalidSets, exception.ErrorCategory);
    }

    [Fact]
    public async Task CompletedResponseWithEmptyOutput_FailsWithSafeCategory()
    {
        var providerEnvelope = JsonSerializer.Serialize(new
        {
            id = "resp_empty_test",
            status = "completed",
            output = Array.Empty<object>(),
            error = (object?)null,
            incomplete_details = (object?)null,
        });
        var provider = CreateProvider(
            HandlerReturning(HttpStatusCode.OK, providerEnvelope),
            enabled: true,
            apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));

        Assert.Equal(AiWorkoutProviderFailureCategories.OpenAiEmptyOutput, exception.ErrorCategory);
        Assert.DoesNotContain("resp_empty_test", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompletedResponseWithMalformedStructuredText_FailsWithJsonParseCategory()
    {
        var providerEnvelope = CreateResponseEnvelope("{\"sections\":[}");
        var provider = CreateProvider(
            HandlerReturning(HttpStatusCode.OK, providerEnvelope),
            enabled: true,
            apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));

        Assert.Equal(AiWorkoutProviderFailureCategories.OpenAiJsonParseFailure, exception.ErrorCategory);
    }

    [Fact]
    public async Task CompletedResponseWithTopLevelOutputText_UsesCompatibilityFallback()
    {
        var providerEnvelope = JsonSerializer.Serialize(new
        {
            id = "resp_top_level_output_text_test",
            status = "completed",
            output = Array.Empty<object>(),
            output_text = CreateProviderOutput(exerciseCatalogItemId: 42),
            error = (object?)null,
            incomplete_details = (object?)null,
        });
        var provider = CreateProvider(
            HandlerReturning(HttpStatusCode.OK, providerEnvelope),
            enabled: true,
            apiKey: "test-key");

        var result = await provider.GeneratePlanAsync(CreateValidRequest());

        Assert.Equal(42, Assert.Single(Assert.Single(result.Sections).Exercises).ExerciseCatalogItemId);
    }

    [Fact]
    public async Task CompletedResponseWithUnknownExerciseId_FailsWithUnknownIdCategory()
    {
        var providerOutput = CreateProviderOutput(exerciseCatalogItemId: 987654);
        var provider = CreateProvider(
            HandlerReturning(HttpStatusCode.OK, CreateResponseEnvelope(providerOutput)),
            enabled: true,
            apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(CreateValidRequest()));

        Assert.Equal(AiWorkoutProviderFailureCategories.OpenAiUnknownExerciseId, exception.ErrorCategory);
        Assert.DoesNotContain("987654", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompletedResponseAboveCombinedDurationCap_FailsWithSafeCapDiagnostics()
    {
        var exerciseIds = Enumerable.Range(1, 10).ToList();
        var providerOutput = JsonSerializer.Serialize(new
        {
            sections = new[]
            {
                new
                {
                    name = "Strength block A",
                    exercises = exerciseIds.Take(5).Select(CreateProviderExerciseOutput).ToList(),
                },
                new
                {
                    name = "Strength block B",
                    exercises = exerciseIds.Skip(5).Select(CreateProviderExerciseOutput).ToList(),
                },
            },
        });
        var logger = new RecordingLogger<OpenAiWorkoutPlanProvider>();
        var provider = CreateProvider(
            HandlerReturning(HttpStatusCode.OK, CreateResponseEnvelope(providerOutput)),
            enabled: true,
            apiKey: "test-key",
            logger: logger);
        var request = CreateValidRequest(
            exerciseIds.Select(id => CreateCandidate(id, $"Candidate {id}")).ToList(),
            durationMinutes: 60,
            recommendedMainExerciseCount: 6,
            maximumMainExerciseCount: 9);

        var exception = await Assert.ThrowsAsync<AiWorkoutPlanProviderException>(() =>
            provider.GeneratePlanAsync(request));

        Assert.Equal(AiWorkoutProviderFailureCategories.OpenAiDurationExerciseCapExceeded, exception.ErrorCategory);
        var logEntry = Assert.Single(logger.Entries, entry =>
            Equals(
                entry.Properties.GetValueOrDefault("ErrorCategory"),
                AiWorkoutProviderFailureCategories.OpenAiDurationExerciseCapExceeded));
        Assert.Equal(60, logEntry.Properties["RequestedDurationMinutes"]);
        Assert.Equal(9, logEntry.Properties["MaximumMainExerciseCount"]);
        Assert.Equal(10, logEntry.Properties["ReturnedMainExerciseCount"]);
        Assert.Equal(2, logEntry.Properties["ProviderSectionCount"]);
        Assert.DoesNotContain(providerOutput, logEntry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompletedResponseAtCombinedDurationCap_Succeeds()
    {
        var exerciseIds = Enumerable.Range(1, 9).ToList();
        var providerOutput = JsonSerializer.Serialize(new
        {
            sections = new[]
            {
                new
                {
                    name = "Strength block A",
                    exercises = exerciseIds.Take(5).Select(CreateProviderExerciseOutput).ToList(),
                },
                new
                {
                    name = "Strength block B",
                    exercises = exerciseIds.Skip(5).Select(CreateProviderExerciseOutput).ToList(),
                },
            },
        });
        var provider = CreateProvider(
            HandlerReturning(HttpStatusCode.OK, CreateResponseEnvelope(providerOutput)),
            enabled: true,
            apiKey: "test-key");
        var request = CreateValidRequest(
            exerciseIds.Select(id => CreateCandidate(id, $"Candidate {id}")).ToList(),
            durationMinutes: 60,
            recommendedMainExerciseCount: 6,
            maximumMainExerciseCount: 9);

        var result = await provider.GeneratePlanAsync(request);

        Assert.Equal(2, result.Sections.Count);
        Assert.Equal(9, result.Sections.Sum(section => section.Exercises.Count));
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
        var splitIndex = providerOutput.Length / 2;
        var providerEnvelope = JsonSerializer.Serialize(new
        {
            id = "resp_valid_test",
            status = "completed",
            error = (object?)null,
            incomplete_details = (object?)null,
            output = new object[]
            {
                new
                {
                    id = "rs_test",
                    type = "reasoning",
                    summary = Array.Empty<object>(),
                },
                new
                {
                    id = "msg_test",
                    type = "message",
                    status = "completed",
                    role = "assistant",
                    content = new[]
                    {
                        new { type = "output_text", text = providerOutput[..splitIndex] },
                        new { type = "output_text", text = providerOutput[splitIndex..] },
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

        var result = await provider.GeneratePlanAsync(CreateValidRequest(
            durationMinutes: 60,
            recommendedMainExerciseCount: 6,
            maximumMainExerciseCount: 9));

        var section = Assert.Single(result.Sections);
        var exercise = Assert.Single(section.Exercises);
        Assert.Equal("Main workout", section.Name);
        Assert.Equal(42, exercise.ExerciseCatalogItemId);
        Assert.Equal("backend-only-test-key", observedApiKey);
        Assert.Equal(new Uri("https://example.test/v1/responses"), observedUri);
        Assert.Contains("\"max_output_tokens\":2000", observedRequestBody, StringComparison.Ordinal);
        using var requestDocument = JsonDocument.Parse(observedRequestBody!);
        var inputItems = requestDocument.RootElement.GetProperty("input");
        Assert.Equal("system", inputItems[0].GetProperty("role").GetString());
        Assert.Equal("developer", inputItems[1].GetProperty("role").GetString());
        Assert.Equal("user", inputItems[2].GetProperty("role").GetString());
        var capInstructions = inputItems[1].GetProperty("content").GetString();
        Assert.Contains("no more than 9 main exercises", capInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("across all provider-generated main sections combined", capInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not add extra main sections", capInstructions, StringComparison.Ordinal);
        Assert.Contains("fewer, higher-quality exercises", capInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Warm-up and cooldown are generated locally", capInstructions, StringComparison.Ordinal);
        var providerInput = inputItems[2]
            .GetProperty("content")
            .GetString();
        Assert.Contains("\"exerciseCatalogItemId\":42", providerInput, StringComparison.Ordinal);
        using var providerInputDocument = JsonDocument.Parse(providerInput!);
        Assert.Equal(60, providerInputDocument.RootElement.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(6, providerInputDocument.RootElement.GetProperty("recommendedMainExerciseCount").GetInt32());
        Assert.Equal(9, providerInputDocument.RootElement.GetProperty("maximumMainExerciseCount").GetInt32());
        Assert.False(providerInputDocument.RootElement.GetProperty("includeWarmup").GetBoolean());
        Assert.False(providerInputDocument.RootElement.GetProperty("includeCooldown").GetBoolean());
        var schemaExerciseArray = requestDocument.RootElement
            .GetProperty("text")
            .GetProperty("format")
            .GetProperty("schema")
            .GetProperty("properties")
            .GetProperty("sections")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("exercises");
        Assert.Equal(9, schemaExerciseArray.GetProperty("maxItems").GetInt32());
        Assert.Equal(1, handler.CallCount);
    }

    private static OpenAiWorkoutPlanProvider CreateProvider(
        HttpMessageHandler handler,
        bool enabled,
        string apiKey,
        string workoutModel = "gpt-5-mini",
        int maxCandidateExercises = 60,
        ILogger<OpenAiWorkoutPlanProvider>? logger = null)
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
            }),
            logger ?? NullLogger<OpenAiWorkoutPlanProvider>.Instance);
    }

    private static StubHttpMessageHandler HandlerReturning(HttpStatusCode statusCode, string content) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        }));

    private static string CreateResponseEnvelope(string outputText) => JsonSerializer.Serialize(new
    {
        id = "resp_test",
        status = "completed",
        output = new[]
        {
            new
            {
                id = "msg_test",
                type = "message",
                status = "completed",
                role = "assistant",
                content = new[] { new { type = "output_text", text = outputText } },
            },
        },
        error = (object?)null,
        incomplete_details = (object?)null,
    });

    private static string CreateProviderOutput(int exerciseCatalogItemId) => JsonSerializer.Serialize(new
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
                        exerciseCatalogItemId,
                        sets = 4,
                        reps = "6-8",
                        restSeconds = 120,
                        suggestedWeight = (string?)null,
                        rationale = (string?)null,
                    },
                },
            },
        },
    });

    private static object CreateProviderExerciseOutput(int exerciseCatalogItemId) => new
    {
        exerciseCatalogItemId,
        sets = 4,
        reps = "6-8",
        restSeconds = 120,
        suggestedWeight = (string?)null,
        rationale = (string?)null,
    };

    private static AiWorkoutPlanProviderRequest CreateValidRequest(
        IReadOnlyList<AiWorkoutCandidateExercise>? candidates = null,
        int durationMinutes = 45,
        int recommendedMainExerciseCount = 5,
        int maximumMainExerciseCount = 8)
    {
        return new AiWorkoutPlanProviderRequest
        {
            Goal = "Build strength",
            WorkoutType = "Full body",
            DurationMinutes = durationMinutes,
            RecommendedMainExerciseCount = recommendedMainExerciseCount,
            MaximumMainExerciseCount = maximumMainExerciseCount,
            FitnessLevel = "Intermediate",
            TargetMuscles = ["Quadriceps", "Glutes"],
            ExcludedExercises = ["Leg press"],
            IncludeWarmup = false,
            IncludeCooldown = false,
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

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), properties));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> Properties);
}
