using System.Threading.Channels;
using Deucalion.Events;

namespace Deucalion.Monitors;

public abstract class PullMonitor
{
    public static readonly TimeSpan DefaultIntervalWhenUp = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DefaultIntervalWhenDown = TimeSpan.FromSeconds(15);

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan DefaultWarnTimeout = TimeSpan.FromSeconds(1);

    public string Name { get; set; } = string.Empty;

    public int IgnoreFailCount { get; set; }
    public bool UpsideDown { get; set; }

    public TimeSpan IntervalWhenUp { get; set; } = DefaultIntervalWhenUp;
    public TimeSpan IntervalWhenDown { get; set; } = DefaultIntervalWhenDown;

    public TimeSpan Timeout { get; set; } = DefaultTimeout;
    public TimeSpan WarnTimeout { get; set; } = DefaultWarnTimeout;

    public MonitorState LastKnownState { get; set; } = MonitorState.Unknown;
    public int ConsecutiveFailCount { get; set; } = 0;

    public abstract Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default);

    public async Task RunMonitorLoopAsync(ChannelWriter<IMonitorEvent> writer, CancellationToken stopToken)
    {
        do
        {
            if (stopToken.IsCancellationRequested) break;

            var queryStartTime = DateTimeOffset.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await QueryAsync(stopToken);

            if (response.ResponseTime is null)
            {
                response = response with { ResponseTime = stopwatch.Elapsed };
            }

            // --- Begin merged UpdateMonitorState logic ---
            var name = Name;
            var initialState = response?.State ?? MonitorState.Down;
            var effectiveState = initialState;

            // --- ignoreFailCount Logic ---
            if (initialState == MonitorState.Up)
            {
                ConsecutiveFailCount = 0;
            }
            else if (initialState == MonitorState.Down || initialState == MonitorState.Warn)
            {
                ConsecutiveFailCount++;
                if (IgnoreFailCount > 0 && ConsecutiveFailCount < IgnoreFailCount)
                {
                    effectiveState = MonitorState.Degraded;
                }
                // else: effectiveState remains Down or Warn
            }
            // else: Unknown state doesn't change fail count or trigger Degraded

            // --- upsideDown Logic ---
            if (UpsideDown)
            {
                if (effectiveState == MonitorState.Up)
                {
                    effectiveState = MonitorState.Down;
                }
                else if (effectiveState == MonitorState.Down)
                {
                    effectiveState = MonitorState.Up;
                }
                // Warn and Degraded states are not flipped
            }

            // Notify response
            var effectiveResponse = response is null ? null : response with { State = effectiveState };
            writer.TryWrite(new MonitorChecked(name, queryStartTime, effectiveResponse)); // Use eventTime

            // Send MonitorStateChanged only if the state actually changed from a known different state, or from Unknown.
            var actualStateHasChanged = LastKnownState != effectiveState && LastKnownState == MonitorState.Unknown || // From Unknown to something else
                                        LastKnownState != MonitorState.Unknown && LastKnownState != effectiveState;   // From a known state to a different known state

            if (actualStateHasChanged)
            {
                // Notify change
                writer.TryWrite(new MonitorStateChanged(name, queryStartTime, effectiveState)); // Use eventTime
            }

            LastKnownState = effectiveState;
            // --- End merged UpdateMonitorState logic ---

            if (stopToken.IsCancellationRequested) break;

            TimeSpan delayInterval = (LastKnownState == MonitorState.Up || LastKnownState == MonitorState.Unknown)
                ? IntervalWhenUp
                : IntervalWhenDown;

            try
            {
                await Task.Delay(delayInterval, stopToken);
            }
            catch (OperationCanceledException)
            {
                break; // Exit loop if cancellation is requested during delay
            }
        }
        while (!stopToken.IsCancellationRequested);
    }
}
