namespace Deucalion.Monitors.Events;

public record QueryResponse(string Name, DateTimeOffset At, MonitorResponse Response) : MonitorEventBase(Name, At);
