using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// Builds the Quantum Wake web server.
/// </summary>
/// <remarks>
/// Separated from the entry point so the same server can be started two ways:
/// as its own process, and in-process by the overlay shell, which is what lets
/// the whole application ship as a single executable rather than one binary
/// launching another.
/// </remarks>
public static class ServerHost
{
    /// <summary>
    /// Configures the server without starting it. Call <c>Run</c> to block, or
    /// <c>StartAsync</c> to run it alongside a UI.
    /// </summary>
    public static WebApplication Build(string[] args)
    {

        // Quantumwake server.
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
        builder.Services.AddSingleton<ScanStatus>();
        builder.Services.AddSingleton<LiveSessionService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<LiveSessionService>());

        // Solely for the opt-in community-dataset download; nothing else in the
        // application makes an outbound request.
        builder.Services.AddHttpClient("community", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QuantumWake");
        });

        // The overlay shell attaches to this after startup; under the bare
        // server it stays unattached and the endpoints report unavailable.
        builder.Services.AddSingleton<OverlayBridge>();

        // UEX price integration, opt-in in both directions.
        builder.Services.AddSingleton<UexData>();

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
        else if (EmbeddedWeb.HasFiles)
        {
            // Single-file build: no directory sits beside the executable, so the
            // UI is served out of the assembly instead.
            EmbeddedWeb.Map(app);
        }

        app.MapHub<LiveHub>("/hub/live");

        app.MapGet("/api/install", (LogLibrary lib) => install is null
            ? Results.NotFound(new { message = "No Star Citizen install found." })
            : Results.Ok(new
            {
                channel = install.Channel,
                root = install.RootPath,
                hasGameLog = install.HasGameLog,
                backups = install.BackupLogs().Count,
                names = new
                {
                    loaded = lib.Names.IsLoaded,
                    items = lib.Names.ItemCount,
                    vehicles = lib.Names.VehicleCount,
                    places = lib.Names.PlaceCount
                }
            }));

        // The community dataset: commodity names for the resource ids the game
        // logs but never explains. Enabling it performs the application's one
        // and only outbound request - a single file, fetched on the user's
        // explicit click, cached locally. See CommunityData for the reasoning.
        app.MapGet("/api/community", (LogLibrary lib) => new
        {
            enabled = lib.Community.IsEnabled,
            commodities = lib.Community.Count,
            fetchedAt = lib.Community.FetchedAt,
            source = CommunityData.CommoditiesUrl
        });

        app.MapPost("/api/community/enable", async (LogLibrary lib, IHttpClientFactory httpFactory) =>
        {
            try
            {
                var count = await lib.Community.EnableAsync(httpFactory.CreateClient("community"));
                return Results.Ok(new { enabled = true, commodities = count });
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException)
            {
                return Results.Problem(
                    title: "The community dataset could not be fetched.",
                    detail: e.Message,
                    statusCode: 502);
            }
        });

        app.MapPost("/api/community/disable", (LogLibrary lib) =>
        {
            lib.Community.Disable();
            return Results.Ok(new { enabled = false });
        });

        // The in-game overlay, controllable from the dashboard when the server
        // is hosted inside QuantumWake.exe. The bare server has no window, so
        // these report unavailable rather than pretending.
        app.MapGet("/api/overlay", (OverlayBridge overlay) => new
        {
            available = overlay.Available,
            visible = overlay.Visible
        });

        app.MapPost("/api/overlay", (OverlayBridge overlay, bool visible) =>
            overlay.TrySet(visible)
                ? Results.Ok(new { available = true, visible })
                : Results.Conflict(new
                {
                    message = "No overlay in this process - the dashboard is running under the bare server."
                }));

        // Display names come out of the game's own localisation table, so they go stale
        // when Star Citizen patches. The cache is stamped with Data.p4k's write time and
        // rebuilds itself, but this forces it.
        app.MapPost("/api/names/refresh", (LogLibrary lib) =>
        {
            if (install is null)
                return Results.NotFound(new { message = "No install." });

            lib.LoadNames(install.RootPath);

            return Results.Ok(new
            {
                loaded = lib.Names.IsLoaded,
                items = lib.Names.ItemCount,
                vehicles = lib.Names.VehicleCount,
                places = lib.Names.PlaceCount
            });
        });

        // Shown on the About page and in the footer. Reflection rather than a
        // constant so it can never disagree with the assembly actually running.
        // The build string is the informational version, which a CI build stamps
        // with the commit ("0.2.0+abc1234") and a source build leaves plain.
        app.MapGet("/api/version", () =>
        {
            var assembly = typeof(ServerHost).Assembly;

            return new
            {
                version = assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                build = assembly
                    .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                    .FirstOrDefault()?.InformationalVersion
            };
        });

        app.MapGet("/api/scan/status", (ScanStatus status) => status.Snapshot());

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

        // days=0 (or absent) means all time; the views each pick their own window.
        app.MapGet("/api/stats", (LogLibrary lib, int? days) => lib.Stats(days ?? 0));

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

            var status = app.Services.GetRequiredService<ScanStatus>();
            status.Begin();

            try
            {
                var parsed = lib.Scan(install, Progress(status), force ?? false);
                return Results.Ok(new { parsed, sessions = lib.Store.Count() });
            }
            finally
            {
                status.Finish();
            }
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

        app.MapGet("/api/ledger", (LogLibrary lib, int? days) => lib.Ledger(days ?? 0));

        // Trades with the UEX comparison joined on: what the best sell was, so
        // the page can say what a sale left on the table.
        app.MapGet("/api/commodities", (LogLibrary lib, UexData uex, int? days) =>
            lib.Trades(days ?? 0).Select(t => new
            {
                t.At,
                t.IsSell,
                t.Place,
                t.Scu,
                t.Amount,
                t.UnitPrice,
                t.Mode,
                t.Commodity,
                uexBestSell = t.IsSell && t.Commodity is not null
                    ? uex.Best(t.Commodity)?.BestSell
                    : null
            }));

        // Trading opportunities from wherever the player is: what this place's
        // terminal sells cheap, and where it fetches the most. Empty terminal
        // means the place matched nothing on UEX and the card says so.
        app.MapGet("/api/trade/advice", (UexData uex, string place) => new
        {
            place,
            terminal = uex.TerminalFor(place),
            opportunities = uex.Opportunities(place)
        });

        // What the pilot owns, priced: fleet at in-game purchase prices, kit
        // and stash at item prices, plus what dying costs. Every number here is
        // an estimate built on community prices and says so in the UI.
        app.MapGet("/api/assets", (LogLibrary lib, UexData uex) =>
        {
            var stats = lib.Stats();

            var fleet = stats.Ships.Select(s => new
            {
                s.Name,
                price = uex.VehiclePrice(s.Reference?.Name ?? s.Name)
            }).ToList();

            var loadout = stats.Loadout
                .SelectMany(slot => slot.Items)
                .Select(i => new { i.Name, price = uex.ItemPrice(i.Reference?.Uuid) })
                .ToList();

            var stash = stats.Stash
                .Select(s =>
                {
                    var items = s.Groups.SelectMany(g => g.Items).ToList();
                    var priced = items
                        .Select(i => uex.ItemPrice(lib.Community.Item(i.ItemClass)?.Uuid))
                        .Where(p => p is not null)
                        .Select(p => p!.Value)
                        .ToList();

                    return new
                    {
                        location = s.Name,
                        value = priced.Sum(),
                        priced = priced.Count,
                        items = s.ItemCount
                    };
                })
                .ToList();

            // Claim exposure: deaths per session times the expedite fee of the
            // ships flown that session. The log never says which ship died, so
            // this is labelled an estimate and computed conservatively from the
            // session average.
            decimal claimExposure = 0;
            foreach (var session in lib.Sessions())
            {
                if (session.Deaths == 0)
                    continue;

                var fees = session.Ships
                    .Select(ship => lib.Community.Ship($"{ship.Manufacturer}_{ship.Model}")?.ExpeditedCost)
                    .Where(f => f > 0)
                    .Select(f => f!.Value)
                    .ToList();

                if (fees.Count > 0)
                    claimExposure += session.Deaths * (decimal)fees.Average();
            }

            return Results.Ok(new
            {
                fleet,
                fleetValue = fleet.Sum(f => f.price?.Price ?? 0),
                fleetPriced = fleet.Count(f => f.price is not null),
                loadoutValue = loadout.Sum(l => l.price ?? 0),
                loadoutPriced = loadout.Count(l => l.price is not null),
                loadoutItems = loadout.Count,
                stash,
                stashValue = stash.Sum(s => s.value),
                claimExposure,
                priced = uex.IsEnabled && lib.Community.IsEnabled
            });
        });

        // Items observed entering the player's inventories - the Loot page.
        app.MapGet("/api/loot", (LogLibrary lib, int? days) => lib.Pickups(days ?? 0));

        // The community catalogue joined onto this install's trades, plus UEX
        // live prices when that integration is on. Empty until the community
        // dataset is enabled, and the page explains that.
        app.MapGet("/api/market", (LogLibrary lib, UexData uex) =>
            lib.Market().Select(entry => new
            {
                entry.Id,
                entry.Name,
                entry.Groups,
                entry.Sold,
                entry.Bought,
                entry.MyScuSold,
                entry.MyRevenue,
                entry.MyTrades,
                uex = uex.Best(entry.Name)
            }));

        // ---- UEX: live prices in, logged sale prices out. Both opt-in. ----

        app.MapGet("/api/uex", (UexData uex) => new
        {
            enabled = uex.IsEnabled,
            prices = uex.Count,
            fetchedAt = uex.FetchedAt,
            hasCredentials = uex.HasCredentials,
            source = "api.uexcorp.space"
        });

        // Every terminal price for one commodity: the map grades its sellers
        // and buyers by these, by price or by SCU capacity.
        app.MapGet("/api/uex/market", (UexData uex, string commodity) =>
            uex.Market(commodity).Select(r => new
            {
                terminal = r.Terminal,
                buy = r.Buy,
                sell = r.Sell,
                buyScu = r.BuyScu,
                sellScu = r.SellScu
            }));

        app.MapPost("/api/uex/enable", async (UexData uex, IHttpClientFactory httpFactory) =>
        {
            try
            {
                var count = await uex.EnableAsync(httpFactory.CreateClient("community"));
                return Results.Ok(new { enabled = true, prices = count });
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException or JsonException)
            {
                return Results.Problem(title: "UEX could not be fetched.", detail: e.Message, statusCode: 502);
            }
        });

        app.MapPost("/api/uex/disable", (UexData uex) =>
        {
            uex.Disable();
            return Results.Ok(new { enabled = false });
        });

        // Stored only in local app data; posting empty values removes them.
        app.MapPost("/api/uex/credentials", (UexData uex, UexCredentialsRequest body) =>
        {
            uex.SetCredentials(body.Token, body.Secret);
            return Results.Ok(new { hasCredentials = uex.HasCredentials });
        });

        // What a push would send: every named sale in the last 30 days (the UEX
        // window), with the terminal match or the reason there is none.
        app.MapGet("/api/uex/pushable", (LogLibrary lib, UexData uex) =>
            uex.Pushable(RecentSales(lib)));

        app.MapPost("/api/uex/push", async (LogLibrary lib, UexData uex, IHttpClientFactory httpFactory) =>
        {
            if (!uex.HasCredentials)
                return Results.Problem(title: "No UEX credentials stored.", statusCode: 400);

            var rows = uex.Pushable(RecentSales(lib));
            var matched = rows.Where(r => r.TerminalId is not null && r.CommodityId is not null).ToList();

            if (matched.Count == 0)
                return Results.Ok(new { sent = 0, results = Array.Empty<string>() });

            try
            {
                var results = await uex.PushAsync(httpFactory.CreateClient("community"), matched);
                return Results.Ok(new { sent = matched.Count, results });
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                return Results.Problem(title: "The UEX submission failed.", detail: e.Message, statusCode: 502);
            }
        });

        app.MapGet("/api/loadout", (LogLibrary lib) => lib.Stats().Loadout);
        app.MapGet("/api/loadout/asof", (LogLibrary lib) => new { asOf = lib.Stats().LoadoutAsOf });

        app.MapGet("/api/stash", (LogLibrary lib) => lib.Stats().Stash);

        app.MapGet("/api/map", (LogLibrary lib) =>
        {
            var stats = lib.Stats();

            // Real body coordinates from the community starmap, per system, so
            // the layout can be geometry instead of an even ring. Empty until
            // the dataset is enabled, and the client falls back to the ring.
            var positions = new[] { "stanton", "pyro", "nyx" }
                .Select(system => (System: system, Bodies: lib.Community.BodyPositions(system)))
                .Where(x => x.Bodies.Count > 0)
                .ToDictionary(x => x.System, x => x.Bodies);

            // The atlas carries unvisited places too, so the map can show the whole
            // system and let the player filter down to where they have actually been.
            return Results.Ok(new { nodes = lib.Atlas(), destinations = stats.Destinations, positions });
        });

        // Warm the cache in the background so first paint is not blocked by a cold
        // 400 MB backfill.
        if (install is not null)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    // Names first: cheap when cached, and every view reads better with them.
                    library.LoadNames(install.RootPath);
                    app.Logger.LogInformation("Game names: {Items} items, {Vehicles} vehicles.",
                        library.Names.ItemCount, library.Names.VehicleCount);

                    var status = app.Services.GetRequiredService<ScanStatus>();
                    status.Begin();

                    var parsed = library.Scan(install, Progress(status));
                    status.Finish();
                    app.Logger.LogInformation("Library ready: {Parsed} newly parsed, {Total} sessions.",
                        parsed, library.Store.Count());
                }
                catch (Exception e)
                {
                    app.Logger.LogError(e, "Initial scan failed.");
                }
            });
        }

        app.Logger.LogInformation("Quantum Wake by nekron - http://{Host}:{Port}", host, port);
        return app;
    }

    /// <summary>Bridges the library's progress callback to the shared status.</summary>
    static IProgress<ScanProgress> Progress(ScanStatus status) =>
        new Progress<ScanProgress>(p => status.Report(p.Done, p.Total, p.CurrentFile, p.WasCached));

    /// <summary>
    /// The sales a UEX push draws from: named commodity sells inside UEX's
    /// 30-day submission window, with the unit price the kiosk actually showed.
    /// </summary>
    static IEnumerable<(DateTimeOffset At, string Commodity, string Place, decimal UnitPrice, int Scu)>
        RecentSales(LogLibrary lib)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);

        return lib.Trades(31)
            .Where(t => t.IsSell && t.Commodity is not null && t.At >= cutoff && t.Scu > 0)
            .Select(t => (t.At, t.Commodity!, t.Place, t.UnitPrice, t.Scu));
    }

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
}

/// <summary>Body of POST /api/uex/credentials. Empty values clear the store.</summary>
public sealed record UexCredentialsRequest(string? Token, string? Secret);
