using System.Diagnostics;
using Deucalion.Events;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;

namespace Deucalion.Application;

public class Engine
{
    public void Run(IEnumerable<Monitors.Monitor> monitors, Action<MonitorEventBase> callback, CancellationToken stopToken)
    {
        var start = DateTimeOffset.UtcNow;

        var catalog = monitors
            .Select(monitor => new MonitorStatus() { Monitor = monitor })
            .ToDictionary(ms => ms.Monitor);

        foreach (var (monitor, status) in catalog)
        {
            if (monitor is CheckInMonitor checkInMonitor)
            {
                checkInMonitor.CheckedInEvent += PushMonitorCheckedIn;
                checkInMonitor.TimedOutEvent += PushMonitorTimedOut;
            }
            else if (monitor is PullMonitor pullMonitor)
            {
                status.QueryTimer = new Timer(QueryPullMonitor, monitor, TimeSpan.Zero, pullMonitor.IntervalWhenUp);
            }
        }

        try
        {
            stopToken.WaitHandle.WaitOne();
        }
        finally
        {
            foreach (var (_, status) in catalog)
            {
                status.QueryTimer?.Dispose();
            }
        }

        void PushMonitorCheckedIn(object? sender, EventArgs args)
        {
            if (sender is PushMonitor pushMonitor)
            {
                var monitorResponse = args is MonitorResponseEventArgs mrea ? mrea.Response : MonitorResponse.Up();
                UpdateMonitorState(pushMonitor, monitorResponse);
            }
        }

        void PushMonitorTimedOut(object? sender, EventArgs _)
        {
            if (sender is PushMonitor pushMonitor)
            {
                UpdateMonitorState(pushMonitor, null);
            }
        }

        async void QueryPullMonitor(object? sender)
        {
            var timerEventAt = DateTimeOffset.UtcNow;

            if (sender is PullMonitor pullMonitor)
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await pullMonitor.QueryAsync();
                stopwatch.Stop();

                if (response.ResponseTime is null)
                {
                    response = response with { ResponseTime = stopwatch.Elapsed };
                }

                UpdateMonitorState(pullMonitor, response, timerEventAt);
            }
        }

        void UpdateMonitorState(Monitors.Monitor monitor, MonitorResponse? monitorResponse, DateTimeOffset timerEventAt = default)
        {
            var name = monitor.Name;
            var at = DateTimeOffset.UtcNow;

            if (catalog.TryGetValue(monitor, out var status))
            {
                var initialState = monitorResponse?.State ?? MonitorState.Down;
                var effectiveState = initialState;

                // --- ignoreFailCount Logic ---
                if (initialState == MonitorState.Up)
                {
                    status.ConsecutiveFailCount = 0;
                }
                else if (initialState == MonitorState.Down || initialState == MonitorState.Warn)
                {
                    status.ConsecutiveFailCount++;
                    if (monitor.IgnoreFailCount > 0 && status.ConsecutiveFailCount < monitor.IgnoreFailCount)
                    {
                        effectiveState = MonitorState.Degraded;
                    }
                    // else: effectiveState remains Down or Warn
                }
                // else: Unknown state doesn't change fail count or trigger Degraded

                // --- upsideDown Logic ---
                if (monitor.UpsideDown)
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
                var effectiveResponse = monitorResponse is null ? null : monitorResponse with { State = effectiveState };
                callback(new MonitorChecked(name, at, effectiveResponse));

                var hasStateChanged = status.LastKnownState != MonitorState.Unknown && status.LastKnownState != effectiveState;
                if (hasStateChanged)
                {
                    // Notify change
                    callback(new MonitorStateChanged(name, at, effectiveState));

                    if (monitor is PullMonitor pullMonitor)
                    {
                        // Update timer interval based on the *effective* state
                        var dueTime = effectiveState == MonitorState.Up
                            ? pullMonitor.IntervalWhenUp
                            : pullMonitor.IntervalWhenDown;

                        // Subtract from next dueTime the elapsed time since the current timer event.
                        var deltaUntilNow = DateTimeOffset.UtcNow - timerEventAt;
                        var remaining = dueTime - deltaUntilNow;

                        // Avoid exception when debugging with breakpoints.
                        remaining = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;

                        status.QueryTimer?.Change(remaining, dueTime);
                    }
                }

                status.LastKnownState = effectiveState;
            }
        }
    }

    internal class MonitorStatus
    {
        internal required Monitors.Monitor Monitor { get; init; }
        internal Timer? QueryTimer { get; set; }
        internal MonitorState LastKnownState { get; set; } = MonitorState.Unknown;
        internal int ConsecutiveFailCount { get; set; } = 0;
    }
}
