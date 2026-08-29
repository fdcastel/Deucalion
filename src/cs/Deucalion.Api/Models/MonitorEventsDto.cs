using System.Text.Json.Serialization;
using Deucalion.Storage;

namespace Deucalion.Api.Models;

/// <summary>
/// A monitor's recent events in columnar form, newest first. One object instead of one
/// <c>{ "at", "st", "ms" }</c> row per event: timestamps as a start plus second deltas, states as
/// one digit per event, response times as a parallel array. Four times smaller before
/// compression and about half the size after Brotli at the level the server uses -- the event
/// list is most of <c>GET /api/monitors</c>, and that is the largest transfer on the page.
/// Mirrored by <c>decodeEvents</c> in <c>deucalion-ui/src/services/wire.ts</c>.
/// </summary>
/// <param name="NewestTimestamp">Unix seconds of the newest event (<c>events[0]</c>).</param>
/// <param name="Deltas">
/// Seconds between consecutive events, newest first: <c>at[i + 1] = at[i] - dt[i]</c>. One
/// fewer entry than there are events.
/// </param>
/// <param name="States">One <see cref="MonitorState"/> digit per event, newest first.</param>
/// <param name="ResponseTimesMs">Response time per event in milliseconds; null where the probe recorded none.</param>
internal record MonitorEventsDto(
    [property: JsonPropertyName("at")] long NewestTimestamp,
    [property: JsonPropertyName("dt")] int[] Deltas,
    [property: JsonPropertyName("st")] string States,
    [property: JsonPropertyName("ms")] int?[] ResponseTimesMs
)
{
    /// <returns>Null when there are no events, so the key is omitted from the JSON.</returns>
    internal static MonitorEventsDto? From(IEnumerable<StoredEvent> events)
    {
        var list = events as IList<StoredEvent> ?? events.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var timestamps = new long[list.Count];
        var states = new char[list.Count];
        var responseTimes = new int?[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            timestamps[i] = list[i].At.ToUnixTimeSeconds();
            states[i] = (char)('0' + (int)list[i].State);
            responseTimes[i] = (int?)list[i].ResponseTime?.TotalMilliseconds;
        }

        var deltas = new int[list.Count - 1];
        for (var i = 0; i < deltas.Length; i++)
        {
            deltas[i] = (int)(timestamps[i] - timestamps[i + 1]);
        }

        return new MonitorEventsDto(timestamps[0], deltas, new string(states), responseTimes);
    }
}
