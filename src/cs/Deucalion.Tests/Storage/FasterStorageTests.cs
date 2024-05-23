using Deucalion.Events;
using Deucalion.Storage;
using Xunit;

namespace Deucalion.Tests.Storage;

public class FasterStorageTests
{
    [Fact]
    public void FasterStorage_Works()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), "Deucalion.Tests.Storage");
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, true);
        }

        var storage = new FasterStorage(storagePath);

        var e1 = new MonitorChecked("m1", DateTimeOffset.Now, MonitorResponse.Up(TimeSpan.FromSeconds(1), "test"));
        storage.SaveEvent(e1.Name, StoredEvent.From(e1));
        storage.SaveLastStateChange(e1.Name, e1.At, MonitorState.Up);

        var e2 = new MonitorChecked("m2", DateTimeOffset.Now, MonitorResponse.Warn(TimeSpan.FromSeconds(2), "warn"));
        storage.SaveEvent(e2.Name, StoredEvent.From(e2));

        var e3 = new MonitorChecked("m2", DateTimeOffset.Now, MonitorResponse.Down());
        storage.SaveEvent(e3.Name, StoredEvent.From(e3));
        storage.SaveLastStateChange(e3.Name, e3.At, MonitorState.Down);

        var evs1 = storage.GetLastEvents("m1").ToList();
        var ee1 = StoredEvent.From(e1);
        Assert.Equal(ee1, evs1.First());

        var evs2 = storage.GetLastEvents("m2").ToList();
        var ee2 = StoredEvent.From(e2);
        Assert.Equal(ee2, evs2.First());

        var ee3 = StoredEvent.From(e3);
        Assert.Equal(ee3, evs2.Skip(1).First());

        var s1 = storage.GetStats("m1");
        Assert.Equal(e1.At, s1!.LastSeenUp);
        Assert.Null(s1.LastSeenDown);

        var s2 = storage.GetStats("m2");
        Assert.Null(s2!.LastSeenUp);
        Assert.Equal(e3.At, s2.LastSeenDown);
    }
}

internal static class DateTimeOffsetExtensions
{
    internal static DateTimeOffset Truncate(this DateTimeOffset dateTime, TimeSpan? timeSpan = null)
    {
        // https://stackoverflow.com/a/1005222/33244
        timeSpan ??= TimeSpan.FromSeconds(1);
        return dateTime.AddTicks(-(dateTime.Ticks % timeSpan.Value.Ticks));
    }
}
