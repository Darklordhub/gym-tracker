using backend.Data;
using backend.Dtos;
using backend.Models;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
        context.ExerciseCatalogItems.AddRange(
            CreateCatalogItem("Barbell bench press", "Chest", "Triceps", "Barbell", now),
            CreateCatalogItem("Seated cable row", "Back", "Biceps", "Cable", now),
            CreateCatalogItem("Barbell back squat", "Quadriceps", "Glutes,Hamstrings", "Barbell", now),
            CreateCatalogItem("Romanian deadlift", "Hamstrings", "Glutes,Lower back", "Barbell", now),
            CreateCatalogItem("Dumbbell shoulder press", "Shoulders", "Triceps", "Dumbbell", now),
            CreateCatalogItem("Plank", "Abdominals", "Obliques", "Bodyweight", now));
        await context.SaveChangesAsync();
        var catalogIds = (await context.ExerciseCatalogItems.Select(item => item.Id).ToListAsync()).ToHashSet();

        var service = new AiWorkoutGeneratorService(context);
        var plan = await service.GenerateAsync(
            "1",
            new AiWorkoutGenerateRequest
            {
                Goal = "strength",
                PreferredWorkoutType = "full body",
                DurationMinutes = 45,
                FitnessLevel = "intermediate",
                IncludeWarmup = false,
                IncludeCooldown = false,
            },
            CancellationToken.None);

        var section = Assert.Single(plan.Sections);
        Assert.NotEmpty(section.Exercises);
        Assert.Contains(
            section.Exercises,
            exercise => exercise.ExerciseCatalogItemId.HasValue &&
                        catalogIds.Contains(exercise.ExerciseCatalogItemId.Value));

        var constructor = Assert.Single(typeof(AiWorkoutGeneratorService).GetConstructors());
        Assert.Equal(
            [typeof(AppDbContext)],
            constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
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
