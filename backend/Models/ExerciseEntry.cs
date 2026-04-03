namespace backend.Models;

public class ExerciseEntry
{
    public int Id { get; set; }

    public int WorkoutId { get; set; }
    public Workout? Workout { get; set; }

    public string ExerciseName { get; set; } = string.Empty;
    public int Sets { get; set; }
    public int Reps { get; set; }
    public decimal WeightKg { get; set; }
}