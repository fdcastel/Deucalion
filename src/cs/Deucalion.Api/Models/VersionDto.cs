namespace Deucalion.Api.Models;

/// <summary>
/// Served by <c>GET /api/version</c>: identifies the running build (the assembly's informational
/// version, which carries the git SHA) so a deployed instance can be told apart from master.
/// </summary>
public record VersionDto(
    string Name,
    string Version,
    string Runtime,
    DateTime StartedAt
);
