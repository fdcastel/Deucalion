using Deucalion.Monitors;

namespace Deucalion.Tests.Mocks;

internal class PullMonitorMock(params (MonitorState, TimeSpan)[] timeline) : PullMonitor
{
    private MonitorState CurrentState { get; set; } = MonitorState.Unknown;
    public (MonitorState, TimeSpan)[] Timeline { get; } = timeline;

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
