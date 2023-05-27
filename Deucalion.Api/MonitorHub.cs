using Deucalion.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Deucalion.Api;

public class MonitorHub : Hub<IMonitorHubClient>
{
    public async Task MonitorChanged(MonitorChangedDto e) =>
        await Clients.All.MonitorChanged(e);

    public async Task MonitorChecked(MonitorEventDto e) =>
        await Clients.All.MonitorChecked(e);
}
