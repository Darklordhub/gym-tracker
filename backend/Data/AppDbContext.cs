using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
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
    public DbSet<GoalSettings> GoalSettings => Set<GoalSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<AppUser>()
            .Property(user => user.Email)
            .HasMaxLength(320);

        modelBuilder.Entity<AppUser>()
            .Property(user => user.FullName)
            .HasMaxLength(120);

        modelBuilder.Entity<AppUser>()
            .Property(user => user.DisplayName)
            .HasMaxLength(80);

        modelBuilder.Entity<AppUser>()
            .Property(user => user.Gender)
            .HasMaxLength(50);

        modelBuilder.Entity<AppUser>()
            .Property(user => user.Role)
            .HasMaxLength(20);

        modelBuilder.Entity<AppUser>()
            .Property(user => user.IsActive)
            .HasDefaultValue(true);

        modelBuilder.Entity<WeightEntry>()
            .HasOne(weightEntry => weightEntry.User)
            .WithMany(user => user.WeightEntries)
            .HasForeignKey(weightEntry => weightEntry.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Workout>()
            .HasOne(workout => workout.User)
            .WithMany(user => user.Workouts)
            .HasForeignKey(workout => workout.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkoutTemplate>()
            .HasOne(template => template.User)
            .WithMany(user => user.WorkoutTemplates)
            .HasForeignKey(template => template.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActiveWorkoutSession>()
            .HasOne(session => session.User)
            .WithMany(user => user.ActiveWorkoutSessions)
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GoalSettings>()
            .HasOne(goalSettings => goalSettings.User)
            .WithMany(user => user.GoalSettings)
            .HasForeignKey(goalSettings => goalSettings.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GoalSettings>()
            .HasIndex(goalSettings => goalSettings.UserId)
            .IsUnique();

        modelBuilder.Entity<WeightEntry>()
            .HasIndex(weightEntry => new { weightEntry.UserId, weightEntry.Date });

        modelBuilder.Entity<Workout>()
            .HasIndex(workout => new { workout.UserId, workout.Date });

        modelBuilder.Entity<WorkoutTemplate>()
            .HasIndex(template => new { template.UserId, template.Name });

        modelBuilder.Entity<ActiveWorkoutSession>()
            .HasIndex(session => session.UserId);
    }
}
