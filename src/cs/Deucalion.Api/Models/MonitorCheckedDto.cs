using System.Text.Json.Serialization;
using Deucalion.Events;
using Deucalion.Storage;

namespace Deucalion.Api.Models;

public record MonitorCheckedDto(
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("at")] long Timestamp,
    [property: JsonPropertyName("fr")] MonitorState From,
    [property: JsonPropertyName("st")] MonitorState State,
    [property: JsonPropertyName("ms")] int? ResponseTimeMs,
    [property: JsonPropertyName("ns")] MonitorStatsDto NewStats
)
{
    internal static MonitorCheckedDto FromEvent(MonitorChecked mc, MonitorStats stats, TimeSpan? effectiveWarnTimeout) =>
        new(
            Name: mc.Name,
            Timestamp: mc.At.ToUnixTimeSeconds(),
            From: mc.From,
            State: mc.Response?.State ?? MonitorState.Unknown,
            ResponseTimeMs: (int?)mc.Response?.ResponseTime?.TotalMilliseconds,
            NewStats: MonitorStatsDto.From(stats, effectiveWarnTimeout)!
        );
}
