namespace backend.Models;

public class WeightEntry
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime Date { get; set; }
    public decimal WeightKg { get; set; }
}
