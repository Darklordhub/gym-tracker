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

    [Required]
    [StringLength(40)]
    public string CycleRegularity { get; set; } = "regular";

    public bool? UsesHormonalContraception { get; set; }
    public bool? IsNaturallyCycling { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsEnabled && LastPeriodStartDate is null)
        {
            yield return new ValidationResult(
                "Last period start date is required when cycle-aware guidance is enabled.",
                [nameof(LastPeriodStartDate)]);
        }

        if (IsEnabled && AverageCycleLengthDays is null)
        {
            yield return new ValidationResult(
                "Average cycle length is required when cycle-aware guidance is enabled.",
                [nameof(AverageCycleLengthDays)]);
        }

        if (IsEnabled && AveragePeriodLengthDays is null)
        {
            yield return new ValidationResult(
                "Average period length is required when cycle-aware guidance is enabled.",
                [nameof(AveragePeriodLengthDays)]);
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
