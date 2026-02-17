using Deucalion.Events;

namespace Deucalion.Api.Models;

public record MonitorStateChangedDto(
    string N,
    long At,
    MonitorState St
)
{
    internal static MonitorStateChangedDto From(MonitorStateChanged msc) =>
        new(
            N: msc.Name,
            At: msc.At.ToUnixTimeSeconds(),
            St: msc.NewState
        );
};
