using Deucalion.Storage;

namespace Deucalion.Api.Models;

internal record MonitorEventDto(
    long At,
    MonitorState St,
    int? Ms,
    string? Te
)
{
    internal static MonitorEventDto From(StoredEvent e) =>
        new(
            At: e.At.ToUnixTimeSeconds(),
            St: e.State,
            Ms: (int?)e.ResponseTime?.TotalMilliseconds,
            Te: e.ResponseText
        );
};
