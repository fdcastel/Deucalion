using Deucalion.Events;

namespace Deucalion.Storage;

public record StoredEvent(
    DateTimeOffset At,
    MonitorState State,
    TimeSpan? ResponseTime,
    string? ResponseText
)
{
    public static StoredEvent From(MonitorChecked e) =>
        new(
            e.At,
            e.Response?.State ?? MonitorState.Unknown,
            e.Response?.ResponseTime,
            e.Response?.ResponseText
        );
}
