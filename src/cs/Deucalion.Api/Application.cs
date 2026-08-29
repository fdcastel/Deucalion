using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Deucalion.Api.Endpoints;
using Deucalion.Api.Http;
using Deucalion.Api.Models;
using Deucalion.Api.Options;
using Deucalion.Api.Services;
using Deucalion.Application;
using Deucalion.Application.Configuration;
using Deucalion.Configuration;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Deucalion.Storage;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Deucalion.Api;

public static class Application
{
    internal const string CheckInRateLimitPolicy = "checkin";
    internal const int CheckInRateLimitPerMinute = 60;

    public static WebApplicationBuilder ConfigureApplicationBuilder(this WebApplicationBuilder builder)
    {
        // Json.
        //
        // The ignore condition must be set here as well as on DeucalionJsonContext: inserting the
        // context's *resolver* into ASP.NET's JsonSerializerOptions does not carry over the
        // context's own JsonSourceGenerationOptions, so the two paths would disagree. Covered by
        // the href-omission assertion in ApiIntegrationTests.
        //
        // WhenWritingNull, not WhenWritingDefault: the latter would also drop `lastState: 0`
        // (Unknown), `availability: 0` and `fr: 0`, all of which the UI reads. (Note that
        // JsonIgnoreCondition is not a [Flags] enum -- the old
        // `WhenWritingDefault | WhenWritingNull` silently evaluated to 2|3 == 3 == WhenWritingNull.)
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, DeucalionJsonContext.Default);
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        // CORS
        builder.Services.AddCors();

        // Check-in rate limiting: the endpoint is unauthenticated (or guarded by a shared
        // secret) and reachable from anywhere, so bound how fast one client can hammer it.
        // Partitioned per client IP; a fixed window keeps the bookkeeping to one counter.
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(CheckInRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = CheckInRateLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        // Response compression
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml"]);
        });

        // Application configuration
        var deucalionOptions = new DeucalionOptions();
        builder.Configuration.GetSection(DeucalionOptions.SectionName).Bind(deucalionOptions);
        deucalionOptions.PageTitle ??= "Deucalion status";
        deucalionOptions.Validate(); // Before anything consumes them: a bad value must not reach the purge timer (#23).
        builder.Services.AddSingleton(_ => deucalionOptions);

        var applicationConfiguration = ApplicationConfiguration.ReadFromFile(deucalionOptions.ConfigurationFile ?? "deucalion.yaml");
        builder.Services.AddSingleton(_ => applicationConfiguration);

        // Build monitors from configuration
        var applicationMonitors = ApplicationMonitors.BuildFrom(applicationConfiguration);
        builder.Services.AddSingleton(_ => applicationMonitors);

        // Application services
        builder.Services.AddSingleton<SqliteStorage>(sp =>
        {
            var options = sp.GetRequiredService<DeucalionOptions>();
            return new SqliteStorage(options.StoragePath, sp.GetRequiredService<TimeProvider>());
        });
        builder.Services.AddSingleton<IStorage>(sp => sp.GetRequiredService<SqliteStorage>());
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddHostedService<EngineBackgroundService>();
        builder.Services.AddHostedService<PurgeBackgroundService>();
        builder.Services.AddSingleton<MonitorEventBroadcaster>();

        return builder;
    }

    public static WebApplication ConfigureApplication(this WebApplication app)
    {
        // Before the exception handler, so 500 responses carry CORS headers too. Methods and
        // headers must be allowed explicitly: with origin only, a preflight for a check-in
        // (POST + deucalion-checkin-secret) got no Allow-Methods/Allow-Headers and the browser
        // blocked the call (#23).
        app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        app.UseExceptionHandler(exceptionHandlerApp =>
            exceptionHandlerApp.Run(async context =>
            {
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                if (exceptionFeature?.Error is not null)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<WebApplication>>();
                    logger.LogError(exceptionFeature.Error, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                }

                await Results.Problem().ExecuteAsync(context);
            })
        );

        app.UseRateLimiter();

        app.UseResponseCompression();

        app.Services.GetRequiredService<IStorage>().InitializeAsync().GetAwaiter().GetResult();

        var applicationConfiguration = app.Services.GetRequiredService<ApplicationConfiguration>();
        var applicationMonitors = app.Services.GetRequiredService<ApplicationMonitors>();

        // Setup Api endpoints
        app.MapGet("/api/configuration", (DeucalionOptions options) =>
            Results.Ok(new PageConfigurationDto(options.PageTitle)));

        app.MapDiscoveryEndpoints(app.Services.GetRequiredService<TimeProvider>());

        app.MapGet("/api/monitors/{monitorName?}", async (IStorage storage, string? monitorName, CancellationToken cancellationToken) =>
        {
            if (monitorName is null)
            {
                var tasks = applicationConfiguration.Monitors
                                  .Select(kvp => BuildMonitorDtoAsync(storage, applicationMonitors, kvp.Value, kvp.Key, cancellationToken));
                var results = await Task.WhenAll(tasks);
                return Results.Ok(results);
            }

            if (!applicationConfiguration.Monitors.TryGetValue(monitorName, out var monitor))
            {
                return DeucalionResults.MonitorNotFound(monitorName);
            }

            return Results.Ok(await BuildMonitorDtoAsync(storage, applicationMonitors, monitor, monitorName, cancellationToken));
        });

        app.MapPost("/api/monitors/{monitorName}/checkin", (string monitorName, HttpRequest request) =>
            {
                if (!applicationMonitors.Monitors.TryGetValue(monitorName, out var monitor))
                {
                    return DeucalionResults.MonitorNotFound(monitorName);
                }

                if (monitor is not CheckInMonitor cim)
                {
                    return DeucalionResults.NotCheckInMonitor(monitorName, $"/api/monitors/{monitorName}");
                }

                // No configured secret means no authentication. The header is read through
                // StringValues.ToString(), so an absent header is an empty string and never
                // matches a non-empty secret.
                if (!string.IsNullOrEmpty(cim.Secret) &&
                    !SecretMatches(cim.Secret, request.Headers["deucalion-checkin-secret"].ToString()))
                {
                    return DeucalionResults.InvalidCheckInSecret(monitorName, $"/api/monitors/{monitorName}");
                }

                cim.CheckIn();

                return Results.Ok();
            })
            .RequireRateLimiting(CheckInRateLimitPolicy);

        // SSE event stream
        app.MapGet("/api/monitors/events", async (MonitorEventBroadcaster broadcaster, HttpContext httpContext) =>
        {
            // Reconnect delay hinted to EventSource. Browsers default to a few seconds
            // but the value is implementation-defined; pin it.
            const int SseRetryMilliseconds = 3000;

            var response = httpContext.Response;
            var ct = httpContext.RequestAborted;

            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            // nginx buffers proxied responses by default, which turns a live stream into
            // a stall until the buffer fills. This header opts the response out.
            response.Headers["X-Accel-Buffering"] = "no";

            var (reader, writer) = broadcaster.Subscribe();
            try
            {
                // Write an initial SSE comment to flush the response headers immediately.
                // TypedResults.ServerSentEvents only flushes on first data event; with a
                // long check interval this causes EventSource to stay in CONNECTING
                // state until the first monitor event arrives. The `retry:` field pins
                // the browser's reconnect delay instead of leaving it to its default.
                await response.WriteAsync($": connected\nretry: {SseRetryMilliseconds}\n\n", ct);
                await response.Body.FlushAsync(ct);

                // Frames arrive pre-rendered; keep-alive comments come through the same
                // channel so an idle stream still carries traffic.
                await foreach (var frame in reader.ReadAllAsync(ct))
                {
                    await response.WriteAsync(frame, ct);
                    await response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Client went away. Not an error, and the response has already started,
                // so there is nothing for the exception handler to do with it anyway.
            }
            finally
            {
                // The only unsubscribe path. A Register(RequestAborted) callback alone would
                // leak the writer whenever the loop exits any other way (IOException on a
                // half-open socket, channel completion at shutdown).
                broadcaster.Unsubscribe(writer);
            }
        });

        // Log application version and command-line arguments.
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        // Get version info from assembly -- https://stackoverflow.com/a/64793765/33244
        //   SourceRevisionId included since .NET 8 SDK -- https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/source-link
        var appVersion = DiscoveryEndpoints.InformationalVersion;

        var cmdLineArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
        logger.LogInformation("Application Version = {version}.", appVersion);
        if (cmdLineArgs.Length > 0)
        {
            logger.LogInformation("Command-line = {args}", cmdLineArgs);
        }

        return app;
    }

    /// <summary>
    /// Constant-time comparison: string.Equals short-circuits on the first mismatching
    /// character, which leaks how much of the secret an attacker has right (#23). The length
    /// check leaks only the length, which FixedTimeEquals requires anyway.
    /// </summary>
    private static bool SecretMatches(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    // The frontend's heartbeat strip scales from 60 (mobile) up to 120 ticks
    // (wide desktop) — request the upper bound so the wide-desktop layout
    // gets full history and narrower viewports clip from the left.
    private const int EventHistoryCount = 120;

    private static async Task<MonitorDto> BuildMonitorDtoAsync(IStorage storage, ApplicationMonitors applicationMonitors, PullMonitorConfiguration m, string mn, CancellationToken cancellationToken)
    {
        // Stats use the rolling stats window; the event list uses the longer strip history.
        // These are deliberately different numbers -- see EventHistoryCount above.
        var stats = await storage.GetStatsAsync(mn, historyCount: PullMonitor.StatsWindow, cancellationToken: cancellationToken);

        // Display only: request threads must not write the live monitor's auto-WARN baseline
        // (issue #15). EngineBackgroundService is the sole writer, via WarnThresholdPolicy.Refresh.
        applicationMonitors.Monitors.TryGetValue(mn, out var monitor);
        var (effectiveWarn, timeout) = WarnThresholdPolicy.Compute(monitor, stats?.Latency95, stats?.SampleCount ?? 0);

        return new(
            Name: mn,
            Config: MonitorConfigurationDto.From(m),
            Stats: MonitorStatsDto.From(stats, effectiveWarn, timeout),
            Events: from e in await storage.GetLastEventsAsync(mn, count: EventHistoryCount, cancellationToken: cancellationToken)
                    select MonitorEventDto.From(e)
        );
    }
}
