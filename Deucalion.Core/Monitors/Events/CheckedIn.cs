namespace Deucalion.Monitors.Events;

public record CheckedIn(string Name, TimeSpan At, MonitorResponse Response) : MonitorEvent(Name, At);
