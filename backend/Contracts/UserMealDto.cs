namespace backend.Contracts;

public class UserMealDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbs { get; set; }
    public decimal TotalFat { get; set; }
    public decimal? TotalFiber { get; set; }
    public decimal? TotalSugar { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<UserMealItemDto> Items { get; set; } = [];
}
