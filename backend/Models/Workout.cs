namespace backend.Models;

public class Workout
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Notes { get; set; } = string.Empty;

    public List<ExerciseEntry> ExerciseEntries { get; set; } = new();
}