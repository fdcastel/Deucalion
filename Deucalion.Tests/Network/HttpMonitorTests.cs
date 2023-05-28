using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Network;

public class HttpMonitorTests
{
    [Fact]
    public async Task HttpMonitor_ReturnsUp_WhenReachable()
    {
        HttpMonitor httpMonitor = new() { Url = new Uri("https://google.com") };
        var result = await httpMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, result.State);
        Assert.Null(result.ResponseText);
    }

    [Fact]
    public async Task HttpMonitor_ReturnsDown_WhenUnreachable()
    {
        HttpMonitor httpMonitor = new() { Url = new Uri("https://google.com:12345"), Timeout = TimeSpan.FromMilliseconds(200) };
        var result = await httpMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Timeout", result.ResponseText);
    }

    [Fact]
    public async Task HttpMonitor_WorksWith_ExpectedStatusCode()
    {
        HttpMonitor httpMonitor = new() { Url = new Uri("https://api.google.com/") };
        var result = await httpMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Not Found", result.ResponseText);

        httpMonitor = new() { Url = new Uri("https://api.google.com/"), ExpectedStatusCode = System.Net.HttpStatusCode.NotFound };
        result = await httpMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, result.State);
        Assert.Null(result.ResponseText);
    }

    [Fact]
    public async Task HttpMonitor_WorksWith_ExpectedResponseBodyPattern()
    {
        HttpMonitor httpMonitor = new() { Url = new Uri("https://api.github.com"), ExpectedResponseBodyPattern = "{}" };
        var result = await httpMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, result.State);
        Assert.StartsWith("Unexpected response:", result.ResponseText);

        httpMonitor = new() { Url = new Uri("https://api.github.com"), ExpectedResponseBodyPattern = "current_user_url" };
        result = await httpMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, result.State);
        Assert.Null(result.ResponseText);
    }
}
