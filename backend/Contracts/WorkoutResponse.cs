namespace backend.Contracts;

public class WorkoutResponse
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string WorkoutType { get; set; } = "strength";
    public string Notes { get; set; } = string.Empty;
    public string? CardioActivityType { get; set; }
    public int? CardioDurationMinutes { get; set; }
    public decimal? CardioDistanceKm { get; set; }
    public string? CardioIntensity { get; set; }
    public List<ExerciseEntryResponse> ExerciseEntries { get; set; } = new();
}
