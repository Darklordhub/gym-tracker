namespace backend.Services;

public class NutritionProviderFoodDetail
{
    public string Source { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? FoodType { get; set; }
    public string? FoodCategory { get; set; }
    public string? DataType { get; set; }
    public string? Barcode { get; set; }
    public decimal? CaloriesPer100Grams { get; set; }
    public decimal? ProteinGramsPer100Grams { get; set; }
    public decimal? CarbsGramsPer100Grams { get; set; }
    public decimal? FatGramsPer100Grams { get; set; }
    public decimal? FiberGramsPer100Grams { get; set; }
    public decimal? SugarGramsPer100Grams { get; set; }
    public string[] SupportedUnits { get; set; } = [];
    public IReadOnlyList<NutritionProviderPortion> Portions { get; set; } = [];
    public string? ProviderPayloadJson { get; set; }
}
