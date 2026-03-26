using Deucalion.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Deucalion.Api;

internal class MonitorHub : Hub
{
    internal async Task MonitorChecked(MonitorCheckedDto e) =>
        await Clients.All.SendAsync(nameof(MonitorChecked), e);

    internal async Task MonitorStateChanged(MonitorStateChangedDto e) =>
        await Clients.All.SendAsync(nameof(MonitorStateChanged), e);
}
