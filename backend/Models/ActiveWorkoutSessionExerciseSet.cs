namespace backend.Models;

public class ActiveWorkoutSessionExerciseSet
{
    public int Id { get; set; }

    public int ActiveWorkoutSessionExerciseEntryId { get; set; }
    public ActiveWorkoutSessionExerciseEntry? ActiveWorkoutSessionExerciseEntry { get; set; }

    public int Order { get; set; }
    public int Reps { get; set; }
    public decimal WeightKg { get; set; }
}
