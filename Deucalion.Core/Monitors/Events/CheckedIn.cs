namespace Deucalion.Monitors.Events;

public record CheckedIn(string Name, DateTimeOffset At, MonitorResponse Response) : MonitorEvent(Name, At);
