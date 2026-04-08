using Deucalion.Api;
using Deucalion.Application.Configuration;
using Deucalion.Service;
using Microsoft.AspNetCore.Server.Kestrel.Core;

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
