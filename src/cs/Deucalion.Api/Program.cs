using System.Text.Json.Serialization;
using Deucalion.Api;
using Deucalion.Api.Models;
using Deucalion.Api.Options;
using Deucalion.Api.Services;
using Deucalion.Application.Configuration;
using Deucalion.Monitors;
using Deucalion.Storage;
using Microsoft.AspNetCore.Http.Json;

// Web application
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddCors();
builder.Services.AddSignalR();

// Configuration
var deucalionOptions = new DeucalionOptions();
builder.Configuration.GetSection("Deucalion").Bind(deucalionOptions);

var monitorConfiguration = MonitorConfiguration.ReadFromFile(deucalionOptions.ConfigurationFile ?? "deucalion-sample.yaml");
builder.Services.AddSingleton(_ => monitorConfiguration);

// Application services
var storage = new FasterStorage(deucalionOptions.StoragePath, deucalionOptions.CommitInterval);
builder.Services.AddSingleton(_ => storage);
builder.Services.AddHostedService<EngineBackgroundService>();

var app = builder.Build();

// Setup CORS
app.UseCors(builder =>
{
    builder
        .SetIsOriginAllowed(origin => true) // Any origin
        .AllowCredentials()
        .AllowAnyHeader();
});

// Setup Api endpoints
app.MapGet("/api/monitors", (FasterStorage storage) =>
    monitorConfiguration.Monitors.Keys);

app.MapGet("/api/monitors/*", (FasterStorage storage) =>
    from m in monitorConfiguration.Monitors.Keys
    select new
    {
        Name = m,
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
app.MapHub<MonitorHub>("/hub/monitors");

app.Run();
