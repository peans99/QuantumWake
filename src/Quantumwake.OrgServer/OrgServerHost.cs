using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Quantumwake.OrgServer.Auth;
using Quantumwake.OrgServer.Endpoints;
using Quantumwake.OrgServer.Store;
using Quantumwake.OrgShared;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace Quantumwake.OrgServer;

/// <summary>
/// The org server: one ASP.NET Core app that is its own three deployments -
/// an exe on a spare box, a container, and that container on Azure.
/// </summary>
/// <remarks>
/// <para>
/// Built from an options instance rather than process-wide state, so tests
/// host several of these in one process, in parallel, on port 0. That is the
/// deliberate opposite of the main server's <c>AppPaths</c>, whose test
/// fixture documents the cost of the static.
/// </para>
/// <para>
/// Endpoints live in files by concern rather than in one long host, because
/// nine modules arrive here across six releases and the main server's single
/// file is already the cautionary tale.
/// </para>
/// </remarks>
public static class OrgServerHost
{
    public static WebApplication Build(OrgServerOptions options)
    {
        Validate(options);
        var builder = WebApplication.CreateBuilder();
        // WebApplication's Windows defaults include Event Log, which can turn
        // a warning into a startup failure for an unprivileged service account.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.WebHost.UseUrls($"http://{options.Bind}:{options.Port}");

        // The global cap; the share endpoints of later slices stay under it
        // by construction because OrgLimits caps them tighter.
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.Limits.MaxRequestBodySize = OrgLimits.MaxShareBytes);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new OrgDb(options.DataDirectory, options.Journal));
        builder.Services.AddSingleton(provider => new AccountStore(
            provider.GetRequiredService<OrgDb>(), options.Admins,
            provider.GetRequiredService<ILogger<AccountStore>>()));
        builder.Services.AddSingleton<OrgStore>();
        builder.Services.AddSingleton<AuditStore>();
        builder.Services.AddSingleton<OrgActors>();
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(options.DataDirectory, "keys")))
            .SetApplicationName("Quantumwake.OrgServer");

        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            // The wire format is owned by OrgWire; this keeps the framework's
            // serializer agreeing with it.
            json.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            json.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            json.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
        });

        builder.Services.AddAuthentication("cookie").AddCookie("cookie", cookie =>
        {
            cookie.Cookie.Name = "qw-org-session";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.SecurePolicy = options.BehindProxy
                && options.PublicBaseUrl?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true
                ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            cookie.ExpireTimeSpan = TimeSpan.FromDays(30);
            cookie.SlidingExpiration = true;

            // An API caller with no session gets told so, not bounced to a
            // page it cannot render.
            cookie.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
        });

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = 429;

            // The only surface a stranger can touch gets the tightest budget.
            limiter.AddPolicy("link-start", context => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                }));

            // Polling is sanctioned at the interval the start response stated,
            // so its budget allows exactly that plus slack, and no more.
            limiter.AddPolicy("link-poll", context => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                }));

            limiter.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
                ApiPartition(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 240,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });

        var app = builder.Build();

        if (options.BehindProxy)
        {
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            };
            foreach (var proxy in options.TrustedProxies)
                forwarded.KnownProxies.Add(proxy);
            app.UseForwardedHeaders(forwarded);
        }

        app.UseAuthentication();
        app.UseRateLimiter();

        // Said once, loudly, where an operator looks first. The banner covers
        // the person in the browser; this covers the person who started it and
        // will not open a page at all.
        if (options.LanMode)
        {
            app.Logger.LogWarning(
                "LAN MODE: authentication is off. Everyone who can reach {Bind}:{Port} is signed in "
                + "as the same account and is a server admin. Only run this where the network itself "
                + "is the door - never behind a public address.", options.Bind, options.Port);

            if (options.OAuth.Count > 0)
            {
                app.Logger.LogWarning(
                    "LAN MODE also has {Count} sign-in provider(s) configured. They are ignored while "
                    + "LAN mode is on.", options.OAuth.Count);
            }
        }

        app.MapGet("/healthz", (OrgDb db) =>
        {
            using var connection = db.Open();
            return Results.Ok(new { status = "ok", version = ServerVersion() });
        });

        app.MapGet("/api/meta", () => Results.Ok(new OrgServerMetadata(
            ServerVersion(), OrgWire.FormatVersion, options.LanMode,
            (options.LanMode ? [] : options.OAuth.Select(p => new OrgProviderInfo(p.Key, p.Name)).ToArray()),
            ["org-management", "blueprints", "audit"])))
            .RequireRateLimiting("link-start");

        OrgWeb.Map(app);
        AuthEndpoints.Map(app);
        AccountEndpoints.Map(app);
        OrgEndpoints.Map(app);

        return app;
    }

    internal static string ServerVersion() =>
        typeof(OrgServerHost).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string ApiPartition(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = header[7..].Trim();
            var identity = context.RequestServices.GetRequiredService<AccountStore>()
                .RateLimitIdentity(token);
            if (identity is not null)
                return identity;
        }
        return "ip:" + ClientIp(context);
    }

    private static void Validate(OrgServerOptions options)
    {
        if (options.BehindProxy && options.TrustedProxies.Count == 0)
            throw new InvalidOperationException(
                "--BehindProxy requires --TrustedProxies so forwarded headers cannot be forged.");

        if (options.PublicBaseUrl is { Length: > 0 } text
            && (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")))
            throw new InvalidOperationException("--PublicBaseUrl must be an absolute http(s) address.");

        if (options.PublicBaseUrl is { Length: > 0 } publicText
            && Uri.TryCreate(publicText, UriKind.Absolute, out var publicUri)
            && publicUri.Scheme == "http" && !publicUri.IsLoopback && !options.LanMode)
            throw new InvalidOperationException("A public --PublicBaseUrl must use HTTPS.");
    }
}
