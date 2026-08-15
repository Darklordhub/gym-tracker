using System.Net;
using System.Text;
using System.Text.Json;
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

public class AiWorkoutGeneratorServiceProviderIntegrationTests
{
    [Fact]
    public async Task OpenAiResponsesApiOutput_ReturnsRehydratedPlanAndRecordsSucceeded()
    {
        await using var database = await WorkoutGeneratorTestDatabase.CreateAsync();
        var catalogExercise = CreateExercise("Original barbell squat", "Quadriceps", isActive: true);
        catalogExercise.LocalNameOverride = "STRIDE catalog squat";
        catalogExercise.LocalInstructionsOverride = "Use the STRIDE-approved squat setup.";
        catalogExercise.LocalThumbnailUrlOverride = "/media/catalog-squat-thumb.jpg";
        catalogExercise.LocalVideoUrlOverride = "/media/catalog-squat.mp4";
        database.Context.ExerciseCatalogItems.AddRange(
            catalogExercise,
            CreateExercise("Cable row", "Back", isActive: true));
        await database.Context.SaveChangesAsync();

        var providerOutput = JsonSerializer.Serialize(new
        {
            sections = new[]
            {
                new
                {
                    name = "Strength block",
                    exercises = new[]
                    {
                        new
                        {
                            exerciseCatalogItemId = catalogExercise.Id,
                            sets = 4,
                            reps = "5 reps",
                            restSeconds = 180,
                            suggestedWeight = "80 kg",
                            rationale = "Use the bounded catalog selection.",
                        },
                    },
                },
            },
        });
        var providerEnvelope = JsonSerializer.Serialize(new
        {
            id = "resp_integration_test",
            status = "completed",
            output = new object[]
            {
                new { id = "rs_test", type = "reasoning", summary = Array.Empty<object>() },
                new
                {
                    id = "msg_test",
                    type = "message",
                    status = "completed",
                    role = "assistant",
                    content = new[] { new { type = "output_text", text = providerOutput } },
                },
            },
            error = (object?)null,
            incomplete_details = (object?)null,
        });
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(providerEnvelope, Encoding.UTF8, "application/json"),
        }));
        var provider = CreateOpenAiProvider(handler);
        var service = database.CreateService(
            new FakeCandidateSelector([CandidateFrom(catalogExercise)]),
            provider,
            enabled: true);

        var plan = await service.GenerateAsync("1", FullBodyRequest(), CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(["Warm-up", "Strength block", "Cooldown"], plan.Sections.Select(section => section.Name));
        Assert.DoesNotContain(plan.Notes, note => note == "Generated with the local STRIDE planner.");
        var exercise = Assert.Single(plan.Sections.Single(section => section.Name == "Strength block").Exercises);
        Assert.Equal(catalogExercise.Id, exercise.ExerciseCatalogItemId);
        Assert.Equal("STRIDE catalog squat", exercise.Name);
        Assert.Equal("Use the STRIDE-approved squat setup.", exercise.Instructions);
        Assert.Equal("/media/catalog-squat-thumb.jpg", exercise.ThumbnailUrl);
        Assert.Equal("/media/catalog-squat.mp4", exercise.VideoUrl);
        var attempt = Assert.Single(await database.Context.AiWorkoutGenerationAttempts.ToListAsync());
        Assert.Equal(AiWorkoutGenerationAttemptStatuses.Succeeded, attempt.Status);
        Assert.Equal(OpenAiWorkoutPlanProvider.Name, attempt.Provider);
        Assert.Equal("workout-plan-v2", attempt.PromptVersion);
        Assert.Null(attempt.ErrorCategory);
        Assert.Null(attempt.SafeErrorMessage);
    }

    [Fact]
    public async Task EnabledProvider_UsesOnlyBoundedCandidatesAndRehydratesCatalogExerciseData()
    {
        await using var database = await WorkoutGeneratorTestDatabase.CreateAsync();
        var catalogExercise = CreateExercise("Original barbell squat", "Quadriceps", isActive: true);
        catalogExercise.LocalNameOverride = "STRIDE catalog squat";
        catalogExercise.LocalInstructionsOverride = "Use the STRIDE-approved squat setup.";
        catalogExercise.LocalThumbnailUrlOverride = "/media/catalog-squat-thumb.jpg";
        catalogExercise.LocalVideoUrlOverride = "/media/catalog-squat.mp4";
        database.Context.ExerciseCatalogItems.AddRange(
            catalogExercise,
            CreateExercise("Cable row", "Back", isActive: true),
            CreateExercise("Inactive press", "Chest", isActive: false));
        await database.Context.SaveChangesAsync();

        var selector = new FakeCandidateSelector(
        [
            CandidateFrom(catalogExercise),
        ]);
        var provider = new FakeProvider(new AiWorkoutPlanProviderResult
        {
            Sections =
            [
                new AiWorkoutPlanProviderSection
                {
                    Name = "Strength block",
                    Exercises =
                    [
                        new AiWorkoutPlanProviderExercise
                        {
                            ExerciseCatalogItemId = catalogExercise.Id,
                            Sets = 4,
                            Reps = "5 reps",
                            RestSeconds = 180,
                            SuggestedWeight = "80 kg",
                            Rationale = "This must never reach the client.",
                        },
                    ],
                },
            ],
        }, async () =>
        {
            var reservedAttempt = Assert.Single(await database.Context.AiWorkoutGenerationAttempts.ToListAsync());
            Assert.Equal(AiWorkoutGenerationAttemptStatuses.Reserved, reservedAttempt.Status);
        });
        var service = database.CreateService(selector, provider, enabled: true);

        var plan = await service.GenerateAsync("1", FullBodyRequest(), CancellationToken.None);

        Assert.Equal(1, selector.CallCount);
        Assert.Equal(1, provider.CallCount);
        var providerRequest = Assert.IsType<AiWorkoutPlanProviderRequest>(provider.LastRequest);
        Assert.Single(providerRequest.CandidateExercises);
        Assert.Equal(catalogExercise.Id, providerRequest.CandidateExercises[0].ExerciseCatalogItemId);
        Assert.Equal(45, providerRequest.DurationMinutes);
        Assert.Equal(5, providerRequest.RecommendedMainExerciseCount);
        Assert.Equal(8, providerRequest.MaximumMainExerciseCount);
        Assert.False(providerRequest.IncludeWarmup);
        Assert.False(providerRequest.IncludeCooldown);

        Assert.Equal(["Warm-up", "Strength block", "Cooldown"], plan.Sections.Select(section => section.Name));
        var exercise = Assert.Single(plan.Sections.Single(section => section.Name == "Strength block").Exercises);
        Assert.Equal(catalogExercise.Id, exercise.ExerciseCatalogItemId);
        Assert.Equal("STRIDE catalog squat", exercise.Name);
        Assert.Equal("Use the STRIDE-approved squat setup.", exercise.Instructions);
        Assert.Equal("/media/catalog-squat-thumb.jpg", exercise.ThumbnailUrl);
        Assert.Equal("/media/catalog-squat.mp4", exercise.VideoUrl);
        Assert.Equal(4, exercise.Sets);
        Assert.Equal("5 reps", exercise.Reps);
        Assert.Equal(180, exercise.RestSeconds);
        Assert.Equal("80 kg", exercise.SuggestedWeight);
        Assert.Contains(plan.Notes, note => note.Contains("STRIDE catalog candidates", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Notes, note => note.Contains("never reach", StringComparison.Ordinal));
        var attempt = Assert.Single(await database.Context.AiWorkoutGenerationAttempts.ToListAsync());
        Assert.Equal(AiWorkoutGenerationAttemptStatuses.Succeeded, attempt.Status);
        Assert.Equal(1, attempt.CandidateExerciseCount);
        Assert.Equal(1, attempt.SelectedExerciseCount);
        Assert.Equal("Fake", attempt.Provider);
        Assert.Equal("fake-model", attempt.Model);
    }

    [Fact]
    public async Task EnabledProvider_RejectsInactiveCatalogIdsAndFallsBackLocally()
    {
        await using var database = await WorkoutGeneratorTestDatabase.CreateAsync();
        var active = CreateExercise("Barbell back squat", "Quadriceps", isActive: true);
        var inactive = CreateExercise("Inactive leg press", "Quadriceps", isActive: false);
        database.Context.ExerciseCatalogItems.AddRange(active, inactive, CreateExercise("Cable row", "Back", isActive: true));
        await database.Context.SaveChangesAsync();

        var selector = new FakeCandidateSelector([CandidateFrom(active), CandidateFrom(inactive)]);
        var provider = new FakeProvider(ResultFor(inactive.Id));
        var service = database.CreateService(selector, provider, enabled: true);

        var plan = await service.GenerateAsync("1", FullBodyRequest(), CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Contains(plan.Notes, note => note == "Generated with the local STRIDE planner.");
        Assert.DoesNotContain(plan.Sections.SelectMany(section => section.Exercises), exercise => exercise.ExerciseCatalogItemId == inactive.Id);
        var attempt = Assert.Single(await database.Context.AiWorkoutGenerationAttempts.ToListAsync());
        Assert.Equal(AiWorkoutProviderFailureCategories.OpenAiInactiveExerciseId, attempt.ErrorCategory);
        Assert.Equal("The AI workout provider did not produce a usable workout plan.", attempt.SafeErrorMessage);
    }

    [Fact]
    public async Task EnabledProvider_RejectsIdsOutsideCandidatesAndFallsBackLocally()
    {
        await using var database = await WorkoutGeneratorTestDatabase.CreateAsync();
        var active = CreateExercise("Barbell back squat", "Quadriceps", isActive: true);
        database.Context.ExerciseCatalogItems.AddRange(active, CreateExercise("Cable row", "Back", isActive: true));
        await database.Context.SaveChangesAsync();

        var provider = new FakeProvider(ResultFor(987654));
        var plan = await database.CreateService(new FakeCandidateSelector([CandidateFrom(active)]), provider, enabled: true)
            .GenerateAsync("1", FullBodyRequest(), CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Contains(plan.Notes, note => note == "Generated with the local STRIDE planner.");
        var attempt = Assert.Single(await database.Context.AiWorkoutGenerationAttempts.ToListAsync());
        Assert.Equal(AiWorkoutProviderFailureCategories.OpenAiUnknownExerciseId, attempt.ErrorCategory);
    }

    [Fact]
    public async Task EnabledProvider_RejectsExcludedCatalogExercisesAndFallsBackLocally()
    {
        await using var database = await WorkoutGeneratorTestDatabase.CreateAsync();
        var active = CreateExercise("Barbell back squat", "Quadriceps", isActive: true);
        database.Context.ExerciseCatalogItems.AddRange(active, CreateExercise("Cable row", "Back", isActive: true));
        await database.Context.SaveChangesAsync();

        var request = FullBodyRequest();
        request.ExcludedExercises = [active.Name];
        var plan = await database.CreateService(
                new FakeCandidateSelector([CandidateFrom(active)]),
                new FakeProvider(ResultFor(active.Id)),
                enabled: true)
            .GenerateAsync("1", request, CancellationToken.None);

        Assert.Contains(plan.Notes, note => note == "Generated with the local STRIDE planner.");
    }

    [Fact]
    public async Task EnabledProvider_RejectsDuplicateExercisesAndFallsBackLocally()
    {
        await using var database = await WorkoutGeneratorTestDatabase.CreateAsync();
        var active = CreateExercise("Barbell back squat", "Quadriceps", isActive: true);
        database.Context.ExerciseCatalogItems.AddRange(active, CreateExercise("Cable row", "Back", isActive: true));
        await database.Context.SaveChangesAsync();

        var selector = new FakeCandidateSelector([CandidateFrom(active)]);
        var provider = new FakeProvider(new AiWorkoutPlanProviderResult
        {
            Sections =
            [
                new AiWorkoutPlanProviderSection
                {
                    Name = "Main",
                    Exercises =
                    [
                        ProviderExercise(active.Id),
                        ProviderExercise(active.Id),
                    ],
                },
            ],
        });

        var plan = await database.CreateService(selector, provider, enabled: true)
            .GenerateAsync("1", FullBodyRequest(), CancellationToken.None);

        Assert.Contains(plan.Notes, note => note == "Generated with the local STRIDE planner.");
        var attempt = Assert.Single(await database.Context.AiWorkoutGenerationAttempts.ToListAsync());
        Assert.Equal(AiWorkoutProviderFailureCategories.OpenAiDuplicateExerciseId, attempt.ErrorCategory);
    }

    [Fact]
    public async Task EnabledProvider_RejectsExerciseCountAboveDurationCapAndRecordsCategory()
    {
        await using var database = await WorkoutGeneratorTestDatabase.CreateAsync();
        var catalogExercises = Enumerable.Range(1, 10)
            .Select(index => CreateExercise($"Test exercise {index}", "Quadriceps", isActive: true))
            .ToList();
        database.Context.ExerciseCatalogItems.AddRange(catalogExercises);
        await database.Context.SaveChangesAsync();

        var provider = new FakeProvider(new AiWorkoutPlanProviderResult
        {
            Sections =
            [
                new AiWorkoutPlanProviderSection
                {
                    Name = "Main",
                    Exercises = catalogExercises.Select(exercise => ProviderExercise(exercise.Id)).ToList(),
                },
            ],
        });
        var request = FullBodyRequest();
        request.DurationMinutes = 60;
        var plan = await database.CreateService(
                new FakeCandidateSelector(catalogExercises.Select(CandidateFrom).ToList()),
                provider,
                enabled: true)
            .GenerateAsync("1", request, CancellationToken.None);

        Assert.Contains(plan.Notes, note => note == "Generated with the local STRIDE planner.");
        Assert.Equal(60, provider.LastRequest?.DurationMinutes);
        Assert.Equal(6, provider.LastRequest?.RecommendedMainExerciseCount);
        Assert.Equal(9, provider.LastRequest?.MaximumMainExerciseCount);
        var attempt = Assert.Single(await database.Context.AiWorkoutGenerationAttempts.ToListAsync());
        Assert.Equal(AiWorkoutGenerationAttemptStatuses.FallbackSucceeded, attempt.Status);
        Assert.Equal(AiWorkoutProviderFailureCategories.OpenAiDurationExerciseCapExceeded, attempt.ErrorCategory);
    }

    [Fact]
    public async Task EnabledProvider_FailureFallsBackWithoutLeakingProviderText()
    {
        await using var database = await WorkoutGeneratorTestDatabase.CreateAsync();
        var active = CreateExercise("Barbell back squat", "Quadriceps", isActive: true);
        database.Context.ExerciseCatalogItems.AddRange(active, CreateExercise("Cable row", "Back", isActive: true));
        await database.Context.SaveChangesAsync();

        const string rawProviderText = "raw-openai-response: internal details";
        var provider = new FakeProvider(new AiWorkoutPlanProviderException(rawProviderText));
        var service = database.CreateService(new FakeCandidateSelector([CandidateFrom(active)]), provider, enabled: true);

        var plan = await service.GenerateAsync("1", FullBodyRequest(), CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Contains(plan.Notes, note => note == "Generated with the local STRIDE planner.");
        Assert.DoesNotContain(plan.Notes, note => note.Contains(rawProviderText, StringComparison.Ordinal));
        Assert.NotEmpty(plan.Sections);
        var attempt = Assert.Single(await database.Context.AiWorkoutGenerationAttempts.ToListAsync());
        Assert.Equal(AiWorkoutGenerationAttemptStatuses.FallbackSucceeded, attempt.Status);
        Assert.Equal("ProviderFailure", attempt.ErrorCategory);
        Assert.DoesNotContain(rawProviderText, attempt.SafeErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnabledProvider_RateLimitedUserGetsLocalFallbackWithoutCallingProvider()
    {
        await using var database = await WorkoutGeneratorTestDatabase.CreateAsync();
        var active = CreateExercise("Barbell back squat", "Quadriceps", isActive: true);
        database.Context.ExerciseCatalogItems.AddRange(active, CreateExercise("Cable row", "Back", isActive: true));
        database.Context.AiWorkoutGenerationAttempts.Add(new AiWorkoutGenerationAttempt
        {
            UserId = 1,
            Status = AiWorkoutGenerationAttemptStatuses.Succeeded,
            Provider = "Fake",
            Model = "fake-model",
            RequestHash = new string('a', 64),
            CandidateExerciseCount = 1,
            SelectedExerciseCount = 1,
            PromptVersion = "workout-plan-v1",
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-4),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        });
        await database.Context.SaveChangesAsync();

        var provider = new FakeProvider(ResultFor(active.Id));
        var plan = await database.CreateService(
                new FakeCandidateSelector([CandidateFrom(active)]),
                provider,
                enabled: true,
                generationOptions: new AiWorkoutGenerationOptions
                {
                    Enabled = true,
                    MaxCandidateExercises = 60,
                    MaxGenerationsPerUserPerHour = 1,
                    MaxGenerationsPerUserPerDay = 5,
                    MaxGlobalGenerationsPerDay = 50,
                    CooldownSeconds = 0,
                })
            .GenerateAsync("1", FullBodyRequest(), CancellationToken.None);

        Assert.Equal(0, provider.CallCount);
        Assert.Contains(plan.Notes, note => note.Contains("temporarily limited", StringComparison.Ordinal));
        Assert.Contains(
            await database.Context.AiWorkoutGenerationAttempts.ToListAsync(),
            attempt => attempt.Status == AiWorkoutGenerationAttemptStatuses.RateLimited);
    }

    private static AiWorkoutGenerateRequest FullBodyRequest() => new()
    {
        Goal = "strength",
        PreferredWorkoutType = "full body",
        DurationMinutes = 45,
        FitnessLevel = "intermediate",
        IncludeWarmup = true,
        IncludeCooldown = true,
    };

    private static AiWorkoutPlanProviderResult ResultFor(int exerciseCatalogItemId) => new()
    {
        Sections =
        [
            new AiWorkoutPlanProviderSection
            {
                Name = "Main",
                Exercises = [ProviderExercise(exerciseCatalogItemId)],
            },
        ],
    };

    private static AiWorkoutPlanProviderExercise ProviderExercise(int exerciseCatalogItemId) => new()
    {
        ExerciseCatalogItemId = exerciseCatalogItemId,
        Sets = 3,
        Reps = "8 reps",
        RestSeconds = 90,
    };

    private static OpenAiWorkoutPlanProvider CreateOpenAiProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/v1/"),
            Timeout = TimeSpan.FromSeconds(45),
        };
        var generationOptions = new AiWorkoutGenerationOptions
        {
            Enabled = true,
            Provider = OpenAiWorkoutPlanProvider.Name,
            MaxCandidateExercises = 60,
            TimeoutSeconds = 45,
        };

        return new OpenAiWorkoutPlanProvider(
            httpClient,
            Options.Create(generationOptions),
            Options.Create(new OpenAiWorkoutGenerationOptions
            {
                ApiKey = "test-only-key",
                WorkoutModel = "gpt-5-mini",
                WorkoutMaxOutputTokens = 2000,
            }),
            NullLogger<OpenAiWorkoutPlanProvider>.Instance);
    }

    private static AiWorkoutCandidate CandidateFrom(ExerciseCatalogItem item) => new()
    {
        ExerciseCatalogItemId = item.Id,
        Name = item.Name,
        Category = "compound",
        TargetMuscle = item.PrimaryMuscle,
        Equipment = item.Equipment,
        Difficulty = item.Difficulty,
        FocusGroup = "lower",
        MovementPattern = "squat",
    };

    private static ExerciseCatalogItem CreateExercise(string name, string primaryMuscle, bool isActive)
    {
        var now = DateTime.UtcNow;
        return new ExerciseCatalogItem
        {
            Source = "test",
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            Instructions = $"Perform {name} with control.",
            PrimaryMuscle = primaryMuscle,
            SecondaryMuscles = "Glutes",
            Equipment = "Barbell",
            Difficulty = "Intermediate",
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private sealed class FakeCandidateSelector(IReadOnlyList<AiWorkoutCandidate> candidates) : IAiWorkoutCandidateSelector
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<AiWorkoutCandidate>> SelectCandidatesAsync(
            string userId,
            AiWorkoutGenerateRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(candidates);
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return handler(request, cancellationToken);
        }
    }

    private sealed class FakeProvider : IAiWorkoutPlanProvider
    {
        private readonly AiWorkoutPlanProviderResult? _result;
        private readonly Exception? _exception;
        private readonly Func<Task>? _onGenerate;

        public FakeProvider(AiWorkoutPlanProviderResult result, Func<Task>? onGenerate = null)
        {
            _result = result;
            _onGenerate = onGenerate;
        }

        public FakeProvider(Exception exception) => _exception = exception;

        public string ProviderName => "Fake";
        public string? ModelName => "fake-model";
        public int CallCount { get; private set; }
        public AiWorkoutPlanProviderRequest? LastRequest { get; private set; }

        public void ValidateConfiguration()
        {
        }

        public async Task<AiWorkoutPlanProviderResult> GeneratePlanAsync(
            AiWorkoutPlanProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            if (_onGenerate is not null)
            {
                await _onGenerate();
            }

            if (_exception is not null)
            {
                throw _exception;
            }

            return _result!;
        }
    }

    private sealed class WorkoutGeneratorTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private WorkoutGeneratorTestDatabase(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<WorkoutGeneratorTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new WorkoutGeneratorTestDatabase(connection, context);
        }

        public AiWorkoutGeneratorService CreateService(
            IAiWorkoutCandidateSelector selector,
            IAiWorkoutPlanProvider provider,
            bool enabled,
            AiWorkoutGenerationOptions? generationOptions = null)
        {
            var options = generationOptions ?? new AiWorkoutGenerationOptions
            {
                Enabled = enabled,
                MaxCandidateExercises = 60,
            };

            return new AiWorkoutGeneratorService(
                Context,
                selector,
                provider,
                new AiWorkoutGenerationLimiter(Context, Options.Create(options), TimeProvider.System),
                Options.Create(options),
                NullLogger<AiWorkoutGeneratorService>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
