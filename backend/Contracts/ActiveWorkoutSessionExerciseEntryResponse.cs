namespace backend.Contracts;

public class ActiveWorkoutSessionExerciseEntryResponse
{
    public int Id { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public int Sets { get; set; }
    public int Reps { get; set; }
    public decimal WeightKg { get; set; }
}
