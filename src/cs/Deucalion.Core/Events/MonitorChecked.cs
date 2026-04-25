namespace Deucalion.Events;

public record MonitorChecked(
    string Name,
    DateTimeOffset At,
    MonitorState From,
    MonitorResponse? Response
) : IMonitorEvent;
