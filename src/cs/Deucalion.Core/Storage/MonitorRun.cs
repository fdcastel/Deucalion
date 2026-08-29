namespace Deucalion.Storage;

/// <summary>
/// A monitor's current run, for the discovery payload (`/api/status`): "down since" / "up since".
/// A run is a maximal sequence of events on the same side of the down/available divide --
/// <see cref="MonitorState.Down"/> on one side, <see cref="MonitorState.Up"/>,
/// <see cref="MonitorState.Warn"/> and <see cref="MonitorState.Degraded"/> on the other, so a
/// Warn blip does not reset "up since". <see cref="MonitorState.Unknown"/> events neither start
/// nor end a run.
/// </summary>
/// <param name="State">The newest non-Unknown state.</param>
/// <param name="Since">When the run started: the first stored event after the newest event of the other kind.</param>
/// <param name="SinceIsLowerBound">
/// True when no event of the other kind is stored at all, i.e. the run reaches the oldest row
/// still retained (see <c>EventRetentionPeriod</c> / <c>MaxEventsPerMonitor</c>): the monitor
/// has been in this state <em>at least</em> since <paramref name="Since"/>.
/// </param>
/// <param name="LastResponseTime">The newest probe's response time, if it recorded one.</param>
public record MonitorRun(
    MonitorState State,
    DateTimeOffset Since,
    bool SinceIsLowerBound,
    TimeSpan? LastResponseTime
);
