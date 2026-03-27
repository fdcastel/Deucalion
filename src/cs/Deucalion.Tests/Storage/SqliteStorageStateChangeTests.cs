using Xunit;

namespace Deucalion.Tests.Storage;

public class SqliteStorageStateChangeTests : SqliteStorageTestBase
{
    [Fact]
    public async Task SaveLastStateChangeAsync_InsertUp_CreatesRecordWithUpTimestamp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Arrange
        var monitorName = "state-change-monitor-1";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        await Storage.SaveLastStateChangeAsync(monitorName, timestamp, MonitorState.Up, cancellationToken);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = await GetLastStateChangeTimestampsAsync(monitorName);
        Assert.Equal(timestamp.UtcTicks, lastSeenUpTicks);
        Assert.Null(lastSeenDownTicks);
    }

    [Fact]
    public async Task SaveLastStateChangeAsync_InsertDown_CreatesRecordWithDownTimestamp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Arrange
        var monitorName = "state-change-monitor-2";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        await Storage.SaveLastStateChangeAsync(monitorName, timestamp, MonitorState.Down, cancellationToken);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = await GetLastStateChangeTimestampsAsync(monitorName);
        Assert.Null(lastSeenUpTicks);
        Assert.Equal(timestamp.UtcTicks, lastSeenDownTicks);
    }

    [Fact]
    public async Task SaveLastStateChangeAsync_UpdateToUp_UpdatesUpTimestampAndPreservesDown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Arrange
        var monitorName = "state-change-monitor-3";
        var downTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var upTimestamp = DateTimeOffset.UtcNow;

        // Act
        await Storage.SaveLastStateChangeAsync(monitorName, downTimestamp, MonitorState.Down, cancellationToken);
        await Storage.SaveLastStateChangeAsync(monitorName, upTimestamp, MonitorState.Up, cancellationToken);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = await GetLastStateChangeTimestampsAsync(monitorName);
        Assert.Equal(upTimestamp.UtcTicks, lastSeenUpTicks);       // Up timestamp should be updated
        Assert.Equal(downTimestamp.UtcTicks, lastSeenDownTicks); // Down timestamp should be preserved
    }

    [Fact]
    public async Task SaveLastStateChangeAsync_UpdateToDown_UpdatesDownTimestampAndPreservesUp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Arrange
        var monitorName = "state-change-monitor-4";
        var upTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var downTimestamp = DateTimeOffset.UtcNow;

        // Act
        await Storage.SaveLastStateChangeAsync(monitorName, upTimestamp, MonitorState.Up, cancellationToken);
        await Storage.SaveLastStateChangeAsync(monitorName, downTimestamp, MonitorState.Down, cancellationToken);

        // Assert
        var (lastSeenUpTicks, lastSeenDownTicks) = await GetLastStateChangeTimestampsAsync(monitorName);
        Assert.Equal(upTimestamp.UtcTicks, lastSeenUpTicks);         // Up timestamp should be preserved
        Assert.Equal(downTimestamp.UtcTicks, lastSeenDownTicks);   // Down timestamp should be updated
    }

    [Fact]
    public async Task SaveLastStateChangeAsync_IgnoreOtherStates_DoesNotInsertOrUpdate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Arrange
        var monitorNameInsert = "state-change-monitor-5-insert";
        var monitorNameUpdate = "state-change-monitor-5-update";
        var initialTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var warnTimestamp = DateTimeOffset.UtcNow;

        // Setup initial state for update test
        await Storage.SaveLastStateChangeAsync(monitorNameUpdate, initialTimestamp, MonitorState.Up, cancellationToken);

        // Act
        await Storage.SaveLastStateChangeAsync(monitorNameInsert, warnTimestamp, MonitorState.Warn, cancellationToken);
        await Storage.SaveLastStateChangeAsync(monitorNameInsert, warnTimestamp, MonitorState.Unknown, cancellationToken);
        await Storage.SaveLastStateChangeAsync(monitorNameUpdate, warnTimestamp, MonitorState.Warn, cancellationToken);
        await Storage.SaveLastStateChangeAsync(monitorNameUpdate, warnTimestamp, MonitorState.Unknown, cancellationToken);

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
}
