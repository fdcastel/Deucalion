using Microsoft.Data.Sqlite;

namespace Deucalion.Storage;

public sealed class SqliteStorage : IStorage, IDisposable
{
    private const string EventsTableName = "Events";

    private readonly string _connectionString;
    private readonly string _dbFile;

    public SqliteStorage(string? storagePath = null)
    {
        var dbPath = storagePath ?? Path.Combine(Path.GetTempPath(), "Deucalion");
        // Ensure the directory exists
        Directory.CreateDirectory(dbPath);

        _dbFile = Path.Combine(dbPath, "deucalion.sqlite.db"); // Store the full path
        // Microsoft.Data.Sqlite pools connections by default (Pooling=True), and InitializeAsync
        // switches the database to WAL. Do not add Cache=Shared: it is SQLite shared-cache mode,
        // not pooling, and it replaces WAL's file-level locking with in-process table locks that
        // serialise readers against the writer and can raise SQLITE_LOCKED, which busy_timeout
        // does not retry (#21).
        _connectionString = $"Data Source={_dbFile};Mode=ReadWriteCreate";
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            -- Enable Write-Ahead Logging for better concurrency
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS {EventsTableName} (
                MonitorName TEXT NOT NULL,
                TimestampTicks INTEGER NOT NULL,
                State INTEGER NOT NULL,
                ResponseTimeTicks INTEGER NULL,
                ResponseText TEXT NULL,
                PRIMARY KEY (MonitorName, TimestampTicks)
            );

            CREATE INDEX IF NOT EXISTS IX_{EventsTableName}_MonitorName_TimestampTicks
            ON {EventsTableName} (MonitorName, TimestampTicks DESC);

            -- Was written on every state change but never read by any client; the UI derives
            -- incident runs from the event window it already has. Dropped so existing databases
            -- do not keep an orphan table around.
            DROP TABLE IF EXISTS MonitorStateChanges;
        """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<MonitorStats?> GetStatsAsync(string monitorName, int historyCount = 60, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        MonitorState? lastEventState = null;
        long relevantEventCount = 0;
        long downEventCount = 0;

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                WITH LastEvent AS (
                    SELECT State
                    FROM {EventsTableName}
                    WHERE MonitorName = @MonitorName
                    ORDER BY TimestampTicks DESC
                    LIMIT 1
                ),
                RecentEvents AS (
                    SELECT State, ResponseTimeTicks
                    FROM {EventsTableName}
                    WHERE MonitorName = @MonitorName
                    ORDER BY TimestampTicks DESC
                    LIMIT @HistoryCount
                )
                SELECT
                    (SELECT State FROM LastEvent) AS LastEventState,
                    (SELECT COALESCE(SUM(CASE WHEN State IN ({(int)MonitorState.Down}, {(int)MonitorState.Up}, {(int)MonitorState.Warn}, {(int)MonitorState.Degraded}) THEN 1 ELSE 0 END), 0) FROM RecentEvents) AS RelevantEventCount,
                    (SELECT COALESCE(SUM(CASE WHEN State = {(int)MonitorState.Down} THEN 1 ELSE 0 END), 0) FROM RecentEvents) AS DownEventCount;
            """;
            command.Parameters.AddWithValue("@MonitorName", monitorName);
            command.Parameters.AddWithValue("@HistoryCount", historyCount);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                lastEventState = reader.IsDBNull(0) ? (MonitorState?)null : (MonitorState)reader.GetInt64(0);
                relevantEventCount = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                downEventCount = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
            }
        }

        // Pull the recent response times so we can compute average + percentiles in C#.
        // Restricted to Up/Warn: a Down probe's recorded elapsed (e.g. PingMonitor's
        // synchronous OS-level failure timing of 0 ms, or HTTP 5xx returned quickly)
        // isn't a healthy-latency sample and would skew percentiles toward zero.
        var responseTimes = new List<long>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT ResponseTimeTicks
                FROM {EventsTableName}
                WHERE MonitorName = @MonitorName
                  AND ResponseTimeTicks IS NOT NULL
                  AND State IN ({(int)MonitorState.Up}, {(int)MonitorState.Warn})
                ORDER BY TimestampTicks DESC
                LIMIT @HistoryCount;
            """;
            command.Parameters.AddWithValue("@MonitorName", monitorName);
            command.Parameters.AddWithValue("@HistoryCount", historyCount);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                responseTimes.Add(reader.GetInt64(0));
            }
        }

        if (!lastEventState.HasValue)
        {
            // No events at all for this monitor.
            return null;
        }

        // Calculate final stats
        var availability = 100.0;
        if (relevantEventCount > 0)
        {
            var availableCount = relevantEventCount - downEventCount;
            availability = 100.0 * availableCount / relevantEventCount;
        }

        TimeSpan? minResponseTime = null;
        TimeSpan? p50 = null;
        TimeSpan? p95 = null;
        TimeSpan? p99 = null;

        if (responseTimes.Count > 0)
        {
            var sorted = responseTimes.ToArray();
            Array.Sort(sorted);
            minResponseTime = TimeSpan.FromTicks(sorted[0]);
            p50 = TimeSpan.FromTicks(Percentile(sorted, 0.50));
            p95 = TimeSpan.FromTicks(Percentile(sorted, 0.95));
            p99 = TimeSpan.FromTicks(Percentile(sorted, 0.99));
        }

        return new MonitorStats(
            LastState: lastEventState.Value,
            Availability: availability,
            MinResponseTime: minResponseTime,
            Latency50: p50,
            Latency95: p95,
            Latency99: p99,
            SampleCount: responseTimes.Count
        );
    }

    // Nearest-rank percentile on a pre-sorted array.
    private static long Percentile(long[] sortedValues, double p)
    {
        var rank = (int)Math.Ceiling(p * sortedValues.Length);
        if (rank < 1) rank = 1;
        if (rank > sortedValues.Length) rank = sortedValues.Length;
        return sortedValues[rank - 1];
    }

    public async Task<IEnumerable<StoredEvent>> GetLastEventsAsync(string monitorName, int count = 60, CancellationToken cancellationToken = default)
    {
        var results = new List<StoredEvent>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT TimestampTicks, State, ResponseTimeTicks, ResponseText
                FROM {EventsTableName}
                WHERE MonitorName = @MonitorName
                ORDER BY TimestampTicks DESC
                LIMIT @Count;
            """;
            command.Parameters.AddWithValue("@MonitorName", monitorName);
            command.Parameters.AddWithValue("@Count", count);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new StoredEvent(
                    At: new DateTimeOffset(reader.GetInt64(0), TimeSpan.Zero),
                    State: (MonitorState)reader.GetInt64(1),
                    ResponseTime: reader.IsDBNull(2) ? null : new TimeSpan(reader.GetInt64(2)),
                    ResponseText: reader.IsDBNull(3) ? null : reader.GetString(3)
                ));
            }
        }
        return results;
    }

    public async Task SaveEventAsync(string monitorName, StoredEvent storedEvent, CancellationToken cancellationToken = default)
    {
        // Create connection per operation
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        // Upsert: (MonitorName, TimestampTicks) is the primary key, and DateTimeOffset.UtcNow's
        // ~15.6ms granularity on Windows means two rapid probes -- e.g. back-to-back check-ins,
        // which short-circuit the poll delay -- can share a timestamp. A plain INSERT threw
        // SqliteException there, and the event was logged as an error and dropped.
        command.CommandText = $"""
            INSERT INTO {EventsTableName} (MonitorName, TimestampTicks, State, ResponseTimeTicks, ResponseText)
            VALUES (@MonitorName, @TimestampTicks, @State, @ResponseTimeTicks, @ResponseText)
            ON CONFLICT(MonitorName, TimestampTicks) DO UPDATE SET
                State = excluded.State,
                ResponseTimeTicks = excluded.ResponseTimeTicks,
                ResponseText = excluded.ResponseText;
        """;
        command.Parameters.AddWithValue("@MonitorName", monitorName);
        command.Parameters.AddWithValue("@TimestampTicks", storedEvent.At.UtcTicks);
        command.Parameters.AddWithValue("@State", (int)storedEvent.State);
        command.Parameters.AddWithValue("@ResponseTimeTicks", storedEvent.ResponseTime?.Ticks ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ResponseText", storedEvent.ResponseText ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes events older than the specified retention period.
    /// </summary>
    /// <param name="retentionPeriod">The maximum age of events to keep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    public async Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        // If retention is zero or negative, nothing should be purged.
        if (retentionPeriod <= TimeSpan.Zero)
        {
            return 0;
        }

        var cutoffTimestamp = DateTimeOffset.UtcNow - retentionPeriod;
        var cutoffTicks = cutoffTimestamp.UtcTicks;

        // Create connection per operation
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            DELETE FROM {EventsTableName}
            WHERE TimestampTicks < @CutoffTicks;
        """;
        command.Parameters.AddWithValue("@CutoffTicks", cutoffTicks);

        // The caller (PurgeBackgroundService) logs the outcome with a real logger.
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Releases the pooled connections, and with them the database file handle. There are no
    /// unmanaged resources, so no finalizer or disposal flag is needed.
    /// </summary>
    public void Dispose()
    {
        using var connection = new SqliteConnection(_connectionString);
        SqliteConnection.ClearPool(connection);
    }
}
