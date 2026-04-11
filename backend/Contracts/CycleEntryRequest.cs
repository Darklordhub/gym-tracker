using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class CycleEntryRequest : IValidatableObject
{
    [Required]
    public DateOnly PeriodStartDate { get; set; }

    [Required]
    public DateOnly PeriodEndDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PeriodEndDate < PeriodStartDate)
        {
            yield return new ValidationResult(
                "Period end date must be on or after the start date.",
                [nameof(PeriodEndDate), nameof(PeriodStartDate)]);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (PeriodStartDate > today)
        {
            yield return new ValidationResult(
                "Period start date cannot be in the future.",
                [nameof(PeriodStartDate)]);
        }

        if (PeriodEndDate > today)
        {
            yield return new ValidationResult(
                "Period end date cannot be in the future.",
                [nameof(PeriodEndDate)]);
        }
    }
}
