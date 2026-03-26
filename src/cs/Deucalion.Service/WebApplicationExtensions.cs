using System.Web;
using Deucalion.Api.Options;

namespace Deucalion.Service;

internal static class WebApplicationExtensions
{
    /// <summary>
    /// Use file server with response cache for all static files in '/assets'.
    /// </summary>
    internal static WebApplication UseCachedFileServer(this WebApplication app)
    {
        var fso = new FileServerOptions();
        fso.StaticFileOptions.OnPrepareResponse = (context) =>
        {
            // 
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

    /// <summary>
    /// Serve 'index.html' replacing SEO elements with values from app configuration.
    /// The processed result is cached at startup since PageTitle and PageDescription don't change at runtime.
    /// </summary>
    internal static WebApplication UseIndexPage(this WebApplication app)
    {
        // Build the processed index.html content once at startup
        var cachedContent = BuildIndexContent(app);

        if (cachedContent is not null)
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/")
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(cachedContent);
                    return;
                }

                await next();
            });
        }

        return app;
    }

    private static string? BuildIndexContent(WebApplication app)
    {
        var indexFile = app.Environment.WebRootFileProvider.GetFileInfo("/index.html").PhysicalPath;
        if (indexFile is null)
        {
            return null;
        }

        var options = app.Services.GetRequiredService<DeucalionOptions>();
        var htmlTitle = HttpUtility.HtmlEncode(options.PageTitle);
        var htmlDescription = HttpUtility.HtmlEncode(options.PageDescription);

        var indexContent = File.ReadAllText(indexFile);
        return indexContent
            .Replace("<!-- $DEUCALION__PAGETITLE -->", $"<title>{htmlTitle}</title>")
            .Replace("<!-- $DEUCALION__PAGEDESCRIPTION -->", $"<meta name=\"description\" content=\"{htmlDescription}\">");
    }
}
