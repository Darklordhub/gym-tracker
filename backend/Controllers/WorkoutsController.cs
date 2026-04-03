using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkoutsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public WorkoutsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkoutResponse>>> GetWorkouts()
    {
        var workouts = await _dbContext.Workouts
            .Include(workout => workout.ExerciseEntries)
            .OrderByDescending(workout => workout.Date)
            .ThenByDescending(workout => workout.Id)
            .ToListAsync();

        return Ok(workouts.Select(MapWorkout));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkoutResponse>> GetWorkout(int id)
    {
        var workout = await _dbContext.Workouts
            .Include(currentWorkout => currentWorkout.ExerciseEntries)
            .FirstOrDefaultAsync(currentWorkout => currentWorkout.Id == id);

        if (workout is null)
        {
            return NotFound();
        }

        return Ok(MapWorkout(workout));
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutResponse>> CreateWorkout(WorkoutRequest request)
    {
        var workout = new Workout
        {
            Date = NormalizeDate(request.Date),
            Notes = request.Notes.Trim(),
            ExerciseEntries = request.ExerciseEntries.Select(exercise => new ExerciseEntry
            {
                ExerciseName = exercise.ExerciseName.Trim(),
                Sets = exercise.Sets,
                Reps = exercise.Reps,
                WeightKg = decimal.Round(exercise.WeightKg, 1, MidpointRounding.AwayFromZero),
            }).ToList(),
        };

        _dbContext.Workouts.Add(workout);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetWorkout), new { id = workout.Id }, MapWorkout(workout));
    }

    private static WorkoutResponse MapWorkout(Workout workout)
    {
        return new WorkoutResponse
        {
            Id = workout.Id,
            Date = workout.Date,
            Notes = workout.Notes,
            ExerciseEntries = workout.ExerciseEntries
                .OrderBy(exercise => exercise.Id)
                .Select(exercise => new ExerciseEntryResponse
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

    private static DateTime NormalizeDate(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    }
}
