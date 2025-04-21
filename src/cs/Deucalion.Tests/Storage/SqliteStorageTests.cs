using Deucalion.Events;
using Deucalion.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Deucalion.Tests.Storage;

public class SqliteStorageTests
{
    private readonly string _storagePath;
    private readonly string _dbFilePath;
    private readonly SqliteStorage _storage;

    public SqliteStorageTests()
    {
        // Use a unique path for each test run to avoid conflicts
        _storagePath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.SqliteStorage_{Guid.NewGuid()}");
        Directory.CreateDirectory(_storagePath);
        _dbFilePath = Path.Combine(_storagePath, "deucalion.sqlite.db"); // Construct the full path
        _storage = new SqliteStorage(_storagePath);
    }

    private async Task<(long? LastSeenUpTicks, long? LastSeenDownTicks)> GetLastStateChangeTimestampsAsync(string monitorName)
    {
        // Use the known path to the test database file
        using var connection = new SqliteConnection($"Data Source={_dbFilePath}");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT LastSeenUpTicks, LastSeenDownTicks
            FROM MonitorStateChanges
            WHERE MonitorName = @MonitorName;";
        command.Parameters.AddWithValue("@MonitorName", monitorName);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var upTicks = reader.IsDBNull(0) ? (long?)null : reader.GetInt64(0);
            var downTicks = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
            return (upTicks, downTicks);
        }
        return (null, null); // No record found
    }

    [Fact]
    public async Task SaveEventAsync_InsertsNewEventAndCalculatesStats()
    {
        // Arrange
        var monitorName = "test-monitor-1";
        var timestamp = DateTimeOffset.UtcNow;
        var responseTime = TimeSpan.FromMilliseconds(123);
        var storedEvent = new StoredEvent(timestamp, MonitorState.Up, responseTime, "OK");

        // Act
        await _storage.SaveEventAsync(monitorName, storedEvent);
        var stats = await _storage.GetStatsAsync(monitorName);

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
        var events = (await _storage.GetLastEventsAsync(monitorName)).ToList();
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
        await _storage.SaveEventAsync(monitorName, event1);
        await _storage.SaveEventAsync(monitorName, event2);
        await _storage.SaveEventAsync(monitorName, event3);
        var stats = await _storage.GetStatsAsync(monitorName);

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
        var finalStats = await _storage.GetStatsAsync(monitorName);
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
            await _storage.SaveEventAsync(monitorName, ev);
        }

        // Act
        var retrievedEvents = (await _storage.GetLastEventsAsync(monitorName, 3)).ToList();

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
        await _storage.SaveEventAsync(e1.Name, se1);

        var e2 = new MonitorChecked("m1", now.AddSeconds(-10), MonitorResponse.Warn(TimeSpan.FromSeconds(2), "warn"));
        var se2 = StoredEvent.From(e2);
        await _storage.SaveEventAsync(e2.Name, se2);

        var e3 = new MonitorChecked("m1", now, MonitorResponse.Down(text: "down")); // Fixed parameter name
        var se3 = StoredEvent.From(e3);
        await _storage.SaveEventAsync(e3.Name, se3);

        var evs1 = (await _storage.GetLastEventsAsync("m1")).ToList();

        // Assert count
        Assert.Equal(3, evs1.Count);

        // Assert order (GetLastEvents returns newest first)
        Assert.Equal(se3 with { At = se3.At.ToUniversalTime() }, evs1[0]);
        Assert.Equal(se2 with { At = se2.At.ToUniversalTime() }, evs1[1]);
        Assert.Equal(se1 with { At = se1.At.ToUniversalTime() }, evs1[2]);

        // Test count limit
        var evsLimited = (await _storage.GetLastEventsAsync("m1", 2)).ToList();
        Assert.Equal(2, evsLimited.Count);
        Assert.Equal(se3 with { At = se3.At.ToUniversalTime() }, evsLimited[0]);
        Assert.Equal(se2 with { At = se2.At.ToUniversalTime() }, evsLimited[1]);

        // Test different monitor
        var evsOther = (await _storage.GetLastEventsAsync("m2")).ToList();
        Assert.Empty(evsOther);
    }

    [Fact]
    public async Task SqliteStorage_SaveStateChangeAndGetStatsAsync_Works()
    {
        var now = DateTimeOffset.UtcNow; // Use UtcNow

        // Monitor 1: Up
        var e1_1 = new MonitorChecked("m1", now.AddMinutes(-5), MonitorResponse.Up(TimeSpan.FromMilliseconds(50), "up1"));
        await _storage.SaveEventAsync(e1_1.Name, StoredEvent.From(e1_1));
        await _storage.SaveLastStateChangeAsync(e1_1.Name, e1_1.At, MonitorState.Up);
        var e1_2 = new MonitorChecked("m1", now.AddMinutes(-2), MonitorResponse.Up(TimeSpan.FromMilliseconds(60), "up2"));
        await _storage.SaveEventAsync(e1_2.Name, StoredEvent.From(e1_2));
        // No state change here, LastSeenUp should remain e1_1.At

        // Monitor 2: Down -> Up -> Down
        var e2_1 = new MonitorChecked("m2", now.AddMinutes(-10), MonitorResponse.Down(text: "down1")); // Fixed parameter name
        await _storage.SaveEventAsync(e2_1.Name, StoredEvent.From(e2_1));
        await _storage.SaveLastStateChangeAsync(e2_1.Name, e2_1.At, MonitorState.Down);
        var e2_2 = new MonitorChecked("m2", now.AddMinutes(-5), MonitorResponse.Up(TimeSpan.FromMilliseconds(100), "up1"));
        await _storage.SaveEventAsync(e2_2.Name, StoredEvent.From(e2_2));
        await _storage.SaveLastStateChangeAsync(e2_2.Name, e2_2.At, MonitorState.Up);
        var e2_3 = new MonitorChecked("m2", now.AddMinutes(-1), MonitorResponse.Down(text: "down2")); // Fixed parameter name
        await _storage.SaveEventAsync(e2_3.Name, StoredEvent.From(e2_3));
        await _storage.SaveLastStateChangeAsync(e2_3.Name, e2_3.At, MonitorState.Down);

        // Monitor 3: Only Warn
        var e3_1 = new MonitorChecked("m3", now.AddMinutes(-3), MonitorResponse.Warn(TimeSpan.FromMilliseconds(200), "warn1"));
        await _storage.SaveEventAsync(e3_1.Name, StoredEvent.From(e3_1));
        // No Up/Down state change saved

        // --- Assertions ---

        // Monitor 1 Stats
        var s1 = await _storage.GetStatsAsync("m1");
        Assert.NotNull(s1);
        Assert.Equal(MonitorState.Up, s1.LastState);
        Assert.Equal(e1_2.At.ToUniversalTime(), s1.LastUpdate.ToUniversalTime());
        Assert.Equal(100.0, s1.Availability, precision: 1); // Both events are Up
        Assert.Equal(TimeSpan.FromMilliseconds(55), s1.AverageResponseTime); // (50+60)/2
        Assert.Equal(e1_1.At.ToUniversalTime(), s1.LastSeenUp?.ToUniversalTime()); // Set by SaveLastStateChange
        Assert.Null(s1.LastSeenDown);

        // Monitor 2 Stats
        var s2 = await _storage.GetStatsAsync("m2");
        Assert.NotNull(s2);
        Assert.Equal(MonitorState.Down, s2.LastState);
        Assert.Equal(e2_3.At.ToUniversalTime(), s2.LastUpdate.ToUniversalTime());
        // Availability: 1 Up, 2 Down -> (3 - 2) / 3 = 33.33...%
        Assert.Equal(1.0 / 3.0 * 100.0, s2.Availability, precision: 1);
        Assert.Equal(TimeSpan.FromMilliseconds(100), s2.AverageResponseTime); // Only e2_2 has response time
        Assert.Equal(e2_2.At.ToUniversalTime(), s2.LastSeenUp?.ToUniversalTime()); // Set by SaveLastStateChange
        Assert.Equal(e2_3.At.ToUniversalTime(), s2.LastSeenDown?.ToUniversalTime()); // Set by SaveLastStateChange

        // Monitor 3 Stats
        var s3 = await _storage.GetStatsAsync("m3");
        Assert.NotNull(s3);
        Assert.Equal(MonitorState.Warn, s3.LastState);
        Assert.Equal(e3_1.At.ToUniversalTime(), s3.LastUpdate.ToUniversalTime());
        // Availability: 1 Warn -> (1 - 0) / 1 = 100% (Warn counts as available)
        Assert.Equal(100.0, s3.Availability, precision: 1);
        Assert.Equal(TimeSpan.FromMilliseconds(200), s3.AverageResponseTime);
        Assert.Null(s3.LastSeenUp); // No SaveLastStateChange called
        Assert.Null(s3.LastSeenDown); // No SaveLastStateChange called

        // Non-existent Monitor Stats
        var s4 = await _storage.GetStatsAsync("m4");
        Assert.Null(s4);
    }

    // --- Tests for SaveLastStateChangeAsync ---

    [Fact]
    public async Task SaveLastStateChangeAsync_InsertUp_CreatesRecordWithUpTimestamp()
    {
        // Arrange
        var monitorName = "state-change-monitor-1";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        await _storage.SaveLastStateChangeAsync(monitorName, timestamp, MonitorState.Up);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = await GetLastStateChangeTimestampsAsync(monitorName);
        Assert.Equal(timestamp.UtcTicks, lastSeenUpTicks);
        Assert.Null(lastSeenDownTicks);
    }

    [Fact]
    public async Task SaveLastStateChangeAsync_InsertDown_CreatesRecordWithDownTimestamp()
    {
        // Arrange
        var monitorName = "state-change-monitor-2";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        await _storage.SaveLastStateChangeAsync(monitorName, timestamp, MonitorState.Down);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = await GetLastStateChangeTimestampsAsync(monitorName);
        Assert.Null(lastSeenUpTicks);
        Assert.Equal(timestamp.UtcTicks, lastSeenDownTicks);
    }

    [Fact]
    public async Task SaveLastStateChangeAsync_UpdateToUp_UpdatesUpTimestampAndPreservesDown()
    {
        // Arrange
        var monitorName = "state-change-monitor-3";
        var downTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var upTimestamp = DateTimeOffset.UtcNow;

        // Act
        await _storage.SaveLastStateChangeAsync(monitorName, downTimestamp, MonitorState.Down);
        await _storage.SaveLastStateChangeAsync(monitorName, upTimestamp, MonitorState.Up);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = await GetLastStateChangeTimestampsAsync(monitorName);
        Assert.Equal(upTimestamp.UtcTicks, lastSeenUpTicks);       // Up timestamp should be updated
        Assert.Equal(downTimestamp.UtcTicks, lastSeenDownTicks); // Down timestamp should be preserved
    }

    [Fact]
    public async Task SaveLastStateChangeAsync_UpdateToDown_UpdatesDownTimestampAndPreservesUp()
    {
        // Arrange
        var monitorName = "state-change-monitor-4";
        var upTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var downTimestamp = DateTimeOffset.UtcNow;

        // Act
        await _storage.SaveLastStateChangeAsync(monitorName, upTimestamp, MonitorState.Up);
        await _storage.SaveLastStateChangeAsync(monitorName, downTimestamp, MonitorState.Down);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = await GetLastStateChangeTimestampsAsync(monitorName);
        Assert.Equal(upTimestamp.UtcTicks, lastSeenUpTicks);         // Up timestamp should be preserved
        Assert.Equal(downTimestamp.UtcTicks, lastSeenDownTicks);   // Down timestamp should be updated
    }

    [Fact]
    public async Task SaveLastStateChangeAsync_IgnoreOtherStates_DoesNotInsertOrUpdate()
    {
        // Arrange
        var monitorNameInsert = "state-change-monitor-5-insert";
        var monitorNameUpdate = "state-change-monitor-5-update";
        var initialTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var warnTimestamp = DateTimeOffset.UtcNow;

        // Setup initial state for update test
        await _storage.SaveLastStateChangeAsync(monitorNameUpdate, initialTimestamp, MonitorState.Up);

        // Act
        await _storage.SaveLastStateChangeAsync(monitorNameInsert, warnTimestamp, MonitorState.Warn);
        await _storage.SaveLastStateChangeAsync(monitorNameInsert, warnTimestamp, MonitorState.Unknown);
        await _storage.SaveLastStateChangeAsync(monitorNameUpdate, warnTimestamp, MonitorState.Warn);
        await _storage.SaveLastStateChangeAsync(monitorNameUpdate, warnTimestamp, MonitorState.Unknown);

        // Assert
        // Verify no record was created for the insert attempts
        var (insertUp, insertDown) = await GetLastStateChangeTimestampsAsync(monitorNameInsert);
        Assert.Null(insertUp);
        Assert.Null(insertDown);

        // Verify the existing record was not updated
        var (updateUp, updateDown) = await GetLastStateChangeTimestampsAsync(monitorNameUpdate);
        Assert.Equal(initialTimestamp.UtcTicks, updateUp); // Should still be the initial Up timestamp
        Assert.Null(updateDown);                           // Should still be null
    }

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
            await _storage.SaveEventAsync(monitorName, ev);
        }

        // Pre-assertion: Ensure all events are saved initially
        var initialEvents = (await _storage.GetLastEventsAsync(monitorName, 10)).ToList();
        Assert.Equal(eventsToSave.Count, initialEvents.Count);

        // Act
        var deletedCount = await _storage.PurgeOldEventsAsync(retentionPeriod);

        // Assert
        Assert.Equal(3, deletedCount); // Expecting 3 old events to be deleted

        var remainingEvents = (await _storage.GetLastEventsAsync(monitorName, 10)).ToList();
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

    // --- PurgeOldEventsAsync Edge Cases ---

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
        foreach (var ev in eventsToSave) await _storage.SaveEventAsync(monitorName, ev);

        // Act
        var deletedCount = await _storage.PurgeOldEventsAsync(TimeSpan.Zero);

        // Assert
        Assert.Equal(0, deletedCount); // Nothing should be deleted with zero retention
        var remainingEvents = (await _storage.GetLastEventsAsync(monitorName, 10)).ToList();
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
        foreach (var ev in eventsToSave) await _storage.SaveEventAsync(monitorName, ev);

        // Act
        var deletedCount = await _storage.PurgeOldEventsAsync(retentionPeriod);

        // Assert
        Assert.Equal(eventsToSave.Count, deletedCount); // All events should be deleted
        var remainingEvents = (await _storage.GetLastEventsAsync(monitorName, 10)).ToList();
        Assert.Empty(remainingEvents); // No events should remain
    }

    [Fact]
    public async Task PurgeOldEventsAsync_NoEventsOrAllNew_PurgesNone()
    {
        // Arrange
        var monitorNameNoEvents = "purge-no-events";
        var monitorNameAllNew = "purge-all-new";
        var now = DateTimeOffset.UtcNow;
        var retentionPeriod = TimeSpan.FromDays(1); // Purge anything older than 1 day

        // Setup monitor with all new events
        var newEvents = new List<StoredEvent>
        {
            new(now.AddHours(-1), MonitorState.Up, TimeSpan.FromMilliseconds(100), "New Event 1"),
            new(now.AddHours(-2), MonitorState.Up, TimeSpan.FromMilliseconds(110), "New Event 2"),
        };
        foreach (var ev in newEvents) await _storage.SaveEventAsync(monitorNameAllNew, ev);

        // Act
        var deletedCountNoEvents = await _storage.PurgeOldEventsAsync(retentionPeriod);
        var deletedCountAllNew = await _storage.PurgeOldEventsAsync(retentionPeriod);

        // Assert
        Assert.Equal(0, deletedCountNoEvents); // No events existed, so 0 deleted
        Assert.Equal(0, deletedCountAllNew);   // All events were newer than retention, so 0 deleted

        var remainingEventsAllNew = (await _storage.GetLastEventsAsync(monitorNameAllNew, 10)).ToList();
        Assert.Equal(newEvents.Count, remainingEventsAllNew.Count); // All new events should remain
    }

    // --- GetStatsAsync Edge Cases ---

    [Fact]
    public async Task GetStatsAsync_OnlyDownEvents_ReturnsCorrectStats()
    {
        // Arrange
        var monitorName = "stats-only-down";
        var time1 = DateTimeOffset.UtcNow.AddMinutes(-2);
        var time2 = DateTimeOffset.UtcNow;
        var event1 = new StoredEvent(time1, MonitorState.Down, null, "Down 1");
        var event2 = new StoredEvent(time2, MonitorState.Down, null, "Down 2");

        await _storage.SaveEventAsync(monitorName, event1);
        await _storage.SaveEventAsync(monitorName, event2);
        await _storage.SaveLastStateChangeAsync(monitorName, time2, MonitorState.Down); // Save last state change

        // Act
        var stats = await _storage.GetStatsAsync(monitorName);

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

        await _storage.SaveEventAsync(monitorName, event1);
        await _storage.SaveEventAsync(monitorName, event2);
        await _storage.SaveEventAsync(monitorName, event3);
        await _storage.SaveEventAsync(monitorName, event4);

        // Act
        var stats = await _storage.GetStatsAsync(monitorName);

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
            await _storage.SaveEventAsync(monitorName, ev);
        }

        // Stats before purge (4 events: 3 Up, 1 Down -> 75% avail, avg RT (50+100+120)/3 = 90ms)
        var statsBefore = await _storage.GetStatsAsync(monitorName);
        Assert.NotNull(statsBefore);
        Assert.Equal(75.0, statsBefore.Availability);
        Assert.Equal(TimeSpan.FromMilliseconds(90), statsBefore.AverageResponseTime);

        // Act: Purge old events
        await _storage.PurgeOldEventsAsync(retentionPeriod);

        // Assert: Get stats again and verify recalculation
        var statsAfter = await _storage.GetStatsAsync(monitorName);
        Assert.NotNull(statsAfter);
        Assert.Equal(MonitorState.Up, statsAfter.LastState); // Last event was "Recent Up 2"
        Assert.Equal(now.ToUniversalTime(), statsAfter.LastUpdate.ToUniversalTime());
        // Availability (remaining): 2 Up, 0 Down -> 100%
        Assert.Equal(100.0, statsAfter.Availability);
        // Average Response Time (remaining): (100 + 120) / 2 = 110ms
        Assert.Equal(TimeSpan.FromMilliseconds(110), statsAfter.AverageResponseTime);
    }
}
