using Deucalion.Monitors;

namespace Deucalion.Api.Models;

public record MonitorEventDto(
    long At,
    MonitorState St,
    int? Ms,
    string? Te
);
