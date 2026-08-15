using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class OpenAiWorkoutPlanProvider : IAiWorkoutPlanProvider
{
    public const string Name = "OpenAI";

    private const int MaxResponseContentLength = 2 * 1024 * 1024;

    private static readonly HashSet<string> SupportedWorkoutModels = new(StringComparer.Ordinal)
    {
        "gpt-5-mini",
        "gpt-4o-mini",
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly AiWorkoutGenerationOptions _generationOptions;
    private readonly OpenAiWorkoutGenerationOptions _openAiOptions;
    private readonly ILogger<OpenAiWorkoutPlanProvider> _logger;

    public OpenAiWorkoutPlanProvider(
        HttpClient httpClient,
        IOptions<AiWorkoutGenerationOptions> generationOptions,
        IOptions<OpenAiWorkoutGenerationOptions> openAiOptions,
        ILogger<OpenAiWorkoutPlanProvider> logger)
    {
        _httpClient = httpClient;
        _generationOptions = generationOptions.Value;
        _openAiOptions = openAiOptions.Value;
        _logger = logger;
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
        var workoutPlanSchema = CreateWorkoutPlanSchema(request.MaximumMainExerciseCount);
        var capInstructions = FormattableString.Invariant(
            $"The workout duration is {request.DurationMinutes} minutes. Select no more than {request.MaximumMainExerciseCount} main exercises total across all provider-generated main sections combined. Aim for {request.RecommendedMainExerciseCount} main exercises when that produces a high-quality plan. Do not add extra main sections to bypass the total exercise cap. Prefer fewer, higher-quality exercises over many movements. Warm-up and cooldown are generated locally by STRIDE and must not be included.");
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
                    content = "Build a safe catalog-backed working workout plan using only the supplied candidate exercise IDs. Return only the requested structured output, never invent exercise IDs, and never include warm-up or cooldown sections.",
                },
                new
                {
                    role = "developer",
                    content = capInstructions,
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
                    schema = workoutPlanSchema,
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
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiSchemaMismatch,
                    "The AI workout provider returned an invalid response.",
                    httpStatus: response.StatusCode);
            }

            OpenAiResponse? providerResponse;
            try
            {
                await using var responseStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
                providerResponse = await JsonSerializer.DeserializeAsync<OpenAiResponse>(
                    responseStream,
                    JsonOptions,
                    timeoutSource.Token);
            }
            catch (JsonException)
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiSchemaMismatch,
                    "The AI workout provider returned an invalid response.",
                    httpStatus: response.StatusCode);
            }
            catch (NotSupportedException)
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiSchemaMismatch,
                    "The AI workout provider returned an invalid response.",
                    httpStatus: response.StatusCode);
            }

            var outputItemCount = providerResponse?.Output?.Count ?? 0;
            _logger.LogInformation(
                "OpenAI workout response received. HttpStatus {HttpStatus}, ResponseId {ResponseId}, ResponseStatus {ResponseStatus}, OutputItemCount {OutputItemCount}.",
                (int)response.StatusCode,
                providerResponse?.Id,
                providerResponse?.Status,
                outputItemCount);

            if (providerResponse?.Error is not null)
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiHttpFailure,
                    "The AI workout provider request failed.",
                    providerResponse,
                    response.StatusCode);
            }

            var contentItems = providerResponse?.Output?
                .Where(item => string.Equals(item.Type, "message", StringComparison.Ordinal))
                .SelectMany(item => item.Content ?? [])
                .ToList() ?? [];

            if (contentItems.Any(item => string.Equals(item.Type, "refusal", StringComparison.Ordinal)))
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiValidationFailure,
                    "The AI workout provider declined the workout request.",
                    providerResponse,
                    response.StatusCode);
            }

            var nestedOutputTextParts = contentItems
                .Where(item => string.Equals(item.Type, "output_text", StringComparison.Ordinal))
                .Select(item => item.Text)
                .Where(text => text is not null)
                .Cast<string>()
                .ToList();
            var outputText = nestedOutputTextParts.Count > 0
                ? string.Concat(nestedOutputTextParts)
                : providerResponse?.OutputText;

            if (string.IsNullOrWhiteSpace(outputText))
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiEmptyOutput,
                    "The AI workout provider returned an invalid response.",
                    providerResponse,
                    response.StatusCode);
            }

            if (!string.Equals(providerResponse?.Status, "completed", StringComparison.Ordinal))
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiValidationFailure,
                    "The AI workout provider did not complete the workout plan.",
                    providerResponse,
                    response.StatusCode);
            }

            if (outputText.Length > MaxResponseContentLength)
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiSchemaMismatch,
                    "The AI workout provider returned an invalid response.",
                    providerResponse,
                    response.StatusCode);
            }

            JsonDocument outputDocument;
            try
            {
                outputDocument = JsonDocument.Parse(outputText);
            }
            catch (JsonException)
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiJsonParseFailure,
                    "The AI workout provider returned an invalid response.",
                    providerResponse,
                    response.StatusCode);
            }

            AiWorkoutPlanProviderResult? result;
            using (outputDocument)
            {
                if (outputDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw CreateFailure(
                        AiWorkoutProviderFailureCategories.OpenAiSchemaMismatch,
                        "The AI workout provider returned an invalid workout plan.",
                        providerResponse,
                        response.StatusCode);
                }

                try
                {
                    result = outputDocument.RootElement.Deserialize<AiWorkoutPlanProviderResult>(JsonOptions);
                }
                catch (JsonException)
                {
                    throw CreateFailure(
                        AiWorkoutProviderFailureCategories.OpenAiSchemaMismatch,
                        "The AI workout provider returned an invalid workout plan.",
                        providerResponse,
                        response.StatusCode);
                }
            }

            if (result is null)
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiSchemaMismatch,
                    "The AI workout provider returned an invalid workout plan.",
                    providerResponse,
                    response.StatusCode);
            }

            ValidateResult(result, request, providerResponse, response.StatusCode);
            return result;
        }
        catch (AiWorkoutPlanProviderException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw CreateFailure(
                AiWorkoutProviderFailureCategories.OpenAiHttpFailure,
                "The AI workout provider request timed out.");
        }
        catch (HttpRequestException)
        {
            throw CreateFailure(
                AiWorkoutProviderFailureCategories.OpenAiHttpFailure,
                "The AI workout provider is currently unavailable.");
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
            request.DurationMinutes is < 15 or > 180 ||
            request.RecommendedMainExerciseCount is < 1 or > 10 ||
            request.MaximumMainExerciseCount is < 1 or > 10 ||
            request.RecommendedMainExerciseCount > request.MaximumMainExerciseCount ||
            request.IncludeWarmup ||
            request.IncludeCooldown)
        {
            throw new AiWorkoutPlanProviderException(
                "The AI workout provider input is invalid.",
                AiWorkoutProviderFailureCategories.OpenAiValidationFailure);
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
            throw new AiWorkoutPlanProviderException(
                "The AI workout provider input is invalid.",
                AiWorkoutProviderFailureCategories.OpenAiValidationFailure);
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
                throw new AiWorkoutPlanProviderException(
                    "The AI workout provider input is invalid.",
                    AiWorkoutProviderFailureCategories.OpenAiValidationFailure);
            }
        }
    }

    private void ValidateResult(
        AiWorkoutPlanProviderResult result,
        AiWorkoutPlanProviderRequest request,
        OpenAiResponse? providerResponse,
        HttpStatusCode httpStatus)
    {
        if (result.Sections is null || result.Sections.Count < 1)
        {
            throw CreateFailure(
                AiWorkoutProviderFailureCategories.OpenAiNoSections,
                "The AI workout provider returned an invalid workout plan.");
        }

        if (result.Sections.Count > 6)
        {
            throw CreateFailure(
                AiWorkoutProviderFailureCategories.OpenAiValidationFailure,
                "The AI workout provider returned an invalid workout plan.");
        }

        foreach (var section in result.Sections)
        {
            if (section is null ||
                !IsSafeText(section.Name, 80))
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiValidationFailure,
                    "The AI workout provider returned an invalid workout plan.");
            }

            if (section.Exercises is null || section.Exercises.Count < 1)
            {
                throw CreateFailure(
                    AiWorkoutProviderFailureCategories.OpenAiNoExercises,
                    "The AI workout provider returned an invalid workout plan.");
            }
        }

        var returnedMainExerciseCount = result.Sections.Sum(section => section.Exercises.Count);
        if (returnedMainExerciseCount > request.MaximumMainExerciseCount)
        {
            throw CreateFailure(
                AiWorkoutProviderFailureCategories.OpenAiDurationExerciseCapExceeded,
                "The AI workout provider returned an invalid workout plan.",
                providerResponse,
                httpStatus,
                requestedDurationMinutes: request.DurationMinutes,
                maximumMainExerciseCount: request.MaximumMainExerciseCount,
                returnedMainExerciseCount: returnedMainExerciseCount,
                providerSectionCount: result.Sections.Count);
        }

        var candidateIds = request.CandidateExercises
            .Select(candidate => candidate.ExerciseCatalogItemId)
            .ToHashSet();
        var selectedIds = new HashSet<int>();

        foreach (var section in result.Sections)
        {

            foreach (var exercise in section.Exercises)
            {
                if (exercise is null)
                {
                    throw CreateFailure(
                        AiWorkoutProviderFailureCategories.OpenAiValidationFailure,
                        "The AI workout provider returned an invalid workout plan.");
                }

                if (!candidateIds.Contains(exercise.ExerciseCatalogItemId))
                {
                    throw CreateFailure(
                        AiWorkoutProviderFailureCategories.OpenAiUnknownExerciseId,
                        "The AI workout provider returned an invalid workout plan.",
                        rejectedExerciseId: exercise.ExerciseCatalogItemId);
                }

                if (!selectedIds.Add(exercise.ExerciseCatalogItemId))
                {
                    throw CreateFailure(
                        AiWorkoutProviderFailureCategories.OpenAiDuplicateExerciseId,
                        "The AI workout provider returned an invalid workout plan.",
                        rejectedExerciseId: exercise.ExerciseCatalogItemId);
                }

                if (exercise.Sets is < 1 or > 8)
                {
                    throw CreateFailure(
                        AiWorkoutProviderFailureCategories.OpenAiInvalidSets,
                        "The AI workout provider returned an invalid workout plan.",
                        rejectedExerciseId: exercise.ExerciseCatalogItemId);
                }

                if (!IsSafeText(exercise.Reps, 40))
                {
                    throw CreateFailure(
                        AiWorkoutProviderFailureCategories.OpenAiInvalidReps,
                        "The AI workout provider returned an invalid workout plan.",
                        rejectedExerciseId: exercise.ExerciseCatalogItemId);
                }

                if (exercise.RestSeconds is < 15 or > 300)
                {
                    throw CreateFailure(
                        AiWorkoutProviderFailureCategories.OpenAiInvalidRest,
                        "The AI workout provider returned an invalid workout plan.",
                        rejectedExerciseId: exercise.ExerciseCatalogItemId);
                }

                if (!IsSafeOptionalText(exercise.SuggestedWeight, 60) ||
                    !IsSafeOptionalText(exercise.Rationale, 240))
                {
                    throw CreateFailure(
                        AiWorkoutProviderFailureCategories.OpenAiValidationFailure,
                        "The AI workout provider returned an invalid workout plan.",
                        rejectedExerciseId: exercise.ExerciseCatalogItemId);
                }
            }
        }
    }

    private static bool IsSafeText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        !value.Any(char.IsControl);

    private static bool IsSafeOptionalText(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) || IsSafeText(value, maximumLength);

    private void EnsureSuccessfulResponse(HttpResponseMessage response)
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

        throw CreateFailure(
            AiWorkoutProviderFailureCategories.OpenAiHttpFailure,
            message,
            httpStatus: response.StatusCode);
    }

    private AiWorkoutPlanProviderException CreateFailure(
        string errorCategory,
        string safeMessage,
        OpenAiResponse? providerResponse = null,
        HttpStatusCode? httpStatus = null,
        int? rejectedExerciseId = null,
        int? requestedDurationMinutes = null,
        int? maximumMainExerciseCount = null,
        int? returnedMainExerciseCount = null,
        int? providerSectionCount = null)
    {
        _logger.LogWarning(
            "OpenAI workout response rejected. Category {ErrorCategory}, HttpStatus {HttpStatus}, ResponseId {ResponseId}, ResponseStatus {ResponseStatus}, HasIncompleteDetails {HasIncompleteDetails}, OutputItemCount {OutputItemCount}, RejectedExerciseId {RejectedExerciseId}, RequestedDurationMinutes {RequestedDurationMinutes}, MaximumMainExerciseCount {MaximumMainExerciseCount}, ReturnedMainExerciseCount {ReturnedMainExerciseCount}, ProviderSectionCount {ProviderSectionCount}.",
            errorCategory,
            httpStatus.HasValue ? (int)httpStatus.Value : null,
            providerResponse?.Id,
            providerResponse?.Status,
            providerResponse?.IncompleteDetails is not null,
            providerResponse?.Output?.Count ?? 0,
            rejectedExerciseId,
            requestedDurationMinutes,
            maximumMainExerciseCount,
            returnedMainExerciseCount,
            providerSectionCount);

        return new AiWorkoutPlanProviderException(safeMessage, errorCategory);
    }

    private static JsonElement CreateWorkoutPlanSchema(int maximumMainExerciseCount)
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "type": "object",
              "properties": {
                "sections": {
                  "type": "array",
                  "description": "Provider-generated main workout sections only. Across all sections combined, return no more than {{maximumMainExerciseCount}} exercises.",
                  "minItems": 1,
                  "maxItems": 6,
                  "items": {
                    "type": "object",
                    "properties": {
                      "name": { "type": "string", "maxLength": 80 },
                      "exercises": {
                        "type": "array",
                        "description": "Main exercises in this section. The combined total across every section must not exceed {{maximumMainExerciseCount}}.",
                        "minItems": 1,
                        "maxItems": {{maximumMainExerciseCount}},
                        "items": {
                          "type": "object",
                          "properties": {
                            "exerciseCatalogItemId": { "type": "integer" },
                            "sets": { "type": "integer", "minimum": 1, "maximum": 8 },
                            "reps": { "type": "string", "maxLength": 40 },
                            "restSeconds": { "type": "integer", "minimum": 15, "maximum": 300 },
                            "suggestedWeight": { "type": ["string", "null"], "maxLength": 60 },
                            "rationale": { "type": ["string", "null"], "maxLength": 240 }
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
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("output")]
        public List<OpenAiOutputItem>? Output { get; init; }

        [JsonPropertyName("output_text")]
        public string? OutputText { get; init; }

        [JsonPropertyName("incomplete_details")]
        public OpenAiIncompleteDetails? IncompleteDetails { get; init; }

        [JsonPropertyName("error")]
        public JsonElement? Error { get; init; }
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

        [JsonPropertyName("refusal")]
        public string? Refusal { get; init; }
    }

    private sealed class OpenAiIncompleteDetails
    {
        [JsonPropertyName("reason")]
        public string? Reason { get; init; }
    }
}
