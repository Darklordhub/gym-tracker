using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class ReadinessLogRequest : IValidatableObject
{
    [Required]
    public DateOnly Date { get; set; }

    [Range(1, 3)]
    public int EnergyLevel { get; set; }

    [Range(1, 3)]
    public int SorenessLevel { get; set; }

    [Range(1, 3)]
    public int SleepQuality { get; set; }

    [Range(1, 3)]
    public int MotivationLevel { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            yield return new ValidationResult("Readiness date cannot be in the future.", [nameof(Date)]);
        }
    }
}
