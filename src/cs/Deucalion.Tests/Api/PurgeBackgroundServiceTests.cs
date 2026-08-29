using Deucalion.Api.Options;
using Deucalion.Api.Services;
using Deucalion.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Deucalion.Tests.Api;

public class PurgeBackgroundServiceTests
{
    /// <summary>
    /// Records what it was asked to purge, so the test asserts on the service's scheduling
    /// rather than on SQLite. Only PurgeOldEventsAsync is exercised.
    /// </summary>
    private sealed class RecordingStorage : IStorage
    {
        private readonly TaskCompletionSource _firstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public List<TimeSpan> Retentions { get; } = [];
        public List<int> MaxEventsPerMonitor { get; } = [];
        public Task FirstCall => _firstCall.Task;
        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>Set to have the next purge throw, to check the loop survives it.</summary>
        public bool ThrowOnPurge { get; set; }

        public Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, int maxEventsPerMonitor, CancellationToken cancellationToken = default)
        {
            lock (Retentions)
            {
                Retentions.Add(retentionPeriod);
                MaxEventsPerMonitor.Add(maxEventsPerMonitor);
            }

            Interlocked.Increment(ref _callCount);
            _firstCall.TrySetResult();

            return ThrowOnPurge
                ? throw new InvalidOperationException("database is busy")
                : Task.FromResult(0);
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<MonitorStats?> GetStatsAsync(string monitorName, int historyCount = 60, CancellationToken cancellationToken = default) => Task.FromResult<MonitorStats?>(null);
        public Task<MonitorRun?> GetCurrentRunAsync(string monitorName, CancellationToken cancellationToken = default) => Task.FromResult<MonitorRun?>(null);
        public Task<IEnumerable<StoredEvent>> GetLastEventsAsync(string monitorName, int count = 60, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<StoredEvent>>([]);
        public Task SaveEventAsync(string monitorName, StoredEvent storedEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static (PurgeBackgroundService Service, RecordingStorage Storage, FakeTimeProvider Time) Build(
        TimeSpan? purgeInterval = null,
        TimeSpan? retention = null,
        int maxEventsPerMonitor = 100_000)
    {
        var storage = new RecordingStorage();
        var options = new DeucalionOptions
        {
            PurgeInterval = purgeInterval ?? TimeSpan.FromHours(24),
            EventRetentionPeriod = retention ?? TimeSpan.FromDays(30),
            MaxEventsPerMonitor = maxEventsPerMonitor,
        };
        var time = new FakeTimeProvider();
        var service = new PurgeBackgroundService(storage, options, time, NullLogger<PurgeBackgroundService>.Instance);
        return (service, storage, time);
    }

    /// <summary>
    /// Advances the fake clock until at least <paramref name="count"/> purges have been recorded.
    /// </summary>
    /// <remarks>
    /// The advance is retried because the service may not have reached
    /// <c>WaitForNextTickAsync</c> when the first one lands -- a real race, and a single Advance
    /// would simply be missed. Retrying keeps the assertion deterministic without a sleep, at the
    /// cost of moving virtual time by an unknown number of intervals, so callers assert
    /// "at least N purges" rather than an exact count.
    /// </remarks>
    private static async Task AdvanceUntilPurgesAsync(
        FakeTimeProvider time, TimeSpan interval, RecordingStorage storage, int count, CancellationToken cancellationToken)
    {
        while (storage.CallCount < count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            time.Advance(interval);

            // Give the timer continuation a chance to run before advancing again.
            for (var i = 0; i < 20 && storage.CallCount < count; i++)
            {
                await Task.Yield();
            }
        }
    }

    [Fact]
    public async Task PurgesOnceImmediatelyAtStartup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (service, storage, _) = Build(retention: TimeSpan.FromDays(7), maxEventsPerMonitor: 1234);

        await service.StartAsync(cancellationToken);
        await storage.FirstCall.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        Assert.Equal(1, storage.CallCount);
        Assert.Equal(TimeSpan.FromDays(7), storage.Retentions[0]);
        Assert.Equal(1234, storage.MaxEventsPerMonitor[0]); // The row cap reaches the storage (#23).

        await service.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task PurgesAgainOnEveryInterval()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        var interval = TimeSpan.FromHours(24);
        var (service, storage, time) = Build(purgeInterval: interval);

        await service.StartAsync(cancellationToken);
        await storage.FirstCall.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        // Further ticks come from the virtual clock -- no real waiting.
        await AdvanceUntilPurgesAsync(time, interval, storage, 2, timeout.Token);
        await AdvanceUntilPurgesAsync(time, interval, storage, 3, timeout.Token);

        Assert.True(storage.CallCount >= 3, $"Expected at least 3 purges, got {storage.CallCount}.");
        Assert.All(storage.Retentions, r => Assert.Equal(TimeSpan.FromDays(30), r));

        await service.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task DoesNotPurgeBeforeTheIntervalElapses()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        var interval = TimeSpan.FromHours(24);
        var (service, storage, time) = Build(purgeInterval: interval);

        await service.StartAsync(cancellationToken);
        await storage.FirstCall.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        // Drive one real tick first, so we know the timer is armed -- otherwise the
        // assertion below would pass vacuously.
        await AdvanceUntilPurgesAsync(time, interval, storage, 2, timeout.Token);
        var armed = storage.CallCount;

        // Just short of the next interval: no further purge.
        time.Advance(interval - TimeSpan.FromMinutes(1));
        for (var i = 0; i < 50; i++)
        {
            await Task.Yield();
        }

        Assert.Equal(armed, storage.CallCount);

        await service.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task AFailingPurgeIsLoggedAndTheLoopContinues()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        var interval = TimeSpan.FromHours(1);
        var (service, storage, time) = Build(purgeInterval: interval);
        storage.ThrowOnPurge = true;

        await service.StartAsync(cancellationToken);
        await storage.FirstCall.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        // A transient database error must not end periodic purging.
        storage.ThrowOnPurge = false;
        await AdvanceUntilPurgesAsync(time, interval, storage, 2, timeout.Token);

        Assert.True(storage.CallCount >= 2, $"Expected purging to continue after a failure, got {storage.CallCount} calls.");

        await service.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task StopAsync_EndsTheLoopCleanly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (service, storage, time) = Build(purgeInterval: TimeSpan.FromHours(1));

        await service.StartAsync(cancellationToken);
        await storage.FirstCall.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        await service.StopAsync(cancellationToken);
        var afterStop = storage.CallCount;

        // Ticking the clock after shutdown must not schedule more work.
        time.Advance(TimeSpan.FromHours(5));
        await Task.Yield();

        Assert.Equal(afterStop, storage.CallCount);
        Assert.True(service.ExecuteTask is null or { IsCompleted: true });
    }
}
