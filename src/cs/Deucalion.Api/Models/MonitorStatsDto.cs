using Deucalion.Storage;

namespace Deucalion.Api.Models;

public record MonitorStatsDto(
    MonitorState LastState,
    long LastUpdate,

    double Availability,
    int AverageResponseTimeMs,

    int? MinResponseTimeMs,
    int? Latency50Ms,
    int? Latency95Ms,
    int? Latency99Ms,

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
                MinResponseTimeMs: stats.MinResponseTime is { } min ? (int)min.TotalMilliseconds : null,
                Latency50Ms: stats.Latency50 is { } p50 ? (int)p50.TotalMilliseconds : null,
                Latency95Ms: stats.Latency95 is { } p95 ? (int)p95.TotalMilliseconds : null,
                Latency99Ms: stats.Latency99 is { } p99 ? (int)p99.TotalMilliseconds : null,
                LastSeenUp: stats.LastSeenUp?.ToUnixTimeSeconds(),
                LastSeenDown: stats.LastSeenDown?.ToUnixTimeSeconds()
            );
}
