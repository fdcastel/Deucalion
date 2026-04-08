using System.Security.Cryptography;
using System.Text;
using System.Web;
using Deucalion.Api.Options;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace Deucalion.Service;

internal static class WebApplicationExtensions
{
    /// <summary>
    /// Serve static files with pre-compressed (Brotli/Gzip) support and immutable caching for '/assets'.
    /// </summary>
    internal static WebApplication UseCachedFileServer(this WebApplication app)
    {
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        var webRootPath = app.Environment.WebRootPath;

        // Middleware: serve pre-compressed .br/.gz sidecar files for /assets/
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value;
            if (path?.StartsWith("/assets/") == true)
            {
                var acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
                var physicalPath = Path.Combine(webRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                string? compressedPath = null;
                string? encoding = null;

                if (acceptEncoding.Contains("br"))
                {
                    var brPath = physicalPath + ".br";
                    if (File.Exists(brPath))
                    {
                        compressedPath = brPath;
                        encoding = "br";
                    }
                }

                if (compressedPath is null && acceptEncoding.Contains("gzip"))
                {
                    var gzPath = physicalPath + ".gz";
                    if (File.Exists(gzPath))
                    {
                        compressedPath = gzPath;
                        encoding = "gzip";
                    }
                }

                if (compressedPath is not null)
                {
                    if (contentTypeProvider.TryGetContentType(path, out var contentType))
                    {
                        context.Response.ContentType = contentType;
                    }

                    context.Response.Headers.ContentEncoding = encoding;
                    context.Response.Headers.Vary = "Accept-Encoding";
                    context.Response.ContentLength = new FileInfo(compressedPath).Length;

                    SetImmutableCacheHeaders(context.Response);

                    await context.Response.SendFileAsync(compressedPath);
                    return;
                }
            }

            await next();
        });

        // Fallback: serve uncompressed files (response compression middleware handles on-the-fly compression)
        var fso = new FileServerOptions();
        fso.StaticFileOptions.OnPrepareResponse = (context) =>
        {
            if (context.Context.Request.Path.StartsWithSegments("/assets"))
            {
                SetImmutableCacheHeaders(context.Context.Response);
            }
        };

        app.UseFileServer(fso);

        return app;
    }

    /// <summary>
    /// Serve 'index.html' replacing SEO elements with values from app configuration.
    /// The processed result is cached at startup since PageTitle and PageDescription don't change at runtime.
    /// Supports conditional requests via ETag for efficient revalidation.
    /// </summary>
    internal static WebApplication UseIndexPage(this WebApplication app)
    {
        // Build the processed index.html content once at startup
        var cachedContent = BuildIndexContent(app);

        if (cachedContent is not null)
        {
            // Pre-compute ETag based on content hash
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(cachedContent));
            var etag = $"\"{Convert.ToHexString(hashBytes[..8])}\"";

            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/")
                {
                    // Conditional GET: return 304 if client has current version
                    var ifNoneMatch = context.Request.Headers.IfNoneMatch.ToString();
                    if (ifNoneMatch == "*" || ifNoneMatch.Contains(etag))
                    {
                        context.Response.StatusCode = StatusCodes.Status304NotModified;
                        context.Response.Headers.CacheControl = "no-cache";
                        context.Response.Headers.ETag = etag;
                        return;
                    }

                    context.Response.ContentType = "text/html";
                    context.Response.Headers.CacheControl = "no-cache";
                    context.Response.Headers.ETag = etag;
                    await context.Response.WriteAsync(cachedContent);
                    return;
                }

                await next();
            });
        }

        return app;
    }

    private static void SetImmutableCacheHeaders(HttpResponse response)
    {
        var headers = response.GetTypedHeaders();
        headers.CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromDays(365),
            Extensions = { new NameValueHeaderValue("immutable") }
        };
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
