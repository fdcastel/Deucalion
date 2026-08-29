using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public sealed class CheckInMonitor : PullMonitor
{
    public static readonly TimeSpan DefaultIntervalToDown = TimeSpan.FromMinutes(1);

    // CheckIn() runs on ASP.NET request threads; QueryAsync and the delay arming run on the
    // engine's polling loop. Everything below is shared between them and guarded by _gate
    // (issue #22: DateTimeOffset? is a multi-word struct, so an unguarded read can tear).
    private readonly Lock _gate = new();
    private DateTimeOffset? _lastCheckInTime;
    private MonitorResponse? _lastResponse;
    private bool _checkInPending;
    private CancellationTokenSource? _delayCts;

    // null means "no authentication" -- see the check-in endpoint in Deucalion.Api.
    public string? Secret { get; set; }
    public TimeSpan IntervalToDown { get; set; } = DefaultIntervalToDown;

    public void CheckIn(MonitorResponse? response = null)
    {
        CancellationTokenSource? delayCts;
        lock (_gate)
        {
            // Record first, then short-circuit: whatever the polling loop is doing right now,
            // it either sees the live delay source cancelled or finds the pending flag when it
            // next arms a delay. Either way this check-in is reflected by the next probe.
            _lastCheckInTime = TimeProvider.GetUtcNow();
            _lastResponse = response ?? MonitorResponse.Up();
            _checkInPending = true;
            delayCts = _delayCts;
        }

        // Cancelled outside the lock: cancelling runs the delay's continuation, which may run the
        // polling loop inline on this thread, and that loop takes _gate itself.
        try
        {
            delayCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Lost the race with DisarmDelay() + Dispose(): the loop is already past this delay
            // and about to probe, and the check-in is recorded above, so nothing is missed.
        }
    }

    public override Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset? lastCheckInTime;
        MonitorResponse? lastResponse;
        lock (_gate)
        {
            // This probe reflects every check-in recorded so far; only later ones are "pending".
            _checkInPending = false;
            lastCheckInTime = _lastCheckInTime;
            lastResponse = _lastResponse;
        }

        if (!lastCheckInTime.HasValue)
            return Task.FromResult(MonitorResponse.Down());

        if ((TimeProvider.GetUtcNow() - lastCheckInTime.Value) > IntervalToDown)
            return Task.FromResult(MonitorResponse.Down());

        return Task.FromResult(lastResponse ?? MonitorResponse.Up());
    }

    /// <summary>
    /// Installs the polling loop's delay source so <see cref="CheckIn"/> can cut the delay short.
    /// Engine implementation detail -- the loop owns the source's lifetime and must call
    /// <see cref="DisarmDelay"/> before disposing it.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when a check-in arrived after the last <see cref="QueryAsync"/>,
    /// i.e. in the window where no delay source was armed (issue #22). The caller should skip the
    /// delay and probe again immediately; the source is armed either way.
    /// </returns>
    public bool ArmDelay(CancellationTokenSource delayCts)
    {
        lock (_gate)
        {
            _delayCts = delayCts;
            return !_checkInPending;
        }
    }

    /// <summary>
    /// Forgets the delay source armed by <see cref="ArmDelay"/>. Call before disposing it so a
    /// later <see cref="CheckIn"/> never targets a disposed source.
    /// </summary>
    public void DisarmDelay()
    {
        lock (_gate)
        {
            _delayCts = null;
        }
    }
}
