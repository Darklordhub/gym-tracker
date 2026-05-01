namespace backend.Contracts;

public class UserMealItemDto
{
    public int Id { get; set; }
    public int? NutritionCatalogItemId { get; set; }
    public string FoodNameSnapshot { get; set; } = string.Empty;
    public string? BrandNameSnapshot { get; set; }
    public string SourceProvider { get; set; } = string.Empty;
    public string ExternalFoodId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal ConsumedGrams { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbs { get; set; }
    public decimal Fat { get; set; }
    public decimal? Fiber { get; set; }
    public decimal? Sugar { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
