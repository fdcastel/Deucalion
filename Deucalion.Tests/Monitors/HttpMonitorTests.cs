using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class HttpMonitorTests
    {
        [Fact]
        public async Task HttpMonitor_ReturnsUp_WhenReachable()
        {
            HttpMonitor httpMonitor = new() { Options = new() { Url = new Uri("https://google.com") } };
            var result = await httpMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result);
        }

        [Fact]
        public async Task HttpMonitor_ReturnsDown_WhenUnreachable()
        {
            HttpMonitor httpMonitor = new() { Options = new() { Url = new Uri("https://google.com:12345"), Timeout = TimeSpan.FromMilliseconds(200) } };
            var result = await httpMonitor.QueryAsync();
            Assert.Equal(MonitorState.Down, result);
        }

        [Fact]
        public async Task HttpMonitor_WorksWith_ExpectedStatusCode()
        {
            HttpMonitor httpMonitor = new() { Options = new() { Url = new Uri("https://api.google.com/") } };
            var result = await httpMonitor.QueryAsync();
            Assert.Equal(MonitorState.Down, result);

            httpMonitor = new() { Options = new() { Url = new Uri("https://api.google.com/"), ExpectedStatusCode = System.Net.HttpStatusCode.NotFound } };
            result = await httpMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result);
        }

        [Fact]
        public async Task HttpMonitor_WorksWith_ExpectedResponseBodyPattern()
        {
            HttpMonitor httpMonitor = new() { Options = new() { Url = new Uri("https://api.github.com"), ExpectedResponseBodyPattern = "{}" } };
            var result = await httpMonitor.QueryAsync();
            Assert.Equal(MonitorState.Down, result);

            httpMonitor = new() { Options = new() { Url = new Uri("https://api.github.com"), ExpectedResponseBodyPattern = "current_user_url" } };
            result = await httpMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result);
        }
    }
}
