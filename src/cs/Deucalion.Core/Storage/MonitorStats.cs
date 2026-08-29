namespace Deucalion.Storage;

/// <summary>
/// Rolling statistics over a monitor's recent event window.
/// </summary>
/// <remarks>
/// Deliberately narrow: every member here is projected into MonitorStatsDto and read by the UI.
/// LastUpdate, AverageResponseTime and LastSeenUp/LastSeenDown used to live here but never
/// reached any client -- the UI derives incident runs from the event window it already has.
/// </remarks>
public record MonitorStats(
    MonitorState LastState,

    double Availability,

    TimeSpan? MinResponseTime = null,
    TimeSpan? Latency50 = null,
    TimeSpan? Latency95 = null,
    TimeSpan? Latency99 = null,

    int SampleCount = 0
);
