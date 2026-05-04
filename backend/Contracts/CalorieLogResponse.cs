namespace backend.Contracts;

public class CalorieLogResponse
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int CaloriesConsumed { get; set; }
    public string SourceMode { get; set; } = string.Empty;
    public decimal? TotalProtein { get; set; }
    public decimal? TotalCarbs { get; set; }
    public decimal? TotalFat { get; set; }
    public DateTime? LastRolledUpAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
