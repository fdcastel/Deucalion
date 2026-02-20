using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Deucalion.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Deucalion.Tests.Api;

public sealed class ApiIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _tempPath;
    private readonly string _configurationPath;
    private readonly TestApiFactory _factory;

    public ApiIntegrationTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.Api_{Guid.NewGuid()}");
        _configurationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "deucalion-sample.yaml"));

        Directory.CreateDirectory(_tempPath);

        File.WriteAllText(_configurationPath,
            """
            defaults:
              intervalWhenUp: 00:00:00.250
              intervalWhenDown: 00:00:00.250
              intervalToDown: 00:00:02

            monitors:
              web-main: !http
                url: https://example.com
                group: Main

              checkin-main: !checkin
                secret: test-secret
                group: Main
            """);

        _factory = new TestApiFactory();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GetConfiguration_ReturnsConfiguredMetadata()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/configuration");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Deucalion (dev) 📡", payload.GetProperty("pageTitle").GetString());
        Assert.Equal("Deucalion. A high performance monitoring tool.", payload.GetProperty("pageDescription").GetString());
    }

    [Fact]
    public async Task GetMonitors_ReturnsConfiguredMonitors_WithStats()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var now = DateTimeOffset.UtcNow;

        await storage.SaveEventAsync("checkin-main", new StoredEvent(now, MonitorState.Up, TimeSpan.FromMilliseconds(123), null));

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/monitors");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, payload.ValueKind);

        var monitorNames = payload.EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("web-main", monitorNames);
        Assert.Contains("checkin-main", monitorNames);

        var checkInMonitor = payload.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "checkin-main");
        Assert.True(checkInMonitor.TryGetProperty("stats", out var stats));
        Assert.Equal((int)MonitorState.Up, stats.GetProperty("lastState").GetInt32());
    }

    [Fact]
    public async Task CheckInEndpoint_ValidatesSecretAndReturnsExpectedStatusCodes()
    {
        using var client = _factory.CreateClient();

        using (var unknownResponse = await client.PostAsync("/api/monitors/unknown/checkin", content: null))
        {
            Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        }

        using (var nonCheckInResponse = await client.PostAsync("/api/monitors/web-main/checkin", content: null))
        {
            Assert.Equal(HttpStatusCode.BadRequest, nonCheckInResponse.StatusCode);
        }

        using (var unauthorized = await client.PostAsync("/api/monitors/checkin-main/checkin", content: null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitors/checkin-main/checkin");
        request.Headers.Add("deucalion-checkin-secret", "test-secret");

        using var okResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
    }

    [Fact]
    public async Task SignalRHub_BroadcastsMonitorCheckedEvent_ToConnectedClients()
    {
        using var client = _factory.CreateClient();

        var checkedEventReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/api/monitors/hub", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        connection.On<JsonElement>("MonitorChecked", payload =>
        {
            var monitorName = payload.GetProperty("n").GetString();
            if (monitorName == "checkin-main")
            {
                checkedEventReceived.TrySetResult(monitorName!);
            }
        });

        await connection.StartAsync();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitors/checkin-main/checkin");
            request.Headers.Add("deucalion-checkin-secret", "test-secret");

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var monitorName = await checkedEventReceived.Task.WaitAsync(timeout.Token);
            Assert.Equal("checkin-main", monitorName);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    public void Dispose()
    {
        const int maxAttempts = 5;
        TryDeleteFile(_configurationPath);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(_tempPath))
                {
                    Directory.Delete(_tempPath, true);
                }

                break;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Ignore file deletion race during test cleanup.
        }
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
