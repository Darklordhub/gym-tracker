using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class NutritionCalculateIngredientRequest : IValidatableObject
{
    [Required]
    public string Source { get; set; } = string.Empty;

    [Required]
    public string ExternalId { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "100000")]
    public decimal Quantity { get; set; }

    [Required]
    [StringLength(20)]
    public string Unit { get; set; } = "g";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            yield return new ValidationResult(
                "Source is required.",
                [nameof(Source)]);
        }

        if (string.IsNullOrWhiteSpace(ExternalId))
        {
            yield return new ValidationResult(
                "External ID is required.",
                [nameof(ExternalId)]);
        }

        if (string.IsNullOrWhiteSpace(Unit))
        {
            yield return new ValidationResult(
                "Unit is required.",
                [nameof(Unit)]);
        }
    }
}
