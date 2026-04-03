using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class WorkoutRequest : IValidatableObject
{
    public DateTime Date { get; set; }

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    public List<ExerciseEntryRequest> ExerciseEntries { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date == default)
        {
            yield return new ValidationResult(
                "Date is required.",
                new[] { nameof(Date) });
        }

        if (ExerciseEntries.Count == 0)
        {
            yield return new ValidationResult(
                "At least one exercise entry is required.",
                new[] { nameof(ExerciseEntries) });
        }
    }
}
