using Deucalion.Api.Endpoints;
using Deucalion.Application.Configuration;
using Deucalion.Storage;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Deucalion.Tests.Api;

/// <summary>
/// The parts of <see cref="DiscoveryEndpoints"/> that the integration suite cannot observe: the
/// engine probes every monitor once at host start there, so a never-probed monitor only exists
/// against a storage double.
/// </summary>
public class DiscoveryEndpointsTests
{
    private static readonly ApplicationConfiguration Configuration = ApplicationConfiguration.ReadFromString(
        """
        monitors:
          alpha: !checkin
            group: A
          beta: !checkin
            group: B
        """);

    [Fact]
    public async Task NeverProbedMonitor_IsUnknown_WithoutSince_AndWithoutQueryingTheRun()
    {
        var storage = new EmptyStorage();

        var status = await DiscoveryEndpoints.BuildStatusAsync(storage, Configuration, new FakeTimeProvider(), group: null, TestContext.Current.CancellationToken);

        Assert.NotNull(status);
        Assert.Equal("operational", status.Status); // nothing known to be down
        Assert.Null(status.Availability);
        Assert.All(status.Monitors, m =>
        {
            Assert.Equal("unknown", m.State);
            Assert.Null(m.Since);
            Assert.Null(m.SinceIsLowerBound);
            Assert.Null(m.LatencyMs);
        });
        Assert.Equal(0, storage.RunQueries);
    }

    [Fact]
    public async Task GroupFilter_UnknownGroup_YieldsNull_AndKnownGroupsAreListed()
    {
        var status = await DiscoveryEndpoints.BuildStatusAsync(new EmptyStorage(), Configuration, new FakeTimeProvider(), group: "nope", TestContext.Current.CancellationToken);

        Assert.Null(status);
        Assert.Equal(["A", "B"], DiscoveryEndpoints.KnownGroups(Configuration));
    }

    private sealed class EmptyStorage : IStorage
    {
        public int RunQueries { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<MonitorStats?> GetStatsAsync(string monitorName, int historyCount = 60, CancellationToken cancellationToken = default) => Task.FromResult<MonitorStats?>(null);
        public Task<IEnumerable<StoredEvent>> GetLastEventsAsync(string monitorName, int count = 60, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<StoredEvent>());
        public Task<MonitorRun?> GetCurrentRunAsync(string monitorName, CancellationToken cancellationToken = default)
        {
            RunQueries++;
            return Task.FromResult<MonitorRun?>(null);
        }
        public Task SaveEventAsync(string monitorName, StoredEvent storedEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, int maxEventsPerMonitor, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
