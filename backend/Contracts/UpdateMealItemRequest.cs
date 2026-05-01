using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class UpdateMealItemRequest : IValidatableObject
{
    [Range(typeof(decimal), "0.01", "100000")]
    public decimal Quantity { get; set; }

    [Required]
    [StringLength(20)]
    public string Unit { get; set; } = "g";

    [Range(0, 10000)]
    public int? SortOrder { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Unit))
        {
            yield return new ValidationResult(
                "Unit is required.",
                [nameof(Unit)]);
        }
    }
}
