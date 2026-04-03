namespace backend.Contracts;

public class WorkoutResponse
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<ExerciseEntryResponse> ExerciseEntries { get; set; } = new();
}
