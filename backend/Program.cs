using System.Text;
using backend.Authorization;
using backend.Configuration;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var enableHttpsRedirection = builder.Configuration.GetValue("Http:UseHttpsRedirection", false);
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var wgerOptions = builder.Configuration.GetSection(WgerOptions.SectionName).Get<WgerOptions>() ?? new WgerOptions();
var exerciseMediaEnrichmentOptions = builder.Configuration
    .GetSection(ExerciseMediaEnrichmentOptions.SectionName)
    .Get<ExerciseMediaEnrichmentOptions>() ?? new ExerciseMediaEnrichmentOptions();
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
builder.Services.Configure<WgerOptions>(builder.Configuration.GetSection(WgerOptions.SectionName));
builder.Services.Configure<ExerciseMediaEnrichmentOptions>(builder.Configuration.GetSection(ExerciseMediaEnrichmentOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddSingleton<PasswordHasher<AppUser>>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<LegacyDataMigrationService>();
builder.Services.AddScoped<TrainingIntelligenceService>();
builder.Services.AddScoped<ProgressiveOverloadService>();
builder.Services.AddScoped<ExerciseCatalogService>();
builder.Services.AddScoped<ExerciseCatalogSeedService>();
builder.Services.AddScoped<ExerciseCatalogMediaEnrichmentService>();
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
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var legacyDataMigrationService = scope.ServiceProvider.GetRequiredService<LegacyDataMigrationService>();
    var exerciseCatalogSeedService = scope.ServiceProvider.GetRequiredService<ExerciseCatalogSeedService>();

    const int maxMigrationAttempts = 10;

    for (var attempt = 1; attempt <= maxMigrationAttempts; attempt++)
    {
        try
        {
            dbContext.Database.Migrate();
            await legacyDataMigrationService.MigrateAsync();
            await exerciseCatalogSeedService.SeedAsync();
            break;
        }
        catch (Exception exception) when (attempt < maxMigrationAttempts)
        {
            logger.LogWarning(
                exception,
                "Database migration attempt {Attempt} of {MaxAttempts} failed. Retrying in 5 seconds.",
                attempt,
                maxMigrationAttempts);

            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
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

app.Run();
