namespace Deucalion.Monitors.Events;

public record CheckInMissed(string Name, DateTimeOffset At) : MonitorEvent(Name, At);
