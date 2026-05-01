using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class AddMealItemRequest : IValidatableObject
{
    [Required]
    [StringLength(40)]
    public string SourceProvider { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string ExternalFoodId { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "100000")]
    public decimal Quantity { get; set; }

    [Required]
    [StringLength(20)]
    public string Unit { get; set; } = "g";

    [Range(0, 10000)]
    public int? SortOrder { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(SourceProvider))
        {
            yield return new ValidationResult(
                "Source provider is required.",
                [nameof(SourceProvider)]);
        }

        if (string.IsNullOrWhiteSpace(ExternalFoodId))
        {
            yield return new ValidationResult(
                "External food ID is required.",
                [nameof(ExternalFoodId)]);
        }

        if (string.IsNullOrWhiteSpace(Unit))
        {
            yield return new ValidationResult(
                "Unit is required.",
                [nameof(Unit)]);
        }
    }
}
