using Deucalion.Api.Options;
using Deucalion.Storage;

namespace Deucalion.Api.Services;

internal class PurgeBackgroundService(
    IStorage storage,
    DeucalionOptions options,
    TimeProvider timeProvider,
    ILogger<PurgeBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run initial purge immediately at startup
        await PurgeDatabaseAsync(stoppingToken);

        // Clock is injected so tests can drive the interval without waiting on it.
        using var purgeTimer = new PeriodicTimer(options.PurgeInterval, timeProvider);
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
            logger.LogInformation("Starting periodic database purge (Retention: {RetentionPeriod}, MaxEventsPerMonitor: {MaxEventsPerMonitor})...", options.EventRetentionPeriod, options.MaxEventsPerMonitor);
            var deletedCount = await storage.PurgeOldEventsAsync(options.EventRetentionPeriod, options.MaxEventsPerMonitor, cancellationToken);
            logger.LogInformation("Database purge completed. Deleted {DeletedCount} old events.", deletedCount);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Database purge operation was cancelled.");
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error occurred during periodic database purge.");
            }
        }
    }
}
