using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FASTER.core;

namespace Deucalion.Storage;

internal record MonitorEntry(
    FasterLog Log,
    FasterLogSettings LogSettings,
    MonitorStats? Stats
);

public class FasterStorage : IDisposable
{
    private const string LastSeenDownFileName = "last-seen-down";
    private const string LastSeenUpFileName = "last-seen-up";

    // Average log entry size: 150 bytes (worst case: DNS with text).

    private const long LogPageSize = 1 << 16; // 64 KB
    private const long LogMemorySize = 1 << 18; // 256 KB -- Keep ~1.2 days in memory
    private const long LogSegmentSize = 1 << 24; // 16 MB
    private const long LogMaxSize = 1 << 26; // 64 MB -- Truncate (discard) log data older than ~10 months

    public static readonly TimeSpan DefaultCommitInterval = TimeSpan.FromMinutes(1);

    private readonly string _storagePath;
    private readonly Timer _commitTimer;

    private readonly ConcurrentDictionary<string, MonitorEntry> _monitors = new();
    private bool _disposedValue;

    public FasterStorage(string? storagePath = null, TimeSpan? commitInterval = null)
    {
        _storagePath = storagePath ?? Path.Combine(Path.GetTempPath(), "Deucalion");

        _commitTimer = new Timer(CommitCallbackAsync, null, TimeSpan.Zero, commitInterval ?? DefaultCommitInterval);
    }

    public MonitorStats? GetStats(string monitorName) =>
        GetEntryFor(monitorName).Stats;

    public IEnumerable<StoredEvent> GetLastEvents(string monitorName, int count = 60) =>
        ScanLastEvents(GetEntryFor(monitorName).Log, count);

    public MonitorStats SaveEvent(string monitorName, StoredEvent storedEvent)
    {
        var entry = GetEntryFor(monitorName);

        // Append event to log
        var rawBytes = Serialize(storedEvent);
        entry.Log.Enqueue(rawBytes);

        // Recalculate statistics
        //   null-forgiving: After .Enqueue(), RecomputeStats cannot return null.
        var newStats = RecomputeStats(entry.Log, monitorName)! with
        {
            LastSeenUp = entry.Stats?.LastSeenUp,
            LastSeenDown = entry.Stats?.LastSeenDown
        };

        // Update dictionary
        var newEntry = entry with { Stats = newStats };
        _monitors.TryUpdate(monitorName, newEntry, entry);

        // Return updated stats
        return newStats;
    }

    public void SaveLastStateChange(string monitorName, DateTimeOffset at, MonitorState state)
    {
        var entry = GetEntryFor(monitorName);

        MonitorStats? newStats = null;
        switch (state)
        {
            case MonitorState.Up:
                WriteLastStateChangeToFile(at, monitorName, LastSeenUpFileName);
                if (entry.Stats is not null)
                {
                    newStats = entry.Stats with { LastSeenUp = at };
                }
                break;

            case MonitorState.Down:
                WriteLastStateChangeToFile(at, monitorName, LastSeenDownFileName);
                if (entry.Stats is not null)
                {
                    newStats = entry.Stats with { LastSeenDown = at };
                }
                break;
        }

        if (newStats is not null)
        {
            var newEntry = entry with { Stats = newStats };
            _monitors.TryUpdate(monitorName, newEntry, entry);
        }
    }

    public async Task CommitAllAsync()
    {
        foreach (var monitorName in _monitors.Keys)
        {
            var log = GetEntryFor(monitorName).Log;
            try
            {
                await log.CommitAsync();

                // Truncate the log to last LogMaxSize bytes.
                if (log.CommittedUntilAddress - LogMaxSize > 0)
                {
                    log.TruncateUntilPageStart(log.CommittedUntilAddress - LogMaxSize);
                }
            }
            catch (CommitFailureException)
            {
                // Ignore. Will retry on next heartbeat.
            }
        }
    }

    private async void CommitCallbackAsync(object? _)
    {
        await CommitAllAsync();
    }

    private static T? Deserialize<T>(byte[] raw) => JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(raw));

    private static byte[] Serialize<T>(T obj) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));

    private string GetMonitorPath(string monitorName) => Path.Combine(_storagePath, monitorName.EncodePath());
    private string GetMonitorPath(string monitorName, string fileName) => Path.Combine(_storagePath, monitorName.EncodePath(), fileName);

    private static DateTimeOffset? ReadLastStateChangeFromFile(string fileName) =>
        DateTimeOffset.TryParseExact(
            File.Exists(fileName) ? File.ReadAllText(fileName) : string.Empty,
            "u",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result
        )
            ? result
            : null;

    private void WriteLastStateChangeToFile(DateTimeOffset at, string monitorName, string fileName)
    {
        File.WriteAllText(
            GetMonitorPath(monitorName, fileName),
            at.ToString("u")
        );
    }

    private MonitorEntry GetEntryFor(string monitorName) =>
        _monitors.GetOrAdd(monitorName, (key) =>
            {
                var newSetttings = new FasterLogSettings(GetMonitorPath(monitorName))
                {
                    PageSize = LogPageSize,
                    MemorySize = LogMemorySize,
                    SegmentSize = LogSegmentSize,

                    AutoRefreshSafeTailAddress = true /* needed for ScanUncommited */
                };

                var newLog = new FasterLog(newSetttings);
                var newSummary = RecomputeStats(newLog, monitorName, includeLastSeen: true);
                return new MonitorEntry(newLog, newSetttings, newSummary);
            });

    private MonitorStats? RecomputeStats(FasterLog log, string monitorName, bool includeLastSeen = false)
    {
        var evs = ScanLastEvents(log).ToList();
        if (evs.Count == 0)
        {
            return null;
        }

        var lastEvent = evs[^1];

        var unknownCount = evs.Count(e => e.State == MonitorState.Unknown);
        var downCount = evs.Count(e => e.State == MonitorState.Down);
        var eventsWithResponseTime = evs.Where(e => e.ResponseTime.HasValue).ToList();

        return new(
            LastState: lastEvent.State,
            LastUpdate: lastEvent.At,

            Availability: 100 * (evs.Count - downCount) / (evs.Count - unknownCount),
            AverageResponseTime: eventsWithResponseTime.Count > 0
                ? TimeSpan.FromMilliseconds(eventsWithResponseTime.Average(e => e.ResponseTime!.Value.TotalMilliseconds))
                : TimeSpan.Zero,

            LastSeenDown: includeLastSeen ? ReadLastStateChangeFromFile(GetMonitorPath(monitorName, LastSeenDownFileName)) : null,
            LastSeenUp: includeLastSeen ? ReadLastStateChangeFromFile(GetMonitorPath(monitorName, LastSeenUpFileName)) : null
        );
    }

    public static IEnumerable<StoredEvent> ScanLastEvents(FasterLog log, int count = 60)
    {
        var result = new Queue<byte[]>(count);

        // Scan from last 2 commited pages -- https://github.com/microsoft/FASTER/discussions/610
        var committedPageStart = log.CommittedUntilAddress & ~(LogPageSize - 1);
        if (committedPageStart == log.CommittedUntilAddress) // corner case: committed until page boundary
            committedPageStart -= LogPageSize;
        committedPageStart -= LogPageSize;
        committedPageStart = Math.Max(committedPageStart, 0); // avoid negative values

        using var iter = log.Scan(committedPageStart, long.MaxValue, scanUncommitted: true);
        // ToDo: Use MemoryPool<byte>
        while (iter.GetNext(out var rawEvent, out _, out _))
        {
            if (result.Count == count)
            {
                result.TryDequeue(out var _);
            }

            result.Enqueue(rawEvent);
        }

        return result
            .Select(re => Deserialize<StoredEvent>(re)!);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _commitTimer.Dispose();

                foreach (var key in _monitors.Keys)
                {
                    _monitors.Remove(key, out var entry);
                    entry?.Log.Dispose();
                    entry?.LogSettings.Dispose();
                }
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
