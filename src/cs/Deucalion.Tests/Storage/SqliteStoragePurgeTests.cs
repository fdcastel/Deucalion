using Deucalion.Storage;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Deucalion.Tests.Storage;

public class SqliteStoragePurgeTests : SqliteStorageTestBase
{
    private readonly FakeTimeProvider _time;

    public SqliteStoragePurgeTests() : this(new FakeTimeProvider()) { }

    private SqliteStoragePurgeTests(FakeTimeProvider time) : base(time)
    {
        _time = time;
    }

    private DateTimeOffset Now => _time.GetUtcNow();

    private static IEnumerable<StoredEvent> EventsEndingAt(DateTimeOffset newest, int count, TimeSpan step, string? responseText = null) =>
        Enumerable.Range(0, count).Select(i => new StoredEvent(newest - step * i, MonitorState.Up, TimeSpan.FromMilliseconds(i), responseText));

    [Fact]
    public async Task PurgeOldEventsAsync_RemovesOldEventsAndKeepsRecentOnes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "purge-test-monitor";
        var retentionPeriod = TimeSpan.FromDays(7);
        var cutoff = Now - retentionPeriod;

        var eventsToSave = new List<StoredEvent>
        {
            // Should be kept
            new(cutoff.AddHours(1), MonitorState.Up, TimeSpan.FromMilliseconds(100), "Recent 1"),
            new(Now, MonitorState.Up, TimeSpan.FromMilliseconds(110), "Recent 2"),

            // Should be purged
            new(cutoff.AddHours(-1), MonitorState.Down, null, "Old 1"),
            new(cutoff.AddDays(-1), MonitorState.Up, TimeSpan.FromMilliseconds(120), "Old 2"),
            new(Now.AddDays(-30), MonitorState.Up, TimeSpan.FromMilliseconds(130), "Very Old 3")
        };

        foreach (var ev in eventsToSave.OrderBy(e => e.At))
        {
            await Storage.SaveEventAsync(monitorName, ev, cancellationToken);
        }

        Assert.Equal(eventsToSave.Count, await CountEventsAsync(monitorName, cancellationToken));

        var deletedCount = await Storage.PurgeOldEventsAsync(retentionPeriod, cancellationToken);

        Assert.Equal(3, deletedCount);

        var remainingEvents = (await Storage.GetLastEventsAsync(monitorName, 10, cancellationToken)).ToList();
        Assert.Equal(["Recent 2", "Recent 1"], remainingEvents.Select(e => e.ResponseText));
        Assert.All(remainingEvents, e => Assert.True(e.At >= cutoff));
    }

    [Fact]
    public async Task Issue23_PurgeOldEventsAsync_UsesTheInjectedClock()
    {
        // The cutoff used to come from DateTimeOffset.UtcNow regardless of the TimeProvider the
        // host injects, so the retention could not be driven by a fake clock.
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "purge-clock";
        var retentionPeriod = TimeSpan.FromDays(7);

        await Storage.SaveEventAsync(monitorName, new(Now.AddDays(-8), MonitorState.Up, null, "8 days old"), cancellationToken);
        await Storage.SaveEventAsync(monitorName, new(Now.AddDays(-6), MonitorState.Up, null, "6 days old"), cancellationToken);

        Assert.Equal(1, await Storage.PurgeOldEventsAsync(retentionPeriod, cancellationToken));
        Assert.Equal("6 days old", Assert.Single(await Storage.GetLastEventsAsync(monitorName, 10, cancellationToken)).ResponseText);

        // Two virtual days later the remaining event is 8 days old and goes too.
        _time.Advance(TimeSpan.FromDays(2));

        Assert.Equal(1, await Storage.PurgeOldEventsAsync(retentionPeriod, cancellationToken));
        Assert.Empty(await Storage.GetLastEventsAsync(monitorName, 10, cancellationToken));
    }

    [Fact]
    public async Task PurgeOldEventsAsync_ZeroRetentionAndNoCap_DoesNotPurge()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "purge-zero-retention";
        var eventsToSave = new List<StoredEvent>
        {
            new(Now.AddMinutes(-10), MonitorState.Up, TimeSpan.FromMilliseconds(100), "Event 1"),
            new(Now.AddMinutes(-5), MonitorState.Down, null, "Event 2"),
        };
        foreach (var ev in eventsToSave) await Storage.SaveEventAsync(monitorName, ev, cancellationToken);

        var deletedCount = await Storage.PurgeOldEventsAsync(TimeSpan.Zero, maxEventsPerMonitor: 0, cancellationToken);

        Assert.Equal(0, deletedCount);
        Assert.Equal(eventsToSave.Count, await CountEventsAsync(monitorName, cancellationToken));
    }

    [Fact]
    public async Task PurgeOldEventsAsync_AllEventsOld_PurgesAll()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "purge-all-old";
        var retentionPeriod = TimeSpan.FromHours(1);
        var eventsToSave = new List<StoredEvent>
        {
            new(Now.AddHours(-2), MonitorState.Up, TimeSpan.FromMilliseconds(100), "Old Event 1"),
            new(Now.AddHours(-3), MonitorState.Down, null, "Old Event 2"),
        };
        foreach (var ev in eventsToSave) await Storage.SaveEventAsync(monitorName, ev, cancellationToken);

        var deletedCount = await Storage.PurgeOldEventsAsync(retentionPeriod, cancellationToken);

        Assert.Equal(eventsToSave.Count, deletedCount);
        Assert.Empty(await Storage.GetLastEventsAsync(monitorName, 10, cancellationToken));
    }

    [Fact]
    public async Task PurgeOldEventsAsync_NoEventsOrAllNew_PurgesNone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitorName = "purge-all-new";
        var retentionPeriod = TimeSpan.FromDays(1);

        var deletedCountNoEvents = await Storage.PurgeOldEventsAsync(retentionPeriod, cancellationToken);

        var newEvents = new List<StoredEvent>
        {
            new(Now.AddHours(-1), MonitorState.Up, TimeSpan.FromMilliseconds(100), "New Event 1"),
            new(Now.AddHours(-2), MonitorState.Up, TimeSpan.FromMilliseconds(110), "New Event 2"),
        };
        foreach (var ev in newEvents) await Storage.SaveEventAsync(monitorName, ev, cancellationToken);

        var deletedCountAllNew = await Storage.PurgeOldEventsAsync(retentionPeriod, cancellationToken);

        Assert.Equal(0, deletedCountNoEvents);
        Assert.Equal(0, deletedCountAllNew);
        Assert.Equal(newEvents.Count, await CountEventsAsync(monitorName, cancellationToken));
    }

    [Fact]
    public async Task Issue23_PurgeOldEventsAsync_CapsTheEventsKeptPerMonitor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var step = TimeSpan.FromSeconds(3);

        await BulkInsertAsync("busy", EventsEndingAt(Now, 150, step), cancellationToken);
        await BulkInsertAsync("quiet", EventsEndingAt(Now, 50, step), cancellationToken);

        // Retention disabled: only the cap can delete here.
        var deletedCount = await Storage.PurgeOldEventsAsync(TimeSpan.Zero, maxEventsPerMonitor: 100, cancellationToken);

        Assert.Equal(50, deletedCount);
        Assert.Equal(100, await CountEventsAsync("busy", cancellationToken));
        Assert.Equal(50, await CountEventsAsync("quiet", cancellationToken));

        // The newest 100 survive, not an arbitrary 100.
        var busy = (await Storage.GetLastEventsAsync("busy", 1000, cancellationToken)).ToList();
        Assert.Equal(Now, busy[0].At);
        Assert.Equal(Now - step * 99, busy[^1].At);
    }

    [Fact]
    public async Task Issue23_PurgeOldEventsAsync_AgeAndCap_AreBothApplied()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var step = TimeSpan.FromDays(1);

        // 20 daily events: 10 within a 10-day retention, of which the cap keeps 5.
        await BulkInsertAsync("m", EventsEndingAt(Now, 20, step), cancellationToken);

        var deletedCount = await Storage.PurgeOldEventsAsync(TimeSpan.FromDays(10), maxEventsPerMonitor: 5, cancellationToken);

        Assert.Equal(15, deletedCount);
        Assert.Equal(5, await CountEventsAsync("m", cancellationToken));
    }

    [Fact]
    public async Task Issue23_PurgeOldEventsAsync_DeletesInChunks_AndKeepsTheNewestRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var step = TimeSpan.FromSeconds(1);
        const int keep = 3_000;
        var total = 2 * SqliteStorage.PurgeChunkSize + 5_000; // > 2 chunks to delete, plus a partial one

        await BulkInsertAsync("bulk", EventsEndingAt(Now, total, step), cancellationToken);

        // Cutoff falls right between the newest 'keep' rows and the rest.
        var retentionPeriod = step * (keep - 1) + TimeSpan.FromTicks(1);
        var deletedCount = await Storage.PurgeOldEventsAsync(retentionPeriod, cancellationToken);

        Assert.Equal(total - keep, deletedCount);
        Assert.Equal(keep, await CountEventsAsync("bulk", cancellationToken));

        var remaining = (await Storage.GetLastEventsAsync("bulk", total, cancellationToken)).ToList();
        Assert.Equal(Now, remaining[0].At);
        Assert.Equal(Now - step * (keep - 1), remaining[^1].At);
    }

    [Fact]
    public async Task Issue23_PurgeOldEventsAsync_ReturnsDiskToTheFileSystem()
    {
        // The purge never ran VACUUM (nor was auto_vacuum set), so the file never shrank: a user
        // tightening the retention saw zero disk reclaimed.
        var cancellationToken = TestContext.Current.CancellationToken;
        var padding = new string('x', 512);

        await BulkInsertAsync("fat", EventsEndingAt(Now, 20_000, TimeSpan.FromSeconds(1), padding), cancellationToken);
        var sizeBefore = await GetDatabaseSizeAsync(cancellationToken);
        Assert.True(sizeBefore > 8 * 1024 * 1024, $"Expected a database of several MB, got {sizeBefore} bytes.");

        Assert.Equal(2L, await ExecuteScalarAsync<long>("PRAGMA auto_vacuum;", cancellationToken)); // INCREMENTAL, set by InitializeAsync

        var deletedCount = await Storage.PurgeOldEventsAsync(TimeSpan.Zero, maxEventsPerMonitor: 100, cancellationToken);

        // Measured straight after the purge, without any help from the test: the purge itself
        // must checkpoint, or the shrink would sit in the WAL until some later checkpoint.
        var sizeAfter = new FileInfo(DbFilePath).Length;
        var walAfter = new FileInfo(DbFilePath + "-wal") is { Exists: true } wal ? wal.Length : 0;

        Assert.Equal(19_900, deletedCount);
        Assert.True(sizeAfter < sizeBefore / 10, $"Expected the file to shrink by an order of magnitude, but it went from {sizeBefore} to {sizeAfter} bytes.");
        Assert.Equal(0, walAfter);
    }

    [Fact]
    public async Task Issue23_PurgeOldEventsAsync_ConvertsADatabaseCreatedWithoutAutoVacuum()
    {
        // Databases from before this fix have auto_vacuum=NONE, which incremental_vacuum cannot
        // shrink; the first purge that deletes rows must fall back to a full VACUUM and convert.
        var cancellationToken = TestContext.Current.CancellationToken;
        await ExecuteScalarAsync<object?>("PRAGMA auto_vacuum=NONE; VACUUM;", cancellationToken);
        Assert.Equal(0L, await ExecuteScalarAsync<long>("PRAGMA auto_vacuum;", cancellationToken));

        await BulkInsertAsync("legacy", EventsEndingAt(Now, 20_000, TimeSpan.FromSeconds(1), new string('x', 512)), cancellationToken);
        var sizeBefore = await GetDatabaseSizeAsync(cancellationToken);

        var deletedCount = await Storage.PurgeOldEventsAsync(TimeSpan.Zero, maxEventsPerMonitor: 100, cancellationToken);
        var sizeAfter = await GetDatabaseSizeAsync(cancellationToken);

        Assert.Equal(19_900, deletedCount);
        Assert.True(sizeAfter < sizeBefore / 10, $"Expected the file to shrink by an order of magnitude, but it went from {sizeBefore} to {sizeAfter} bytes.");
        Assert.Equal(2L, await ExecuteScalarAsync<long>("PRAGMA auto_vacuum;", cancellationToken));
    }
}
