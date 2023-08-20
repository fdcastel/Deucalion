using Microsoft.AspNetCore.Http.Features;

namespace Deucalion.Service;

public static class WebApplicationExtensions
{
    public static WebApplication UseDeucalionSpa(this WebApplication app)
    {
        app.UseStaticFiles();

        app.Use(async (context, next) =>
        {
            // Serve "index.html" in "/" replacing "import_meta_env_placeholder\".
            if (context.Request.Path == "/")
            {
                // Workaround for "Synchronous operations are disallowed" error in Linux. (!?)
                //   -- https://stackoverflow.com/a/67632199/33244
                var syncIOFeature = context.Features.Get<IHttpBodyControlFeature>();
                if (syncIOFeature != null)
                {
                    syncIOFeature.AllowSynchronousIO = true;
                }

                var pageTitle = Environment.GetEnvironmentVariable("DEUCALION_PAGE_TITLE");
                var apiUrl = Environment.GetEnvironmentVariable("DEUCALION_API_URL");

                var importMetaEnvJson = "{\"DEUCALION_PAGE_TITLE\":\"" + pageTitle + "\",\"DEUCALION_API_URL\":\"" + apiUrl + "\"}";

                var indexFile = app.Environment.WebRootFileProvider.GetFileInfo("/index.html").PhysicalPath;
                if (indexFile is not null)
                {
                    var indexContent = await File.ReadAllTextAsync(indexFile);

                    var newContent = indexContent.Replace(
                        "\"import_meta_env_placeholder\"",
                        importMetaEnvJson
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
}
