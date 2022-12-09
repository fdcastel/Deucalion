namespace Deucalion.Monitors.Events;

public record StateChanged(string Name, TimeSpan At, MonitorState NewState) : MonitorEvent(Name, At);
