using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Configuration;
using backend.Contracts;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class UsdaNutritionProvider : INutritionProvider
{
    private const string SourceName = "usda";
    private static readonly string[] GramSupportedUnits = ["g"];

    private readonly HttpClient _httpClient;
    private readonly NutritionOptions _options;
    private readonly ILogger<UsdaNutritionProvider> _logger;

    public UsdaNutritionProvider(
        HttpClient httpClient,
        IOptions<NutritionOptions> options,
        ILogger<UsdaNutritionProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string Source => SourceName;

    public async Task<IReadOnlyList<FoodSearchResultDto>> SearchFoodsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var apiKey = GetRequiredApiKey();
        var requestUri = $"foods/search?query={Uri.EscapeDataString(query)}&pageNumber={page}&pageSize={pageSize}&api_key={Uri.EscapeDataString(apiKey)}";
        var response = await SendAsync(
            () => _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken),
            "search USDA foods",
            cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync(stream, UsdaNutritionJsonContext.Default.UsdaFoodSearchResponse, cancellationToken);
        var foods = payload?.Foods ?? [];

        return foods.Select(MapSearchResult).ToList();
    }

    public async Task<FoodDetailDto?> GetFoodDetailAsync(
        string externalId,
        CancellationToken cancellationToken)
    {
        var apiKey = GetRequiredApiKey();
        var requestUri = $"food/{Uri.EscapeDataString(externalId)}?api_key={Uri.EscapeDataString(apiKey)}";

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CreateProviderException("fetch USDA food details", exception);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadErrorBodyAsync(response, cancellationToken);
            _logger.LogWarning(
                "USDA detail request failed. StatusCode={StatusCode} ExternalId={ExternalId} Body={Body}",
                (int)response.StatusCode,
                externalId,
                detail);
            throw new NutritionProviderException("USDA nutrition data is currently unavailable.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync(stream, UsdaNutritionJsonContext.Default.UsdaFoodDetailResponse, cancellationToken);

        return payload is null ? null : MapFoodDetail(payload);
    }

    private string GetRequiredApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.UsdaApiKey))
        {
            return _options.UsdaApiKey.Trim();
        }

        throw new InvalidOperationException(
            "Nutrition:UsdaApiKey is not configured. Set Nutrition__UsdaApiKey for the backend.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> sendRequest,
        string operation,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await sendRequest();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CreateProviderException(operation, exception);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var detail = await ReadErrorBodyAsync(response, cancellationToken);
        _logger.LogWarning(
            "USDA request failed. Operation={Operation} StatusCode={StatusCode} Body={Body}",
            operation,
            (int)response.StatusCode,
            detail);

        throw new NutritionProviderException("USDA nutrition data is currently unavailable.");
    }

    private NutritionProviderException CreateProviderException(string operation, Exception exception)
    {
        _logger.LogError(exception, "Unexpected error while attempting to {Operation}.", operation);
        return new NutritionProviderException("USDA nutrition data is currently unavailable.", exception);
    }

    private static FoodSearchResultDto MapSearchResult(UsdaSearchFoodItem food)
    {
        var nutrients = ResolvePer100GramNutrients(
            food.DataType,
            food.ServingSize,
            food.ServingSizeUnit,
            food.LabelNutrients,
            food.FoodNutrients);

        return new FoodSearchResultDto
        {
            Source = SourceName,
            ExternalId = food.FdcId.ToString(),
            Name = food.Description?.Trim() ?? string.Empty,
            BrandName = NormalizeOptionalText(food.BrandOwner),
            FoodCategory = NormalizeOptionalText(food.FoodCategory),
            DataType = NormalizeOptionalText(food.DataType),
            CaloriesPer100Grams = nutrients.CaloriesPer100Grams,
            ProteinGramsPer100Grams = nutrients.ProteinGramsPer100Grams,
            CarbsGramsPer100Grams = nutrients.CarbsGramsPer100Grams,
            FatGramsPer100Grams = nutrients.FatGramsPer100Grams,
        };
    }

    private static FoodDetailDto MapFoodDetail(UsdaFoodDetailResponse food)
    {
        var nutrients = ResolvePer100GramNutrients(
            food.DataType,
            food.ServingSize,
            food.ServingSizeUnit,
            food.LabelNutrients,
            food.FoodNutrients);
        var supportsGramCalculations = nutrients.HasAnyValue;

        return new FoodDetailDto
        {
            Source = SourceName,
            ExternalId = food.FdcId.ToString(),
            Name = food.Description?.Trim() ?? string.Empty,
            BrandName = NormalizeOptionalText(food.BrandOwner),
            FoodCategory = NormalizeOptionalText(food.FoodCategory),
            DataType = NormalizeOptionalText(food.DataType),
            CaloriesPer100Grams = nutrients.CaloriesPer100Grams,
            ProteinGramsPer100Grams = nutrients.ProteinGramsPer100Grams,
            CarbsGramsPer100Grams = nutrients.CarbsGramsPer100Grams,
            FatGramsPer100Grams = nutrients.FatGramsPer100Grams,
            FiberGramsPer100Grams = nutrients.FiberGramsPer100Grams,
            SupportedUnits = supportsGramCalculations ? GramSupportedUnits : [],
        };
    }

    private static NutrientSnapshot ResolvePer100GramNutrients(
        string? dataType,
        decimal? servingSize,
        string? servingSizeUnit,
        UsdaLabelNutrients? labelNutrients,
        IReadOnlyList<UsdaFoodNutrient>? foodNutrients)
    {
        if (TryResolveFromLabelNutrients(servingSize, servingSizeUnit, labelNutrients, out var snapshot))
        {
            return snapshot;
        }

        return TryResolveFromFoodNutrients(dataType, foodNutrients, out snapshot)
            ? snapshot
            : NutrientSnapshot.Empty;
    }

    private static bool TryResolveFromLabelNutrients(
        decimal? servingSize,
        string? servingSizeUnit,
        UsdaLabelNutrients? labelNutrients,
        out NutrientSnapshot snapshot)
    {
        snapshot = NutrientSnapshot.Empty;

        if (labelNutrients is null
            || !servingSize.HasValue
            || servingSize.Value <= 0
            || !IsGramUnit(servingSizeUnit))
        {
            return false;
        }

        var multiplier = 100m / servingSize.Value;
        snapshot = new NutrientSnapshot(
            ScaleValue(labelNutrients.Calories?.Value, multiplier),
            ScaleValue(labelNutrients.Protein?.Value, multiplier),
            ScaleValue(labelNutrients.Carbohydrates?.Value, multiplier),
            ScaleValue(labelNutrients.Fat?.Value, multiplier),
            ScaleValue(labelNutrients.Fiber?.Value, multiplier));

        return snapshot.HasAnyValue;
    }

    private static bool TryResolveFromFoodNutrients(
        string? dataType,
        IReadOnlyList<UsdaFoodNutrient>? foodNutrients,
        out NutrientSnapshot snapshot)
    {
        snapshot = NutrientSnapshot.Empty;

        if (foodNutrients is null || foodNutrients.Count == 0)
        {
            return false;
        }

        if (string.Equals(dataType, "Branded", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        snapshot = new NutrientSnapshot(
            FindNutrientAmount(foodNutrients, nutrientNumber: "1008", nutrientName: "Energy", requiredUnit: "KCAL"),
            FindNutrientAmount(foodNutrients, nutrientNumber: "1003", nutrientName: "Protein", requiredUnit: "G"),
            FindNutrientAmount(foodNutrients, nutrientNumber: "1005", nutrientName: "Carbohydrate, by difference", requiredUnit: "G"),
            FindNutrientAmount(foodNutrients, nutrientNumber: "1004", nutrientName: "Total lipid (fat)", requiredUnit: "G"),
            FindNutrientAmount(foodNutrients, nutrientNumber: "1079", nutrientName: "Fiber, total dietary", requiredUnit: "G"));

        return snapshot.HasAnyValue;
    }

    private static decimal? FindNutrientAmount(
        IReadOnlyList<UsdaFoodNutrient> nutrients,
        string nutrientNumber,
        string nutrientName,
        string requiredUnit)
    {
        foreach (var nutrient in nutrients)
        {
            var number = nutrient.Nutrient?.Number ?? nutrient.NutrientNumber;
            var name = nutrient.Nutrient?.Name ?? nutrient.NutrientName;
            var unit = nutrient.Nutrient?.UnitName ?? nutrient.UnitName;

            var numberMatches = string.Equals(number, nutrientNumber, StringComparison.OrdinalIgnoreCase);
            var nameMatches = string.Equals(name, nutrientName, StringComparison.OrdinalIgnoreCase);
            var unitMatches = string.Equals(unit, requiredUnit, StringComparison.OrdinalIgnoreCase);

            if ((numberMatches || nameMatches) && unitMatches && nutrient.Amount.HasValue)
            {
                return Round(nutrient.Amount.Value);
            }
        }

        return null;
    }

    private static bool IsGramUnit(string? unit)
    {
        return string.Equals(unit?.Trim(), "g", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal? ScaleValue(decimal? value, decimal multiplier)
    {
        return value.HasValue ? Round(value.Value * multiplier) : null;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static async Task<string?> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(body) ? null : body.Trim();
        }
        catch
        {
            return null;
        }
    }

    private sealed record NutrientSnapshot(
        decimal? CaloriesPer100Grams,
        decimal? ProteinGramsPer100Grams,
        decimal? CarbsGramsPer100Grams,
        decimal? FatGramsPer100Grams,
        decimal? FiberGramsPer100Grams)
    {
        public static readonly NutrientSnapshot Empty = new(null, null, null, null, null);

        public bool HasAnyValue =>
            CaloriesPer100Grams.HasValue
            || ProteinGramsPer100Grams.HasValue
            || CarbsGramsPer100Grams.HasValue
            || FatGramsPer100Grams.HasValue
            || FiberGramsPer100Grams.HasValue;
    }
}

[JsonSerializable(typeof(UsdaFoodSearchResponse))]
[JsonSerializable(typeof(UsdaFoodDetailResponse))]
internal partial class UsdaNutritionJsonContext : JsonSerializerContext
{
}

internal sealed class UsdaFoodSearchResponse
{
    [JsonPropertyName("foods")]
    public List<UsdaSearchFoodItem> Foods { get; set; } = [];
}

internal sealed class UsdaSearchFoodItem
{
    [JsonPropertyName("fdcId")]
    public int FdcId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("brandOwner")]
    public string? BrandOwner { get; set; }

    [JsonPropertyName("foodCategory")]
    public string? FoodCategory { get; set; }

    [JsonPropertyName("dataType")]
    public string? DataType { get; set; }

    [JsonPropertyName("servingSize")]
    public decimal? ServingSize { get; set; }

    [JsonPropertyName("servingSizeUnit")]
    public string? ServingSizeUnit { get; set; }

    [JsonPropertyName("labelNutrients")]
    public UsdaLabelNutrients? LabelNutrients { get; set; }

    [JsonPropertyName("foodNutrients")]
    public List<UsdaFoodNutrient> FoodNutrients { get; set; } = [];
}

internal sealed class UsdaFoodDetailResponse
{
    [JsonPropertyName("fdcId")]
    public int FdcId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("brandOwner")]
    public string? BrandOwner { get; set; }

    [JsonPropertyName("foodCategory")]
    public string? FoodCategory { get; set; }

    [JsonPropertyName("dataType")]
    public string? DataType { get; set; }

    [JsonPropertyName("servingSize")]
    public decimal? ServingSize { get; set; }

    [JsonPropertyName("servingSizeUnit")]
    public string? ServingSizeUnit { get; set; }

    [JsonPropertyName("labelNutrients")]
    public UsdaLabelNutrients? LabelNutrients { get; set; }

    [JsonPropertyName("foodNutrients")]
    public List<UsdaFoodNutrient> FoodNutrients { get; set; } = [];
}

internal sealed class UsdaLabelNutrients
{
    [JsonPropertyName("calories")]
    public UsdaNutrientValue? Calories { get; set; }

    [JsonPropertyName("protein")]
    public UsdaNutrientValue? Protein { get; set; }

    [JsonPropertyName("carbohydrates")]
    public UsdaNutrientValue? Carbohydrates { get; set; }

    [JsonPropertyName("fat")]
    public UsdaNutrientValue? Fat { get; set; }

    [JsonPropertyName("fiber")]
    public UsdaNutrientValue? Fiber { get; set; }
}

internal sealed class UsdaNutrientValue
{
    [JsonPropertyName("value")]
    public decimal? Value { get; set; }
}

internal sealed class UsdaFoodNutrient
{
    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("nutrient")]
    public UsdaNutrientMetadata? Nutrient { get; set; }

    [JsonPropertyName("nutrientName")]
    public string? NutrientName { get; set; }

    [JsonPropertyName("nutrientNumber")]
    public string? NutrientNumber { get; set; }

    [JsonPropertyName("unitName")]
    public string? UnitName { get; set; }
}

internal sealed class UsdaNutrientMetadata
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("unitName")]
    public string? UnitName { get; set; }
}
