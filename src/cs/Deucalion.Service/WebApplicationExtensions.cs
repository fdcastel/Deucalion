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
                var physicalPath = Path.Combine(webRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                // Parse Accept-Encoding as a proper token list with q-values. A substring test
                // would match e.g. "xbr" and would serve brotli to a client sending "br;q=0".
                StringWithQualityHeaderValue.TryParseList(context.Request.Headers.AcceptEncoding, out var acceptedEncodings);

                string? compressedPath = null;
                string? encoding = null;

                if (Accepts(acceptedEncodings, "br"))
                {
                    var brPath = physicalPath + ".br";
                    if (File.Exists(brPath))
                    {
                        compressedPath = brPath;
                        encoding = "br";
                    }
                }

                if (compressedPath is null && Accepts(acceptedEncodings, "gzip"))
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
    /// The processed result is cached at startup since PageTitle doesn't change at runtime.
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
                    // The page is a read-only resource: anything but GET/HEAD is not allowed.
                    var method = context.Request.Method;
                    if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
                    {
                        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                        context.Response.Headers.Allow = "GET, HEAD";
                        return;
                    }

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

                    // HEAD: same headers as GET, no body.
                    if (HttpMethods.IsGet(method))
                    {
                        await context.Response.WriteAsync(cachedContent);
                    }

                    return;
                }

                await next();
            });
        }

        return app;
    }

    /// <summary>
    /// Whether the parsed Accept-Encoding list allows <paramref name="encoding"/>: an explicit
    /// entry wins (its q-value decides, missing q means 1); otherwise a wildcard entry decides;
    /// otherwise the encoding is not accepted. Comparison is case-insensitive per RFC 9110.
    /// </summary>
    private static bool Accepts(IList<StringWithQualityHeaderValue>? acceptedEncodings, string encoding)
    {
        if (acceptedEncodings is null)
        {
            return false;
        }

        StringWithQualityHeaderValue? wildcard = null;
        foreach (var item in acceptedEncodings)
        {
            if (item.Value.Equals(encoding, StringComparison.OrdinalIgnoreCase))
            {
                return (item.Quality ?? 1) > 0;
            }

            if (item.Value.Equals("*", StringComparison.Ordinal))
            {
                wildcard = item;
            }
        }

        return wildcard is not null && (wildcard.Quality ?? 1) > 0;
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

        var indexContent = File.ReadAllText(indexFile);
        return indexContent
            .Replace("<!-- $DEUCALION__PAGETITLE -->", $"<title>{htmlTitle}</title>");
    }
}
