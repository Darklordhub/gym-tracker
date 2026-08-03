using System.Text;
using backend.Authorization;
using backend.Configuration;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var enableHttpsRedirection = builder.Configuration.GetValue("Http:UseHttpsRedirection", false);
var appOptions = builder.Configuration.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions();
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var wgerOptions = builder.Configuration.GetSection(WgerOptions.SectionName).Get<WgerOptions>() ?? new WgerOptions();
var nutritionOptions = builder.Configuration.GetSection(NutritionOptions.SectionName).Get<NutritionOptions>() ?? new NutritionOptions();
var exerciseMediaEnrichmentOptions = builder.Configuration
    .GetSection(ExerciseMediaEnrichmentOptions.SectionName)
    .Get<ExerciseMediaEnrichmentOptions>() ?? new ExerciseMediaEnrichmentOptions();
var exerciseMediaStorageOptions = builder.Configuration
    .GetSection(ExerciseMediaStorageOptions.SectionName)
    .Get<ExerciseMediaStorageOptions>() ?? new ExerciseMediaStorageOptions();
var aiWorkoutGenerationOptions = builder.Configuration
    .GetSection(AiWorkoutGenerationOptions.SectionName)
    .Get<AiWorkoutGenerationOptions>() ?? new AiWorkoutGenerationOptions();
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray()
    ?? Array.Empty<string>();

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. Set ConnectionStrings__DefaultConnection.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "JWT signing key is not configured or too short. Set Jwt__SigningKey to a random string at least 32 characters long.");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<WgerOptions>(builder.Configuration.GetSection(WgerOptions.SectionName));
builder.Services.Configure<NutritionOptions>(builder.Configuration.GetSection(NutritionOptions.SectionName));
builder.Services.Configure<ExerciseMediaEnrichmentOptions>(builder.Configuration.GetSection(ExerciseMediaEnrichmentOptions.SectionName));
builder.Services.Configure<ExerciseMediaStorageOptions>(builder.Configuration.GetSection(ExerciseMediaStorageOptions.SectionName));
builder.Services.Configure<MediaGenerationOptions>(builder.Configuration.GetSection(MediaGenerationOptions.SectionName));
builder.Services.Configure<OpenAiVideoGenerationOptions>(builder.Configuration.GetSection(OpenAiVideoGenerationOptions.SectionName));
builder.Services.Configure<AiWorkoutGenerationOptions>(builder.Configuration.GetSection(AiWorkoutGenerationOptions.SectionName));
builder.Services.Configure<OpenAiWorkoutGenerationOptions>(builder.Configuration.GetSection(OpenAiWorkoutGenerationOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddSingleton<ApplicationInitializationState>();
builder.Services.AddSingleton<PasswordHasher<AppUser>>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<LegacyDataMigrationService>();
builder.Services.AddScoped<TrainingIntelligenceService>();
builder.Services.AddScoped<ProgressiveOverloadService>();
builder.Services.AddScoped<ExerciseCatalogService>();
builder.Services.AddScoped<ExerciseCatalogSeedService>();
builder.Services.AddScoped<IAiWorkoutGeneratorService, AiWorkoutGeneratorService>();
builder.Services.AddHttpClient<OpenAiWorkoutPlanProvider>(httpClient =>
{
    var timeoutSeconds = aiWorkoutGenerationOptions.TimeoutSeconds is >= 5 and <= 120
        ? aiWorkoutGenerationOptions.TimeoutSeconds
        : AiWorkoutGenerationOptions.DefaultTimeoutSeconds;

    httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
    httpClient.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
})
.RedactLoggedHeaders(static _ => true);
builder.Services.AddScoped<IAiWorkoutPlanProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<OpenAiWorkoutPlanProvider>());
builder.Services.AddScoped<INutritionService, NutritionService>();
builder.Services.AddScoped<INutritionRollupService, NutritionRollupService>();
builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<ExerciseCatalogMediaEnrichmentService>();
builder.Services.AddScoped<ExerciseMediaPromptBuilderService>();
builder.Services.AddScoped<ExerciseMediaDraftService>();
builder.Services.AddSingleton<ExerciseMediaStorageService>();
builder.Services.AddHttpClient<OpenAiExerciseMediaGenerationProvider>(httpClient =>
{
    httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
    httpClient.Timeout = Timeout.InfiniteTimeSpan;
})
.RedactLoggedHeaders(static _ => true);
builder.Services.AddScoped<IExerciseMediaGenerationProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<OpenAiExerciseMediaGenerationProvider>());
builder.Services.AddHttpClient<UsdaNutritionProvider>(httpClient =>
{
    httpClient.BaseAddress = new Uri(
        nutritionOptions.UsdaBaseUrl.EndsWith('/')
            ? nutritionOptions.UsdaBaseUrl
            : $"{nutritionOptions.UsdaBaseUrl}/");
    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    httpClient.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<INutritionProvider>(serviceProvider => serviceProvider.GetRequiredService<UsdaNutritionProvider>());
builder.Services.AddSingleton<IExerciseMediaHostResolver, ExerciseMediaHostResolver>();
builder.Services.AddHttpClient<ExerciseMediaUrlValidationService>(httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(exerciseMediaEnrichmentOptions.UrlValidationTimeoutSeconds, 3, 60));
})
.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    ExerciseMediaSafeHttpHandler.Create(
        serviceProvider.GetRequiredService<IExerciseMediaHostResolver>(),
        TimeSpan.FromSeconds(Math.Clamp(exerciseMediaEnrichmentOptions.UrlValidationTimeoutSeconds, 3, 60))));
builder.Services.AddHttpClient<IWgerExerciseCatalogSyncService, WgerExerciseCatalogSyncService>(httpClient =>
{
    httpClient.BaseAddress = new Uri(wgerOptions.BaseUrl.EndsWith('/') ? wgerOptions.BaseUrl : $"{wgerOptions.BaseUrl}/");
    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    httpClient.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<WgerExerciseMediaProvider>(httpClient =>
{
    httpClient.BaseAddress = new Uri(wgerOptions.BaseUrl.EndsWith('/') ? wgerOptions.BaseUrl : $"{wgerOptions.BaseUrl}/");
    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    httpClient.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IExerciseMediaProvider>(serviceProvider => serviceProvider.GetRequiredService<WgerExerciseMediaProvider>());
builder.Services.AddHttpClient<ExerciseDbExerciseMediaProvider>(httpClient =>
{
    if (!string.IsNullOrWhiteSpace(exerciseMediaEnrichmentOptions.ExerciseDb.BaseUrl))
    {
        httpClient.BaseAddress = new Uri(
            exerciseMediaEnrichmentOptions.ExerciseDb.BaseUrl.EndsWith('/')
                ? exerciseMediaEnrichmentOptions.ExerciseDb.BaseUrl
                : $"{exerciseMediaEnrichmentOptions.ExerciseDb.BaseUrl}/");
    }

    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

    foreach (var header in exerciseMediaEnrichmentOptions.ExerciseDb.RequestHeaders)
    {
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
    }

    httpClient.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddScoped<IExerciseMediaProvider>(serviceProvider => serviceProvider.GetRequiredService<ExerciseDbExerciseMediaProvider>());
builder.Services.AddHttpClient<FreeExerciseDbMediaProvider>(httpClient =>
{
    if (!string.IsNullOrWhiteSpace(exerciseMediaEnrichmentOptions.FreeExerciseDb.BaseUrl))
    {
        httpClient.BaseAddress = new Uri(
            exerciseMediaEnrichmentOptions.FreeExerciseDb.BaseUrl.EndsWith('/')
                ? exerciseMediaEnrichmentOptions.FreeExerciseDb.BaseUrl
                : $"{exerciseMediaEnrichmentOptions.FreeExerciseDb.BaseUrl}/");
    }

    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

    foreach (var header in exerciseMediaEnrichmentOptions.FreeExerciseDb.RequestHeaders)
    {
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
    }

    httpClient.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddScoped<IExerciseMediaProvider>(serviceProvider => serviceProvider.GetRequiredService<FreeExerciseDbMediaProvider>());
builder.Services.AddHostedService<ExerciseCatalogMediaEnrichmentBackgroundService>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(AppRoles.Admin));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = app.Logger;
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var legacyDataMigrationService = scope.ServiceProvider.GetRequiredService<LegacyDataMigrationService>();
    var exerciseCatalogSeedService = scope.ServiceProvider.GetRequiredService<ExerciseCatalogSeedService>();
    var initializationState = app.Services.GetRequiredService<ApplicationInitializationState>();

    const int maxMigrationAttempts = 10;
    var initializationSucceeded = false;

    for (var attempt = 1; attempt <= maxMigrationAttempts; attempt++)
    {
        try
        {
            dbContext.Database.Migrate();
            await legacyDataMigrationService.MigrateAsync();
            await exerciseCatalogSeedService.SeedAsync();
            initializationSucceeded = true;
            initializationState.MarkSucceeded();
            logger.LogInformation(
                "Database migrations and startup data initialization completed successfully on attempt {Attempt}.",
                attempt);
            break;
        }
        catch (Exception exception) when (attempt < maxMigrationAttempts)
        {
            logger.LogWarning(
                exception,
                "Database migration attempt {Attempt} of {MaxAttempts} failed. Retrying in 5 seconds.",
                attempt,
                maxMigrationAttempts);

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception)
        {
            initializationState.MarkFailed("Database startup initialization failed.");
            logger.LogError(
                exception,
                "Database migration and startup data initialization failed on final attempt {Attempt} of {MaxAttempts}.",
                attempt,
                maxMigrationAttempts);

            if (!appOptions.AllowStartupWithMigrationFailure)
            {
                throw;
            }

            logger.LogWarning(
                "Application startup will continue without successful database initialization because App__AllowStartupWithMigrationFailure=true. Readiness will report unhealthy until initialization succeeds.");
        }
    }

    if (!initializationSucceeded)
    {
        logger.LogWarning("Database startup initialization did not complete successfully.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (allowedOrigins.Length > 0)
{
    app.UseCors("frontend");
}

if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
}

if (string.IsNullOrWhiteSpace(exerciseMediaStorageOptions.RootPath))
{
    throw new InvalidOperationException("MediaStorage:RootPath must be configured.");
}

if (exerciseMediaStorageOptions.MaxFileSizeMb is < 1 or > 1024)
{
    throw new InvalidOperationException("MediaStorage:MaxFileSizeMb must be between 1 and 1024.");
}

if (!Uri.TryCreate(exerciseMediaStorageOptions.PublicBaseUrl, UriKind.Absolute, out var mediaStoragePublicUri) ||
    (mediaStoragePublicUri.Scheme != Uri.UriSchemeHttp && mediaStoragePublicUri.Scheme != Uri.UriSchemeHttps) ||
    mediaStoragePublicUri.Query.Length > 0 ||
    mediaStoragePublicUri.Fragment.Length > 0)
{
    throw new InvalidOperationException(
        "MediaStorage:PublicBaseUrl must be an absolute HTTP or HTTPS URL without a query or fragment.");
}

var mediaStorageRootPath = Path.GetFullPath(Path.IsPathRooted(exerciseMediaStorageOptions.RootPath)
    ? exerciseMediaStorageOptions.RootPath
    : Path.Combine(app.Environment.ContentRootPath, exerciseMediaStorageOptions.RootPath));
var mediaStorageRequestPath = mediaStoragePublicUri.AbsolutePath.TrimEnd('/');
if (string.IsNullOrWhiteSpace(mediaStorageRequestPath) || mediaStorageRequestPath == "/")
{
    throw new InvalidOperationException("MediaStorage:PublicBaseUrl must include a request path.");
}

var publicMediaRootPath = Path.Combine(mediaStorageRootPath, "public");
Directory.CreateDirectory(publicMediaRootPath);
var mediaContentTypeProvider = new FileExtensionContentTypeProvider();
mediaContentTypeProvider.Mappings.Clear();
mediaContentTypeProvider.Mappings[".mp4"] = "video/mp4";
mediaContentTypeProvider.Mappings[".jpg"] = "image/jpeg";
mediaContentTypeProvider.Mappings[".jpeg"] = "image/jpeg";
mediaContentTypeProvider.Mappings[".png"] = "image/png";
mediaContentTypeProvider.Mappings[".webp"] = "image/webp";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(publicMediaRootPath),
    RequestPath = mediaStorageRequestPath,
    ContentTypeProvider = mediaContentTypeProvider,
    OnPrepareResponse = context => context.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff"),
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/healthz", async (AppDbContext dbContext) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "ok" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});
app.MapGet("/readyz", async (AppDbContext dbContext, ApplicationInitializationState initializationState) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync();
    if (!canConnect)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                database = "unavailable",
                initialization = initializationState.Status.ToString().ToLowerInvariant(),
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!initializationState.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                database = "ok",
                initialization = initializationState.Status.ToString().ToLowerInvariant(),
                message = initializationState.FailureMessage ?? "Database startup initialization has not completed successfully.",
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "ok",
        database = "ok",
        initialization = initializationState.Status.ToString().ToLowerInvariant(),
    });
});

app.Run();
