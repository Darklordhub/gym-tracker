using backend.Authorization;

namespace backend.Models;

public class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? HeightCm { get; set; }
    public string? Gender { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<WeightEntry> WeightEntries { get; set; } = new();
    public List<Workout> Workouts { get; set; } = new();
    public List<WorkoutTemplate> WorkoutTemplates { get; set; } = new();
    public List<ActiveWorkoutSession> ActiveWorkoutSessions { get; set; } = new();
    public List<GoalSettings> GoalSettings { get; set; } = new();
    public List<UserCycleSettings> CycleSettings { get; set; } = new();
    public List<UserCycleEntry> CycleEntries { get; set; } = new();
    public List<UserCycleSymptomLog> CycleSymptomLogs { get; set; } = new();
    public List<UserReadinessLog> ReadinessLogs { get; set; } = new();
    public string Role { get; set; } = AppRoles.User;
    public bool IsActive { get; set; } = true;
}
