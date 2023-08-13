namespace Deucalion.Monitors.Events;

public record StateChanged(string Name, DateTimeOffset At, MonitorState NewState) : MonitorEventBase(Name, At);
