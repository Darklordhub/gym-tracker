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
public class CaloriesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public CaloriesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("latest")]
    public async Task<ActionResult<CalorieLogResponse>> GetLatest()
    {
        var userId = User.GetRequiredUserId();
        var log = await _dbContext.UserCalorieLogs
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
    public async Task<ActionResult<CalorieLogResponse>> UpsertLog(CalorieLogRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.GetRequiredUserId();
        var now = DateTime.UtcNow;
        var existingLog = await _dbContext.UserCalorieLogs
            .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.Date == request.Date);

        if (existingLog is null)
        {
            var createdLog = new UserCalorieLog
            {
                UserId = userId,
                Date = request.Date,
                CaloriesConsumed = request.CaloriesConsumed,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _dbContext.UserCalorieLogs.Add(createdLog);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLatest), MapLog(createdLog));
        }

        existingLog.CaloriesConsumed = request.CaloriesConsumed;
        existingLog.UpdatedAt = now;

        await _dbContext.SaveChangesAsync();

        return Ok(MapLog(existingLog));
    }

    private static CalorieLogResponse MapLog(UserCalorieLog log)
    {
        return new CalorieLogResponse
        {
            Id = log.Id,
            Date = log.Date,
            CaloriesConsumed = log.CaloriesConsumed,
            CreatedAt = log.CreatedAt,
            UpdatedAt = log.UpdatedAt,
        };
    }
}
