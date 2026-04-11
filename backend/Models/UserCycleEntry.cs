namespace backend.Models;

public class UserCycleEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
