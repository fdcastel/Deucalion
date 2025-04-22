using Deucalion.Storage;
using Microsoft.Data.Sqlite;

namespace Deucalion.Tests.Storage;

public abstract class SqliteStorageTestBase : IDisposable
{
    protected readonly string StoragePath;
    protected readonly string DbFilePath;
    protected readonly SqliteStorage Storage;

    protected SqliteStorageTestBase()
    {
        // Use a unique path for each test run to avoid conflicts
        StoragePath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.SqliteStorage_{Guid.NewGuid()}");
        Directory.CreateDirectory(StoragePath);
        DbFilePath = Path.Combine(StoragePath, "deucalion.sqlite.db"); // Construct the full path
        Storage = new SqliteStorage(StoragePath);
    }

    protected async Task<(long? LastSeenUpTicks, long? LastSeenDownTicks)> GetLastStateChangeTimestampsAsync(string monitorName)
    {
        // Use the known path to the test database file
        using var connection = new SqliteConnection($"Data Source={DbFilePath}");
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

            // Attempt to delete the directory. Retry logic might be needed if file locks persist briefly.
            try
            {
                if (Directory.Exists(StoragePath))
                {
                    Directory.Delete(StoragePath, true);
                }
            }
            catch (IOException ex)
            {
                // Log or handle the exception if deletion fails (e.g., file still locked)
                Console.WriteLine($"Warning: Could not delete test directory {StoragePath}. {ex.Message}");
            }
        }
    }
}
