using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
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
        var templates = await _dbContext.WorkoutTemplates
            .Include(template => template.ExerciseEntries)
            .OrderBy(template => template.Name)
            .ThenBy(template => template.Id)
            .ToListAsync();

        return Ok(templates.Select(MapTemplate));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkoutTemplateResponse>> GetWorkoutTemplate(int id)
    {
        var template = await _dbContext.WorkoutTemplates
            .Include(currentTemplate => currentTemplate.ExerciseEntries)
            .FirstOrDefaultAsync(currentTemplate => currentTemplate.Id == id);

        if (template is null)
        {
            return NotFound();
        }

        return Ok(MapTemplate(template));
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutTemplateResponse>> CreateWorkoutTemplate(WorkoutTemplateRequest request)
    {
        var template = new WorkoutTemplate
        {
            Name = request.Name.Trim(),
            Notes = request.Notes.Trim(),
            ExerciseEntries = request.ExerciseEntries.Select(exercise => new WorkoutTemplateExerciseEntry
            {
                ExerciseName = exercise.ExerciseName.Trim(),
                Sets = exercise.Sets,
                Reps = exercise.Reps,
                WeightKg = decimal.Round(exercise.WeightKg, 1, MidpointRounding.AwayFromZero),
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
                    Sets = exercise.Sets,
                    Reps = exercise.Reps,
                    WeightKg = exercise.WeightKg,
                })
                .ToList(),
        };
    }
}
