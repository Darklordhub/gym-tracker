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
public class WorkoutTemplatesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public WorkoutTemplatesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkoutTemplateResponse>>> GetWorkoutTemplates()
    {
        var userId = User.GetRequiredUserId();
        var templates = await _dbContext.WorkoutTemplates
            .Where(template => template.UserId == userId)
            .Include(template => template.ExerciseEntries)
            .ThenInclude(exercise => exercise.Sets)
            .OrderBy(template => template.Name)
            .ThenBy(template => template.Id)
            .ToListAsync();

        return Ok(templates.Select(MapTemplate));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkoutTemplateResponse>> GetWorkoutTemplate(int id)
    {
        var userId = User.GetRequiredUserId();
        var template = await _dbContext.WorkoutTemplates
            .Include(currentTemplate => currentTemplate.ExerciseEntries)
            .ThenInclude(exercise => exercise.Sets)
            .FirstOrDefaultAsync(currentTemplate => currentTemplate.Id == id && currentTemplate.UserId == userId);

        if (template is null)
        {
            return NotFound();
        }

        return Ok(MapTemplate(template));
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutTemplateResponse>> CreateWorkoutTemplate(WorkoutTemplateRequest request)
    {
        var userId = User.GetRequiredUserId();
        var template = new WorkoutTemplate
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Notes = request.Notes.Trim(),
            ExerciseEntries = request.ExerciseEntries.Select(exercise => new WorkoutTemplateExerciseEntry
            {
                ExerciseName = exercise.ExerciseName.Trim(),
                Sets = exercise.Sets
                    .Select((set, index) => new WorkoutTemplateExerciseSet
                    {
                        Order = index + 1,
                        Reps = set.Reps,
                        WeightKg = decimal.Round(set.WeightKg, 1, MidpointRounding.AwayFromZero),
                    })
                    .ToList(),
            }).ToList(),
        };

        _dbContext.WorkoutTemplates.Add(template);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetWorkoutTemplate),
            new { id = template.Id },
            MapTemplate(template));
    }

    private static WorkoutTemplateResponse MapTemplate(WorkoutTemplate template)
    {
        return new WorkoutTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            Notes = template.Notes,
            ExerciseEntries = template.ExerciseEntries
                .OrderBy(exercise => exercise.Id)
                .Select(exercise => new WorkoutTemplateExerciseEntryResponse
                {
                    Id = exercise.Id,
                    ExerciseName = exercise.ExerciseName,
                    Sets = exercise.Sets
                        .OrderBy(set => set.Order)
                        .Select(set => new ExerciseSetResponse
                        {
                            Id = set.Id,
                            Order = set.Order,
                            Reps = set.Reps,
                            WeightKg = set.WeightKg,
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }
}
