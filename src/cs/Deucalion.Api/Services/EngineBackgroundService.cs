using Deucalion.Api.Models;
using Deucalion.Application;
using Deucalion.Application.Configuration;
using Deucalion.Events;
using Deucalion.Storage;
using Microsoft.AspNetCore.SignalR;

namespace Deucalion.Api.Services;

internal class EngineBackgroundService(
    ApplicationMonitors monitors,
    SqliteStorage storage,
    IHubContext<MonitorHub, IMonitorHubClient> hubContext,
    ILogger<EngineBackgroundService> logger) : BackgroundService
{
    private readonly ApplicationMonitors _monitors = monitors;
    private readonly SqliteStorage _storage = storage;
    private readonly IHubContext<MonitorHub, IMonitorHubClient> _hubContext = hubContext;
    private readonly ILogger<EngineBackgroundService> _logger = logger;

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Task.Run(() =>
        {
            var engine = new Engine();
            engine.Run(_monitors.Monitors.Values, CallbackAsync, cancellationToken);
        }, cancellationToken);

        return Task.CompletedTask;
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
}
