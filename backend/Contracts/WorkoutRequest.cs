using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class WorkoutRequest : IValidatableObject
{
    public DateTime Date { get; set; }
    public string? WorkoutType { get; set; }

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    public List<ExerciseEntryRequest> ExerciseEntries { get; set; } = new();
    [StringLength(40)]
    public string? CardioActivityType { get; set; }
    [Range(1, 600)]
    public int? CardioDurationMinutes { get; set; }
    [Range(0.1, 500)]
    public decimal? CardioDistanceKm { get; set; }
    [StringLength(20)]
    public string? CardioIntensity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date == default)
        {
            yield return new ValidationResult(
                "Date is required.",
                new[] { nameof(Date) });
        }

        var workoutType = NormalizeWorkoutType(WorkoutType);

        if (workoutType == "cardio")
        {
            if (string.IsNullOrWhiteSpace(CardioActivityType))
            {
                yield return new ValidationResult(
                    "Cardio type is required.",
                    new[] { nameof(CardioActivityType) });
            }

            if (!CardioDurationMinutes.HasValue)
            {
                yield return new ValidationResult(
                    "Duration is required for cardio sessions.",
                    new[] { nameof(CardioDurationMinutes) });
            }

            if (ExerciseEntries.Count > 0)
            {
                yield return new ValidationResult(
                    "Cardio sessions do not use strength exercise entries.",
                    new[] { nameof(ExerciseEntries) });
            }

            yield break;
        }

        if (workoutType != "strength")
        {
            yield return new ValidationResult(
                "Workout type must be either strength or cardio.",
                new[] { nameof(WorkoutType) });
        }

        if (ExerciseEntries.Count == 0)
        {
            yield return new ValidationResult(
                "At least one exercise entry is required.",
                new[] { nameof(ExerciseEntries) });
        }
    }

    private static string NormalizeWorkoutType(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "strength" : value.Trim().ToLowerInvariant();
    }
}
