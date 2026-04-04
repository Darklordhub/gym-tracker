using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class GoalSettingsRequest : IValidatableObject
{
    [Range(20, 500)]
    public decimal? TargetBodyWeightKg { get; set; }

    [Range(1, 14)]
    public int? WeeklyWorkoutTarget { get; set; }

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

        if (normalizedFitnessPhase is not ("cut" or "maintain" or "bulk"))
        {
            yield return new ValidationResult(
                "Fitness phase must be cut, maintain, or bulk.",
                new[] { nameof(FitnessPhase) });
        }
    }
}
