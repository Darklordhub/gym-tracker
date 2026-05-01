using backend.Configuration;
using backend.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class NutritionService : INutritionService
{
    private const string PrimarySource = "usda";
    private const int MaxPageSize = 50;

    private readonly IMemoryCache _memoryCache;
    private readonly IReadOnlyDictionary<string, INutritionProvider> _providers;
    private readonly NutritionOptions _options;

    public NutritionService(
        IEnumerable<INutritionProvider> providers,
        IMemoryCache memoryCache,
        IOptions<NutritionOptions> options)
    {
        _memoryCache = memoryCache;
        _options = options.Value;
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
        var provider = GetProvider(normalizedSource);
        var cacheKey = BuildDetailCacheKey(provider.Source, normalizedExternalId);

        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(_options.DetailCacheMinutes, 1));
            return await provider.GetFoodDetailAsync(normalizedExternalId, cancellationToken);
        });
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
                    $"Unit '{ingredient.Unit}' is not supported. Phase 1 only supports gram-based quantities.",
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
}
