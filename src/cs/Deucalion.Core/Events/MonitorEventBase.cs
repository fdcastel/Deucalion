namespace Deucalion.Events;

public abstract record MonitorEventBase(
    string Name,
    DateTimeOffset At
);
