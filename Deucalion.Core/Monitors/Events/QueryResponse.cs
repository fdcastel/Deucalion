namespace Deucalion.Monitors.Events
{
    public record QueryResponse(string Name, TimeSpan At, MonitorState State, TimeSpan ResponseTime) : MonitorEvent(Name, At);
}
