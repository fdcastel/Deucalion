using Deucalion.Api;
using Deucalion.Service;

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
    .UseDeucalionSpa()
    .Run();
