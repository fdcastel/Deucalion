using Deucalion.Api.Models;
using Deucalion.Application;
using Deucalion.Application.Configuration;
using Deucalion.Monitors;
using Deucalion.Monitors.Events;
using Deucalion.Storage;
using Microsoft.AspNetCore.SignalR;

namespace Deucalion.Api.Services;

public class EngineBackgroundService : BackgroundService
{
    private readonly MonitorConfiguration _configuration;
    private readonly FasterStorage _storage;
    private readonly IHubContext<MonitorHub, IMonitorHubClient> _hubContext;
    private readonly ILogger<EngineBackgroundService> _logger;

    public EngineBackgroundService(
        MonitorConfiguration configuration,
        FasterStorage storage,
        IHubContext<MonitorHub, IMonitorHubClient> hubContext,
        ILogger<EngineBackgroundService> logger)
    {
        _configuration = configuration;
        _storage = storage;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Task.Run(() =>
        {
            var monitors = _configuration.Monitors.Values.ToList();

            var engine = new Engine();
            engine.Run(monitors, CallbackAsync, cancellationToken);
        }, cancellationToken);

        return Task.CompletedTask;
    }

    private async void CallbackAsync(MonitorEventBase e)
    {
        switch (e)
        {
            case MonitorChecked mc:
                _storage.AddEvent(mc);
                await _hubContext.Clients.All.MonitorChecked(new MonitorEventDto(
                     N: mc.Name,
                     At: mc.At.ToUnixTimeSeconds(),
                     St: mc.Response?.State ?? MonitorState.Unknown,
                     Ms: mc.Response?.ResponseTime?.Milliseconds,
                     Te: mc.Response?.ResponseText
                ));
                break;

            case MonitorStateChanged sc:
                await _hubContext.Clients.All.MonitorStateChanged(new MonitorStateChangedDto(
                    N: sc.Name,
                    At: sc.At,
                    St: sc.NewState
                ));
                break;

            default:
                _logger.LogWarning("Ignoring unknown event class ({className}).", nameof(e));
                break;
        }
    }
}
