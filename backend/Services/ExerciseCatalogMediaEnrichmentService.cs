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
    FreeExerciseDb,
}

public enum ExerciseCatalogMediaDiagnosticsSourceFilter
{
    All,
    Wger,
    Local,
}

public sealed record ExerciseCatalogMediaEnrichmentRequest(
    int Limit,
    ExerciseMediaType MediaType,
    ExerciseMediaProviderSelection ProviderSelection,
    bool RepairBadUrls,
    bool DryRun);

public sealed record ExerciseCatalogMediaDiagnosticsRequest(
    bool ValidateUrls,
    int SampleSize,
    ExerciseCatalogMediaDiagnosticsSourceFilter SourceFilter);

public class ExerciseCatalogMediaEnrichmentService
{
    private static readonly string[][] SecondaryEquipmentPrefixes =
    [
        ["barbell"],
        ["dumbbell"],
        ["cable"],
        ["machine"],
        ["bodyweight"],
        ["resistance", "band"],
    ];

    private readonly AppDbContext _dbContext;
    private readonly IReadOnlyList<IExerciseMediaProvider> _providers;
    private readonly ILogger<ExerciseCatalogMediaEnrichmentService> _logger;
    private readonly ExerciseMediaEnrichmentOptions _options;
    private readonly ExerciseMediaUrlValidationService _urlValidationService;

    public ExerciseCatalogMediaEnrichmentService(
        AppDbContext dbContext,
        IEnumerable<IExerciseMediaProvider> providers,
        IOptions<ExerciseMediaEnrichmentOptions> options,
        ExerciseMediaUrlValidationService urlValidationService,
        ILogger<ExerciseCatalogMediaEnrichmentService> logger)
    {
        _dbContext = dbContext;
        _providers = providers.OrderBy(provider => provider.Priority).ToList();
        _logger = logger;
        _options = options.Value;
        _urlValidationService = urlValidationService;
    }

    public async Task<ExerciseCatalogMediaDiagnosticsResponse> GetMediaDiagnosticsAsync(
        ExerciseCatalogMediaDiagnosticsRequest request,
        CancellationToken cancellationToken = default)
    {
        var sampleSize = Math.Clamp(request.SampleSize, 1, 100);

        var filteredItems = await ApplySourceFilter(_dbContext.ExerciseCatalogItems.AsNoTracking(), request.SourceFilter)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var activeItems = filteredItems
            .Where(item => item.IsActive)
            .ToList();

        var response = new ExerciseCatalogMediaDiagnosticsResponse
        {
            SourceFilter = ToApiValue(request.SourceFilter),
            UrlValidationRun = request.ValidateUrls,
            SampleSize = sampleSize,
            TotalCatalogItems = filteredItems.Count,
            ActiveItems = activeItems.Count,
            ItemsWithEffectiveThumbnail = activeItems.Count(HasEffectiveThumbnail),
            ItemsMissingEffectiveThumbnail = activeItems.Count(item => !HasEffectiveThumbnail(item)),
            ItemsWithProviderThumbnail = activeItems.Count(item => !string.IsNullOrWhiteSpace(item.ThumbnailUrl)),
            ItemsWithLocalThumbnailUrlOverride = activeItems.Count(item => !string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride)),
            ItemsWithVideoUrl = activeItems.Count(item => !string.IsNullOrWhiteSpace(item.VideoUrl)),
            ItemsWithLocalVideoUrlOverride = activeItems.Count(item => !string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride)),
        };

        foreach (var item in activeItems.Where(item => IsMissingAnyEffectiveMedia(item)).Take(sampleSize))
        {
            response.SampleMissingItems.Add(new ExerciseCatalogMediaStillMissingSample
            {
                Id = item.Id,
                Name = item.Name,
                Source = item.Source,
                ExternalId = item.ExternalId,
                PrimaryMuscle = item.PrimaryMuscle,
                Equipment = item.Equipment,
                ReasonMissing = BuildDiagnosticsMissingReason(item),
            });
        }

        if (!request.ValidateUrls)
        {
            return response;
        }

        foreach (var item in activeItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var effectiveThumbnail = GetEffectiveThumbnailUrl(item);
            if (!IsUsableUrl(effectiveThumbnail))
            {
                continue;
            }

            var validation = await _urlValidationService.ValidateImageUrlAsync(effectiveThumbnail, cancellationToken);
            if (validation.IsValid)
            {
                continue;
            }

            response.BrokenThumbnailUrlCount++;
            if (response.SampleBrokenUrls.Count >= sampleSize)
            {
                continue;
            }

            response.SampleBrokenUrls.Add(new ExerciseCatalogMediaBrokenUrlSample
            {
                Id = item.Id,
                Name = item.Name,
                MediaField = GetEffectiveThumbnailField(item),
                Url = validation.Url,
                StatusCode = validation.StatusCode,
                Error = validation.Error,
            });
        }

        return response;
    }

    public async Task<ExerciseCatalogMediaEnrichmentResponse> EnrichMissingMediaAsync(
        ExerciseCatalogMediaEnrichmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(request.Limit, 1, Math.Clamp(_options.MaxItems, 1, 1000));
        var providers = GetProviders(request.ProviderSelection);
        var activeItems = await _dbContext.ExerciseCatalogItems
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.Source == "wger")
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var workItems = new List<ExerciseCatalogMediaWorkItem>(limit);

        foreach (var item in activeItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var workItem = await BuildWorkItemAsync(item, request, cancellationToken);
            if (!workItem.ShouldProcess)
            {
                continue;
            }

            workItems.Add(workItem);
            if (workItems.Count >= limit)
            {
                break;
            }
        }

        var response = new ExerciseCatalogMediaEnrichmentResponse
        {
            Checked = workItems.Count,
            Limit = limit,
            MediaType = ToApiValue(request.MediaType),
            Provider = ToApiValue(request.ProviderSelection),
            DryRun = request.DryRun,
            RepairBadUrls = request.RepairBadUrls,
        };

        if (providers.Count == 0)
        {
            PopulateStillMissingSamples(response, workItems, request.MediaType);
            PopulateBrokenUnrepairedSamples(response, workItems);
            response.StillMissingImages = CountStillMissingImages(workItems, request.MediaType);
            response.StillMissingVideos = CountStillMissingVideos(workItems, request.MediaType);
            return response;
        }

        var now = DateTime.UtcNow;
        var ambiguousItems = new HashSet<int>();
        var lowConfidenceItems = new HashSet<int>();

        foreach (var workItem in workItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = workItem.Item;
            var needsImage = workItem.NeedsImage;
            var needsVideo = workItem.NeedsVideo;
            var itemUpdated = false;
            var providerNamesUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matchedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var messages = new HashSet<string>(StringComparer.Ordinal);
            var maxConfidence = 0d;
            string? imageAction = null;
            string? videoAction = null;

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
                    case ExerciseMediaMatchStatus.LowConfidence:
                        lowConfidenceItems.Add(item.Id);
                        continue;
                    case ExerciseMediaMatchStatus.Failed:
                        response.Failed++;
                        continue;
                    case ExerciseMediaMatchStatus.NotFound:
                    case ExerciseMediaMatchStatus.Skipped:
                        continue;
                    case ExerciseMediaMatchStatus.Found:
                    {
                        var validatedImage = needsImage
                            ? await ValidateCandidateUrlAsync(result.ImageUrl, ExerciseMediaUrlKind.Image, cancellationToken)
                            : null;
                        var validatedVideo = needsVideo
                            ? await ValidateCandidateUrlAsync(result.VideoUrl, ExerciseMediaUrlKind.Video, cancellationToken)
                            : null;

                        var providerUpdatedAnyField = false;

                        if (needsImage && validatedImage?.IsValid == true)
                        {
                            var replacingBrokenUrl = workItem.FinalBrokenProviderImage;
                            imageAction = request.DryRun
                                ? replacingBrokenUrl ? "would repair" : "would add"
                                : replacingBrokenUrl ? "repaired" : "added";

                            if (!request.DryRun)
                            {
                                item.ThumbnailUrl = validatedImage.Url;
                            }

                            if (replacingBrokenUrl)
                            {
                                response.BadUrlsRepaired++;
                            }
                            else
                            {
                                response.ImagesAdded++;
                            }

                            workItem.FinalBrokenProviderImage = false;
                            workItem.FinalHasEffectiveImage = true;
                            needsImage = false;
                            providerUpdatedAnyField = true;
                        }

                        if (needsVideo && validatedVideo?.IsValid == true)
                        {
                            var replacingBrokenUrl = workItem.FinalBrokenProviderVideo;
                            videoAction = request.DryRun
                                ? replacingBrokenUrl ? "would repair" : "would add"
                                : replacingBrokenUrl ? "repaired" : "added";

                            if (!request.DryRun)
                            {
                                item.VideoUrl = validatedVideo.Url;
                            }

                            if (replacingBrokenUrl)
                            {
                                response.BadUrlsRepaired++;
                            }
                            else
                            {
                                response.VideosAdded++;
                            }

                            workItem.FinalBrokenProviderVideo = false;
                            workItem.FinalHasEffectiveVideo = true;
                            needsVideo = false;
                            providerUpdatedAnyField = true;
                        }

                        if (!providerUpdatedAnyField)
                        {
                            continue;
                        }

                        providerNamesUsed.Add(provider.Name);
                        if (!string.IsNullOrWhiteSpace(result.MatchedName))
                        {
                            matchedNames.Add(result.MatchedName.Trim());
                        }

                        if (!string.IsNullOrWhiteSpace(result.Message))
                        {
                            messages.Add(result.Message);
                        }

                        maxConfidence = Math.Max(maxConfidence, result.Confidence);
                        itemUpdated = true;
                        IncrementProviderBreakdown(response.SourceBreakdown, provider.Name);
                        continue;
                    }
                }
            }

            workItem.NeedsImage = needsImage;
            workItem.NeedsVideo = needsVideo;

            if (itemUpdated)
            {
                if (!request.DryRun)
                {
                    item.LastSyncedAt = now;
                    item.UpdatedAt = now;
                }

                response.Updated++;
                TryAddUpdatedSample(
                    response.SampleUpdated,
                    item,
                    providerNamesUsed,
                    matchedNames,
                    maxConfidence,
                    imageAction,
                    videoAction,
                    messages);
            }
        }

        if (response.Updated > 0 && !request.DryRun)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        response.SkippedAmbiguous = ambiguousItems.Count;
        response.SkippedLowConfidence = lowConfidenceItems.Count;
        PopulateStillMissingSamples(response, workItems, request.MediaType);
        PopulateBrokenUnrepairedSamples(response, workItems);
        response.StillMissingImages = CountStillMissingImages(workItems, request.MediaType);
        response.StillMissingVideos = CountStillMissingVideos(workItems, request.MediaType);

        return response;
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

    public static string NormalizeNameWithoutEquipmentPrefix(string? value)
    {
        var tokens = GetNameTokens(value).ToList();
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        foreach (var prefixTokens in SecondaryEquipmentPrefixes)
        {
            if (tokens.Count <= prefixTokens.Length)
            {
                continue;
            }

            if (tokens.Take(prefixTokens.Length).SequenceEqual(prefixTokens, StringComparer.Ordinal))
            {
                tokens = tokens.Skip(prefixTokens.Length).ToList();
                break;
            }
        }

        return string.Join(' ', tokens);
    }

    public static string? BuildAbsoluteMediaUrl(string? value, string? assetBaseUrl, Uri? fallbackBaseAddress = null)
    {
        var normalizedValue = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return null;
        }

        if (Uri.TryCreate(normalizedValue, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var baseUri = TryGetAbsoluteUri(assetBaseUrl) ?? fallbackBaseAddress;
        if (baseUri is null)
        {
            return null;
        }

        var normalizedPath = NormalizeRelativeMediaPath(normalizedValue, baseUri);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        return new Uri(baseUri, normalizedPath).ToString();
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

    private async Task<ExerciseCatalogMediaWorkItem> BuildWorkItemAsync(
        ExerciseCatalogItem item,
        ExerciseCatalogMediaEnrichmentRequest request,
        CancellationToken cancellationToken)
    {
        var considerImages = request.MediaType is ExerciseMediaType.All or ExerciseMediaType.Images;
        var considerVideos = request.MediaType is ExerciseMediaType.All or ExerciseMediaType.Videos;

        ExerciseMediaUrlValidationResult? imageValidation = null;
        ExerciseMediaUrlValidationResult? videoValidation = null;
        var brokenProviderImage = false;
        var brokenProviderVideo = false;

        if (request.RepairBadUrls && considerImages && !string.IsNullOrWhiteSpace(item.ThumbnailUrl))
        {
            imageValidation = await _urlValidationService.ValidateImageUrlAsync(item.ThumbnailUrl, cancellationToken);
            brokenProviderImage = !imageValidation.IsValid;
        }

        if (request.RepairBadUrls && considerVideos && !string.IsNullOrWhiteSpace(item.VideoUrl))
        {
            videoValidation = await _urlValidationService.ValidateVideoUrlAsync(item.VideoUrl, cancellationToken);
            brokenProviderVideo = !videoValidation.IsValid;
        }

        var needsImage = considerImages
                         && (string.IsNullOrWhiteSpace(item.ThumbnailUrl) || (request.RepairBadUrls && brokenProviderImage));
        var needsVideo = considerVideos
                         && (string.IsNullOrWhiteSpace(item.VideoUrl) || (request.RepairBadUrls && brokenProviderVideo));

        return new ExerciseCatalogMediaWorkItem
        {
            Item = item,
            NeedsImage = needsImage,
            NeedsVideo = needsVideo,
            ShouldProcess = needsImage || needsVideo,
            BrokenProviderImage = brokenProviderImage,
            BrokenProviderVideo = brokenProviderVideo,
            BrokenProviderImageValidation = imageValidation,
            BrokenProviderVideoValidation = videoValidation,
            FinalBrokenProviderImage = brokenProviderImage,
            FinalBrokenProviderVideo = brokenProviderVideo,
            FinalHasEffectiveImage = !string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride)
                                     || (!string.IsNullOrWhiteSpace(item.ThumbnailUrl) && !brokenProviderImage),
            FinalHasEffectiveVideo = !string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride)
                                     || (!string.IsNullOrWhiteSpace(item.VideoUrl) && !brokenProviderVideo),
        };
    }

    private async Task<ExerciseMediaUrlValidationResult?> ValidateCandidateUrlAsync(
        string? url,
        ExerciseMediaUrlKind kind,
        CancellationToken cancellationToken)
    {
        if (!IsUsableUrl(url))
        {
            return null;
        }

        return kind == ExerciseMediaUrlKind.Image
            ? await _urlValidationService.ValidateImageUrlAsync(url, cancellationToken)
            : await _urlValidationService.ValidateVideoUrlAsync(url, cancellationToken);
    }

    private List<IExerciseMediaProvider> GetProviders(ExerciseMediaProviderSelection selection)
    {
        return selection switch
        {
            ExerciseMediaProviderSelection.Wger => _providers.Where(provider => string.Equals(provider.Name, "wger", StringComparison.OrdinalIgnoreCase)).ToList(),
            ExerciseMediaProviderSelection.ExerciseDb => _providers.Where(provider => string.Equals(provider.Name, "exercisedb", StringComparison.OrdinalIgnoreCase)).ToList(),
            ExerciseMediaProviderSelection.FreeExerciseDb => _providers.Where(provider => string.Equals(provider.Name, "free-exercise-db", StringComparison.OrdinalIgnoreCase)).ToList(),
            _ => _providers.ToList(),
        };
    }

    private static IQueryable<ExerciseCatalogItem> ApplySourceFilter(
        IQueryable<ExerciseCatalogItem> query,
        ExerciseCatalogMediaDiagnosticsSourceFilter sourceFilter)
    {
        return sourceFilter switch
        {
            ExerciseCatalogMediaDiagnosticsSourceFilter.Wger => query.Where(item => item.Source == "wger"),
            ExerciseCatalogMediaDiagnosticsSourceFilter.Local => query.Where(item => item.Source != "wger"),
            _ => query,
        };
    }

    private static int CountStillMissingImages(IEnumerable<ExerciseCatalogMediaWorkItem> workItems, ExerciseMediaType mediaType)
    {
        return mediaType is ExerciseMediaType.All or ExerciseMediaType.Images
            ? workItems.Count(item => !item.FinalHasEffectiveImage)
            : 0;
    }

    private static int CountStillMissingVideos(IEnumerable<ExerciseCatalogMediaWorkItem> workItems, ExerciseMediaType mediaType)
    {
        return mediaType is ExerciseMediaType.All or ExerciseMediaType.Videos
            ? workItems.Count(item => !item.FinalHasEffectiveVideo)
            : 0;
    }

    private static void PopulateStillMissingSamples(
        ExerciseCatalogMediaEnrichmentResponse response,
        IEnumerable<ExerciseCatalogMediaWorkItem> workItems,
        ExerciseMediaType mediaType)
    {
        foreach (var workItem in workItems)
        {
            if (response.SampleStillMissing.Count >= 25)
            {
                break;
            }

            if (!ShouldAddStillMissingSample(workItem, mediaType))
            {
                continue;
            }

            response.SampleStillMissing.Add(new ExerciseCatalogMediaStillMissingSample
            {
                Id = workItem.Item.Id,
                Name = workItem.Item.Name,
                Source = workItem.Item.Source,
                ExternalId = workItem.Item.ExternalId,
                PrimaryMuscle = workItem.Item.PrimaryMuscle,
                Equipment = workItem.Item.Equipment,
                ReasonMissing = BuildEnrichmentMissingReason(workItem, mediaType),
            });
        }
    }

    private static void PopulateBrokenUnrepairedSamples(
        ExerciseCatalogMediaEnrichmentResponse response,
        IEnumerable<ExerciseCatalogMediaWorkItem> workItems)
    {
        foreach (var workItem in workItems)
        {
            if (response.SampleBrokenUnrepaired.Count >= 25)
            {
                break;
            }

            if (workItem.FinalBrokenProviderImage
                && workItem.BrokenProviderImageValidation is not null
                && response.SampleBrokenUnrepaired.Count < 25)
            {
                response.SampleBrokenUnrepaired.Add(new ExerciseCatalogMediaBrokenUrlSample
                {
                    Id = workItem.Item.Id,
                    Name = workItem.Item.Name,
                    MediaField = "thumbnailUrl",
                    Url = workItem.BrokenProviderImageValidation.Url,
                    StatusCode = workItem.BrokenProviderImageValidation.StatusCode,
                    Error = workItem.BrokenProviderImageValidation.Error,
                });
            }

            if (workItem.FinalBrokenProviderVideo
                && workItem.BrokenProviderVideoValidation is not null
                && response.SampleBrokenUnrepaired.Count < 25)
            {
                response.SampleBrokenUnrepaired.Add(new ExerciseCatalogMediaBrokenUrlSample
                {
                    Id = workItem.Item.Id,
                    Name = workItem.Item.Name,
                    MediaField = "videoUrl",
                    Url = workItem.BrokenProviderVideoValidation.Url,
                    StatusCode = workItem.BrokenProviderVideoValidation.StatusCode,
                    Error = workItem.BrokenProviderVideoValidation.Error,
                });
            }
        }
    }

    private static bool ShouldAddStillMissingSample(ExerciseCatalogMediaWorkItem workItem, ExerciseMediaType mediaType)
    {
        return (mediaType is ExerciseMediaType.All or ExerciseMediaType.Images) && !workItem.FinalHasEffectiveImage
               || (mediaType is ExerciseMediaType.All or ExerciseMediaType.Videos) && !workItem.FinalHasEffectiveVideo;
    }

    private static bool IsMissingAnyEffectiveMedia(ExerciseCatalogItem item)
    {
        return !HasEffectiveThumbnail(item) || !HasEffectiveVideo(item);
    }

    private static bool HasEffectiveThumbnail(ExerciseCatalogItem item)
    {
        return !string.IsNullOrWhiteSpace(GetEffectiveThumbnailUrl(item));
    }

    private static bool HasEffectiveVideo(ExerciseCatalogItem item)
    {
        return !string.IsNullOrWhiteSpace(GetEffectiveVideoUrl(item));
    }

    private static string? GetEffectiveThumbnailUrl(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride) ? item.ThumbnailUrl : item.LocalThumbnailUrlOverride.Trim();
    }

    private static string? GetEffectiveVideoUrl(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride) ? item.VideoUrl : item.LocalVideoUrlOverride.Trim();
    }

    private static string GetEffectiveThumbnailField(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride)
            ? "thumbnailUrl"
            : "localThumbnailUrlOverride";
    }

    private static void IncrementProviderBreakdown(Dictionary<string, int> sourceBreakdown, string providerName)
    {
        sourceBreakdown.TryGetValue(providerName, out var currentCount);
        sourceBreakdown[providerName] = currentCount + 1;
    }

    private static void TryAddUpdatedSample(
        List<ExerciseCatalogMediaEnrichmentSample> samples,
        ExerciseCatalogItem item,
        IEnumerable<string> providerNamesUsed,
        IEnumerable<string> matchedNames,
        double confidence,
        string? imageAction,
        string? videoAction,
        IEnumerable<string> messages)
    {
        if (samples.Count >= 25)
        {
            return;
        }

        samples.Add(new ExerciseCatalogMediaEnrichmentSample
        {
            Id = item.Id,
            Name = item.Name,
            Source = item.Source,
            ProviderUsed = string.Join(", ", providerNamesUsed),
            MatchedExerciseName = JoinDistinctOrNull(matchedNames, " | "),
            Confidence = confidence,
            ImageAction = imageAction,
            VideoAction = videoAction,
            Message = JoinDistinctOrNull(messages, " / "),
        });
    }

    private static string BuildDiagnosticsMissingReason(ExerciseCatalogItem item)
    {
        var missingThumbnail = !HasEffectiveThumbnail(item);
        var missingVideo = !HasEffectiveVideo(item);

        return (missingThumbnail, missingVideo) switch
        {
            (true, true) => "Missing effective thumbnail and video.",
            (true, false) => "Missing effective thumbnail.",
            (false, true) => "Missing effective video.",
            _ => "Media is available.",
        };
    }

    private static string BuildEnrichmentMissingReason(ExerciseCatalogMediaWorkItem workItem, ExerciseMediaType mediaType)
    {
        var reasons = new List<string>();

        if (mediaType is ExerciseMediaType.All or ExerciseMediaType.Images && !workItem.FinalHasEffectiveImage)
        {
            reasons.Add(workItem.FinalBrokenProviderImage
                ? "thumbnail URL is broken and no replacement passed validation"
                : "no high-confidence thumbnail match was found");
        }

        if (mediaType is ExerciseMediaType.All or ExerciseMediaType.Videos && !workItem.FinalHasEffectiveVideo)
        {
            reasons.Add(workItem.FinalBrokenProviderVideo
                ? "video URL is broken and no replacement passed validation"
                : "no high-confidence video match was found");
        }

        return reasons.Count == 0 ? "Media is available." : string.Join("; ", reasons);
    }

    private static string? JoinDistinctOrNull(IEnumerable<string> values, string separator)
    {
        var normalizedValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return normalizedValues.Count == 0 ? null : string.Join(separator, normalizedValues);
    }

    private static Uri? TryGetAbsoluteUri(string? value)
    {
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ? uri : null;
    }

    private static string NormalizeRelativeMediaPath(string value, Uri baseUri)
    {
        var trimmed = value.Trim().Replace('\\', '/');

        while (trimmed.StartsWith("./", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        trimmed = trimmed.TrimStart('/');

        var suffixStart = trimmed.IndexOfAny(['?', '#']);
        var pathPart = suffixStart >= 0 ? trimmed[..suffixStart] : trimmed;
        var suffix = suffixStart >= 0 ? trimmed[suffixStart..] : string.Empty;
        var basePath = baseUri.AbsolutePath.Trim('/');

        if (basePath.EndsWith("exercises", StringComparison.OrdinalIgnoreCase)
            && pathPart.StartsWith("exercises/", StringComparison.OrdinalIgnoreCase))
        {
            pathPart = pathPart["exercises/".Length..];
        }

        var encodedPath = string.Join(
            '/',
            pathPart
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(segment => Uri.EscapeDataString(Uri.UnescapeDataString(segment))));

        return string.IsNullOrWhiteSpace(encodedPath)
            ? string.Empty
            : $"{encodedPath}{suffix}";
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
            ExerciseMediaProviderSelection.FreeExerciseDb => "free-exercise-db",
            _ => "all",
        };
    }

    private static string ToApiValue(ExerciseCatalogMediaDiagnosticsSourceFilter sourceFilter)
    {
        return sourceFilter switch
        {
            ExerciseCatalogMediaDiagnosticsSourceFilter.Wger => "wger",
            ExerciseCatalogMediaDiagnosticsSourceFilter.Local => "local",
            _ => "all",
        };
    }

    private sealed class ExerciseCatalogMediaWorkItem
    {
        public required ExerciseCatalogItem Item { get; init; }
        public bool NeedsImage { get; set; }
        public bool NeedsVideo { get; set; }
        public bool ShouldProcess { get; init; }
        public bool BrokenProviderImage { get; init; }
        public bool BrokenProviderVideo { get; init; }
        public ExerciseMediaUrlValidationResult? BrokenProviderImageValidation { get; init; }
        public ExerciseMediaUrlValidationResult? BrokenProviderVideoValidation { get; init; }
        public bool FinalBrokenProviderImage { get; set; }
        public bool FinalBrokenProviderVideo { get; set; }
        public bool FinalHasEffectiveImage { get; set; }
        public bool FinalHasEffectiveVideo { get; set; }
    }
}
