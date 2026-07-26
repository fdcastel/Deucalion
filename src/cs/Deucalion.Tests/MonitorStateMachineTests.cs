using System.Threading.Channels;
using Deucalion.Application;
using Deucalion.Events;
using Deucalion.Monitors;
using Deucalion.Tests.Mocks;
using Xunit;

namespace Deucalion.Tests;

/// <summary>
/// Covers the state machine in <see cref="MonitorExtensions.RunAsync"/>: fail-count
/// suppression, upside-down inversion, response-time back-fill and probe-failure recovery.
/// </summary>
public class MonitorStateMachineTests
{
    /// <summary>
    /// Runs a monitor until <paramref name="count"/> MonitorChecked events have been observed,
    /// then cancels. Each blocking channel read is the synchronisation point, so the assertions
    /// never depend on wall-clock timing -- no FakeTimeProvider advancing, no sleeps.
    /// </summary>
    /// <remarks>
    /// The interval must be non-zero: with a zero delay every await in the loop completes
    /// synchronously, so RunAsync never yields to its caller and the reader below never runs.
    /// </remarks>
    private static async Task<List<MonitorChecked>> ProbeAsync(PullMonitor monitor, int count, CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(1);
        monitor.IntervalWhenUp = interval;
        monitor.IntervalWhenDown = interval;

        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var engineTask = monitor.RunAsync(channel.Writer, cts.Token);

        var observed = new List<MonitorChecked>(count);
        while (observed.Count < count)
        {
            var evt = await channel.Reader.ReadAsync(cts.Token);
            if (evt is MonitorChecked checkedEvent)
            {
                observed.Add(checkedEvent);
            }
        }

        await cts.CancelAsync();
        try { await engineTask; } catch (OperationCanceledException) { }

        return observed;
    }

    private static MonitorState[] StatesOf(IEnumerable<MonitorChecked> events) =>
        [.. events.Select(e => e.Response?.State ?? MonitorState.Unknown)];

    // ---- IgnoreFailCount --------------------------------------------------

    [Fact]
    public async Task IgnoreFailCount_ReportsEarlyFailuresAsDegraded()
    {
        var monitor = new ScriptedMonitor(MonitorState.Down) { Name = "m", IgnoreFailCount = 3 };

        var events = await ProbeAsync(monitor, 4, TestContext.Current.CancellationToken);

        // The first two failures are suppressed; the third is a real Down.
        Assert.Equal(
            [MonitorState.Degraded, MonitorState.Degraded, MonitorState.Down, MonitorState.Down],
            StatesOf(events));
    }

    [Fact]
    public async Task IgnoreFailCount_ResetsAfterAnUpProbe()
    {
        var monitor = new ScriptedMonitor(
            MonitorState.Down, MonitorState.Up, MonitorState.Down, MonitorState.Down)
        { Name = "m", IgnoreFailCount = 3 };

        var events = await ProbeAsync(monitor, 4, TestContext.Current.CancellationToken);

        // The Up at index 1 clears the counter, so the following Downs are suppressed again.
        Assert.Equal(
            [MonitorState.Degraded, MonitorState.Up, MonitorState.Degraded, MonitorState.Degraded],
            StatesOf(events));
    }

    [Fact]
    public async Task IgnoreFailCount_Unset_ReportsFailuresImmediately()
    {
        var monitor = new ScriptedMonitor(MonitorState.Down) { Name = "m" };

        var events = await ProbeAsync(monitor, 2, TestContext.Current.CancellationToken);

        Assert.Equal([MonitorState.Down, MonitorState.Down], StatesOf(events));
    }

    // ---- UpsideDown -------------------------------------------------------

    [Fact]
    public async Task UpsideDown_InvertsUpAndDown()
    {
        var monitor = new ScriptedMonitor(MonitorState.Up, MonitorState.Down)
        { Name = "m", UpsideDown = true };

        var events = await ProbeAsync(monitor, 2, TestContext.Current.CancellationToken);

        Assert.Equal([MonitorState.Down, MonitorState.Up], StatesOf(events));
    }

    [Fact]
    public async Task UpsideDown_LeavesWarnUntouched()
    {
        var monitor = new ScriptedMonitor(MonitorState.Warn)
        { Name = "m", UpsideDown = true };

        var events = await ProbeAsync(monitor, 1, TestContext.Current.CancellationToken);

        Assert.Equal([MonitorState.Warn], StatesOf(events));
    }

    // ---- Warn is an up-ish state ------------------------------------------

    [Fact]
    public async Task Warn_DoesNotCountAsAFailure()
    {
        // Warn means "up but slow". It must not accumulate toward IgnoreFailCount,
        // which would report a merely-slow monitor as Degraded ("May be down").
        var monitor = new ScriptedMonitor(MonitorState.Warn) { Name = "m", IgnoreFailCount = 3 };

        var events = await ProbeAsync(monitor, 3, TestContext.Current.CancellationToken);

        Assert.Equal([MonitorState.Warn, MonitorState.Warn, MonitorState.Warn], StatesOf(events));
    }

    [Fact]
    public async Task Warn_DoesNotAdvanceTheFailCounterForLaterFailures()
    {
        // Two Warns then a Down: with IgnoreFailCount 2 the Down is the *first* failure,
        // so it is suppressed to Degraded rather than counted as the second.
        var monitor = new ScriptedMonitor(MonitorState.Warn, MonitorState.Warn, MonitorState.Down, MonitorState.Down)
        { Name = "m", IgnoreFailCount = 2 };

        var events = await ProbeAsync(monitor, 4, TestContext.Current.CancellationToken);

        Assert.Equal(
            [MonitorState.Warn, MonitorState.Warn, MonitorState.Degraded, MonitorState.Down],
            StatesOf(events));
    }

    [Fact]
    public void DelayFor_Warn_UsesIntervalWhenUp()
    {
        // A slow endpoint must not be polled 4x harder than a healthy one.
        var monitor = new ScriptedMonitor(MonitorState.Warn)
        {
            IntervalWhenUp = TimeSpan.FromMinutes(1),
            IntervalWhenDown = TimeSpan.FromSeconds(15),
        };

        Assert.Equal(monitor.IntervalWhenUp, MonitorExtensions.DelayFor(monitor, MonitorState.Warn));
    }

    // ---- Response time ----------------------------------------------------

    [Fact]
    public async Task ResponseTime_IsBackFilledWhenTheMonitorLeavesItNull()
    {
        var monitor = new ScriptedMonitor(_ => new MonitorResponse(State: MonitorState.Up))
        { Name = "m" };

        var events = await ProbeAsync(monitor, 1, TestContext.Current.CancellationToken);

        Assert.NotNull(events[0].Response?.ResponseTime);
    }

    [Fact]
    public async Task ResponseTime_ReportedByTheMonitorIsPreserved()
    {
        var reported = TimeSpan.FromMilliseconds(4242);
        var monitor = new ScriptedMonitor(_ => new MonitorResponse(MonitorState.Up, ResponseTime: reported))
        { Name = "m" };

        var events = await ProbeAsync(monitor, 1, TestContext.Current.CancellationToken);

        Assert.Equal(reported, events[0].Response?.ResponseTime);
    }

    // ---- Probe failure recovery -------------------------------------------

    [Fact]
    public async Task QueryThrows_ReportsDownAndKeepsPolling()
    {
        // Regression test: an unexpected exception used to escape the polling loop and
        // silently kill the monitor for the lifetime of the process.
        var monitor = new ScriptedMonitor(i => i == 0
            ? throw new InvalidOperationException("boom")
            : new MonitorResponse(MonitorState.Up, TimeSpan.FromMilliseconds(1)))
        { Name = "m" };

        var events = await ProbeAsync(monitor, 3, TestContext.Current.CancellationToken);

        Assert.Equal([MonitorState.Down, MonitorState.Up, MonitorState.Up], StatesOf(events));
        var failureText = events[0].Response?.ResponseText;
        Assert.NotNull(failureText);
        Assert.Contains("boom", failureText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryThrowsEveryTime_KeepsReportingDown()
    {
        var monitor = new ScriptedMonitor(_ => throw new InvalidOperationException("always"))
        { Name = "m" };

        var events = await ProbeAsync(monitor, 3, TestContext.Current.CancellationToken);

        Assert.Equal([MonitorState.Down, MonitorState.Down, MonitorState.Down], StatesOf(events));
    }

    // ---- Poll interval selection ------------------------------------------

    [Theory]
    [InlineData(MonitorState.Up)]
    [InlineData(MonitorState.Unknown)]
    public void DelayFor_UpStates_UsesIntervalWhenUp(MonitorState state)
    {
        var monitor = new ScriptedMonitor(MonitorState.Up)
        {
            IntervalWhenUp = TimeSpan.FromMinutes(1),
            IntervalWhenDown = TimeSpan.FromSeconds(15),
        };

        Assert.Equal(monitor.IntervalWhenUp, MonitorExtensions.DelayFor(monitor, state));
    }

    [Theory]
    [InlineData(MonitorState.Down)]
    [InlineData(MonitorState.Degraded)]
    public void DelayFor_FailingStates_UsesIntervalWhenDown(MonitorState state)
    {
        var monitor = new ScriptedMonitor(MonitorState.Up)
        {
            IntervalWhenUp = TimeSpan.FromMinutes(1),
            IntervalWhenDown = TimeSpan.FromSeconds(15),
        };

        Assert.Equal(monitor.IntervalWhenDown, MonitorExtensions.DelayFor(monitor, state));
    }
}
