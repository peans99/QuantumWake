using System.Reflection;
using Microsoft.AspNetCore.StaticFiles;

namespace Quantumwake.Server;

/// <summary>
/// Serves the dashboard from resources compiled into this assembly.
/// </summary>
/// <remarks>
/// <para>
/// The <c>web/</c> directory is also copied next to the binary, and that copy
/// wins when it exists — editing a stylesheet during development should not
/// mean a rebuild. This is the fallback, and it is what makes a single-file
/// build possible: a lone executable has no directory beside it to read.
/// </para>
/// <para>
/// A middleware rather than an <c>IFileProvider</c>. The static-file pipeline
/// wants directory listings, range requests and change tokens that a fixed set
/// of resources cannot meaningfully provide, and this is twenty lines against
/// a class that would have to lie about all three.
/// </para>
/// </remarks>
internal static class EmbeddedWeb
{
    private const string Prefix = "web/";

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>Request path (lower case, leading slash) to manifest resource name.</summary>
    private static readonly Dictionary<string, string> Files = Build();

    public static bool HasFiles => Files.Count > 0;

    private static Dictionary<string, string> Build()
    {
        var assembly = typeof(EmbeddedWeb).Assembly;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            // Logical names are written with forward slashes by the csproj, but
            // normalise anyway: a backslash here would be invisible until a
            // request for /assets/mark.svg quietly 404s.
            var path = "/" + name[Prefix.Length..].Replace('\\', '/');
            map[path] = name;
        }

        return map;
    }

    public static void Map(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                await next();
                return;
            }

            var path = context.Request.Path.Value ?? "/";
            if (path is "/" or "")
                path = "/index.html";

            if (!Files.TryGetValue(path, out var resource))
            {
                await next();
                return;
            }

            await using var stream = typeof(EmbeddedWeb).Assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                await next();
                return;
            }

            context.Response.ContentType = ContentTypes.TryGetContentType(path, out var type)
                ? type
                : "application/octet-stream";

            context.Response.ContentLength = stream.Length;

            if (HttpMethods.IsHead(context.Request.Method))
                return;

            await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
        });
    }
}
