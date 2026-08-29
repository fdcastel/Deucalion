using System.Diagnostics;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors;

/// <summary>
/// Regression for issue #15: <see cref="PullMonitor.AutoWarnTimeout"/> is written by the engine
/// thread and read by the monitor's own polling thread. A <c>TimeSpan?</c> is a 16-byte struct,
/// so a plain field can be observed half-written: HasValue from one write, Ticks from another.
/// </summary>
public class PullMonitorAutoWarnConcurrencyTests
{
    [Fact]
    public void AutoWarnTimeout_ConcurrentReadsNeverObserveATornValue()
    {
        var monitor = new HttpMonitor { Url = new Uri("https://example.com") };

        // Distinct on both halves of the struct: null flips HasValue and zeroes Ticks, the two
        // non-null values differ in every byte of Ticks. Any interleaving of halves yields a
        // value outside this set (e.g. HasValue=true with Ticks=0, or a mixed-bit tick count).
        TimeSpan?[] written =
        [
            null,
            TimeSpan.FromTicks(0x0101_0101_0101_0101),
            TimeSpan.FromTicks(0x7E7E_7E7E_7E7E_7E7E),
        ];
        var allowed = written.ToHashSet();

        // Dedicated threads and a stopwatch, deliberately: spinning on thread-pool threads and
        // stopping them with a timer starves the pool (timer callbacks run on it too) and stalls
        // every Task.Delay in test classes running in parallel with this one.
        var budget = TimeSpan.FromMilliseconds(300);
        var clock = Stopwatch.StartNew();

        var writer = new Thread(() =>
        {
            for (var i = 0; clock.Elapsed < budget; i++)
            {
                monitor.AutoWarnTimeout = written[i % written.Length];
            }
        });

        var readerCount = Math.Clamp(Environment.ProcessorCount - 1, 2, 4);
        var reads = new long[readerCount];
        var torn = new List<TimeSpan?>[readerCount];
        var readers = Enumerable.Range(0, readerCount).Select(r => new Thread(() =>
        {
            torn[r] = [];
            while (clock.Elapsed < budget)
            {
                var observed = monitor.AutoWarnTimeout;
                reads[r]++;
                if (!allowed.Contains(observed))
                {
                    torn[r].Add(observed);
                    if (torn[r].Count >= 8) break;
                }
            }
        })).ToArray();

        writer.Start();
        foreach (var reader in readers) reader.Start();
        writer.Join();
        foreach (var reader in readers) reader.Join();

        var totalReads = reads.Sum();
        var tornValues = torn.SelectMany(t => t).ToArray();

        Assert.True(totalReads > 0, "Readers did not run.");
        Assert.True(
            tornValues.Length == 0,
            $"Observed {tornValues.Length} torn AutoWarnTimeout value(s) in {totalReads} reads: " +
            string.Join(", ", tornValues.Take(8).Select(v => v is null ? "null" : $"{v.Value.Ticks:X16}")));
    }
}
