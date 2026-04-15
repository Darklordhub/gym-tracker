using backend.Contracts;
using backend.Data;
using backend.Extensions;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class GoalsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public GoalsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<GoalSettingsResponse>> GetGoals()
    {
        var userId = User.GetRequiredUserId();
        var goals = await _dbContext.GoalSettings
            .AsNoTracking()
            .Where(goalSettings => goalSettings.UserId == userId)
            .FirstOrDefaultAsync();
        return Ok(MapGoals(goals));
    }

    [HttpPut]
    public async Task<ActionResult<GoalSettingsResponse>> UpsertGoals(GoalSettingsRequest request)
    {
        var userId = User.GetRequiredUserId();
        var goals = await _dbContext.GoalSettings
            .Where(goalSettings => goalSettings.UserId == userId)
            .FirstOrDefaultAsync();

        if (goals is null)
        {
            goals = new GoalSettings
            {
                UserId = userId,
            };
            _dbContext.GoalSettings.Add(goals);
        }

        goals.TargetBodyWeightKg = request.TargetBodyWeightKg.HasValue
            ? decimal.Round(request.TargetBodyWeightKg.Value, 1, MidpointRounding.AwayFromZero)
            : null;
        goals.WeeklyWorkoutTarget = request.WeeklyWorkoutTarget;
        goals.FitnessPhase = NormalizeFitnessPhase(request.FitnessPhase);
        goals.DailyCalorieTarget = request.DailyCalorieTarget;
        goals.CalorieTargetMode = NormalizeCalorieTargetMode(request.CalorieTargetMode);

        await _dbContext.SaveChangesAsync();

        return Ok(MapGoals(goals));
    }

    private static GoalSettingsResponse MapGoals(GoalSettings? goals)
    {
        return new GoalSettingsResponse
        {
            TargetBodyWeightKg = goals?.TargetBodyWeightKg,
            WeeklyWorkoutTarget = goals?.WeeklyWorkoutTarget,
            FitnessPhase = goals?.FitnessPhase ?? "maintain",
            DailyCalorieTarget = goals?.DailyCalorieTarget,
            CalorieTargetMode = string.IsNullOrWhiteSpace(goals?.CalorieTargetMode) ? "manual" : goals!.CalorieTargetMode,
        };
    }

    private static string NormalizeFitnessPhase(string fitnessPhase)
    {
        return fitnessPhase.Trim().ToLowerInvariant();
    }

    private static string NormalizeCalorieTargetMode(string calorieTargetMode)
    {
        return calorieTargetMode.Trim().ToLowerInvariant();
    }
}
