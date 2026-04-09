namespace backend.Models;

public class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public List<WeightEntry> WeightEntries { get; set; } = new();
    public List<Workout> Workouts { get; set; } = new();
    public List<WorkoutTemplate> WorkoutTemplates { get; set; } = new();
    public List<ActiveWorkoutSession> ActiveWorkoutSessions { get; set; } = new();
    public List<GoalSettings> GoalSettings { get; set; } = new();
}
