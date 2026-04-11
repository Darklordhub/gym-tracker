namespace backend.Models;

public class UserCycleSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public bool IsEnabled { get; set; }
    public DateOnly? LastPeriodStartDate { get; set; }
    public int? AverageCycleLengthDays { get; set; }
    public int? AveragePeriodLengthDays { get; set; }
    public string CycleRegularity { get; set; } = "regular";
    public bool? UsesHormonalContraception { get; set; }
    public bool? IsNaturallyCycling { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
