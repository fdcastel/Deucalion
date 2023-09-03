namespace Deucalion.Monitors;

public sealed class CheckInMonitor : PushMonitor, IDisposable
{
    private readonly ManualResetEvent _checkInEvent = new(false);
    private Timer? _resetTimer;
    private bool _disposed;

    public string? Secret { get; set; }

    public event EventHandler? CheckedInEvent;
    public event EventHandler? TimedOutEvent;

    public override void CheckIn(MonitorResponse? response = null)
    {
        _checkInEvent.Set();
        OnCheckedInEvent(response);

        var resetIn = IntervalToDownOrDefault;
        if (_resetTimer is null)
        {
            _resetTimer = new(Reset, null, resetIn, Timeout.InfiniteTimeSpan);
        }
        else
        {
            _resetTimer.Change(resetIn, Timeout.InfiniteTimeSpan);
        }
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

    private void OnCheckedInEvent(MonitorResponse? response = null)
    {
        // https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/events/how-to-publish-events-that-conform-to-net-framework-guidelines#example
        var checkedInEvent = CheckedInEvent;
        checkedInEvent?.Invoke(this, response ?? EventArgs.Empty);
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
