using Deucalion.Monitors;
using Deucalion.Monitors.Options;
using Deucalion.Tests.Monitors;
using Deucalion.Tests.Monitors.Options;
using Xunit;
using Xunit.Abstractions;

namespace Deucalion.Tests
{
    public class EngineTests
    {
        private readonly ITestOutputHelper _output;

        public EngineTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Engine_Works()
        {
            Engine engine = new()
            {
                Options = new() { Interval = TimeSpan.FromMilliseconds(500) }
            };

            List<IMonitor<MonitorOptions>> monitors = new()
            {
                new FakeMonitor() {
                    Options = new FakeMonitorOptions() {
                        Name = "d0",
                        Delay = TimeSpan.FromSeconds(0)
                    }
                },

                new FakeMonitor() {
                    Options = new FakeMonitorOptions() {
                        Name = "d500",
                        Delay = TimeSpan.FromMilliseconds(500)
                    }
                },
            };

            var responseCount = 0;

            void callback(MonitorResponse response)
            {
                _output.WriteLine($"{response.Name}: {response.IsUp} ({response.ResponseTime})");
                responseCount++;

                if (response.Name == "d0")
                {
                    Assert.True(response.ResponseTime < TimeSpan.FromMilliseconds(10));
                }
                else
                {
                    Assert.True(response.ResponseTime > TimeSpan.FromMilliseconds(500));
                }
            }

            try
            {
                using CancellationTokenSource cts = new(2000);
                await engine.RunAsync(monitors, callback, cts.Token);

            }
            catch (OperationCanceledException)
            {
                // NOP
            }

            Assert.Equal(4, responseCount);
        }
    }
}