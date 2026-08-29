using Deucalion.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Deucalion.Tests.Storage;

public abstract class SqliteStorageTestBase : IAsyncLifetime, IDisposable
{
    protected readonly string StoragePath;
    protected readonly string DbFilePath;
    protected readonly SqliteStorage Storage;

    /// <param name="timeProvider">The storage's clock; defaults to the system clock.</param>
    protected SqliteStorageTestBase(TimeProvider? timeProvider = null)
    {
        // A unique directory per test instance, so tests never share a database.
        StoragePath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.SqliteStorage_{Guid.NewGuid()}");
        Directory.CreateDirectory(StoragePath);
        DbFilePath = Path.Combine(StoragePath, "deucalion.sqlite.db");
        Storage = new SqliteStorage(StoragePath, timeProvider);
    }

    // Unpooled: a pooled connection keeps reporting the auto_vacuum mode it first saw, which
    // would make assertions about a mode changed through another connection lie.
    private string HelperConnectionString => $"Data Source={DbFilePath};Pooling=False";

    public ValueTask InitializeAsync() => new(Storage.InitializeAsync(TestContext.Current.CancellationToken));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        // Dispose the storage first -- it clears the connection pool, which releases the
        // database file handle before we try to delete the directory.
        Storage.Dispose();

        TestPaths.DeleteWithRetry(StoragePath);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Inserts many events in one transaction. SaveEventAsync opens a connection and commits per
    /// row, which is what the engine needs but far too slow for tests that want tens of
    /// thousands of rows.
    /// </summary>
    protected async Task BulkInsertAsync(string monitorName, IEnumerable<StoredEvent> events, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(HelperConnectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Events (MonitorName, TimestampTicks, State, ResponseTimeTicks, ResponseText)
            VALUES (@MonitorName, @TimestampTicks, @State, @ResponseTimeTicks, @ResponseText);
            """;
        var timestamp = command.Parameters.Add("@TimestampTicks", SqliteType.Integer);
        var state = command.Parameters.Add("@State", SqliteType.Integer);
        var responseTime = command.Parameters.Add("@ResponseTimeTicks", SqliteType.Integer);
        var responseText = command.Parameters.Add("@ResponseText", SqliteType.Text);
        command.Parameters.AddWithValue("@MonitorName", monitorName);

        foreach (var e in events)
        {
            timestamp.Value = e.At.UtcTicks;
            state.Value = (int)e.State;
            responseTime.Value = e.ResponseTime?.Ticks ?? (object)DBNull.Value;
            responseText.Value = e.ResponseText ?? (object)DBNull.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    protected async Task<long> CountEventsAsync(string monitorName, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(HelperConnectionString);
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Events WHERE MonitorName = @MonitorName;";
        command.Parameters.AddWithValue("@MonitorName", monitorName);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>
    /// Size of the database on disk. Checkpoints first, so what is still in the WAL is folded into
    /// the main file and the number reflects what the file system will keep.
    /// </summary>
    protected async Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(HelperConnectionString);
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new FileInfo(DbFilePath).Length;
    }

    protected async Task<T> ExecuteScalarAsync<T>(string sql, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(HelperConnectionString);
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
