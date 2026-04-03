using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class ExerciseEntryRequest : IValidatableObject
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string ExerciseName { get; set; } = string.Empty;

    [Range(1, 20)]
    public int Sets { get; set; }

    [Range(1, 100)]
    public int Reps { get; set; }

    [Range(0, 500)]
    public decimal WeightKg { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ExerciseName))
        {
            yield return new ValidationResult(
                "Exercise name is required.",
                new[] { nameof(ExerciseName) });
        }
    }
}
