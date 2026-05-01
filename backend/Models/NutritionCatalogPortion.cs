namespace backend.Models;

public class NutritionCatalogPortion
{
    public int Id { get; set; }
    public int NutritionCatalogItemId { get; set; }
    public NutritionCatalogItem? NutritionCatalogItem { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal GramWeight { get; set; }
    public string? ProviderPortionId { get; set; }
    public bool IsDefault { get; set; }
}
