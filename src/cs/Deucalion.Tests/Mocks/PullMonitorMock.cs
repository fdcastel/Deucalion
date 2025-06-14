using Deucalion.Monitors;

namespace Deucalion.Tests.Mocks;

internal class PullMonitorMock(params (MonitorState, TimeSpan)[] timeline) : PullMonitor
{
    public (MonitorState State, TimeSpan Duration)[] Timeline { get; } = timeline; // Duration is now illustrative, not directly used by mock's timing
    private int _queryCount = -1; // Start at -1 so first call is index 0

    public override Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default)
    {
        int currentQueryIndex = Interlocked.Increment(ref _queryCount);
        MonitorState stateToReturn;

        if (currentQueryIndex < Timeline.Length)
        {
            stateToReturn = Timeline[currentQueryIndex].State;
        }
        else
        {
            // If called more times than timeline entries, return the last state or a default
            stateToReturn = Timeline.Length > 0 ? Timeline[^1].State : MonitorState.Unknown;
        }

        return Task.FromResult(new MonitorResponse()
        {
            State = stateToReturn,
            ResponseTime = TimeSpan.FromMilliseconds(333)
        });
    }
}
