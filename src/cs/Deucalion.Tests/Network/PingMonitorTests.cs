using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Network;

/// <summary>
/// Real ICMP echo, which most containers block. Skipped unless DEUCALION_TESTS_NETWORK=1.
/// </summary>
public class PingMonitorTests
{
    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task PingMonitor_ReturnsUp_WhenReachable()
    {
        PingMonitor pingMonitor = new() { Host = "1.1.1.1" };
        var result = await pingMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Up, result.State);
        Assert.Null(result.ResponseText);
    }

    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task PingMonitor_ReturnsDown_WhenUnreachable()
    {
        PingMonitor pingMonitor = new() { Host = "8.8.8.7", Timeout = TimeSpan.FromMilliseconds(200) };
        var result = await pingMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Down, result.State);
        Assert.NotNull(result.ResponseText);
    }
}
