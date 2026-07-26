using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public sealed class CheckInMonitor : PullMonitor
{
    public static readonly TimeSpan DefaultIntervalToDown = TimeSpan.FromMinutes(1);

    private DateTimeOffset? _lastCheckInTime;
    private MonitorResponse? _lastResponse;

    // null means "no authentication" -- see the check-in endpoint in Deucalion.Api.
    public string? Secret { get; set; }
    public TimeSpan IntervalToDown { get; set; } = DefaultIntervalToDown;

    public void CheckIn(MonitorResponse? response = null)
    {
        _lastCheckInTime = TimeProvider.GetUtcNow();
        _lastResponse = response ?? MonitorResponse.Up();

        try
        {
            DelayCts?.Cancel(); // Short-circuit the polling delay
        }
        catch (ObjectDisposedException)
        {
            // The polling loop already moved past this delay and disposed the source.
            // The check-in is still recorded above; the next probe picks it up.
        }
    }

    public override Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default)
    {
        if (!_lastCheckInTime.HasValue)
            return Task.FromResult(MonitorResponse.Down());

        if ((TimeProvider.GetUtcNow() - _lastCheckInTime.Value) > IntervalToDown)
            return Task.FromResult(MonitorResponse.Down());

        return Task.FromResult(_lastResponse ?? MonitorResponse.Up());
    }

    /// <summary>
    /// Set by the polling loop each iteration so <see cref="CheckIn"/> can cut the delay short.
    /// Engine implementation detail -- the loop owns its lifetime and disposes it.
    /// </summary>
    public CancellationTokenSource? DelayCts { get; set; }
}
