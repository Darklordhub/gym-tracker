using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using backend.Configuration;
using backend.Models;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class OpenAiExerciseMediaGenerationProvider : IExerciseMediaGenerationProvider
{
    public const string Name = "OpenAI";

    private static readonly HashSet<int> AllowedVideoSeconds = [4, 8, 12];
    private static readonly HashSet<string> AllowedVideoSizes = new(StringComparer.Ordinal)
    {
        "720x1280",
        "1280x720",
        "1024x1792",
        "1792x1024",
    };

    private readonly HttpClient _httpClient;
    private readonly ExerciseMediaStorageService _storageService;
    private readonly MediaGenerationOptions _generationOptions;
    private readonly OpenAiVideoGenerationOptions _openAiOptions;

    public OpenAiExerciseMediaGenerationProvider(
        HttpClient httpClient,
        ExerciseMediaStorageService storageService,
        IOptions<MediaGenerationOptions> generationOptions,
        IOptions<OpenAiVideoGenerationOptions> openAiOptions)
    {
        _httpClient = httpClient;
        _storageService = storageService;
        _generationOptions = generationOptions.Value;
        _openAiOptions = openAiOptions.Value;
    }

    public string ProviderName => Name;

    public async Task<ExerciseMediaGenerationStartResult> StartGenerationAsync(
        ExerciseMediaDraft draft,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        using var timeoutSource = CreateTimeoutSource(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "videos");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(draft.PromptText), "prompt" },
            { new StringContent(_openAiOptions.VideoModel.Trim()), "model" },
            { new StringContent(_openAiOptions.VideoSeconds.ToString()), "seconds" },
            { new StringContent(_openAiOptions.VideoSize.Trim()), "size" },
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey.Trim());
        request.Content = form;

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            EnsureSuccessfulResponse(response);

            var job = await response.Content.ReadFromJsonAsync<OpenAiVideoJob>(
                cancellationToken: timeoutSource.Token);
            if (job is null ||
                string.IsNullOrWhiteSpace(job.Id) ||
                job.Id.Length > 240 ||
                job.Id.Any(char.IsControl))
            {
                throw new ExerciseMediaGenerationException("The video provider returned an invalid job response.");
            }

            return new ExerciseMediaGenerationStartResult
            {
                ProviderJobId = job.Id,
                Provider = Name,
                Model = _openAiOptions.VideoModel.Trim(),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExerciseMediaGenerationException("The video provider request timed out.");
        }
        catch (HttpRequestException)
        {
            throw new ExerciseMediaGenerationException("The video provider is currently unavailable.");
        }
        catch (NotSupportedException)
        {
            throw new ExerciseMediaGenerationException("The video provider returned an invalid response.");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ExerciseMediaGenerationException("The video provider returned an invalid response.");
        }
    }

    public async Task<ExerciseMediaGenerationRefreshResult> RefreshGenerationStatusAsync(
        ExerciseMediaDraft draft,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        var providerJobId = draft.ProviderJobId?.Trim();
        if (string.IsNullOrWhiteSpace(providerJobId))
        {
            throw new ExerciseMediaGenerationException("The draft does not have a provider job identifier.");
        }

        using var timeoutSource = CreateTimeoutSource(cancellationToken);
        var encodedJobId = Uri.EscapeDataString(providerJobId);

        try
        {
            using var statusRequest = CreateAuthorizedRequest(
                HttpMethod.Get,
                $"videos/{encodedJobId}");
            using var statusResponse = await _httpClient.SendAsync(
                statusRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            EnsureSuccessfulResponse(statusResponse);

            var job = await statusResponse.Content.ReadFromJsonAsync<OpenAiVideoJob>(
                cancellationToken: timeoutSource.Token);
            if (job is null || string.IsNullOrWhiteSpace(job.Status))
            {
                throw new ExerciseMediaGenerationException("The video provider returned an invalid job response.");
            }

            switch (job.Status.Trim().ToLowerInvariant())
            {
                case "queued":
                case "in_progress":
                    return new ExerciseMediaGenerationRefreshResult
                    {
                        State = ExerciseMediaGenerationState.Pending,
                    };

                case "failed":
                case "cancelled":
                    return new ExerciseMediaGenerationRefreshResult
                    {
                        State = ExerciseMediaGenerationState.Failed,
                        ErrorMessage = "Video generation failed. Review the prompt and try again.",
                    };

                case "completed":
                    return await DownloadCompletedVideoAsync(
                        draft,
                        encodedJobId,
                        timeoutSource.Token);

                default:
                    throw new ExerciseMediaGenerationException("The video provider returned an unsupported job status.");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExerciseMediaGenerationException("The video provider request timed out.");
        }
        catch (HttpRequestException)
        {
            throw new ExerciseMediaGenerationException("The video provider is currently unavailable.");
        }
        catch (NotSupportedException)
        {
            throw new ExerciseMediaGenerationException("The video provider returned an invalid response.");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ExerciseMediaGenerationException("The video provider returned an invalid response.");
        }
        catch (ExerciseMediaStorageException exception)
        {
            throw new ExerciseMediaGenerationException(exception.Message);
        }
    }

    private async Task<ExerciseMediaGenerationRefreshResult> DownloadCompletedVideoAsync(
        ExerciseMediaDraft draft,
        string encodedJobId,
        CancellationToken cancellationToken)
    {
        using var contentRequest = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"videos/{encodedJobId}/content");
        using var contentResponse = await _httpClient.SendAsync(
            contentRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        EnsureSuccessfulResponse(contentResponse);

        var contentType = contentResponse.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(contentType, "video/mp4", StringComparison.OrdinalIgnoreCase))
        {
            throw new ExerciseMediaGenerationException("The video provider returned an unsupported media format.");
        }

        await using var contentStream = await contentResponse.Content.ReadAsStreamAsync(cancellationToken);
        var storedFile = await _storageService.SaveGeneratedVideoAsync(
            draft.ExerciseCatalogItemId,
            draft.Id,
            contentStream,
            contentType,
            "video.mp4",
            cancellationToken);

        return new ExerciseMediaGenerationRefreshResult
        {
            State = ExerciseMediaGenerationState.Completed,
            GeneratedVideoUrl = storedFile.PublicUrl,
        };
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey.Trim());
        return request;
    }

    private CancellationTokenSource CreateTimeoutSource(CancellationToken cancellationToken)
    {
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_openAiOptions.TimeoutSeconds));
        return timeoutSource;
    }

    private void ValidateConfiguration()
    {
        if (!_generationOptions.Enabled)
        {
            throw new ExerciseMediaGenerationException("Media generation is disabled.");
        }

        if (!string.Equals(_generationOptions.Provider, Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new ExerciseMediaGenerationException("The configured media generation provider is not available.");
        }

        if (string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
        {
            throw new ExerciseMediaGenerationException("The OpenAI API key is not configured.");
        }

        var model = _openAiOptions.VideoModel?.Trim();
        if (string.IsNullOrWhiteSpace(model) ||
            model.Length > 100 ||
            model.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new ExerciseMediaGenerationException("The configured OpenAI video model is invalid.");
        }

        if (!AllowedVideoSeconds.Contains(_openAiOptions.VideoSeconds))
        {
            throw new ExerciseMediaGenerationException("OpenAI video seconds must be 4, 8, or 12.");
        }

        if (!AllowedVideoSizes.Contains(_openAiOptions.VideoSize?.Trim() ?? string.Empty))
        {
            throw new ExerciseMediaGenerationException("The configured OpenAI video size is invalid.");
        }

        if (_openAiOptions.TimeoutSeconds is < 5 or > 300)
        {
            throw new ExerciseMediaGenerationException("OpenAI timeout seconds must be between 5 and 300.");
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
                "The video provider rejected the configured credentials.",
            HttpStatusCode.TooManyRequests =>
                "The video provider is temporarily rate limited.",
            HttpStatusCode.BadRequest =>
                "The video provider rejected the generation request.",
            HttpStatusCode.NotFound =>
                "The video provider job was not found.",
            _ =>
                "The video provider request failed.",
        };

        throw new ExerciseMediaGenerationException(message);
    }

    private sealed class OpenAiVideoJob
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;
    }
}
