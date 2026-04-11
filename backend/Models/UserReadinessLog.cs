namespace backend.Models;

public class UserReadinessLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public DateOnly Date { get; set; }
    public int EnergyLevel { get; set; }
    public int SorenessLevel { get; set; }
    public int SleepQuality { get; set; }
    public int MotivationLevel { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
