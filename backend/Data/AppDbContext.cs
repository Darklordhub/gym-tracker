using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<WeightEntry> WeightEntries => Set<WeightEntry>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<ExerciseEntry> ExerciseEntries => Set<ExerciseEntry>();
    public DbSet<ExerciseSet> ExerciseSets => Set<ExerciseSet>();
    public DbSet<WorkoutTemplate> WorkoutTemplates => Set<WorkoutTemplate>();
    public DbSet<WorkoutTemplateExerciseEntry> WorkoutTemplateExerciseEntries => Set<WorkoutTemplateExerciseEntry>();
    public DbSet<WorkoutTemplateExerciseSet> WorkoutTemplateExerciseSets => Set<WorkoutTemplateExerciseSet>();
    public DbSet<ActiveWorkoutSession> ActiveWorkoutSessions => Set<ActiveWorkoutSession>();
    public DbSet<ActiveWorkoutSessionExerciseEntry> ActiveWorkoutSessionExerciseEntries => Set<ActiveWorkoutSessionExerciseEntry>();
    public DbSet<ActiveWorkoutSessionExerciseSet> ActiveWorkoutSessionExerciseSets => Set<ActiveWorkoutSessionExerciseSet>();
}
