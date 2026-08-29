using System.Reflection;
using System.Runtime.InteropServices;
using Deucalion.Api.Http;
using Deucalion.Api.Models;
using Deucalion.Application.Configuration;
using Deucalion.Configuration;
using Deucalion.Monitors;
using Deucalion.Storage;

namespace Deucalion.Api.Endpoints;

/// <summary>
/// The agent-facing door into the API: a self-describing status summary (whole page, one group,
/// or one monitor) and a version endpoint. Everything here is read-only over storage -- it never
/// touches the monitor objects, so agent polling cannot race the engine.
/// </summary>
public static class DiscoveryEndpoints
{
    public const string StatusPath = "/api/status";
    public const string MonitorStatusPathTemplate = "/api/status/{name}";
    public const string VersionPath = "/api/version";
    public const string MonitorsPath = "/api/monitors";
    public const string EventsPath = "/api/monitors/events";
    public const string DocsPath = "/llms.txt";

    private const string GroupQueryParameter = "group";

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
        app.MapGet("/api/status/{monitorName}", WriteMonitorStatusAsync);
        app.MapGet(VersionPath, WriteVersionAsync);

        return app;
    }

    /// <summary>
    /// Writes the <c>/api/status</c> payload to <paramref name="context"/>, honouring an optional
    /// <c>?group=</c> filter. Public so the service host can serve the same document for
    /// <c>GET /</c> with <c>Accept: application/json</c>.
    /// </summary>
    public static async Task WriteStatusAsync(HttpContext context)
    {
        var storage = context.RequestServices.GetRequiredService<IStorage>();
        var configuration = context.RequestServices.GetRequiredService<ApplicationConfiguration>();
        var timeProvider = context.RequestServices.GetRequiredService<TimeProvider>();

        var group = context.Request.Query.TryGetValue(GroupQueryParameter, out var values) ? values.ToString() : null;

        var status = await BuildStatusAsync(storage, configuration, timeProvider, group, context.RequestAborted);
        if (status is null)
        {
            await DeucalionResults.GroupNotFound(group!, KnownGroups(configuration)).ExecuteAsync(context);
            return;
        }

        await Results.Json(status, DeucalionJsonContext.Default.StatusDto).ExecuteAsync(context);
    }

    private static async Task WriteMonitorStatusAsync(HttpContext context, string monitorName)
    {
        var storage = context.RequestServices.GetRequiredService<IStorage>();
        var configuration = context.RequestServices.GetRequiredService<ApplicationConfiguration>();
        var timeProvider = context.RequestServices.GetRequiredService<TimeProvider>();

        if (!configuration.Monitors.TryGetValue(monitorName, out var monitorConfiguration))
        {
            await DeucalionResults.MonitorNotFound(monitorName).ExecuteAsync(context);
            return;
        }

        var monitor = await BuildMonitorAsync(storage, monitorName, monitorConfiguration, context.RequestAborted);
        var escapedName = Uri.EscapeDataString(monitorName);
        var payload = new MonitorStatusDto(
            UpdatedAt: timeProvider.GetUtcNow().UtcDateTime,
            Monitor: monitor,
            Links: new MonitorStatusLinksDto(
                Self: $"{StatusPath}/{escapedName}",
                Status: StatusPath,
                Monitor: $"{MonitorsPath}/{escapedName}",
                Events: EventsPath
            )
        );

        await Results.Json(payload, DeucalionJsonContext.Default.MonitorStatusDto).ExecuteAsync(context);
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

    /// <returns>The summary, or null when <paramref name="group"/> is set but matches no monitor.</returns>
    internal static async Task<StatusDto?> BuildStatusAsync(IStorage storage, ApplicationConfiguration configuration, TimeProvider timeProvider, string? group, CancellationToken cancellationToken)
    {
        var selected = group is null
            ? configuration.Monitors.AsEnumerable()
            : configuration.Monitors.Where(kvp => string.Equals(kvp.Value.Group, group, StringComparison.OrdinalIgnoreCase));

        var monitors = await Task.WhenAll(selected.Select(kvp => BuildMonitorAsync(storage, kvp.Key, kvp.Value, cancellationToken)));
        if (group is not null && monitors.Length == 0)
        {
            return null;
        }

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
            Group: group,
            UpdatedAt: timeProvider.GetUtcNow().UtcDateTime,
            Availability: availabilities.Length > 0 ? Math.Round(availabilities.Average(), 2) : null,
            Monitors: monitors,
            Links: new StatusLinksDto(
                Self: group is null ? StatusPath : $"{StatusPath}?{GroupQueryParameter}={Uri.EscapeDataString(group)}",
                Monitors: MonitorsPath,
                MonitorStatus: MonitorStatusPathTemplate,
                Events: EventsPath,
                Version: VersionPath,
                Docs: DocsPath
            )
        );
    }

    internal static IEnumerable<string> KnownGroups(ApplicationConfiguration configuration) =>
        configuration.Monitors.Values
            .Select(m => m.Group)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)!;

    private static async Task<StatusMonitorDto> BuildMonitorAsync(IStorage storage, string name, PullMonitorConfiguration configuration, CancellationToken cancellationToken)
    {
        // Same rolling window as /api/monitors, so the two endpoints agree on availability.
        var stats = await storage.GetStatsAsync(name, historyCount: PullMonitor.StatsWindow, cancellationToken: cancellationToken);
        var state = stats?.LastState ?? MonitorState.Unknown;

        // `since` comes from the current run in storage, not from the stats window: a monitor
        // down for three days must say so, not "since the 60th-newest probe".
        var run = state == MonitorState.Unknown ? null : await storage.GetCurrentRunAsync(name, cancellationToken);

        return new StatusMonitorDto(
            Name: name,
            Group: configuration.Group,
            Type: MonitorConfigurationDto.From(configuration).Type,
            State: StateName(state),
            Since: run?.Since.UtcDateTime,
            SinceIsLowerBound: run?.SinceIsLowerBound,
            Availability: stats is null ? null : Math.Round(stats.Availability, 2),
            LatencyMs: (int?)run?.LastResponseTime?.TotalMilliseconds
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
