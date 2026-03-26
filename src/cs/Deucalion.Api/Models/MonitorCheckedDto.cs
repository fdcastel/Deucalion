using System.Text.Json.Serialization;
using Deucalion.Events;
using Deucalion.Storage;

namespace Deucalion.Api.Models;

public record MonitorCheckedDto(
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("at")] long Timestamp,
    [property: JsonPropertyName("st")] MonitorState State,
    [property: JsonPropertyName("ms")] int? ResponseTimeMs,
    [property: JsonPropertyName("te")] string? ResponseText,
    [property: JsonPropertyName("ns")] MonitorStatsDto NewStats
)
{
    internal static MonitorCheckedDto From(MonitorChecked mc, MonitorStats stats) =>
        new(
            Name: mc.Name,
            Timestamp: mc.At.ToUnixTimeSeconds(),
            State: mc.Response?.State ?? MonitorState.Unknown,
            ResponseTimeMs: (int?)mc.Response?.ResponseTime?.TotalMilliseconds,
            ResponseText: mc.Response?.ResponseText,
            NewStats: MonitorStatsDto.From(stats)!
        );
}
