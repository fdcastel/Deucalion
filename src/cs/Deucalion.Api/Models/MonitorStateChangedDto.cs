using System.Text.Json.Serialization;
using Deucalion.Events;

namespace Deucalion.Api.Models;

public record MonitorStateChangedDto(
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("at")] long Timestamp,
    [property: JsonPropertyName("st")] MonitorState State
)
{
    internal static MonitorStateChangedDto From(MonitorStateChanged msc) =>
        new(
            Name: msc.Name,
            Timestamp: msc.At.ToUnixTimeSeconds(),
            State: msc.NewState
        );
};
