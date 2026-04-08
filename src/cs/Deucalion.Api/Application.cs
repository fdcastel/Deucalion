using System.Reflection;
using System.Text.Json.Serialization;
using Deucalion.Api.Http;
using Deucalion.Api.Models;
using Deucalion.Api.Options;
using Deucalion.Api.Services;
using Deucalion.Application.Configuration;
using Deucalion.Configuration;
using Deucalion.Network.Monitors;
using Deucalion.Storage;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.ResponseCompression;

namespace Deucalion.Api;

public static class Application
{
    public static WebApplicationBuilder ConfigureApplicationBuilder(this WebApplicationBuilder builder)
    {
        // Json
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, DeucalionJsonContext.Default);
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull;
        });

        // CORS
        builder.Services.AddCors();

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
        builder.Configuration.GetSection("Deucalion").Bind(deucalionOptions);
        deucalionOptions.PageTitle ??= "Deucalion status";
        deucalionOptions.PageDescription ??= "Deucalion. A high performance monitoring tool.";
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
            return new SqliteStorage(options.StoragePath);
        });
        builder.Services.AddSingleton<IStorage>(sp => sp.GetRequiredService<SqliteStorage>());
        builder.Services.AddHostedService<EngineBackgroundService>();
        builder.Services.AddHostedService<PurgeBackgroundService>();
        builder.Services.AddSingleton<MonitorEventBroadcaster>();

        return builder;
    }

    public static WebApplication ConfigureApplication(this WebApplication app)
    {
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

        app.UseCors(x => x.AllowAnyOrigin());

        app.UseResponseCompression();

        app.Services.GetRequiredService<IStorage>().InitializeAsync().GetAwaiter().GetResult();

        var applicationConfiguration = app.Services.GetRequiredService<ApplicationConfiguration>();
        var applicationMonitors = app.Services.GetRequiredService<ApplicationMonitors>();

        // Setup Api endpoints
        app.MapGet("/api/configuration", (DeucalionOptions options) =>
            Results.Ok(new PageConfigurationDto(options.PageTitle, options.PageDescription)));

        app.MapGet("/api/monitors/{monitorName?}", async (IStorage storage, string? monitorName, CancellationToken cancellationToken) =>
        {
            if (monitorName is null)
            {
                var tasks = applicationConfiguration.Monitors
                                  .Select(kvp => BuildMonitorDtoAsync(storage, kvp.Value, kvp.Key, cancellationToken));
                var results = await Task.WhenAll(tasks);
                return Results.Ok(results);
            }

            if (!applicationConfiguration.Monitors.TryGetValue(monitorName, out var monitor))
            {
                return DeucalionResults.MonitorNotFound(monitorName);
            }

            return Results.Ok(await BuildMonitorDtoAsync(storage, monitor, monitorName, cancellationToken));
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

                if (cim.Secret is not null && cim.Secret != request.Headers["deucalion-checkin-secret"])
                {
                    return DeucalionResults.InvalidCheckInSecret(monitorName, $"/api/monitors/{monitorName}");
                }

                cim.CheckIn();

                return Results.Ok();
            });

        // SSE event stream
        app.MapGet("/api/monitors/events", async (MonitorEventBroadcaster broadcaster, HttpContext httpContext) =>
        {
            var response = httpContext.Response;
            var ct = httpContext.RequestAborted;

            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";

            // Write an initial SSE comment to flush the response headers immediately.
            // TypedResults.ServerSentEvents only flushes on first data event; with a
            // long check interval this causes EventSource to stay in CONNECTING
            // state until the first monitor event arrives.
            await response.WriteAsync(": connected\n\n", ct);
            await response.Body.FlushAsync(ct);

            var (reader, writer) = broadcaster.Subscribe();
            ct.Register(() => broadcaster.Unsubscribe(writer));

            await foreach (var item in reader.ReadAllAsync(ct))
            {
                var payload = item.EventType is not null
                    ? $"event: {item.EventType}\ndata: {item.Data}\n\n"
                    : $"data: {item.Data}\n\n";
                await response.WriteAsync(payload, ct);
                await response.Body.FlushAsync(ct);
            }
        });

        // Log application version and command-line arguments.
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        // Get version info from assembly -- https://stackoverflow.com/a/64793765/33244
        //   SourceRevisionId included since .NET 8 SDK -- https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/source-link
        var appVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var cmdLineArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
        logger.LogInformation("Application Version = {version}.", appVersion);
        if (cmdLineArgs.Length > 0)
        {
            logger.LogInformation("Command-line = {args}", cmdLineArgs);
        }

        return app;
    }

    private static async Task<MonitorDto> BuildMonitorDtoAsync(IStorage storage, PullMonitorConfiguration m, string mn, CancellationToken cancellationToken) =>
        new(
            Name: mn,
            Config: MonitorConfigurationDto.From(m),
            Stats: MonitorStatsDto.From(await storage.GetStatsAsync(mn, cancellationToken: cancellationToken)),
            Events: from e in await storage.GetLastEventsAsync(mn, cancellationToken: cancellationToken)
                    select MonitorEventDto.From(e)
        );
}
