namespace Deucalion.Monitors.Events;

public record MonitorEventBase(
    string Name,
    DateTimeOffset At
);
