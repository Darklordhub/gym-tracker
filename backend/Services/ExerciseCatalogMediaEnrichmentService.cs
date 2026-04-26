using backend.Configuration;
using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public enum ExerciseMediaType
{
    All,
    Images,
    Videos,
}

public enum ExerciseMediaProviderSelection
{
    All,
    Wger,
    ExerciseDb,
}

public sealed record ExerciseCatalogMediaEnrichmentRequest(
    int Limit,
    ExerciseMediaType MediaType,
    ExerciseMediaProviderSelection ProviderSelection);

public class ExerciseCatalogMediaEnrichmentService
{
    private readonly AppDbContext _dbContext;
    private readonly IReadOnlyList<IExerciseMediaProvider> _providers;
    private readonly ILogger<ExerciseCatalogMediaEnrichmentService> _logger;
    private readonly ExerciseMediaEnrichmentOptions _options;

    public ExerciseCatalogMediaEnrichmentService(
        AppDbContext dbContext,
        IEnumerable<IExerciseMediaProvider> providers,
        IOptions<ExerciseMediaEnrichmentOptions> options,
        ILogger<ExerciseCatalogMediaEnrichmentService> logger)
    {
        _dbContext = dbContext;
        _providers = providers.OrderBy(provider => provider.Priority).ToList();
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ExerciseCatalogMediaEnrichmentResponse> EnrichMissingMediaAsync(
        ExerciseCatalogMediaEnrichmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(request.Limit, 1, Math.Clamp(_options.MaxItems, 1, 1000));
        var providers = GetProviders(request.ProviderSelection);

        var items = await _dbContext.ExerciseCatalogItems
            .Where(item => item.IsActive)
            .Where(item => NeedsProviderImage(item, request.MediaType) || NeedsProviderVideo(item, request.MediaType))
            .OrderByDescending(item => item.Source == "wger")
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var response = new ExerciseCatalogMediaEnrichmentResponse
        {
            Checked = items.Count,
            Limit = limit,
            MediaType = ToApiValue(request.MediaType),
            Provider = ToApiValue(request.ProviderSelection),
        };

        if (providers.Count == 0)
        {
            response.StillMissingImages = items.Count(item => IsEffectivelyMissingImage(item, request.MediaType));
            response.StillMissingVideos = items.Count(item => IsEffectivelyMissingVideo(item, request.MediaType));
            return response;
        }

        var now = DateTime.UtcNow;
        var ambiguousItems = new HashSet<int>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var needsImage = NeedsProviderImage(item, request.MediaType);
            var needsVideo = NeedsProviderVideo(item, request.MediaType);

            if (!needsImage && !needsVideo)
            {
                continue;
            }

            var itemUpdated = false;

            foreach (var provider in providers)
            {
                if ((!needsImage && !needsVideo) || !provider.IsEnabled)
                {
                    continue;
                }

                ExerciseMediaMatchResult result;
                try
                {
                    result = await provider.TryGetMediaAsync(
                        item,
                        new ExerciseMediaRequirement
                        {
                            NeedImage = needsImage,
                            NeedVideo = needsVideo,
                        },
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Exercise media provider {Provider} failed for exercise catalog item {ItemId} ({Name}).",
                        provider.Name,
                        item.Id,
                        item.Name);
                    response.Failed++;
                    continue;
                }

                switch (result.Status)
                {
                    case ExerciseMediaMatchStatus.Ambiguous:
                        ambiguousItems.Add(item.Id);
                        continue;
                    case ExerciseMediaMatchStatus.Failed:
                        response.Failed++;
                        continue;
                    case ExerciseMediaMatchStatus.NotFound:
                    case ExerciseMediaMatchStatus.Skipped:
                        continue;
                    case ExerciseMediaMatchStatus.Found:
                    {
                        var providerUpdatedAnyField = false;

                        if (needsImage && IsUsableUrl(result.ImageUrl))
                        {
                            item.ThumbnailUrl = result.ImageUrl!.Trim();
                            response.ImagesAdded++;
                            needsImage = false;
                            providerUpdatedAnyField = true;
                        }

                        if (needsVideo && IsUsableUrl(result.VideoUrl))
                        {
                            item.VideoUrl = result.VideoUrl!.Trim();
                            response.VideosAdded++;
                            needsVideo = false;
                            providerUpdatedAnyField = true;
                        }

                        if (providerUpdatedAnyField)
                        {
                            itemUpdated = true;
                            IncrementProviderBreakdown(response.SourceBreakdown, provider.Name);
                        }

                        continue;
                    }
                }
            }

            if (itemUpdated)
            {
                item.LastSyncedAt = now;
                item.UpdatedAt = now;
                response.Updated++;
            }
        }

        if (response.Updated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        response.SkippedAmbiguous = ambiguousItems.Count;
        response.StillMissingImages = items.Count(item => IsEffectivelyMissingImage(item, request.MediaType));
        response.StillMissingVideos = items.Count(item => IsEffectivelyMissingVideo(item, request.MediaType));

        return response;
    }

    private List<IExerciseMediaProvider> GetProviders(ExerciseMediaProviderSelection selection)
    {
        return selection switch
        {
            ExerciseMediaProviderSelection.Wger => _providers.Where(provider => string.Equals(provider.Name, "wger", StringComparison.OrdinalIgnoreCase)).ToList(),
            ExerciseMediaProviderSelection.ExerciseDb => _providers.Where(provider => string.Equals(provider.Name, "exercisedb", StringComparison.OrdinalIgnoreCase)).ToList(),
            _ => _providers.ToList(),
        };
    }

    private static void IncrementProviderBreakdown(Dictionary<string, int> sourceBreakdown, string providerName)
    {
        sourceBreakdown.TryGetValue(providerName, out var currentCount);
        sourceBreakdown[providerName] = currentCount + 1;
    }

    private static bool NeedsProviderImage(ExerciseCatalogItem item, ExerciseMediaType mediaType)
    {
        return mediaType is ExerciseMediaType.All or ExerciseMediaType.Images
               && string.IsNullOrWhiteSpace(item.ThumbnailUrl);
    }

    private static bool NeedsProviderVideo(ExerciseCatalogItem item, ExerciseMediaType mediaType)
    {
        return mediaType is ExerciseMediaType.All or ExerciseMediaType.Videos
               && string.IsNullOrWhiteSpace(item.VideoUrl);
    }

    private static bool IsEffectivelyMissingImage(ExerciseCatalogItem item, ExerciseMediaType mediaType)
    {
        return mediaType is ExerciseMediaType.All or ExerciseMediaType.Images
               && string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride)
               && string.IsNullOrWhiteSpace(item.ThumbnailUrl);
    }

    private static bool IsEffectivelyMissingVideo(ExerciseCatalogItem item, ExerciseMediaType mediaType)
    {
        return mediaType is ExerciseMediaType.All or ExerciseMediaType.Videos
               && string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride)
               && string.IsNullOrWhiteSpace(item.VideoUrl);
    }

    public static bool IsUsableUrl(string? value)
    {
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && value!.Trim().Length <= 500;
    }

    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray());

        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string[] GetNameTokens(string? value)
    {
        return NormalizeName(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static double GetTokenSimilarity(string left, string right)
    {
        var leftTokens = GetNameTokens(left);
        var rightTokens = GetNameTokens(right);

        if (leftTokens.Length == 0 || rightTokens.Length == 0)
        {
            return 0;
        }

        var leftSet = leftTokens.ToHashSet(StringComparer.Ordinal);
        var rightSet = rightTokens.ToHashSet(StringComparer.Ordinal);
        var intersectionCount = leftSet.Intersect(rightSet, StringComparer.Ordinal).Count();
        var unionCount = leftSet.Union(rightSet, StringComparer.Ordinal).Count();

        return unionCount == 0 ? 0 : (double)intersectionCount / unionCount;
    }

    public static double GetTokenContainmentSimilarity(string left, string right)
    {
        var leftTokens = GetNameTokens(left);
        var rightTokens = GetNameTokens(right);

        if (leftTokens.Length == 0 || rightTokens.Length == 0)
        {
            return 0;
        }

        var leftSet = leftTokens.ToHashSet(StringComparer.Ordinal);
        var rightSet = rightTokens.ToHashSet(StringComparer.Ordinal);
        var intersectionCount = leftSet.Intersect(rightSet, StringComparer.Ordinal).Count();
        var denominator = Math.Min(leftSet.Count, rightSet.Count);

        return denominator == 0 ? 0 : (double)intersectionCount / denominator;
    }

    public static double GetLevenshteinSimilarity(string left, string right)
    {
        var normalizedLeft = NormalizeName(left);
        var normalizedRight = NormalizeName(right);

        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0;
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return 1;
        }

        var distances = new int[normalizedRight.Length + 1];
        for (var index = 0; index <= normalizedRight.Length; index++)
        {
            distances[index] = index;
        }

        for (var leftIndex = 1; leftIndex <= normalizedLeft.Length; leftIndex++)
        {
            var previousDiagonal = distances[0];
            distances[0] = leftIndex;

            for (var rightIndex = 1; rightIndex <= normalizedRight.Length; rightIndex++)
            {
                var current = distances[rightIndex];
                var substitutionCost = normalizedLeft[leftIndex - 1] == normalizedRight[rightIndex - 1] ? 0 : 1;
                distances[rightIndex] = Math.Min(
                    Math.Min(distances[rightIndex] + 1, distances[rightIndex - 1] + 1),
                    previousDiagonal + substitutionCost);
                previousDiagonal = current;
            }
        }

        var maxLength = Math.Max(normalizedLeft.Length, normalizedRight.Length);
        return maxLength == 0 ? 1 : 1 - (double)distances[normalizedRight.Length] / maxLength;
    }

    private static string ToApiValue(ExerciseMediaType mediaType)
    {
        return mediaType switch
        {
            ExerciseMediaType.Images => "images",
            ExerciseMediaType.Videos => "videos",
            _ => "all",
        };
    }

    private static string ToApiValue(ExerciseMediaProviderSelection selection)
    {
        return selection switch
        {
            ExerciseMediaProviderSelection.Wger => "wger",
            ExerciseMediaProviderSelection.ExerciseDb => "exercisedb",
            _ => "all",
        };
    }
}
