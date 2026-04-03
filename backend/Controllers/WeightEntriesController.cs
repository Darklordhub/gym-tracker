using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeightEntriesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public WeightEntriesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WeightEntryResponse>>> GetWeightEntries()
    {
        var weightEntries = await _dbContext.WeightEntries
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.Id)
            .ToListAsync();

        return Ok(weightEntries.Select(MapWeightEntry));
    }

    [HttpPost]
    public async Task<ActionResult<WeightEntryResponse>> CreateWeightEntry(WeightEntryRequest request)
    {
        var weightEntry = new WeightEntry
        {
            Date = NormalizeDate(request.Date),
            WeightKg = decimal.Round(request.WeightKg, 1, MidpointRounding.AwayFromZero),
        };

        _dbContext.WeightEntries.Add(weightEntry);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetWeightEntry), new { id = weightEntry.Id }, MapWeightEntry(weightEntry));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WeightEntryResponse>> GetWeightEntry(int id)
    {
        var weightEntry = await _dbContext.WeightEntries.FindAsync(id);

        if (weightEntry is null)
        {
            return NotFound();
        }

        return Ok(MapWeightEntry(weightEntry));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<WeightEntryResponse>> UpdateWeightEntry(int id, WeightEntryRequest request)
    {
        var weightEntry = await _dbContext.WeightEntries.FindAsync(id);

        if (weightEntry is null)
        {
            return NotFound();
        }

        weightEntry.Date = NormalizeDate(request.Date);
        weightEntry.WeightKg = decimal.Round(request.WeightKg, 1, MidpointRounding.AwayFromZero);

        await _dbContext.SaveChangesAsync();

        return Ok(MapWeightEntry(weightEntry));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteWeightEntry(int id)
    {
        var weightEntry = await _dbContext.WeightEntries.FindAsync(id);

        if (weightEntry is null)
        {
            return NotFound();
        }

        _dbContext.WeightEntries.Remove(weightEntry);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private static DateTime NormalizeDate(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    }

    private static WeightEntryResponse MapWeightEntry(WeightEntry weightEntry)
    {
        return new WeightEntryResponse
        {
            Id = weightEntry.Id,
            Date = weightEntry.Date,
            WeightKg = weightEntry.WeightKg,
        };
    }
}
