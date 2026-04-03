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

        var personalRecords = BuildPersonalRecordLookup(workouts);

        return Ok(workouts.Select(workout => MapWorkout(workout, personalRecords)));
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

        var personalRecords = await GetPersonalRecordLookupAsync();

        return Ok(MapWorkout(workout, personalRecords));
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

        var personalRecords = await GetPersonalRecordLookupAsync();

        return CreatedAtAction(
            nameof(GetWorkout),
            new { id = workout.Id },
            MapWorkout(workout, personalRecords));
    }

    private static WorkoutResponse MapWorkout(
        Workout workout,
        IReadOnlyDictionary<string, decimal> personalRecords)
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
                    IsPersonalRecord = IsPersonalRecord(exercise, personalRecords),
                    PersonalRecordWeightKg = GetPersonalRecordWeight(exercise, personalRecords),
                })
                .ToList(),
        };
    }

    private static DateTime NormalizeDate(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    }

    private async Task<IReadOnlyDictionary<string, decimal>> GetPersonalRecordLookupAsync()
    {
        var exerciseEntries = await _dbContext.ExerciseEntries
            .AsNoTracking()
            .Select(exercise => new
            {
                exercise.ExerciseName,
                exercise.WeightKg,
            })
            .ToListAsync();

        return exerciseEntries
            .GroupBy(exercise => NormalizeExerciseName(exercise.ExerciseName))
            .ToDictionary(group => group.Key, group => group.Max(exercise => exercise.WeightKg));
    }

    private static IReadOnlyDictionary<string, decimal> BuildPersonalRecordLookup(IEnumerable<Workout> workouts)
    {
        return workouts
            .SelectMany(workout => workout.ExerciseEntries)
            .GroupBy(exercise => NormalizeExerciseName(exercise.ExerciseName))
            .ToDictionary(group => group.Key, group => group.Max(exercise => exercise.WeightKg));
    }

    private static bool IsPersonalRecord(
        ExerciseEntry exercise,
        IReadOnlyDictionary<string, decimal> personalRecords)
    {
        return personalRecords.TryGetValue(NormalizeExerciseName(exercise.ExerciseName), out var personalRecord)
            && exercise.WeightKg == personalRecord;
    }

    private static decimal GetPersonalRecordWeight(
        ExerciseEntry exercise,
        IReadOnlyDictionary<string, decimal> personalRecords)
    {
        return personalRecords.TryGetValue(NormalizeExerciseName(exercise.ExerciseName), out var personalRecord)
            ? personalRecord
            : exercise.WeightKg;
    }

    private static string NormalizeExerciseName(string exerciseName)
    {
        return exerciseName.Trim().ToUpperInvariant();
    }
}
