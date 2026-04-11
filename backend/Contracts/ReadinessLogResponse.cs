namespace backend.Contracts;

public class ReadinessLogResponse
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int EnergyLevel { get; set; }
    public int SorenessLevel { get; set; }
    public int SleepQuality { get; set; }
    public int MotivationLevel { get; set; }
    public string? Notes { get; set; }
    public string ReadinessLabel { get; set; } = "Balanced";
    public decimal ReadinessScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
