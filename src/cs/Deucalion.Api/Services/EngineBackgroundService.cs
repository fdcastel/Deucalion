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

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Start the monitoring engine
        _ = Task.Run(() =>
        {
            var engine = new Engine();
            engine.Run(_monitors.Monitors.Values, CallbackAsync, cancellationToken);
        }, cancellationToken);

        // Start the periodic purge timer
        _purgeTimer = new Timer(
            PurgeDatabaseCallback,
            null,
            TimeSpan.FromMinutes(1), // Initial delay before first purge (e.g., 1 minute after start)
            _options.PurgeInterval // Interval from configuration
        );

        return Task.CompletedTask;
    }

    private void PurgeDatabaseCallback(object? state)
    {
        try
        {
            _logger.LogInformation("Starting periodic database purge (Retention: {RetentionPeriod})...", _options.EventRetentionPeriod);
            var deletedCount = _storage.PurgeOldEvents(_options.EventRetentionPeriod);
            _logger.LogInformation("Database purge completed. Deleted {DeletedCount} old events.", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during periodic database purge.");
        }
    }

    private async void CallbackAsync(MonitorEventBase e)
    {
        switch (e)
        {
            case MonitorChecked mc:
                _logger.LogDebug("{event}.", mc);
                _storage.SaveEvent(mc.Name, StoredEvent.From(mc));

                var newStats = _storage.GetStats(mc.Name);
                if (newStats != null)
                {
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
                _storage.SaveLastStateChange(msc.Name, msc.At, msc.NewState);
                await _hubContext.Clients.All.MonitorStateChanged(MonitorStateChangedDto.From(msc));
                break;

            default:
                _logger.LogWarning("Unknown event: {event}.", e);
                break;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping EngineBackgroundService.");
        _purgeTimer?.Change(Timeout.Infinite, 0); // Stop the timer
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _purgeTimer?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
