using System.Net.Http.Json;
using System.Text.Json;
using backend.Configuration;
using backend.Models;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class FreeExerciseDbMediaProvider : IExerciseMediaProvider
{
    public string Name => "free-exercise-db";
    public int Priority => 300;

    private readonly HttpClient _httpClient;
    private readonly ExerciseDbMediaProviderOptions _options;
    private readonly ILogger<FreeExerciseDbMediaProvider> _logger;
    private IReadOnlyList<FreeExerciseDbMediaEntry>? _entries;

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public FreeExerciseDbMediaProvider(
        HttpClient httpClient,
        IOptions<ExerciseMediaEnrichmentOptions> options,
        ILogger<FreeExerciseDbMediaProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value.FreeExerciseDb;
        _logger = logger;
    }

    public async Task<ExerciseMediaMatchResult> TryGetMediaAsync(
        ExerciseCatalogItem item,
        ExerciseMediaRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return ExerciseMediaMatchResult.Skipped("Free Exercise DB media enrichment is disabled.");
        }

        var entries = await EnsureEntriesLoadedAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return ExerciseMediaMatchResult.NotFound("Free Exercise DB returned no media entries.");
        }

        var normalizedName = ExerciseCatalogMediaEnrichmentService.NormalizeName(item.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return ExerciseMediaMatchResult.NotFound("Exercise name is empty after normalization.");
        }

        var eligibleEntries = entries
            .Where(entry =>
                (!requirement.NeedImage || ExerciseCatalogMediaEnrichmentService.IsUsableUrl(entry.ImageUrl))
                || (!requirement.NeedVideo || ExerciseCatalogMediaEnrichmentService.IsUsableUrl(entry.VideoUrl)))
            .ToList();

        if (eligibleEntries.Count == 0)
        {
            return ExerciseMediaMatchResult.NotFound("Free Exercise DB has no usable media entries for the requested media type.");
        }

        var exactMatches = eligibleEntries
            .Where(entry => string.Equals(entry.NormalizedName, normalizedName, StringComparison.Ordinal))
            .ToList();

        if (exactMatches.Count == 1)
        {
            return BuildFoundResult(exactMatches[0], requirement, 1, "Matched by exact normalized exercise name.");
        }

        if (exactMatches.Count > 1)
        {
            var exactDisambiguated = DisambiguateExactMatches(item, exactMatches);
            if (exactDisambiguated.Count == 1)
            {
                return BuildFoundResult(exactDisambiguated[0], requirement, 0.99, "Matched by exact normalized name with equipment/muscle disambiguation.");
            }

            return ExerciseMediaMatchResult.Ambiguous("Multiple Free Exercise DB entries matched the normalized exercise name.", 0.99);
        }

        var rankedMatches = eligibleEntries
            .Select(entry => new RankedFreeExerciseDbMatch(entry, ScoreCandidate(item, entry)))
            .Where(match => match.Score >= 0.9)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (rankedMatches.Count == 0)
        {
            return ExerciseMediaMatchResult.NotFound("No high-confidence Free Exercise DB fallback match was found.");
        }

        var bestMatch = rankedMatches[0];
        var secondBestScore = rankedMatches.Count > 1 ? rankedMatches[1].Score : 0;

        if (bestMatch.Score >= 0.96 && bestMatch.Score - secondBestScore >= 0.05)
        {
            return BuildFoundResult(bestMatch.Entry, requirement, bestMatch.Score, "Matched by high-confidence fuzzy name comparison.");
        }

        return ExerciseMediaMatchResult.Ambiguous(
            "Free Exercise DB produced multiple plausible matches, so enrichment was skipped.",
            bestMatch.Score);
    }

    private async Task<IReadOnlyList<FreeExerciseDbMediaEntry>> EnsureEntriesLoadedAsync(CancellationToken cancellationToken)
    {
        if (_entries is not null)
        {
            return _entries;
        }

        JsonElement payload;
        try
        {
            payload = await _httpClient.GetFromJsonAsync<JsonElement>(_options.ExercisePath, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to fetch Free Exercise DB data from {Path}.", _options.ExercisePath);
            throw;
        }

        var elements = ExtractEntries(payload).Take(Math.Max(1, _options.MaxDatasetItems)).ToList();
        _entries = elements
            .Select(MapEntry)
            .Where(entry => entry is not null)
            .Cast<FreeExerciseDbMediaEntry>()
            .ToList();

        return _entries;
    }

    private IEnumerable<JsonElement> ExtractEntries(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            return payload.EnumerateArray();
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "results", "data", "exercises", "items" })
            {
                if (payload.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
                {
                    return property.EnumerateArray();
                }
            }
        }

        return [];
    }

    private FreeExerciseDbMediaEntry? MapEntry(JsonElement item)
    {
        var name = NormalizeOptionalText(
            GetString(item, "name")
            ?? GetString(item, "exerciseName")
            ?? GetString(item, "title"));

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var imageUrl = BuildAbsoluteUrl(
            GetString(item, "thumbnailUrl")
            ?? GetString(item, "imageUrl")
            ?? GetString(item, "gifUrl")
            ?? GetString(item, "image")
            ?? GetString(item, "gif"));

        var videoUrl = BuildAbsoluteUrl(
            GetString(item, "videoUrl")
            ?? GetString(item, "video"));

        return new FreeExerciseDbMediaEntry
        {
            Name = name,
            NormalizedName = ExerciseCatalogMediaEnrichmentService.NormalizeName(name),
            ImageUrl = ExerciseCatalogMediaEnrichmentService.IsUsableUrl(imageUrl) ? imageUrl : null,
            VideoUrl = ExerciseCatalogMediaEnrichmentService.IsUsableUrl(videoUrl) ? videoUrl : null,
            Equipment = NormalizeOptionalText(GetString(item, "equipment") ?? GetFirstString(item, "equipments")),
            BodyPart = NormalizeOptionalText(GetString(item, "bodyPart") ?? GetFirstString(item, "bodyParts")),
            TargetMuscle = NormalizeOptionalText(GetString(item, "target") ?? GetFirstString(item, "targetMuscles")),
        };
    }

    private string? BuildAbsoluteUrl(string? value)
    {
        var normalizedValue = NormalizeOptionalText(value);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return null;
        }

        if (Uri.TryCreate(normalizedValue, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        if (!string.IsNullOrWhiteSpace(_options.AssetBaseUrl) && Uri.TryCreate(_options.AssetBaseUrl, UriKind.Absolute, out var assetBaseUri))
        {
            return new Uri(assetBaseUri, normalizedValue).ToString();
        }

        if (_httpClient.BaseAddress is not null)
        {
            return new Uri(_httpClient.BaseAddress, normalizedValue).ToString();
        }

        return null;
    }

    private static ExerciseMediaMatchResult BuildFoundResult(
        FreeExerciseDbMediaEntry entry,
        ExerciseMediaRequirement requirement,
        double confidence,
        string message)
    {
        var imageUrl = requirement.NeedImage ? entry.ImageUrl : null;
        var videoUrl = requirement.NeedVideo ? entry.VideoUrl : null;

        if (!ExerciseCatalogMediaEnrichmentService.IsUsableUrl(imageUrl) && !ExerciseCatalogMediaEnrichmentService.IsUsableUrl(videoUrl))
        {
            return ExerciseMediaMatchResult.NotFound("Matched Free Exercise DB entry has no usable media for the requested fields.");
        }

        return ExerciseMediaMatchResult.Found(imageUrl, videoUrl, confidence, message);
    }

    private static List<FreeExerciseDbMediaEntry> DisambiguateExactMatches(
        ExerciseCatalogItem item,
        IEnumerable<FreeExerciseDbMediaEntry> matches)
    {
        var equipment = ExerciseCatalogMediaEnrichmentService.NormalizeName(item.Equipment);
        var primaryMuscle = ExerciseCatalogMediaEnrichmentService.NormalizeName(item.PrimaryMuscle);
        var secondaryMuscles = (item.SecondaryMuscles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ExerciseCatalogMediaEnrichmentService.NormalizeName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        return matches
            .Where(entry =>
                string.Equals(entry.EquipmentNormalized, equipment, StringComparison.Ordinal)
                || string.Equals(entry.TargetMuscleNormalized, primaryMuscle, StringComparison.Ordinal)
                || secondaryMuscles.Contains(entry.TargetMuscleNormalized))
            .ToList();
    }

    private static double ScoreCandidate(ExerciseCatalogItem item, FreeExerciseDbMediaEntry entry)
    {
        var nameSimilarity = Math.Max(
            ExerciseCatalogMediaEnrichmentService.GetLevenshteinSimilarity(item.Name, entry.Name),
            ExerciseCatalogMediaEnrichmentService.GetTokenSimilarity(item.Name, entry.Name));

        var tokenContainment = ExerciseCatalogMediaEnrichmentService.GetTokenContainmentSimilarity(item.Name, entry.Name);
        var equipmentMatch = !string.IsNullOrWhiteSpace(item.Equipment)
            && string.Equals(
                ExerciseCatalogMediaEnrichmentService.NormalizeName(item.Equipment),
                entry.EquipmentNormalized,
                StringComparison.Ordinal);

        var muscleMatch = MatchesMuscle(item, entry);
        var score = nameSimilarity;

        if (tokenContainment >= 1 && (equipmentMatch || muscleMatch))
        {
            score = Math.Max(score, 0.95);
        }

        if (equipmentMatch)
        {
            score += 0.03;
        }

        if (muscleMatch)
        {
            score += 0.03;
        }

        return Math.Min(score, 1);
    }

    private static bool MatchesMuscle(ExerciseCatalogItem item, FreeExerciseDbMediaEntry entry)
    {
        var primaryMuscle = ExerciseCatalogMediaEnrichmentService.NormalizeName(item.PrimaryMuscle);
        if (string.Equals(primaryMuscle, entry.TargetMuscleNormalized, StringComparison.Ordinal))
        {
            return true;
        }

        return (item.SecondaryMuscles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ExerciseCatalogMediaEnrichmentService.NormalizeName)
            .Any(muscle => string.Equals(muscle, entry.TargetMuscleNormalized, StringComparison.Ordinal));
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

    private static string? GetFirstString(JsonElement item, string propertyName)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in property.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString()))
                {
                    return element.GetString();
                }
            }
        }

        return null;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class FreeExerciseDbMediaEntry
    {
        public string Name { get; init; } = string.Empty;
        public string NormalizedName { get; init; } = string.Empty;
        public string? ImageUrl { get; init; }
        public string? VideoUrl { get; init; }
        public string? Equipment { get; init; }
        public string? BodyPart { get; init; }
        public string? TargetMuscle { get; init; }

        public string EquipmentNormalized => ExerciseCatalogMediaEnrichmentService.NormalizeName(Equipment);
        public string BodyPartNormalized => ExerciseCatalogMediaEnrichmentService.NormalizeName(BodyPart);
        public string TargetMuscleNormalized => ExerciseCatalogMediaEnrichmentService.NormalizeName(TargetMuscle);
    }

    private sealed record RankedFreeExerciseDbMatch(FreeExerciseDbMediaEntry Entry, double Score);
}
