using Deucalion.Storage;

namespace Deucalion.Api.Models;

/// <param name="Since">
/// Unix seconds when the current run began -- "down since" or "up since", the same divide as
/// <see cref="MonitorRun"/> -- computed from the whole stored history, not the stats window: the
/// UI's event list is at most 120 probes, and a monitor down for three days must say so. Null when
/// the monitor has only Unknown events.
/// </param>
/// <param name="SinceIsLowerBound">
/// True when the run reaches the oldest stored event, so the monitor has been in this state
/// <em>at least</em> since <paramref name="Since"/>. Absent when <paramref name="Since"/> is.
/// </param>
public record MonitorStatsDto(
    MonitorState LastState,

    double Availability,

    long? Since,
    bool? SinceIsLowerBound,

    int? MinResponseTimeMs,
    int? Latency50Ms,
    int? Latency95Ms,
    int? Latency99Ms,

    int? WarnTimeoutMs,
    int? TimeoutMs
)
{
    internal static MonitorStatsDto? From(MonitorStats? stats, MonitorRun? run, TimeSpan? effectiveWarnTimeout, TimeSpan? timeout) =>
        stats is null
            ? null
            : new(
                LastState: stats.LastState,
                Availability: stats.Availability,
                Since: run?.Since.ToUnixTimeSeconds(),
                SinceIsLowerBound: run?.SinceIsLowerBound,
                MinResponseTimeMs: stats.MinResponseTime is { } min ? (int)min.TotalMilliseconds : null,
                Latency50Ms: stats.Latency50 is { } p50 ? (int)p50.TotalMilliseconds : null,
                Latency95Ms: stats.Latency95 is { } p95 ? (int)p95.TotalMilliseconds : null,
                Latency99Ms: stats.Latency99 is { } p99 ? (int)p99.TotalMilliseconds : null,
                WarnTimeoutMs: effectiveWarnTimeout is { } w ? (int)w.TotalMilliseconds : null,
                TimeoutMs: timeout is { } t ? (int)t.TotalMilliseconds : null
            );
}
