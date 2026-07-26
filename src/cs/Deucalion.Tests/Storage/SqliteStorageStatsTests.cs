using Deucalion.Events;
using Deucalion.Storage;
using Xunit;

namespace Deucalion.Tests.Storage;

public class SqliteStorageStatsTests : SqliteStorageTestBase
{
    [Fact]
    public async Task GetStatsAsync_ReportsLastStateAndAvailabilityPerMonitor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        // Monitor 1: two Up probes.
        var e1_1 = new MonitorChecked("m1", now.AddMinutes(-5), MonitorState.Unknown, MonitorResponse.Up(TimeSpan.FromMilliseconds(50), "up1"));
        await Storage.SaveEventAsync(e1_1.Name, StoredEvent.From(e1_1), cancellationToken);
        var e1_2 = new MonitorChecked("m1", now.AddMinutes(-2), MonitorState.Up, MonitorResponse.Up(TimeSpan.FromMilliseconds(60), "up2"));
        await Storage.SaveEventAsync(e1_2.Name, StoredEvent.From(e1_2), cancellationToken);

        // Monitor 2: Down -> Up -> Down.
        var e2_1 = new MonitorChecked("m2", now.AddMinutes(-10), MonitorState.Unknown, MonitorResponse.Down(text: "down1"));
        await Storage.SaveEventAsync(e2_1.Name, StoredEvent.From(e2_1), cancellationToken);
        var e2_2 = new MonitorChecked("m2", now.AddMinutes(-5), MonitorState.Down, MonitorResponse.Up(TimeSpan.FromMilliseconds(100), "up1"));
        await Storage.SaveEventAsync(e2_2.Name, StoredEvent.From(e2_2), cancellationToken);
        var e2_3 = new MonitorChecked("m2", now.AddMinutes(-1), MonitorState.Up, MonitorResponse.Down(text: "down2"));
        await Storage.SaveEventAsync(e2_3.Name, StoredEvent.From(e2_3), cancellationToken);

        // Monitor 3: a single Warn probe.
        var e3_1 = new MonitorChecked("m3", now.AddMinutes(-3), MonitorState.Unknown, MonitorResponse.Warn(TimeSpan.FromMilliseconds(200), "warn1"));
        await Storage.SaveEventAsync(e3_1.Name, StoredEvent.From(e3_1), cancellationToken);

        var s1 = await Storage.GetStatsAsync("m1", cancellationToken: cancellationToken);
        Assert.NotNull(s1);
        Assert.Equal(MonitorState.Up, s1.LastState);
        Assert.Equal(100.0, s1.Availability, tolerance: 0.1);
        Assert.Equal(TimeSpan.FromMilliseconds(50), s1.MinResponseTime);
        Assert.Equal(2, s1.SampleCount);

        var s2 = await Storage.GetStatsAsync("m2", cancellationToken: cancellationToken);
        Assert.NotNull(s2);
        Assert.Equal(MonitorState.Down, s2.LastState);
        // 1 Up, 2 Down -> (3 - 2) / 3.
        Assert.Equal(1.0 / 3.0 * 100.0, s2.Availability, tolerance: 0.1);
        Assert.Equal(TimeSpan.FromMilliseconds(100), s2.MinResponseTime);

        var s3 = await Storage.GetStatsAsync("m3", cancellationToken: cancellationToken);
        Assert.NotNull(s3);
        Assert.Equal(MonitorState.Warn, s3.LastState);
        // Warn counts as available.
        Assert.Equal(100.0, s3.Availability, tolerance: 0.1);
        Assert.Equal(TimeSpan.FromMilliseconds(200), s3.MinResponseTime);

        var s4 = await Storage.GetStatsAsync("m4", cancellationToken: cancellationToken);
        Assert.Null(s4);
    }

    [Fact]
    public async Task GetStatsAsync_OnlyDownEvents_ReportsZeroAvailability()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "stats-only-down";
        var time1 = DateTimeOffset.UtcNow.AddMinutes(-2);
        var time2 = DateTimeOffset.UtcNow;

        await Storage.SaveEventAsync(monitorName, new StoredEvent(time1, MonitorState.Down, null, "Down 1"), cancellationToken);
        await Storage.SaveEventAsync(monitorName, new StoredEvent(time2, MonitorState.Down, null, "Down 2"), cancellationToken);

        var stats = await Storage.GetStatsAsync(monitorName, cancellationToken: cancellationToken);

        Assert.NotNull(stats);
        Assert.Equal(MonitorState.Down, stats.LastState);
        Assert.Equal(0.0, stats.Availability);
        Assert.Equal(0, stats.SampleCount);
    }

    [Fact]
    public async Task GetStatsAsync_NullResponseTimes_AreExcludedFromLatencySamples()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "stats-null-response";
        var now = DateTimeOffset.UtcNow;

        // Up (null RT), Warn (100ms), Up (null RT), Down (null RT).
        await Storage.SaveEventAsync(monitorName, new StoredEvent(now.AddMinutes(-3), MonitorState.Up, null, "Up Null RT"), cancellationToken);
        await Storage.SaveEventAsync(monitorName, new StoredEvent(now.AddMinutes(-2), MonitorState.Warn, TimeSpan.FromMilliseconds(100), "Warn 100ms"), cancellationToken);
        await Storage.SaveEventAsync(monitorName, new StoredEvent(now.AddMinutes(-1), MonitorState.Up, null, "Up Null RT 2"), cancellationToken);
        await Storage.SaveEventAsync(monitorName, new StoredEvent(now, MonitorState.Down, null, "Down Null RT"), cancellationToken);

        var stats = await Storage.GetStatsAsync(monitorName, cancellationToken: cancellationToken);

        Assert.NotNull(stats);
        Assert.Equal(MonitorState.Down, stats.LastState);
        // 3 Up/Warn, 1 Down -> (4 - 1) / 4.
        Assert.Equal(75.0, stats.Availability);
        // Only the Warn probe carries a timing.
        Assert.Equal(1, stats.SampleCount);
        Assert.Equal(TimeSpan.FromMilliseconds(100), stats.Latency50);
    }

    [Fact]
    public async Task GetStatsAsync_DownEventsWithTimings_ExcludedFromLatencyStats()
    {
        // PingMonitor records Down(elapsed=0ms) when the OS-level ping fails
        // synchronously. Those zero timings must not pollute MIN/P50/P95/P99
        // -- otherwise an all-Down monitor reports 0ms across the board.
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "stats-down-with-timing";
        var now = DateTimeOffset.UtcNow;

        await Storage.SaveEventAsync(monitorName, new StoredEvent(now.AddMinutes(-3), MonitorState.Down, TimeSpan.Zero, "TimedOut"), cancellationToken);
        await Storage.SaveEventAsync(monitorName, new StoredEvent(now.AddMinutes(-2), MonitorState.Down, TimeSpan.Zero, "TimedOut"), cancellationToken);
        await Storage.SaveEventAsync(monitorName, new StoredEvent(now.AddMinutes(-1), MonitorState.Up, TimeSpan.FromMilliseconds(80), null), cancellationToken);
        await Storage.SaveEventAsync(monitorName, new StoredEvent(now, MonitorState.Down, TimeSpan.Zero, "TimedOut"), cancellationToken);

        var stats = await Storage.GetStatsAsync(monitorName, cancellationToken: cancellationToken);

        Assert.NotNull(stats);
        // Only the single Up sample contributes to the percentiles.
        Assert.Equal(TimeSpan.FromMilliseconds(80), stats.MinResponseTime);
        Assert.Equal(TimeSpan.FromMilliseconds(80), stats.Latency50);
        Assert.Equal(TimeSpan.FromMilliseconds(80), stats.Latency95);
        Assert.Equal(TimeSpan.FromMilliseconds(80), stats.Latency99);
        Assert.Equal(1, stats.SampleCount);
    }

    [Fact]
    public async Task GetStatsAsync_OnlyDownWithTimings_ProducesNullPercentiles()
    {
        // An all-Down monitor (e.g. ping blocked at the firewall) should show "--" for every
        // latency stat, not zeros.
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "stats-all-down-timed";
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            await Storage.SaveEventAsync(monitorName, new StoredEvent(now.AddSeconds(-i), MonitorState.Down, TimeSpan.Zero, "TimedOut"), cancellationToken);
        }

        var stats = await Storage.GetStatsAsync(monitorName, cancellationToken: cancellationToken);

        Assert.NotNull(stats);
        Assert.Null(stats.MinResponseTime);
        Assert.Null(stats.Latency50);
        Assert.Null(stats.Latency95);
        Assert.Null(stats.Latency99);
        Assert.Equal(0, stats.SampleCount);
    }

    [Fact]
    public async Task GetStatsAsync_AfterPurge_RecalculatesFromRemainingEvents()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "stats-after-purge";
        var now = DateTimeOffset.UtcNow;
        var retentionPeriod = TimeSpan.FromHours(2);
        var cutoff = now - retentionPeriod;

        List<StoredEvent> eventsToSave =
        [
            // Older than the retention period -- will be purged.
            new(cutoff.AddHours(-2), MonitorState.Up, TimeSpan.FromMilliseconds(50), "Old Up 1"),
            new(cutoff.AddHours(-1), MonitorState.Down, null, "Old Down 1"),

            // Within the retention period -- will be kept.
            new(cutoff.AddMinutes(30), MonitorState.Up, TimeSpan.FromMilliseconds(100), "Recent Up 1"),
            new(now, MonitorState.Up, TimeSpan.FromMilliseconds(120), "Recent Up 2"),
        ];

        foreach (var ev in eventsToSave.OrderBy(e => e.At))
        {
            await Storage.SaveEventAsync(monitorName, ev, cancellationToken);
        }

        // 3 Up, 1 Down -> 75%.
        var statsBefore = await Storage.GetStatsAsync(monitorName, cancellationToken: cancellationToken);
        Assert.NotNull(statsBefore);
        Assert.Equal(75.0, statsBefore.Availability);
        Assert.Equal(3, statsBefore.SampleCount);

        await Storage.PurgeOldEventsAsync(retentionPeriod, cancellationToken);

        var statsAfter = await Storage.GetStatsAsync(monitorName, cancellationToken: cancellationToken);
        Assert.NotNull(statsAfter);
        Assert.Equal(MonitorState.Up, statsAfter.LastState);
        Assert.Equal(100.0, statsAfter.Availability);
        Assert.Equal(2, statsAfter.SampleCount);
        Assert.Equal(TimeSpan.FromMilliseconds(100), statsAfter.MinResponseTime);
    }
}
