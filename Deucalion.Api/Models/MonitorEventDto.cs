using Deucalion.Monitors;

namespace Deucalion.Api.Models;

public record MonitorEventDto(
    string? N,
    long At,
    MonitorState St,
    int? Ms,
    string? Te
);
