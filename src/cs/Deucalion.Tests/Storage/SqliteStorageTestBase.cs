using Deucalion.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Deucalion.Tests.Storage;

public abstract class SqliteStorageTestBase : IAsyncLifetime, IDisposable
{
    protected readonly string StoragePath;
    protected readonly string DbFilePath;
    protected readonly SqliteStorage Storage;
    private readonly string _directConnectionString;

    protected SqliteStorageTestBase()
    {
        // Use a unique path for each test run to avoid conflicts
        StoragePath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.SqliteStorage_{Guid.NewGuid()}");
        Directory.CreateDirectory(StoragePath);
        DbFilePath = Path.Combine(StoragePath, "deucalion.sqlite.db"); // Construct the full path
        _directConnectionString = $"Data Source={DbFilePath};Pooling=False";
        Storage = new SqliteStorage(StoragePath);
    }

    public Task InitializeAsync() => Storage.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task<(long? LastSeenUpTicks, long? LastSeenDownTicks)> GetLastStateChangeTimestampsAsync(string monitorName)
    {
        // Use the known path to the test database file
        using var connection = new SqliteConnection(_directConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT LastSeenUpTicks, LastSeenDownTicks
            FROM MonitorStateChanges
            WHERE MonitorName = @MonitorName;";
        command.Parameters.AddWithValue("@MonitorName", monitorName);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var upTicks = reader.IsDBNull(0) ? (long?)null : reader.GetInt64(0);
            var downTicks = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
            return (upTicks, downTicks);
        }
        return (null, null); // No record found
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Explicitly close and dispose the storage to release the file lock
            (Storage as IDisposable)?.Dispose();

            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (Directory.Exists(StoragePath))
                    {
                        Directory.Delete(StoragePath, true);
                    }

                    break;
                }
                catch (IOException ex) when (attempt < maxAttempts)
                {
                    Console.WriteLine($"Retrying test directory cleanup for {StoragePath} (attempt {attempt}/{maxAttempts}): {ex.Message}");
                    Thread.Sleep(50 * attempt);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Warning: Could not delete test directory {StoragePath}. {ex.Message}");
                    break;
                }
            }
        }
    }
}
