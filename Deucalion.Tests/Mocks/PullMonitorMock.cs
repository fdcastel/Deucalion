using Deucalion.Monitors;

namespace Deucalion.Tests.Mocks;

internal class PullMonitorMock : PullMonitor
{
    private MonitorState CurrentState { get; set; }
    public (MonitorState, TimeSpan)[] Timeline { get; }

    public PullMonitorMock(params (MonitorState, TimeSpan)[] timeline)
    {
        CurrentState = MonitorState.Unknown;
        Timeline = timeline;
    }

    public void Start()
    {
        Task.Run(async () =>
        {
            foreach (var (state, ts) in Timeline)
            {
                CurrentState = state;
                await Task.Delay(ts);
            }
        });
    }

    public override Task<MonitorResponse> QueryAsync() =>
        Task.FromResult(new MonitorResponse()
        {
            State = CurrentState,
            ResponseTime = TimeSpan.FromMilliseconds(333)
        });
}
