using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using backend.Configuration;
using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public interface IWgerExerciseCatalogSyncService
{
    Task<ExerciseCatalogSyncResponse> SyncAsync(CancellationToken cancellationToken = default);
}

public partial class WgerExerciseCatalogSyncService : IWgerExerciseCatalogSyncService
{
    private const string SourceName = "wger";

    private readonly HttpClient _httpClient;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<WgerExerciseCatalogSyncService> _logger;
    private readonly WgerOptions _options;

    public WgerExerciseCatalogSyncService(
        HttpClient httpClient,
        AppDbContext dbContext,
        IOptions<WgerOptions> options,
        ILogger<WgerExerciseCatalogSyncService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ExerciseCatalogSyncResponse> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new ExerciseCatalogSyncResponse
            {
                Provider = SourceName,
                IsEnabled = false,
                Message = "Wger sync is disabled.",
            };
        }

        var pageSize = Math.Clamp(_options.PageSize, 1, 100);
        var maxItems = Math.Max(pageSize, _options.MaxItems);
        var processed = 0;
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var itemsWithThumbnail = 0;
        var itemsWithVideo = 0;
        var itemsMissingMedia = 0;
        var mediaExtractionFailures = 0;
        var offset = 0;
        var syncedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Starting Wger exercise catalog sync. BaseUrl={BaseUrl} PageSize={PageSize} MaxItems={MaxItems} LanguageId={LanguageId} LanguageCode={LanguageCode}",
            _options.BaseUrl,
            pageSize,
            maxItems,
            _options.LanguageId,
            _options.LanguageCode);

        var existingItems = await _dbContext.ExerciseCatalogItems
            .Where(item => item.Source == SourceName && item.ExternalId != null)
            .ToDictionaryAsync(
                item => item.ExternalId!,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        while (processed < maxItems)
        {
            var remaining = maxItems - processed;
            var limit = Math.Min(pageSize, remaining);
            var requestUri = $"exerciseinfo/?limit={limit}&offset={offset}";

            WgerPagedResponse<WgerExerciseInfoDto>? page;
            try
            {
                page = await _httpClient.GetFromJsonAsync<WgerPagedResponse<WgerExerciseInfoDto>>(requestUri, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Wger sync failed while fetching page at offset {Offset}.", offset);
                throw;
            }

            if (page?.Results is null || page.Results.Count == 0)
            {
                break;
            }

            foreach (var exercise in page.Results)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processed++;

                var mapped = MapExercise(exercise, syncedAt);
                if (mapped is null)
                {
                    skipped++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(mapped.ThumbnailUrl))
                {
                    itemsWithThumbnail++;
                }

                if (!string.IsNullOrWhiteSpace(mapped.VideoUrl))
                {
                    itemsWithVideo++;
                }

                if (string.IsNullOrWhiteSpace(mapped.ThumbnailUrl) && string.IsNullOrWhiteSpace(mapped.VideoUrl))
                {
                    itemsMissingMedia++;
                }

                if ((exercise.Images.Count > 0 && string.IsNullOrWhiteSpace(mapped.ThumbnailUrl))
                    || (exercise.Videos.Count > 0 && string.IsNullOrWhiteSpace(mapped.VideoUrl)))
                {
                    mediaExtractionFailures++;
                    _logger.LogDebug(
                        "Wger media metadata was present but no usable media URL was selected. ExerciseId={ExerciseId} ImageCandidates={ImageCandidates} VideoCandidates={VideoCandidates}",
                        exercise.Id,
                        exercise.Images.Count,
                        exercise.Videos.Count);
                }

                if (existingItems.TryGetValue(mapped.ExternalId!, out var existingItem))
                {
                    ApplyUpdate(existingItem, mapped, syncedAt);
                    updated++;
                    continue;
                }

                _dbContext.ExerciseCatalogItems.Add(mapped);
                existingItems[mapped.ExternalId!] = mapped;
                created++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(page.Next))
            {
                break;
            }

            offset += page.Results.Count;
        }

        var response = new ExerciseCatalogSyncResponse
        {
            Provider = SourceName,
            IsEnabled = true,
            Processed = processed,
            Created = created,
            Updated = updated,
            Skipped = skipped,
            Message = $"Processed {processed} Wger exercises. Created {created}, updated {updated}, skipped {skipped}.",
        };

        _logger.LogInformation(
            "Completed Wger exercise catalog sync. Processed={Processed} Created={Created} Updated={Updated} Skipped={Skipped}",
            processed,
            created,
            updated,
            skipped);

        _logger.LogInformation(
            "Wger media enrichment summary. ItemsWithThumbnail={ItemsWithThumbnail} ItemsWithVideo={ItemsWithVideo} ItemsMissingMedia={ItemsMissingMedia} MediaExtractionFailures={MediaExtractionFailures}",
            itemsWithThumbnail,
            itemsWithVideo,
            itemsMissingMedia,
            mediaExtractionFailures);

        return response;
    }

    private ExerciseCatalogItem? MapExercise(WgerExerciseInfoDto exercise, DateTime syncedAt)
    {
        var translation = SelectTranslation(exercise.Translations);
        var name = NormalizeText(GetString(translation, "name"));

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Skipping Wger exercise {ExerciseId} because it has no usable name for the configured language.", exercise.Id);
            return null;
        }

        var descriptionText = NormalizeRichText(GetString(translation, "description"));
        var primaryMuscle = NormalizeText(GetFirstName(exercise.Muscles));

        var secondaryMuscles = new List<string>();

        if (exercise.Muscles.Count > 1)
        {
            secondaryMuscles.AddRange(exercise.Muscles
                .Skip(1)
                .Select(GetName)
                .Where(value => !string.IsNullOrWhiteSpace(value))!
                .Select(NormalizeText)
                .Where(value => !string.IsNullOrWhiteSpace(value))!);
        }

        secondaryMuscles.AddRange(exercise.MusclesSecondary
            .Select(GetName)
            .Where(value => !string.IsNullOrWhiteSpace(value))!
            .Select(NormalizeText)
            .Where(value => !string.IsNullOrWhiteSpace(value))!);

        var equipment = string.Join(", ", exercise.Equipment
            .Select(GetName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase));

        var thumbnailUrl = SelectBestMediaUrl(exercise.Images, MediaKind.Image);
        var videoUrl = SelectBestMediaUrl(exercise.Videos, MediaKind.Video);
        var externalId = exercise.Id.ToString();
        var slug = BuildSlug(name, externalId);

        return new ExerciseCatalogItem
        {
            Source = SourceName,
            ExternalId = externalId,
            Name = name,
            Slug = slug,
            Description = descriptionText,
            Instructions = descriptionText,
            PrimaryMuscle = primaryMuscle,
            SecondaryMuscles = string.Join(',', secondaryMuscles.Distinct(StringComparer.OrdinalIgnoreCase)),
            Equipment = string.IsNullOrWhiteSpace(equipment) ? null : equipment,
            Difficulty = null,
            VideoUrl = videoUrl,
            ThumbnailUrl = thumbnailUrl,
            LocalMediaPath = null,
            IsActive = true,
            LastSyncedAt = syncedAt,
            CreatedAt = syncedAt,
            UpdatedAt = syncedAt,
        };
    }

    private void ApplyUpdate(ExerciseCatalogItem target, ExerciseCatalogItem source, DateTime syncedAt)
    {
        target.Name = source.Name;
        target.Slug = source.Slug;
        target.Description = ChooseUpdatedValue(target.Description, source.Description);
        target.Instructions = ChooseUpdatedValue(target.Instructions, source.Instructions);
        target.PrimaryMuscle = ChooseUpdatedValue(target.PrimaryMuscle, source.PrimaryMuscle);
        target.SecondaryMuscles = ChooseUpdatedValue(target.SecondaryMuscles, source.SecondaryMuscles) ?? string.Empty;
        target.Equipment = ChooseUpdatedValue(target.Equipment, source.Equipment);
        target.VideoUrl = ChooseUpdatedValue(target.VideoUrl, source.VideoUrl);
        target.ThumbnailUrl = ChooseUpdatedValue(target.ThumbnailUrl, source.ThumbnailUrl);
        target.IsActive = true;
        target.LastSyncedAt = syncedAt;
        target.UpdatedAt = syncedAt;
    }

    private JsonElement? SelectTranslation(IReadOnlyList<JsonElement> translations)
    {
        if (translations.Count == 0)
        {
            return null;
        }

        if (_options.LanguageId.HasValue)
        {
            var exactLanguageIdMatch = translations.FirstOrDefault(translation => GetLanguageId(translation) == _options.LanguageId.Value);
            if (exactLanguageIdMatch.ValueKind != JsonValueKind.Undefined)
            {
                return exactLanguageIdMatch;
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.LanguageCode))
        {
            var exactLanguageCodeMatch = translations.FirstOrDefault(translation =>
                string.Equals(GetLanguageCode(translation), _options.LanguageCode, StringComparison.OrdinalIgnoreCase));

            if (exactLanguageCodeMatch.ValueKind != JsonValueKind.Undefined)
            {
                return exactLanguageCodeMatch;
            }
        }

        var firstNamedTranslation = translations.FirstOrDefault(translation => !string.IsNullOrWhiteSpace(GetString(translation, "name")));
        if (firstNamedTranslation.ValueKind != JsonValueKind.Undefined)
        {
            return firstNamedTranslation;
        }

        return translations[0];
    }

    private static string? GetFirstName(IEnumerable<JsonElement> items)
    {
        return items
            .Select(GetName)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? SelectBestMediaUrl(IEnumerable<JsonElement> items, MediaKind mediaKind)
    {
        MediaCandidate? best = null;

        foreach (var item in items)
        {
            var url = NormalizeText(GetBestMediaUrlFromItem(item, mediaKind));
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var candidate = new MediaCandidate(
                url,
                ScoreMediaCandidate(item, mediaKind));

            if (best is null || candidate.Score > best.Score)
            {
                best = candidate;
            }
        }

        return best?.Url;
    }

    private static string? GetName(JsonElement item)
    {
        return NormalizeText(
            GetString(item, "name")
            ?? GetString(item, "name_en")
            ?? GetString(item, "name_original")
            ?? GetString(item, "short_name"));
    }

    private static string? ChooseUpdatedValue(string? existingValue, string? incomingValue)
    {
        var normalizedIncoming = NormalizeText(incomingValue);
        return string.IsNullOrWhiteSpace(normalizedIncoming)
            ? existingValue
            : normalizedIncoming;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeRichText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var stripped = HtmlTagRegex().Replace(value, " ");
        stripped = System.Net.WebUtility.HtmlDecode(stripped);
        stripped = WhitespaceRegex().Replace(stripped, " ").Trim();

        return string.IsNullOrWhiteSpace(stripped) ? null : stripped;
    }

    private static string BuildSlug(string name, string externalId)
    {
        var normalized = name.ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var baseSlug = builder.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "exercise";
        }

        return $"{baseSlug}-{SourceName}-{externalId}";
    }

    private static int? GetLanguageId(JsonElement translation)
    {
        if (translation.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (translation.TryGetProperty("language", out var language))
        {
            if (language.ValueKind == JsonValueKind.Number && language.TryGetInt32(out var languageId))
            {
                return languageId;
            }

            if (language.ValueKind == JsonValueKind.Object)
            {
                if (language.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var nestedLanguageId))
                {
                    return nestedLanguageId;
                }
            }
        }

        if (translation.TryGetProperty("language_id", out var languageIdProperty)
            && languageIdProperty.ValueKind == JsonValueKind.Number
            && languageIdProperty.TryGetInt32(out var propertyLanguageId))
        {
            return propertyLanguageId;
        }

        return null;
    }

    private static string? GetLanguageCode(JsonElement translation)
    {
        if (translation.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (translation.TryGetProperty("language_code", out var code))
        {
            return code.ValueKind == JsonValueKind.String ? code.GetString() : null;
        }

        if (translation.TryGetProperty("language", out var language) && language.ValueKind == JsonValueKind.Object)
        {
            if (language.TryGetProperty("short_name", out var shortName) && shortName.ValueKind == JsonValueKind.String)
            {
                return shortName.GetString();
            }

            if (language.TryGetProperty("name_short", out var nameShort) && nameShort.ValueKind == JsonValueKind.String)
            {
                return nameShort.GetString();
            }
        }

        return null;
    }

    private static string? GetString(JsonElement? element, string propertyName)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(element.Value, propertyName);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var propertyValue))
        {
            return null;
        }

        return propertyValue.ValueKind switch
        {
            JsonValueKind.String => propertyValue.GetString(),
            JsonValueKind.Number => propertyValue.GetRawText(),
            _ => null,
        };
    }

    private static string? GetBestMediaUrlFromItem(JsonElement item, MediaKind mediaKind)
    {
        var preferredKeys = mediaKind == MediaKind.Image
            ? new[] { "image_thumbnail", "thumbnail", "medium", "small", "image", "url", "original" }
            : new[] { "url", "video", "video_url", "file", "source" };

        foreach (var key in preferredKeys)
        {
            var value = NormalizeText(GetString(item, key));
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static int ScoreMediaCandidate(JsonElement item, MediaKind mediaKind)
    {
        var score = 0;

        if (GetBoolean(item, "is_main") == true
            || GetBoolean(item, "main") == true
            || GetBoolean(item, "featured") == true)
        {
            score += 10;
        }

        if (GetBoolean(item, "is_approved") == true
            || GetBoolean(item, "approved") == true)
        {
            score += 8;
        }

        var status = NormalizeText(
            GetString(item, "status")
            ?? GetString(item, "state")
            ?? GetString(item, "approval_status"));

        if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        if (mediaKind == MediaKind.Image)
        {
            if (!string.IsNullOrWhiteSpace(GetString(item, "image_thumbnail"))
                || !string.IsNullOrWhiteSpace(GetString(item, "thumbnail")))
            {
                score += 4;
            }

            if (!string.IsNullOrWhiteSpace(GetString(item, "image"))
                || !string.IsNullOrWhiteSpace(GetString(item, "original")))
            {
                score += 2;
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(GetString(item, "url"))
                || !string.IsNullOrWhiteSpace(GetString(item, "video_url")))
            {
                score += 4;
            }
        }

        return score;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var propertyValue))
        {
            return null;
        }

        return propertyValue.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when propertyValue.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String when bool.TryParse(propertyValue.GetString(), out var boolean) => boolean,
            _ => null,
        };
    }

    [GeneratedRegex("<.*?>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    private enum MediaKind
    {
        Image,
        Video,
    }

    private sealed record MediaCandidate(string Url, int Score);
}
