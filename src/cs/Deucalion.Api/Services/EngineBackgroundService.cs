using System.Threading.Channels;
using Deucalion.Api.Models;
using Deucalion.Api.Options;
using Deucalion.Application;
using Deucalion.Application.Configuration;
using Deucalion.Events;
using Deucalion.Storage;
using Microsoft.AspNetCore.SignalR;

namespace Deucalion.Api.Services;

internal class EngineBackgroundService(
    ApplicationMonitors monitors,
    IStorage storage,
    IHubContext<MonitorHub, IMonitorHubClient> hubContext,
    DeucalionOptions options,
    ILogger<EngineBackgroundService> logger) : BackgroundService
{
    private readonly ApplicationMonitors _monitors = monitors;
    private readonly IStorage _storage = storage;
    private readonly IHubContext<MonitorHub, IMonitorHubClient> _hubContext = hubContext;
    private readonly DeucalionOptions _options = options;
    private readonly ILogger<EngineBackgroundService> _logger = logger;
    private CancellationTokenSource? _internalCts;
    private Task? _engineTask;

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
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Event handling canceled during shutdown for {EventType}.", evt.GetType().Name);
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

        if (_engineTask != null) await _engineTask;
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping EngineBackgroundService.");

        // Signal cancellation to internal operations
        _internalCts?.Cancel();

        // Await background tasks
        if (_engineTask != null) await _engineTask;

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _internalCts?.Dispose();
        _internalCts = null;
        base.Dispose();
    }
}
