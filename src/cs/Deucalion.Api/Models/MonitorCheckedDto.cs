using Deucalion.Monitors;
using Deucalion.Monitors.Events;
using Deucalion.Storage;

namespace Deucalion.Api.Models;

public record MonitorCheckedDto(
    string N,
    long At,
    MonitorState St,
    int? Ms,
    string? Te,
    MonitorStatsDto Ns
)
{
    internal static MonitorCheckedDto From(MonitorChecked mc, MonitorStats stats) =>
        new(
            N: mc.Name,
            At: mc.At.ToUnixTimeSeconds(),
            St: mc.Response?.State ?? MonitorState.Unknown,
            Ms: (int?)mc.Response?.ResponseTime?.TotalMilliseconds,
            Te: mc.Response?.ResponseText,
            Ns: MonitorStatsDto.From(stats)
        );
}
