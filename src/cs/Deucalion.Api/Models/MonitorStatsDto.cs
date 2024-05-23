using Deucalion.Storage;

namespace Deucalion.Api.Models;

public record MonitorStatsDto(
    MonitorState LastState,
    long LastUpdate,

    double Availability,
    int AverageResponseTimeMs,

    long? LastSeenUp,
    long? LastSeenDown
)
{
    internal static MonitorStatsDto? From(MonitorStats? stats) =>
        stats is null
            ? null
            : new(
                LastState: stats.LastState,
                LastUpdate: stats.LastUpdate.ToUnixTimeSeconds(),
                Availability: stats.Availability,
                AverageResponseTimeMs: (int)stats.AverageResponseTime.TotalMilliseconds,
                LastSeenUp: stats.LastSeenUp?.ToUnixTimeSeconds(),
                LastSeenDown: stats.LastSeenDown?.ToUnixTimeSeconds()
            );
}
