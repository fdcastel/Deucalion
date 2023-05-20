namespace Deucalion.Monitors.Events;

public record StateChanged(string Name, DateTimeOffset At, MonitorState NewState) : MonitorEvent(Name, At);
