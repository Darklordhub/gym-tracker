using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ExerciseCatalogSeedService
{
    private const string BrokenWalkingLungeThumbnailUrl =
        "https://images.unsplash.com/photo-1518611012118-fbaf6f6c1f47?auto=format&fit=crop&w=900&q=80";

    private const string FixedWalkingLungeThumbnailUrl =
        "https://wger.de/media/exercise-images/1102/cf41f3fb-a3e6-4d0b-b704-6404a7e584fc.jpg";

    private readonly AppDbContext _dbContext;
    private readonly ILogger<ExerciseCatalogSeedService> _logger;

    public ExerciseCatalogSeedService(AppDbContext dbContext, ILogger<ExerciseCatalogSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (await _dbContext.ExerciseCatalogItems.AnyAsync())
        {
            await RepairKnownSeedMediaAsync();
            return;
        }

        var now = DateTime.UtcNow;

        var items = new[]
        {
            CreateItem(
                name: "Barbell Back Squat",
                slug: "barbell-back-squat",
                description: "A foundational lower-body lift for building strength and positional control.",
                instructions: "Set the bar across the upper back, brace through the trunk, sit down between the hips, and drive up through the mid-foot.",
                primaryMuscle: "quadriceps",
                secondaryMuscles: "glutes,hamstrings,core",
                equipment: "barbell",
                difficulty: "intermediate",
                thumbnailUrl: "https://images.unsplash.com/photo-1517836357463-d25dfeac3438?auto=format&fit=crop&w=900&q=80",
                videoUrl: "https://www.youtube.com/watch?v=ultWZbUMPL8",
                now: now),
            CreateItem(
                name: "Romanian Deadlift",
                slug: "romanian-deadlift",
                description: "A hip-dominant hinge used to train the posterior chain through a controlled eccentric.",
                instructions: "Keep a soft knee bend, push the hips back, lower the bar along the thighs, and stand tall once hamstring tension peaks.",
                primaryMuscle: "hamstrings",
                secondaryMuscles: "glutes,lower back",
                equipment: "barbell",
                difficulty: "intermediate",
                thumbnailUrl: "https://images.unsplash.com/photo-1518611012118-696072aa579a?auto=format&fit=crop&w=900&q=80",
                videoUrl: "https://www.youtube.com/watch?v=2SHsk9AzdjA",
                now: now),
            CreateItem(
                name: "Flat Dumbbell Bench Press",
                slug: "flat-dumbbell-bench-press",
                description: "A horizontal press that lets each arm move freely while building chest and triceps strength.",
                instructions: "Plant the feet, press the dumbbells above the shoulders, lower under control, and drive back up without losing shoulder position.",
                primaryMuscle: "chest",
                secondaryMuscles: "front delts,triceps",
                equipment: "dumbbells",
                difficulty: "beginner",
                thumbnailUrl: "https://images.unsplash.com/photo-1518310383802-640c2de311b2?auto=format&fit=crop&w=900&q=80",
                videoUrl: "https://www.youtube.com/watch?v=VmB1G1K7v94",
                now: now),
            CreateItem(
                name: "Pull-Up",
                slug: "pull-up",
                description: "A bodyweight vertical pull for upper-back and arm development.",
                instructions: "Start from a dead hang, pull the chest toward the bar, and lower with control to a full arm extension.",
                primaryMuscle: "lats",
                secondaryMuscles: "biceps,upper back,forearms",
                equipment: "pull-up bar",
                difficulty: "intermediate",
                thumbnailUrl: "https://images.unsplash.com/photo-1517963879433-6ad2b056d712?auto=format&fit=crop&w=900&q=80",
                videoUrl: "https://www.youtube.com/watch?v=eGo4IYlbE5g",
                now: now),
            CreateItem(
                name: "Seated Cable Row",
                slug: "seated-cable-row",
                description: "A stable horizontal pull that helps build upper-back volume with a consistent line of pull.",
                instructions: "Sit tall, brace the trunk, row the handle toward the lower ribs, and return with full shoulder protraction.",
                primaryMuscle: "upper back",
                secondaryMuscles: "lats,biceps,rear delts",
                equipment: "cable",
                difficulty: "beginner",
                thumbnailUrl: "https://images.unsplash.com/photo-1534438327276-14e5300c3a48?auto=format&fit=crop&w=900&q=80",
                videoUrl: "https://www.youtube.com/watch?v=GZbfZ033f74",
                now: now),
            CreateItem(
                name: "Walking Lunge",
                slug: "walking-lunge",
                description: "A unilateral leg movement that builds stability, coordination, and lower-body strength.",
                instructions: "Step forward into a long stride, lower with control, drive through the front foot, and continue into the next rep.",
                primaryMuscle: "quadriceps",
                secondaryMuscles: "glutes,hamstrings,adductors",
                equipment: "bodyweight",
                difficulty: "beginner",
                thumbnailUrl: FixedWalkingLungeThumbnailUrl,
                videoUrl: "https://www.youtube.com/watch?v=L8fvypPrzzs",
                now: now),
            CreateItem(
                name: "Standing Overhead Press",
                slug: "standing-overhead-press",
                description: "A vertical press that trains upper-body strength, trunk rigidity, and overhead control.",
                instructions: "Brace the trunk, press the bar in a straight path overhead, and finish with the head through and ribs stacked.",
                primaryMuscle: "shoulders",
                secondaryMuscles: "triceps,upper chest,core",
                equipment: "barbell",
                difficulty: "intermediate",
                thumbnailUrl: "https://images.unsplash.com/photo-1517838277536-f5f99be501cd?auto=format&fit=crop&w=900&q=80",
                videoUrl: "https://www.youtube.com/watch?v=2yjwXTZQDDI",
                now: now),
            CreateItem(
                name: "Lat Pulldown",
                slug: "lat-pulldown",
                description: "A machine-based vertical pull that helps accumulate pulling volume with stable positioning.",
                instructions: "Set the thighs under the pads, pull the bar toward the upper chest, and return slowly to a full overhead stretch.",
                primaryMuscle: "lats",
                secondaryMuscles: "biceps,upper back,rear delts",
                equipment: "machine",
                difficulty: "beginner",
                thumbnailUrl: "https://images.unsplash.com/photo-1583454110551-21f2fa2afe61?auto=format&fit=crop&w=900&q=80",
                videoUrl: "https://www.youtube.com/watch?v=CAwf7n6Luuc",
                now: now),
        };

        _dbContext.ExerciseCatalogItems.AddRange(items);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} exercise catalog items.", items.Length);
    }

    private async Task RepairKnownSeedMediaAsync()
    {
        var walkingLunge = await _dbContext.ExerciseCatalogItems
            .FirstOrDefaultAsync(item =>
                item.Source == "local"
                && item.Slug == "walking-lunge"
                && !item.IsManuallyEdited
                && string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride)
                && item.ThumbnailUrl == BrokenWalkingLungeThumbnailUrl);

        if (walkingLunge is null)
        {
            return;
        }

        walkingLunge.ThumbnailUrl = FixedWalkingLungeThumbnailUrl;
        walkingLunge.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Repaired broken thumbnail URL for seeded exercise {ExerciseName}.", walkingLunge.Name);
    }

    private static ExerciseCatalogItem CreateItem(
        string name,
        string slug,
        string description,
        string instructions,
        string primaryMuscle,
        string secondaryMuscles,
        string equipment,
        string difficulty,
        string thumbnailUrl,
        string videoUrl,
        DateTime now)
    {
        return new ExerciseCatalogItem
        {
            Source = "local",
            Name = name,
            Slug = slug,
            Description = description,
            Instructions = instructions,
            PrimaryMuscle = primaryMuscle,
            SecondaryMuscles = secondaryMuscles,
            Equipment = equipment,
            Difficulty = difficulty,
            ThumbnailUrl = thumbnailUrl,
            VideoUrl = videoUrl,
            IsActive = true,
            LastSyncedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
