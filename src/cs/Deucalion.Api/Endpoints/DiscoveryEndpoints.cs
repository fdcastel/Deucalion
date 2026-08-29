using System.Reflection;
using System.Runtime.InteropServices;
using Deucalion.Api.Models;
using Deucalion.Application.Configuration;
using Deucalion.Configuration;
using Deucalion.Monitors;
using Deucalion.Storage;

namespace Deucalion.Api.Endpoints;

/// <summary>
/// The agent-facing door into the API: a self-describing status summary and a version endpoint.
/// Everything here is read-only over storage -- it never touches the monitor objects, so agent
/// polling cannot race the engine.
/// </summary>
public static class DiscoveryEndpoints
{
    public const string StatusPath = "/api/status";
    public const string VersionPath = "/api/version";
    public const string MonitorsPath = "/api/monitors";
    public const string EventsPath = "/api/monitors/events";
    public const string DocsPath = "/llms.txt";

    private static readonly StatusLinksDto Links = new(
        Self: StatusPath,
        Monitors: MonitorsPath,
        Events: EventsPath,
        Version: VersionPath,
        Docs: DocsPath
    );

    // Get version info from assembly -- https://stackoverflow.com/a/64793765/33244
    //   SourceRevisionId included since .NET 8 SDK -- https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/source-link
    // The entry assembly is the host (Deucalion.Service in production); under a test host it is
    // the test runner, so fall back to this assembly rather than reporting the runner's version.
    public static string InformationalVersion { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(DiscoveryEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    private static DateTime _startedAt;

    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app, TimeProvider timeProvider)
    {
        _startedAt = timeProvider.GetUtcNow().UtcDateTime;

        app.MapGet(StatusPath, WriteStatusAsync);
        app.MapGet(VersionPath, WriteVersionAsync);

        return app;
    }

    /// <summary>
    /// Writes the <c>/api/status</c> payload to <paramref name="context"/>. Public so the
    /// service host can serve the same document for <c>GET /</c> with <c>Accept: application/json</c>.
    /// </summary>
    public static async Task WriteStatusAsync(HttpContext context)
    {
        var storage = context.RequestServices.GetRequiredService<IStorage>();
        var configuration = context.RequestServices.GetRequiredService<ApplicationConfiguration>();
        var timeProvider = context.RequestServices.GetRequiredService<TimeProvider>();

        var status = await BuildStatusAsync(storage, configuration, timeProvider, context.RequestAborted);
        await Results.Json(status, DeucalionJsonContext.Default.StatusDto).ExecuteAsync(context);
    }

    private static Task WriteVersionAsync(HttpContext context)
    {
        var version = new VersionDto(
            Name: "Deucalion",
            Version: InformationalVersion,
            Runtime: RuntimeInformation.FrameworkDescription,
            StartedAt: _startedAt
        );
        return Results.Json(version, DeucalionJsonContext.Default.VersionDto).ExecuteAsync(context);
    }

    internal static async Task<StatusDto> BuildStatusAsync(IStorage storage, ApplicationConfiguration configuration, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var monitors = await Task.WhenAll(configuration.Monitors.Select(kvp => BuildMonitorAsync(storage, kvp.Key, kvp.Value, cancellationToken)));

        // Monitors that have never been probed do not vote: with no evidence either way they
        // must neither drag the summary into "degraded" nor mask a real outage.
        var known = monitors.Where(m => m.State != "unknown").ToArray();
        var down = known.Count(m => m.State == "down");
        var status = down switch
        {
            0 => "operational",
            _ when down == known.Length => "outage",
            _ => "degraded",
        };

        var availabilities = monitors.Where(m => m.Availability is not null).Select(m => m.Availability!.Value).ToArray();

        return new StatusDto(
            Status: status,
            UpdatedAt: timeProvider.GetUtcNow().UtcDateTime,
            Availability: availabilities.Length > 0 ? Math.Round(availabilities.Average(), 2) : null,
            Monitors: monitors,
            Links: Links
        );
    }

    private static async Task<StatusMonitorDto> BuildMonitorAsync(IStorage storage, string name, PullMonitorConfiguration configuration, CancellationToken cancellationToken)
    {
        // Same rolling window as /api/monitors, so the two endpoints agree on availability.
        var stats = await storage.GetStatsAsync(name, historyCount: PullMonitor.StatsWindow, cancellationToken: cancellationToken);
        var events = (await storage.GetLastEventsAsync(name, count: PullMonitor.StatsWindow, cancellationToken: cancellationToken)).ToArray();

        var state = stats?.LastState ?? MonitorState.Unknown;

        // `since`: the oldest event of the trailing run in the current state. Bounded by the
        // window, so a long-stable monitor reports "at least since".
        DateTime? since = null;
        int? latencyMs = null;
        if (events.Length > 0)
        {
            // Newest first.
            latencyMs = (int?)events[0].ResponseTime?.TotalMilliseconds;
            var run = events.TakeWhile(e => e.State == state).ToArray();
            since = run.Length > 0 ? run[^1].At.UtcDateTime : null;
        }

        return new StatusMonitorDto(
            Name: name,
            Group: configuration.Group,
            Type: MonitorConfigurationDto.From(configuration).Type,
            State: StateName(state),
            Since: since,
            Availability: stats is null ? null : Math.Round(stats.Availability, 2),
            LatencyMs: latencyMs
        );
    }

    internal static string StateName(MonitorState state) => state switch
    {
        MonitorState.Up => "up",
        MonitorState.Warn => "warn",
        MonitorState.Down => "down",
        MonitorState.Degraded => "degraded",
        _ => "unknown",
    };
}
