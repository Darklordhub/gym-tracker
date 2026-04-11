namespace backend.Contracts;

public class CycleEntryResponse
{
    public int Id { get; set; }
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
