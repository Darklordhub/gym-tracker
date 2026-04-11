namespace backend.Models;

public class Workout
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime Date { get; set; }
    public string WorkoutType { get; set; } = "strength";
    public string Notes { get; set; } = string.Empty;
    public string? CardioActivityType { get; set; }
    public int? CardioDurationMinutes { get; set; }
    public decimal? CardioDistanceKm { get; set; }
    public string? CardioIntensity { get; set; }

    public List<ExerciseEntry> ExerciseEntries { get; set; } = new();
}
