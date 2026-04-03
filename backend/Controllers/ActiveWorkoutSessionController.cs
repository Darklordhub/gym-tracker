using backend.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActiveWorkoutSessionController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ActiveWorkoutSessionController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<ActiveWorkoutSessionResponse>> GetCurrentActiveWorkoutSession()
    {
        var session = await GetCurrentSessionQuery().FirstOrDefaultAsync();

        if (session is null)
        {
            return NotFound();
        }

        return Ok(MapSession(session));
    }

    [HttpPost]
    public async Task<ActionResult<ActiveWorkoutSessionResponse>> StartActiveWorkoutSession(
        ActiveWorkoutSessionRequest request)
    {
        var existingSession = await GetCurrentSessionQuery().FirstOrDefaultAsync();

        if (existingSession is not null)
        {
            return Conflict(new { message = "An active workout session already exists." });
        }

        var session = BuildSession(request);
        session.StartedAtUtc = DateTime.UtcNow;

        _dbContext.ActiveWorkoutSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCurrentActiveWorkoutSession), MapSession(session));
    }

    [HttpPut]
    public async Task<ActionResult<ActiveWorkoutSessionResponse>> UpdateActiveWorkoutSession(
        ActiveWorkoutSessionRequest request)
    {
        var session = await GetCurrentSessionQuery().FirstOrDefaultAsync();

        if (session is null)
        {
            return NotFound();
        }

        session.Notes = request.Notes.Trim();
        _dbContext.ActiveWorkoutSessionExerciseEntries.RemoveRange(session.ExerciseEntries);
        session.ExerciseEntries = request.ExerciseEntries.Select(MapSessionExerciseEntry).ToList();

        await _dbContext.SaveChangesAsync();

        return Ok(MapSession(session));
    }

    [HttpPost("complete")]
    public async Task<ActionResult<WorkoutResponse>> CompleteActiveWorkoutSession()
    {
        var session = await GetCurrentSessionQuery().FirstOrDefaultAsync();

        if (session is null)
        {
            return NotFound();
        }

        if (session.ExerciseEntries.Count == 0)
        {
            ModelState.AddModelError(
                nameof(ActiveWorkoutSession.ExerciseEntries),
                "Add at least one exercise before completing the workout.");

            return ValidationProblem(ModelState);
        }

        var workout = new Workout
        {
            Date = DateTime.SpecifyKind(session.StartedAtUtc.Date, DateTimeKind.Utc),
            Notes = session.Notes,
            ExerciseEntries = session.ExerciseEntries.Select(exercise => new ExerciseEntry
            {
                ExerciseName = exercise.ExerciseName,
                Sets = exercise.Sets,
                Reps = exercise.Reps,
                WeightKg = exercise.WeightKg,
            }).ToList(),
        };

        _dbContext.Workouts.Add(workout);
        _dbContext.ActiveWorkoutSessions.Remove(session);
        await _dbContext.SaveChangesAsync();

        var completedWorkout = await _dbContext.Workouts
            .Include(currentWorkout => currentWorkout.ExerciseEntries)
            .FirstAsync(currentWorkout => currentWorkout.Id == workout.Id);

        var personalRecords = await _dbContext.ExerciseEntries
            .AsNoTracking()
            .Select(exercise => new
            {
                exercise.ExerciseName,
                exercise.WeightKg,
            })
            .ToListAsync();

        var personalRecordLookup = personalRecords
            .GroupBy(exercise => NormalizeExerciseName(exercise.ExerciseName))
            .ToDictionary(group => group.Key, group => group.Max(exercise => exercise.WeightKg));

        return CreatedAtAction(
            nameof(WorkoutsController.GetWorkout),
            "Workouts",
            new { id = completedWorkout.Id },
            MapWorkout(completedWorkout, personalRecordLookup));
    }

    private IQueryable<ActiveWorkoutSession> GetCurrentSessionQuery()
    {
        return _dbContext.ActiveWorkoutSessions
            .Include(session => session.ExerciseEntries)
            .OrderByDescending(session => session.StartedAtUtc)
            .ThenByDescending(session => session.Id);
    }

    private static ActiveWorkoutSession BuildSession(ActiveWorkoutSessionRequest request)
    {
        return new ActiveWorkoutSession
        {
            Notes = request.Notes.Trim(),
            ExerciseEntries = request.ExerciseEntries.Select(MapSessionExerciseEntry).ToList(),
        };
    }

    private static ActiveWorkoutSessionExerciseEntry MapSessionExerciseEntry(ExerciseEntryRequest exercise)
    {
        return new ActiveWorkoutSessionExerciseEntry
        {
            ExerciseName = exercise.ExerciseName.Trim(),
            Sets = exercise.Sets,
            Reps = exercise.Reps,
            WeightKg = decimal.Round(exercise.WeightKg, 1, MidpointRounding.AwayFromZero),
        };
    }

    private static ActiveWorkoutSessionResponse MapSession(ActiveWorkoutSession session)
    {
        return new ActiveWorkoutSessionResponse
        {
            Id = session.Id,
            StartedAtUtc = session.StartedAtUtc,
            Notes = session.Notes,
            ExerciseEntries = session.ExerciseEntries
                .OrderBy(exercise => exercise.Id)
                .Select(exercise => new ActiveWorkoutSessionExerciseEntryResponse
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
                    IsPersonalRecord = personalRecords.TryGetValue(
                        NormalizeExerciseName(exercise.ExerciseName),
                        out var personalRecord) && exercise.WeightKg == personalRecord,
                    PersonalRecordWeightKg = personalRecords.TryGetValue(
                        NormalizeExerciseName(exercise.ExerciseName),
                        out personalRecord) ? personalRecord : exercise.WeightKg,
                })
                .ToList(),
        };
    }

    private static string NormalizeExerciseName(string exerciseName)
    {
        return exerciseName.Trim().ToUpperInvariant();
    }
}
