namespace Deucalion.Service;

public static class WebApplicationExtensions
{
    public static WebApplication UseCachedFileServer(this WebApplication app)
    {
        var fso = new FileServerOptions();
        fso.StaticFileOptions.OnPrepareResponse = (context) =>
        {
            // Use response cache (max-age: 7 days) for all static files in /assets
            if (context.Context.Request.Path.StartsWithSegments("/assets"))
            {
                var headers = context.Context.Response.GetTypedHeaders();
                headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromDays(7),
                    SharedMaxAge = TimeSpan.FromHours(12)
                };
            }
        };

        app.UseFileServer(fso);

        return app;
    }
}
