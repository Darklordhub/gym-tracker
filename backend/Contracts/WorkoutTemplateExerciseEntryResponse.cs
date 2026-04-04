namespace backend.Contracts;

public class WorkoutTemplateExerciseEntryResponse
{
    public int Id { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public List<ExerciseSetResponse> Sets { get; set; } = new();
}
