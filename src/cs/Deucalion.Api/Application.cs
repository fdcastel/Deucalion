using System.Text.Json.Serialization;
using Deucalion.Api.Http;
using Deucalion.Api.Models;
using Deucalion.Api.Options;
using Deucalion.Api.Services;
using Deucalion.Application.Configuration;
using Deucalion.Configuration;
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

        var applicationConfiguration = app.Services.GetRequiredService<ApplicationConfiguration>();
        var applicationMonitors = app.Services.GetRequiredService<ApplicationMonitors>();

        // Setup Api endpoints
        app.MapGet("/api/configuration", (DeucalionOptions options) =>
            Results.Ok(new
            {
                options.PageTitle,
                options.PageDescription
            }));

        app.MapGet("/api/monitors/{monitorName?}", (FasterStorage storage, string? monitorName) =>
        {
            if (monitorName is null)
            {
                return Results.Ok(from kvp in applicationConfiguration.Monitors
                                  select BuildMonitorDto(storage, kvp.Value, kvp.Key));
            }

            if (!applicationConfiguration.Monitors.TryGetValue(monitorName, out var monitor))
            {
                return DeucalionResults.MonitorNotFound(monitorName);
            }

            return Results.Ok(BuildMonitorDto(storage, monitor, monitorName));
        });

        app.MapPost("/api/monitors/{monitorName}/checkin", (string monitorName, [FromBody] MonitorCheckInDto arguments) =>
            {
                if (!applicationMonitors.Monitors.TryGetValue(monitorName, out var monitor))
                {
                    return DeucalionResults.MonitorNotFound(monitorName);
                }

                if (monitor is not CheckInMonitor cim)
                {
                    return DeucalionResults.NotCheckInMonitor(monitorName, $"/api/monitors/{monitorName}");
                }

                if (cim.Secret is not null && cim.Secret != arguments.Secret)
                {
                    return DeucalionResults.InvalidCheckInSecret(monitorName, $"/api/monitors/{monitorName}");
                }

                cim.CheckIn(arguments.ToMonitorResponse());

                return Results.Ok();
            });

        // Setup SignalR hubs
        app.MapHub<MonitorHub>("/api/monitors/hub");
        return app;
    }

    private static MonitorDto BuildMonitorDto(FasterStorage storage, MonitorConfiguration m, string mn) =>
        new(
            Name: mn,
            Config: MonitorConfigurationDto.From(m),
            Stats: MonitorStatsDto.From(storage.GetStats(mn)),
            Events: from e in storage.GetLastEvents(mn)
                    select MonitorEventDto.From(e)
        );
}
