namespace Deucalion.Api.Models;

/// <summary>
/// Self-describing summary served by <c>GET /api/status</c> (and by <c>GET /</c> under
/// <c>Accept: application/json</c>). Long keys, string states and ISO-8601 timestamps on purpose:
/// this payload is for agents and humans on a one-shot discovery fetch, not for the UI, so it
/// is NOT mirrored in <c>deucalion-types.ts</c>.
/// </summary>
public record StatusDto(
    string Status,
    DateTime UpdatedAt,
    double? Availability,
    StatusMonitorDto[] Monitors,
    StatusLinksDto Links
);

public record StatusMonitorDto(
    string Name,
    string? Group,
    string Type,
    string State,
    DateTime? Since,
    double? Availability,
    int? LatencyMs
);

public record StatusLinksDto(
    string Self,
    string Monitors,
    string Events,
    string Version,
    string Docs
);
