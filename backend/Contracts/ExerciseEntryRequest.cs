using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class ExerciseEntryRequest : IValidatableObject
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string ExerciseName { get; set; } = string.Empty;

    public List<ExerciseSetRequest> Sets { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ExerciseName))
        {
            yield return new ValidationResult(
                "Exercise name is required.",
                new[] { nameof(ExerciseName) });
        }

        if (Sets.Count == 0)
        {
            yield return new ValidationResult(
                "Add at least one set.",
                new[] { nameof(Sets) });
        }
    }
}
