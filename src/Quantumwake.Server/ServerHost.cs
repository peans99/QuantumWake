using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
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

        // Before anything reads a path: --data moves every cache, digest, job
        // and setting somewhere else, which is what makes a genuinely fresh
        // first run testable without disturbing the real one.
        Core.AppPaths.UseFromArguments(args);

        // Whether this data folder was in use before this process touched it,
        // decided here because everything below starts writing to it - the
        // database appears within seconds of the first scan, so asking later
        // would call every install established, new ones included.
        var establishedInstall =
            Directory.Exists(Core.AppPaths.Root)
            && Directory.EnumerateFileSystemEntries(Core.AppPaths.Root).Any();

        var builder = WebApplication.CreateBuilder(args);

        var install = ResolveInstall(args, builder.Configuration);

        // Scope the cache to the install, so a PTU channel or a simulated install never
        // blends its sessions into the LIVE totals.
        var database = builder.Configuration["Database"]
            ?? SessionStore.DatabasePathFor(install?.RootPath);

        var library = new LogLibrary(database);

        builder.Services.AddSingleton(library);

        // The wipe is read before anything asks the library a question, so no
        // page can render pre-wipe totals in the moment before it is applied.
        var wipes = new WipeStore();
        builder.Services.AddSingleton(wipes);
        library.Wipe = wipes.Current;


        // Only when there is one. Registering a null instance throws
        // "Value cannot be null. (Parameter 'implementationInstance')" out of
        // the container - which is how a missing install used to take the
        // whole dashboard down instead of showing the page that explains it.
        if (install is not null)
            builder.Services.AddSingleton(install);
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
        builder.Services.AddSingleton<UexFeeds>();
        builder.Services.AddSingleton<JobStore>();
        builder.Services.AddSingleton<TripStore>();
        builder.Services.AddSingleton<UpdateStore>();
        builder.Services.AddSingleton<UpdateCheck>();


        builder.Services.AddSingleton<OverlayLayoutStore>();

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

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = files,
                OnPrepareResponse = MustRevalidate,
            });
        }
        else if (EmbeddedWeb.HasFiles)
        {
            // Single-file build: no directory sits beside the executable, so the
            // UI is served out of the assembly instead.
            EmbeddedWeb.Map(app);
        }

        app.MapHub<LiveHub>("/hub/live");

        // Without this the browser applies its own guess at how long a file
        // stays fresh, and an update arrives half-applied: the version number
        // comes from the API and reads new, while the page around it is the old
        // stylesheet and the old script. Reported as "I updated and the map has
        // not changed", and the only cure was knowing to press Ctrl+F5.
        //
        // no-cache does not mean "do not cache" - it means "ask first". The tag
        // is still sent, the answer is still 304, and the whole conversation is
        // a loopback round trip. Correctness is worth a millisecond.
        static void MustRevalidate(StaticFileResponseContext context) =>
            context.Context.Response.Headers.CacheControl = "no-cache";

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

        // Pointing the app at a folder by hand, for when no amount of
        // scanning finds one. Takes effect on the next start, because the
        // install is resolved once and everything downstream holds it.
        app.MapGet("/api/install/path", () => new
        {
            saved = InstallPathStore.Load(),
            detected = GameInstallLocator.Discover().Select(i => new { i.Channel, i.RootPath }),
            fromLauncher = GameInstallLocator.FromLauncherLog().Select(i => new { i.Channel, i.RootPath })
        });

        app.MapPost("/api/install/path", (InstallPathRequest body) =>
        {
            if (string.IsNullOrWhiteSpace(body.Path))
            {
                InstallPathStore.Save(null);
                return Results.Ok(new { cleared = true });
            }

            var resolved = InstallPathStore.Save(body.Path);

            return resolved is null
                ? Results.BadRequest(new
                {
                    message = "No Star Citizen logs there. Pick the folder holding Game.log "
                        + @"or its parent - usually ...\StarCitizen\LIVE."
                })
                : Results.Ok(new { resolved.Channel, resolved.RootPath, restartNeeded = true });
        });

        // First-run marker: the dashboard shows its setup screen until this
        // file exists. A file rather than a database row so wiping the cache
        // to rescan does not resurrect the wizard.
        var setupPath = Core.AppPaths.In("setup-done");

        app.MapGet("/api/setup", () =>
        {
            if (File.Exists(setupPath))
                return new { done = true };

            // A folder that was already in use is not a first flight. The
            // marker arrived with the wizard, so without this every existing
            // user is greeted by "reading your logs for the first time" over a
            // dashboard full of their own history. Writing the marker settles
            // it rather than re-deciding on every start.
            if (!establishedInstall)
                return new { done = false };

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(setupPath)!);
                File.WriteAllText(setupPath, DateTimeOffset.UtcNow.ToString("O"));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Failing to write it only costs one more check next time.
            }

            return new { done = true };
        });

        app.MapPost("/api/setup/done", () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(setupPath)!);
            File.WriteAllText(setupPath, DateTimeOffset.UtcNow.ToString("O"));
            return Results.Ok(new { done = true });
        });

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

        // What the widget shows. Read by the overlay itself on every load, so
        // a change made in the dashboard reaches the other window.
        app.MapGet("/api/overlay/layout", (OverlayLayoutStore layout) => new
        {
            current = layout.Current,
            tabs = OverlayLayout.SelectableTabs,
            cards = OverlayLayout.SelectableCards,
            reloadToken = layout.ReloadToken
        });

        app.MapPost("/api/overlay/layout", (OverlayLayoutStore store, OverlayLayout body) =>
            Results.Ok(store.Save(body)));

        app.MapPost("/api/overlay/reload", (OverlayLayoutStore store) =>
            Results.Ok(new { reloadToken = store.RequestReload() }));

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

        // ---- update checks: asked for once, never on a timer ----

        // Local state only. Answering this question costs no network, so the
        // page can decide whether to ask without connecting to anything.
        app.MapGet("/api/updates", (UpdateStore updates) =>
        {
            var preference = updates.Current;

            return new
            {
                preference.Asked,
                preference.Automatic,
                preference.LastCheckedAt,
                preference.LastSeenVersion,
                releases = UpdateCheck.ReleasesPage
            };
        });

        app.MapPost("/api/updates/answer", (bool automatic, UpdateStore updates) =>
            Results.Ok(updates.Answer(automatic)));

        // The one call that reaches the internet, and only from a click or from
        // a startup the player has already agreed to.
        app.MapPost("/api/updates/check", async (UpdateStore updates, UpdateCheck check) =>
        {
            var assembly = typeof(ServerHost).Assembly;
            var current = assembly.GetName().Version?.ToString(3) ?? "0.0.0";

            var result = await check.LookAsync(current);
            updates.Checked(result.Latest);

            return Results.Ok(result);
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

        // The logbook proper: the latest things the pilot did, one merged
        // timeline from every dated record the library keeps - sessions,
        // trades and purchases, first-seen pickups.
        app.MapGet("/api/logbook", (LogLibrary lib, int? days, int? limit) =>
        {
            var cutoff = (days ?? 0) > 0 ? DateTimeOffset.UtcNow.AddDays(-days!.Value) : DateTimeOffset.MinValue;
            var entries = new List<LogbookLine>();

            foreach (var s in lib.Sessions())
            {
                if (s.StartedAt < cutoff)
                    continue;

                var detail = $"{(int)s.Duration.TotalHours}h {s.Duration.Minutes:D2}m"
                    + $" · {s.Jumps.Count} jump{(s.Jumps.Count == 1 ? "" : "s")}"
                    + (s.Deaths > 0 ? $" · {s.Deaths} death{(s.Deaths == 1 ? "" : "s")}" : "");

                entries.Add(new LogbookLine(
                    s.StartedAt, "session",
                    s.PrimaryShip is { Length: > 0 } ship ? $"Session aboard {ship}" : "Session on foot",
                    s.LastLocation ?? "", detail, null));
            }

            foreach (var l in lib.Ledger(days ?? 0))
            {
                // The kiosk logs one line per order, and the order can be for
                // several: "quantity[2] client_price[168000]" is two drives at
                // 84,000, not one at 168,000. Without the count on the line,
                // buying two of something twice reads as two purchases of one,
                // and the price looks doubled rather than the order being.
                // Cargo already carries its SCU in the text.
                var what = l.Kind == "Item bought" && l.Quantity > 1
                    ? $"{l.What} ×{l.Quantity}"
                    : l.What;

                entries.Add(new LogbookLine(
                    l.At,
                    l.Amount > 0 ? "sold" : "bought",
                    what, l.Where, l.Shop, l.Amount));
            }

            foreach (var p in lib.Pickups(days ?? 0))
                entries.Add(new LogbookLine(p.At, "loot", p.Item, p.Place, "first seen", null));

            return entries
                .OrderByDescending(e => e.At)
                .Take(Math.Clamp(limit ?? 150, 1, 500));
        });

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
                t.PlaceId,
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
        app.MapGet("/api/assets", (LogLibrary lib, UexData uex, UexFeeds feeds) =>
        {
            var stats = lib.Stats();

            var fleet = stats.Ships.Select(s => new
            {
                s.Name,
                price = uex.VehiclePrice(s.Reference?.Name ?? s.Name),

                // A ship you can rent is a ship that might be a rental; the
                // page says so rather than deciding for the player.
                rental = feeds.CheapestRental(s.Reference?.Name ?? s.Name)
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
        app.MapGet("/api/contracts", (LogLibrary lib, int? days) => lib.Contracts(days ?? 0));

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

        // ---- reference catalogues: pure information display, no player ----
        // ---- data - what the community digest describes, priced by UEX ----

        app.MapGet("/api/reference/ships", (LogLibrary lib, UexData uex, UexFeeds feeds) =>
            lib.Community.Ships.Values
                .GroupBy(s => s.Name)
                .Select(g => g.First())
                .Select(s => new
                {
                    rental = feeds.CheapestRental(s.Name),
                    s.Name,
                    s.Career,
                    s.Role,
                    s.Crew,
                    s.IsSpaceship,
                    s.ExpeditedCost,
                    s.StandardClaimTime,
                    s.CargoScu,
                    s.ScmSpeed,
                    s.MaxSpeed,
                    s.ShieldHp,
                    s.Health,
                    price = uex.VehiclePrice(s.Name)
                })
                .OrderBy(s => s.Name));

        // Manufacturer codes to full names, for every page that shows a maker.
        app.MapGet("/api/manufacturers", (LogLibrary lib) => lib.Community.Manufacturers);

        // Every crafting blueprint, with the crafted item's shop price joined
        // by uuid - so crafting can be weighed against just buying one.
        app.MapGet("/api/reference/blueprints", (LogLibrary lib, UexData uex) =>
        {
            // The toast names a blueprint loosely ("Defiant"), the catalogue
            // names its output fully ("Defiant Ballistic Repeater"), so the
            // join is a contains either way round rather than an equality.
            var received = lib.Blueprints();

            return lib.Community.Blueprints.Select(b =>
            {
                var mine = received.FirstOrDefault(r =>
                    b.Output.Contains(r.Name, StringComparison.OrdinalIgnoreCase)
                    || r.Name.Contains(b.Output, StringComparison.OrdinalIgnoreCase));

                return new
                {
                    b.Output,
                    b.Type,
                    b.Grade,
                    b.Kind,
                    b.CraftSeconds,
                    b.Materials,
                    @default = b.Default,
                    b.RewardPools,
                    shopPrice = uex.ItemPrice(b.OutputUuid),
                    owned = mine is not null,
                    receivedAt = mine?.At
                };
            });
        });

        // What the logs say you were given, whether or not the catalogue
        // recognises the name.
        app.MapGet("/api/blueprints/owned", (LogLibrary lib) => lib.Blueprints());

        // The starmap's own paragraph about one place, for the map detail card.
        app.MapGet("/api/map/lore", (LogLibrary lib, string name) =>
            lib.Community.PlaceLore(name) is { } lore
                ? Results.Ok(new { lore })
                : Results.NotFound());

        // The game's own deposit spawn tables, with UEX's best sell joined on
        // resources that are also commodities - what to mine AND what it pays.
        app.MapGet("/api/reference/resources", (LogLibrary lib, UexData uex, UexFeeds feeds) =>
        {
            // Raw ore is listed under its own name ("Quartz (Raw)"), so the
            // join tries the decorated form the raw tables actually use.
            var raw = feeds.RawOrePrices
                .GroupBy(r => r.Commodity, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.MaxBy(r => r.Sell)!, StringComparer.OrdinalIgnoreCase);

            var refineries = feeds.RefineryYields
                .GroupBy(r => r.Commodity, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Yield).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            return lib.Community.ResourceSpawns.Select(s =>
            {
                var best = uex.Best(s.Resource);
                raw.TryGetValue($"{s.Resource} (Raw)", out var rawRow);
                rawRow ??= raw.GetValueOrDefault(s.Resource);

                var refinery = refineries.GetValueOrDefault($"{s.Resource} (Ore)")
                    ?? refineries.GetValueOrDefault(s.Resource);
                var bestRefinery = refinery?.FirstOrDefault();

                return new
                {
                    s.Resource,
                    s.Deposit,
                    s.Kind,
                    s.Location,
                    s.System,
                    s.Group,
                    s.GroupChance,
                    s.Share,
                    bestSell = best?.BestSell > 0 ? best.BestSell : (decimal?)null,
                    bestSellTerminal = best?.BestSell > 0 ? best.BestSellTerminal : null,
                    rawSell = rawRow?.Sell,
                    rawTerminal = rawRow?.Terminal,
                    refineryYield = bestRefinery?.Yield,
                    refineryTerminal = bestRefinery?.Terminal
                };
            });
        });

        app.MapGet("/api/reference/items", (LogLibrary lib, UexData uex) =>
            lib.Community.Items
                .Select(kv =>
                {
                    var stock = uex.ItemMarket(kv.Value.Uuid);
                    var cheapest = stock.Count > 0 ? stock.MinBy(r => r.Buy) : null;

                    return new
                    {
                        className = kv.Key,
                        kv.Value.Name,
                        kv.Value.Type,
                        kv.Value.SubType,
                        kv.Value.Size,
                        kv.Value.Grade,
                        kv.Value.Manufacturer,
                        price = uex.ItemPrice(kv.Value.Uuid),
                        stockedAt = stock.Count,
                        cheapestAt = cheapest?.Terminal,
                        terminals = stock.Count > 0
                            ? stock.OrderBy(r => r.Buy).Select(r => $"{r.Terminal} — {r.Buy:N0} aUEC")
                            : null
                    };
                })
                .OrderBy(i => i.className));

        // Hauls worth flying, sized to a hold and a wallet the caller names.
        // Each end of a haul carries the map's own id for it where the terminal
        // could be matched, so planning a run puts real dots on the map instead
        // of the page guessing at the names a second time.
        app.MapGet("/api/routes", (LogLibrary lib, UexData uex, double? scu, decimal? capital, string? from) =>
            uex.Routes(scu ?? 0, capital ?? 0, from, 30).Select(r => new
            {
                r.Commodity,
                r.BuyAt,
                buyAtId = lib.Terminals.IdFor(r.BuyAt),
                r.BuyPrice,
                r.SellAt,
                sellAtId = lib.Terminals.IdFor(r.SellAt),
                r.SellPrice,
                r.MarginPerScu,
                r.Units,
                r.Profit,
                r.Outlay,
                r.LimitedBy
            }));

        // Where the player last woke, for the Now card. Its own endpoint
        // because the casualties page recomputes every statistic to answer,
        // and the dashboard's first paint should not pay for that.
        app.MapGet("/api/respawn", (LogLibrary lib) =>
        {
            var respawns = lib.Respawns();
            var beds = lib.MedicalBeds();

            // Two different signals, shown side by side rather than ranked:
            // a bed is where regen gets set but is also just where someone
            // healed, and waking somewhere is proof of nothing but the past.
            var bed = beds.Count > 0
                ? new { beds[0].Place, beds[0].At, times = beds.Count }
                : null;

            if (respawns.Count == 0)
                return Results.Ok(new { known = bed is not null, bed });

            var latest = respawns[0];

            // How settled the answer is: the same place for the last few
            // deaths reads as a regen point, a different one every time reads
            // as coincidence, and the card says which.
            var recent = respawns.Take(4).ToList();
            var agreeing = recent.Count(r =>
                string.Equals(r.Place, latest.Place, StringComparison.OrdinalIgnoreCase));

            return Results.Ok(new
            {
                known = true,
                latest.Place,
                latest.At,
                latest.Cause,
                agreeing,
                of = recent.Count,
                settled = agreeing >= 2,
                bed
            });
        });

        // What dying has cost: deaths and incapacitations over time, where they
        // happened, and the claim fees the fleet implies.
        app.MapGet("/api/casualties", (LogLibrary lib, int? days) =>
        {
            var stats = lib.Stats(days ?? 0);
            var sessions = lib.Sessions()
                .Where(s => (days ?? 0) == 0 || s.StartedAt >= DateTimeOffset.UtcNow.AddDays(-days!.Value))
                .ToList();

            // The place a session ended is the closest the logs come to naming
            // where things went wrong; a session with no death names nowhere.
            var byPlace = sessions
                .Where(s => s.Deaths > 0 && s.LastLocation is { Length: > 0 })
                .GroupBy(s => s.LastLocation!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { place = g.Key, deaths = g.Sum(s => s.Deaths) })
                .OrderByDescending(x => x.deaths)
                .Take(15);

            var byShip = sessions
                .Where(s => s.Deaths > 0 && s.PrimaryShip is { Length: > 0 })
                .GroupBy(s => s.PrimaryShip!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { ship = g.Key, deaths = g.Sum(s => s.Deaths) })
                .OrderByDescending(x => x.deaths)
                .Take(15);

            var timeline = sessions
                .Where(s => s.Deaths > 0 || s.Incapacitations > 0)
                .OrderByDescending(s => s.StartedAt)
                .Take(60)
                .Select(s => new
                {
                    at = s.StartedAt,
                    s.Deaths,
                    s.Incapacitations,
                    place = s.LastLocation,
                    ship = s.PrimaryShip
                });

            // What a claim costs is a property of the ship, so the exposure is
            // the fleet's own expedite fees rather than one blanket number.
            var fees = stats.Ships
                .Select(s => new { s.Name, fee = s.Reference?.ExpeditedCost ?? 0 })
                .Where(s => s.fee > 0)
                .OrderByDescending(s => s.fee)
                .ToList();

            var deaths = sessions.Sum(s => s.Deaths);

            // Where the player woke after dying: the closest thing to a
            // respawn point the logs allow, and an inference rather than a
            // reading - the UI says so.
            var respawns = lib.Respawns(days ?? 0);

            var wokeAt = respawns
                .GroupBy(r => r.Place, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { place = g.Key, times = g.Count(), last = g.Max(r => r.At) })
                .OrderByDescending(x => x.times)
                .ThenByDescending(x => x.last)
                .Take(10)
                .ToList();

            // The other signal: beds are where regen is set, when it is set.
            var beds = lib.MedicalBeds(days ?? 0);

            var bedsUsed = beds
                .GroupBy(b => b.Place, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { place = g.Key, times = g.Count(), last = g.Max(b => b.At) })
                .OrderByDescending(x => x.last)
                .Take(10)
                .ToList();

            return new
            {
                deaths,
                lastWokeAt = respawns.Count > 0 ? respawns[0].Place : null,
                lastWokeWhen = respawns.Count > 0 ? respawns[0].At : (DateTimeOffset?)null,
                lastBedAt = beds.Count > 0 ? beds[0].Place : null,
                lastBedWhen = beds.Count > 0 ? beds[0].At : (DateTimeOffset?)null,
                wokeAt,
                bedsUsed,
                incapacitations = sessions.Sum(s => s.Incapacitations),
                sessionsWithDeaths = sessions.Count(s => s.Deaths > 0),
                averageFee = fees.Count > 0 ? fees.Average(f => f.fee) : 0,
                estimatedFees = fees.Count > 0 ? deaths * fees.Average(f => f.fee) : 0,
                byPlace,
                byShip,
                timeline,
                fees
            };
        });

        // Replacing a kit: which shop carries the most of what you were
        // wearing, and what the trip costs.
        app.MapGet("/api/outfitting", (LogLibrary lib, UexData uex, UexFeeds feeds) =>
        {
            var worn = lib.Stats().Loadout
                .SelectMany(slot => slot.Items)
                .Select(i => new { i.Name, uuid = i.Reference?.Uuid })
                .Where(i => i.uuid is not null)
                .DistinctBy(i => i.uuid)
                .ToList();

            // Terminal to what it stocks of this kit, and for how much.
            var shops = new Dictionary<string, List<(string Item, decimal Price)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in worn)
                foreach (var row in uex.ItemMarket(item.uuid))
                {
                    if (!shops.TryGetValue(row.Terminal, out var carried))
                        shops[row.Terminal] = carried = [];

                    if (!carried.Any(c => c.Item == item.Name))
                        carried.Add((item.Name, row.Buy));
                }

            var ranked = shops
                .Select(s => new
                {
                    terminal = s.Key,
                    covers = s.Value.Count,
                    total = s.Value.Sum(i => i.Price),
                    items = s.Value.OrderByDescending(i => i.Price).Select(i => new { i.Item, i.Price })
                })
                .OrderByDescending(s => s.covers)
                .ThenBy(s => s.total)
                .Take(10);

            return new
            {
                kitSize = worn.Count,
                priced = worn.Count(w => uex.ItemPrice(w.uuid) is > 0),
                cheapest = worn.Sum(w => uex.ItemPrice(w.uuid) ?? 0),
                shops = ranked
            };
        });

        // ---- jobs: the player's own plans, checked against what they hold ----

        app.MapGet("/api/jobs", (JobStore jobs, LogLibrary lib, UexData uex) =>
        {
            var stats = lib.Stats();

            // Where each held thing is. Stash listings are per location and
            // record presence, not counts - removals are never logged - so a
            // job can say WHERE something is but never how many.
            var held = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var place in stats.Stash)
                foreach (var group in place.Groups)
                    foreach (var item in group.Items)
                    {
                        if (!held.TryGetValue(item.Name, out var places))
                            held[item.Name] = places = [];

                        if (!places.Contains(place.Name, StringComparer.OrdinalIgnoreCase))
                            places.Add(place.Name);
                    }

            var worn = stats.Loadout
                .SelectMany(slot => slot.Items)
                .Select(i => i.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return jobs.All().Select(job =>
            {
                var lines = job.Items.Select(item =>
                {
                    // Held: an exact stash name, else anything containing it -
                    // "Hadanite" should find "Hadanite (Raw)".
                    var where = held.TryGetValue(item.Name, out var exact)
                        ? exact
                        : held.Where(h => h.Key.Contains(item.Name, StringComparison.OrdinalIgnoreCase))
                            .SelectMany(h => h.Value)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                    // Where to get what is missing: a commodity has a cheapest
                    // terminal, an item has a shop.
                    var commodity = uex.Best(item.Name);
                    var buyPrice = commodity?.BestBuy > 0 ? commodity.BestBuy : (decimal?)null;
                    var buyAt = commodity?.BestBuy > 0 ? commodity.BestBuyTerminal : null;

                    if (buyPrice is null && MatchItem(lib, item.Name) is { Uuid: { } uuid })
                    {
                        buyPrice = uex.ItemPrice(uuid);

                        // A price with no counter behind it is half an answer:
                        // the card said what a shield costs and left "where"
                        // blank, which is the only part you can act on. The
                        // same loose match the shopping lookup uses, so a line
                        // reading "Hydro Jet" still finds the HydroJet.
                        buyAt = uex.ItemMarket(uuid).MinBy(r => r.Buy)?.Terminal;
                    }

                    return new
                    {
                        item.Name,
                        item.Needed,
                        item.Unit,
                        have = where.Count > 0 || worn.Contains(item.Name),
                        where,
                        wornNow = worn.Contains(item.Name),
                        buyPrice,
                        buyAt
                    };
                }).ToList();

                return new
                {
                    job.Id,
                    job.Title,
                    job.Destination,
                    job.DestinationId,
                    job.Kind,
                    job.Source,
                    job.CreatedAt,
                    job.Done,
                    job.Pinned,
                    items = lines,
                    haveCount = lines.Count(l => l.have),
                    totalCount = lines.Count
                };
            });
        });

        app.MapPost("/api/jobs", (JobStore jobs, JobRequest body) =>
            Results.Ok(jobs.Add(
                body.Title ?? "Untitled job",
                body.Kind ?? "list",
                body.Source,
                body.Items ?? [],
                body.Destination,
                body.DestinationId)));

        // Where a list is to be shopped. Cleared by sending nothing, which is
        // a real answer: a list for wherever you happen to be.
        app.MapPost("/api/jobs/{id}/destination", (string id, JobStore jobs, DestinationRequest body) =>
            jobs.SetDestination(id, body.Place, body.PlaceId)
                ? Results.Ok(new { id, body.Place })
                : Results.NotFound());

        // One thing added from a catalogue page, into whichever list the
        // player is currently filling.
        app.MapPost("/api/jobs/collect", (JobStore jobs, JobItem body) =>
        {
            var job = jobs.Collect(body);
            return Results.Ok(new { job.Id, job.Title });
        });

        app.MapPost("/api/jobs/{id}/toggle", (string id, JobStore jobs) =>
            jobs.Toggle(id) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapPost("/api/jobs/{id}/pin", (string id, JobStore jobs) =>
            jobs.TogglePin(id) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapDelete("/api/jobs/{id}", (string id, JobStore jobs) =>
            jobs.Remove(id) ? Results.Ok(new { id }) : Results.NotFound());

        // ---- flight plans: where to go next, in order ----

        // ---- the wipe: where the player's countable history begins ----

        app.MapGet("/api/wipe", (WipeStore wipes, LogLibrary lib) => Describe(wipes.Current, lib));

        app.MapPost("/api/wipe", (WipeRequest body, WipeStore wipes, LogLibrary lib) =>
        {
            var wipe = wipes.Set(body.At, body.Patch, ScopeOf(body.Covers));
            lib.Wipe = wipe;

            return Results.Ok(Describe(wipe, lib));
        });

        app.MapGet("/api/trips", (TripStore trips) => trips.All());

        app.MapPost("/api/trips", (TripStore trips, TripRequest body) =>
            Results.Ok(trips.Add(body.Title, body.Stops)));

        // One stop added from the map, a route or a list, into whichever plan
        // the player is filling.
        app.MapPost("/api/trips/stops", (TripStore trips, TripStop body) =>
        {
            var trip = trips.AddStop(body);
            return Results.Ok(new { trip.Id, trip.Title, stops = trip.Stops.Count });
        });

        app.MapPost("/api/trips/{id}/track", (string id, TripStore trips) =>
            trips.Track(id) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapPost("/api/trips/{id}/stops/{stopId}/toggle", (string id, string stopId, TripStore trips) =>
            trips.ToggleStop(id, stopId) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapPost("/api/trips/{id}/stops/{stopId}/move", (string id, string stopId, int delta, TripStore trips) =>
            trips.MoveStop(id, stopId, delta) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapDelete("/api/trips/{id}/stops/{stopId}", (string id, string stopId, TripStore trips) =>
            trips.RemoveStop(id, stopId) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapDelete("/api/trips/{id}", (string id, TripStore trips) =>
            trips.Remove(id) ? Results.Ok(new { id }) : Results.NotFound());

        // ---- optional UEX feeds, each switched on by itself ----

        app.MapGet("/api/uex/feeds", (UexFeeds feeds) =>
            UexFeeds.All.Select(f => new
            {
                f.Key,
                f.Title,
                f.Description,
                f.Cost,
                enabled = feeds.IsEnabled(f.Key),
                fetchedAt = feeds.FetchedAt(f.Key)
            }));

        app.MapPost("/api/uex/feeds/{key}/enable",
            async (string key, UexFeeds feeds, IHttpClientFactory httpFactory) =>
            {
                try
                {
                    var count = await feeds.EnableAsync(key, httpFactory.CreateClient("community"));
                    return Results.Ok(new { key, enabled = true, rows = count });
                }
                catch (ArgumentException)
                {
                    return Results.NotFound(new { message = $"No UEX feed called '{key}'." });
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException or JsonException)
                {
                    return Results.Problem(
                        title: $"The UEX {key} feed could not be fetched.",
                        detail: e.Message,
                        statusCode: 502);
                }
            });

        app.MapPost("/api/uex/feeds/{key}/disable", (string key, UexFeeds feeds) =>
        {
            feeds.Disable(key);
            return Results.Ok(new { key, enabled = false });
        });

        // What each feed knows, for the pages that show it.
        app.MapGet("/api/uex/rentals", (UexFeeds feeds) => feeds.RentalPrices);
        app.MapGet("/api/uex/fuel", (UexFeeds feeds) => feeds.FuelPrices);
        app.MapGet("/api/uex/refineries", (UexFeeds feeds) => feeds.RefineryYields);
        app.MapGet("/api/uex/raw-prices", (UexFeeds feeds) => feeds.RawOrePrices);
        app.MapGet("/api/uex/places", (UexFeeds feeds) => feeds.PlaceDirectory);

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
        /*
         * What can be bolted onto one of your ships, and where it is sold.
         *
         * The ship data carries every port with the rule for what may replace
         * what is in it, so this is the game's own answer rather than a guess:
         * a size 2 shield port takes a size 2 shield, and the shops that stock
         * one are known. Ports nobody sells parts for come back empty and are
         * dropped, so the page shows what can actually be shopped for today.
         */
        app.MapGet("/api/fleet/upgrades", (LogLibrary lib, UexData uex, string ship) =>
        {
            var slots = lib.Community.Slots(ship);

            if (slots.Count == 0)
                return Results.Ok(new
                {
                    ship,

                    // Told apart on purpose: an install whose reference data
                    // predates ports needs a refresh, which is a different
                    // sentence from "this ship has nothing to change".
                    known = lib.Community.HasSlots,
                    groups = Array.Empty<object>()
                });

            // Everything sold, by what it is and how big: one pass over the
            // catalogue rather than one per port.
            var catalogue = lib.Community.Items.Values
                .Where(i => i.Uuid is not null && i.Type is { Length: > 0 })
                .GroupBy(i => (i.Type!, i.Size))
                .ToDictionary(g => g.Key, g => g.ToList());

            var groups = slots
                .GroupBy(s => (s.Kind, s.Size))
                .Select(group =>
                {
                    var fitted = group
                        .Select(s => s.Fitted)
                        .Where(f => f is { Length: > 0 })
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var options = (catalogue.TryGetValue(group.Key, out var candidates) ? candidates : [])
                        .Select(item => new
                        {
                            item.Name,
                            item.Manufacturer,
                            item.Grade,
                            price = uex.ItemPrice(item.Uuid),
                            shops = uex.ItemMarket(item.Uuid)
                                .GroupBy(r => r.Terminal, StringComparer.OrdinalIgnoreCase)
                                .Select(g => g.MinBy(r => r.Buy)!)
                                .OrderBy(r => r.Buy)
                                .Take(4)
                                .Select(r =>
                                {
                                    var place = lib.Terminals.Resolve(r.Terminal);

                                    return new
                                    {
                                        terminal = r.Terminal,
                                        placeId = place?.RawId ?? string.Empty,
                                        place = place?.Name,
                                        system = place?.System,
                                        security = TerminalPlaces.SecurityOfSystem(place?.System),
                                        price = r.Buy
                                    };
                                })
                                .ToList()
                        })

                        // Nothing to buy is not an upgrade: an item with no
                        // shop behind it would send the player nowhere.
                        .Where(o => o.Name is { Length: > 0 } && o.shops.Count > 0)
                        .OrderBy(o => o.price ?? decimal.MaxValue)
                        .Take(12)
                        .ToList();

                    return new
                    {
                        kind = group.Key.Kind,
                        size = group.Key.Size,
                        ports = Holes(group),
                        fitted,
                        options
                    };
                })
                .Where(g => g.options.Count > 0)
                .OrderBy(g => g.kind)
                .ThenBy(g => g.size)
                .ToList();

            return Results.Ok(new { ship, known = true, groups });
        });

        /*
         * Everything a shopping list can be written from.
         *
         * A list line is free text and stays free text - the player knows what
         * they want better than this app does - but making them spell
         * "Quantanium" or "FR-76 Chest Armor" from memory is asking them to
         * guess at names already sitting in the reference data. Only things
         * that can actually be bought are offered: a name with no counter
         * behind it would put a line on the list that no plan could ever
         * route.
         */
        app.MapGet("/api/shopping/catalogue", (LogLibrary lib, UexData uex) =>
        {
            var commodities = lib.Community.All.Values
                .Select(c => c.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = lib.Community.Items.Values
                .Where(i => i.Name is { Length: > 0 } && uex.ItemPrice(i.Uuid) is > 0)
                .Select(i => i.Name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new { commodities, items };
        });

        /*
         * Where to buy one named thing - whatever kind of thing it is.
         *
         * A shopping list is written by hand, so a line on it can be a
         * commodity ("Agricium"), a ship part ("Atlas Quantum Drive"), or a
         * typo. The trade market and the item shops are two different feeds
         * with two different join keys, and the list should not have to know
         * which one its line belongs to - so both are tried here and the
         * answer says which one replied.
         */
        app.MapGet("/api/shopping/sellers", (LogLibrary lib, UexData uex, string name) =>
        {
            object Row(string kind, string terminal, decimal price, decimal scu, DateTimeOffset? seen)
            {
                var place = lib.Terminals.Resolve(terminal);

                return new
                {
                    kind,
                    terminal,
                    placeId = place?.RawId ?? string.Empty,
                    place = place?.Name,
                    system = place?.System,
                    security = TerminalPlaces.SecurityOfSystem(place?.System),
                    price,
                    scu,
                    seenAt = seen
                };
            }

            var traded = uex.Market(name)
                .Where(r => r.Buy > 0)
                .OrderBy(r => r.Buy)
                .Select(r => Row("commodity", r.Terminal, r.Buy, r.BuyScu,
                    r.Seen > 0 ? DateTimeOffset.FromUnixTimeSeconds(r.Seen) : null))
                .ToList();

            if (traded.Count > 0)
                return new { name, kind = "commodity", sellers = (IReadOnlyList<object>)traded };

            // Items join on the game's entity uuid, which only the reference
            // data knows, so the written name has to find the item first.
            var item = MatchItem(lib, name);

            if (item is null)
                return new { name, kind = "unknown", sellers = (IReadOnlyList<object>)Array.Empty<object>() };

            var stocked = uex.ItemMarket(item.Uuid)
                .GroupBy(r => r.Terminal, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.MinBy(r => r.Buy)!)
                .OrderBy(r => r.Buy)
                .Select(r => Row("item", r.Terminal, r.Buy, 0, null))
                .ToList();

            return new { name = item.Name ?? name, kind = "item", sellers = (IReadOnlyList<object>)stocked };
        });

        // Every counter that trades one commodity, with where it is and how
        // rough the neighbourhood is - so the page can offer a choice rather
        // than one number, and say what taking the best price would cost.
        app.MapGet("/api/uex/market", (LogLibrary lib, UexData uex, string commodity) =>
            uex.Market(commodity).Select(r =>
            {
                var place = lib.Terminals.Resolve(r.Terminal);

                return new
                {
                    terminal = r.Terminal,

                    // The map's own id for the place this counter stands in,
                    // empty when the two naming schemes cannot be reconciled.
                    // The page shades and plans by this rather than matching
                    // names itself.
                    placeId = place?.RawId ?? string.Empty,
                    place = place?.Name,
                    system = place?.System,
                    security = TerminalPlaces.SecurityOfSystem(place?.System),
                    buy = r.Buy,
                    sell = r.Sell,
                    buyScu = r.BuyScu,
                    sellScu = r.SellScu,
                    seenAt = r.Seen > 0 ? DateTimeOffset.FromUnixTimeSeconds(r.Seen) : (DateTimeOffset?)null
                };
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

        app.MapGet("/api/stash", (LogLibrary lib, bool? everSeen) => lib.Stash(everSeen ?? false));

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

    /// <summary>
/// The wipe as the page reads it: names rather than a flags number, and the
/// count of what it is holding back, which is the honest part.
/// </summary>
static object Describe(Wipe wipe, LogLibrary lib) => new
{
    at = wipe.At == DateTimeOffset.MinValue ? (DateTimeOffset?)null : wipe.At,
    wipe.Patch,

    // A patch that landed after the line currently drawn. The page offers it
    // and the player decides: the logs can date a patch, but nothing in them
    // says whether it wiped.
    suggested = lib.PatchSinceWipe() is { } found
        ? new { patch = $"Alpha {found.Patch}", at = found.At }
        : null,
    covers = Enum.GetValues<WipeScope>()
        .Where(v => v is not (WipeScope.None or WipeScope.Everything) && wipe.Scope.HasFlag(v))
        .Select(v => v.ToString().ToLowerInvariant())
        .ToArray(),
    hidden = lib.SessionsBeforeWipe(),
    stored = lib.Store.Count(),
    @default = WipeStore.Default.At
};

/// <summary>
/// What the page said the wipe took.
/// </summary>
/// <remarks>
/// Unknown names are ignored rather than refused: a scope this build does not
/// have is a page from a newer one, and the sensible reading of "money and
/// something I have never heard of" is money.
/// </remarks>
static WipeScope ScopeOf(List<string>? covers)
{
    if (covers is null || covers.Count == 0)
        return WipeScope.Everything;

    var scope = WipeScope.None;

    foreach (var name in covers)
        if (Enum.TryParse<WipeScope>(name, ignoreCase: true, out var one))
            scope |= one;

    return scope == WipeScope.None ? WipeScope.Everything : scope;
}

/// <summary>
/// The reference item a written line means, or null.
/// </summary>
/// <remarks>
/// Hand-written names are close, not exact: "Atlas quantum drive" for "Atlas",
/// a manufacturer word in front, a plural on the end. An exact name wins; then
/// a whole-word containment either way, longest first, so "Bulwark" does not
/// beat "Bulwark Mk2" for a line naming the Mk2. A guess this loose is only
/// safe because the answer is shown with its price and shop: the player sees
/// what was matched before flying anywhere.
/// </remarks>
static ItemInfo? MatchItem(LogLibrary lib, string written)
{
    var wanted = Compact(written);
    if (wanted.Length < 3)
        return null;

    ItemInfo? best = null;
    var bestLength = 0;

    foreach (var item in lib.Community.Items.Values)
    {
        if (item.Name is not { Length: > 0 } name || item.Uuid is null)
            continue;

        var compact = Compact(name);
        if (compact.Length == 0)
            continue;

        if (compact == wanted)
            return item;

        if (compact.Length > bestLength
            && (compact.Contains(wanted, StringComparison.Ordinal) || wanted.Contains(compact, StringComparison.Ordinal)))
        {
            best = item;
            bestLength = compact.Length;
        }
    }

    return best;

    static string Compact(string value) =>
        new([.. value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}

/// <summary>
/// How many holes of one kind and size a ship really has.
/// </summary>
/// <remarks>
/// Two things stop this being a count of rows. A port that accepts sizes 1 to
/// 3 is three rows and one hole. And a gimbal mount accepts a gun directly or
/// a gimbal that then holds the gun, so the mount and the gun inside it are
/// the same hole offered twice - which is why a Corsair looked like it had
/// twelve size 2 gun ports instead of six. A port whose id extends another
/// port's id is inside it, so only the outermost of each chain is counted.
/// </remarks>
static int Holes(IEnumerable<ShipSlot> slots)
{
    var ports = slots.Select(s => s.Port).Distinct(StringComparer.Ordinal).ToList();

    return ports.Count(port =>
        !ports.Any(other => other != port && port.StartsWith(other + ".", StringComparison.Ordinal)));
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

        // A folder the user pointed us at once beats any amount of guessing.
        if (InstallPathStore.Load() is { } remembered
            && GameInstallLocator.FromPath(remembered) is { } saved)
            return saved;

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

/// <summary>Body of POST /api/install/path. Empty clears the override.</summary>
public sealed record InstallPathRequest(string? Path);

/// <summary>Body of POST /api/jobs.</summary>
public sealed record JobRequest(
    string? Title,
    string? Kind,
    string? Source,
    List<JobItem>? Items,
    string? Destination = null,
    string? DestinationId = null);

/// <summary>Body of POST /api/jobs/{id}/destination. Both null clears it.</summary>
public sealed record DestinationRequest(string? Place, string? PlaceId);


/// <summary>
/// Body of POST /api/wipe. A null date counts everything again, and
/// <paramref name="Covers"/> names what the wipe took - "money", "ships",
/// "inventory", "history" - with an empty list read as all of it.
/// </summary>
public sealed record WipeRequest(DateTimeOffset? At, string? Patch, List<string>? Covers);

/// <summary>Body of POST /api/trips.</summary>
public sealed record TripRequest(string? Title, List<TripStop>? Stops);

/// <summary>One line of the merged logbook timeline.</summary>
public sealed record LogbookLine(
    DateTimeOffset At, string Kind, string What, string Place, string Detail, decimal? Amount);
