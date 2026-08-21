using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using SCCompanion.Core.Logging;
using SCCompanion.Data;
using SCCompanion.Server;

// SC Companion server.
//
// Serves one web UI to three consumers: a browser on a second screen, the
// WebView2 control inside the overlay shell, and (later) remote clients in
// server mode. REST answers historical queries; SignalR pushes the live view.

var builder = WebApplication.CreateBuilder(args);

var install = ResolveInstall(args, builder.Configuration);

// Scope the cache to the install, so a PTU channel or a simulated install never
// blends its sessions into the LIVE totals.
var database = builder.Configuration["Database"]
    ?? SessionStore.DatabasePathFor(install?.RootPath);

var library = new LogLibrary(database);

builder.Services.AddSingleton(library);
builder.Services.AddSingleton(install!);
builder.Services.AddSignalR();
builder.Services.AddSingleton<LiveSessionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LiveSessionService>());

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Bind to loopback unless explicitly opened up. Standalone mode is local-only;
// exposing the dashboard to the LAN (for a tablet as second screen) is opt-in.
var port = builder.Configuration.GetValue("Port", 31337);
var host = builder.Configuration.GetValue<bool>("Lan") ? "0.0.0.0" : "127.0.0.1";
builder.WebHost.UseUrls($"http://{host}:{port}");

var app = builder.Build();

// The web UI lives outside the project so the overlay and the browser load the
// exact same files.
var webRoot = ResolveWebRoot();
if (webRoot is not null)
{
    var files = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

app.MapHub<LiveHub>("/hub/live");

app.MapGet("/api/install", () => install is null
    ? Results.NotFound(new { message = "No Star Citizen install found." })
    : Results.Ok(new
    {
        channel = install.Channel,
        root = install.RootPath,
        hasGameLog = install.HasGameLog,
        backups = install.BackupLogs().Count
    }));

app.MapGet("/api/now", (LiveSessionService live) => live.Current);

// Server-Sent Events feed for the browser UI.
//
// The SignalR hub above stays mapped as the seam for Phase 6 multi-client work,
// but the dashboard uses SSE instead: EventSource is built into the browser, so
// the page needs no bundled client library and no CDN - which matters because
// standalone mode makes no outbound network calls at all.
app.MapGet("/api/stream", async (HttpContext context, LiveSessionService live, CancellationToken token) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers["X-Accel-Buffering"] = "no";

    var options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    try
    {
        while (!token.IsCancellationRequested)
        {
            var payload = JsonSerializer.Serialize(live.Current, options);
            await context.Response.WriteAsync($"data: {payload}\n\n", token);
            await context.Response.Body.FlushAsync(token);
            await Task.Delay(TimeSpan.FromSeconds(1), token);
        }
    }
    catch (OperationCanceledException)
    {
        // Client navigated away.
    }
});

app.MapGet("/api/stats", (LogLibrary lib) => lib.Stats());

app.MapGet("/api/sessions", (LogLibrary lib) => lib.Sessions().Select(s => new
{
    s.Id,
    s.StartedAt,
    s.EndedAt,
    duration = s.Duration.TotalSeconds,
    inGame = s.InGameDuration.TotalSeconds,
    menu = s.MenuDuration.TotalSeconds,
    s.Handle,
    s.GameVersion,
    s.PrimaryShip,
    s.LastLocation,
    ships = s.Ships.Count,
    locations = s.Locations.Count,
    jumps = s.Jumps.Count,
    contracts = s.Contracts.Count,
    s.Incapacitations,
    s.Deaths,
    s.Disconnects
}));

app.MapGet("/api/sessions/{id}", (string id, LogLibrary lib) =>
    lib.Session(id) is { } session ? Results.Ok(session) : Results.NotFound());

// Rescan on demand: unchanged backups are skipped by fingerprint, so this is
// cheap after the first run.
app.MapPost("/api/scan", (LogLibrary lib, bool? force) =>
{
    if (install is null)
        return Results.NotFound(new { message = "No install." });

    var parsed = lib.Scan(install, force: force ?? false);
    return Results.Ok(new { parsed, sessions = lib.Store.Count() });
});

app.MapGet("/api/fleet", (LogLibrary lib) =>
{
    var stats = lib.Stats();
    return Results.Ok(new
    {
        owned = stats.FleetSize,
        history = stats.FleetHistory,
        flown = stats.Ships
    });
});

app.MapGet("/api/spending", (LogLibrary lib) =>
{
    var stats = lib.Stats();
    return Results.Ok(new
    {
        total = stats.Spend,
        count = stats.PurchaseCount,
        shops = stats.Shops,
        items = stats.Items
    });
});

app.MapGet("/api/loadout", (LogLibrary lib) => lib.Stats().Loadout);

app.MapGet("/api/stash", (LogLibrary lib) => lib.Stats().Stash);

app.MapGet("/api/map", (LogLibrary lib) =>
{
    var stats = lib.Stats();
    return Results.Ok(new { nodes = stats.Locations, destinations = stats.Destinations });
});

// Warm the cache in the background so first paint is not blocked by a cold
// 400 MB backfill.
if (install is not null)
{
    _ = Task.Run(() =>
    {
        try
        {
            var parsed = library.Scan(install);
            app.Logger.LogInformation("Library ready: {Parsed} newly parsed, {Total} sessions.",
                parsed, library.Store.Count());
        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "Initial scan failed.");
        }
    });
}

app.Logger.LogInformation("SC Companion by nekron - http://{Host}:{Port}", host, port);
app.Run();
return;

static GameInstall? ResolveInstall(string[] args, IConfiguration configuration)
{
    var index = Array.IndexOf(args, "--path");
    if (index >= 0 && index + 1 < args.Length)
        return GameInstallLocator.FromPath(args[index + 1]);

    var configured = configuration["InstallPath"];
    if (!string.IsNullOrWhiteSpace(configured))
        return GameInstallLocator.FromPath(configured);

    return GameInstallLocator.Preferred();
}

/// <summary>Finds the shared web/ directory whether running from source or published.</summary>
static string? ResolveWebRoot()
{
    var candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "web"),
        Path.Combine(Directory.GetCurrentDirectory(), "web"),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "web"))
    };

    return candidates.FirstOrDefault(Directory.Exists);
}
