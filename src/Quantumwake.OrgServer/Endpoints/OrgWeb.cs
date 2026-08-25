using System.Reflection;

namespace Quantumwake.OrgServer.Endpoints;

/// <summary>
/// The handful of pages a browser needs: the landing page, the link-approve
/// page, the account page and the admin desk.
/// </summary>
/// <remarks>
/// Served the same two ways as the main server's web/: loose files beside a
/// development build so edits show up, embedded resources in a published
/// binary so the exe is the whole deployment. No cache - these pages change
/// with the binary and nothing else.
/// </remarks>
public static class OrgWeb
{
    public static void Map(WebApplication app)
    {
        MapFile(app, "/", "index.html", "text/html");
        MapFile(app, "/link", "link.html", "text/html");
        MapFile(app, "/account", "account.html", "text/html");
        MapFile(app, "/admin", "admin.html", "text/html");
        MapFile(app, "/org.css", "org.css", "text/css");
        MapFile(app, "/org.js", "org.js", "text/javascript");
    }

    private static void MapFile(WebApplication app, string path, string file, string contentType)
    {
        app.MapGet(path, () =>
        {
            var loose = Path.Combine(AppContext.BaseDirectory, "web-org", file);
            if (File.Exists(loose))
                return Results.File(loose, contentType);

            var embedded = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream($"web-org/{file}");
            return embedded is null
                ? Results.NotFound()
                : Results.Stream(embedded, contentType);
        });
    }
}
