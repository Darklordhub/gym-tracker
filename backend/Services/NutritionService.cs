using backend.Configuration;
using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Collections.Concurrent;
using System.Text.Json;

namespace backend.Services;

public class NutritionService : INutritionService
{
    private const string PrimarySource = "usda";
    private const int MaxPageSize = 50;
    private static readonly TimeSpan CatalogFreshnessWindow = TimeSpan.FromDays(NutritionOptions.CatalogFreshnessDays);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DetailRefreshLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;
    private readonly IReadOnlyDictionary<string, INutritionProvider> _providers;
    private readonly NutritionOptions _options;
    private readonly ILogger<NutritionService> _logger;

    public NutritionService(
        AppDbContext dbContext,
        IEnumerable<INutritionProvider> providers,
        IMemoryCache memoryCache,
        IOptions<NutritionOptions> options,
        ILogger<NutritionService> logger)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
        _options = options.Value;
        _logger = logger;
        _providers = providers.ToDictionary(
            provider => provider.Source,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<FoodSearchResultDto>> SearchFoodsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = NormalizeRequiredText(query, nameof(query), "Search query is required.");
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var provider = GetProvider(PrimarySource);
        var cacheKey = BuildSearchCacheKey(provider.Source, normalizedQuery, normalizedPage, normalizedPageSize);

        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(_options.SearchCacheMinutes, 1));
            return await provider.SearchFoodsAsync(normalizedQuery, normalizedPage, normalizedPageSize, cancellationToken);
        }) ?? [];
    }

    public async Task<FoodDetailDto?> GetFoodDetailAsync(
        string source,
        string externalId,
        CancellationToken cancellationToken)
    {
        var normalizedSource = NormalizeRequiredText(source, nameof(source), "Nutrition source is required.");
        var normalizedExternalId = NormalizeRequiredText(externalId, nameof(externalId), "External ID is required.");
        var cachedItem = await FindCachedItemAsync(normalizedSource, normalizedExternalId, cancellationToken);

        if (IsFresh(cachedItem))
        {
            return MapCachedItemToFoodDetail(cachedItem!);
        }

        var provider = GetProvider(normalizedSource);
        var lockKey = BuildDetailCacheKey(provider.Source, normalizedExternalId);
        var refreshLock = DetailRefreshLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));

        await refreshLock.WaitAsync(cancellationToken);

        try
        {
            cachedItem = await FindCachedItemAsync(provider.Source, normalizedExternalId, cancellationToken);

            if (IsFresh(cachedItem))
            {
                return MapCachedItemToFoodDetail(cachedItem!);
            }

            var providerDetail = await FetchProviderDetailWithMemoryCacheAsync(provider, normalizedExternalId, cancellationToken);

            if (providerDetail is null)
            {
                return null;
            }

            var persistedItem = await UpsertCachedItemAsync(providerDetail, cancellationToken);
            return MapProviderDetailToFoodDetail(providerDetail, persistedItem);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public async Task<NutritionCalculateResult> CalculateAsync(
        NutritionCalculateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Ingredients.Count == 0)
        {
            throw new ArgumentException("At least one ingredient is required.", nameof(request));
        }

        var results = new List<IngredientNutritionResult>(request.Ingredients.Count);

        foreach (var ingredient in request.Ingredients)
        {
            var source = NormalizeRequiredText(ingredient.Source, nameof(ingredient.Source), "Ingredient source is required.");
            var externalId = NormalizeRequiredText(ingredient.ExternalId, nameof(ingredient.ExternalId), "Ingredient external ID is required.");
            var normalizedUnit = NormalizeUnit(ingredient.Unit);

            if (normalizedUnit != "g")
            {
                throw new ArgumentException(
                    $"Unit '{ingredient.Unit}' is not supported. Nutrition calculation currently supports gram-based quantities only.",
                    nameof(request));
            }

            var food = await GetFoodDetailAsync(source, externalId, cancellationToken);

            if (food is null)
            {
                throw new ArgumentException(
                    $"Food '{externalId}' was not found for source '{source}'.",
                    nameof(request));
            }

            if (!food.SupportedUnits.Contains("g", StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Food '{food.Name}' does not expose gram-based nutrient data from {food.Source}.",
                    nameof(request));
            }

            var grams = decimal.Round(ingredient.Quantity, 2, MidpointRounding.AwayFromZero);
            var multiplier = grams / 100m;
            var calories = Scale(food.CaloriesPer100Grams, multiplier);
            var protein = Scale(food.ProteinGramsPer100Grams, multiplier);
            var carbs = Scale(food.CarbsGramsPer100Grams, multiplier);
            var fat = Scale(food.FatGramsPer100Grams, multiplier);
            var fiber = Scale(food.FiberGramsPer100Grams, multiplier);

            results.Add(new IngredientNutritionResult
            {
                Source = food.Source,
                ExternalId = food.ExternalId,
                Name = food.Name,
                Quantity = grams,
                Unit = "g",
                Grams = grams,
                Calories = calories,
                ProteinGrams = protein,
                CarbsGrams = carbs,
                FatGrams = fat,
                FiberGrams = fiber,
            });
        }

        return new NutritionCalculateResult
        {
            Ingredients = results,
            TotalCalories = Sum(results.Select(item => item.Calories)),
            TotalProteinGrams = Sum(results.Select(item => item.ProteinGrams)),
            TotalCarbsGrams = Sum(results.Select(item => item.CarbsGrams)),
            TotalFatGrams = Sum(results.Select(item => item.FatGrams)),
            TotalFiberGrams = Sum(results.Select(item => item.FiberGrams)),
        };
    }

    private INutritionProvider GetProvider(string source)
    {
        if (_providers.TryGetValue(source, out var provider))
        {
            return provider;
        }

        throw new ArgumentException($"Nutrition source '{source}' is not supported.", nameof(source));
    }

    private static string BuildDetailCacheKey(string source, string externalId)
    {
        return $"nutrition:detail:{source}:{externalId}";
    }

    private static string BuildSearchCacheKey(string source, string query, int page, int pageSize)
    {
        return $"nutrition:search:{source}:{query.Trim().ToLowerInvariant()}:{page}:{pageSize}";
    }

    private static string NormalizeRequiredText(string value, string parameterName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return string.Empty;
        }

        return unit.Trim().ToLowerInvariant() switch
        {
            "g" => "g",
            "gram" => "g",
            "grams" => "g",
            _ => unit.Trim().ToLowerInvariant(),
        };
    }

    private static decimal Scale(decimal? valuePer100Grams, decimal multiplier)
    {
        return valuePer100Grams.HasValue
            ? decimal.Round(valuePer100Grams.Value * multiplier, 2, MidpointRounding.AwayFromZero)
            : 0m;
    }

    private static decimal Sum(IEnumerable<decimal> values)
    {
        return decimal.Round(values.Sum(), 2, MidpointRounding.AwayFromZero);
    }

    private async Task<NutritionCatalogItem?> FindCachedItemAsync(
        string source,
        string externalId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.NutritionCatalogItems
            .AsNoTracking()
            .Include(item => item.Portions)
            .FirstOrDefaultAsync(
                item => item.Source == source && item.ExternalId == externalId,
                cancellationToken);
    }

    private async Task<NutritionProviderFoodDetail?> FetchProviderDetailWithMemoryCacheAsync(
        INutritionProvider provider,
        string externalId,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildDetailCacheKey(provider.Source, externalId);

        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(_options.DetailCacheMinutes, 1));
            return await provider.GetFoodDetailAsync(externalId, cancellationToken);
        });
    }

    private async Task<NutritionCatalogItem> UpsertCachedItemAsync(
        NutritionProviderFoodDetail providerDetail,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var now = DateTime.UtcNow;
            var trackedItem = await _dbContext.NutritionCatalogItems
                .Include(item => item.Portions)
                .FirstOrDefaultAsync(
                    item => item.Source == providerDetail.Source && item.ExternalId == providerDetail.ExternalId,
                    cancellationToken);

            var isNew = trackedItem is null;

            if (trackedItem is null)
            {
                trackedItem = new NutritionCatalogItem
                {
                    Source = providerDetail.Source,
                    ExternalId = providerDetail.ExternalId,
                    CreatedAt = now,
                };

                _dbContext.NutritionCatalogItems.Add(trackedItem);
            }

            ApplyCachedItemUpdates(trackedItem, providerDetail, now);
            ReplacePortions(trackedItem, providerDetail);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();

                var persistedItem = await _dbContext.NutritionCatalogItems
                    .AsNoTracking()
                    .Include(item => item.Portions)
                    .FirstAsync(item => item.Source == providerDetail.Source && item.ExternalId == providerDetail.ExternalId, cancellationToken);

                return persistedItem;
            }
            catch (DbUpdateException exception) when (isNew && attempt == 1 && IsUniqueConstraintViolation(exception))
            {
                _logger.LogInformation(
                    exception,
                    "Nutrition cache insert raced with another request. Retrying as update for {Source}/{ExternalId}.",
                    providerDetail.Source,
                    providerDetail.ExternalId);
                _dbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException(
            $"Unable to upsert nutrition cache item '{providerDetail.Source}/{providerDetail.ExternalId}'.");
    }

    private static void ApplyCachedItemUpdates(
        NutritionCatalogItem target,
        NutritionProviderFoodDetail providerDetail,
        DateTime now)
    {
        target.Name = providerDetail.Name;
        target.BrandName = NormalizeOptionalText(providerDetail.BrandName);
        target.FoodType = NormalizeOptionalText(providerDetail.FoodType ?? providerDetail.DataType);
        target.Barcode = NormalizeOptionalText(providerDetail.Barcode);
        target.CaloriesPer100g = providerDetail.CaloriesPer100Grams ?? 0m;
        target.ProteinPer100g = providerDetail.ProteinGramsPer100Grams ?? 0m;
        target.CarbsPer100g = providerDetail.CarbsGramsPer100Grams ?? 0m;
        target.FatPer100g = providerDetail.FatGramsPer100Grams ?? 0m;
        target.FiberPer100g = providerDetail.FiberGramsPer100Grams;
        target.SugarPer100g = providerDetail.SugarGramsPer100Grams;
        target.ProviderPayloadJson = providerDetail.ProviderPayloadJson;
        target.LastFetchedAt = now;
        target.UpdatedAt = now;
    }

    private static void ReplacePortions(
        NutritionCatalogItem target,
        NutritionProviderFoodDetail providerDetail)
    {
        target.Portions.Clear();

        foreach (var portion in BuildDistinctPortions(providerDetail))
        {
            target.Portions.Add(new NutritionCatalogPortion
            {
                UnitName = portion.UnitName,
                Amount = portion.Amount,
                GramWeight = portion.GramWeight,
                ProviderPortionId = portion.ProviderPortionId,
                IsDefault = portion.IsDefault,
            });
        }
    }

    private static IReadOnlyList<NutritionProviderPortion> BuildDistinctPortions(NutritionProviderFoodDetail providerDetail)
    {
        var deduped = new List<NutritionProviderPortion>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var portion in providerDetail.Portions)
        {
            var unitName = NormalizeOptionalText(portion.UnitName);

            if (string.IsNullOrWhiteSpace(unitName) || portion.Amount <= 0 || portion.GramWeight <= 0)
            {
                continue;
            }

            var key = !string.IsNullOrWhiteSpace(portion.ProviderPortionId)
                ? $"provider:{portion.ProviderPortionId.Trim()}"
                : $"{unitName.Trim().ToLowerInvariant()}|{portion.Amount}|{portion.GramWeight}|{portion.IsDefault}";

            if (!seenKeys.Add(key))
            {
                continue;
            }

            deduped.Add(new NutritionProviderPortion
            {
                UnitName = unitName.Trim(),
                Amount = decimal.Round(portion.Amount, 2, MidpointRounding.AwayFromZero),
                GramWeight = decimal.Round(portion.GramWeight, 2, MidpointRounding.AwayFromZero),
                ProviderPortionId = NormalizeOptionalText(portion.ProviderPortionId),
                IsDefault = portion.IsDefault,
            });
        }

        return deduped;
    }

    private static FoodDetailDto MapCachedItemToFoodDetail(NutritionCatalogItem item)
    {
        var cachedFoodCategory = TryReadProviderPayloadField(item.ProviderPayloadJson, "foodCategory");
        var cachedDataType = TryReadProviderPayloadField(item.ProviderPayloadJson, "dataType");

        return new FoodDetailDto
        {
            Source = item.Source,
            ExternalId = item.ExternalId,
            Name = item.Name,
            BrandName = item.BrandName,
            FoodType = item.FoodType,
            FoodCategory = cachedFoodCategory,
            DataType = cachedDataType ?? item.FoodType,
            Barcode = item.Barcode,
            CaloriesPer100Grams = item.CaloriesPer100g,
            ProteinGramsPer100Grams = item.ProteinPer100g,
            CarbsGramsPer100Grams = item.CarbsPer100g,
            FatGramsPer100Grams = item.FatPer100g,
            FiberGramsPer100Grams = item.FiberPer100g,
            SugarGramsPer100Grams = item.SugarPer100g,
            SupportedUnits = ResolveSupportedUnits(item.Portions),
        };
    }

    private static FoodDetailDto MapProviderDetailToFoodDetail(
        NutritionProviderFoodDetail providerDetail,
        NutritionCatalogItem cachedItem)
    {
        return new FoodDetailDto
        {
            Source = providerDetail.Source,
            ExternalId = providerDetail.ExternalId,
            Name = providerDetail.Name,
            BrandName = providerDetail.BrandName,
            FoodType = providerDetail.FoodType,
            FoodCategory = providerDetail.FoodCategory,
            DataType = providerDetail.DataType,
            Barcode = providerDetail.Barcode,
            CaloriesPer100Grams = providerDetail.CaloriesPer100Grams,
            ProteinGramsPer100Grams = providerDetail.ProteinGramsPer100Grams,
            CarbsGramsPer100Grams = providerDetail.CarbsGramsPer100Grams,
            FatGramsPer100Grams = providerDetail.FatGramsPer100Grams,
            FiberGramsPer100Grams = providerDetail.FiberGramsPer100Grams,
            SugarGramsPer100Grams = providerDetail.SugarGramsPer100Grams,
            SupportedUnits = ResolveSupportedUnits(cachedItem.Portions),
        };
    }

    private static string[] ResolveSupportedUnits(IEnumerable<NutritionCatalogPortion> portions)
    {
        return portions.Any(portion => portion.IsDefault && NormalizeUnit(portion.UnitName) == "g")
            ? ["g"]
            : [];
    }

    private static bool IsFresh(NutritionCatalogItem? item)
    {
        return item is not null && item.LastFetchedAt >= DateTime.UtcNow.Subtract(CatalogFreshnessWindow);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            && string.Equals(postgresException.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);
    }

    private static string? TryReadProviderPayloadField(string? providerPayloadJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(providerPayloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(providerPayloadJson);

            if (document.RootElement.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String)
            {
                return NormalizeOptionalText(property.GetString());
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
