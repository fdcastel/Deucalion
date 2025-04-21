using Deucalion.Events;
using Deucalion.Storage;
using Xunit;

namespace Deucalion.Tests.Storage;

public class SqliteStoragePurgeTests : SqliteStorageTestBase
{
    [Fact]
    public async Task PurgeOldEventsAsync_RemovesOldEventsAndKeepsRecentOnes()
    {
        // Arrange
        var monitorName = "purge-test-monitor";
        var now = DateTimeOffset.UtcNow;
        var retentionPeriod = TimeSpan.FromDays(7);
        var cutoff = now - retentionPeriod;

        var eventsToSave = new List<StoredEvent>
        {
            // Should be kept
            new(cutoff.AddHours(1), MonitorState.Up, TimeSpan.FromMilliseconds(100), "Recent 1"),
            new(now, MonitorState.Up, TimeSpan.FromMilliseconds(110), "Recent 2"),

            // Should be purged
            new(cutoff.AddHours(-1), MonitorState.Down, null, "Old 1"),
            new(cutoff.AddDays(-1), MonitorState.Up, TimeSpan.FromMilliseconds(120), "Old 2"),
            new(now.AddDays(-30), MonitorState.Up, TimeSpan.FromMilliseconds(130), "Very Old 3")
        };

        // Save events in chronological order
        foreach (var ev in eventsToSave.OrderBy(e => e.At))
        {
            await Storage.SaveEventAsync(monitorName, ev);
        }

        // Pre-assertion: Ensure all events are saved initially
        var initialEvents = (await Storage.GetLastEventsAsync(monitorName, 10)).ToList();
        Assert.Equal(eventsToSave.Count, initialEvents.Count);

        // Act
        var deletedCount = await Storage.PurgeOldEventsAsync(retentionPeriod);

        // Assert
        Assert.Equal(3, deletedCount); // Expecting 3 old events to be deleted

        var remainingEvents = (await Storage.GetLastEventsAsync(monitorName, 10)).ToList();
        Assert.Equal(2, remainingEvents.Count); // Expecting 2 recent events to remain

        // Verify the correct events remain (newest first)
        Assert.Contains(remainingEvents, e => e.ResponseText == "Recent 2");
        Assert.Contains(remainingEvents, e => e.ResponseText == "Recent 1");
        Assert.DoesNotContain(remainingEvents, e => e.ResponseText == "Old 1");
        Assert.DoesNotContain(remainingEvents, e => e.ResponseText == "Old 2");
        Assert.DoesNotContain(remainingEvents, e => e.ResponseText == "Very Old 3");

        // Verify timestamps are correct relative to cutoff
        Assert.All(remainingEvents, e => Assert.True(e.At >= cutoff));
    }

    [Fact]
    public async Task PurgeOldEventsAsync_ZeroRetention_DoesNotPurge()
    {
        // Arrange
        var monitorName = "purge-zero-retention";
        var now = DateTimeOffset.UtcNow;
        var eventsToSave = new List<StoredEvent>
        {
            new(now.AddMinutes(-10), MonitorState.Up, TimeSpan.FromMilliseconds(100), "Event 1"),
            new(now.AddMinutes(-5), MonitorState.Down, null, "Event 2"),
        };
        foreach (var ev in eventsToSave) await Storage.SaveEventAsync(monitorName, ev);

        // Act
        var deletedCount = await Storage.PurgeOldEventsAsync(TimeSpan.Zero);

        // Assert
        Assert.Equal(0, deletedCount); // Nothing should be deleted with zero retention
        var remainingEvents = (await Storage.GetLastEventsAsync(monitorName, 10)).ToList();
        Assert.Equal(eventsToSave.Count, remainingEvents.Count); // All events should remain
    }

    [Fact]
    public async Task PurgeOldEventsAsync_AllEventsOld_PurgesAll()
    {
        // Arrange
        var monitorName = "purge-all-old";
        var now = DateTimeOffset.UtcNow;
        var retentionPeriod = TimeSpan.FromHours(1); // Purge anything older than 1 hour
        var eventsToSave = new List<StoredEvent>
        {
            new(now.AddHours(-2), MonitorState.Up, TimeSpan.FromMilliseconds(100), "Old Event 1"),
            new(now.AddHours(-3), MonitorState.Down, null, "Old Event 2"),
        };
        foreach (var ev in eventsToSave) await Storage.SaveEventAsync(monitorName, ev);

        // Act
        var deletedCount = await Storage.PurgeOldEventsAsync(retentionPeriod);

        // Assert
        Assert.Equal(eventsToSave.Count, deletedCount); // All events should be deleted
        var remainingEvents = (await Storage.GetLastEventsAsync(monitorName, 10)).ToList();
        Assert.Empty(remainingEvents); // No events should remain
    }

    [Fact]
    public async Task PurgeOldEventsAsync_NoEventsOrAllNew_PurgesNone()
    {
        // Arrange
        // Removed unused variable: var monitorNameNoEvents = "purge-no-events";
        var monitorNameAllNew = "purge-all-new";
        var now = DateTimeOffset.UtcNow;
        var retentionPeriod = TimeSpan.FromDays(1); // Purge anything older than 1 day

        // Setup monitor with all new events
        var newEvents = new List<StoredEvent>
        {
            new(now.AddHours(-1), MonitorState.Up, TimeSpan.FromMilliseconds(100), "New Event 1"),
            new(now.AddHours(-2), MonitorState.Up, TimeSpan.FromMilliseconds(110), "New Event 2"),
        };
        foreach (var ev in newEvents) await Storage.SaveEventAsync(monitorNameAllNew, ev);

        // Act
        var deletedCountNoEvents = await Storage.PurgeOldEventsAsync(retentionPeriod);
        var deletedCountAllNew = await Storage.PurgeOldEventsAsync(retentionPeriod);

        // Assert
        Assert.Equal(0, deletedCountNoEvents); // No events existed, so 0 deleted
        Assert.Equal(0, deletedCountAllNew);   // All events were newer than retention, so 0 deleted

        var remainingEventsAllNew = (await Storage.GetLastEventsAsync(monitorNameAllNew, 10)).ToList();
        Assert.Equal(newEvents.Count, remainingEventsAllNew.Count); // All new events should remain
    }
}
