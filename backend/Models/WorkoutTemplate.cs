namespace backend.Models;

public class WorkoutTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public List<WorkoutTemplateExerciseEntry> ExerciseEntries { get; set; } = new();
}
