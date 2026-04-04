using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class ExerciseSetRequest
{
    [Range(1, 100)]
    public int Reps { get; set; }

    [Range(0, 500)]
    public decimal WeightKg { get; set; }
}
