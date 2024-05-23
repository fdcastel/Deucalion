namespace Deucalion.Events;

public record MonitorChecked(
    string Name,
    DateTimeOffset At,
    MonitorResponse? Response
) : MonitorEventBase(Name, At);
