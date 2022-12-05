using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public class CheckInMonitor : IMonitor<MonitorOptions>
    {
        private readonly ManualResetEvent _checkInEvent = new(false);
        private Timer? _resetTimer = null;

        public required MonitorOptions Options { get; init; }

        public Task<bool> IsUpAsync()
        {
            bool result = _checkInEvent.WaitOne(TimeSpan.Zero);
            return Task.FromResult(result);
        }

        public void CheckIn()
        {
            _ = _checkInEvent.Set();

            if (_resetTimer is null)
            {
                _resetTimer = new(Reset, null, Options.IntervalWhenUpOrDefault, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _ = _resetTimer.Change(Options.IntervalWhenUpOrDefault, Timeout.InfiniteTimeSpan);
            }
        }

        private void Reset(object? state)
        {
            _ = _checkInEvent.Reset();
        }
    }
}
