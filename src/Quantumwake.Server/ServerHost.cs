using System.Net;
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

        // Every outbound request in the application shares this client: the
        // opt-in community-dataset download, the update check, and the
        // StarStrings release check and download. All of them happen on a
        // click, none of them carry an identifier.
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
        builder.Services.AddSingleton<ChecklistStore>();
        builder.Services.AddSingleton<TripStore>();
        builder.Services.AddSingleton<MapNoteStore>();
        builder.Services.AddSingleton<ExportBuilder>();
        builder.Services.AddSingleton<ImportStore>();
        builder.Services.AddSingleton<UpdateStore>();
        builder.Services.AddSingleton<UpdateCheck>();
        builder.Services.AddSingleton<ShellBridge>();
        builder.Services.AddSingleton<SelfUpdate>();

        // Prices are the one dataset here with a shelf life, so they are the one
        // thing allowed to refetch themselves - and only once asked.
        builder.Services.AddSingleton<TradeDataStore>();
        builder.Services.AddHostedService<TradeDataRefresh>();

        // MrKraken's StarStrings, installed on request. The only thing in the
        // app that writes outside its own data folder.
        builder.Services.AddSingleton<StarStringsStore>();
        builder.Services.AddSingleton<StarStrings>();
        builder.Services.AddSingleton<TextOverlayStore>();
        builder.Services.AddSingleton<TextOverlayService>();



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
        var lan = builder.Configuration.GetValue<bool>("Lan");
        builder.WebHost.UseUrls($"http://{(lan ? "0.0.0.0" : "127.0.0.1")}:{port}");

        var app = builder.Build();
        // The browser on this machine will lend its loopback address to any
        // page it is showing, so a request arriving over loopback still has to
        // prove it was made *for* this app: a Host naming another site is DNS
        // rebinding, and a write declaring a foreign Origin is another page's
        // form. Requests from other machines fall through to the LAN guard
        // below, whose read-only rule already says everything there is to say.
        app.Use(async (context, next) =>
        {
            var remote = context.Connection.RemoteIpAddress;
            var here = remote is null || IPAddress.IsLoopback(remote);

            string? origin = context.Request.Headers.Origin;

            if (!here || OriginGuard.Allows(context.Request.Method, context.Request.Host.Host, origin))
            {
                await next();
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = OriginGuard.Refusal });
        });


        // Opening the dashboard to the LAN opens the API with it, and neither
        // has a login - so from off this machine it is read-only.
        //
        // The point of -Lan is a tablet showing the dashboard, which needs reads
        // and the live feed and nothing else. Everything that changes something
        // is a POST: storing UEX credentials, installing StarStrings into the
        // game directory, moving the wipe line, forcing a rescan. None of that
        // should be reachable by anyone who joins the same wifi.
        //
        // GET, HEAD and OPTIONS pass, as does the SignalR hub - LiveHub declares
        // no callable methods, so it only ever broadcasts outwards. Requests
        // from this machine are untouched, so the app itself is unaffected.
        if (lan)
        {
            app.Use(async (context, next) =>
            {
                var remote = context.Connection.RemoteIpAddress;
                var here = remote is null || IPAddress.IsLoopback(remote);

                if (here || LanGuard.AllowsFromElsewhere(
                        context.Request.Method, context.Request.Path.Value ?? "/"))
                {
                    await next();
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = LanGuard.Refusal });
            });

            app.Logger.LogWarning(
                "Listening on every interface (-Lan). Anyone on this network can read your "
                + "history; changing anything still requires this machine.");
        }

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
        app.MapGet("/api/community", (LogLibrary lib) =>
        {
            // The dump the files came from against the build the logs are on.
            // Both numbers are already to hand - scunpacked stamps its commit,
            // the log header names its build - so the page can say the dataset
            // predates the patch without asking the network anything.
            // The number only: a log's build tag carries the file's own date and
            // time as well, which is noise in a sentence about versions.
            var playing = CommunityData.BuildIn(PlayedBuild(lib));
            var dumped = lib.Community.DumpBuild;

            return new
            {
                enabled = lib.Community.IsEnabled,
                commodities = lib.Community.Count,
                fetchedAt = lib.Community.FetchedAt,
                dump = lib.Community.Dump,
                playing,
                behind = lib.Community.IsEnabled
                         && playing is not null
                         && dumped is not null
                         && !playing.Equals(dumped, StringComparison.Ordinal),
                source = CommunityData.CommoditiesUrl
            };
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
        /*
         * StarStrings: what is installed, what is available, and the two
         * buttons that change it.
         *
         * Read is free and offline; the release check costs one request and is
         * only made when asked, in keeping with everything else here.
         */
        app.MapGet("/api/starstrings", async (StarStringsStore store, StarStrings mod, bool? check) =>
        {
            var installed = store.Current;
            var present = store.StillPresent();
            var latest = check == true ? await mod.LatestAsync() : null;

            return new
            {
                repository = StarStrings.Repository,
                gameRoot = install?.RootPath,

                // "Installed" means the files are still there. A game patch can
                // put the original localisation back without telling anyone.
                installed = present,
                displaced = installed is not null && !present,
                release = installed?.Release,
                installedAt = installed?.InstalledAt,
                publishedAt = installed?.PublishedAt,
                files = installed?.Files.Select(f => f.Path) ?? [],
                latest = latest is null ? null : new
                {
                    latest.Name,
                    latest.PublishedAt,
                    latest.Url
                },
                newer = StarStrings.IsNewer(installed, latest)
            };
        });

        app.MapPost("/api/starstrings/install", async (StarStrings mod) =>
        {
            if (install is not { } game)
                return Results.BadRequest(new { problem = "No Star Citizen install was found to write into." });

            var (done, problem) = await mod.InstallAsync(game);

            return problem is null
                ? Results.Ok(new { done!.Release, done.InstalledAt, files = done.Files.Count })
                : Results.BadRequest(new { problem });
        });

        app.MapPost("/api/starstrings/remove", (StarStrings mod) =>
            Results.Ok(new { removed = mod.Remove() }));

        // Asking what would change writes nothing. Installing is a separate
        // call because the file lands in the player's game folder.
        app.MapGet("/api/textoverlay", (TextOverlayService overlay) => overlay.Status(install));

        app.MapPost("/api/textoverlay/install", (TextOverlayService overlay) =>
        {
            var (done, problem) = overlay.Install(install);

            return problem is null
                ? Results.Ok(new { done!.InstalledAt, done.Marked, done.Layered })
                : Results.BadRequest(new { problem });
        });

        app.MapPost("/api/textoverlay/remove", (TextOverlayService overlay) =>
        {
            overlay.Remove();
            return Results.Ok(new { removed = true });
        });

        app.MapPost("/api/updates/check", async (UpdateStore updates, UpdateCheck check, SelfUpdate selfUpdate) =>

        {
            var assembly = typeof(ServerHost).Assembly;
            var current = assembly.GetName().Version?.ToString(3) ?? "0.0.0";

            var result = await check.LookAsync(current);
            updates.Checked(result.Latest);

            // The page needs to know whether to offer one click or a trip to the
            // browser, and only the server can tell: it depends on how this copy
            // was started, not on what the release carries.
            return Results.Ok(new
            {
                result.Newer, result.Current, result.Latest, result.Url,
                result.Notes, result.PublishedAt,
                canInstall = selfUpdate.Possible && result.Asset is not null,

                // Ninety megabytes is worth knowing before agreeing to it, not
                // after: a metered connection is somebody's actual money.
                downloadBytes = result.Asset?.Size,
            });
        });

        // The whole of the update, from one click: fetch the published file,
        // check it against the hash GitHub published for it, move the running
        // executable aside, put the new one in its place, and restart.
        //
        // A POST, so the LAN rule refuses it from another machine without
        // anything here having to say so - replacing somebody's application over
        // the wifi is exactly the kind of thing that rule exists for.
        app.MapPost("/api/updates/install", async (UpdateStore updates, UpdateCheck check, SelfUpdate selfUpdate) =>
        {
            var assembly = typeof(ServerHost).Assembly;
            var current = assembly.GetName().Version?.ToString(3) ?? "0.0.0";

            // Looked up again rather than taken from the page: what gets
            // installed is decided here, from the feed, not from whatever a
            // request body claims is the newest version.
            var result = await check.LookAsync(current);
            updates.Checked(result.Latest);

            var (installed, refused) = await selfUpdate.InstallAsync(result);

            return refused is not null
                ? Results.Json(new { message = refused.Message }, statusCode: refused.Status)
                : Results.Ok(installed);
        });

        app.MapGet("/api/scan/status", (ScanStatus status) => status.Snapshot());

        app.MapGet("/api/now", (LiveSessionService live) => live.Current);

        // The Now page is a briefing, not another report to go hunting through:
        // one request joins the live place to the player's plan, shopping lists,
        // stash, and opt-in market feeds. The game never writes a cargo manifest,
        // so trade rows are deliberately "buy here, sell there" leads rather than
        // pretending the player is carrying a commodity it cannot see.
        app.MapGet("/api/briefing", (
            LiveSessionService live, TripStore trips, JobStore jobs,
            LogLibrary lib, UexData uex, UexFeeds feeds) =>
            BuildBriefing(live.Current, trips, jobs, lib, uex, feeds));

        // The map reads the same deliberately limited service evidence as the
        // briefing. It receives map ids, not UEX's terminal names, so its
        // filtering and detail card use exactly the resolver the trade views do.
        app.MapGet("/api/map/services", (LogLibrary lib, UexData uex, UexFeeds feeds) =>
            BuildMapServices(lib, uex, feeds));

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
                        .Select(i => uex.ItemPrice(lib.ItemUuid(i.ItemClass)))
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
        // Priced at the endpoint rather than in the library, the same join the
        // stash uses: the item class names a community entry, which carries the
        // uuid UEX prices against.
        //
        // Two sources, in confidence order. A receipt is this install's own -
        // the game charged for it, so it settles both the price and the fact
        // that the thing is sold at all. UEX is broader but crowd-sourced, and
        // misses 29 of the 106 items these logs prove were bought at a kiosk.
        // Neither is a catalogue, so "sold" is a floor and the page says so.
        app.MapGet("/api/loot", (LogLibrary lib, UexData uex, int? days) =>
        {
            var receipts = lib.Receipts();

            return lib.Pickups(days ?? 0).Select(p =>
            {
                var uuid = lib.ItemUuid(p.ItemClass);
                var receipt = receipts.GetValueOrDefault(p.ItemClass);
                var listed = uex.TypicalItemPrice(uuid);

                return new
                {
                    p.At,
                    p.Item,
                    p.ItemClass,
                    p.Place,
                    p.Category,
                    price = listed ?? receipt?.UnitPrice,
                    pricedFrom = listed is not null ? "market" : receipt is not null ? "receipt" : null,
                    sold = uex.ItemMarket(uuid).Count > 0 || receipt is not null
                };
            });
        });
        app.MapGet("/api/contracts", (LogLibrary lib, int? days) => lib.Contracts(days ?? 0));

        // Work done per faction, and the little reputation anyone has written
        // down. See LogLibrary.Standings for why those are two different things.
        app.MapGet("/api/standing", (LogLibrary lib, int? days) => lib.Standings(days ?? 0));

        // Who the party channel has named. See LogLibrary.Wingmen for why these
        // are floors rather than totals.
        app.MapGet("/api/crew", (LogLibrary lib, int? days) => lib.Wingmen(days ?? 0));

        // Its own route rather than a field on the crew rows: a pilot appears
        // once per ship here, so folding it in would either repeat every other
        // count or need the page to flatten it back out.
        app.MapGet("/api/crew/ships", (LogLibrary lib, int? days) => lib.SharedShips(days ?? 0));

        // What the logs are still carrying. Unscoped by wipe on purpose - see
        // LogLibrary.Signals.
        app.MapGet("/api/signals", (LogLibrary lib) => lib.Signals());

        // The community catalogue joined onto this install's trades, plus UEX
        // live prices when that integration is on. Empty until the community
        // dataset is enabled, and the page explains that.
        app.MapGet("/api/market", (LogLibrary lib, UexData uex) =>
            lib.Market(uex).Select(entry => new
            {
                entry.Id,
                entry.Name,
                entry.Groups,
                entry.Sold,
                entry.Bought,
                entry.MyScuSold,
                entry.MyRevenue,
                entry.MyTrades,
                entry.Source,
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

            object Owned(string output) => new
            {
                owned = received.Any(r =>
                    output.Contains(r.Name, StringComparison.OrdinalIgnoreCase)
                    || r.Name.Contains(output, StringComparison.OrdinalIgnoreCase))
            };

            // The install describes the recipe itself; the download adds how a
            // blueprint is obtained, which is not in the game files this reads.
            if (lib.GameCommodities.Blueprints.Count > 0)
            {
                return lib.GameCommodities.Blueprints.Select(b =>
                {
                    var facts = lib.GameCommodities.Item(b.OutputClass);
                    var mine = received.FirstOrDefault(r =>
                        b.Output.Contains(r.Name, StringComparison.OrdinalIgnoreCase)
                        || r.Name.Contains(b.Output, StringComparison.OrdinalIgnoreCase));

                    return (object)new
                    {
                        b.Output,
                        Type = facts?.Type,
                        Grade = facts?.Grade ?? 0,
                        b.Kind,
                        b.CraftSeconds,
                        b.Materials,
                        @default = false,
                        b.RewardPools,
                        shopPrice = uex.ItemPrice(lib.ItemUuid(b.OutputClass)),
                        owned = mine is not null,
                        receivedAt = mine?.At,
                        source = "install"
                    };
                });
            }

            return lib.Community.Blueprints.Select(b =>
            {
                var mine = received.FirstOrDefault(r =>
                    b.Output.Contains(r.Name, StringComparison.OrdinalIgnoreCase)
                    || r.Name.Contains(b.Output, StringComparison.OrdinalIgnoreCase));

                return (object)new
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
                    receivedAt = mine?.At,
                    source = "dataset"
                };
            });
        });

        // What the logs say you were given, whether or not the catalogue
        // recognises the name.
        app.MapGet("/api/blueprints/owned", (LogLibrary lib) => lib.Blueprints());

        // The starmap's own paragraph about one place, for the map detail card.
        // The install carries the star map's own paragraphs, so this answers
        // without the download; the download stays the fallback for the places
        // it describes and the install does not.
        app.MapGet("/api/map/lore", (LogLibrary lib, string name) =>
            (lib.GameCommodities.Lore(name) ?? lib.Community.PlaceLore(name)) is { } lore
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

            // The download still wins here, and this is the one page where it
            // does. The install's own deposit tables read cleanly but reach
            // 1,228 rows across 47 places against the download's 2,642 across
            // 234, because the cave tables are a separate record type this does
            // not walk yet. So the install fills the page for somebody who has
            // no download rather than replacing one they do have.
            var spawns = lib.Community.ResourceSpawns.Count > 0
                ? lib.Community.ResourceSpawns.Select(s => (
                    s.Resource, Deposit: s.Deposit, s.Kind, s.Location, s.System,
                    s.Group, GroupChance: (double)s.GroupChance, Share: (double)s.Share,
                    Source: "dataset"))
                : lib.GameCommodities.Spawns.Select(s => (
                    s.Resource, s.Deposit, s.Kind, s.Location, s.System,
                    s.Group, s.GroupChance, s.Share, Source: "install"));

            return spawns.Select(s =>
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
                    s.Source,
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
            lib.Items()
                .Select(item =>
                {
                    var stock = uex.ItemMarket(item.Uuid);
                    var cheapest = stock.Count > 0 ? stock.MinBy(r => r.Buy) : null;

                    return new
                    {
                        className = item.ClassName,
                        item.Name,
                        item.Type,
                        item.SubType,
                        item.Size,
                        item.Grade,
                        item.Manufacturer,
                        item.Source,
                        item.Description,
                        item.Tags,
                        price = uex.ItemPrice(item.Uuid),
                        stockedAt = stock.Count,
                        cheapestAt = cheapest?.Terminal,
                        terminals = stock.Count > 0
                            ? stock.OrderBy(r => r.Buy).Select(r => $"{r.Terminal} — {r.Buy:N0} aUEC")
                            : null
                    };
                }));

        // Hauls worth flying, sized to a hold and a wallet the caller names.
        // Each end of a haul carries the map's own id for it where the terminal
        // could be matched, so planning a run puts real dots on the map instead
        // of the page guessing at the names a second time.
        app.MapGet("/api/routes", (LogLibrary lib, UexData uex, double? scu, decimal? capital, string? from,
            string? ranking, bool? freshOnly, string? evidence) =>
            uex.Routes(
                scu ?? 0,
                capital ?? 0,
                from,
                limit: 30,
                reliableFirst: !string.Equals(ranking, "profit", StringComparison.OrdinalIgnoreCase),
                freshOnly: freshOnly == true,
                evidence: evidence ?? "any").Select(r => new
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
                r.LimitedBy,
                r.DesiredUnits,
                r.BuyStockScu,
                r.SellDemandScu,
                r.BuyAvailability,
                r.SellAvailability,
                r.Availability,
                mapReady = !string.IsNullOrWhiteSpace(lib.Terminals.IdFor(r.BuyAt))
                    && !string.IsNullOrWhiteSpace(lib.Terminals.IdFor(r.SellAt)),
                r.BuySeenAt,
                r.SellSeenAt,
                r.Freshness,
                r.FallbackSells
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
        app.MapGet("/api/casualties", (LogLibrary lib, UexFeeds feeds, int? days) =>
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

            // Waking up at login is not a visit to a clinic, and the game says
            // both with the same line - so the counted ones are the beds the
            // player went to, and the rest are reported separately rather than
            // dropped.
            //
            // The place directory can rule a bed OUT of being medical, and
            // nothing more: somewhere with no clinic had no clinic bed to use,
            // whatever the toast said. It cannot rule one IN - Port Tressler
            // has habs and a clinic, so a bed there is still either - and a
            // place the directory does not carry says nothing at all.
            string Sort(Quantumwake.Core.State.MedicalBedVisit bed) => bed.Kind switch
            {
                "wake" or "after-death" => bed.Kind,
                _ when feeds.HasClinic(bed.Place) == false => "hab",
                _ => "heal",
            };

            var sorted = beds.Select(b => new { Bed = b, Kind = Sort(b) }).ToList();
            var deliberate = sorted.Where(b => b.Kind != "wake" && b.Kind != "hab").ToList();

            var bedsUsed = deliberate
                .GroupBy(b => b.Bed.Place, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    place = g.Key,
                    times = g.Count(),
                    last = g.Max(b => b.Bed.At),
                    afterDeath = g.Count(b => b.Kind == "after-death")
                })
                .OrderByDescending(x => x.last)
                .Take(10)
                .ToList();

            var bedKinds = new
            {
                wake = sorted.Count(b => b.Kind == "wake"),
                afterDeath = sorted.Count(b => b.Kind == "after-death"),
                hab = sorted.Count(b => b.Kind == "hab"),
                heal = sorted.Count(b => b.Kind == "heal"),

                // Whether the directory could be asked at all, and whether the
                // copy on disk is new enough to carry the flag - so the page
                // can offer a refresh rather than quietly sorting nothing.
                directory = feeds.PlaceDirectory.Count,
                clinicsKnown = feeds.PlaceDirectory.Count(p => p.Clinic is not null)
            };

            return new
            {
                deaths,
                lastWokeAt = respawns.Count > 0 ? respawns[0].Place : null,
                lastWokeWhen = respawns.Count > 0 ? respawns[0].At : (DateTimeOffset?)null,
                lastBedAt = beds.Count > 0 ? beds[0].Place : null,
                lastBedWhen = beds.Count > 0 ? beds[0].At : (DateTimeOffset?)null,
                wokeAt,
                bedsUsed,
                bedKinds,
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

        app.MapGet("/api/jobs", (JobStore jobs, LogLibrary lib, UexData uex,
            ImportStore imports, string? imported) =>
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

            object Project(Job job, ImportBatch? from)
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
                    // Never the id the file carried - see SharedId.
                    Id = from is null ? job.Id : SharedId(from, job.Id),
                    job.Title,
                    job.Destination,
                    job.DestinationId,
                    job.Kind,
                    job.Source,
                    job.CreatedAt,
                    job.Done,

                    // Pinning is this machine's business whatever a file says.
                    Pinned = from is null && job.Pinned,
                    imported = from is null ? null : Marker(from),
                    items = lines,
                    haveCount = lines.Count(l => l.have),
                    totalCount = lines.Count
                };
            }

            // The reader's own first, then whatever they asked to see beside it.
            // The stash and loadout joins apply to imported rows too, and that is
            // the point: "Bob needs four Agricium" is worth much more next to
            // "and you have some at Port Tressler".
            return jobs.All().Select(job => Project(job, null))
                .Concat(Shared(imports, imported)
                    .SelectMany(batch => (batch.Authored?.Jobs ?? [])
                        .Select(job => Project(job, batch))));
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

        // ---- checklists: authored preparation, never guessed from the log ----

        app.MapGet("/api/checklists", (ChecklistStore checklists, ImportStore imports, string? imported) =>
            checklists.All().Select(list => Draw(list, null))
                .Concat(Shared(imports, imported)
                    .SelectMany(batch => (batch.Authored?.Checklists ?? [])
                        .Select(list => Draw(list, batch)))));

        app.MapPost("/api/checklists", (ChecklistStore checklists, ChecklistRequest body) =>
            Results.Ok(checklists.Add(body.Title)));

        app.MapPost("/api/checklists/{id}/items", (string id, ChecklistStore checklists, ChecklistItemRequest body) =>
        {
            var list = checklists.AddItem(id, body.Text, body.DueAt, body.Note, body.Attachments);
            return list is null ? Results.NotFound() : Results.Ok(list);
        });

        app.MapPost("/api/checklists/{id}/items/{itemId}/toggle", (string id, string itemId, ChecklistStore checklists) =>
            checklists.ToggleItem(id, itemId) ? Results.Ok(new { id, itemId }) : Results.NotFound());

        app.MapPost("/api/checklists/{id}/pin", (string id, ChecklistStore checklists) =>
            checklists.TogglePin(id) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapDelete("/api/checklists/{id}/items/{itemId}", (string id, string itemId, ChecklistStore checklists) =>
            checklists.RemoveItem(id, itemId) ? Results.Ok(new { id, itemId }) : Results.NotFound());

        app.MapDelete("/api/checklists/{id}", (string id, ChecklistStore checklists) =>
            checklists.Remove(id) ? Results.Ok(new { id }) : Results.NotFound());

        // ---- sharing: a file of the pilot's own, for a pilot they fly with ----

        // POST, not GET, and that is the whole security story for this feature.
        //
        // LanGuard whitelists by method, so a GET here would hand every receipt,
        // blueprint, job and the pilot's handle to anyone on the same wifi the
        // moment -Lan is on - in one request, in a form built for keeping. The
        // existing reads let somebody look at a page; this one lets them take
        // the lot. As a POST it is refused off-machine by the rule that already
        // exists, with no deny-list for anyone to remember to update.
        app.MapPost("/api/export", (ExportRequest body, ExportBuilder exports) =>
        {
            var choice = body.Choice();

            if (choice.AskedForNothing)
                return Results.BadRequest(new { message = "Choose at least one thing to export." });

            var document = exports.Build(choice, Producer(), DateTimeOffset.UtcNow);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, ExportDocument.Json);

            return Results.File(bytes, "application/json", ExportFileName(document));
        });

        // What to send with a bug report. Read-only, and built by allow-list:
        // every field is one this code chose to put in, rather than a log with
        // the bad parts taken out. See Diagnostics for why that way round.
        //
        // No install path - it names a user folder on plenty of machines, and
        // answers no parser question. No UEX keys; whether keys exist is a
        // boolean. No handles: the one field carrying log text is scrubbed
        // against the identifiers this install is known to have.
        app.MapGet("/api/diagnostics", (LogLibrary lib, WipeStore wipes, UexData uex, bool? samples) =>
        {
            var sessions = lib.Store.All().ToList();
            var stats = lib.Stats();

            var known = sessions
                .SelectMany(session => new[] { session.Handle, session.Geid })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Example lines are a second yes, not part of the first: they are raw
            // log text, and the only reason one exists is that a format changed -
            // which is exactly when a name can appear in a shape nothing knows.
            var health = lib.Health(known, samples == true);

            return Results.Ok(new
            {
                producer = Producer(),
                takenAt = DateTimeOffset.UtcNow,
                install = new
                {
                    found = install is not null,
                    channel = install?.Channel,
                    hasGameLog = install?.HasGameLog ?? false,
                    backups = install?.BackupLogs().Count() ?? 0,
                },
                library = new
                {
                    sessions = sessions.Count,
                    counted = sessions.Count - lib.SessionsBeforeWipe(),
                    first = sessions.Count > 0 ? sessions.Min(x => x.StartedAt) : (DateTimeOffset?)null,
                    last = sessions.Count > 0 ? sessions.Max(x => x.StartedAt) : (DateTimeOffset?)null,
                    builds = sessions
                        .Select(x => CommunityData.BuildIn(x.BuildTag))
                        .Where(x => x is not null)
                        .GroupBy(x => x!, StringComparer.Ordinal)
                        .OrderByDescending(g => g.Count())
                        .Select(g => new { build = g.Key, sessions = g.Count() }),
                },
                parser = new
                {
                    unread = health.Unread,
                    samples = samples == true,
                    tags = health.ByTag,
                },
                // The counts behind "this page is empty for me".
                views = new
                {
                    ships = stats.Ships.Count,
                    places = stats.Locations.Count,
                    destinations = stats.Destinations.Count,
                    contracts = stats.ContractsSeen,
                    purchases = stats.PurchaseCount,
                    trades = stats.TradeCount,
                    fleet = stats.FleetSize,
                    loadout = stats.Loadout.Count,
                    stash = stats.Stash.Count,
                },
                data = new
                {
                    community = lib.Community.IsEnabled,
                    communityDump = lib.Community.Dump,
                    uex = uex.IsEnabled,
                    uexKeysStored = uex.HasCredentials,
                },
                wipe = new
                {
                    at = wipes.Current.At,
                    patch = wipes.Current.Patch,
                    scope = wipes.Current.Scope.ToString(),
                    hidden = lib.SessionsBeforeWipe(),
                },
            });
        });

        // Counts and the window, never rows: nothing leaves without a click, and
        // a click is worth more when it follows seeing what would go.
        app.MapGet("/api/export/preview", (ExportBuilder exports,
            bool? receipts, bool? blueprints, bool? authored, int? days) =>
        {
            var choice = new ExportChoice(
                receipts == true, blueprints == true, authored == true,
                days ?? ExportBuilder.DefaultDays);

            var counts = exports.Preview(choice);

            return Results.Ok(new
            {
                counts.Receipts, counts.Blueprints, counts.Jobs, counts.Checklists, counts.Trips,
                days = choice.Days,
                defaultDays = ExportBuilder.DefaultDays,
            });
        });

        // Imports live in their own store and are never folded into the
        // authored files. Removing one has to leave the pilot's own work
        // untouched, and that is only cheap if it was never mixed in.
        app.MapGet("/api/imports", (ImportStore imports) => new
        {
            batches = imports.All().Select(Summarise),
            quarantined = imports.Quarantined,
        });

        app.MapGet("/api/imports/{id}", (string id, ImportStore imports) =>
        {
            var batch = imports.Find(id);
            return batch is null ? Results.NotFound() : Results.Ok(batch);
        });

        app.MapPost("/api/imports", (ImportRequest body, ImportStore imports, bool? force) =>
        {
            var text = body.Document ?? string.Empty;

            if (ImportReader.TooBig(System.Text.Encoding.UTF8.GetByteCount(text)) is { } tooBig)
                return Results.Json(new { message = tooBig.Message }, statusCode: tooBig.Status);

            var fingerprint = ImportStore.FingerprintOf(text);

            // The common way to arrive here twice is a double-clicked picker, so
            // ask rather than refuse: re-importing something purged on purpose
            // is just as legitimate, and only the reader knows which this is.
            if (force != true && imports.Matching(fingerprint) is { } already)
                return Results.Conflict(new { duplicate = true, batch = Summarise(already) });

            var (reading, problem) = ImportReader.Read(text, DateTimeOffset.UtcNow);

            if (problem is not null)
                return Results.Json(new { message = problem.Message }, statusCode: problem.Status);

            return Results.Ok(new { batch = Summarise(imports.Add(reading!, fingerprint, body.SourceName, DateTimeOffset.UtcNow)) });
        });

        // Receipts and blueprints keep their own doors, and this is deliberate.
        //
        // /api/commodities feeds four separate aggregates on the Cargo page and
        // /api/blueprints/owned feeds the "Set as goal" picker. Concatenating
        // would mean remembering to filter in five places, which is how one gets
        // forgotten - and the one that gets forgotten produces a lifetime
        // earnings figure counting somebody else's sales, or a build plan for a
        // blueprint the reader does not hold. Arriving in a different payload
        // makes that impossible rather than merely unlikely.
        app.MapGet("/api/imports/receipts", (ImportStore imports, string? imported) =>
            Shared(imports, imported ?? "all").SelectMany(batch =>
                (batch.Receipts?.Rows ?? []).Select(row => new
                {
                    row.At, row.IsSell, row.Place, row.PlaceId, row.Scu,
                    row.Amount, row.UnitPrice, row.Commodity, row.ResourceId,
                    observedTo = batch.Receipts!.ObservedTo,
                    imported = Marker(batch),
                })));

        app.MapGet("/api/imports/blueprints", (ImportStore imports, string? imported) =>
            Shared(imports, imported ?? "all").SelectMany(batch =>
                (batch.Blueprints?.Rows ?? []).Select(row => new
                {
                    row.At,
                    row.Name,
                    imported = Marker(batch),
                })));

        app.MapPost("/api/imports/{id}/hide", (string id, ImportStore imports) =>
            imports.ToggleHidden(id) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapDelete("/api/imports/{id}/{cls}", (string id, string cls, ImportStore imports) =>
            imports.RemoveClass(id, cls) ? Results.Ok(new { id, cls }) : Results.NotFound());

        app.MapDelete("/api/imports/{id}", (string id, ImportStore imports) =>
            imports.Remove(id) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapDelete("/api/imports", (ImportStore imports) => Results.Ok(new { removed = imports.Clear() }));

        // ---- flight plans: where to go next, in order ----

        // ---- the wipe: where the player's countable history begins ----

        app.MapGet("/api/wipe", (WipeStore wipes, LogLibrary lib) => Describe(wipes.Current, lib));

        app.MapPost("/api/wipe", (WipeRequest body, WipeStore wipes, LogLibrary lib) =>
        {
            var wipe = wipes.Set(body.At, body.Patch, ScopeOf(body.Covers));
            lib.Wipe = wipe;

            return Results.Ok(Describe(wipe, lib));
        });

        app.MapGet("/api/trips", (TripStore trips, ImportStore imports, string? imported) =>
            trips.All().Select(trip => Draw(trip, null))
                .Concat(Shared(imports, imported)
                    .SelectMany(batch => (batch.Authored?.Trips ?? [])
                        .Select(trip => Draw(trip, batch)))));

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

        app.MapPost("/api/trips/{id}/stops/{stopId}/actions", (string id, string stopId,
            TripStore trips, RunActionRequest body) =>
            trips.AddAction(id, stopId, body.Kind, body.Text, body.Quantity, body.Unit)
                ? Results.Ok(new { id, stopId }) : Results.NotFound());

        app.MapPost("/api/trips/{id}/stops/{stopId}/actions/{actionId}/toggle", (string id,
            string stopId, string actionId, TripStore trips) =>
            trips.ToggleAction(id, stopId, actionId)
                ? Results.Ok(new { id, stopId, actionId }) : Results.NotFound());

        app.MapDelete("/api/trips/{id}/stops/{stopId}/actions/{actionId}", (string id,
            string stopId, string actionId, TripStore trips) =>
            trips.RemoveAction(id, stopId, actionId)
                ? Results.Ok(new { id, stopId, actionId }) : Results.NotFound());

        app.MapDelete("/api/trips/{id}/stops/{stopId}", (string id, string stopId, TripStore trips) =>
            trips.RemoveStop(id, stopId) ? Results.Ok(new { id }) : Results.NotFound());

        app.MapDelete("/api/trips/{id}", (string id, TripStore trips) =>
            trips.Remove(id) ? Results.Ok(new { id }) : Results.NotFound());

        // ---- map notes: personal POIs, deliberately not telemetry ----

        app.MapGet("/api/map-notes", (MapNoteStore notes) => notes.All());

        app.MapPost("/api/map-notes", (MapNoteStore notes, MapNoteRequest body) =>
        {
            var item = notes.Add(body.PlaceId, body.Place, body.Title, body.Note, body.Tags);
            return item is null ? Results.BadRequest(new { message = "Choose a map location first." }) : Results.Ok(item);
        });

        app.MapDelete("/api/map-notes/{id}", (string id, MapNoteStore notes) =>
            notes.Remove(id) ? Results.Ok(new { id }) : Results.NotFound());

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

                // Not "enabled = true". A fetch that finished after somebody
                // pressed Disable stands down and applies nothing, and saying
                // otherwise would leave the page showing an integration that is
                // off as though it were on.
                return Results.Ok(new { enabled = uex.IsEnabled, prices = count });
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

        // What a commodity has been doing lately. A fetch per counter, so it
        // happens on the click that opens the page and is then cached - never
        // as part of a page load, and never at all while UEX is off.
        app.MapGet("/api/uex/history", async (
            UexData uex, IHttpClientFactory httpFactory, string commodity,
            int? perSide, CancellationToken token) =>
        {
            if (!uex.IsEnabled)
                return Results.Ok(new UexHistory(commodity, 0, 0, []));

            // Each counter is a separate request to UEX, so the caller says how
            // many it needs and the number is clamped here rather than trusted.
            // The Market panel's strip asks for one per side; the commodity page,
            // which is a deliberate drill-down, asks for the default four.
            var sample = Math.Clamp(perSide ?? 4, 1, 8);

            try
            {
                return Results.Ok(await uex.HistoryAsync(
                    commodity, httpFactory.CreateClient("community"), sample, token));
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
            {
                return Results.Problem(
                    title: "UEX history could not be fetched.", detail: e.Message, statusCode: 502);
            }
        });

        // Whether prices may refetch themselves, and when one was last tried.
        // staleAfterHours is served rather than duplicated in the page: the
        // interval is a judgement about someone else's server, and it should be
        // stated in one place.
        app.MapGet("/api/uex/auto", (TradeDataStore auto, UexData uex) =>
        {
            var preference = auto.Current;

            return new
            {
                preference.Asked,
                preference.Automatic,
                preference.LastCheckedAt,
                staleAfterHours = TradeDataStore.StaleAfter.TotalHours,
                fetchedAt = uex.FetchedAt,

                // The toggle is meaningless while UEX is off, and the page says
                // so rather than offering a switch that does nothing.
                uexEnabled = uex.IsEnabled
            };
        });

        app.MapPost("/api/uex/auto/answer", (bool automatic, TradeDataStore auto) =>
        {
            var preference = auto.Answer(automatic);
            return Results.Ok(new { preference.Asked, preference.Automatic });
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

        app.Logger.LogInformation(
            "Quantum Wake by nekron - http://{Host}:{Port}", lan ? "0.0.0.0" : "127.0.0.1", port);
        return app;
    }

    /// <summary>
/// The wipe as the page reads it: names rather than a flags number, and the
/// count of what it is holding back, which is the honest part.
/// </summary>
/// <summary>
/// The build tag of the most recent session, or null when nothing is stored.
/// </summary>
/// <remarks>
/// The newest session rather than the newest by version string, because
/// "4.10" sorts below "4.9" as text and the question is what was played last.
/// </remarks>
static string? PlayedBuild(LogLibrary lib) =>
    lib.Store.All()
        .Where(s => !string.IsNullOrWhiteSpace(s.BuildTag))
        .OrderByDescending(s => s.StartedAt)
        .Select(s => s.BuildTag)
        .FirstOrDefault();

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
    /// The shared files a page should draw beside the reader's own work.
    /// </summary>
    /// <remarks>
    /// None unless asked for. Importing a friend's forty jobs and finding your
    /// own page now has forty-three cards on it is an ambush by a feature
    /// somebody used once, so this answers nothing until a page says otherwise.
    /// </remarks>
    static IEnumerable<ImportBatch> Shared(ImportStore imports, string? imported)
    {
        if (string.IsNullOrWhiteSpace(imported) || imported == "none")
            return [];

        var batches = imports.All().Where(b => b.Readable && !b.Hidden);

        return imported == "all" ? batches : batches.Where(b => b.Id == imported);
    }

    /// <summary>A checklist as a page sees it, with whose it is when it is not the reader's.</summary>
    static object Draw(Checklist list, ImportBatch? from) => new
    {
        Id = from is null ? list.Id : SharedId(from, list.Id),
        list.Title,
        list.CreatedAt,
        Pinned = from is null && list.Pinned,
        Items = from is null
            ? list.Items
            : [.. list.Items.Select(i => i with { Id = SharedId(from, i.Id) })],
        imported = from is null ? null : Marker(from),
    };

    /// <summary>A flight plan as a page sees it.</summary>
    static object Draw(Trip trip, ImportBatch? from) => new
    {
        Id = from is null ? trip.Id : SharedId(from, trip.Id),
        trip.Title,
        trip.CreatedAt,
        Tracked = from is null && trip.Tracked,
        Stops = from is null
            ? trip.Stops
            : [.. trip.Stops.Select(s => s with
            {
                Id = SharedId(from, s.Id),
                Actions = [.. (s.Actions ?? []).Select(action => action with { Id = SharedId(from, action.Id) })],
            })],
        trip.Next,
        trip.Done,
        imported = from is null ? null : Marker(from),
    };

    /// <summary>Whose a row is, for a page that has to say so.</summary>
    /// <remarks>
    /// An object rather than a flag, because the card needs the handle to name
    /// them and the batch id to offer hiding the file inline. A boolean would
    /// have to be widened later across every endpoint at once.
    /// </remarks>
    static object Marker(ImportBatch batch) => new
    {
        batchId = batch.Id,
        batch.Handle,
        batch.ImportedAt,
        batch.Note,
    };

    /// <summary>
    /// An imported row's id on the wire, which is never the id the file carried.
    /// </summary>
    /// <remarks>
    /// Job, checklist and trip ids are eight hex characters minted locally with
    /// no namespace, so a file exported from this very machine and read back
    /// carries ids identical to the reader's own by construction - not by bad
    /// luck. The page builds /api/jobs/{id}/pin and DELETE /api/jobs/{id}
    /// straight out of them, so a collision would mean clicking Delete on
    /// somebody else's card deleting your own job.
    ///
    /// Prefixed, those routes simply miss and answer 404. Safe because the id
    /// cannot address anything, rather than safe because every future caller
    /// remembers to check.
    /// </remarks>
    static string SharedId(ImportBatch batch, string id) => $"imp:{batch.Id}:{id}";

    /// <summary>
    /// A batch without its contents: what the imports list draws.
    /// </summary>
    /// <remarks>
    /// The rows stay behind deliberately. This endpoint is asked for on every
    /// visit to the page, and a batch can hold twenty thousand receipts.
    /// </remarks>
    static object Summarise(ImportBatch batch) => new
    {
        batch.Id,
        batch.ImportedAt,
        batch.ExportedAt,
        batch.Handle,
        batch.Note,
        batch.SourceName,
        batch.FormatVersion,
        batch.ContentVersion,
        batch.ProducerVersion,
        batch.Classes,
        batch.Counts,
        batch.Rejected,
        batch.Truncated,
        batch.Hidden,
        batch.Readable,
    };

    /// <summary>What this build stamps on a file it writes.</summary>
    /// <remarks>
    /// Reflection rather than a constant, for the same reason /api/version uses
    /// it: a number written by hand is one that can disagree with the assembly
    /// that wrote the file, and the file outlives the build.
    /// </remarks>
    static ExportProducer Producer()
    {
        var assembly = typeof(ServerHost).Assembly;

        return new ExportProducer(
            "Quantum Wake",
            assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion);
    }

    /// <summary>
    /// What the browser saves it as: the handle, the date, and nothing a file
    /// system will argue about.
    /// </summary>
    static string ExportFileName(ExportFile document)
    {
        var who = new string((document.Handle ?? "export")
            .ToLowerInvariant()
            .Where(c => char.IsAsciiLetterOrDigit(c) || c == '-')
            .ToArray());

        if (who.Length == 0)
            who = "export";

        return $"quantumwake-{who}-{document.ExportedAt:yyyyMMdd-HHmm}.json";
    }

    /// <summary>
    /// The sales a UEX push draws from: named commodity sells inside UEX's
    /// 30-day submission window, with the unit price the kiosk actually showed.
    /// </summary>
    static IEnumerable<(DateTimeOffset At, string Commodity, string Place, decimal UnitPrice, int Scu)>
        RecentSales(LogLibrary lib) =>
        lib.TradesWithin(30)
            .Where(t => t.IsSell && t.Commodity is not null && t.Scu > 0)
            .Select(t => (t.At, t.Commodity!, t.Place, t.UnitPrice, t.Scu));

    /// <summary>Builds the short list of useful things at the player's live place.</summary>
    /// <remarks>
    /// A briefing is deliberately narrower than its source pages. The next three
    /// stops are enough to act on without hiding the rest of a plan, and the
    /// shopping/stash lists are capped for the same reason: Now is for deciding
    /// what to do before leaving a hangar, not for replacing their full views.
    /// </remarks>
    static PilotBriefing BuildBriefing(
        NowState now, TripStore trips, JobStore jobs, LogLibrary lib, UexData uex, UexFeeds feeds)
    {
        var trip = trips.Tracked();

        // The same rule as Trip.Next, and it has to be: arriving ticks the stop,
        // so selecting on Done alone drops it from the briefing at the exact
        // moment its run sheet starts to matter. That was the third copy of this
        // condition and the one nobody updated, which is the argument for
        // asking the trip rather than re-deciding it here.
        var stops = trip?.Stops.Where(Trip.Outstanding).Take(3)
            .Select(s => new BriefingStop(s.Id, s.PlaceId, s.Place, s.Note,
                [.. (s.Actions ?? []).Where(a => !a.Done)]))
            .ToList() ?? [];

        if (!now.InGame || string.IsNullOrWhiteSpace(now.Location))
            return new PilotBriefing(now.LocationId, now.Location, trip?.Id, trip?.Title, stops, [], [], [], []);

        var placeId = now.LocationId;
        var place = now.Location;
        var stats = lib.Stats();

        // A shopping list already says "in hand" for anything found in any
        // stash. Keep that same conservative reading here: a line may be at a
        // different moon, but recommending a second purchase would be worse
        // than leaving the player to move what they already own.
        var held = stats.Stash
            .SelectMany(s => s.Groups)
            .SelectMany(g => g.Items)
            .Select(i => i.Name)
            .ToList();

        var worn = stats.Loadout
            .SelectMany(slot => slot.Items)
            .Select(i => i.Name)
            .ToList();

        bool InHand(string name) => worn.Contains(name, StringComparer.OrdinalIgnoreCase)
            || held.Any(h => h.Equals(name, StringComparison.OrdinalIgnoreCase)
                || h.Contains(name, StringComparison.OrdinalIgnoreCase));

        bool AtHere(string terminal)
        {
            var resolved = lib.Terminals.Resolve(terminal);

            return placeId is { Length: > 0 } && resolved?.RawId == placeId
                || string.Equals(resolved?.Name, place, StringComparison.OrdinalIgnoreCase);
        }

        var shopping = new List<BriefingShopping>();

        foreach (var job in jobs.All().Where(j => !j.Done))
        foreach (var item in job.Items.Where(i => !InHand(i.Name)))
        {
            var commodity = uex.Market(item.Name)
                .Where(row => row.Buy > 0 && AtHere(row.Terminal))
                .OrderBy(row => row.Buy)
                .Select(row => new { row.Terminal, row.Buy, Kind = "commodity" })
                .FirstOrDefault();

            var stocked = commodity is null && MatchItem(lib, item.Name) is { Uuid: { } uuid }
                ? uex.ItemMarket(uuid)
                    .Where(row => AtHere(row.Terminal))
                    .OrderBy(row => row.Buy)
                    .Select(row => new { row.Terminal, row.Buy, Kind = "item" })
                    .FirstOrDefault()
                : null;

            var seller = commodity ?? stocked;
            if (seller is not null)
                shopping.Add(new BriefingShopping(
                    job.Id, job.Title, item.Name, item.Needed, item.Unit,
                    seller.Terminal, seller.Buy, seller.Kind));
        }

        var stash = stats.Stash
            .FirstOrDefault(s => placeId is { Length: > 0 }
                ? s.LocationId == placeId
                : string.Equals(s.Name, place, StringComparison.OrdinalIgnoreCase));

        var stashItems = stash?.Groups
            .SelectMany(group => group.Items.Select(item =>
                new BriefingStash(item.Name, group.Category, item.LastSeen)))
            .OrderBy(item => ItemCategories.Rank(item.Category))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList() ?? [];

        var services = new List<BriefingService>
        {
            // A listed item is evidence of an actual shop at this location;
            // absence is deliberately not read as "no shops", because the
            // catalogue only knows things currently on the player's lists.
            new("Shops", shopping.Count > 0 ? "items listed" : "not reported", uex.IsEnabled),
            new("Trade counter", uex.TerminalFor(place) is not null ? "known" : "not listed", uex.IsEnabled),
            new("Refuel", feeds.FuelPrices.Any(f => AtHere(f.Terminal)) ? "known" : "not listed",
                feeds.IsEnabled(UexFeeds.Fuel)),
            new("Clinic", feeds.HasClinic(place) switch
            {
                true => "known",
                false => "not listed",
                _ => "not reported"
            }, feeds.IsEnabled(UexFeeds.Places)),

            // Neither Game.log nor the installed UEX feeds describe repair
            // services. Leaving that uncertainty visible is more useful than
            // treating a refuel counter as proof a repair pad is present.
            new("Repair", "not reported", false)
        };

        var trade = uex.Opportunities(place, limit: 3)
            .Select(o => new BriefingTrade(
                o.Commodity, o.BuyHere, o.SellThere, o.SellTerminal, o.MarginPerScu))
            .ToList();

        return new PilotBriefing(
            placeId, place, trip?.Id, trip?.Title, stops,
            [.. shopping.Take(8)], trade, services, stashItems);
    }

    /// <summary>Maps service evidence onto the app's own atlas identifiers.</summary>
    static IReadOnlyList<MapServicePlace> BuildMapServices(LogLibrary lib, UexData uex, UexFeeds feeds)
    {
        var servicesByPlace = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        void Add(string? terminalOrPlace, string service)
        {
            var place = lib.Terminals.Resolve(terminalOrPlace);
            if (place is null)
                return;

            if (!servicesByPlace.TryGetValue(place.RawId, out var services))
                servicesByPlace[place.RawId] = services = new(StringComparer.Ordinal);

            services.Add(service);
        }

        // A known counter is a useful broad answer to "shops". It does not
        // claim a particular item is stocked; the briefing makes that narrower
        // assertion only when a current shopping-list line matches it.
        foreach (var terminal in uex.KnownTerminals())
            Add(terminal, "shop");

        foreach (var terminal in feeds.FuelPrices.Select(fuel => fuel.Terminal))
            Add(terminal, "refuel");

        foreach (var place in feeds.PlaceDirectory.Where(place => place.Clinic is true))
            Add(place.Name, "clinic");

        return servicesByPlace
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new MapServicePlace(entry.Key, [.. entry.Value.OrderBy(service => service)]))
            .ToList();
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

/// <summary>The current place joined onto the small set of decisions it enables.</summary>
public sealed record PilotBriefing(
    string? LocationId,
    string? Location,
    string? TripId,
    string? TripTitle,
    IReadOnlyList<BriefingStop> Stops,
    IReadOnlyList<BriefingShopping> Shopping,
    IReadOnlyList<BriefingTrade> Trade,
    IReadOnlyList<BriefingService> Services,
    IReadOnlyList<BriefingStash> Stash);

/// <summary>One outstanding flight-plan stop, in the order it will be flown.</summary>
/// <param name="Actions">
/// What is still to be done at this stop, so a card that keeps a landed stop
/// alive can say why it is keeping it.
/// </param>
public sealed record BriefingStop(
    string Id, string PlaceId, string Place, string? Note,
    IReadOnlyList<RunAction> Actions);

/// <summary>One missing shopping-list item stocked at the live place.</summary>
public sealed record BriefingShopping(
    string JobId, string JobTitle, string Name, double Needed, string Unit,
    string Terminal, decimal Price, string Kind);

/// <summary>A market lead, never a claim about cargo currently in the hold.</summary>
public sealed record BriefingTrade(
    string Commodity, decimal BuyHere, decimal SellThere, string SellTerminal, decimal MarginPerScu);

/// <summary>A service the installed data can identify, or explicitly cannot.</summary>
public sealed record BriefingService(string Name, string Status, bool DataEnabled);

/// <summary>One item last seen at the live place.</summary>
public sealed record BriefingStash(string Name, string Category, DateTimeOffset LastSeen);

/// <summary>One atlas place and the services the installed feeds can locate there.</summary>
public sealed record MapServicePlace(string PlaceId, IReadOnlyList<string> Services);


/// <summary>
/// Body of POST /api/wipe. A null date counts everything again, and
/// <paramref name="Covers"/> names what the wipe took - "money", "ships",
/// "inventory", "history" - with an empty list read as all of it.
/// </summary>
public sealed record WipeRequest(DateTimeOffset? At, string? Patch, List<string>? Covers);

/// <summary>Body of POST /api/trips.</summary>
public sealed record TripRequest(string? Title, List<TripStop>? Stops);

/// <summary>Body of POST /api/trips/{id}/stops/{stopId}/actions.</summary>
public sealed record RunActionRequest(string? Kind, string? Text, decimal? Quantity, string? Unit);

/// <summary>Body of POST /api/map-notes.</summary>
public sealed record MapNoteRequest(
    string? PlaceId,
    string? Place,
    string? Title,
    string? Note,
    List<string>? Tags);

/// <summary>
/// Body of POST /api/export: what to share, and how far back.
/// </summary>
/// <remarks>
/// Every class is false unless asked for, even though the page's boxes start
/// ticked. The failure direction of a stale client or a typo has to be sharing
/// nothing rather than sharing everything.
/// </remarks>
public sealed record ExportRequest(
    bool Receipts = false,
    bool Blueprints = false,
    bool Authored = false,
    int? Days = null,
    bool Handle = true,
    string? Note = null)
{
    public ExportChoice Choice() =>
        new(Receipts, Blueprints, Authored, Days ?? ExportBuilder.DefaultDays, Handle, Note);
}

/// <summary>
/// Body of POST /api/imports: the file's text, as the picker read it.
/// </summary>
/// <remarks>
/// A JSON body rather than multipart. Nothing else in the app posts multipart,
/// FileReader hands back the string for nothing, and a body keeps the size check
/// a single question about how many bytes arrived.
/// </remarks>
public sealed record ImportRequest(string? Document, string? SourceName = null);

/// <summary>Body of POST /api/checklists.</summary>
public sealed record ChecklistRequest(string? Title);

/// <summary>Body of POST /api/checklists/{id}/items.</summary>
public sealed record ChecklistItemRequest(
    string? Text,
    DateTimeOffset? DueAt,
    string? Note,
    List<ChecklistAttachment>? Attachments);

/// <summary>One line of the merged logbook timeline.</summary>
public sealed record LogbookLine(
    DateTimeOffset At, string Kind, string What, string Place, string Detail, decimal? Amount);
