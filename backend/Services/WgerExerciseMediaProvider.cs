using System.Net.Http.Json;
using System.Text.Json;
using backend.Configuration;
using backend.Contracts;
using backend.Models;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class WgerExerciseMediaProvider : IExerciseMediaProvider
{
    public string Name => "wger";
    public int Priority => 100;

    private readonly HttpClient _httpClient;
    private readonly WgerOptions _wgerOptions;
    private readonly ExerciseMediaEnrichmentOptions _enrichmentOptions;
    private readonly ILogger<WgerExerciseMediaProvider> _logger;

    public bool IsEnabled => _wgerOptions.Enabled && _enrichmentOptions.Wger.Enabled;

    public WgerExerciseMediaProvider(
        HttpClient httpClient,
        IOptions<WgerOptions> wgerOptions,
        IOptions<ExerciseMediaEnrichmentOptions> enrichmentOptions,
        ILogger<WgerExerciseMediaProvider> logger)
    {
        _httpClient = httpClient;
        _wgerOptions = wgerOptions.Value;
        _enrichmentOptions = enrichmentOptions.Value;
        _logger = logger;
    }

    public async Task<ExerciseMediaMatchResult> TryGetMediaAsync(
        ExerciseCatalogItem item,
        ExerciseMediaRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return ExerciseMediaMatchResult.Skipped("Wger media enrichment is disabled.");
        }

        if (!string.Equals(item.Source, "wger", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(item.ExternalId))
        {
            return ExerciseMediaMatchResult.Skipped("Wger media lookup requires a Wger-backed item with an external id.");
        }

        if (!int.TryParse(item.ExternalId, out var exerciseId) || exerciseId <= 0)
        {
            return ExerciseMediaMatchResult.NotFound("Wger external id is invalid.");
        }

        WgerExerciseInfoDto? exercise;
        try
        {
            exercise = await _httpClient.GetFromJsonAsync<WgerExerciseInfoDto>($"exerciseinfo/{exerciseId}/", cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to load Wger media for exercise {ExerciseId}.", exerciseId);
            return ExerciseMediaMatchResult.Failed("Failed to load Wger media.");
        }

        if (exercise is null)
        {
            return ExerciseMediaMatchResult.NotFound("Wger returned no exercise details.");
        }

        var imageUrl = requirement.NeedImage ? SelectBestMediaUrl(exercise.Images, MediaKind.Image) : null;
        var videoUrl = requirement.NeedVideo ? SelectBestMediaUrl(exercise.Videos, MediaKind.Video) : null;

        if (!ExerciseCatalogMediaEnrichmentService.IsUsableUrl(imageUrl) && !ExerciseCatalogMediaEnrichmentService.IsUsableUrl(videoUrl))
        {
            return ExerciseMediaMatchResult.NotFound("Wger has no usable media for this exercise.");
        }

        return ExerciseMediaMatchResult.Found(
            ExerciseCatalogMediaEnrichmentService.IsUsableUrl(imageUrl) ? imageUrl : null,
            ExerciseCatalogMediaEnrichmentService.IsUsableUrl(videoUrl) ? videoUrl : null,
            1,
            "Matched by Wger external id.");
    }

    private static string? SelectBestMediaUrl(IEnumerable<JsonElement> items, MediaKind mediaKind)
    {
        MediaCandidate? best = null;

        foreach (var item in items)
        {
            var url = NormalizeText(GetBestMediaUrlFromItem(item, mediaKind));
            if (!ExerciseCatalogMediaEnrichmentService.IsUsableUrl(url))
            {
                continue;
            }

            var candidate = new MediaCandidate(url!, ScoreMediaCandidate(item, mediaKind));
            if (best is null || candidate.Score > best.Score)
            {
                best = candidate;
            }
        }

        return best?.Url;
    }

    private static int ScoreMediaCandidate(JsonElement item, MediaKind mediaKind)
    {
        var score = 0;

        if (GetBoolean(item, "is_main") is true)
        {
            score += 100;
        }

        if (mediaKind == MediaKind.Image)
        {
            if (GetInt32(item, "size") is int imageSize)
            {
                score += imageSize;
            }

            if (!string.IsNullOrWhiteSpace(GetString(item, "image")))
            {
                score += 20;
            }
        }
        else
        {
            if (GetInt32(item, "width") is int videoWidth)
            {
                score += videoWidth;
            }

            if (GetInt32(item, "height") is int videoHeight)
            {
                score += videoHeight;
            }

            if (!string.IsNullOrWhiteSpace(GetString(item, "video")))
            {
                score += 20;
            }
        }

        return score;
    }

    private static string? GetBestMediaUrlFromItem(JsonElement item, MediaKind mediaKind)
    {
        if (mediaKind == MediaKind.Image)
        {
            return GetString(item, "image")
                ?? GetString(item, "url")
                ?? GetString(item, "original")
                ?? GetString(item, "source");
        }

        return GetString(item, "video")
            ?? GetString(item, "url")
            ?? GetString(item, "original")
            ?? GetString(item, "source");
    }

    private static string? GetString(JsonElement item, string propertyName)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null,
        };
    }

    private static int? GetInt32(JsonElement item, string propertyName)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) ? value : null;
    }

    private static bool? GetBoolean(JsonElement item, string propertyName)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private enum MediaKind
    {
        Image,
        Video,
    }

    private sealed record MediaCandidate(string Url, int Score);
}
