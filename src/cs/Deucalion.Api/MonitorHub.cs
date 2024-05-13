using Deucalion.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Deucalion.Api;

internal class MonitorHub : Hub<IMonitorHubClient>
{
    internal async Task MonitorChecked(MonitorCheckedDto e) =>
        await Clients.All.MonitorChecked(e);

    internal async Task MonitorStateChanged(MonitorStateChangedDto e) =>
        await Clients.All.MonitorStateChanged(e);
}
