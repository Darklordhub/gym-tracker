namespace backend.Contracts;

public class WorkoutTemplateResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<WorkoutTemplateExerciseEntryResponse> ExerciseEntries { get; set; } = new();
}
