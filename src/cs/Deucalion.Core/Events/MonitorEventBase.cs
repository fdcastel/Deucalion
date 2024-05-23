namespace Deucalion.Events;

public record MonitorEventBase(
    string Name,
    DateTimeOffset At
);
