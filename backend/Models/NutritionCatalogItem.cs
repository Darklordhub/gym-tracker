namespace backend.Models;

public class NutritionCatalogItem
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? FoodType { get; set; }
    public string? Barcode { get; set; }
    public decimal CaloriesPer100g { get; set; }
    public decimal ProteinPer100g { get; set; }
    public decimal CarbsPer100g { get; set; }
    public decimal FatPer100g { get; set; }
    public decimal? FiberPer100g { get; set; }
    public decimal? SugarPer100g { get; set; }
    public string? ProviderPayloadJson { get; set; }
    public DateTime LastFetchedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<NutritionCatalogPortion> Portions { get; set; } = new();
}
