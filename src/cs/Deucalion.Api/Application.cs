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

        return builder;
    }

    public static WebApplication ConfigureApplication(this WebApplication app)
    {
        app.UseExceptionHandler(exceptionHandlerApp =>
            exceptionHandlerApp.Run(async context => await Results.Problem().ExecuteAsync(context))
        );

        app.UseCors(x => x.AllowAnyOrigin());

        app.UseResponseCompression();

        app.Services.GetRequiredService<IStorage>().InitializeAsync().GetAwaiter().GetResult();

        var applicationConfiguration = app.Services.GetRequiredService<ApplicationConfiguration>();
        var applicationMonitors = app.Services.GetRequiredService<ApplicationMonitors>();

        // Setup Api endpoints
        app.MapGet("/api/configuration", (DeucalionOptions options) =>
            Results.Ok(new
            {
                options.PageTitle,
                options.PageDescription
            }));

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

        // Setup SignalR hubs
        app.MapHub<MonitorHub>("/api/monitors/hub");

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
