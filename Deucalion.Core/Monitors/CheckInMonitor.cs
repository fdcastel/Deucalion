using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public class CheckInMonitor : IPushMonitor<CheckInMonitorOptions>
    {
        private readonly ManualResetEvent _checkInEvent = new(false);
        private Timer? _resetTimer = null;

        public required CheckInMonitorOptions Options { get; init; }

        public Task<MonitorState> QueryAsync()
        {
            if (_resetTimer is null)
                // Never checked in.
                return Task.FromResult(MonitorState.Unknown);

            var result = _checkInEvent.WaitOne(TimeSpan.Zero);
            return Task.FromResult(MonitorState.FromBool(result));
        }

        public void CheckIn()
        {
            _checkInEvent.Set();

            var resetIn = Options.IntervalToDownOrDefault;
            if (_resetTimer is null)
            {
                _resetTimer = new(Reset, null, resetIn, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _resetTimer.Change(resetIn, Timeout.InfiniteTimeSpan);
            }
        }

        private void Reset(object? state)
        {
            _checkInEvent.Reset();
        }
    }
}
