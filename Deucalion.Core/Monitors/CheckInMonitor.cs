using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public class CheckInMonitor : IMonitor<MonitorOptions>
    {
        private readonly ManualResetEvent _checkInEvent = new(false);
        private Timer? _resetTimer = null;

        public required MonitorOptions Options { get; init; }

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

            if (_resetTimer is null)
            {
                _resetTimer = new(Reset, null, Options.IntervalWhenUpOrDefault, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _resetTimer.Change(Options.IntervalWhenUpOrDefault, Timeout.InfiniteTimeSpan);
            }
        }

        private void Reset(object? state)
        {
            _checkInEvent.Reset();
        }
    }
}
