using Deucalion.Api;
using Deucalion.Application.Configuration;

try
{
    WebApplication.CreateBuilder(args)
        .ConfigureApplicationBuilder()
        .Build()
        .ConfigureApplication()
        .Run();
}
catch (ConfigurationErrorException ex)
{
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    return 1;
}

return 0;

public partial class Program;
