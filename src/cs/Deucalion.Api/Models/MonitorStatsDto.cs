using Deucalion.Storage;

namespace Deucalion.Api.Models;

public record MonitorStatsDto(
    MonitorState LastState,

    double Availability,

    int? MinResponseTimeMs,
    int? Latency50Ms,
    int? Latency95Ms,
    int? Latency99Ms,

    int? WarnTimeoutMs
)
{
    internal static MonitorStatsDto? From(MonitorStats? stats, TimeSpan? effectiveWarnTimeout) =>
        stats is null
            ? null
            : new(
                LastState: stats.LastState,
                Availability: stats.Availability,
                MinResponseTimeMs: stats.MinResponseTime is { } min ? (int)min.TotalMilliseconds : null,
                Latency50Ms: stats.Latency50 is { } p50 ? (int)p50.TotalMilliseconds : null,
                Latency95Ms: stats.Latency95 is { } p95 ? (int)p95.TotalMilliseconds : null,
                Latency99Ms: stats.Latency99 is { } p99 ? (int)p99.TotalMilliseconds : null,
                WarnTimeoutMs: effectiveWarnTimeout is { } w ? (int)w.TotalMilliseconds : null
            );
}
