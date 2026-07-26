using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Deucalion.Tests.Network;

/// <summary>
/// A throwaway HTTP server on an ephemeral port, so HttpMonitor's contract can be tested
/// without depending on the public internet being reachable (or on someone else's rate limit).
/// </summary>
internal sealed class TestHttpServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    public Uri Url { get; }

    private TestHttpServer(WebApplication app, Uri url)
    {
        _app = app;
        Url = url;
    }

    /// <summary>
    /// Starts a server whose single endpoint is <paramref name="handler"/>.
    /// </summary>
    public static async Task<TestHttpServer> StartAsync(RequestDelegate handler)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.Run(handler);

        await app.StartAsync();

        // Port 0 means "pick a free one" -- read back what Kestrel actually bound.
        var address = app.Urls.First();

        return new TestHttpServer(app, new Uri(address));
    }

    /// <summary>Serves a fixed status code and body.</summary>
    public static Task<TestHttpServer> StartAsync(HttpStatusCode statusCode, string body = "") =>
        StartAsync(async context =>
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(body);
        });

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
