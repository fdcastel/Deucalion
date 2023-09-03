using Deucalion.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Deucalion.Api;

public class MonitorHub : Hub<IMonitorHubClient>
{
    public async Task MonitorChecked(MonitorCheckedDto e) =>
        await Clients.All.MonitorChecked(e);

    public async Task MonitorStateChanged(MonitorStateChangedDto e) =>
        await Clients.All.MonitorStateChanged(e);
}
