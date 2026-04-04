namespace backend.Models;

public class ExerciseSet
{
    public int Id { get; set; }

    public int ExerciseEntryId { get; set; }
    public ExerciseEntry? ExerciseEntry { get; set; }

    public int Order { get; set; }
    public int Reps { get; set; }
    public decimal WeightKg { get; set; }
}
