using Microsoft.Data.Sqlite;

namespace Deucalion.Storage;

public sealed class SqliteStorage : IStorage, IDisposable
{
    private const string EventsTableName = "Events";

    /// <summary>
    /// Rows deleted per statement by <see cref="PurgeOldEventsAsync(TimeSpan, int, CancellationToken)"/>.
    /// Each chunk is its own implicit transaction, so the write lock is released between chunks
    /// and the engine keeps saving events while a large backlog is purged.
    /// </summary>
    public const int PurgeChunkSize = 10_000;

    private readonly string _connectionString;
    private readonly string _dbFile;
    private readonly TimeProvider _timeProvider;

    public SqliteStorage(string? storagePath = null, TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;

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
            -- Lets the purge hand free pages back to the OS with 'PRAGMA incremental_vacuum'
            -- instead of a full VACUUM. Only takes effect on a database that has no tables yet;
            -- older databases are converted once by the first purge that deletes rows.
            PRAGMA auto_vacuum=INCREMENTAL;

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
    /// Deletes events older than <paramref name="retentionPeriod"/>. No per-monitor row cap.
    /// </summary>
    public Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default) =>
        PurgeOldEventsAsync(retentionPeriod, maxEventsPerMonitor: 0, cancellationToken);

    /// <summary>
    /// Deletes events older than <paramref name="retentionPeriod"/> and, per monitor, any beyond
    /// the newest <paramref name="maxEventsPerMonitor"/>; then returns the freed pages to the OS.
    /// </summary>
    /// <param name="retentionPeriod">The maximum age of events to keep; zero or negative disables the age purge.</param>
    /// <param name="maxEventsPerMonitor">Newest events kept per monitor; zero or negative disables the cap.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <remarks>
    /// "Now" comes from the injected <see cref="TimeProvider"/>. Rows go in chunks of
    /// <see cref="PurgeChunkSize"/> per monitor (both queries walk the primary-key index), so a
    /// first purge of a large backlog is not one unbounded DELETE holding the write lock (#23).
    /// </remarks>
    public async Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, int maxEventsPerMonitor, CancellationToken cancellationToken = default)
    {
        var purgeByAge = retentionPeriod > TimeSpan.Zero;
        var purgeByCount = maxEventsPerMonitor > 0;
        if (!purgeByAge && !purgeByCount)
        {
            return 0;
        }

        var cutoffTicks = (_timeProvider.GetUtcNow() - retentionPeriod).UtcTicks;

        // Create connection per operation
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var deleted = 0;
        foreach (var monitorName in await ListMonitorNamesAsync(connection, cancellationToken))
        {
            if (purgeByAge)
            {
                deleted += await DeleteInChunksAsync(connection, $"""
                    DELETE FROM {EventsTableName}
                    WHERE rowid IN (
                        SELECT rowid FROM {EventsTableName}
                        WHERE MonitorName = @MonitorName AND TimestampTicks < @CutoffTicks
                        ORDER BY TimestampTicks
                        LIMIT @ChunkSize);
                    """, monitorName, ("@CutoffTicks", cutoffTicks), cancellationToken);
            }

            if (purgeByCount)
            {
                deleted += await DeleteInChunksAsync(connection, $"""
                    DELETE FROM {EventsTableName}
                    WHERE rowid IN (
                        SELECT rowid FROM {EventsTableName}
                        WHERE MonitorName = @MonitorName
                        ORDER BY TimestampTicks DESC
                        LIMIT @ChunkSize OFFSET @MaxEvents);
                    """, monitorName, ("@MaxEvents", maxEventsPerMonitor), cancellationToken);
            }
        }

        if (deleted > 0)
        {
            await ReclaimSpaceAsync(connection, cancellationToken);
        }

        // The caller (PurgeBackgroundService) logs the outcome with a real logger.
        return deleted;
    }

    private static async Task<List<string>> ListMonitorNamesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var names = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT DISTINCT MonitorName FROM {EventsTableName};";
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static async Task<int> DeleteInChunksAsync(
        SqliteConnection connection, string sql, string monitorName, (string Name, long Value) parameter, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@MonitorName", monitorName);
        command.Parameters.AddWithValue("@ChunkSize", PurgeChunkSize);
        command.Parameters.AddWithValue(parameter.Name, parameter.Value);

        var total = 0;
        int chunk;
        do
        {
            chunk = await command.ExecuteNonQueryAsync(cancellationToken);
            total += chunk;
        } while (chunk == PurgeChunkSize);

        return total;
    }

    /// <summary>
    /// Gives deleted pages back to the file system. Without this the database never shrank:
    /// tightening the retention reclaimed zero disk (#23).
    /// </summary>
    private static async Task ReclaimSpaceAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // 0 = NONE, 1 = FULL, 2 = INCREMENTAL.
        long autoVacuum;
        using (var query = connection.CreateCommand())
        {
            query.CommandText = "PRAGMA auto_vacuum;";
            autoVacuum = (long)(await query.ExecuteScalarAsync(cancellationToken))!;
        }

        using var command = connection.CreateCommand();
        command.CommandText = autoVacuum switch
        {
            // Frees every page on the freelist and truncates the file. Cheap: no rewrite.
            2 => "PRAGMA incremental_vacuum;",
            // FULL already shrinks the file on every commit.
            1 => "",
            // Database created before InitializeAsync set auto_vacuum: switching away from NONE
            // needs one full VACUUM (a rewrite). Later purges take the incremental path.
            _ => "PRAGMA auto_vacuum=INCREMENTAL; VACUUM;",
        };
        if (command.CommandText.Length > 0)
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // In WAL mode the truncation lives in the log until a checkpoint copies it into the main
        // file; TRUNCATE also resets the log, which a VACUUM can leave as large as the database.
        // A busy checkpoint (concurrent reader) is not an error: the next automatic one finishes it.
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteNonQueryAsync(cancellationToken);
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
