using Deucalion.Monitors;
using Deucalion.Monitors.Events;

namespace Deucalion.Api.Models;

public record MonitorStateChangedDto(
    string N,
    DateTimeOffset At,
    MonitorState St
)
{
    internal static MonitorStateChangedDto From(MonitorStateChanged msc) =>
        new(
            N: msc.Name,
            At: msc.At,
            St: msc.NewState
        );
};
