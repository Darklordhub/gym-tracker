using backend.Configuration;
using backend.Data;
using backend.Dtos;
using backend.Models;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Tests;

public class AiWorkoutCandidateSelectorTests
{
    [Fact]
    public void CandidateContract_DoesNotExposeInstructionsOrMediaFields()
    {
        var propertyNames = typeof(AiWorkoutCandidate)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Instructions", propertyNames);
        Assert.DoesNotContain("VideoUrl", propertyNames);
        Assert.DoesNotContain("ThumbnailUrl", propertyNames);
        Assert.DoesNotContain("LocalMediaPath", propertyNames);
    }

    [Fact]
    public async Task SelectCandidates_ReturnsNoMoreThanConfiguredMaximum()
    {
        await using var database = await CandidateSelectorTestDatabase.CreateAsync();
        for (var index = 0; index < 30; index++)
        {
            database.Context.ExerciseCatalogItems.Add(CreateExercise(
                $"Barbell squat variation {index}",
                "Quadriceps",
                "Glutes,Hamstrings"));
        }

        await database.Context.SaveChangesAsync();
        var selector = database.CreateSelector(maxCandidateExercises: 12);

        var candidates = await selector.SelectCandidatesAsync("1", LowerBodyRequest());

        Assert.Equal(12, candidates.Count);
        Assert.Equal(Enumerable.Range(1, 12), candidates.Select(candidate => candidate.Rank));
    }

    [Fact]
    public async Task SelectCandidates_ClampsInvalidConfiguredMaximumSafely()
    {
        await using var database = await CandidateSelectorTestDatabase.CreateAsync();
        for (var index = 0; index < 20; index++)
        {
            database.Context.ExerciseCatalogItems.Add(CreateExercise(
                $"Goblet squat variation {index}",
                "Quadriceps",
                "Glutes"));
        }

        await database.Context.SaveChangesAsync();
        var selector = database.CreateSelector(maxCandidateExercises: 1);

        var candidates = await selector.SelectCandidatesAsync("1", LowerBodyRequest());

        Assert.Equal(AiWorkoutGenerationOptions.MinCandidateExercises, candidates.Count);
    }

    [Fact]
    public async Task SelectCandidates_ReturnsOnlyActiveCatalogExercises()
    {
        await using var database = await CandidateSelectorTestDatabase.CreateAsync();
        var active = CreateExercise("Active back squat", "Quadriceps", "Glutes");
        var inactive = CreateExercise("Inactive front squat", "Quadriceps", "Glutes", isActive: false);
        database.Context.ExerciseCatalogItems.AddRange(active, inactive);
        await database.Context.SaveChangesAsync();

        var candidates = await database.CreateSelector().SelectCandidatesAsync("1", LowerBodyRequest());

        Assert.Contains(candidates, candidate => candidate.ExerciseCatalogItemId == active.Id);
        Assert.DoesNotContain(candidates, candidate => candidate.ExerciseCatalogItemId == inactive.Id);
    }

    [Fact]
    public async Task SelectCandidates_RemovesExcludedExercises()
    {
        await using var database = await CandidateSelectorTestDatabase.CreateAsync();
        var excluded = CreateExercise("Barbell back squat", "Quadriceps", "Glutes,Hamstrings");
        var available = CreateExercise("Barbell front squat", "Quadriceps", "Glutes");
        database.Context.ExerciseCatalogItems.AddRange(excluded, available);
        await database.Context.SaveChangesAsync();
        var request = LowerBodyRequest();
        request.ExcludedExercises = ["  BARBELL back-squat  "];

        var candidates = await database.CreateSelector().SelectCandidatesAsync("1", request);

        Assert.DoesNotContain(candidates, candidate => candidate.ExerciseCatalogItemId == excluded.Id);
        Assert.Contains(candidates, candidate => candidate.ExerciseCatalogItemId == available.Id);
    }

    [Fact]
    public async Task SelectCandidates_TargetMusclesInfluenceRanking()
    {
        await using var database = await CandidateSelectorTestDatabase.CreateAsync();
        var squat = CreateExercise("Barbell back squat", "Quadriceps", "Glutes,Hamstrings");
        var press = CreateExercise("Barbell bench press", "Chest", "Triceps,Shoulders");
        database.Context.ExerciseCatalogItems.AddRange(squat, press);
        await database.Context.SaveChangesAsync();
        var selector = database.CreateSelector();

        var legCandidates = await selector.SelectCandidatesAsync(
            "1",
            TargetedRequest("quadriceps"));
        var chestCandidates = await selector.SelectCandidatesAsync(
            "1",
            TargetedRequest("chest"));

        Assert.Equal(squat.Id, legCandidates[0].ExerciseCatalogItemId);
        Assert.Equal(press.Id, chestCandidates[0].ExerciseCatalogItemId);
        Assert.Contains("target muscle match", legCandidates[0].MatchReasons);
        Assert.Contains("target muscle match", chestCandidates[0].MatchReasons);
    }

    [Fact]
    public async Task SelectCandidates_RanksRecentlyUsedExerciseBelowFreshAlternative()
    {
        await using var database = await CandidateSelectorTestDatabase.CreateAsync();
        var recent = CreateExercise("Barbell back squat", "Quadriceps", "Glutes,Hamstrings");
        var fresh = CreateExercise("Barbell front squat", "Quadriceps", "Glutes");
        database.Context.ExerciseCatalogItems.AddRange(recent, fresh);
        database.Context.Workouts.Add(new Workout
        {
            UserId = 1,
            Date = DateTime.UtcNow,
            WorkoutType = "strength",
            ExerciseEntries =
            [
                new ExerciseEntry
                {
                    ExerciseName = recent.Name,
                    Sets =
                    [
                        new ExerciseSet { Order = 1, Reps = 8, WeightKg = 80 },
                    ],
                },
            ],
        });
        await database.Context.SaveChangesAsync();

        var candidates = await database.CreateSelector().SelectCandidatesAsync("1", LowerBodyRequest());
        var recentCandidate = Assert.Single(candidates, candidate => candidate.ExerciseCatalogItemId == recent.Id);
        var freshCandidate = Assert.Single(candidates, candidate => candidate.ExerciseCatalogItemId == fresh.Id);

        Assert.True(freshCandidate.Rank < recentCandidate.Rank);
        Assert.True(freshCandidate.Score > recentCandidate.Score);
        Assert.True(recentCandidate.WasRecentlyUsed);
        Assert.Contains("recent-use penalty applied", recentCandidate.MatchReasons);
    }

    [Fact]
    public async Task SelectCandidates_HandlesEmptyAndInsufficientCatalogSafely()
    {
        await using var database = await CandidateSelectorTestDatabase.CreateAsync();
        var selector = database.CreateSelector();

        var emptyResult = await selector.SelectCandidatesAsync("1", LowerBodyRequest());

        Assert.Empty(emptyResult);

        var onlyExercise = CreateExercise("Bodyweight squat", "Quadriceps", "Glutes");
        database.Context.ExerciseCatalogItems.Add(onlyExercise);
        await database.Context.SaveChangesAsync();

        var insufficientResult = await selector.SelectCandidatesAsync("1", LowerBodyRequest());

        var candidate = Assert.Single(insufficientResult);
        Assert.Equal(onlyExercise.Id, candidate.ExerciseCatalogItemId);
    }

    private static AiWorkoutGenerateRequest LowerBodyRequest()
    {
        return new AiWorkoutGenerateRequest
        {
            Goal = "strength",
            PreferredWorkoutType = "lower body",
            DurationMinutes = 45,
            FitnessLevel = "intermediate",
        };
    }

    private static AiWorkoutGenerateRequest TargetedRequest(string targetMuscle)
    {
        return new AiWorkoutGenerateRequest
        {
            Goal = "strength",
            DurationMinutes = 45,
            FitnessLevel = "intermediate",
            TargetMuscles = [targetMuscle],
        };
    }

    private static ExerciseCatalogItem CreateExercise(
        string name,
        string primaryMuscle,
        string secondaryMuscles,
        bool isActive = true)
    {
        var now = DateTime.UtcNow;
        return new ExerciseCatalogItem
        {
            Source = "test",
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            Instructions = $"Perform {name} with controlled technique.",
            PrimaryMuscle = primaryMuscle,
            SecondaryMuscles = secondaryMuscles,
            Equipment = name.Contains("bodyweight", StringComparison.OrdinalIgnoreCase) ? "Bodyweight" : "Barbell",
            Difficulty = "Intermediate",
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private sealed class CandidateSelectorTestDatabase : IAsyncDisposable
    {
        private CandidateSelectorTestDatabase(SqliteConnection connection, AppDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public AppDbContext Context { get; }

        public static async Task<CandidateSelectorTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Users.Add(new AppUser
            {
                Id = 1,
                Email = $"candidate-{Guid.NewGuid():N}@example.test",
                PasswordHash = "test",
                FullName = "Candidate Test User",
                CreatedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
            return new CandidateSelectorTestDatabase(connection, context);
        }

        public AiWorkoutCandidateSelector CreateSelector(int maxCandidateExercises = 60)
        {
            return new AiWorkoutCandidateSelector(
                Context,
                Options.Create(new AiWorkoutGenerationOptions
                {
                    Enabled = false,
                    MaxCandidateExercises = maxCandidateExercises,
                }));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
