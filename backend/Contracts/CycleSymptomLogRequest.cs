using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class CycleSymptomLogRequest : IValidatableObject
{
    [Required]
    public DateOnly Date { get; set; }

    [Range(1, 5)]
    public int FatigueLevel { get; set; }

    [Range(1, 5)]
    public int CrampsLevel { get; set; }

    [Required]
    [StringLength(50)]
    public string Mood { get; set; } = "steady";

    [Range(1, 5)]
    public int BloatingLevel { get; set; }

    [Range(1, 5)]
    public int SleepQuality { get; set; }

    [Range(1, 5)]
    public int RecoveryFeeling { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            yield return new ValidationResult("Symptom log date cannot be in the future.", [nameof(Date)]);
        }
    }
}
