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
public class ReadinessController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ReadinessController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("latest")]
    public async Task<ActionResult<ReadinessLogResponse>> GetLatest()
    {
        var userId = User.GetRequiredUserId();
        var log = await _dbContext.UserReadinessLogs
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.UpdatedAt)
            .FirstOrDefaultAsync();

        if (log is null)
        {
            return NotFound();
        }

        return Ok(MapLog(log));
    }

    [HttpPost]
    public async Task<ActionResult<ReadinessLogResponse>> UpsertLog(ReadinessLogRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.GetRequiredUserId();
        var now = DateTime.UtcNow;
        var existingLog = await _dbContext.UserReadinessLogs
            .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.Date == request.Date);

        if (existingLog is null)
        {
            var createdLog = new UserReadinessLog
            {
                UserId = userId,
                Date = request.Date,
                EnergyLevel = request.EnergyLevel,
                SorenessLevel = request.SorenessLevel,
                SleepQuality = request.SleepQuality,
                MotivationLevel = request.MotivationLevel,
                Notes = NormalizeOptionalText(request.Notes),
                CreatedAt = now,
                UpdatedAt = now,
            };

            _dbContext.UserReadinessLogs.Add(createdLog);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLatest), MapLog(createdLog));
        }

        existingLog.EnergyLevel = request.EnergyLevel;
        existingLog.SorenessLevel = request.SorenessLevel;
        existingLog.SleepQuality = request.SleepQuality;
        existingLog.MotivationLevel = request.MotivationLevel;
        existingLog.Notes = NormalizeOptionalText(request.Notes);
        existingLog.UpdatedAt = now;

        await _dbContext.SaveChangesAsync();

        return Ok(MapLog(existingLog));
    }

    private static ReadinessLogResponse MapLog(UserReadinessLog log)
    {
        var score = CalculateReadinessScore(log);

        return new ReadinessLogResponse
        {
            Id = log.Id,
            Date = log.Date,
            EnergyLevel = log.EnergyLevel,
            SorenessLevel = log.SorenessLevel,
            SleepQuality = log.SleepQuality,
            MotivationLevel = log.MotivationLevel,
            Notes = log.Notes,
            ReadinessScore = score,
            ReadinessLabel = score >= 2.5m ? "High readiness" : score >= 1.9m ? "Balanced readiness" : "Low readiness",
            CreatedAt = log.CreatedAt,
            UpdatedAt = log.UpdatedAt,
        };
    }

    private static decimal CalculateReadinessScore(UserReadinessLog log)
    {
        var sorenessRecovery = 4 - log.SorenessLevel;
        var score = (log.EnergyLevel + sorenessRecovery + log.SleepQuality + log.MotivationLevel) / 4m;
        return decimal.Round(score, 2, MidpointRounding.AwayFromZero);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
