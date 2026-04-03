using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class ActiveWorkoutSessionRequest : IValidatableObject
{
    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    public List<ExerciseEntryRequest> ExerciseEntries { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExerciseEntries.Any(exercise => string.IsNullOrWhiteSpace(exercise.ExerciseName)))
        {
            yield return new ValidationResult(
                "Exercise entries cannot contain blank exercise names.",
                new[] { nameof(ExerciseEntries) });
        }
    }
}
