using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Configuration;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class OpenAiWorkoutPlanProvider : IAiWorkoutPlanProvider
{
    public const string Name = "OpenAI";

    private const int MaxResponseContentLength = 2 * 1024 * 1024;

    private static readonly HashSet<string> SupportedWorkoutModels = new(StringComparer.Ordinal)
    {
        "gpt-5-mini",
        "gpt-5.5-mini",
        "gpt-5-mini",
        "gpt-4o-mini",
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonElement WorkoutPlanSchema = CreateWorkoutPlanSchema();

    private readonly HttpClient _httpClient;
    private readonly AiWorkoutGenerationOptions _generationOptions;
    private readonly OpenAiWorkoutGenerationOptions _openAiOptions;

    public OpenAiWorkoutPlanProvider(
        HttpClient httpClient,
        IOptions<AiWorkoutGenerationOptions> generationOptions,
        IOptions<OpenAiWorkoutGenerationOptions> openAiOptions)
    {
        _httpClient = httpClient;
        _generationOptions = generationOptions.Value;
        _openAiOptions = openAiOptions.Value;
    }

    public string ProviderName => Name;
    public string? ModelName => string.IsNullOrWhiteSpace(_openAiOptions.WorkoutModel)
        ? null
        : _openAiOptions.WorkoutModel.Trim();

    public async Task<AiWorkoutPlanProviderResult> GeneratePlanAsync(
        AiWorkoutPlanProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        ValidateRequest(request);

        var providerInput = JsonSerializer.Serialize(request, JsonOptions);
        var payload = new
        {
            model = _openAiOptions.WorkoutModel.Trim(),
            store = false,
            max_output_tokens = _openAiOptions.WorkoutMaxOutputTokens,
            input = new[]
            {
                new
                {
                    role = "system",
                    content = "Build a safe workout plan using only the supplied candidate exercise IDs. Return only the requested structured output and never invent exercise IDs.",
                },
                new
                {
                    role = "user",
                    content = providerInput,
                },
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "workout_plan",
                    strict = true,
                    schema = WorkoutPlanSchema,
                },
            },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey.Trim());
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_generationOptions.TimeoutSeconds));

        try
        {
            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            EnsureSuccessfulResponse(response);

            if (response.Content.Headers.ContentLength is > MaxResponseContentLength)
            {
                throw new AiWorkoutPlanProviderException("The AI workout provider returned an invalid response.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            var providerResponse = await JsonSerializer.DeserializeAsync<OpenAiResponse>(
                responseStream,
                JsonOptions,
                timeoutSource.Token);

            var contentItems = providerResponse?.Output?
                .Where(item => string.Equals(item.Type, "message", StringComparison.Ordinal))
                .SelectMany(item => item.Content ?? [])
                .ToList() ?? [];

            if (contentItems.Any(item => string.Equals(item.Type, "refusal", StringComparison.Ordinal)))
            {
                throw new AiWorkoutPlanProviderException("The AI workout provider declined the workout request.");
            }

            if (!string.Equals(providerResponse?.Status, "completed", StringComparison.Ordinal))
            {
                throw new AiWorkoutPlanProviderException("The AI workout provider did not complete the workout plan.");
            }

            var outputText = contentItems
                .FirstOrDefault(item => string.Equals(item.Type, "output_text", StringComparison.Ordinal))?
                .Text;
            if (string.IsNullOrWhiteSpace(outputText) || outputText.Length > MaxResponseContentLength)
            {
                throw new AiWorkoutPlanProviderException("The AI workout provider returned an invalid response.");
            }

            var result = JsonSerializer.Deserialize<AiWorkoutPlanProviderResult>(outputText, JsonOptions);
            if (result is null)
            {
                throw new AiWorkoutPlanProviderException("The AI workout provider returned an invalid workout plan.");
            }

            ValidateResult(result, request);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiWorkoutPlanProviderException("The AI workout provider request timed out.");
        }
        catch (HttpRequestException)
        {
            throw new AiWorkoutPlanProviderException("The AI workout provider is currently unavailable.");
        }
        catch (NotSupportedException)
        {
            throw new AiWorkoutPlanProviderException("The AI workout provider returned an invalid response.");
        }
        catch (JsonException)
        {
            throw new AiWorkoutPlanProviderException("The AI workout provider returned an invalid response.");
        }
    }

    public void ValidateConfiguration()
    {
        if (!_generationOptions.Enabled)
        {
            throw new AiWorkoutPlanProviderException("AI workout generation is disabled.");
        }

        if (!string.Equals(_generationOptions.Provider?.Trim(), Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new AiWorkoutPlanProviderException("The configured AI workout provider is not available.");
        }

        if (_generationOptions.MaxCandidateExercises is
            < AiWorkoutGenerationOptions.MinCandidateExercises or
            > AiWorkoutGenerationOptions.MaxCandidateExercisesLimit)
        {
            throw new AiWorkoutPlanProviderException("AI workout candidate limit must be between 10 and 100.");
        }

        if (_generationOptions.TimeoutSeconds is < 5 or > 120)
        {
            throw new AiWorkoutPlanProviderException("AI workout timeout seconds must be between 5 and 120.");
        }

        if (string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
        {
            throw new AiWorkoutPlanProviderException("The OpenAI API key is not configured.");
        }

        var model = _openAiOptions.WorkoutModel?.Trim();
        if (string.IsNullOrWhiteSpace(model) || !SupportedWorkoutModels.Contains(model))
        {
            throw new AiWorkoutPlanProviderException("The configured OpenAI workout model is not supported.");
        }

        if (_openAiOptions.WorkoutMaxOutputTokens is < 256 or > 16000)
        {
            throw new AiWorkoutPlanProviderException("OpenAI workout max output tokens must be between 256 and 16000.");
        }
    }

    private void ValidateRequest(AiWorkoutPlanProviderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Goal) || request.Goal.Length > 80 ||
            string.IsNullOrWhiteSpace(request.WorkoutType) || request.WorkoutType.Length > 60 ||
            string.IsNullOrWhiteSpace(request.FitnessLevel) || request.FitnessLevel.Length > 40 ||
            request.DurationMinutes is < 15 or > 180)
        {
            throw new AiWorkoutPlanProviderException("The AI workout provider input is invalid.");
        }

        if (request.TargetMuscles is null ||
            request.TargetMuscles.Count > 20 ||
            request.TargetMuscles.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 80) ||
            request.ExcludedExercises is null ||
            request.ExcludedExercises.Count > 100 ||
            request.ExcludedExercises.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 160) ||
            request.CandidateExercises is null ||
            request.CandidateExercises.Count is < 1 ||
            request.CandidateExercises.Count > _generationOptions.MaxCandidateExercises)
        {
            throw new AiWorkoutPlanProviderException("The AI workout provider input is invalid.");
        }

        var candidateIds = new HashSet<int>();
        foreach (var candidate in request.CandidateExercises)
        {
            if (candidate is null ||
                candidate.ExerciseCatalogItemId <= 0 ||
                !candidateIds.Add(candidate.ExerciseCatalogItemId) ||
                string.IsNullOrWhiteSpace(candidate.Name) ||
                candidate.Name.Length > 160 ||
                candidate.PrimaryMuscle?.Length > 80 ||
                candidate.Equipment?.Length > 80 ||
                candidate.Difficulty?.Length > 40 ||
                candidate.SecondaryMuscles is null ||
                candidate.SecondaryMuscles.Count > 12 ||
                candidate.SecondaryMuscles.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 80))
            {
                throw new AiWorkoutPlanProviderException("The AI workout provider input is invalid.");
            }
        }
    }

    private static void ValidateResult(
        AiWorkoutPlanProviderResult result,
        AiWorkoutPlanProviderRequest request)
    {
        if (result.Sections is null || result.Sections.Count is < 1 or > 10)
        {
            throw new AiWorkoutPlanProviderException("The AI workout provider returned an invalid workout plan.");
        }

        var candidateIds = request.CandidateExercises
            .Select(candidate => candidate.ExerciseCatalogItemId)
            .ToHashSet();
        var selectedIds = new HashSet<int>();

        foreach (var section in result.Sections)
        {
            if (section is null ||
                string.IsNullOrWhiteSpace(section.Name) ||
                section.Name.Length > 80 ||
                section.Exercises is null ||
                section.Exercises.Count is < 1)
            {
                throw new AiWorkoutPlanProviderException("The AI workout provider returned an invalid workout plan.");
            }

            foreach (var exercise in section.Exercises)
            {
                if (exercise is null ||
                    !candidateIds.Contains(exercise.ExerciseCatalogItemId) ||
                    !selectedIds.Add(exercise.ExerciseCatalogItemId) ||
                    exercise.Sets is < 1 or > 20 ||
                    string.IsNullOrWhiteSpace(exercise.Reps) ||
                    exercise.Reps.Length > 40 ||
                    exercise.RestSeconds is < 0 or > 900 ||
                    exercise.SuggestedWeight?.Length > 60 ||
                    exercise.Rationale?.Length > 240)
                {
                    throw new AiWorkoutPlanProviderException("The AI workout provider returned an invalid workout plan.");
                }
            }
        }
    }

    private static void EnsureSuccessfulResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "The AI workout provider rejected the configured credentials.",
            HttpStatusCode.TooManyRequests =>
                "The AI workout provider is temporarily rate limited.",
            HttpStatusCode.BadRequest =>
                "The AI workout provider rejected the workout request.",
            _ =>
                "The AI workout provider request failed.",
        };

        throw new AiWorkoutPlanProviderException(message);
    }

    private static JsonElement CreateWorkoutPlanSchema()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "type": "object",
              "properties": {
                "sections": {
                  "type": "array",
                  "minItems": 1,
                  "maxItems": 10,
                  "items": {
                    "type": "object",
                    "properties": {
                      "name": { "type": "string", "maxLength": 80 },
                      "exercises": {
                        "type": "array",
                        "minItems": 1,
                        "items": {
                          "type": "object",
                          "properties": {
                            "exerciseCatalogItemId": { "type": "integer" },
                            "sets": { "type": "integer", "minimum": 1, "maximum": 20 },
                            "reps": { "type": "string", "maxLength": 40 },
                            "restSeconds": { "type": "integer", "minimum": 0, "maximum": 900 },
                            "suggestedWeight": { "type": ["string", "null"] },
                            "rationale": { "type": ["string", "null"] }
                          },
                          "required": [
                            "exerciseCatalogItemId",
                            "sets",
                            "reps",
                            "restSeconds",
                            "suggestedWeight",
                            "rationale"
                          ],
                          "additionalProperties": false
                        }
                      }
                    },
                    "required": ["name", "exercises"],
                    "additionalProperties": false
                  }
                }
              },
              "required": ["sections"],
              "additionalProperties": false
            }
            """);

        return document.RootElement.Clone();
    }

    private sealed class OpenAiResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("output")]
        public List<OpenAiOutputItem>? Output { get; init; }
    }

    private sealed class OpenAiOutputItem
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("content")]
        public List<OpenAiContentItem>? Content { get; init; }
    }

    private sealed class OpenAiContentItem
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
}
