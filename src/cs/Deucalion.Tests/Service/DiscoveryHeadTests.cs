extern alias Service;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Deucalion.Tests.Service;

/// <summary>
/// The agent-discoverability surface of the service host (#35): the API advertised from the
/// served <c>index.html</c>, content negotiation on <c>/</c>, <c>/llms.txt</c>, and the
/// <c>--healthcheck</c> probe. Same fixture shape as <see cref="ServiceHostTests"/>.
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class DiscoveryHeadTests : IAsyncLifetime
{
    private const string ConfigurationFileEnvVar = "Deucalion__ConfigurationFile";
    private const string StoragePathEnvVar = "Deucalion__StoragePath";
    private const string PageTitleEnvVar = "Deucalion__PageTitle";

    private const string PageTitle = "Acme <Status>";

    // The real index.html carries both placeholders; the template mirrors their positions.
    private const string IndexTemplate =
        """
        <!doctype html>
        <html><head><meta charset="utf-8"><!-- $DEUCALION__PAGETITLE --></head>
        <body><div id="root"></div><!-- $DEUCALION__NOSCRIPT --></body></html>
        """;

    private const string BrowserAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";

    private readonly string _tempPath;
    private readonly ServiceFactory _factory;

    public DiscoveryHeadTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.Discovery_{Guid.NewGuid()}");
        var webRootPath = Path.Combine(_tempPath, "wwwroot");
        Directory.CreateDirectory(webRootPath);

        File.WriteAllText(Path.Combine(webRootPath, "index.html"), IndexTemplate);

        // Serve the real llms.txt from the repository, not a stand-in: the test must fail if
        // the shipped file stops naming an endpoint.
        File.Copy(FindRepoFile(Path.Combine("src", "ts", "deucalion-ui", "public", "llms.txt")), Path.Combine(webRootPath, "llms.txt"));

        var configurationPath = Path.Combine(_tempPath, "deucalion.yaml");
        File.WriteAllText(configurationPath,
            """
            monitors:
              checkin-only: !checkin
                group: Main
            """);

        Environment.SetEnvironmentVariable(ConfigurationFileEnvVar, configurationPath);
        Environment.SetEnvironmentVariable(StoragePathEnvVar, Path.Combine(_tempPath, "storage"));
        Environment.SetEnvironmentVariable(PageTitleEnvVar, PageTitle);

        _factory = new ServiceFactory(_tempPath);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();

        Environment.SetEnvironmentVariable(PageTitleEnvVar, null);
        Environment.SetEnvironmentVariable(ConfigurationFileEnvVar, null);
        Environment.SetEnvironmentVariable(StoragePathEnvVar, null);

        TestPaths.DeleteWithRetry(_tempPath);
    }

    // --- Head injection -----------------------------------------------------------------------

    [Fact]
    public async Task GetIndex_AdvertisesApiInHeadAndNoscript()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        // Head: title, alternate link and description -- the parts an extraction pipeline keeps.
        var head = html[..html.IndexOf("</head>", StringComparison.Ordinal)];
        Assert.Contains("<title>Acme &lt;Status&gt;</title>", head, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"alternate\" type=\"application/json\" href=\"/api/status\" />", head, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\" content=\"Acme &lt;Status&gt; — live service status. Machine-readable status at /api/status; docs at /llms.txt\" />", head, StringComparison.Ordinal);

        // Body: the noscript pointer sits inside <body>, after #root.
        var body = html[html.IndexOf("<body>", StringComparison.Ordinal)..];
        Assert.Contains("<noscript>", body, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/api/status\">/api/status</a>", body, StringComparison.Ordinal);
        Assert.True(body.IndexOf("id=\"root\"", StringComparison.Ordinal) < body.IndexOf("<noscript>", StringComparison.Ordinal));

        Assert.DoesNotContain("$DEUCALION__", html, StringComparison.Ordinal);
        Assert.DoesNotContain(PageTitle, html, StringComparison.Ordinal); // never raw, always encoded
    }

    // --- Content negotiation on / -------------------------------------------------------------

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/json, text/html;q=0.9")]
    [InlineData("text/html;q=0.5, application/*")]
    [InlineData("application/json;q=0.9, */*;q=0.1")]
    public async Task GetIndex_AcceptPrefersJson_ReturnsStatusDocument(string accept)
    {
        using var client = _factory.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Get, accept);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Accept", response.Headers.Vary);
        Assert.Null(response.Headers.ETag); // the HTML's ETag must not leak onto the JSON variant

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("/api/status", payload.GetProperty("links").GetProperty("self").GetString());
        Assert.Equal("checkin-only", payload.GetProperty("monitors")[0].GetProperty("name").GetString());

        // Same document as the endpoint itself. Compare the stable parts only: the engine's
        // start-up probe of checkin-only may land between the two requests and flip its state.
        var direct = await client.GetFromJsonAsync<JsonElement>("/api/status", TestContext.Current.CancellationToken);
        Assert.Equal(direct.GetProperty("links").GetRawText(), payload.GetProperty("links").GetRawText());
        Assert.Equal(
            direct.GetProperty("monitors").EnumerateArray().Select(m => m.GetProperty("name").GetString()),
            payload.GetProperty("monitors").EnumerateArray().Select(m => m.GetProperty("name").GetString()));
    }

    [Theory]
    [InlineData(BrowserAccept)]
    [InlineData("*/*")]
    [InlineData("text/html, application/json")] // a tie keeps HTML
    [InlineData("text/*")]
    [InlineData("application/json;q=0")]
    [InlineData("not a media type")]
    [InlineData(null)]
    public async Task GetIndex_AcceptDoesNotPreferJson_ReturnsHtml(string? accept)
    {
        using var client = _factory.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Get, accept);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Accept", response.Headers.Vary);
        Assert.NotNull(response.Headers.ETag);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIndex_AcceptJson_IgnoresHtmlETag()
    {
        // The conditional-GET shortcut belongs to the HTML variant only: a client holding the
        // HTML ETag but asking for JSON must get the JSON, not a 304.
        using var client = _factory.CreateClient();

        using var initial = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var etag = initial.Headers.ETag!;

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.IfNoneMatch.Add(etag);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HeadIndex_AcceptJson_ReturnsJsonHeadersWithoutBody()
    {
        using var client = _factory.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Head, "application/json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PostIndex_AcceptJson_StillReturns405()
    {
        using var client = _factory.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Post, "application/json");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // --- /llms.txt ----------------------------------------------------------------------------

    [Fact]
    public async Task GetLlmsTxt_ServesTheShippedFile_NamingEveryEndpoint()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/llms.txt", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        foreach (var path in new[] { "/api/status", "/api/monitors", "/api/monitors/{name}", "/api/monitors/events", "/api/version", "/api/monitors/{name}/checkin" })
        {
            Assert.Contains(path, text, StringComparison.Ordinal);
        }

        // The SSE short keys, as documented for agents.
        foreach (var key in new[] { "\"n\"", "\"at\"", "\"fr\"", "\"st\"", "\"ms\"", "\"ns\"" })
        {
            Assert.Contains(key, text, StringComparison.Ordinal);
        }

        Assert.Contains("deucalion-checkin-secret", text, StringComparison.Ordinal);
    }

    // --- --healthcheck ------------------------------------------------------------------------

    [Theory]
    [InlineData("8080")]
    [InlineData("8080;8081")]
    [InlineData(" 9000 ")]
    [InlineData(null)]
    public async Task HealthCheck_AgainstRunningHost_ReturnsZero(string? httpPorts)
    {
        // TestServer's handler answers any host:port in-process, so this exercises the real
        // probe -- URL building, the /api/version round trip, and the 2xx check.
        using var handler = _factory.Server.CreateHandler();

        var exitCode = await Service::Program.RunHealthCheckAsync(handler, httpPorts, TimeSpan.FromSeconds(3));

        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Found)]
    public async Task HealthCheck_NonSuccessStatus_ReturnsOne(HttpStatusCode statusCode)
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(statusCode));

        var exitCode = await Service::Program.RunHealthCheckAsync(handler, "8080", TimeSpan.FromSeconds(3));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task HealthCheck_ProbesVersionEndpointOnConfiguredPort()
    {
        Uri? requested = null;
        using var handler = new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Service::Program.RunHealthCheckAsync(handler, "9090;9091", TimeSpan.FromSeconds(3));

        Assert.Equal(new Uri("http://localhost:9090/api/version"), requested);
    }

    [Fact]
    public async Task HealthCheck_NoListener_ReturnsOne()
    {
        // A port nothing listens on: the connection is refused, which must read as unhealthy
        // rather than crash the probe.
        using var handler = new SocketsHttpHandler();

        var exitCode = await Service::Program.RunHealthCheckAsync(handler, FreePort().ToString(), TimeSpan.FromSeconds(3));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task HealthCheck_Timeout_ReturnsOne()
    {
        using var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var exitCode = await Service::Program.RunHealthCheckAsync(handler, "8080", TimeSpan.FromMilliseconds(200));

        Assert.Equal(1, exitCode);
    }

    // --- Helpers ------------------------------------------------------------------------------

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string? accept)
    {
        var request = new HttpRequestMessage(method, "/");
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string FindRepoFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"'{relativePath}' not found above '{AppContext.BaseDirectory}'.");
    }

    private static int FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : this((request, _) => Task.FromResult(respond(request)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }

    private sealed class ServiceFactory(string contentRoot) : WebApplicationFactory<Service::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseContentRoot(contentRoot);
        }
    }
}
