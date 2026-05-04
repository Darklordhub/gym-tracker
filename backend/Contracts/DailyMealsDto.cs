namespace backend.Contracts;

public class DailyMealsDto
{
    public DateOnly Date { get; set; }
    public IReadOnlyList<UserMealDto> Meals { get; set; } = [];
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbs { get; set; }
    public decimal TotalFat { get; set; }
    public decimal? TotalFiber { get; set; }
    public decimal? TotalSugar { get; set; }
    public bool CaloriesLinkedToDailyLog { get; set; }
    public string? SourceMode { get; set; }
    public string? ConflictMessage { get; set; }
    public int? DailyLogCalories { get; set; }
}
