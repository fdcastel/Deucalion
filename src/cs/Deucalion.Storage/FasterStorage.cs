using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Deucalion.Monitors;
using FASTER.core;

namespace Deucalion.Storage;

public class FasterStorage : IDisposable
{
    public static readonly TimeSpan DefaultCommitInterval = TimeSpan.FromMinutes(1);
    public static readonly long LogPageSize = 1L << 16;

    private readonly string _storagePath;
    private readonly Timer _commitTimer;

    private readonly ConcurrentDictionary<string, (FasterLog, FasterLogSettings)> _monitors = new();
    private bool _disposedValue;

    public FasterStorage(string? storagePath = null, TimeSpan? commitInterval = null)
    {
        _storagePath = storagePath ?? Path.Combine(Path.GetTempPath(), "Deucalion");

        _commitTimer = new Timer(CommitCallbackAsync, null, TimeSpan.Zero, commitInterval ?? DefaultCommitInterval);
    }

    public MonitorSummary GetSummary(string monitorName) =>
        new(
            ReadLastStateFromFile(GetMonitorPath(monitorName, "last-down")),
            ReadLastStateFromFile(GetMonitorPath(monitorName, "last-up"))
        );

    public IEnumerable<StoredEvent> GetLastEvents(string monitorName, int count = 60)
    {
        var result = new Queue<byte[]>(count);

        // Scan from last commited page -- https://github.com/microsoft/FASTER/discussions/610
        var log = GetLogFor(monitorName);
        var committedPageStart = log.CommittedUntilAddress & ~(LogPageSize - 1);
        committedPageStart = Math.Max(committedPageStart, 0); // avoid negative values

        using var iter = log.Scan(committedPageStart, log.TailAddress);
        while (iter.GetNext(out var rawEvent, out _, out _))
        {
            if (result.Count == count)
            {
                result.TryDequeue(out var _);
            }
            result.Enqueue(rawEvent!);
        }

        return result
            .Select(re => Deserialize<StoredEvent>(re)!);
    }

    public void AddEvent(string monitorName, StoredEvent entry)
    {
        var log = GetLogFor(monitorName);
        var rawBytes = Serialize(entry);
        log.Enqueue(rawBytes);
    }

    public void AddStateChangeEvent(string monitorName, DateTimeOffset at, MonitorState state)
    {
        switch (state)
        {
            case MonitorState.Up:
                File.WriteAllText(GetMonitorPath(monitorName, "last-up"), at.ToString("u"));
                break;

            case MonitorState.Down:
                File.WriteAllText(GetMonitorPath(monitorName, "last-down"), at.ToString("u"));
                break;
        }
    }

    public async Task CommitAllAsync()
    {
        foreach (var monitorName in _monitors.Keys)
        {
            var log = GetLogFor(monitorName);

            try
            {
                await log.CommitAsync();
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

    private static DateTimeOffset? ReadLastStateFromFile(string fileName) =>
        DateTimeOffset.TryParseExact(
            File.Exists(fileName) ? File.ReadAllText(fileName) : string.Empty,
            "u",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result
        )
            ? result
            : null;

    private FasterLog GetLogFor(string monitorName)
    {
        if (_monitors.TryGetValue(monitorName, out var existing))
        {
            var (log, _) = existing;
            return log;
        }

        var newSetttings = new FasterLogSettings(GetMonitorPath(monitorName))
        {
            PageSize = LogPageSize
        };

        var newLog = new FasterLog(newSetttings);

        _monitors.AddOrUpdate(monitorName, (newLog, newSetttings), (name, log) => log);
        return newLog;
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
                    _monitors.Remove(key, out var value);

                    var (log, settings) = value;
                    log?.Dispose();
                    settings?.Dispose();
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
