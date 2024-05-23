namespace Deucalion.Storage;

public record MonitorStats(
    MonitorState LastState,
    DateTimeOffset LastUpdate,

    double Availability,
    TimeSpan AverageResponseTime,

    DateTimeOffset? LastSeenDown = null,
    DateTimeOffset? LastSeenUp = null
);
