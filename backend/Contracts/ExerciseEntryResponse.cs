namespace backend.Contracts;

public class ExerciseEntryResponse
{
    public int Id { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public List<ExerciseSetResponse> Sets { get; set; } = new();
    public bool IsPersonalRecord { get; set; }
    public decimal PersonalRecordWeightKg { get; set; }
}
