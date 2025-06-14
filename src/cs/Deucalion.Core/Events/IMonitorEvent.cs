namespace Deucalion.Events;

public interface IMonitorEvent
{
    DateTimeOffset At { get; init; }

    string Name { get; init; }
}
