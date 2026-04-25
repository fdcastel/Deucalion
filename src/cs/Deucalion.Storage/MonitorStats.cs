namespace Deucalion.Storage;

public record MonitorStats(
    MonitorState LastState,
    DateTimeOffset LastUpdate,

    double Availability,
    TimeSpan AverageResponseTime,

    TimeSpan? MinResponseTime = null,
    TimeSpan? Latency50 = null,
    TimeSpan? Latency95 = null,
    TimeSpan? Latency99 = null,

    DateTimeOffset? LastSeenDown = null,
    DateTimeOffset? LastSeenUp = null
);
