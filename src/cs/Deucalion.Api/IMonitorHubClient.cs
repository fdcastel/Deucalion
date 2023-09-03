using Deucalion.Api.Models;

namespace Deucalion.Api;

public interface IMonitorHubClient
{
    Task MonitorChecked(MonitorCheckedDto e);
    Task MonitorStateChanged(MonitorStateChangedDto e);
}
