using Deucalion.Events;
using Deucalion.Storage;
using Xunit;

namespace Deucalion.Tests.Storage;

public class SqliteStorageEventTests : SqliteStorageTestBase
{
    [Fact]
    public async Task SaveEventAsync_InsertsNewEventAndCalculatesStats()
    {
        // Arrange
        var monitorName = "test-monitor-1";
        var timestamp = DateTimeOffset.UtcNow;
        var responseTime = TimeSpan.FromMilliseconds(123);
        var storedEvent = new StoredEvent(timestamp, MonitorState.Up, responseTime, "OK");

        // Act
        await Storage.SaveEventAsync(monitorName, storedEvent);
        var stats = await Storage.GetStatsAsync(monitorName);

        // Assert
        // Verify stats directly, and implicitly test DB state via GetLastEvents/GetStats
        Assert.NotNull(stats);
        Assert.Equal(MonitorState.Up, stats.LastState);
        Assert.Equal(timestamp.ToUniversalTime(), stats.LastUpdate.ToUniversalTime()); // Compare UTC
        Assert.Null(stats.LastSeenUp); // SaveEvent doesn't update LastSeenUp/Down
        Assert.Null(stats.LastSeenDown);
        Assert.Equal(100.0, stats.Availability); // Only one event, which is Up
        Assert.Equal(responseTime, stats.AverageResponseTime);

        // Explicitly check GetLastEventsAsync
        var events = (await Storage.GetLastEventsAsync(monitorName)).ToList();
        Assert.Single(events);
        Assert.Equal(storedEvent with { At = storedEvent.At.ToUniversalTime() }, events[0]);
    }

    [Fact]
    public async Task SaveEventAsync_UpdatesStatsCorrectly()
    {
        // Arrange
        var monitorName = "test-monitor-2";
        var time1 = DateTimeOffset.UtcNow.AddMinutes(-2);
        var time2 = DateTimeOffset.UtcNow.AddMinutes(-1);
        var time3 = DateTimeOffset.UtcNow;

        var event1 = new StoredEvent(time1, MonitorState.Up, TimeSpan.FromMilliseconds(100), "OK");
        var event2 = new StoredEvent(time2, MonitorState.Down, null, "Timeout");
        var event3 = new StoredEvent(time3, MonitorState.Up, TimeSpan.FromMilliseconds(150), "OK Again");

        // Act
        await Storage.SaveEventAsync(monitorName, event1);
        await Storage.SaveEventAsync(monitorName, event2);
        await Storage.SaveEventAsync(monitorName, event3);
        var stats = await Storage.GetStatsAsync(monitorName);

        // Assert
        // Check stats returned by the last SaveEvent
        Assert.NotNull(stats);
        Assert.Equal(MonitorState.Up, stats.LastState);
        Assert.Equal(time3.ToUniversalTime(), stats.LastUpdate.ToUniversalTime());
        // LastSeenUp/Down are not set by SaveEvent, need SaveLastStateChange
        Assert.Null(stats.LastSeenUp);
        Assert.Null(stats.LastSeenDown);
        // Availability: 2 Up, 1 Down -> (3 - 1) / 3 = 66.66...%
        Assert.InRange(stats.Availability, 66.6, 66.7);
        // Avg Response Time: (100 + 150) / 2 = 125ms
        Assert.Equal(TimeSpan.FromMilliseconds(125), stats.AverageResponseTime);

        // Verify GetStatsAsync reflects the latest state
        var finalStats = await Storage.GetStatsAsync(monitorName);
        Assert.NotNull(finalStats);
        Assert.Equal(stats.LastState, finalStats.LastState);
        Assert.Equal(stats.LastUpdate, finalStats.LastUpdate);
        Assert.Equal(stats.Availability, finalStats.Availability);
        Assert.Equal(stats.AverageResponseTime, finalStats.AverageResponseTime);
        Assert.Null(finalStats.LastSeenUp); // Still null
        Assert.Null(finalStats.LastSeenDown); // Still null
    }

    [Fact]
    public async Task GetLastEventsAsync_ReturnsCorrectEvents()
    {
        // Arrange
        var monitorName = "test-monitor-3";
        var baseTime = DateTimeOffset.UtcNow;
        var eventsToSave = Enumerable.Range(0, 5)
            .Select(i => new StoredEvent(baseTime.AddMinutes(-i), MonitorState.Up, TimeSpan.FromMilliseconds(100 + i), $"OK {i}"))
            .ToList();

        foreach (var ev in eventsToSave.OrderBy(e => e.At)) // Save in chronological order
        {
            await Storage.SaveEventAsync(monitorName, ev);
        }

        // Act
        var retrievedEvents = (await Storage.GetLastEventsAsync(monitorName, 3)).ToList();

        // Assert
        Assert.Equal(3, retrievedEvents.Count);
        // Should be the latest 3 events, ordered descending by time (newest first)
        Assert.Equal(baseTime.ToUniversalTime(), retrievedEvents[0].At.ToUniversalTime()); // i=0
        Assert.Equal(baseTime.AddMinutes(-1).ToUniversalTime(), retrievedEvents[1].At.ToUniversalTime()); // i=1
        Assert.Equal(baseTime.AddMinutes(-2).ToUniversalTime(), retrievedEvents[2].At.ToUniversalTime()); // i=2
        Assert.Equal(TimeSpan.FromMilliseconds(100), retrievedEvents[0].ResponseTime);
        Assert.Equal(TimeSpan.FromMilliseconds(101), retrievedEvents[1].ResponseTime);
        Assert.Equal(TimeSpan.FromMilliseconds(102), retrievedEvents[2].ResponseTime);
    }

    [Fact]
    public async Task SqliteStorage_SaveAndRetrieveEventsAsync_Works()
    {
        var now = DateTimeOffset.UtcNow; // Use UtcNow for consistency
        var e1 = new MonitorChecked("m1", now.AddSeconds(-20), MonitorResponse.Up(TimeSpan.FromSeconds(1), "test"));
        var se1 = StoredEvent.From(e1);
        await Storage.SaveEventAsync(e1.Name, se1);

        var e2 = new MonitorChecked("m1", now.AddSeconds(-10), MonitorResponse.Warn(TimeSpan.FromSeconds(2), "warn"));
        var se2 = StoredEvent.From(e2);
        await Storage.SaveEventAsync(e2.Name, se2);

        var e3 = new MonitorChecked("m1", now, MonitorResponse.Down(text: "down")); // Fixed parameter name
        var se3 = StoredEvent.From(e3);
        await Storage.SaveEventAsync(e3.Name, se3);

        var evs1 = (await Storage.GetLastEventsAsync("m1")).ToList();

        // Assert count
        Assert.Equal(3, evs1.Count);

        // Assert order (GetLastEvents returns newest first)
        Assert.Equal(se3 with { At = se3.At.ToUniversalTime() }, evs1[0]);
        Assert.Equal(se2 with { At = se2.At.ToUniversalTime() }, evs1[1]);
        Assert.Equal(se1 with { At = se1.At.ToUniversalTime() }, evs1[2]);

        // Test count limit
        var evsLimited = (await Storage.GetLastEventsAsync("m1", 2)).ToList();
        Assert.Equal(2, evsLimited.Count);
        Assert.Equal(se3 with { At = se3.At.ToUniversalTime() }, evsLimited[0]);
        Assert.Equal(se2 with { At = se2.At.ToUniversalTime() }, evsLimited[1]);

        // Test different monitor
        var evsOther = (await Storage.GetLastEventsAsync("m2")).ToList();
        Assert.Empty(evsOther);
    }
}
