using Deucalion.Monitors;

namespace Deucalion.Api.Models;

public record MonitorStatsDto(
    MonitorState LastState,
    long LastUpdate,

    double Availability,
    int AverageResponseTimeMs,

    long? LastUp,
    long? LastDown
);
