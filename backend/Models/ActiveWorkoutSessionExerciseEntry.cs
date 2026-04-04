namespace backend.Models;

public class ActiveWorkoutSessionExerciseEntry
{
    public int Id { get; set; }

    public int ActiveWorkoutSessionId { get; set; }
    public ActiveWorkoutSession? ActiveWorkoutSession { get; set; }

    public string ExerciseName { get; set; } = string.Empty;

    public List<ActiveWorkoutSessionExerciseSet> Sets { get; set; } = new();
}
