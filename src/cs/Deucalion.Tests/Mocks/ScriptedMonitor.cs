using Deucalion.Monitors;

namespace Deucalion.Tests.Mocks;

/// <summary>
/// A monitor whose every probe is scripted by a delegate over the probe index.
/// Intervals default to zero so <see cref="Deucalion.Application.MonitorExtensions.RunAsync"/>
/// free-runs: tests pace themselves by doing blocking reads on the event channel
/// rather than by advancing a clock, which removes timing races entirely.
/// </summary>
internal sealed class ScriptedMonitor(Func<int, MonitorResponse> script) : PullMonitor
{
    private int _probeCount;

    /// <summary>Number of times <see cref="QueryAsync"/> has been entered.</summary>
    public int ProbeCount => _probeCount;

    public ScriptedMonitor(params MonitorState[] states)
        : this(i => new MonitorResponse(
            State: states[Math.Min(i, states.Length - 1)],
            ResponseTime: TimeSpan.FromMilliseconds(333)))
    {
    }

    public override Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default)
    {
        var index = _probeCount++;
        return Task.FromResult(script(index));
    }
}
