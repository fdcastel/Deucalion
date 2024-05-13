namespace Deucalion.Monitors.Events;

public record MonitorStateChanged(
    string Name,
    DateTimeOffset At,
    MonitorState NewState
) : MonitorEventBase(Name, At);
