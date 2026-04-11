namespace backend.Contracts;

public class CycleSymptomLogResponse
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int FatigueLevel { get; set; }
    public int CrampsLevel { get; set; }
    public string Mood { get; set; } = "steady";
    public int BloatingLevel { get; set; }
    public int SleepQuality { get; set; }
    public int RecoveryFeeling { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
