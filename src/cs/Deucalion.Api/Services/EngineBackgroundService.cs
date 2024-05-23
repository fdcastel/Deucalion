using Deucalion.Api.Models;
using Deucalion.Application;
using Deucalion.Events;
using Deucalion.Storage;
using Microsoft.AspNetCore.SignalR;

namespace Deucalion.Api.Services;

internal class EngineBackgroundService(
    IEnumerable<Monitors.Monitor> monitors,
    FasterStorage storage,
    IHubContext<MonitorHub, IMonitorHubClient> hubContext,
    ILogger<EngineBackgroundService> logger) : BackgroundService
{
    private readonly List<Monitors.Monitor> _monitors = monitors.ToList();
    private readonly FasterStorage _storage = storage;
    private readonly IHubContext<MonitorHub, IMonitorHubClient> _hubContext = hubContext;
    private readonly ILogger<EngineBackgroundService> _logger = logger;

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Task.Run(() =>
        {
            var engine = new Engine();
            engine.Run(_monitors, CallbackAsync, cancellationToken);
        }, cancellationToken);

        return Task.CompletedTask;
    }

    private async void CallbackAsync(MonitorEventBase e)
    {
        switch (e)
        {
            case MonitorChecked mc:
                var newStats = _storage.SaveEvent(mc.Name, StoredEvent.From(mc));
                await _hubContext.Clients.All.MonitorChecked(MonitorCheckedDto.From(mc, newStats));
                break;

            case MonitorStateChanged msc:
                _storage.SaveLastStateChange(msc.Name, msc.At, msc.NewState);
                await _hubContext.Clients.All.MonitorStateChanged(MonitorStateChangedDto.From(msc));
                break;

            default:
                _logger.LogWarning("Unknown event: {event}.", e);
                break;
        }
    }
}
