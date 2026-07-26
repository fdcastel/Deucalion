using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Threading.Channels;
using Deucalion.Api.Models;
using Deucalion.Application;
using Deucalion.Application.Configuration;
using Deucalion.Events;
using Deucalion.Monitors;
using Deucalion.Storage;

namespace Deucalion.Api.Services;

internal class EngineBackgroundService(
    ApplicationMonitors monitors,
    IStorage storage,
    MonitorEventBroadcaster broadcaster,
    ILogger<EngineBackgroundService> logger) : BackgroundService
{
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
                await monitors.Monitors.Values.RunAllAsync(channel.Writer, internalToken);
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
                            logger.LogWarning("Unknown event: {event}.", evt);
                            break;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    logger.LogDebug("Event handling canceled during shutdown for {EventType}.", evt.GetType().Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error handling event of type {EventType}.", evt.GetType().Name);
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
        logger.LogDebug("MonitorChecked: {@event}", mc);
        await storage.SaveEventAsync(mc.Name, StoredEvent.From(mc), cancellationToken);

        var newStats = await storage.GetStatsAsync(mc.Name, historyCount: PullMonitor.StatsWindow, cancellationToken: cancellationToken);
        if (newStats != null)
        {
            monitors.Monitors.TryGetValue(mc.Name, out var monitor);
            var (effectiveWarn, timeout) = WarnThresholdPolicy.Refresh(monitor, newStats.Latency95, newStats.SampleCount);

            var dto = MonitorCheckedDto.FromEvent(mc, newStats, effectiveWarn, timeout);
            var json = JsonSerializer.Serialize(dto, DeucalionJsonContext.Default.MonitorCheckedDto);
            broadcaster.Broadcast(new SseItem<string>(json, "MonitorChecked"));
        }
        else
        {
            logger.LogWarning("Could not calculate stats after saving event for monitor {MonitorName}", mc.Name);
        }
    }

    private async Task HandleMonitorStateChangedAsync(MonitorStateChanged msc, CancellationToken cancellationToken)
    {
        logger.LogDebug("MonitorStateChanged: {@event}", msc);
        await storage.SaveLastStateChangeAsync(msc.Name, msc.At, msc.NewState, cancellationToken);
        var dto = MonitorStateChangedDto.FromEvent(msc);
        var json = JsonSerializer.Serialize(dto, DeucalionJsonContext.Default.MonitorStateChangedDto);
        broadcaster.Broadcast(new SseItem<string>(json, "MonitorStateChanged"));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping EngineBackgroundService.");

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
