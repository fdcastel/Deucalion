using Deucalion.Api.Options;
using Deucalion.Storage;

namespace Deucalion.Api.Services;

internal class PurgeBackgroundService(
    IStorage storage,
    DeucalionOptions options,
    ILogger<PurgeBackgroundService> logger) : BackgroundService
{
    private readonly IStorage _storage = storage;
    private readonly DeucalionOptions _options = options;
    private readonly ILogger<PurgeBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var purgeTimer = new PeriodicTimer(_options.PurgeInterval);
        try
        {
            while (await purgeTimer.WaitForNextTickAsync(stoppingToken))
            {
                await PurgeDatabaseAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    private async Task PurgeDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting periodic database purge (Retention: {RetentionPeriod})...", _options.EventRetentionPeriod);
            var deletedCount = await _storage.PurgeOldEventsAsync(_options.EventRetentionPeriod, cancellationToken);
            _logger.LogInformation("Database purge completed. Deleted {DeletedCount} old events.", deletedCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Database purge operation was cancelled.");
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during periodic database purge.");
            }
        }
    }
}
