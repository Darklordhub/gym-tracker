using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class WeightEntryRequest : IValidatableObject
{
    public DateTime Date { get; set; }

    [Range(20, 500)]
    public decimal WeightKg { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date == default)
        {
            yield return new ValidationResult(
                "Date is required.",
                new[] { nameof(Date) });
        }
    }
}
