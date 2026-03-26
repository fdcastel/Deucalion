using Deucalion.Api;
using Deucalion.Application.Configuration;
using Deucalion.Service;

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
