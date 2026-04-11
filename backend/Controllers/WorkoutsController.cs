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
        var userId = User.GetRequiredUserId();
        var workouts = await _dbContext.Workouts
            .Where(workout => workout.UserId == userId)
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
        var userId = User.GetRequiredUserId();
        var workout = await _dbContext.Workouts
            .Include(currentWorkout => currentWorkout.ExerciseEntries)
            .ThenInclude(exercise => exercise.Sets)
            .FirstOrDefaultAsync(currentWorkout => currentWorkout.Id == id && currentWorkout.UserId == userId);

        if (workout is null)
        {
            return NotFound();
        }

        var personalRecords = await GetPersonalRecordLookupAsync(userId);

        return Ok(MapWorkout(workout, personalRecords));
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutResponse>> CreateWorkout(WorkoutRequest request)
    {
        var userId = User.GetRequiredUserId();
        var workoutType = NormalizeWorkoutType(request.WorkoutType);
        var workout = new Workout
        {
            UserId = userId,
            Date = NormalizeDate(request.Date),
            WorkoutType = workoutType,
            Notes = request.Notes.Trim(),
            CardioActivityType = workoutType == "cardio" ? NormalizeOptionalText(request.CardioActivityType) : null,
            CardioDurationMinutes = workoutType == "cardio" ? request.CardioDurationMinutes : null,
            CardioDistanceKm = workoutType == "cardio" && request.CardioDistanceKm.HasValue
                ? decimal.Round(request.CardioDistanceKm.Value, 1, MidpointRounding.AwayFromZero)
                : null,
            CardioIntensity = workoutType == "cardio" ? NormalizeOptionalText(request.CardioIntensity)?.ToLowerInvariant() : null,
            ExerciseEntries = workoutType == "cardio"
                ? new List<ExerciseEntry>()
                : request.ExerciseEntries.Select(exercise => new ExerciseEntry
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

        var personalRecords = await GetPersonalRecordLookupAsync(userId);

        return CreatedAtAction(
            nameof(GetWorkout),
            new { id = workout.Id },
            MapWorkout(workout, personalRecords));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteWorkout(int id)
    {
        var userId = User.GetRequiredUserId();
        var workout = await _dbContext.Workouts
            .FirstOrDefaultAsync(currentWorkout => currentWorkout.Id == id && currentWorkout.UserId == userId);

        if (workout is null)
        {
            return NotFound();
        }

        _dbContext.Workouts.Remove(workout);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private static WorkoutResponse MapWorkout(
        Workout workout,
        IReadOnlyDictionary<string, decimal> personalRecords)
    {
        return new WorkoutResponse
        {
            Id = workout.Id,
            Date = workout.Date,
            WorkoutType = NormalizeWorkoutType(workout.WorkoutType),
            Notes = workout.Notes,
            CardioActivityType = workout.CardioActivityType,
            CardioDurationMinutes = workout.CardioDurationMinutes,
            CardioDistanceKm = workout.CardioDistanceKm,
            CardioIntensity = workout.CardioIntensity,
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

    private async Task<IReadOnlyDictionary<string, decimal>> GetPersonalRecordLookupAsync(int userId)
    {
        var exerciseEntries = await _dbContext.ExerciseSets
            .AsNoTracking()
            .Where(set => set.ExerciseEntry != null && set.ExerciseEntry.Workout != null && set.ExerciseEntry.Workout.UserId == userId)
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

    private static string NormalizeWorkoutType(string? workoutType)
    {
        return string.IsNullOrWhiteSpace(workoutType) ? "strength" : workoutType.Trim().ToLowerInvariant();
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
