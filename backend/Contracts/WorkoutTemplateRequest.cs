using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class WorkoutTemplateRequest : IValidatableObject
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    public List<WorkoutTemplateExerciseEntryRequest> ExerciseEntries { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult(
                "Template name is required.",
                new[] { nameof(Name) });
        }

        if (ExerciseEntries.Count == 0)
        {
            yield return new ValidationResult(
                "At least one exercise entry is required.",
                new[] { nameof(ExerciseEntries) });
        }
    }
}
