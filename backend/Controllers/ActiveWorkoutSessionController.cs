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
        var session = await GetCurrentSessionQuery(User.GetRequiredUserId()).FirstOrDefaultAsync();

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
        var userId = User.GetRequiredUserId();
        var existingSession = await GetCurrentSessionQuery(userId).FirstOrDefaultAsync();

        if (existingSession is not null)
        {
            return Conflict(new { message = "An active workout session already exists." });
        }

        var session = BuildSession(request);
        session.UserId = userId;
        session.StartedAtUtc = DateTime.UtcNow;

        _dbContext.ActiveWorkoutSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCurrentActiveWorkoutSession), MapSession(session));
    }

    [HttpPut]
    public async Task<ActionResult<ActiveWorkoutSessionResponse>> UpdateActiveWorkoutSession(
        ActiveWorkoutSessionRequest request)
    {
        var session = await GetCurrentSessionQuery(User.GetRequiredUserId()).FirstOrDefaultAsync();

        if (session is null)
        {
            return NotFound();
        }

        session.Notes = request.Notes.Trim();
        _dbContext.ActiveWorkoutSessionExerciseSets.RemoveRange(
            session.ExerciseEntries.SelectMany(exercise => exercise.Sets));
        _dbContext.ActiveWorkoutSessionExerciseEntries.RemoveRange(session.ExerciseEntries);
        session.ExerciseEntries = request.ExerciseEntries.Select(MapSessionExerciseEntry).ToList();

        await _dbContext.SaveChangesAsync();

        return Ok(MapSession(session));
    }

    [HttpPost("complete")]
    public async Task<ActionResult<WorkoutResponse>> CompleteActiveWorkoutSession()
    {
        var userId = User.GetRequiredUserId();
        var session = await GetCurrentSessionQuery(userId).FirstOrDefaultAsync();

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
            UserId = userId,
            Date = DateTime.SpecifyKind(session.StartedAtUtc.Date, DateTimeKind.Utc),
            Notes = session.Notes,
            ExerciseEntries = session.ExerciseEntries.Select(exercise => new ExerciseEntry
            {
                ExerciseName = exercise.ExerciseName,
                Sets = exercise.Sets.Select(set => new ExerciseSet
                {
                    Order = set.Order,
                    Reps = set.Reps,
                    WeightKg = set.WeightKg,
                }).ToList(),
            }).ToList(),
        };

        _dbContext.Workouts.Add(workout);
        _dbContext.ActiveWorkoutSessions.Remove(session);
        await _dbContext.SaveChangesAsync();

        var completedWorkout = await _dbContext.Workouts
            .Include(currentWorkout => currentWorkout.ExerciseEntries)
            .ThenInclude(exercise => exercise.Sets)
            .FirstAsync(currentWorkout => currentWorkout.Id == workout.Id);

        var personalRecords = await _dbContext.ExerciseSets
            .AsNoTracking()
            .Where(set => set.ExerciseEntry != null && set.ExerciseEntry.Workout != null && set.ExerciseEntry.Workout.UserId == userId)
            .Select(set => new
            {
                ExerciseName = set.ExerciseEntry!.ExerciseName,
                set.WeightKg,
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

    private IQueryable<ActiveWorkoutSession> GetCurrentSessionQuery(int userId)
    {
        return _dbContext.ActiveWorkoutSessions
            .Where(session => session.UserId == userId)
            .Include(session => session.ExerciseEntries)
            .ThenInclude(exercise => exercise.Sets)
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
            Sets = exercise.Sets
                .Select((set, index) => new ActiveWorkoutSessionExerciseSet
                {
                    Order = index + 1,
                    Reps = set.Reps,
                    WeightKg = decimal.Round(set.WeightKg, 1, MidpointRounding.AwayFromZero),
                })
                .ToList(),
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
                    IsPersonalRecord = personalRecords.TryGetValue(
                        NormalizeExerciseName(exercise.ExerciseName),
                        out var personalRecord) && exercise.Sets.Any(set => set.WeightKg == personalRecord),
                    PersonalRecordWeightKg = personalRecords.TryGetValue(
                        NormalizeExerciseName(exercise.ExerciseName),
                        out personalRecord) ? personalRecord : exercise.Sets.Count > 0 ? exercise.Sets.Max(set => set.WeightKg) : 0,
                })
                .ToList(),
        };
    }

    private static string NormalizeExerciseName(string exerciseName)
    {
        return exerciseName.Trim().ToUpperInvariant();
    }
}
