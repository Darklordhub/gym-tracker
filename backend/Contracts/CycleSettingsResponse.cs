namespace backend.Contracts;

public class CycleSettingsResponse
{
    public bool IsEnabled { get; set; }
    public DateOnly? LastPeriodStartDate { get; set; }
    public int? AverageCycleLengthDays { get; set; }
    public int? AveragePeriodLengthDays { get; set; }
    public string CycleRegularity { get; set; } = "regular";
    public bool? UsesHormonalContraception { get; set; }
    public bool? IsNaturallyCycling { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsSetupComplete { get; set; }
    public bool CanPredict { get; set; }
    public string? SetupMessage { get; set; }
}
