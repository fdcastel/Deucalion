namespace Deucalion.Events;

public record MonitorStateChanged(
    string Name,
    DateTimeOffset At,
    MonitorState From,
    MonitorState NewState
) : IMonitorEvent;
