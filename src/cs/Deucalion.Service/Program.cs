using Deucalion.Api;
using Deucalion.Api.Endpoints;
using Deucalion.Application.Configuration;
using Deucalion.Service;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// Docker HEALTHCHECK entry point: probe the running instance and exit, never build a host.
if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    using var handler = new SocketsHttpHandler();
    return await RunHealthCheckAsync(handler, Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS"), TimeSpan.FromSeconds(3));
}

try
{
    // During development use "wwwroot" from publish folder.
    var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development;
    var webRootPath = isDevelopment ? "../../../publish/wwwroot" : "./wwwroot";

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
    {
        Args = args,
        WebRootPath = webRootPath
    });

    builder.Services.AddWindowsService();

    // Enable HTTP/2 (multiplexed asset loading, HPACK header compression)
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ConfigureEndpointDefaults(listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        });
    });

    builder.ConfigureApplicationBuilder()
        .Build()
        .ConfigureApplication()
        .UseIndexPage()    // Must be called before .UseCachedFileServer()
        .UseCachedFileServer()
        .Run();
}
catch (ConfigurationErrorException ex)
{
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    return 1;
}

return 0;

public partial class Program
{
    /// <summary>
    /// <c>--healthcheck</c>: GET <c>/api/version</c> on the first port of
    /// <paramref name="httpPorts"/> (<c>ASPNETCORE_HTTP_PORTS</c>, default 8080) and report 0 on
    /// a 2xx, 1 on anything else -- a non-2xx, a timeout, or no listener at all. Plain
    /// <see cref="HttpClient"/> only: this runs inside the AOT-published binary.
    /// </summary>
    public static async Task<int> RunHealthCheckAsync(HttpMessageHandler handler, string? httpPorts, TimeSpan timeout)
    {
        var port = httpPorts?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "8080";

        using var client = new HttpClient(handler, disposeHandler: false) { Timeout = timeout };
        try
        {
            using var response = await client.GetAsync($"http://localhost:{port}{DiscoveryEndpoints.VersionPath}");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return 1;
        }
    }
}
