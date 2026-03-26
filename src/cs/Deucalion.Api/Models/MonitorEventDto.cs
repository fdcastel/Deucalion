using System.Text.Json.Serialization;
using Deucalion.Storage;

namespace Deucalion.Api.Models;

internal record MonitorEventDto(
    [property: JsonPropertyName("at")] long Timestamp,
    [property: JsonPropertyName("st")] MonitorState State,
    [property: JsonPropertyName("ms")] int? ResponseTimeMs,
    [property: JsonPropertyName("te")] string? ResponseText
)
{
    internal static MonitorEventDto From(StoredEvent e) =>
        new(
            Timestamp: e.At.ToUnixTimeSeconds(),
            State: e.State,
            ResponseTimeMs: (int?)e.ResponseTime?.TotalMilliseconds,
            ResponseText: e.ResponseText
        );
};
