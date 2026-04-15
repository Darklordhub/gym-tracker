using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class CalorieLogRequest : IValidatableObject
{
    [Required]
    public DateOnly Date { get; set; }

    [Range(0, 15000)]
    public int CaloriesConsumed { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            yield return new ValidationResult("Calorie log date cannot be in the future.", [nameof(Date)]);
        }
    }
}
