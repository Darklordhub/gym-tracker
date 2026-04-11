using backend.Contracts;
using backend.Data;
using backend.Extensions;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CycleController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public CycleController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("settings")]
    public async Task<ActionResult<CycleSettingsResponse>> GetSettings()
    {
        var userId = User.GetRequiredUserId();
        var settings = await _dbContext.UserCycleSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.UserId == userId);

        return Ok(MapSettings(settings));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<CycleSettingsResponse>> UpsertSettings(CycleSettingsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.GetRequiredUserId();
        var settings = await _dbContext.UserCycleSettings
            .FirstOrDefaultAsync(entry => entry.UserId == userId);

        var now = DateTime.UtcNow;

        if (settings is null)
        {
            settings = new UserCycleSettings
            {
                UserId = userId,
                CreatedAt = now,
            };
            _dbContext.UserCycleSettings.Add(settings);
        }

        settings.IsEnabled = request.IsEnabled;
        settings.LastPeriodStartDate = request.LastPeriodStartDate;
        settings.AverageCycleLengthDays = request.AverageCycleLengthDays;
        settings.AveragePeriodLengthDays = request.AveragePeriodLengthDays;
        settings.CycleRegularity = NormalizeRegularity(request.CycleRegularity);
        settings.UsesHormonalContraception = request.UsesHormonalContraception;
        settings.IsNaturallyCycling = request.IsNaturallyCycling;
        settings.UpdatedAt = now;

        await _dbContext.SaveChangesAsync();

        return Ok(MapSettings(settings));
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<CycleEntryResponse>>> GetHistory()
    {
        var userId = User.GetRequiredUserId();
        var entries = await _dbContext.UserCycleEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.PeriodStartDate)
            .ToListAsync();

        return Ok(entries.Select(MapEntry));
    }

    [HttpPost("history")]
    public async Task<ActionResult<CycleEntryResponse>> CreateHistoryEntry(CycleEntryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.GetRequiredUserId();
        var entry = new UserCycleEntry
        {
            UserId = userId,
            PeriodStartDate = request.PeriodStartDate,
            PeriodEndDate = request.PeriodEndDate,
            Notes = NormalizeOptionalText(request.Notes),
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.UserCycleEntries.Add(entry);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetHistory), MapEntry(entry));
    }

    [HttpPut("history/{entryId:int}")]
    public async Task<ActionResult<CycleEntryResponse>> UpdateHistoryEntry(int entryId, CycleEntryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.GetRequiredUserId();
        var entry = await _dbContext.UserCycleEntries
            .FirstOrDefaultAsync(item => item.Id == entryId && item.UserId == userId);

        if (entry is null)
        {
            return NotFound();
        }

        entry.PeriodStartDate = request.PeriodStartDate;
        entry.PeriodEndDate = request.PeriodEndDate;
        entry.Notes = NormalizeOptionalText(request.Notes);

        await _dbContext.SaveChangesAsync();

        return Ok(MapEntry(entry));
    }

    [HttpDelete("history/{entryId:int}")]
    public async Task<IActionResult> DeleteHistoryEntry(int entryId)
    {
        var userId = User.GetRequiredUserId();
        var entry = await _dbContext.UserCycleEntries
            .FirstOrDefaultAsync(item => item.Id == entryId && item.UserId == userId);

        if (entry is null)
        {
            return NotFound();
        }

        _dbContext.UserCycleEntries.Remove(entry);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("guidance")]
    public async Task<ActionResult<CycleGuidanceResponse>> GetGuidance()
    {
        var userId = User.GetRequiredUserId();
        var settings = await _dbContext.UserCycleSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.UserId == userId);

        var entries = await _dbContext.UserCycleEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.PeriodStartDate)
            .ToListAsync();

        var workouts = await _dbContext.Workouts
            .AsNoTracking()
            .Where(workout => workout.UserId == userId)
            .Include(workout => workout.ExerciseEntries)
                .ThenInclude(entry => entry.Sets)
            .OrderByDescending(workout => workout.Date)
            .Take(12)
            .ToListAsync();

        return Ok(CycleGuidanceService.Build(settings, entries, workouts));
    }

    private static CycleSettingsResponse MapSettings(UserCycleSettings? settings)
    {
        return new CycleSettingsResponse
        {
            IsEnabled = settings?.IsEnabled ?? false,
            LastPeriodStartDate = settings?.LastPeriodStartDate,
            AverageCycleLengthDays = settings?.AverageCycleLengthDays,
            AveragePeriodLengthDays = settings?.AveragePeriodLengthDays,
            CycleRegularity = settings?.CycleRegularity ?? "regular",
            UsesHormonalContraception = settings?.UsesHormonalContraception,
            IsNaturallyCycling = settings?.IsNaturallyCycling,
            UpdatedAt = settings?.UpdatedAt,
        };
    }

    private static CycleEntryResponse MapEntry(UserCycleEntry entry)
    {
        return new CycleEntryResponse
        {
            Id = entry.Id,
            PeriodStartDate = entry.PeriodStartDate,
            PeriodEndDate = entry.PeriodEndDate,
            Notes = entry.Notes,
            CreatedAt = entry.CreatedAt,
        };
    }

    private static string NormalizeRegularity(string value)
    {
        return value.Trim().ToLowerInvariant();
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
