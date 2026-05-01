namespace backend.Services;

public class NutritionProviderPortion
{
    public string UnitName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal GramWeight { get; set; }
    public string? ProviderPortionId { get; set; }
    public bool IsDefault { get; set; }
}
