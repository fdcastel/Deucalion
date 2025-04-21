using Deucalion.Events;
using Deucalion.Storage;
using Xunit;

namespace Deucalion.Tests.Storage;

public class SqliteStorageStatsTests : SqliteStorageTestBase
{
    [Fact]
    public async Task SqliteStorage_SaveStateChangeAndGetStatsAsync_Works()
    {
        var now = DateTimeOffset.UtcNow; // Use UtcNow

        // Monitor 1: Up
        var e1_1 = new MonitorChecked("m1", now.AddMinutes(-5), MonitorResponse.Up(TimeSpan.FromMilliseconds(50), "up1"));
        await Storage.SaveEventAsync(e1_1.Name, StoredEvent.From(e1_1));
        await Storage.SaveLastStateChangeAsync(e1_1.Name, e1_1.At, MonitorState.Up);
        var e1_2 = new MonitorChecked("m1", now.AddMinutes(-2), MonitorResponse.Up(TimeSpan.FromMilliseconds(60), "up2"));
        await Storage.SaveEventAsync(e1_2.Name, StoredEvent.From(e1_2));
        // No state change here, LastSeenUp should remain e1_1.At

        // Monitor 2: Down -> Up -> Down
        var e2_1 = new MonitorChecked("m2", now.AddMinutes(-10), MonitorResponse.Down(text: "down1")); // Fixed parameter name
        await Storage.SaveEventAsync(e2_1.Name, StoredEvent.From(e2_1));
        await Storage.SaveLastStateChangeAsync(e2_1.Name, e2_1.At, MonitorState.Down);
        var e2_2 = new MonitorChecked("m2", now.AddMinutes(-5), MonitorResponse.Up(TimeSpan.FromMilliseconds(100), "up1"));
        await Storage.SaveEventAsync(e2_2.Name, StoredEvent.From(e2_2));
        await Storage.SaveLastStateChangeAsync(e2_2.Name, e2_2.At, MonitorState.Up);
        var e2_3 = new MonitorChecked("m2", now.AddMinutes(-1), MonitorResponse.Down(text: "down2")); // Fixed parameter name
        await Storage.SaveEventAsync(e2_3.Name, StoredEvent.From(e2_3));
        await Storage.SaveLastStateChangeAsync(e2_3.Name, e2_3.At, MonitorState.Down);

        // Monitor 3: Only Warn
        var e3_1 = new MonitorChecked("m3", now.AddMinutes(-3), MonitorResponse.Warn(TimeSpan.FromMilliseconds(200), "warn1"));
        await Storage.SaveEventAsync(e3_1.Name, StoredEvent.From(e3_1));
        // No Up/Down state change saved

        // --- Assertions ---

        // Monitor 1 Stats
        var s1 = await Storage.GetStatsAsync("m1");
        Assert.NotNull(s1);
        Assert.Equal(MonitorState.Up, s1.LastState);
        Assert.Equal(e1_2.At.ToUniversalTime(), s1.LastUpdate.ToUniversalTime());
        Assert.Equal(100.0, s1.Availability, precision: 1); // Both events are Up
        Assert.Equal(TimeSpan.FromMilliseconds(55), s1.AverageResponseTime); // (50+60)/2
        Assert.Equal(e1_1.At.ToUniversalTime(), s1.LastSeenUp?.ToUniversalTime()); // Set by SaveLastStateChange
        Assert.Null(s1.LastSeenDown);

        // Monitor 2 Stats
        var s2 = await Storage.GetStatsAsync("m2");
        Assert.NotNull(s2);
        Assert.Equal(MonitorState.Down, s2.LastState);
        Assert.Equal(e2_3.At.ToUniversalTime(), s2.LastUpdate.ToUniversalTime());
        // Availability: 1 Up, 2 Down -> (3 - 2) / 3 = 33.33...%
        Assert.Equal(1.0 / 3.0 * 100.0, s2.Availability, precision: 1);
        Assert.Equal(TimeSpan.FromMilliseconds(100), s2.AverageResponseTime); // Only e2_2 has response time
        Assert.Equal(e2_2.At.ToUniversalTime(), s2.LastSeenUp?.ToUniversalTime()); // Set by SaveLastStateChange
        Assert.Equal(e2_3.At.ToUniversalTime(), s2.LastSeenDown?.ToUniversalTime()); // Set by SaveLastStateChange

        // Monitor 3 Stats
        var s3 = await Storage.GetStatsAsync("m3");
        Assert.NotNull(s3);
        Assert.Equal(MonitorState.Warn, s3.LastState);
        Assert.Equal(e3_1.At.ToUniversalTime(), s3.LastUpdate.ToUniversalTime());
        // Availability: 1 Warn -> (1 - 0) / 1 = 100% (Warn counts as available)
        Assert.Equal(100.0, s3.Availability, precision: 1);
        Assert.Equal(TimeSpan.FromMilliseconds(200), s3.AverageResponseTime);
        Assert.Null(s3.LastSeenUp); // No SaveLastStateChange called
        Assert.Null(s3.LastSeenDown); // No SaveLastStateChange called

        // Non-existent Monitor Stats
        var s4 = await Storage.GetStatsAsync("m4");
        Assert.Null(s4);
    }

    [Fact]
    public async Task GetStatsAsync_OnlyDownEvents_ReturnsCorrectStats()
    {
        // Arrange
        var monitorName = "stats-only-down";
        var time1 = DateTimeOffset.UtcNow.AddMinutes(-2);
        var time2 = DateTimeOffset.UtcNow;
        var event1 = new StoredEvent(time1, MonitorState.Down, null, "Down 1");
        var event2 = new StoredEvent(time2, MonitorState.Down, null, "Down 2");

        await Storage.SaveEventAsync(monitorName, event1);
        await Storage.SaveEventAsync(monitorName, event2);
        await Storage.SaveLastStateChangeAsync(monitorName, time2, MonitorState.Down); // Save last state change

        // Act
        var stats = await Storage.GetStatsAsync(monitorName);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(MonitorState.Down, stats.LastState);
        Assert.Equal(time2.ToUniversalTime(), stats.LastUpdate.ToUniversalTime());
        Assert.Equal(0.0, stats.Availability); // Only Down events -> 0% availability
        Assert.Equal(TimeSpan.Zero, stats.AverageResponseTime); // No successful (Up/Warn) events with response times
        Assert.Null(stats.LastSeenUp);
        Assert.Equal(time2.ToUniversalTime(), stats.LastSeenDown?.ToUniversalTime());
    }

    [Fact]
    public async Task GetStatsAsync_NullResponseTimes_CalculatesAverageCorrectly()
    {
        // Arrange
        var monitorName = "stats-null-response";
        var time1 = DateTimeOffset.UtcNow.AddMinutes(-3);
        var time2 = DateTimeOffset.UtcNow.AddMinutes(-2);
        var time3 = DateTimeOffset.UtcNow.AddMinutes(-1);
        var time4 = DateTimeOffset.UtcNow;

        // Events: Up (null RT), Warn (100ms), Up (null RT), Down (null RT)
        var event1 = new StoredEvent(time1, MonitorState.Up, null, "Up Null RT");
        var event2 = new StoredEvent(time2, MonitorState.Warn, TimeSpan.FromMilliseconds(100), "Warn 100ms");
        var event3 = new StoredEvent(time3, MonitorState.Up, null, "Up Null RT 2");
        var event4 = new StoredEvent(time4, MonitorState.Down, null, "Down Null RT");

        await Storage.SaveEventAsync(monitorName, event1);
        await Storage.SaveEventAsync(monitorName, event2);
        await Storage.SaveEventAsync(monitorName, event3);
        await Storage.SaveEventAsync(monitorName, event4);

        // Act
        var stats = await Storage.GetStatsAsync(monitorName);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(MonitorState.Down, stats.LastState);
        Assert.Equal(time4.ToUniversalTime(), stats.LastUpdate.ToUniversalTime());
        // Availability: 3 Up/Warn, 1 Down -> (4 - 1) / 4 = 75%
        Assert.Equal(75.0, stats.Availability);
        // Average Response Time: Only event2 has a non-null RT. Should be 100ms.
        Assert.Equal(TimeSpan.FromMilliseconds(100), stats.AverageResponseTime);
    }

    [Fact]
    public async Task GetStatsAsync_AfterPurge_RecalculatesCorrectly()
    {
        // Arrange
        var monitorName = "stats-after-purge";
        var now = DateTimeOffset.UtcNow;
        var retentionPeriod = TimeSpan.FromHours(2); // Keep last 2 hours
        var cutoff = now - retentionPeriod;

        var eventsToSave = new List<StoredEvent>
        {
            // Old events (will be purged)
            new(cutoff.AddHours(-2), MonitorState.Up, TimeSpan.FromMilliseconds(50), "Old Up 1"),
            new(cutoff.AddHours(-1), MonitorState.Down, null, "Old Down 1"),

            // Recent events (will be kept)
            new(cutoff.AddMinutes(30), MonitorState.Up, TimeSpan.FromMilliseconds(100), "Recent Up 1"),
            new(now, MonitorState.Up, TimeSpan.FromMilliseconds(120), "Recent Up 2")
        };

        foreach (var ev in eventsToSave.OrderBy(e => e.At))
        {
            await Storage.SaveEventAsync(monitorName, ev);
        }

        // Stats before purge (4 events: 3 Up, 1 Down -> 75% avail, avg RT (50+100+120)/3 = 90ms)
        var statsBefore = await Storage.GetStatsAsync(monitorName);
        Assert.NotNull(statsBefore);
        Assert.Equal(75.0, statsBefore.Availability);
        Assert.Equal(TimeSpan.FromMilliseconds(90), statsBefore.AverageResponseTime);

        // Act: Purge old events
        await Storage.PurgeOldEventsAsync(retentionPeriod);

        // Assert: Get stats again and verify recalculation
        var statsAfter = await Storage.GetStatsAsync(monitorName);
        Assert.NotNull(statsAfter);
        Assert.Equal(MonitorState.Up, statsAfter.LastState); // Last event was "Recent Up 2"
        Assert.Equal(now.ToUniversalTime(), statsAfter.LastUpdate.ToUniversalTime());
        // Availability (remaining): 2 Up, 0 Down -> 100%
        Assert.Equal(100.0, statsAfter.Availability);
        // Average Response Time (remaining): (100 + 120) / 2 = 110ms
        Assert.Equal(TimeSpan.FromMilliseconds(110), statsAfter.AverageResponseTime);
    }
}
