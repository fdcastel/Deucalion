using Deucalion.Monitors;
using Deucalion.Monitors.Events;
using Deucalion.Storage;
using Xunit;

namespace Deucalion.Tests.Storage;

public class FasterStorageTests
{
    [Fact]
    public async Task FasterStorage_Works()
    {
        var storagePath = Path.Join(Path.GetTempPath(), "Deucalion.Tests.Storage");
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, true);
        }

        var storage = new FasterStorage(storagePath);

        var e1 = new MonitorChecked("m1", DateTimeOffset.Now, MonitorResponse.Up(TimeSpan.FromSeconds(1), "test"));
        storage.AddEvent(e1.Name, StoredEvent.From(e1));

        var e2 = new MonitorChecked("m2", DateTimeOffset.Now, MonitorResponse.Warn(TimeSpan.FromSeconds(2), "warn"));
        storage.AddEvent(e2.Name, StoredEvent.From(e2));

        var e3 = new MonitorChecked("m2", DateTimeOffset.Now, MonitorResponse.Down());
        storage.AddEvent(e3.Name, StoredEvent.From(e3));

        await storage.CommitAllAsync();

        var evs1 = storage.GetLastEvents("m1").ToList();
        var ee1 = StoredEvent.From(e1);
        Assert.Equal(ee1, evs1.First());

        var evs2 = storage.GetLastEvents("m2").ToList();
        var ee2 = StoredEvent.From(e2);
        Assert.Equal(ee2, evs2.First());

        var ee3 = StoredEvent.From(e3);
        Assert.Equal(ee3, evs2.Skip(1).First());

        var mons = storage.GetMonitors().ToList();
        Assert.Equal(2, mons.Count);
    }
}
