namespace Deucalion.Events;

public record MonitorChecked(
    string Name,
    DateTimeOffset At,
    MonitorResponse? Response
) : IMonitorEvent;
