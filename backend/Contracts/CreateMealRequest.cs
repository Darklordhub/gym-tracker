using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class CreateMealRequest : IValidatableObject
{
    [Required]
    [StringLength(40)]
    public string MealType { get; set; } = string.Empty;

    [StringLength(160)]
    public string? Title { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(MealType))
        {
            yield return new ValidationResult(
                "Meal type is required.",
                [nameof(MealType)]);
        }
    }
}
