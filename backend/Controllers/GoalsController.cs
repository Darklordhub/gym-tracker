using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
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
        var goals = await _dbContext.GoalSettings.AsNoTracking().FirstOrDefaultAsync();
        return Ok(MapGoals(goals));
    }

    [HttpPut]
    public async Task<ActionResult<GoalSettingsResponse>> UpsertGoals(GoalSettingsRequest request)
    {
        var goals = await _dbContext.GoalSettings.FirstOrDefaultAsync();

        if (goals is null)
        {
            goals = new GoalSettings();
            _dbContext.GoalSettings.Add(goals);
        }

        goals.TargetBodyWeightKg = request.TargetBodyWeightKg.HasValue
            ? decimal.Round(request.TargetBodyWeightKg.Value, 1, MidpointRounding.AwayFromZero)
            : null;
        goals.WeeklyWorkoutTarget = request.WeeklyWorkoutTarget;
        goals.FitnessPhase = NormalizeFitnessPhase(request.FitnessPhase);

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
        };
    }

    private static string NormalizeFitnessPhase(string fitnessPhase)
    {
        return fitnessPhase.Trim().ToLowerInvariant();
    }
}
