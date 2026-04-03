namespace backend.Models;

public class ActiveWorkoutSession
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public string Notes { get; set; } = string.Empty;

    public List<ActiveWorkoutSessionExerciseEntry> ExerciseEntries { get; set; } = new();
}
