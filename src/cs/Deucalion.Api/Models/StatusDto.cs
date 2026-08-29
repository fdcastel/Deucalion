namespace Deucalion.Api.Models;

/// <summary>
/// Self-describing summary served by <c>GET /api/status</c> (and by <c>GET /</c> under
/// <c>Accept: application/json</c>). Long keys, string states and ISO-8601 timestamps on purpose:
/// this payload is for agents and humans on a one-shot discovery fetch, not for the UI, so it
/// is NOT mirrored in <c>deucalion-types.ts</c>.
/// </summary>
/// <param name="Group">Echoes the <c>?group=</c> filter; absent for the unfiltered document.</param>
public record StatusDto(
    string Status,
    string? Group,
    DateTime UpdatedAt,
    double? Availability,
    StatusMonitorDto[] Monitors,
    StatusLinksDto Links
);

/// <param name="Since">
/// When the current run started: "down since" or "up since" (Warn and Degraded count as up).
/// Bounded only by event retention -- see <paramref name="SinceIsLowerBound"/>.
/// </param>
/// <param name="SinceIsLowerBound">
/// True when the run reaches the oldest stored event, so the monitor has been in this state
/// <em>at least</em> since <paramref name="Since"/>. Absent when <paramref name="Since"/> is.
/// </param>
public record StatusMonitorDto(
    string Name,
    string? Group,
    string Type,
    string State,
    DateTime? Since,
    bool? SinceIsLowerBound,
    double? Availability,
    int? LatencyMs
);

/// <param name="MonitorStatus">Template for the per-monitor document, e.g. <c>/api/status/{name}</c>.</param>
public record StatusLinksDto(
    string Self,
    string Monitors,
    string MonitorStatus,
    string Events,
    string Version,
    string Docs
);

/// <summary>Served by <c>GET /api/status/{name}</c>: one monitor, same shape as its entry in <see cref="StatusDto"/>.</summary>
public record MonitorStatusDto(
    DateTime UpdatedAt,
    StatusMonitorDto Monitor,
    MonitorStatusLinksDto Links
);

/// <param name="Monitor">The full-detail document for this monitor (<c>/api/monitors/{name}</c>).</param>
public record MonitorStatusLinksDto(
    string Self,
    string Status,
    string Monitor,
    string Events
);
