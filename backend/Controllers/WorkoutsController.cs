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
            .ThenInclude(exercise => exercise.Sets)
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
            .ThenInclude(exercise => exercise.Sets)
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
                Sets = exercise.Sets
                    .Select((set, index) => new ExerciseSet
                    {
                        Order = index + 1,
                        Reps = set.Reps,
                        WeightKg = decimal.Round(set.WeightKg, 1, MidpointRounding.AwayFromZero),
                    })
                    .ToList(),
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
        var exerciseEntries = await _dbContext.ExerciseSets
            .AsNoTracking()
            .Select(set => new
            {
                ExerciseName = set.ExerciseEntry!.ExerciseName,
                set.WeightKg,
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
            .Where(exercise => exercise.Sets.Count > 0)
            .GroupBy(exercise => NormalizeExerciseName(exercise.ExerciseName))
            .ToDictionary(group => group.Key, group => group.Max(exercise => exercise.Sets.Max(set => set.WeightKg)));
    }

    private static bool IsPersonalRecord(
        ExerciseEntry exercise,
        IReadOnlyDictionary<string, decimal> personalRecords)
    {
        return personalRecords.TryGetValue(NormalizeExerciseName(exercise.ExerciseName), out var personalRecord)
            && exercise.Sets.Any(set => set.WeightKg == personalRecord);
    }

    private static decimal GetPersonalRecordWeight(
        ExerciseEntry exercise,
        IReadOnlyDictionary<string, decimal> personalRecords)
    {
        return personalRecords.TryGetValue(NormalizeExerciseName(exercise.ExerciseName), out var personalRecord)
            ? personalRecord
            : exercise.Sets.Count > 0 ? exercise.Sets.Max(set => set.WeightKg) : 0;
    }

    private static string NormalizeExerciseName(string exerciseName)
    {
        return exerciseName.Trim().ToUpperInvariant();
    }
}
