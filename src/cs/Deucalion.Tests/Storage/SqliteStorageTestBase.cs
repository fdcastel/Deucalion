using Deucalion.Storage;
using Xunit;

namespace Deucalion.Tests.Storage;

public abstract class SqliteStorageTestBase : IAsyncLifetime, IDisposable
{
    protected readonly string StoragePath;
    protected readonly string DbFilePath;
    protected readonly SqliteStorage Storage;

    protected SqliteStorageTestBase()
    {
        // A unique directory per test instance, so tests never share a database.
        StoragePath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.SqliteStorage_{Guid.NewGuid()}");
        Directory.CreateDirectory(StoragePath);
        DbFilePath = Path.Combine(StoragePath, "deucalion.sqlite.db");
        Storage = new SqliteStorage(StoragePath);
    }

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
}
