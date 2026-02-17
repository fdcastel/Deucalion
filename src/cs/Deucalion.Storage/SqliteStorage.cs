using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Deucalion.Storage;

public class SqliteStorage : IDisposable // Add IDisposable
{
    private const string EventsTableName = "Events";
    private const string MonitorStateChangesTableName = "MonitorStateChanges";

    private readonly string _connectionString;
    private readonly string _dbFile;

    public SqliteStorage(string? storagePath = null)
    {
        var dbPath = storagePath ?? Path.Combine(Path.GetTempPath(), "Deucalion");
        // Ensure the directory exists
        Directory.CreateDirectory(dbPath);

        _dbFile = Path.Combine(dbPath, "deucalion.sqlite.db"); // Store the full path
        // Enable connection pooling (Cache=Shared) and WAL mode for better concurrency
        _connectionString = $"Data Source={_dbFile};Mode=ReadWriteCreate;Cache=Shared";
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

            CREATE TABLE IF NOT EXISTS {MonitorStateChangesTableName} (
                MonitorName TEXT PRIMARY KEY,
                LastSeenUpTicks INTEGER NULL,
                LastSeenDownTicks INTEGER NULL
            );
        """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<MonitorStats?> GetStatsAsync(string monitorName, int historyCount = 60, CancellationToken cancellationToken = default)
    {
        DateTimeOffset? lastSeenUp = null;
        DateTimeOffset? lastSeenDown = null;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Get the last seen state
        using (var cmdLastSeen = connection.CreateCommand())
        {
            cmdLastSeen.CommandText = $"""
                SELECT LastSeenUpTicks, LastSeenDownTicks
                FROM {MonitorStateChangesTableName}
                WHERE MonitorName = @MonitorName;
            """;
            cmdLastSeen.Parameters.AddWithValue("@MonitorName", monitorName);

            using var readerLastSeen = await cmdLastSeen.ExecuteReaderAsync(cancellationToken);
            if (await readerLastSeen.ReadAsync(cancellationToken))
            {
                var lastSeenUpTicks = readerLastSeen.IsDBNull(0) ? (long?)null : readerLastSeen.GetInt64(0);
                var lastSeenDownTicks = readerLastSeen.IsDBNull(1) ? (long?)null : readerLastSeen.GetInt64(1);
                if (lastSeenUpTicks.HasValue) lastSeenUp = new DateTimeOffset(lastSeenUpTicks.Value, TimeSpan.Zero);
                if (lastSeenDownTicks.HasValue) lastSeenDown = new DateTimeOffset(lastSeenDownTicks.Value, TimeSpan.Zero);
            }
        }

        // Get the latest event details
        long? lastEventTimestampTicks = null;
        MonitorState? lastEventState = null;
        using (var cmdLastEvent = connection.CreateCommand())
        {
            cmdLastEvent.CommandText = $"""
                SELECT TimestampTicks, State
                FROM {EventsTableName}
                WHERE MonitorName = @MonitorName
                ORDER BY TimestampTicks DESC
                LIMIT 1;
            """;
            cmdLastEvent.Parameters.AddWithValue("@MonitorName", monitorName);

            using var readerLastEvent = await cmdLastEvent.ExecuteReaderAsync(cancellationToken);
            if (await readerLastEvent.ReadAsync(cancellationToken))
            {
                lastEventTimestampTicks = readerLastEvent.GetInt64(0);
                lastEventState = (MonitorState)readerLastEvent.GetInt64(1);
            }
        }

        if (!lastEventTimestampTicks.HasValue || !lastEventState.HasValue)
        {
            // There are no events at all. Return based on LastSeenUp/Down or null
            return (lastSeenUp.HasValue || lastSeenDown.HasValue)
                ? new MonitorStats(MonitorState.Unknown, DateTimeOffset.MinValue, 0, TimeSpan.Zero, lastSeenDown, lastSeenUp)
                : null;
        }

        // Get the aggregate stats for recent events
        double? averageResponseTimeTicks = null;
        long relevantEventCount = 0;
        long downEventCount = 0;
        using (var cmdAggStats = connection.CreateCommand())
        {
            cmdAggStats.CommandText = $"""
                WITH RecentEvents AS (
                    SELECT State, ResponseTimeTicks
                    FROM {EventsTableName}
                    WHERE MonitorName = @MonitorName
                    ORDER BY TimestampTicks DESC
                    LIMIT @HistoryCount
                )
                SELECT
                    AVG(CAST(ResponseTimeTicks AS REAL)) as AverageResponseTimeTicks, -- Cast needed for AVG with potential NULLs
                    SUM(CASE WHEN State IN ({(int)MonitorState.Down}, {(int)MonitorState.Up}, {(int)MonitorState.Warn}, {(int)MonitorState.Degraded}) THEN 1 ELSE 0 END) as RelevantEventCount,
                    SUM(CASE WHEN State = {(int)MonitorState.Down} THEN 1 ELSE 0 END) as DownEventCount
                FROM RecentEvents;
            """;
            cmdAggStats.Parameters.AddWithValue("@MonitorName", monitorName);
            cmdAggStats.Parameters.AddWithValue("@HistoryCount", historyCount);

            using var readerAggStats = await cmdAggStats.ExecuteReaderAsync(cancellationToken);
            if (await readerAggStats.ReadAsync(cancellationToken))
            {
                averageResponseTimeTicks = readerAggStats.IsDBNull(0) ? (double?)null : readerAggStats.GetDouble(0);
                relevantEventCount = readerAggStats.GetInt64(1);
                downEventCount = readerAggStats.GetInt64(2);
            }
        }

        // Calculate final stats
        var availability = 100.0;
        if (relevantEventCount > 0)
        {
            var availableCount = relevantEventCount - downEventCount;
            availability = 100.0 * availableCount / relevantEventCount;
        }

        var averageResponseTime = averageResponseTimeTicks.HasValue
            ? TimeSpan.FromTicks((long)averageResponseTimeTicks.Value)
            : TimeSpan.Zero;

        return new MonitorStats(
            LastState: lastEventState.Value,
            LastUpdate: new DateTimeOffset(lastEventTimestampTicks.Value, TimeSpan.Zero),
            Availability: availability,
            AverageResponseTime: averageResponseTime,
            LastSeenDown: lastSeenDown,
            LastSeenUp: lastSeenUp
        );
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
        command.CommandText = $"""
            INSERT INTO {EventsTableName} (MonitorName, TimestampTicks, State, ResponseTimeTicks, ResponseText)
            VALUES (@MonitorName, @TimestampTicks, @State, @ResponseTimeTicks, @ResponseText);
        """;
        command.Parameters.AddWithValue("@MonitorName", monitorName);
        command.Parameters.AddWithValue("@TimestampTicks", storedEvent.At.UtcTicks);
        command.Parameters.AddWithValue("@State", (int)storedEvent.State);
        command.Parameters.AddWithValue("@ResponseTimeTicks", storedEvent.ResponseTime?.Ticks ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ResponseText", storedEvent.ResponseText ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveLastStateChangeAsync(string monitorName, DateTimeOffset at, MonitorState state, CancellationToken cancellationToken = default)
    {
        if (state != MonitorState.Up && state != MonitorState.Down)
        {
            return;
        }

        // Create connection per operation
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {MonitorStateChangesTableName} (MonitorName, LastSeenUpTicks, LastSeenDownTicks)
            VALUES (@MonitorName,
                    CASE WHEN @State = {(int)MonitorState.Up} THEN @TimestampTicks ELSE NULL END,
                    CASE WHEN @State = {(int)MonitorState.Down} THEN @TimestampTicks ELSE NULL END)
            ON CONFLICT(MonitorName) DO
                UPDATE SET
                    LastSeenUpTicks = CASE WHEN @State = {(int)MonitorState.Up} THEN @TimestampTicks
                                        ELSE LastSeenUpTicks -- Use existing value
                                    END,
                    LastSeenDownTicks = CASE WHEN @State = {(int)MonitorState.Down} THEN @TimestampTicks
                                            ELSE LastSeenDownTicks -- Use existing value
                                        END
                WHERE @State = {(int)MonitorState.Up} OR @State = {(int)MonitorState.Down};
        """;
        command.Parameters.AddWithValue("@MonitorName", monitorName);
        command.Parameters.AddWithValue("@TimestampTicks", at.UtcTicks);
        command.Parameters.AddWithValue("@State", (int)state);
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

        var stopwatch = Stopwatch.StartNew();
        var deletedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        stopwatch.Stop();

        // Optional: Log the operation details
        // Consider injecting ILogger if logging is desired here
        Debug.WriteLine($"Purged {deletedRows} events older than {cutoffTimestamp:O} ({retentionPeriod}) in {stopwatch.ElapsedMilliseconds}ms.");

        return deletedRows;
    }

    #region IDisposable
    private bool _disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clear the connection pool associated with this specific database file.
                // This is important for releasing file locks when Cache=Shared is used.
                SqliteConnection.ClearAllPools();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // No unmanaged resources currently, but good practice to have the structure.

            _disposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
