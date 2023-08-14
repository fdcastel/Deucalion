using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Deucalion.Monitors.Events;
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
        _storagePath = storagePath ?? Path.Join(Path.GetTempPath(), "Deucalion");

        _commitTimer = new Timer(CommitCallbackAsync, null, TimeSpan.Zero, commitInterval ?? DefaultCommitInterval);
    }

    public IEnumerable<string> GetMonitors() => _monitors.Keys;

    public IEnumerable<MonitorChecked> GetLastEvents(string monitorName, int count = 60)
    {
        var log = GetLogFor(monitorName);

        var result = new List<MonitorChecked>();

        // Read from 2 last pages -- https://github.com/microsoft/FASTER/discussions/610
        var committedUntilAddress = log.CommittedUntilAddress;
        var committedPageStart = committedUntilAddress & ~(LogPageSize - 1);
        if (committedPageStart == committedUntilAddress) // corner case: committed until page boundary
            committedPageStart -= LogPageSize;

        committedPageStart -= LogPageSize;
        committedPageStart = Math.Max(committedPageStart, 0); // avoid negative values

        using var iter = log.Scan(committedPageStart, committedUntilAddress);
        while (iter.GetNext(out var rawEvent, out _, out _))
        {
            var ev = Deserialize<MonitorChecked>(rawEvent);
            if (ev is not null)
            {
                result.Add(ev);
            }
        }

        return result.TakeLast(count);
    }

    public void AddEvent(MonitorChecked ev)
    {
        var log = GetLogFor(ev.Name);
        var valueBytes = Serialize(ev);
        log.Enqueue(valueBytes);
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

    private static T? Deserialize<T>(byte[] raw) => JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(raw));

    private static byte[] Serialize<T>(T obj) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));

    private async void CommitCallbackAsync(object? _)
    {
        await CommitAllAsync();
    }

    private FasterLog GetLogFor(string monitorName)
    {
        if (_monitors.TryGetValue(monitorName, out var existing))
        {
            var (log, _) = existing;
            return log;
        }

        var monitorPath = Path.Join(_storagePath, monitorName.EncodePath());

        var newSetttings = new FasterLogSettings(monitorPath)
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

                foreach (var existing in _monitors)
                {
                    _monitors.Remove(existing.Key, out var _);

                    var (log, settings) = existing.Value;

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
