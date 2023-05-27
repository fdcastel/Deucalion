using Deucalion.Api.Models;

namespace Deucalion.Api;

public interface IMonitorHubClient
{
    Task MonitorChanged(MonitorChangedDto e);

    Task MonitorChecked(MonitorEventDto e);
}
