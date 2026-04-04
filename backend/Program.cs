using backend.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var enableHttpsRedirection = builder.Configuration.GetValue("Http:UseHttpsRedirection", false);
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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

    const int maxMigrationAttempts = 10;

    for (var attempt = 1; attempt <= maxMigrationAttempts; attempt++)
    {
        try
        {
            dbContext.Database.Migrate();
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
