using Deucalion.Api.Models;
using Deucalion.Api.Options;
using Deucalion.Application;
using Deucalion.Application.Configuration;
using Deucalion.Events;
using Deucalion.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Deucalion.Api.Services;

internal class EngineBackgroundService(
    ApplicationMonitors monitors,
    SqliteStorage storage,
    IHubContext<MonitorHub, IMonitorHubClient> hubContext,
    IOptions<DeucalionOptions> options,
    ILogger<EngineBackgroundService> logger) : BackgroundService
{
    private readonly ApplicationMonitors _monitors = monitors;
    private readonly SqliteStorage _storage = storage;
    private readonly IHubContext<MonitorHub, IMonitorHubClient> _hubContext = hubContext;
    private readonly DeucalionOptions _options = options.Value;
    private readonly ILogger<EngineBackgroundService> _logger = logger;
    private Timer? _purgeTimer;
    private CancellationTokenSource? _internalCts;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var internalToken = _internalCts.Token;

        // Start the monitoring engine in the background
        _ = Task.Run(() => // Removed async from lambda, Run likely expects Action
        {
            var engine = new Engine();
            // Pass a lambda that calls the async callback method
            // Engine.Run itself is likely synchronous or fire-and-forget within Task.Run
            engine.Run(_monitors.Monitors.Values, async e => await CallbackAsync(e, internalToken), internalToken);
        }, internalToken); // Pass internalToken to Task.Run

        // Start the periodic purge timer
        _purgeTimer = new Timer(
            PurgeDatabaseCallback,
            internalToken, // Pass token as state
            TimeSpan.FromMinutes(1), // Initial delay before first purge (e.g., 1 minute after start)
            _options.PurgeInterval // Interval from configuration
        );

        // Keep the service running until cancellation is requested
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async void PurgeDatabaseCallback(object? state)
    {
        if (state is not CancellationToken cancellationToken || cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("PurgeDatabaseCallback cancelled.");
            return;
        }

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
            // Avoid logging error if cancellation was requested during the operation
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during periodic database purge.");
            }
        }
    }

    private async Task CallbackAsync(MonitorEventBase e, CancellationToken cancellationToken)
    {
        // Check for cancellation before proceeding
        if (cancellationToken.IsCancellationRequested) return;

        switch (e)
        {
            case MonitorChecked mc:
                _logger.LogDebug("{event}.", mc);
                await _storage.SaveEventAsync(mc.Name, StoredEvent.From(mc), cancellationToken);

                // Check for cancellation again before potentially long-running GetStatsAsync
                if (cancellationToken.IsCancellationRequested) return;

                var newStats = await _storage.GetStatsAsync(mc.Name, cancellationToken: cancellationToken);
                if (newStats != null)
                {
                    // Check for cancellation before SignalR call
                    if (cancellationToken.IsCancellationRequested) return;
                    await _hubContext.Clients.All.MonitorChecked(MonitorCheckedDto.From(mc, newStats));
                }
                else
                {
                    // Handle the case where stats couldn't be calculated (e.g., log a warning)
                    _logger.LogWarning("Could not calculate stats after saving event for monitor {MonitorName}", mc.Name);
                }
                break;

            case MonitorStateChanged msc:
                _logger.LogDebug("{event}.", msc);
                await _storage.SaveLastStateChangeAsync(msc.Name, msc.At, msc.NewState, cancellationToken);

                // Check for cancellation before SignalR call
                if (cancellationToken.IsCancellationRequested) return;
                await _hubContext.Clients.All.MonitorStateChanged(MonitorStateChangedDto.From(msc));
                break;

            default:
                _logger.LogWarning("Unknown event: {event}.", e);
                break;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping EngineBackgroundService.");

        // Signal cancellation to internal operations
        _internalCts?.Cancel();

        // Stop the timer immediately
        _purgeTimer?.Change(Timeout.Infinite, 0);

        // Allow time for graceful shutdown of Run and callbacks before calling base.StopAsync
        // Adjust delay as needed, or use more sophisticated synchronization if required.
        await Task.Delay(1000, cancellationToken); // Wait a short period

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _purgeTimer?.Dispose();
        _internalCts?.Dispose(); // Dispose the CancellationTokenSource
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
