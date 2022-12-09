namespace Deucalion.Monitors.Events;

public record QueryResponse(string Name, TimeSpan At, MonitorResponse Response) : MonitorEvent(Name, At);
