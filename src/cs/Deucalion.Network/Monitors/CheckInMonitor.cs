using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public sealed class CheckInMonitor : PullMonitor
{
    public static readonly TimeSpan DefaultIntervalToDown = TimeSpan.FromMinutes(1);

    private DateTimeOffset? _lastCheckInTime;
    private MonitorResponse? _lastResponse;
    private CancellationTokenSource? _delayCts;

    public string Secret { get; set; } = string.Empty;
    public TimeSpan IntervalToDown { get; set; } = DefaultIntervalToDown;

    public void CheckIn(MonitorResponse? response = null)
    {
        _lastCheckInTime = DateTimeOffset.UtcNow;
        _lastResponse = response ?? MonitorResponse.Up();
        _delayCts?.Cancel(); // Short-circuit the polling delay
    }

    public override Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default)
    {
        if (!_lastCheckInTime.HasValue)
            return Task.FromResult(MonitorResponse.Down());

        if ((DateTimeOffset.UtcNow - _lastCheckInTime.Value) > IntervalToDown)
            return Task.FromResult(MonitorResponse.Down());

        return Task.FromResult(_lastResponse ?? MonitorResponse.Up());
    }

    public CancellationTokenSource? DelayCts
    {
        get => _delayCts;
        set => _delayCts = value;
    }
}
