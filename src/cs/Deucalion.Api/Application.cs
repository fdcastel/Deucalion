using System.Text.Json.Serialization;
using Deucalion.Api.Http;
using Deucalion.Api.Models;
using Deucalion.Api.Options;
using Deucalion.Api.Services;
using Deucalion.Application.Configuration;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Deucalion.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;

namespace Deucalion.Api;

public static class Application
{
    public static WebApplicationBuilder ConfigureApplicationBuilder(this WebApplicationBuilder builder)
    {
        // Json
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            // Ignore nulls -- https://stackoverflow.com/a/60005662/33244
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull;
        });

        // SignalR
        builder.Services.AddSignalR();

        // Response compression
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "image/svg+xml" });
        });

        // Application configuration
        var deucalionOptions = new DeucalionOptions();
        builder.Configuration.GetSection("Deucalion").Bind(deucalionOptions);
        deucalionOptions.PageTitle ??= "Deucalion status";
        deucalionOptions.PageDescription ??= "Deucalion. A high performance monitoring tool.";
        builder.Services.AddSingleton(_ => deucalionOptions);

        var monitorConfiguration = MonitorConfiguration.ReadFromFile(deucalionOptions.ConfigurationFile ?? "deucalion.yaml");
        builder.Services.AddSingleton(_ => monitorConfiguration);

        // Application services
        var storage = new FasterStorage(deucalionOptions.StoragePath, deucalionOptions.CommitInterval);
        builder.Services.AddSingleton(_ => storage);
        builder.Services.AddHostedService<EngineBackgroundService>();

        return builder;
    }

    public static WebApplication ConfigureApplication(this WebApplication app)
    {
        app.UseExceptionHandler(exceptionHandlerApp =>
            exceptionHandlerApp.Run(async context => await Results.Problem().ExecuteAsync(context))
        );

        app.UseResponseCompression();

        // Setup Api endpoints
        var monitorConfiguration = app.Services.GetRequiredService<MonitorConfiguration>();
        app.MapGet("/api/configuration", (DeucalionOptions options) =>
            new { options.PageTitle, options.PageDescription });

        app.MapGet("/api/monitors", (FasterStorage storage) =>
            from m in monitorConfiguration.Monitors.Keys
            let c = monitorConfiguration.Monitors[m]
            let s = storage.GetStats(m)
            select new
            {
                Name = m,
                Config = new MonitorConfigurationDto(
                    Group: c.Group,
                    Href: ExtractHref(c),
                    Image: c.Image
                ),
                Stats = s is null ? null : new MonitorStatsDto(
                    s.LastState,
                    s.LastUpdate.ToUnixTimeSeconds(),
                    s.Availability,
                    (int)s.AverageResponseTime.TotalMilliseconds,
                    s.LastSeenUp?.ToUnixTimeSeconds(),
                    s.LastSeenDown?.ToUnixTimeSeconds()
                ),
                Events = from e in storage.GetLastEvents(m)
                         select new MonitorEventDto(
                             At: e.At.ToUnixTimeSeconds(),
                             St: e.State,
                             Ms: (int?)e.ResponseTime?.TotalMilliseconds,
                             Te: e.ResponseText
                         )
            });

        app.MapPost("/api/monitors/{monitorName}/checkin", (string monitorName, [FromBody] MonitorCheckInDto arguments) =>
        {
            if (monitorConfiguration.Monitors.TryGetValue(monitorName, out var monitor))
            {
                var instanceUri = $"/api/monitors/{monitorName}";

                if (monitor is CheckInMonitor cim)
                {
                    if (cim.Secret is not null && cim.Secret != arguments.Secret)
                    {
                        return DeucalionResults.InvalidCheckInSecret(monitorName, instanceUri);
                    }

                    cim.CheckIn(arguments.ToMonitorResponse());

                    return Results.Ok();
                }

                return DeucalionResults.NotCheckInMonitor(monitorName, instanceUri);
            }

            return DeucalionResults.MonitorNotFound(monitorName);
        });

        // Setup SignalR hubs
        app.MapHub<MonitorHub>("/api/monitors/hub");
        return app;
    }

    private static string? ExtractHref(MonitorBase monitor)
    {
        if (monitor.Href is null && monitor is HttpMonitor hm)
        {
            return hm.Url.ToString();
        }

        if (string.IsNullOrWhiteSpace(monitor.Href))
        {
            return string.Empty;
        }

        return monitor.Href;
    }
}
