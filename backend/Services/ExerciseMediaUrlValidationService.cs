using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

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

    private static readonly HashSet<string> BlockedHostnames = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "localhost.localdomain",
    };

    private readonly HttpClient _httpClient;

    public ExerciseMediaUrlValidationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
        if (ShouldFallbackToGet(headResult))
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

        if (IsBlockedHostname(uri.Host))
        {
            return "URL points to a blocked network location.";
        }

        IPAddress[] addresses;

        try
        {
            if (IPAddress.TryParse(uri.Host, out var parsedAddress))
            {
                addresses = new[] { parsedAddress };
            }
            else
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            return "Unable to resolve the remote host for validation.";
        }

        if (addresses.Length == 0)
        {
            return "Unable to resolve the remote host for validation.";
        }

        if (addresses.Any(IsBlockedAddress))
        {
            return "URL points to a blocked network location.";
        }

        return null;
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        var normalizedAddress = NormalizeAddress(address);

        if (IPAddress.IsLoopback(normalizedAddress)
            || normalizedAddress.Equals(IPAddress.Any)
            || normalizedAddress.Equals(IPAddress.None)
            || normalizedAddress.Equals(IPAddress.IPv6Any)
            || normalizedAddress.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        var bytes = normalizedAddress.GetAddressBytes();

        if (normalizedAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes.All(static value => value == 0);
        }

        if (normalizedAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xFE) == 0xFC
                || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
                || bytes.All(static value => value == 0);
        }

        return false;
    }

    private static IPAddress NormalizeAddress(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    private static bool IsBlockedHostname(string host)
    {
        var normalizedHost = host.Trim().TrimEnd('.');
        return BlockedHostnames.Contains(normalizedHost)
            || normalizedHost.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
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
    }
}
