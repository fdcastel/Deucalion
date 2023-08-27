using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace Deucalion.Service;

public static class WebApplicationExtensions
{
    public static WebApplication UseDeucalionSpa(this WebApplication app)
    {
        app.UseStaticFiles();

        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/")
            {
                // Serve "index.html" replacing "import_meta_env_placeholder".

                // Workaround for "Synchronous operations are disallowed" error in Linux. (!?)
                //   -- https://stackoverflow.com/a/67632199/33244
                var syncIOFeature = context.Features.Get<IHttpBodyControlFeature>();
                if (syncIOFeature != null)
                {
                    syncIOFeature.AllowSynchronousIO = true;
                }

                var pageTitle = Environment.GetEnvironmentVariable("DEUCALION_PAGE_TITLE") ?? string.Empty;

                var indexFile = app.Environment.WebRootFileProvider.GetFileInfo("/index.html").PhysicalPath;
                if (indexFile is not null)
                {
                    var indexContent = await File.ReadAllTextAsync(indexFile);

                    var newContent = indexContent.Replace(
                        "\"import_meta_env_placeholder\"",
                        GetImportMetaEnvJson(pageTitle)
                    );

                    using var bodyWriter = new StreamWriter(context.Response.Body);
                    await bodyWriter.WriteAsync(newContent);

                    return;
                }
            }

            await next();
        });

        return app;
    }

    private static string GetImportMetaEnvJson(string pageTitle) =>
        JsonSerializer.Serialize(new
        {
            DEUCALION_PAGE_TITLE = pageTitle,
        });
}
