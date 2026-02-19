namespace Deucalion.Storage;

public interface IStorage
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<MonitorStats?> GetStatsAsync(string monitorName, int historyCount = 60, CancellationToken cancellationToken = default);

    Task<IEnumerable<StoredEvent>> GetLastEventsAsync(string monitorName, int count = 60, CancellationToken cancellationToken = default);

    Task SaveEventAsync(string monitorName, StoredEvent storedEvent, CancellationToken cancellationToken = default);

    Task SaveLastStateChangeAsync(string monitorName, DateTimeOffset at, MonitorState state, CancellationToken cancellationToken = default);

    Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}