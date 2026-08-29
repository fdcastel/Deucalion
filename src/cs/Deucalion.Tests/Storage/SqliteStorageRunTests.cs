using Deucalion.Storage;
using Xunit;

namespace Deucalion.Tests.Storage;

/// <summary>
/// <see cref="SqliteStorage.GetCurrentRunAsync"/> backs the "down since" / "up since" of
/// <c>/api/status</c>. It used to be derived from the last 60 events, so anything longer than an
/// hour at the default interval was reported as "since an hour ago".
/// </summary>
public class SqliteStorageRunTests : SqliteStorageTestBase
{
    private const string Monitor = "run-test";
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60);

    private async Task SaveAsync(int index, MonitorState state, int? ms = null)
    {
        var ev = new StoredEvent(T0 + Step * index, state, ms is null ? null : TimeSpan.FromMilliseconds(ms.Value), null);
        await Storage.SaveEventAsync(Monitor, ev, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ThreeDaysDown_SinceIsTheFirstDownProbe_NotTheStatsWindow()
    {
        await SaveAsync(0, MonitorState.Up, 10);
        const int downProbes = 3 * 24 * 60; // 4,320 -- far more than the 60-event stats window
        for (var i = 1; i <= downProbes; i++)
        {
            await SaveAsync(i, MonitorState.Down);
        }

        var run = await Storage.GetCurrentRunAsync(Monitor, TestContext.Current.CancellationToken);

        Assert.NotNull(run);
        Assert.Equal(MonitorState.Down, run.State);
        Assert.Equal(T0 + Step, run.Since);
        Assert.False(run.SinceIsLowerBound);
        Assert.Null(run.LastResponseTime);
    }

    [Fact]
    public async Task WarnDoesNotSplitAnUpRun()
    {
        await SaveAsync(0, MonitorState.Down);
        await SaveAsync(1, MonitorState.Up, 10);
        await SaveAsync(2, MonitorState.Warn, 900);
        await SaveAsync(3, MonitorState.Degraded, 20);
        await SaveAsync(4, MonitorState.Up, 12);

        var run = await Storage.GetCurrentRunAsync(Monitor, TestContext.Current.CancellationToken);

        Assert.NotNull(run);
        Assert.Equal(MonitorState.Up, run.State);
        Assert.Equal(T0 + Step * 1, run.Since);
        Assert.False(run.SinceIsLowerBound);
        Assert.Equal(TimeSpan.FromMilliseconds(12), run.LastResponseTime);
    }

    [Fact]
    public async Task RunReachingTheOldestRow_IsOnlyALowerBound()
    {
        await SaveAsync(0, MonitorState.Down);
        await SaveAsync(1, MonitorState.Down);
        await SaveAsync(2, MonitorState.Down);

        var run = await Storage.GetCurrentRunAsync(Monitor, TestContext.Current.CancellationToken);

        Assert.NotNull(run);
        Assert.Equal(MonitorState.Down, run.State);
        Assert.Equal(T0, run.Since);
        Assert.True(run.SinceIsLowerBound, "no event of the other kind is stored, so the run may predate retention");
    }

    [Fact]
    public async Task UnknownRowsNeitherStartNorEndARun()
    {
        await SaveAsync(0, MonitorState.Down);
        await SaveAsync(1, MonitorState.Unknown);
        await SaveAsync(2, MonitorState.Up, 10);
        await SaveAsync(3, MonitorState.Unknown);
        await SaveAsync(4, MonitorState.Up, 11);

        var run = await Storage.GetCurrentRunAsync(Monitor, TestContext.Current.CancellationToken);

        Assert.NotNull(run);
        Assert.Equal(MonitorState.Up, run.State);
        Assert.Equal(T0 + Step * 2, run.Since);
        Assert.False(run.SinceIsLowerBound);
    }

    [Fact]
    public async Task NoEvents_ReturnsNull()
    {
        Assert.Null(await Storage.GetCurrentRunAsync(Monitor, TestContext.Current.CancellationToken));

        await SaveAsync(0, MonitorState.Unknown);
        Assert.Null(await Storage.GetCurrentRunAsync(Monitor, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OtherMonitorsDoNotLeakIntoTheRun()
    {
        await Storage.SaveEventAsync("other", new StoredEvent(T0 + Step * 5, MonitorState.Down, null, null), TestContext.Current.CancellationToken);
        await SaveAsync(0, MonitorState.Up, 10);
        await SaveAsync(1, MonitorState.Up, 10);

        var run = await Storage.GetCurrentRunAsync(Monitor, TestContext.Current.CancellationToken);

        Assert.NotNull(run);
        Assert.Equal(T0, run.Since);
        Assert.True(run.SinceIsLowerBound);
    }
}
