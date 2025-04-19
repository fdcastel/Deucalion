using Deucalion.Events;
using Deucalion.Storage;
using Microsoft.Data.Sqlite; // Needed for direct DB verification
using Xunit;

namespace Deucalion.Tests.Storage;

public class SqliteStorageTests : IDisposable
{
    private readonly string _storagePath;
    private readonly string _dbFilePath; // Store the full path
    private readonly SqliteStorage _storage;

    public SqliteStorageTests()
    {
        // Use a unique path for each test run to avoid conflicts
        _storagePath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.SqliteStorage_{Guid.NewGuid()}");
        Directory.CreateDirectory(_storagePath);
        _dbFilePath = Path.Combine(_storagePath, "deucalion.sqlite.db"); // Construct the full path
        _storage = new SqliteStorage(_storagePath);
    }

    // Helper method to query the database directly for verification
    private (long? LastSeenUpTicks, long? LastSeenDownTicks) GetLastStateChangeTimestamps(string monitorName)
    {
        // Use the known path to the test database file
        using var connection = new SqliteConnection($"Data Source={_dbFilePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT LastSeenUpTicks, LastSeenDownTicks
            FROM MonitorStateChanges
            WHERE MonitorName = @MonitorName;";
        command.Parameters.AddWithValue("@MonitorName", monitorName);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var upTicks = reader.IsDBNull(0) ? (long?)null : reader.GetInt64(0);
            var downTicks = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
            return (upTicks, downTicks);
        }
        return (null, null); // No record found
    }

    [Fact]
    public void SaveEvent_InsertsNewEventAndCalculatesStats() // Changed to sync
    {
        // Arrange
        var monitorName = "test-monitor-1";
        var timestamp = DateTimeOffset.UtcNow;
        var responseTime = TimeSpan.FromMilliseconds(123);
        var storedEvent = new StoredEvent(timestamp, MonitorState.Up, responseTime, "OK");

        // Act
        _storage.SaveEvent(monitorName, storedEvent); // Changed to sync
        var stats = _storage.GetStats(monitorName); // Changed to sync

        // Assert
        // Verify stats directly, and implicitly test DB state via GetLastEvents/GetStats
        Assert.NotNull(stats);
        Assert.Equal(MonitorState.Up, stats.LastState);
        Assert.Equal(timestamp.ToUniversalTime(), stats.LastUpdate.ToUniversalTime()); // Compare UTC
        Assert.Null(stats.LastSeenUp); // SaveEvent doesn't update LastSeenUp/Down
        Assert.Null(stats.LastSeenDown);
        Assert.Equal(100.0, stats.Availability); // Only one event, which is Up
        Assert.Equal(responseTime, stats.AverageResponseTime);

        // Explicitly check GetLastEvents
        var events = _storage.GetLastEvents(monitorName).ToList();
        Assert.Single(events);
        Assert.Equal(storedEvent with { At = storedEvent.At.ToUniversalTime() }, events[0]);
    }

    [Fact]
    public void SaveEvent_UpdatesStatsCorrectly() // Changed to sync, simplified focus
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
        _storage.SaveEvent(monitorName, event1);
        _storage.SaveEvent(monitorName, event2);
        _storage.SaveEvent(monitorName, event3); // Changed to sync
        var stats = _storage.GetStats(monitorName); // Changed to sync

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

        // Verify GetStats reflects the latest state (but without LastSeenUp/Down from SaveEvent)
        var finalStats = _storage.GetStats(monitorName);
        Assert.NotNull(finalStats);
        Assert.Equal(stats.LastState, finalStats.LastState);
        Assert.Equal(stats.LastUpdate, finalStats.LastUpdate);
        Assert.Equal(stats.Availability, finalStats.Availability);
        Assert.Equal(stats.AverageResponseTime, finalStats.AverageResponseTime);
        Assert.Null(finalStats.LastSeenUp); // Still null
        Assert.Null(finalStats.LastSeenDown); // Still null
    }

    [Fact]
    public void GetLastEvents_ReturnsCorrectEvents() // Changed to sync
    {
        // Arrange
        var monitorName = "test-monitor-3";
        var baseTime = DateTimeOffset.UtcNow;
        var eventsToSave = Enumerable.Range(0, 5)
            .Select(i => new StoredEvent(baseTime.AddMinutes(-i), MonitorState.Up, TimeSpan.FromMilliseconds(100 + i), $"OK {i}"))
            .ToList();

        foreach (var ev in eventsToSave.OrderBy(e => e.At)) // Save in chronological order
        {
            _storage.SaveEvent(monitorName, ev);
        }

        // Act
        var retrievedEvents = _storage.GetLastEvents(monitorName, 3).ToList(); // Changed to sync

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

    // Removed PurgeEventsOlderThanAsync test
    // Removed GetAllMonitorNamesAsync test
    // Removed GetStatsAsync test (covered by other tests)

    // Renamed original test to focus on Save/Retrieve
    [Fact]
    public void SqliteStorage_SaveAndRetrieveEvents_Works()
    {
        var now = DateTimeOffset.UtcNow; // Use UtcNow for consistency
        var e1 = new MonitorChecked("m1", now.AddSeconds(-20), MonitorResponse.Up(TimeSpan.FromSeconds(1), "test"));
        var se1 = StoredEvent.From(e1);
        _storage.SaveEvent(e1.Name, se1);

        var e2 = new MonitorChecked("m1", now.AddSeconds(-10), MonitorResponse.Warn(TimeSpan.FromSeconds(2), "warn"));
        var se2 = StoredEvent.From(e2);
        _storage.SaveEvent(e2.Name, se2);

        var e3 = new MonitorChecked("m1", now, MonitorResponse.Down(text: "down")); // Fixed parameter name
        var se3 = StoredEvent.From(e3);
        _storage.SaveEvent(e3.Name, se3);

        var evs1 = _storage.GetLastEvents("m1").ToList();

        // Assert count
        Assert.Equal(3, evs1.Count);

        // Assert order (GetLastEvents returns newest first)
        Assert.Equal(se3 with { At = se3.At.ToUniversalTime() }, evs1[0]);
        Assert.Equal(se2 with { At = se2.At.ToUniversalTime() }, evs1[1]);
        Assert.Equal(se1 with { At = se1.At.ToUniversalTime() }, evs1[2]);

        // Test count limit
        var evsLimited = _storage.GetLastEvents("m1", 2).ToList();
        Assert.Equal(2, evsLimited.Count);
        Assert.Equal(se3 with { At = se3.At.ToUniversalTime() }, evsLimited[0]);
        Assert.Equal(se2 with { At = se2.At.ToUniversalTime() }, evsLimited[1]);

        // Test different monitor
        var evsOther = _storage.GetLastEvents("m2").ToList();
        Assert.Empty(evsOther);
    }

    // Renamed original test to focus on State Change / Stats
    [Fact]
    public void SqliteStorage_SaveStateChangeAndGetStats_Works()
    {
        var now = DateTimeOffset.UtcNow; // Use UtcNow

        // Monitor 1: Up
        var e1_1 = new MonitorChecked("m1", now.AddMinutes(-5), MonitorResponse.Up(TimeSpan.FromMilliseconds(50), "up1"));
        _storage.SaveEvent(e1_1.Name, StoredEvent.From(e1_1));
        _storage.SaveLastStateChange(e1_1.Name, e1_1.At, MonitorState.Up);
        var e1_2 = new MonitorChecked("m1", now.AddMinutes(-2), MonitorResponse.Up(TimeSpan.FromMilliseconds(60), "up2"));
        _storage.SaveEvent(e1_2.Name, StoredEvent.From(e1_2));
        // No state change here, LastSeenUp should remain e1_1.At

        // Monitor 2: Down -> Up -> Down
        var e2_1 = new MonitorChecked("m2", now.AddMinutes(-10), MonitorResponse.Down(text: "down1")); // Fixed parameter name
        _storage.SaveEvent(e2_1.Name, StoredEvent.From(e2_1));
        _storage.SaveLastStateChange(e2_1.Name, e2_1.At, MonitorState.Down);
        var e2_2 = new MonitorChecked("m2", now.AddMinutes(-5), MonitorResponse.Up(TimeSpan.FromMilliseconds(100), "up1"));
        _storage.SaveEvent(e2_2.Name, StoredEvent.From(e2_2));
        _storage.SaveLastStateChange(e2_2.Name, e2_2.At, MonitorState.Up);
        var e2_3 = new MonitorChecked("m2", now.AddMinutes(-1), MonitorResponse.Down(text: "down2")); // Fixed parameter name
        _storage.SaveEvent(e2_3.Name, StoredEvent.From(e2_3));
        _storage.SaveLastStateChange(e2_3.Name, e2_3.At, MonitorState.Down);

        // Monitor 3: Only Warn
        var e3_1 = new MonitorChecked("m3", now.AddMinutes(-3), MonitorResponse.Warn(TimeSpan.FromMilliseconds(200), "warn1"));
        _storage.SaveEvent(e3_1.Name, StoredEvent.From(e3_1));
        // No Up/Down state change saved

        // --- Assertions ---

        // Monitor 1 Stats
        var s1 = _storage.GetStats("m1");
        Assert.NotNull(s1);
        Assert.Equal(MonitorState.Up, s1.LastState);
        Assert.Equal(e1_2.At.ToUniversalTime(), s1.LastUpdate.ToUniversalTime());
        Assert.Equal(100.0, s1.Availability, precision: 1); // Both events are Up
        Assert.Equal(TimeSpan.FromMilliseconds(55), s1.AverageResponseTime); // (50+60)/2
        Assert.Equal(e1_1.At.ToUniversalTime(), s1.LastSeenUp?.ToUniversalTime()); // Set by SaveLastStateChange
        Assert.Null(s1.LastSeenDown);

        // Monitor 2 Stats
        var s2 = _storage.GetStats("m2");
        Assert.NotNull(s2);
        Assert.Equal(MonitorState.Down, s2.LastState);
        Assert.Equal(e2_3.At.ToUniversalTime(), s2.LastUpdate.ToUniversalTime());
        // Availability: 1 Up, 2 Down -> (3 - 2) / 3 = 33.33...%
        Assert.Equal(1.0 / 3.0 * 100.0, s2.Availability, precision: 1);
        Assert.Equal(TimeSpan.FromMilliseconds(100), s2.AverageResponseTime); // Only e2_2 has response time
        Assert.Equal(e2_2.At.ToUniversalTime(), s2.LastSeenUp?.ToUniversalTime()); // Set by SaveLastStateChange
        Assert.Equal(e2_3.At.ToUniversalTime(), s2.LastSeenDown?.ToUniversalTime()); // Set by SaveLastStateChange

        // Monitor 3 Stats
        var s3 = _storage.GetStats("m3");
        Assert.NotNull(s3);
        Assert.Equal(MonitorState.Warn, s3.LastState);
        Assert.Equal(e3_1.At.ToUniversalTime(), s3.LastUpdate.ToUniversalTime());
        // Availability: 1 Warn -> (1 - 0) / 1 = 100% (Warn counts as available)
        Assert.Equal(100.0, s3.Availability, precision: 1);
        Assert.Equal(TimeSpan.FromMilliseconds(200), s3.AverageResponseTime);
        Assert.Null(s3.LastSeenUp); // No SaveLastStateChange called
        Assert.Null(s3.LastSeenDown); // No SaveLastStateChange called

        // Non-existent Monitor Stats
        var s4 = _storage.GetStats("m4");
        Assert.Null(s4);
    }

    // --- Tests for SaveLastStateChange ---

    [Fact]
    public void SaveLastStateChange_InsertUp_CreatesRecordWithUpTimestamp()
    {
        // Arrange
        var monitorName = "state-change-monitor-1";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        _storage.SaveLastStateChange(monitorName, timestamp, MonitorState.Up);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = GetLastStateChangeTimestamps(monitorName);
        Assert.Equal(timestamp.UtcTicks, lastSeenUpTicks);
        Assert.Null(lastSeenDownTicks);
    }

    [Fact]
    public void SaveLastStateChange_InsertDown_CreatesRecordWithDownTimestamp()
    {
        // Arrange
        var monitorName = "state-change-monitor-2";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        _storage.SaveLastStateChange(monitorName, timestamp, MonitorState.Down);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = GetLastStateChangeTimestamps(monitorName);
        Assert.Null(lastSeenUpTicks);
        Assert.Equal(timestamp.UtcTicks, lastSeenDownTicks);
    }

    [Fact]
    public void SaveLastStateChange_UpdateToUp_UpdatesUpTimestampAndPreservesDown()
    {
        // Arrange
        var monitorName = "state-change-monitor-3";
        var downTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var upTimestamp = DateTimeOffset.UtcNow;

        // Act
        _storage.SaveLastStateChange(monitorName, downTimestamp, MonitorState.Down); // Initial Down state
        _storage.SaveLastStateChange(monitorName, upTimestamp, MonitorState.Up);     // Update to Up

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = GetLastStateChangeTimestamps(monitorName);
        Assert.Equal(upTimestamp.UtcTicks, lastSeenUpTicks);       // Up timestamp should be updated
        Assert.Equal(downTimestamp.UtcTicks, lastSeenDownTicks); // Down timestamp should be preserved
    }

    [Fact]
    public void SaveLastStateChange_UpdateToDown_UpdatesDownTimestampAndPreservesUp()
    {
        // Arrange
        var monitorName = "state-change-monitor-4";
        var upTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var downTimestamp = DateTimeOffset.UtcNow;

        // Act
        _storage.SaveLastStateChange(monitorName, upTimestamp, MonitorState.Up);       // Initial Up state
        _storage.SaveLastStateChange(monitorName, downTimestamp, MonitorState.Down); // Update to Down

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = GetLastStateChangeTimestamps(monitorName);
        Assert.Equal(upTimestamp.UtcTicks, lastSeenUpTicks);         // Up timestamp should be preserved
        Assert.Equal(downTimestamp.UtcTicks, lastSeenDownTicks);   // Down timestamp should be updated
    }

    [Fact]
    public void SaveLastStateChange_IgnoreOtherStates_DoesNotInsertOrUpdate()
    {
        // Arrange
        var monitorNameInsert = "state-change-monitor-5-insert";
        var monitorNameUpdate = "state-change-monitor-5-update";
        var initialTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var warnTimestamp = DateTimeOffset.UtcNow;

        // Setup initial state for update test
        _storage.SaveLastStateChange(monitorNameUpdate, initialTimestamp, MonitorState.Up);

        // Act
        _storage.SaveLastStateChange(monitorNameInsert, warnTimestamp, MonitorState.Warn);    // Try inserting with Warn
        _storage.SaveLastStateChange(monitorNameInsert, warnTimestamp, MonitorState.Unknown); // Try inserting with Unknown
        _storage.SaveLastStateChange(monitorNameUpdate, warnTimestamp, MonitorState.Warn);    // Try updating with Warn
        _storage.SaveLastStateChange(monitorNameUpdate, warnTimestamp, MonitorState.Unknown); // Try updating with Unknown

        // Assert
        // Verify no record was created for the insert attempts
        var (insertUp, insertDown) = GetLastStateChangeTimestamps(monitorNameInsert);
        Assert.Null(insertUp);
        Assert.Null(insertDown);

        // Verify the existing record was not updated
        var (updateUp, updateDown) = GetLastStateChangeTimestamps(monitorNameUpdate);
        Assert.Equal(initialTimestamp.UtcTicks, updateUp); // Should still be the initial Up timestamp
        Assert.Null(updateDown);                           // Should still be null
    }

    public void Dispose()
    {
        _storage.Dispose();
        // Attempt to clean up the storage directory
        try
        {
            if (Directory.Exists(_storagePath))
            {
                Directory.Delete(_storagePath, true);
            }
        }
        catch (IOException) { /* Ignore potential file locking issues during cleanup */ }
        catch (UnauthorizedAccessException) { /* Ignore potential permission issues */ }
        GC.SuppressFinalize(this);
    }
}
