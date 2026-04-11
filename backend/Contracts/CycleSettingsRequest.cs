using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class CycleSettingsRequest : IValidatableObject
{
    public bool IsEnabled { get; set; }
    public DateOnly? LastPeriodStartDate { get; set; }

    [Range(20, 45)]
    public int? AverageCycleLengthDays { get; set; }

    [Range(2, 10)]
    public int? AveragePeriodLengthDays { get; set; }

    [StringLength(40)]
    public string? CycleRegularity { get; set; }

    public bool? UsesHormonalContraception { get; set; }
    public bool? IsNaturallyCycling { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(CycleRegularity))
        {
            yield return new ValidationResult(
                "Cycle regularity is required.",
                [nameof(CycleRegularity)]);
        }

        if (AverageCycleLengthDays.HasValue && AveragePeriodLengthDays.HasValue
            && AveragePeriodLengthDays.Value >= AverageCycleLengthDays.Value)
        {
            yield return new ValidationResult(
                "Average period length must be shorter than average cycle length.",
                [nameof(AveragePeriodLengthDays), nameof(AverageCycleLengthDays)]);
        }
    }
}
