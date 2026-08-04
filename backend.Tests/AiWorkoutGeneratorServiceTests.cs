using backend.Configuration;
using backend.Data;
using backend.Dtos;
using backend.Models;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace backend.Tests;

public class AiWorkoutGeneratorServiceTests
{
    [Fact]
    public async Task LocalCatalogBackedGenerator_RemainsTheActiveImplementation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var excludedExercise = CreateCatalogItem("Barbell bench press", "Chest", "Triceps", "Barbell", now);
        var inactiveExercise = CreateCatalogItem("Inactive leg press", "Quadriceps", "Glutes", "Machine", now);
        inactiveExercise.IsActive = false;
        context.ExerciseCatalogItems.AddRange(
            excludedExercise,
            inactiveExercise,
            CreateCatalogItem("Seated cable row", "Back", "Biceps", "Cable", now),
            CreateCatalogItem("Barbell back squat", "Quadriceps", "Glutes,Hamstrings", "Barbell", now),
            CreateCatalogItem("Romanian deadlift", "Hamstrings", "Glutes,Lower back", "Barbell", now),
            CreateCatalogItem("Dumbbell shoulder press", "Shoulders", "Triceps", "Dumbbell", now),
            CreateCatalogItem("Plank", "Abdominals", "Obliques", "Bodyweight", now));
        await context.SaveChangesAsync();
        var activeCatalogIds = (await context.ExerciseCatalogItems
            .Where(item => item.IsActive)
            .Select(item => item.Id)
            .ToListAsync()).ToHashSet();

        var candidateSelector = new ThrowingCandidateSelector();
        var provider = new ThrowingProvider();
        var service = new AiWorkoutGeneratorService(
            context,
            candidateSelector,
            provider,
            Options.Create(new AiWorkoutGenerationOptions { Enabled = false }),
            NullLogger<AiWorkoutGeneratorService>.Instance);
        var plan = await service.GenerateAsync(
            "1",
            new AiWorkoutGenerateRequest
            {
                Goal = "strength",
                PreferredWorkoutType = "full body",
                DurationMinutes = 45,
                FitnessLevel = "intermediate",
                ExcludedExercises = [excludedExercise.Name],
                IncludeWarmup = true,
                IncludeCooldown = true,
            },
            CancellationToken.None);

        Assert.Equal(["Warm-up", "Main workout", "Cooldown"], plan.Sections.Select(section => section.Name));
        var section = plan.Sections.Single(section => section.Name == "Main workout");
        Assert.NotEmpty(section.Exercises);
        Assert.Contains(
            section.Exercises,
            exercise => exercise.ExerciseCatalogItemId.HasValue &&
                        activeCatalogIds.Contains(exercise.ExerciseCatalogItemId.Value));
        Assert.All(
            section.Exercises.Where(exercise => exercise.ExerciseCatalogItemId.HasValue),
            exercise => Assert.Contains(exercise.ExerciseCatalogItemId!.Value, activeCatalogIds));
        Assert.DoesNotContain(section.Exercises, exercise => exercise.Name == excludedExercise.Name);
        Assert.DoesNotContain(section.Exercises, exercise => exercise.Name == inactiveExercise.Name);
        Assert.Equal(0, candidateSelector.CallCount);
        Assert.Equal(0, provider.CallCount);
    }

    private sealed class ThrowingCandidateSelector : IAiWorkoutCandidateSelector
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<AiWorkoutCandidate>> SelectCandidatesAsync(
            string userId,
            AiWorkoutGenerateRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Candidate selector must not run while the feature is disabled.");
        }
    }

    private sealed class ThrowingProvider : IAiWorkoutPlanProvider
    {
        public int CallCount { get; private set; }
        public string ProviderName => "Test";

        public void ValidateConfiguration()
        {
        }

        public Task<AiWorkoutPlanProviderResult> GeneratePlanAsync(
            AiWorkoutPlanProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Provider must not run while the feature is disabled.");
        }
    }

    private static ExerciseCatalogItem CreateCatalogItem(
        string name,
        string primaryMuscle,
        string secondaryMuscles,
        string equipment,
        DateTime now)
    {
        return new ExerciseCatalogItem
        {
            Source = "test",
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            Instructions = $"Perform {name} with controlled technique.",
            PrimaryMuscle = primaryMuscle,
            SecondaryMuscles = secondaryMuscles,
            Equipment = equipment,
            Difficulty = "Intermediate",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
