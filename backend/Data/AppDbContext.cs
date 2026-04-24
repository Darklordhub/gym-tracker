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
    public DbSet<UserCycleSettings> UserCycleSettings => Set<UserCycleSettings>();
    public DbSet<UserCycleEntry> UserCycleEntries => Set<UserCycleEntry>();
    public DbSet<UserCycleSymptomLog> UserCycleSymptomLogs => Set<UserCycleSymptomLog>();
    public DbSet<UserReadinessLog> UserReadinessLogs => Set<UserReadinessLog>();
    public DbSet<UserCalorieLog> UserCalorieLogs => Set<UserCalorieLog>();
    public DbSet<ExerciseCatalogItem> ExerciseCatalogItems => Set<ExerciseCatalogItem>();

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

        modelBuilder.Entity<UserCycleSettings>()
            .Property(settings => settings.CycleRegularity)
            .HasMaxLength(40);

        modelBuilder.Entity<UserCycleSettings>()
            .Property(settings => settings.IsEnabled)
            .HasDefaultValue(false);

        modelBuilder.Entity<UserCycleEntry>()
            .Property(entry => entry.Notes)
            .HasMaxLength(500);

        modelBuilder.Entity<UserCycleSymptomLog>()
            .Property(log => log.Mood)
            .HasMaxLength(50);

        modelBuilder.Entity<UserCycleSymptomLog>()
            .Property(log => log.Notes)
            .HasMaxLength(500);

        modelBuilder.Entity<UserReadinessLog>()
            .Property(log => log.Notes)
            .HasMaxLength(500);

        modelBuilder.Entity<UserCalorieLog>()
            .Property(log => log.Notes)
            .HasMaxLength(500);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.Source)
            .HasMaxLength(40);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.ExternalId)
            .HasMaxLength(120);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.Name)
            .HasMaxLength(160);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.Slug)
            .HasMaxLength(180);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.Description)
            .HasMaxLength(4000);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.Instructions)
            .HasMaxLength(6000);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.PrimaryMuscle)
            .HasMaxLength(80);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.SecondaryMuscles)
            .HasMaxLength(300);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.Equipment)
            .HasMaxLength(80);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.Difficulty)
            .HasMaxLength(40);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.VideoUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.ThumbnailUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.LocalMediaPath)
            .HasMaxLength(500);

        modelBuilder.Entity<ExerciseCatalogItem>()
            .Property(item => item.IsActive)
            .HasDefaultValue(true);

        modelBuilder.Entity<GoalSettings>()
            .Property(goalSettings => goalSettings.CalorieTargetMode)
            .HasMaxLength(20)
            .HasDefaultValue("manual");

        modelBuilder.Entity<WeightEntry>()
            .HasOne(weightEntry => weightEntry.User)
            .WithMany(user => user.WeightEntries)
            .HasForeignKey(weightEntry => weightEntry.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Workout>()
            .Property(workout => workout.WorkoutType)
            .HasMaxLength(20);

        modelBuilder.Entity<Workout>()
            .Property(workout => workout.CardioActivityType)
            .HasMaxLength(40);

        modelBuilder.Entity<Workout>()
            .Property(workout => workout.CardioIntensity)
            .HasMaxLength(20);

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

        modelBuilder.Entity<UserCycleSettings>()
            .HasOne(settings => settings.User)
            .WithMany(user => user.CycleSettings)
            .HasForeignKey(settings => settings.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserCycleEntry>()
            .HasOne(entry => entry.User)
            .WithMany(user => user.CycleEntries)
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserCycleSymptomLog>()
            .HasOne(log => log.User)
            .WithMany(user => user.CycleSymptomLogs)
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserReadinessLog>()
            .HasOne(log => log.User)
            .WithMany(user => user.ReadinessLogs)
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserCalorieLog>()
            .HasOne(log => log.User)
            .WithMany(user => user.CalorieLogs)
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GoalSettings>()
            .HasIndex(goalSettings => goalSettings.UserId)
            .IsUnique();

        modelBuilder.Entity<UserCycleSettings>()
            .HasIndex(settings => settings.UserId)
            .IsUnique();

        modelBuilder.Entity<UserCycleEntry>()
            .HasIndex(entry => new { entry.UserId, entry.PeriodStartDate })
            .IsUnique();

        modelBuilder.Entity<UserCycleSymptomLog>()
            .HasIndex(log => new { log.UserId, log.Date });

        modelBuilder.Entity<UserReadinessLog>()
            .HasIndex(log => new { log.UserId, log.Date })
            .IsUnique();

        modelBuilder.Entity<UserCalorieLog>()
            .HasIndex(log => new { log.UserId, log.Date })
            .IsUnique();

        modelBuilder.Entity<ExerciseCatalogItem>()
            .HasIndex(item => item.Slug)
            .IsUnique();

        modelBuilder.Entity<ExerciseCatalogItem>()
            .HasIndex(item => new { item.Source, item.ExternalId })
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
