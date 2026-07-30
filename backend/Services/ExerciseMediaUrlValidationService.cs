using System.Net;
using System.Net.Http.Headers;

namespace backend.Services;

public enum ExerciseMediaUrlKind
{
    Image,
    Video,
}

public sealed class ExerciseMediaUrlValidationResult
{
    public string Url { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public int? StatusCode { get; init; }
    public string? ContentType { get; init; }
    public string? Error { get; init; }
    public bool CheckedRemotely { get; init; }
}

public class ExerciseMediaUrlValidationService
{
    private const int MaxRedirectHops = 5;

    private static readonly HashSet<string> KnownEmbeddableVideoHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "youtu.be",
        "vimeo.com",
        "www.vimeo.com",
        "player.vimeo.com",
    };

    private static readonly HashSet<string> DirectVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".webm",
        ".ogg",
        ".mov",
        ".m4v",
    };

    private readonly HttpClient _httpClient;
    private readonly IExerciseMediaHostResolver _hostResolver;

    public ExerciseMediaUrlValidationService(
        HttpClient httpClient,
        IExerciseMediaHostResolver hostResolver)
    {
        _httpClient = httpClient;
        _hostResolver = hostResolver;
    }

    public bool IsLikelyEmbeddableVideoUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return KnownEmbeddableVideoHosts.Contains(uri.Host);
    }

    public async Task<ExerciseMediaUrlValidationResult> ValidateImageUrlAsync(string? value, CancellationToken cancellationToken = default)
    {
        return await ValidateUrlAsync(value, ExerciseMediaUrlKind.Image, cancellationToken);
    }

    public async Task<ExerciseMediaUrlValidationResult> ValidateVideoUrlAsync(string? value, CancellationToken cancellationToken = default)
    {
        return await ValidateUrlAsync(value, ExerciseMediaUrlKind.Video, cancellationToken);
    }

    public async Task<ExerciseMediaUrlValidationResult> ValidateUrlAsync(
        string? value,
        ExerciseMediaUrlKind kind,
        CancellationToken cancellationToken = default)
    {
        var normalizedValue = value?.Trim();
        if (!ExerciseCatalogMediaEnrichmentService.IsUsableUrl(normalizedValue))
        {
            return new ExerciseMediaUrlValidationResult
            {
                Url = normalizedValue ?? string.Empty,
                Error = "URL is empty or not a valid absolute HTTP/HTTPS URL.",
            };
        }

        if (!Uri.TryCreate(normalizedValue, UriKind.Absolute, out var uri)
            || !IsSupportedScheme(uri))
        {
            return new ExerciseMediaUrlValidationResult
            {
                Url = normalizedValue ?? string.Empty,
                Error = "URL must use HTTP or HTTPS.",
            };
        }

        var targetValidationResult = await ValidateOutboundTargetAsync(uri, cancellationToken);
        if (targetValidationResult is not null)
        {
            return new ExerciseMediaUrlValidationResult
            {
                Url = normalizedValue!,
                Error = targetValidationResult,
            };
        }

        if (kind == ExerciseMediaUrlKind.Video && IsLikelyEmbeddableVideoUrl(normalizedValue))
        {
            return new ExerciseMediaUrlValidationResult
            {
                Url = normalizedValue!,
                IsValid = true,
                Error = "Known embeddable video URL.",
            };
        }

        var probeResult = await ProbeAsync(normalizedValue!, kind, cancellationToken);
        if (!probeResult.Success)
        {
            return new ExerciseMediaUrlValidationResult
            {
                Url = normalizedValue!,
                StatusCode = probeResult.StatusCode,
                ContentType = probeResult.ContentType,
                Error = probeResult.Error,
                CheckedRemotely = probeResult.CheckedRemotely,
            };
        }

        if (kind == ExerciseMediaUrlKind.Image)
        {
            if (probeResult.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new ExerciseMediaUrlValidationResult
                {
                    Url = normalizedValue!,
                    IsValid = true,
                    StatusCode = probeResult.StatusCode,
                    ContentType = probeResult.ContentType,
                    CheckedRemotely = probeResult.CheckedRemotely,
                };
            }

            return new ExerciseMediaUrlValidationResult
            {
                Url = normalizedValue!,
                StatusCode = probeResult.StatusCode,
                ContentType = probeResult.ContentType,
                Error = "Response did not return an image content-type.",
                CheckedRemotely = probeResult.CheckedRemotely,
            };
        }

        var extension = Path.GetExtension(new Uri(normalizedValue!).AbsolutePath);
        if (probeResult.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true
            || probeResult.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true
            || DirectVideoExtensions.Contains(extension))
        {
            return new ExerciseMediaUrlValidationResult
            {
                Url = normalizedValue!,
                IsValid = true,
                StatusCode = probeResult.StatusCode,
                ContentType = probeResult.ContentType,
                CheckedRemotely = probeResult.CheckedRemotely,
            };
        }

        return new ExerciseMediaUrlValidationResult
        {
            Url = normalizedValue!,
            StatusCode = probeResult.StatusCode,
            ContentType = probeResult.ContentType,
            Error = "Response did not return a recognized video or embeddable content-type.",
            CheckedRemotely = probeResult.CheckedRemotely,
        };
    }

    private async Task<ProbeResult> ProbeAsync(string url, ExerciseMediaUrlKind kind, CancellationToken cancellationToken)
    {
        var headResult = await SendAsync(HttpMethod.Head, url, kind, cancellationToken);
        if (headResult.CanFallbackToGet && ShouldFallbackToGet(headResult))
        {
            var getResult = await SendAsync(HttpMethod.Get, url, kind, cancellationToken);
            if (getResult.CheckedRemotely)
            {
                return getResult;
            }
        }

        return headResult;
    }

    private async Task<ProbeResult> SendAsync(
        HttpMethod method,
        string url,
        ExerciseMediaUrlKind kind,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var currentUri))
            {
                return new ProbeResult
                {
                    Error = "URL is empty or not a valid absolute HTTP/HTTPS URL.",
                };
            }

            for (var redirectCount = 0; redirectCount <= MaxRedirectHops; redirectCount++)
            {
                var targetValidationResult = await ValidateOutboundTargetAsync(currentUri, cancellationToken);
                if (targetValidationResult is not null)
                {
                    return new ProbeResult
                    {
                        Error = targetValidationResult,
                    };
                }

                using var request = new HttpRequestMessage(method, currentUri);
                request.Headers.Accept.Clear();

                if (method == HttpMethod.Get)
                {
                    request.Headers.Range = new RangeHeaderValue(0, 0);
                }

                if (kind == ExerciseMediaUrlKind.Image)
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
                }
                else
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("video/*"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (IsRedirectStatusCode(response.StatusCode))
                {
                    var nextUri = ResolveRedirectUri(currentUri, response.Headers.Location);
                    if (nextUri is null)
                    {
                        return new ProbeResult
                        {
                            StatusCode = (int)response.StatusCode,
                            Error = "Remote server returned an invalid redirect.",
                            CheckedRemotely = true,
                        };
                    }

                    currentUri = nextUri;
                    continue;
                }

                var statusCode = (int)response.StatusCode;
                var contentType = response.Content.Headers.ContentType?.MediaType;

                return new ProbeResult
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = statusCode,
                    ContentType = contentType,
                    Error = response.IsSuccessStatusCode
                        ? null
                        : $"Remote server responded with {(int)response.StatusCode} {response.StatusCode}.",
                    CheckedRemotely = true,
                    CanFallbackToGet = true,
                };
            }

            return new ProbeResult
            {
                Error = "Remote server redirected too many times.",
                CheckedRemotely = true,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new ProbeResult
            {
                Error = exception is TaskCanceledException
                    ? "Remote server validation timed out."
                    : "Unable to validate the URL against the remote server.",
                CheckedRemotely = true,
                CanFallbackToGet = true,
            };
        }
    }

    private static bool IsSupportedScheme(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private async Task<string?> ValidateOutboundTargetAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!IsSupportedScheme(uri))
        {
            return "URL must use HTTP or HTTPS.";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "URL must not include credentials.";
        }

        try
        {
            await ExerciseMediaSafeHttpHandler.ResolveAllowedAddressesAsync(
                uri.DnsSafeHost,
                _hostResolver,
                cancellationToken);
            return null;
        }
        catch (ExerciseMediaRemoteTargetException exception)
        {
            return exception.Message;
        }
    }

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return numericStatusCode >= 300 && numericStatusCode < 400;
    }

    private static Uri? ResolveRedirectUri(Uri currentUri, Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        if (location.IsAbsoluteUri)
        {
            return location;
        }

        return Uri.TryCreate(currentUri, location, out var resolvedUri) ? resolvedUri : null;
    }

    private static bool ShouldFallbackToGet(ProbeResult result)
    {
        if (!result.CheckedRemotely)
        {
            return true;
        }

        return result.StatusCode is null
            || result.StatusCode == (int)HttpStatusCode.Forbidden
            || result.StatusCode == (int)HttpStatusCode.MethodNotAllowed
            || result.StatusCode == (int)HttpStatusCode.NotImplemented
            || string.IsNullOrWhiteSpace(result.ContentType);
    }

    private sealed class ProbeResult
    {
        public bool Success { get; init; }
        public int? StatusCode { get; init; }
        public string? ContentType { get; init; }
        public string? Error { get; init; }
        public bool CheckedRemotely { get; init; }
        public bool CanFallbackToGet { get; init; }
    }
}
