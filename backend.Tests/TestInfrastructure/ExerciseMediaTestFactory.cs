using backend.Configuration;
using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace backend.Tests.TestInfrastructure;

internal sealed class ExerciseMediaTestFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public ExerciseMediaTestFactory()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "gym-tracker-media-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);

        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        using var context = CreateDbContext();
        context.Database.EnsureCreated();
    }

    public string RootPath { get; }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_dbOptions);
    }

    public ExerciseMediaStorageService CreateStorageService()
    {
        return new ExerciseMediaStorageService(
            Options.Create(new ExerciseMediaStorageOptions
            {
                RootPath = RootPath,
                PublicBaseUrl = "https://media.test/media",
                MaxFileSizeMb = 1,
            }),
            new TestWebHostEnvironment(RootPath));
    }

    public ExerciseMediaDraftService CreateDraftService(
        AppDbContext context,
        IEnumerable<IExerciseMediaGenerationProvider>? providers = null,
        bool generationEnabled = false,
        string providerName = "Fake")
    {
        return new ExerciseMediaDraftService(
            context,
            new ExerciseMediaPromptBuilderService(),
            CreateStorageService(),
            providers ?? [],
            Options.Create(new MediaGenerationOptions
            {
                Enabled = generationEnabled,
                Provider = providerName,
            }));
    }

    public async Task<ExerciseCatalogItem> CreateExerciseAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var item = new ExerciseCatalogItem
        {
            Source = "test-provider",
            ExternalId = "exercise-42",
            Name = "Barbell back squat",
            Slug = $"barbell-back-squat-{Guid.NewGuid():N}",
            Instructions = "Keep a neutral spine and control the descent.",
            PrimaryMuscle = "Quadriceps",
            SecondaryMuscles = "Glutes,Hamstrings",
            Equipment = "Barbell",
            VideoUrl = "https://provider.example/videos/back-squat.mp4",
            ThumbnailUrl = "https://provider.example/images/back-squat.jpg",
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.ExerciseCatalogItems.Add(item);
        await context.SaveChangesAsync();
        return item;
    }

    public async Task<ExerciseMediaDraft> CreateDraftAsync(
        AppDbContext context,
        ExerciseCatalogItem exercise,
        string status = ExerciseMediaDraftStatuses.NeedsReview)
    {
        var draft = await CreateDraftService(context).CreateDraftAsync(
            exercise.Id,
            new CreateExerciseMediaDraftRequest(),
            createdByUserId: 7);

        Assert.NotNull(draft);
        var entity = await context.ExerciseMediaDrafts.SingleAsync(item => item.Id == draft.Id);
        entity.Status = status;
        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task<string> AttachPrivateVideoAsync(
        AppDbContext context,
        ExerciseMediaDraft draft)
    {
        await using var content = new MemoryStream(TestMediaFiles.ValidMp4());
        var stored = await CreateStorageService().SaveGeneratedVideoAsync(
            draft.ExerciseCatalogItemId,
            draft.Id,
            content,
            "video/mp4",
            "video.mp4");

        draft.GeneratedVideoUrl = stored.PreviewUrl;
        draft.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return stored.PreviewUrl;
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public TestWebHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        WebRootPath = contentRootPath;
        WebRootFileProvider = new PhysicalFileProvider(contentRootPath);
    }

    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "backend.Tests";
    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}

internal static class TestMediaFiles
{
    public static byte[] ValidMp4() =>
    [
        0x00, 0x00, 0x00, 0x18,
        (byte)'f', (byte)'t', (byte)'y', (byte)'p',
        (byte)'i', (byte)'s', (byte)'o', (byte)'m',
        0x00, 0x00, 0x00, 0x00,
        (byte)'i', (byte)'s', (byte)'o', (byte)'m',
        (byte)'m', (byte)'p', (byte)'4', (byte)'2',
    ];
}
