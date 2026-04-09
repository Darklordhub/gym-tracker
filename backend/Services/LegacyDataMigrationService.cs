using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class LegacyDataMigrationService
{
    private readonly AppDbContext _dbContext;
    private readonly PasswordHasher<AppUser> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LegacyDataMigrationService> _logger;

    public LegacyDataMigrationService(
        AppDbContext dbContext,
        PasswordHasher<AppUser> passwordHasher,
        IConfiguration configuration,
        ILogger<LegacyDataMigrationService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task MigrateAsync()
    {
        if (!await HasLegacyDataAsync())
        {
            return;
        }

        var configuredEmail = NormalizeEmail(_configuration["LegacyUser:Email"]);
        var configuredPassword = _configuration["LegacyUser:Password"]?.Trim();

        if (string.IsNullOrWhiteSpace(configuredEmail) || string.IsNullOrWhiteSpace(configuredPassword))
        {
            _logger.LogWarning(
                "Legacy data exists without user ownership. Set LegacyUser__Email and LegacyUser__Password once to claim existing records.");
            return;
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(existingUser => existingUser.Email == configuredEmail);

        if (user is null)
        {
            user = new AppUser
            {
                Email = configuredEmail,
                CreatedAt = DateTime.UtcNow,
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, configuredPassword);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }

        await _dbContext.WeightEntries
            .Where(entry => entry.UserId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.UserId, user.Id));

        await _dbContext.Workouts
            .Where(workout => workout.UserId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(workout => workout.UserId, user.Id));

        await _dbContext.WorkoutTemplates
            .Where(template => template.UserId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(template => template.UserId, user.Id));

        await _dbContext.ActiveWorkoutSessions
            .Where(session => session.UserId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.UserId, user.Id));

        await AssignLegacyGoalsAsync(user.Id);

        _logger.LogInformation("Assigned legacy single-user data to account {Email}.", configuredEmail);
    }

    private async Task<bool> HasLegacyDataAsync()
    {
        return await _dbContext.WeightEntries.AnyAsync(entry => entry.UserId == null)
            || await _dbContext.Workouts.AnyAsync(workout => workout.UserId == null)
            || await _dbContext.WorkoutTemplates.AnyAsync(template => template.UserId == null)
            || await _dbContext.ActiveWorkoutSessions.AnyAsync(session => session.UserId == null)
            || await _dbContext.GoalSettings.AnyAsync(goalSettings => goalSettings.UserId == null);
    }

    private async Task AssignLegacyGoalsAsync(int userId)
    {
        var orphanGoals = await _dbContext.GoalSettings
            .Where(goalSettings => goalSettings.UserId == null)
            .OrderByDescending(goalSettings => goalSettings.Id)
            .ToListAsync();

        if (orphanGoals.Count == 0)
        {
            return;
        }

        var existingUserGoals = await _dbContext.GoalSettings
            .FirstOrDefaultAsync(goalSettings => goalSettings.UserId == userId);

        if (existingUserGoals is null)
        {
            orphanGoals[0].UserId = userId;
            _dbContext.GoalSettings.RemoveRange(orphanGoals.Skip(1));
        }
        else
        {
            existingUserGoals.TargetBodyWeightKg = orphanGoals[0].TargetBodyWeightKg;
            existingUserGoals.WeeklyWorkoutTarget = orphanGoals[0].WeeklyWorkoutTarget;
            existingUserGoals.FitnessPhase = orphanGoals[0].FitnessPhase;
            _dbContext.GoalSettings.RemoveRange(orphanGoals);
        }

        await _dbContext.SaveChangesAsync();
    }

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? null
            : email.Trim().ToLowerInvariant();
    }
}
