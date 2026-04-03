namespace backend.Contracts;

public class ActiveWorkoutSessionResponse
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<ActiveWorkoutSessionExerciseEntryResponse> ExerciseEntries { get; set; } = new();
}
