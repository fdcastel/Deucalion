namespace Deucalion.Storage;

public interface IStorage
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<MonitorStats?> GetStatsAsync(string monitorName, int historyCount = 60, CancellationToken cancellationToken = default);

    Task<IEnumerable<StoredEvent>> GetLastEventsAsync(string monitorName, int count = 60, CancellationToken cancellationToken = default);

    /// <summary>
    /// The monitor's current run (see <see cref="MonitorRun"/>), or null when it has no non-Unknown
    /// events. One indexed query regardless of how long the run is.
    /// </summary>
    Task<MonitorRun?> GetCurrentRunAsync(string monitorName, CancellationToken cancellationToken = default);

    Task SaveEventAsync(string monitorName, StoredEvent storedEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes events older than <paramref name="retentionPeriod"/> and, per monitor, any beyond
    /// the newest <paramref name="maxEventsPerMonitor"/>. Zero or negative disables that criterion.
    /// </summary>
    /// <returns>The number of rows deleted.</returns>
    Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, int maxEventsPerMonitor, CancellationToken cancellationToken = default);
}