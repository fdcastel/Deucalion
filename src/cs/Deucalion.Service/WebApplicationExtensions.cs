using System.Web;
using Deucalion.Api.Options;
using Microsoft.AspNetCore.Http.Features;

namespace Deucalion.Service;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Use file server with response cache for all static files in '/assets'.
    /// </summary>
    public static WebApplication UseCachedFileServer(this WebApplication app)
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
    /// </summary>
    public static WebApplication UseIndexPage(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/")
            {
                // Workaround for "Synchronous operations are disallowed" error in Linux. (!?)
                //   -- https://stackoverflow.com/a/67632199/33244
                var syncIOFeature = context.Features.Get<IHttpBodyControlFeature>();
                if (syncIOFeature != null)
                {
                    syncIOFeature.AllowSynchronousIO = true;
                }

                var indexFile = app.Environment.WebRootFileProvider.GetFileInfo("/index.html").PhysicalPath;
                if (indexFile is not null)
                {
                    var options = app.Services.GetRequiredService<DeucalionOptions>();
                    var htmlTitle = HttpUtility.HtmlEncode(options.PageTitle);
                    var htmlDescription = HttpUtility.HtmlEncode(options.PageDescription);

                    var indexContent = await File.ReadAllTextAsync(indexFile);
                    var newContent = indexContent
                        .Replace("<!-- $DEUCALION__PAGETITLE -->", $"<title>{htmlTitle}</title>")
                        .Replace("<!-- $DEUCALION__PAGEDESCRIPTION -->", $"<meta name=\"description\" content=\"{htmlDescription}\">");

                    using var bodyWriter = new StreamWriter(context.Response.Body);
                    await bodyWriter.WriteAsync(newContent);
                    return;
                }
            }

            await next();
        });

        return app;
    }
}
