using backend.Configuration;
using backend.Contracts;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class ExerciseCatalogMediaEnrichmentBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExerciseCatalogMediaEnrichmentBackgroundService> _logger;
    private readonly int _maxItems;

    public ExerciseCatalogMediaEnrichmentBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExerciseCatalogMediaEnrichmentBackgroundService> logger,
        IOptions<ExerciseMediaEnrichmentOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _maxItems = Math.Clamp(options.Value.MaxItems, 1, 1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for DB migrations and seed to complete before running enrichment
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        if (stoppingToken.IsCancellationRequested) return;

        _logger.LogInformation("Auto-enrichment: starting background exercise catalog media enrichment (batch size {MaxItems})", _maxItems);

        const int maxPasses = 10;

        for (var pass = 1; pass <= maxPasses; pass++)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var enrichmentService = scope.ServiceProvider.GetRequiredService<ExerciseCatalogMediaEnrichmentService>();

                var result = await enrichmentService.EnrichMissingMediaAsync(
                    new ExerciseCatalogMediaEnrichmentRequest(
                        _maxItems,
                        ExerciseMediaType.All,
                        ExerciseMediaProviderSelection.All,
                        false,
                        false),
                    stoppingToken);

                _logger.LogInformation(
                    "Auto-enrichment pass {Pass}: checked={Checked}, updated={Updated}, stillMissingImages={StillMissing}",
                    pass, result.Checked, result.Updated, result.StillMissingImages);

                if (result.Updated == 0) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-enrichment pass {Pass} failed", pass);
                break;
            }

            // Brief pause between passes to avoid hammering external APIs
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }

        _logger.LogInformation("Auto-enrichment: completed");
    }
}
