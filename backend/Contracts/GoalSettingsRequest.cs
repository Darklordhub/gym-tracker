using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class GoalSettingsRequest : IValidatableObject
{
    [Range(20, 500)]
    public decimal? TargetBodyWeightKg { get; set; }

    [Range(1, 14)]
    public int? WeeklyWorkoutTarget { get; set; }

    [Range(800, 6000)]
    public int? DailyCalorieTarget { get; set; }

    [Required]
    public string CalorieTargetMode { get; set; } = "manual";

    [Required]
    public string FitnessPhase { get; set; } = "maintain";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FitnessPhase))
        {
            yield return new ValidationResult(
                "Fitness phase is required.",
                new[] { nameof(FitnessPhase) });

            yield break;
        }

        var normalizedFitnessPhase = FitnessPhase.Trim().ToLowerInvariant();
        string? normalizedCalorieTargetMode = null;

        if (normalizedFitnessPhase is not ("cut" or "maintain" or "bulk"))
        {
            yield return new ValidationResult(
                "Fitness phase must be cut, maintain, or bulk.",
                new[] { nameof(FitnessPhase) });
        }

        if (string.IsNullOrWhiteSpace(CalorieTargetMode))
        {
            yield return new ValidationResult(
                "Calorie target mode is required.",
                new[] { nameof(CalorieTargetMode) });

            yield break;
        }

        normalizedCalorieTargetMode = CalorieTargetMode.Trim().ToLowerInvariant();

        if (normalizedCalorieTargetMode is not ("manual" or "goal-based"))
        {
            yield return new ValidationResult(
                "Calorie target mode must be manual or goal-based.",
                new[] { nameof(CalorieTargetMode) });
        }

        if (normalizedCalorieTargetMode == "manual" && DailyCalorieTarget.HasValue && DailyCalorieTarget.Value < 800)
        {
            yield return new ValidationResult(
                "Manual calorie target must be at least 800.",
                new[] { nameof(DailyCalorieTarget) });
        }
    }
}
