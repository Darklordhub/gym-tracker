namespace backend.Models;

public class WorkoutTemplateExerciseEntry
{
    public int Id { get; set; }

    public int WorkoutTemplateId { get; set; }
    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public string ExerciseName { get; set; } = string.Empty;
    public int Sets { get; set; }
    public int Reps { get; set; }
    public decimal WeightKg { get; set; }
}
