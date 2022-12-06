using Deucalion.Monitors;
using Deucalion.Monitors.Events;
using Deucalion.Monitors.Options;
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
            var pulse = TimeSpan.FromMilliseconds(500);

            Engine engine = new()
            {
                Options = new() { Interval = pulse }
            };

            var m1 = new CheckInMonitor()
            {
                Options = new() { Name = "m1", IntervalWhenUp = pulse }
            };

            var m2 = new CheckInMonitor()
            {
                Options = new() { Name = "m2", IntervalWhenUp = pulse }
            };

            List<IMonitor<MonitorOptions>> monitors = new() { m1, m2 };

            var responseCount = 0;
            var changeCount = 0;

            void callback(MonitorEvent monitorEvent)
            {
                _output.WriteLine(monitorEvent.ToString());

                switch (monitorEvent)
                {
                    case MonitorResponse _: responseCount++; break;
                    case MonitorChange _: changeCount++; break;
                }
            }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                await Task.Delay(pulse);
                m1.CheckIn();
                m2.CheckIn();

                await Task.Delay(pulse);
                m2.CheckIn();

                await Task.Delay(pulse);
                m1.CheckIn();

                await Task.Delay(pulse);
                m1.CheckIn();
                m2.CheckIn();
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            try
            {
                using CancellationTokenSource cts = new(pulse * 5);
                await engine.RunAsync(monitors, callback, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // NOP
            }

            Assert.Equal(10, responseCount);
        }
    }
}
