using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public sealed class CheckInMonitor : IPushMonitor<PushMonitorOptions>, IDisposable
    {
        private readonly ManualResetEvent _checkInEvent = new(false);
        private Timer? _resetTimer;
        private bool _disposed;

        public required PushMonitorOptions Options { get; init; }

        public event EventHandler? CheckedInEvent;
        public event EventHandler? TimedOutEvent;

        public void CheckIn()
        {
            _checkInEvent.Set();
            OnCheckedInEvent();

            var resetIn = Options.IntervalToDownOrDefault;
            if (_resetTimer is null)
                _resetTimer = new(Reset, null, resetIn, Timeout.InfiniteTimeSpan);
            else
                _resetTimer.Change(resetIn, Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _resetTimer?.Dispose();
            _disposed = true;
        }

        private void OnCheckedInEvent()
        {
            // https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/events/how-to-publish-events-that-conform-to-net-framework-guidelines#example
            var checkedInEvent = CheckedInEvent;
            checkedInEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnTimedOutEvent()
        {
            var timedOutEvent = TimedOutEvent;
            timedOutEvent?.Invoke(this, EventArgs.Empty);
        }

        private void Reset(object? _)
        {
            _checkInEvent.Reset();
            OnTimedOutEvent();
        }
    }
}
