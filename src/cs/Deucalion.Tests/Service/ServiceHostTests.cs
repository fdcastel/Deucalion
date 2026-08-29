extern alias Service;

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Deucalion.Tests.Service;

/// <summary>
/// Boots <c>Deucalion.Service</c>'s <c>Program</c> (the Windows-service host that layers the
/// index-page and pre-compressed static-file middleware on top of <c>Deucalion.Api</c>) against
/// a throw-away <c>wwwroot</c>, and exercises that middleware over HTTP.
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class ServiceHostTests : IAsyncLifetime
{
    // See ApiIntegrationTests for why these are environment variables.
    private const string ConfigurationFileEnvVar = "Deucalion__ConfigurationFile";
    private const string StoragePathEnvVar = "Deucalion__StoragePath";
    private const string PageTitleEnvVar = "Deucalion__PageTitle";

    private const string PageTitle = "Tom & Jerry <\"Live\">";
    private const string ExpectedTitleElement = "<title>Tom &amp; Jerry &lt;&quot;Live&quot;&gt;</title>";

    private const string IndexTemplate =
        """
        <!doctype html>
        <html><head><meta charset="utf-8"><!-- $DEUCALION__PAGETITLE --></head>
        <body><div id="root"></div></body></html>
        """;

    // Long enough to be worth compressing and to make each sidecar a distinct size.
    private static readonly string AssetContent = string.Concat(Enumerable.Repeat("export const answer = 42;\n", 40));
    private static readonly byte[] AssetBytes = Encoding.UTF8.GetBytes(AssetContent);

    private readonly string _tempPath;
    private readonly string _webRootPath;
    private readonly ServiceFactory _factory;

    public ServiceHostTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.Service_{Guid.NewGuid()}");
        _webRootPath = Path.Combine(_tempPath, "wwwroot");
        var assetsPath = Path.Combine(_webRootPath, "assets");
        Directory.CreateDirectory(assetsPath);

        File.WriteAllText(Path.Combine(_webRootPath, "index.html"), IndexTemplate);

        // x.js has both sidecars; y.js has none.
        File.WriteAllBytes(Path.Combine(assetsPath, "x.js"), AssetBytes);
        File.WriteAllBytes(Path.Combine(assetsPath, "x.js.br"), Compress(AssetBytes, s => new BrotliStream(s, CompressionLevel.Optimal)));
        File.WriteAllBytes(Path.Combine(assetsPath, "x.js.gz"), Compress(AssetBytes, s => new GZipStream(s, CompressionLevel.Optimal)));
        File.WriteAllBytes(Path.Combine(assetsPath, "y.js"), AssetBytes);

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

    // Cleanup lives here, not in IDisposable.Dispose(): xunit.v3 calls only DisposeAsync() on a
    // class that implements IAsyncLifetime, so a Dispose() would silently never run.
    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();

        // ApiIntegrationTests asserts the title from appsettings.Development.json; an env var
        // would override it, so it must not outlive this class.
        Environment.SetEnvironmentVariable(PageTitleEnvVar, null);
        Environment.SetEnvironmentVariable(ConfigurationFileEnvVar, null);
        Environment.SetEnvironmentVariable(StoragePathEnvVar, null);

        TestPaths.DeleteWithRetry(_tempPath);
    }

    // --- Index page ---------------------------------------------------------------------------

    [Fact]
    public async Task GetIndex_ReturnsProcessedHtml_WithNoCacheAndETag()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache, "Cache-Control must be no-cache so the browser revalidates");
        Assert.NotNull(response.Headers.ETag);
        Assert.False(response.Headers.ETag.IsWeak);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(ExpectedTitleElement, html, StringComparison.Ordinal);
        Assert.DoesNotContain("$DEUCALION__PAGETITLE", html, StringComparison.Ordinal);
        Assert.DoesNotContain(PageTitle, html, StringComparison.Ordinal); // never raw, always encoded
    }

    [Fact]
    public async Task GetIndex_ETagIsStableAcrossRequests()
    {
        using var client = _factory.CreateClient();

        using var first = await client.GetAsync("/", TestContext.Current.CancellationToken);
        using var second = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
    }

    [Fact]
    public async Task GetIndex_IfNoneMatchCurrentETag_Returns304WithoutBody()
    {
        using var client = _factory.CreateClient();

        using var initial = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var etag = initial.Headers.ETag!;

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.IfNoneMatch.Add(etag);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Equal(etag, response.Headers.ETag);
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetIndex_IfNoneMatchWildcard_Returns304()
    {
        using var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetIndex_IfNoneMatchStaleETag_Returns200WithBody()
    {
        using var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"0000000000000000\""));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(ExpectedTitleElement, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeadIndex_ReturnsSameHeadersAsGet_WithoutBody()
    {
        using var client = _factory.CreateClient();

        using var get = await client.GetAsync("/", TestContext.Current.CancellationToken);
        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal("text/html", head.Content.Headers.ContentType?.MediaType);
        Assert.Equal(get.Headers.ETag, head.Headers.ETag);
        Assert.True(head.Headers.CacheControl?.NoCache);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task NonReadMethodOnIndex_Returns405WithAllow(string method)
    {
        using var client = _factory.CreateClient();

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(["GET", "HEAD"], response.Content.Headers.Allow.Order().ToArray());
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    // --- /assets: pre-compressed sidecars -----------------------------------------------------

    [Fact]
    public async Task Asset_AcceptBrotli_ServesBrotliSidecar()
    {
        using var client = _factory.CreateClient();

        using var response = await GetAssetAsync(client, "/assets/x.js", "br");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["br"], response.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(SidecarLength("x.js.br"), response.Content.Headers.ContentLength);
        AssertImmutable(response);

        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SidecarLength("x.js.br"), body.Length);
        Assert.Equal(AssetContent, Decompress(body, s => new BrotliStream(s, CompressionMode.Decompress)));
    }

    [Fact]
    public async Task Asset_AcceptGzip_ServesGzipSidecar()
    {
        using var client = _factory.CreateClient();

        using var response = await GetAssetAsync(client, "/assets/x.js", "gzip");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["gzip"], response.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        Assert.Equal(SidecarLength("x.js.gz"), response.Content.Headers.ContentLength);
        AssertImmutable(response);

        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AssetContent, Decompress(body, s => new GZipStream(s, CompressionMode.Decompress)));
    }

    [Fact]
    public async Task Asset_AcceptBoth_PrefersBrotli()
    {
        using var client = _factory.CreateClient();

        using var response = await GetAssetAsync(client, "/assets/x.js", "gzip, deflate, br");

        Assert.Equal(["br"], response.Content.Headers.ContentEncoding);
        Assert.Equal(SidecarLength("x.js.br"), response.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task Asset_BrotliRefusedByQValue_FallsBackToGzip()
    {
        using var client = _factory.CreateClient();

        using var response = await GetAssetAsync(client, "/assets/x.js", "br;q=0, gzip");

        Assert.Equal(["gzip"], response.Content.Headers.ContentEncoding);
        Assert.Equal(SidecarLength("x.js.gz"), response.Content.Headers.ContentLength);
    }

    [Theory]
    [InlineData("br;q=0")]
    [InlineData("br;q=0, gzip;q=0")]
    [InlineData("brotli-unsupported")]
    [InlineData("xbr")]
    [InlineData("identity")]
    public async Task Asset_EncodingNotAcceptable_ServesIdentity(string acceptEncoding)
    {
        using var client = _factory.CreateClient();

        using var response = await GetAssetAsync(client, "/assets/x.js", acceptEncoding);

        await AssertIdentityAsync(response);
    }

    [Fact]
    public async Task Asset_NoAcceptEncoding_ServesIdentity()
    {
        using var client = _factory.CreateClient();

        using var response = await GetAssetAsync(client, "/assets/x.js", acceptEncoding: null);

        await AssertIdentityAsync(response);
    }

    [Fact]
    public async Task Asset_CaseInsensitiveEncodingToken_ServesSidecar()
    {
        using var client = _factory.CreateClient();

        using var response = await GetAssetAsync(client, "/assets/x.js", "BR");

        Assert.Equal(["br"], response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Asset_SidecarMissing_FallsBackToOriginalFile()
    {
        using var client = _factory.CreateClient();

        using var response = await GetAssetAsync(client, "/assets/y.js", "br, gzip");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        AssertImmutable(response);

        // No sidecar means the static-file middleware serves y.js itself. The response
        // compression middleware may then compress it on the fly; either way the client
        // must end up with the original content.
        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var content = response.Content.Headers.ContentEncoding.SingleOrDefault() switch
        {
            null => Encoding.UTF8.GetString(body),
            "br" => Decompress(body, s => new BrotliStream(s, CompressionMode.Decompress)),
            "gzip" => Decompress(body, s => new GZipStream(s, CompressionMode.Decompress)),
            var other => throw new Xunit.Sdk.XunitException($"Unexpected Content-Encoding '{other}'"),
        };
        Assert.Equal(AssetContent, content);
    }

    [Fact]
    public async Task Asset_Unknown_Returns404()
    {
        using var client = _factory.CreateClient();

        using var response = await GetAssetAsync(client, "/assets/missing.js", "br, gzip");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Helpers ------------------------------------------------------------------------------

    private static Task<HttpResponseMessage> GetAssetAsync(HttpClient client, string path, string? acceptEncoding)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (acceptEncoding is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task AssertIdentityAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal(AssetBytes.Length, response.Content.Headers.ContentLength);
        AssertImmutable(response);

        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AssetBytes, body);
    }

    private static void AssertImmutable(HttpResponseMessage response)
    {
        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Public, "Cache-Control must be public");
        Assert.Equal(TimeSpan.FromDays(365), cacheControl.MaxAge);
        Assert.Contains(cacheControl.Extensions, x => x.Name == "immutable");
    }

    private long SidecarLength(string fileName) =>
        new FileInfo(Path.Combine(_webRootPath, "assets", fileName)).Length;

    private static byte[] Compress(byte[] input, Func<Stream, Stream> createCompressor)
    {
        using var output = new MemoryStream();
        using (var compressor = createCompressor(output))
        {
            compressor.Write(input);
        }

        return output.ToArray();
    }

    private static string Decompress(byte[] input, Func<Stream, Stream> createDecompressor)
    {
        using var decompressor = createDecompressor(new MemoryStream(input));
        using var reader = new StreamReader(decompressor, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed class ServiceFactory(string contentRoot) : WebApplicationFactory<Service::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Production: the Service's Program points the web root at ../../../publish/wwwroot
            // in Development. Outside Development it is ./wwwroot under the content root.
            builder.UseEnvironment("Production");
            builder.UseContentRoot(contentRoot);
        }
    }
}
