using System.Security.Cryptography;
using System.Text;
using System.Web;
using Deucalion.Api.Endpoints;
using Deucalion.Api.Options;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Primitives;
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

                    // Content negotiation: the same URL a human gets serves agents the
                    // self-describing JSON summary when they ask for it. Both variants
                    // must carry Vary: Accept, or a cache could hand one client the other's.
                    context.Response.Headers.Vary = "Accept";
                    if (PrefersJson(context.Request.Headers.Accept))
                    {
                        if (HttpMethods.IsHead(method))
                        {
                            context.Response.ContentType = "application/json";
                            return;
                        }

                        await DiscoveryEndpoints.WriteStatusAsync(context);
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

    /// <summary>
    /// Whether the Accept header ranks <c>application/json</c> strictly above <c>text/html</c>.
    /// A browser's Accept (<c>text/html,...,*/*;q=0.8</c>) and curl's bare <c>*/*</c> both keep
    /// HTML; only a client that asks for JSON by name (or with a higher q) gets it.
    /// </summary>
    internal static bool PrefersJson(StringValues accept)
    {
        if (!MediaTypeHeaderValue.TryParseList(accept, out var accepted))
        {
            return false;
        }

        return Quality(accepted, "application", "json") > Quality(accepted, "text", "html");
    }

    /// <summary>
    /// The q-value the most specific matching range assigns to <paramref name="type"/>/<paramref name="subType"/>
    /// (exact beats <c>type/*</c> beats <c>*/*</c>); 0 when nothing matches.
    /// </summary>
    private static double Quality(IList<MediaTypeHeaderValue> accepted, string type, string subType)
    {
        var best = 0.0;
        var bestSpecificity = -1;

        foreach (var range in accepted)
        {
            int specificity;
            if (range.MatchesAllTypes)
            {
                specificity = 0;
            }
            else if (!range.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            else if (range.MatchesAllSubTypes)
            {
                specificity = 1;
            }
            else if (range.SubType.Equals(subType, StringComparison.OrdinalIgnoreCase))
            {
                specificity = 2;
            }
            else
            {
                continue;
            }

            if (specificity > bestSpecificity)
            {
                bestSpecificity = specificity;
                best = range.Quality ?? 1;
            }
        }

        return best;
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

        // Head metadata is what survives an agent's text-extraction pipeline (empirically: the
        // title and meta tags of the served page made it through; the body did not), so the
        // API is advertised here, in the initial payload, not by anything JS renders later.
        var description = HttpUtility.HtmlEncode(
            $"{options.PageTitle} — live service status. Machine-readable status at {DiscoveryEndpoints.StatusPath}; docs at {DiscoveryEndpoints.DocsPath}");

        var head =
            $"<title>{htmlTitle}</title>\n" +
            $"    <link rel=\"alternate\" type=\"application/json\" href=\"{DiscoveryEndpoints.StatusPath}\" />\n" +
            $"    <meta name=\"description\" content=\"{description}\" />";

        var noscript =
            $"<noscript><p>This page needs JavaScript. Machine-readable status: <a href=\"{DiscoveryEndpoints.StatusPath}\">{DiscoveryEndpoints.StatusPath}</a></p></noscript>";

        var indexContent = File.ReadAllText(indexFile);
        return indexContent
            .Replace("<!-- $DEUCALION__PAGETITLE -->", head)
            .Replace("<!-- $DEUCALION__NOSCRIPT -->", noscript);
    }
}
