namespace backend.Models;

public class WorkoutTemplateExerciseSet
{
    public int Id { get; set; }

    public int WorkoutTemplateExerciseEntryId { get; set; }
    public WorkoutTemplateExerciseEntry? WorkoutTemplateExerciseEntry { get; set; }

    public int Order { get; set; }
    public int Reps { get; set; }
    public decimal WeightKg { get; set; }
}
