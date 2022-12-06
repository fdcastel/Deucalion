namespace Deucalion.Monitors.Events
{
    public record MonitorResponse(string Name, TimeSpan At, MonitorState State, TimeSpan ResponseTime) : MonitorEvent(Name, At);
}
