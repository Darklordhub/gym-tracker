using System.Collections.Concurrent;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class NutritionRollupService : INutritionRollupService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DayLocks = new(StringComparer.Ordinal);

    private readonly AppDbContext _dbContext;
    private readonly ILogger<NutritionRollupService> _logger;

    public NutritionRollupService(AppDbContext dbContext, ILogger<NutritionRollupService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task RollupDayAsync(int userId, DateOnly date, CancellationToken cancellationToken)
    {
        return ExecuteRollupAsync(userId, date, forceMealsMode: false, cancellationToken);
    }

    public Task SwitchDayToMealsAsync(int userId, DateOnly date, CancellationToken cancellationToken)
    {
        return ExecuteRollupAsync(userId, date, forceMealsMode: true, cancellationToken);
    }

    private async Task ExecuteRollupAsync(
        int userId,
        DateOnly date,
        bool forceMealsMode,
        CancellationToken cancellationToken)
    {
        var lockKey = $"{userId}:{date:yyyy-MM-dd}";
        var dayLock = DayLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await dayLock.WaitAsync(cancellationToken);

        try
        {
            var dailyTotals = await LoadDailyTotalsAsync(userId, date, cancellationToken);
            var dailyLog = await _dbContext.UserCalorieLogs
                .FirstOrDefaultAsync(log => log.UserId == userId && log.Date == date, cancellationToken);

            if (!forceMealsMode)
            {
                if (dailyLog?.SourceMode == CalorieLogSourceModes.Manual && dailyTotals.HasMealItems)
                {
                    _logger.LogInformation(
                        "Rejected nutrition rollup for user {UserId} on {Date} because a manual calorie entry already exists.",
                        userId,
                        date);
                    throw new NutritionModeConflictException();
                }

                if (dailyLog is null && !dailyTotals.HasMealItems)
                {
                    return;
                }

                if (dailyLog?.SourceMode == CalorieLogSourceModes.Manual && !dailyTotals.HasMealItems)
                {
                    return;
                }
            }

            var now = DateTime.UtcNow;

            if (dailyLog is null)
            {
                dailyLog = new UserCalorieLog
                {
                    UserId = userId,
                    Date = date,
                    CreatedAt = now,
                };

                _dbContext.UserCalorieLogs.Add(dailyLog);
            }

            dailyLog.CaloriesConsumed = RoundCalories(dailyTotals.TotalCalories);
            dailyLog.TotalProtein = dailyTotals.TotalProtein;
            dailyLog.TotalCarbs = dailyTotals.TotalCarbs;
            dailyLog.TotalFat = dailyTotals.TotalFat;
            dailyLog.SourceMode = CalorieLogSourceModes.Meals;
            dailyLog.LastRolledUpAt = now;
            dailyLog.Notes = null;
            dailyLog.UpdatedAt = now;
        }
        finally
        {
            dayLock.Release();
        }
    }

    private async Task<DailyMealTotals> LoadDailyTotalsAsync(int userId, DateOnly date, CancellationToken cancellationToken)
    {
        var meals = await _dbContext.UserMeals
            .AsNoTracking()
            .Where(meal => meal.UserId == userId && meal.Date == date)
            .Select(meal => new
            {
                meal.TotalCalories,
                meal.TotalProtein,
                meal.TotalCarbs,
                meal.TotalFat,
                ItemCount = meal.Items.Count,
            })
            .ToListAsync(cancellationToken);

        return new DailyMealTotals(
            meals.Sum(meal => meal.TotalCalories),
            meals.Sum(meal => meal.TotalProtein),
            meals.Sum(meal => meal.TotalCarbs),
            meals.Sum(meal => meal.TotalFat),
            meals.Sum(meal => meal.ItemCount) > 0);
    }

    private static int RoundCalories(decimal totalCalories)
    {
        return decimal.ToInt32(decimal.Round(totalCalories, 0, MidpointRounding.AwayFromZero));
    }

    private sealed record DailyMealTotals(
        decimal TotalCalories,
        decimal TotalProtein,
        decimal TotalCarbs,
        decimal TotalFat,
        bool HasMealItems);
}
