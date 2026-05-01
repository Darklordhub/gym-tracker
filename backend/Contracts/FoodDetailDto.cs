namespace backend.Contracts;

public class FoodDetailDto
{
    public string Source { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? FoodCategory { get; set; }
    public string? DataType { get; set; }
    public decimal? CaloriesPer100Grams { get; set; }
    public decimal? ProteinGramsPer100Grams { get; set; }
    public decimal? CarbsGramsPer100Grams { get; set; }
    public decimal? FatGramsPer100Grams { get; set; }
    public decimal? FiberGramsPer100Grams { get; set; }
    public string[] SupportedUnits { get; set; } = [];
}
