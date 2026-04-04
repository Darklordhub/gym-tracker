namespace backend.Models;

public class WorkoutTemplateExerciseEntry
{
    public int Id { get; set; }

    public int WorkoutTemplateId { get; set; }
    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public string ExerciseName { get; set; } = string.Empty;

    public List<WorkoutTemplateExerciseSet> Sets { get; set; } = new();
}
