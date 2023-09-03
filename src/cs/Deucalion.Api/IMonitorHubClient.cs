using Deucalion.Api.Models;

namespace Deucalion.Api;

public interface IMonitorHubClient
{
    Task MonitorChecked(MonitorEventDto e);
    Task MonitorStateChanged(MonitorStateChangedDto e);
}
