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

            var eventCount = new Dictionary<Type, int>();

            void MonitorCallback(MonitorEvent monitorEvent)
            {
                _output.WriteLine(monitorEvent.ToString());

                var prior = eventCount.TryGetValue(monitorEvent.GetType(), out var c) ? c : 0;
                eventCount[monitorEvent.GetType()] = prior + 1;
            }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                var start = DateTime.Now;

                await Task.Delay(pulse / 2);
                m1.CheckIn();
                m2.CheckIn();

                await Task.Delay(pulse);
                m1.CheckIn();

                await Task.Delay(pulse);
                m2.CheckIn();

                await Task.Delay(pulse);
                m1.CheckIn();
                m2.CheckIn();
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

            Assert.Equal(6, eventCount[typeof(CheckedIn)]);
            Assert.Equal(2, eventCount[typeof(CheckInMissed)]);
            Assert.Equal(4, eventCount[typeof(StateChanged)]);
        }
    }
}
