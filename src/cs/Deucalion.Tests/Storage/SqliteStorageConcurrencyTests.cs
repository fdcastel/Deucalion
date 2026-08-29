using Deucalion.Storage;
using Xunit;

namespace Deucalion.Tests.Storage;

/// <summary>
/// Regression for #21: the connection string used Cache=Shared (SQLite shared-cache mode, not
/// ADO.NET pooling as its comment claimed). Shared cache replaces WAL's file locking with
/// in-process table locks, and can surface SQLITE_LOCKED under concurrent read + write load.
/// This is the load the daemon actually runs: the engine writes events while the API and SSE
/// clients read stats, and the purge service deletes.
/// </summary>
public class SqliteStorageConcurrencyTests : SqliteStorageTestBase
{
    [Fact]
    public async Task Issue21_ConcurrentWritesReadsAndPurges_DoNotThrow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const int writers = 4;
        const int readers = 4;
        const int iterations = 200;

        var start = DateTimeOffset.UtcNow;

        var tasks = new List<Task>();

        for (var w = 0; w < writers; w++)
        {
            var monitorName = $"monitor-{w}";
            tasks.Add(Task.Run(async () =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    // Distinct timestamps so every iteration is a real insert, not an upsert.
                    var storedEvent = new StoredEvent(start.AddMilliseconds(i), MonitorState.Up, TimeSpan.FromMilliseconds(i), null);
                    await Storage.SaveEventAsync(monitorName, storedEvent, cancellationToken);
                }
            }, cancellationToken));
        }

        for (var r = 0; r < readers; r++)
        {
            var monitorName = $"monitor-{r % writers}";
            tasks.Add(Task.Run(async () =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    await Storage.GetStatsAsync(monitorName, cancellationToken: cancellationToken);
                    await Storage.GetLastEventsAsync(monitorName, cancellationToken: cancellationToken);
                }
            }, cancellationToken));
        }

        tasks.Add(Task.Run(async () =>
        {
            for (var i = 0; i < iterations; i++)
            {
                // A retention of one tick purges everything written so far, so the delete
                // genuinely contends with the inserts instead of being a no-op.
                await Storage.PurgeOldEventsAsync(TimeSpan.FromTicks(1), cancellationToken);
            }
        }, cancellationToken));

        // Task.WhenAll surfaces the first SqliteException from any participant.
        await Task.WhenAll(tasks);

        // The database is still consistent and usable afterwards.
        var storedEvent = new StoredEvent(DateTimeOffset.UtcNow, MonitorState.Down, null, "after");
        await Storage.SaveEventAsync("monitor-0", storedEvent, cancellationToken);
        var stats = await Storage.GetStatsAsync("monitor-0", cancellationToken: cancellationToken);
        Assert.NotNull(stats);
        Assert.Equal(MonitorState.Down, stats.LastState);
    }
}
