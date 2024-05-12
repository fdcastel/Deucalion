using Deucalion.Monitors;
using Deucalion.Monitors.Events;

namespace Deucalion.Storage;

public record StoredEvent(
    DateTimeOffset At,
    MonitorState? St,
    int? Ms,
    string? Te
)
{
    public static StoredEvent From(MonitorChecked e) =>
        new(
            e.At,
            e.Response?.State,
            e.Response?.ResponseTime?.Milliseconds,
            e.Response?.ResponseText
        );
}
