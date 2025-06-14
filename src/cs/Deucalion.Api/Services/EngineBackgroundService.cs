using System.Threading.Channels;
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
    private CancellationTokenSource? _internalCts;
    private Task? _engineTask;
    private Task? _purgeTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var internalToken = _internalCts.Token;

        var channel = Channel.CreateUnbounded<IMonitorEvent>();

        // Start the monitoring engine in the background (now as extension method)
        _engineTask = Task.Run(async () =>
        {
            try
            {
                await _monitors.Monitors.Values.RunAllAsync(channel.Writer, internalToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown for producer
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, internalToken);

        // Start the periodic purge using PeriodicTimer
        _purgeTask = Task.Run(async () =>
        {
            using var purgeTimer = new PeriodicTimer(_options.PurgeInterval);
            try
            {
                while (await purgeTimer.WaitForNextTickAsync(internalToken))
                {
                    await PurgeDatabaseAsync(internalToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown for purge
            }
        }, internalToken);

        // Consume events from the channel
        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    switch (evt)
                    {
                        case MonitorChecked mc:
                            await HandleMonitorCheckedAsync(mc, stoppingToken);
                            break;
                        case MonitorStateChanged msc:
                            await HandleMonitorStateChangedAsync(msc, stoppingToken);
                            break;
                        default:
                            _logger.LogWarning("Unknown event: {event}.", evt);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event of type {EventType}.", evt.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown for consumer
        }

        await Task.WhenAll(_engineTask, _purgeTask);
    }

    private async Task HandleMonitorCheckedAsync(MonitorChecked mc, CancellationToken cancellationToken)
    {
        _logger.LogDebug("MonitorChecked: {@event}", mc);
        await _storage.SaveEventAsync(mc.Name, StoredEvent.From(mc), cancellationToken);

        var newStats = await _storage.GetStatsAsync(mc.Name, cancellationToken: cancellationToken);
        if (newStats != null)
        {
            await _hubContext.Clients.All.MonitorChecked(MonitorCheckedDto.From(mc, newStats));
        }
        else
        {
            _logger.LogWarning("Could not calculate stats after saving event for monitor {MonitorName}", mc.Name);
        }
    }

    private async Task HandleMonitorStateChangedAsync(MonitorStateChanged msc, CancellationToken cancellationToken)
    {
        _logger.LogDebug("MonitorStateChanged: {@event}", msc);
        await _storage.SaveLastStateChangeAsync(msc.Name, msc.At, msc.NewState, cancellationToken);
        await _hubContext.Clients.All.MonitorStateChanged(MonitorStateChangedDto.From(msc));
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
            // Avoid logging error if cancellation was requested during the operation
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during periodic database purge.");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping EngineBackgroundService.");

        // Signal cancellation to internal operations
        _internalCts?.Cancel();

        // Await background tasks
        if (_engineTask != null) await _engineTask;
        if (_purgeTask != null) await _purgeTask;

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _internalCts?.Dispose();
        _internalCts = null;
        base.Dispose();
    }
}
