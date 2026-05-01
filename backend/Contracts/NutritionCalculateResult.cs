namespace backend.Contracts;

public class NutritionCalculateResult
{
    public IReadOnlyList<IngredientNutritionResult> Ingredients { get; set; } = [];
    public decimal TotalCalories { get; set; }
    public decimal TotalProteinGrams { get; set; }
    public decimal TotalCarbsGrams { get; set; }
    public decimal TotalFatGrams { get; set; }
    public decimal TotalFiberGrams { get; set; }
}
