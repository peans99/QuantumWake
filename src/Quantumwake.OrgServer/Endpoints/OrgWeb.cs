using System.Reflection;
using System.Text;

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
    /// <summary>
    /// The LAN-mode warning, spliced into every page by the server rather than
    /// drawn by the pages.
    /// </summary>
    /// <remarks>
    /// It is the one safeguard this mode has, so it must not be able to fail
    /// separately from the page it warns about: a banner fetched by script is
    /// absent on a blocked request, a JavaScript error or a stale cached
    /// asset, and absent is exactly wrong. Server-side it either arrives with
    /// the HTML or the HTML does not arrive either.
    /// </remarks>
    private const string LanBanner =
        """<div class="lan-banner" role="alert"><strong>LAN mode.</strong> There is no sign-in on this server: everyone who can reach it is the same account, and that account can change everything. Only run this where the network itself is the door.</div>""";

    public static void Map(WebApplication app)
    {
        MapPage(app, "/", "index.html");
        MapPage(app, "/link", "link.html");
        MapPage(app, "/account", "account.html");
        MapPage(app, "/admin", "admin.html");
        MapAsset(app, "/org.css", "org.css", "text/css");
        MapAsset(app, "/org.js", "org.js", "text/javascript");
    }

    private static void MapPage(WebApplication app, string path, string file) =>
        app.MapGet(path, (OrgServerOptions options) =>
        {
            if (Read(file) is not { } html)
                return Results.NotFound();

            if (options.LanMode)
                html = html.Replace("<body>", "<body>\n" + LanBanner, StringComparison.Ordinal);

            return Results.Text(html, "text/html", Encoding.UTF8);
        });

    private static void MapAsset(WebApplication app, string path, string file, string contentType) =>
        app.MapGet(path, () => Read(file) is { } text
            ? Results.Text(text, contentType, Encoding.UTF8)
            : Results.NotFound());

    private static string? Read(string file)
    {
        var loose = Path.Combine(AppContext.BaseDirectory, "web-org", file);
        if (File.Exists(loose))
            return File.ReadAllText(loose);

        using var embedded = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"web-org/{file}");
        if (embedded is null)
            return null;

        using var reader = new StreamReader(embedded);
        return reader.ReadToEnd();
    }
}
