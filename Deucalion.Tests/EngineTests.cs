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
        public void Engine_Works()
        {
            var pulse = TimeSpan.FromMilliseconds(500);

            Engine engine = new();

            CheckInMonitor m1 = new() { Options = new() { Name = "m1", IntervalToDown = pulse * 1.1 } };
            CheckInMonitor m2 = new() { Options = new() { Name = "m2", IntervalToDown = pulse * 1.1 } };

            List<IMonitor<MonitorOptions>> monitors = new() { m1, m2 };

            var responseCount = 0;
            var changeCount = 0;

            void MonitorCallback(MonitorEvent monitorEvent)
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
                var start = DateTime.Now;

                void CheckIn(CheckInMonitor monitor)
                {
                    monitor.CheckIn();
                    _output.WriteLine($"        Checkin {{ Name = {monitor.Options.Name}, At = {DateTime.Now - start} }}");
                }

                await Task.Delay(pulse / 2);
                CheckIn(m1);
                CheckIn(m2);

                await Task.Delay(pulse);
                CheckIn(m1);

                await Task.Delay(pulse);
                CheckIn(m2);

                await Task.Delay(pulse);
                CheckIn(m1);
                CheckIn(m2);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            try
            {
                using CancellationTokenSource cts = new(pulse * 4.5);
                engine.Run(monitors, MonitorCallback, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // NOP
            }

            Assert.Equal(10, responseCount);
            Assert.Equal(4, changeCount);
        }
    }
}
