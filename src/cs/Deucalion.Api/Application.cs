using System.Text.Json.Serialization;
using Deucalion.Api.Models;
using Deucalion.Api.Options;
using Deucalion.Api.Services;
using Deucalion.Application.Configuration;
using Deucalion.Monitors;
using Deucalion.Storage;
using Microsoft.AspNetCore.Http.Json;

namespace Deucalion.Api;

public static class Application
{
    public static WebApplicationBuilder ConfigureApplicationBuilder(this WebApplicationBuilder builder)
    {
        // Web application
        builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull);
        builder.Services.AddCors();
        builder.Services.AddSignalR();

        // Configuration
        var deucalionOptions = new DeucalionOptions();
        builder.Configuration.GetSection("Deucalion").Bind(deucalionOptions);
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
        // Setup CORS
        app.UseCors(builder =>
        {
            builder
                .SetIsOriginAllowed(origin => true) // Any origin
                .AllowCredentials()
                .AllowAnyHeader();
        });

        var monitorConfiguration = app.Services.GetRequiredService<MonitorConfiguration>();

        // Setup Api endpoints
        app.MapGet("/api/configuration", (DeucalionOptions options) =>
            new { options.PageTitle });

        app.MapGet("/api/monitors", (FasterStorage storage) =>
            monitorConfiguration.Monitors);

        app.MapGet("/api/monitors/*", (FasterStorage storage) =>
            from m in monitorConfiguration.Monitors.Keys
            select new
            {
                Name = m,
                Info = new MonitorInfoDto(
                    Group: monitorConfiguration.Monitors[m].Group,
                    Href: monitorConfiguration.Monitors[m].Href,
                    Image: monitorConfiguration.Monitors[m].Image
                ),
                Events = from e in storage.GetLastEvents(m)
                         select new MonitorEventDto(
                             N: null,
                             At: e.At.ToUnixTimeSeconds(),
                             St: e.Response?.State ?? MonitorState.Unknown,
                             Ms: e.Response?.ResponseTime?.Milliseconds,
                             Te: e.Response?.ResponseText
                         )
            });

        // Setup SignalR hubs
        app.MapHub<MonitorHub>("/api/monitors/hub");
        return app;
    }
}
