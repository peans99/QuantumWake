using System.Text;
using System.Xml.Linq;
using Quantumwake.Core.Controls;
using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// Keeps a copy of the keybinding profile whenever the game rewrites it.
/// </summary>
/// <remarks>
/// A look every five seconds at one file's length and write time, and a
/// read only when either moved. A file-system watcher would do it sooner,
/// but the game writes the profile in a burst and a watcher fires on the
/// first byte; a poll that sees the file settled is what keeps whole
/// copies. The same reasoning as the screenshot watch.
/// </remarks>
public sealed class ControlsWatchService(ControlsStore store, ILogger<ControlsWatchService> logger, GameInstall? install = null) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(5);

    /// <summary>The live profile's path for an install, whether or not the file exists yet.</summary>
    public static string ProfilePath(GameInstall install) =>
        Path.Combine(install.RootPath, "user", "client", "0", "Profiles", "default", "actionmaps.xml");

    /// <summary>Where the game imports layouts from: <c>pp_rebindkeys</c> and the keybinding screen read this folder.</summary>
    public static string MappingsFolder(GameInstall install) =>
        Path.Combine(install.RootPath, "user", "client", "0", "controls", "mappings");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (install is null) return;
        var path = ProfilePath(install);
        (long Length, DateTime Written)? seen = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Exists)
                {
                    var now = (info.Length, info.LastWriteTimeUtc);
                    if (seen != now)
                    {
                        // A locked file has not been seen yet: retry even when
                        // its size and timestamp stay the same after unlocking.
                        if (store.Snapshot(path) is { } kept)
                        {
                            seen = now;
                            if (kept.Taken)
                                logger.LogInformation("Keybindings changed; kept as {Id}.", kept.Backup.Id);
                        }
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Mid-write, or a permissions hiccup; next tick.
            }
            try { await Task.Delay(Every, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>The Controls page's endpoints: the profile read against the catalogue, backups, exports, templates.</summary>
public static class ControlsEndpoints
{
    public static void Map(WebApplication app, GameInstall? install)
    {
        // Everything the page draws, in one read: the live profile against
        // the catalogue, with each device's template and reference layouts.
        app.MapGet("/api/controls", (LogLibrary lib, ControlsStore store, JoystickTemplates templates) =>
        {
            var controls = lib.GameCommodities.Controls;
            var path = install is null ? null : ControlsWatchService.ProfilePath(install);
            ControlProfile? profile = null;
            string? problem = null;
            if (path is not null && File.Exists(path))
            {
                try { profile = ControlProfile.Parse(File.ReadAllBytes(path)); }
                catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or IOException) { problem = e.Message; }
            }

            return Results.Ok(new
            {
                ready = !controls.Catalogue.IsEmpty,
                gameRunning = GameProcess.IsRunning(),
                profilePath = path,
                profileFound = profile is not null,
                problem,
                profile = profile is null ? null : Describe(profile, controls, templates),
                catalogue = new
                {
                    maps = controls.Catalogue.ActionMaps,
                    actions = controls.Catalogue.Actions.Select(a => new
                    {
                        a.ActionMap, a.Name, a.Label, a.Description, a.Keyboard, a.Mouse, a.Gamepad, a.Joystick, a.ActivationMode, a.OptionGroup,
                    }),
                },
                layouts = controls.Layouts.Select(l => new
                {
                    l.File, l.Profile.Name, label = l.Profile.Label,
                    devices = l.Profile.Joysticks.Select(d => new { d.Key, d.Product, d.Guid }),
                    bindings = l.Profile.Bindings.Count,
                }),
                templates = TemplatesState(templates),
                backups = store.Backups().Take(1).Select(b => new { b.Id, b.TakenAt, b.WrittenAt, b.Bytes }).FirstOrDefault(),
                backupCount = store.Backups().Count,
            });
        });

        app.MapGet("/api/controls/backups", (ControlsStore store) => store.Backups());

        app.MapPost("/api/controls/backups", (ControlsStore store) =>
        {
            if (install is null) return Results.NotFound(new { message = "No install." });
            var kept = store.Snapshot(ControlsWatchService.ProfilePath(install));
            return kept is null
                ? Results.NotFound(new { message = "The game has not written a keybinding profile yet." })
                : Results.Ok(new { kept.Value.Backup, kept.Value.Taken });
        });

        // A kept version, or the live file, as a file to keep anywhere.
        app.MapGet("/api/controls/backups/{id}/file", (string id, ControlsStore store) =>
        {
            var bytes = id == "live"
                ? install is not null && File.Exists(ControlsWatchService.ProfilePath(install)) ? File.ReadAllBytes(ControlsWatchService.ProfilePath(install)) : null
                : store.Read(id);
            if (bytes is null) return Results.NotFound();
            var name = id == "live" ? $"actionmaps-{DateTime.Now:yyyyMMdd-HHmm}.xml" : $"actionmaps-{id}.xml";
            return Results.File(bytes, "application/xml", name);
        });

        // A file the pilot brings back - one downloaded from here, the game's
        // own actionmaps.xml from another machine, or an export from the
        // mappings folder - kept as a version once it reads as a profile.
        app.MapPost("/api/controls/backups/import", async (HttpContext ctx, ControlsStore store) =>
        {
            using var buffer = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(buffer, ctx.RequestAborted);
            var bytes = buffer.ToArray();
            if (bytes.Length == 0) return Results.BadRequest(new { message = "The file is empty." });
            if (bytes.Length > 4 * 1024 * 1024) return Results.BadRequest(new { message = "That is not a keybinding profile: far too large." });
            ControlProfile profile;
            try { profile = ControlProfile.Parse(bytes); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException) { return Results.BadRequest(new { message = $"That file does not read as a keybinding profile: {e.Message}" }); }
            if (profile.Bindings.Count == 0 && profile.Devices.Count == 0)
                return Results.BadRequest(new { message = "That file reads as XML but binds nothing and names no device." });
            var writtenAt = DateTimeOffset.TryParse(ctx.Request.Headers["X-File-Modified"], out var at) ? at : DateTimeOffset.UtcNow;
            var kept = store.Keep(bytes, writtenAt);
            return Results.Ok(new { kept.Backup, kept.Taken, bindings = profile.Bindings.Count, devices = profile.Devices.Count });
        });

        // The restore: the kept version written over the game's own profile.
        // Only with the game closed - it reads the file at start and rewrites
        // it while running, so a restore under a running game is lost or
        // half-merged - and only after the file as it is now has been kept,
        // so the restore is itself undoable.
        app.MapPost("/api/controls/restore", (RestoreRequest body, ControlsStore store) =>
        {
            if (install is null) return Results.NotFound(new { message = "No install." });
            if (GameProcess.IsRunning())
                return Results.Conflict(new { message = "Star Citizen is running. It reads the profile at start and rewrites it in play, so close the game first - or export this version and import it from the keybinding screen instead." });
            if (string.IsNullOrWhiteSpace(body.Source) || store.Read(body.Source) is not { } bytes)
                return Results.NotFound(new { message = "No such version." });

            XDocument live;
            try { live = ControlsExport.ToLive(Quantumwake.Core.GameData.CryXml.Parse(bytes)); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException) { return Results.Problem(e.Message); }

            var path = ControlsWatchService.ProfilePath(install);
            var before = store.Snapshot(path);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, live.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Results.Problem(title: "The profile could not be written.", detail: e.Message, statusCode: 500);
            }
            var after = store.Snapshot(path);
            return Results.Ok(new { restored = body.Source, path, keptBefore = before?.Backup.Id, now = after?.Backup.Id });
        });

        // A stick's curves and dead zones changed. Written the two ways a
        // restore is: into the live profile with the game closed, after
        // keeping it as it was; or as an import file with the game open.
        app.MapPost("/api/controls/axes", (AxesRequest body, ControlsStore store) =>
        {
            if (install is null) return Results.NotFound(new { message = "No install." });
            var path = ControlsWatchService.ProfilePath(install);
            if (!File.Exists(path)) return Results.NotFound(new { message = "The game has not written a keybinding profile yet." });
            if (body.Instance <= 0) return Results.BadRequest(new { message = "Which stick?" });

            var running = GameProcess.IsRunning();
            var how = body.How ?? (running ? "export" : "live");
            if (how == "live" && running)
                return Results.Conflict(new { message = "Star Citizen is running. Close it to write the profile, or apply as an import file and load it from the keybinding screen." });

            XDocument changed;
            try
            {
                var source = Quantumwake.Core.GameData.CryXml.Parse(File.ReadAllBytes(path));
                var profile = ControlProfile.Parse(source);
                var device = profile.Devices.FirstOrDefault(d => d.Type == "joystick" && d.Instance == body.Instance);
                changed = ControlsExport.ApplyAxes(source, body.Instance, device?.Product, device?.Guid,
                    (body.Curves ?? []).Select(c => new CurveSetting(c.Option ?? "", c.Exponent, c.Inverted)).ToList(),
                    body.Deadzones ?? new Dictionary<string, double>());
            }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or IOException)
            {
                return Results.Problem(e.Message);
            }

            if (how == "live")
            {
                var before = store.Snapshot(path);
                try { File.WriteAllText(path, changed.ToString(), new UTF8Encoding(false)); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return Results.Problem(title: "The profile could not be written.", detail: e.Message, statusCode: 500); }
                var after = store.Snapshot(path);
                return Results.Ok(new { how, path, keptBefore = before?.Backup.Id, now = after?.Backup.Id });
            }

            var name = ControlsExport.SafeName(body.Name ?? "quantumwake-axes");
            var export = ControlsExport.Build(changed, name);
            var mappings = Path.Combine(ControlsWatchService.MappingsFolder(install), ControlsExport.MappingFileName(name));
            Directory.CreateDirectory(ControlsWatchService.MappingsFolder(install));
            File.WriteAllText(mappings, export.ToString(), new UTF8Encoding(false));
            return Results.Ok(new { how, name, mappings, command = $"pp_rebindkeys {ControlsExport.MappingFileName(name)}" });
        });

        // Bindings changed - a control given an action, or taken off one -
        // written the same two ways as the axes.
        app.MapPost("/api/controls/bindings", (BindingsRequest body, ControlsStore store) =>
        {
            if (install is null) return Results.NotFound(new { message = "No install." });
            var path = ControlsWatchService.ProfilePath(install);
            if (!File.Exists(path)) return Results.NotFound(new { message = "The game has not written a keybinding profile yet." });
            var changes = (body.Changes ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.ActionMap) && !string.IsNullOrWhiteSpace(c.Action) && !string.IsNullOrWhiteSpace(c.Input))
                .Select(c => new BindingChange(c.ActionMap!, c.Action!, c.Input!, c.Remove, c.ActivationMode))
                .ToList();
            if (changes.Count == 0) return Results.BadRequest(new { message = "Nothing to change." });

            var running = GameProcess.IsRunning();
            var how = body.How ?? (running ? "export" : "live");
            if (how == "live" && running)
                return Results.Conflict(new { message = "Star Citizen is running. Close it to write the profile, or apply as an import file and load it from the keybinding screen." });

            XDocument changed;
            try { changed = ControlsExport.ApplyBindings(Quantumwake.Core.GameData.CryXml.Parse(File.ReadAllBytes(path)), changes); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or IOException) { return Results.Problem(e.Message); }

            if (how == "live")
            {
                var before = store.Snapshot(path);
                try { File.WriteAllText(path, changed.ToString(), new UTF8Encoding(false)); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return Results.Problem(title: "The profile could not be written.", detail: e.Message, statusCode: 500); }
                var after = store.Snapshot(path);
                return Results.Ok(new { how, path, changed = changes.Count, keptBefore = before?.Backup.Id, now = after?.Backup.Id });
            }

            var name = ControlsExport.SafeName(body.Name ?? "quantumwake-bindings");
            var export = ControlsExport.Build(changed, name);
            var mappings = Path.Combine(ControlsWatchService.MappingsFolder(install), ControlsExport.MappingFileName(name));
            Directory.CreateDirectory(ControlsWatchService.MappingsFolder(install));
            File.WriteAllText(mappings, export.ToString(), new UTF8Encoding(false));
            return Results.Ok(new { how, name, mappings, changed = changes.Count, command = $"pp_rebindkeys {ControlsExport.MappingFileName(name)}" });
        });

        // The sticks as Windows sees them this instant, matched to the profile
        // by product GUID, with what is pressed: the page polls this while the
        // Sticks pane is open and lights the picture. Under the bare server
        // there is nothing to read, and it says so.
        app.MapGet("/api/controls/live", (IJoystickReader joysticks, LogLibrary lib) =>
        {
            if (!joysticks.Available)
                return Results.Ok(new { available = false, reason = "The dashboard is running under the bare server; the sticks are read by QuantumWake.exe.", devices = Array.Empty<object>() });

            var devices = joysticks.Devices();
            var readings = joysticks.Read().ToDictionary(r => r.Id);
            // The profile's line for each product, so a reading can say which
            // js number it is - and a stick the profile has no line for is
            // shown as new. Two of one product cannot be told apart here; the
            // page says so for those.
            ControlProfile? profile = null;
            if (install is not null && File.Exists(ControlsWatchService.ProfilePath(install)))
            {
                try { profile = ControlProfile.Parse(File.ReadAllBytes(ControlsWatchService.ProfilePath(install))); }
                catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or IOException) { profile = null; }
            }
            var byGuid = (profile?.Joysticks ?? []).Where(d => d.Guid is not null).ToLookup(d => d.Guid!, StringComparer.OrdinalIgnoreCase);

            return Results.Ok(new
            {
                available = true,
                devices = devices.Select(d =>
                {
                    var matches = byGuid[d.Guid].ToList();
                    var reading = readings.GetValueOrDefault(d.Id);
                    return new
                    {
                        d.Id, d.Guid, vendorId = d.Vendor, productId = d.Product, d.Name, buttonCount = d.Buttons, axisCount = d.Axes, hatCount = d.Hats,
                        keys = matches.Select(m => m.Key).ToList(),
                        product = matches.FirstOrDefault()?.Product,
                        ambiguous = matches.Count > 1,
                        pressed = reading?.Buttons ?? [],
                        hats = reading?.Hats ?? [],
                        axes = reading?.Axes ?? [],
                    };
                }),
                // Sticks the profile names that are not plugged in now.
                missing = (profile?.Joysticks ?? []).Where(p => p.Guid is not null && !devices.Any(d => string.Equals(d.Guid, p.Guid, StringComparison.OrdinalIgnoreCase)))
                    .Select(p => new { p.Key, p.Product, p.Guid }),
            });
        });

        // One kept version, read against the catalogue like the live one.
        app.MapGet("/api/controls/backups/{id}", (string id, LogLibrary lib, ControlsStore store, JoystickTemplates templates) =>
        {
            if (store.Read(id) is not { } bytes) return Results.NotFound();
            try { return Results.Ok(Describe(ControlProfile.Parse(bytes), lib.GameCommodities.Controls, templates)); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException) { return Results.Problem(e.Message); }
        });

        // What changed between two versions; "live" stands for the file as it is now.
        app.MapGet("/api/controls/backups/{older}/diff/{newer}", (string older, string newer, LogLibrary lib, ControlsStore store) =>
        {
            byte[]? Bytes(string id) => id == "live"
                ? install is not null && File.Exists(ControlsWatchService.ProfilePath(install)) ? File.ReadAllBytes(ControlsWatchService.ProfilePath(install)) : null
                : store.Read(id);
            if (Bytes(older) is not { } a || Bytes(newer) is not { } b) return Results.NotFound();
            var catalogue = lib.GameCommodities.Controls.Catalogue;
            var changes = ControlsDiff.Between(ControlProfile.Parse(a), ControlProfile.Parse(b));
            return Results.Ok(changes.Select(c => new
            {
                c.ActionMap, c.Action, c.Before, c.After,
                label = catalogue.Find(c.ActionMap, c.Action)?.Label ?? c.Action,
                map = catalogue.ActionMaps.FirstOrDefault(m => m.Name == c.ActionMap)?.Label ?? c.ActionMap,
                beforeLabels = c.Before.Select(i => ControlInput.Parse(i).Label),
                afterLabels = c.After.Select(i => ControlInput.Parse(i).Label),
            }));
        });

        // An export the game can import: written under the app's own folder
        // first, and copied into the game's mappings folder only on the
        // second, explicit call - the README promises the app writes nothing
        // under the install but the one text file, and this is the one
        // exception, made by hand.
        app.MapPost("/api/controls/export", (ExportRequest body, ControlsStore store) =>
        {
            byte[]? source = body.Source is null or "live"
                ? install is not null && File.Exists(ControlsWatchService.ProfilePath(install)) ? File.ReadAllBytes(ControlsWatchService.ProfilePath(install)) : null
                : store.Read(body.Source);
            if (source is null) return Results.NotFound(new { message = "Nothing to export from." });

            var name = ControlsExport.SafeName(body.Name ?? "quantumwake");
            var retarget = (body.Retarget ?? new Dictionary<string, int>())
                .Where(kv => int.TryParse(kv.Key, out _))
                .ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
            XDocument export;
            try { export = ControlsExport.Build(Quantumwake.Core.GameData.CryXml.Parse(source), name, retarget); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException) { return Results.Problem(e.Message); }

            var folder = Quantumwake.Core.AppPaths.In("controls", "exports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, ControlsExport.MappingFileName(name));
            File.WriteAllText(path, export.ToString(), new UTF8Encoding(false));

            var installed = false;
            string? mappings = null;
            if (body.Install && install is not null)
            {
                mappings = Path.Combine(ControlsWatchService.MappingsFolder(install), ControlsExport.MappingFileName(name));
                Directory.CreateDirectory(ControlsWatchService.MappingsFolder(install));
                File.Copy(path, mappings, overwrite: true);
                installed = true;
            }
            return Results.Ok(new { name, path, installed, mappings, command = $"pp_rebindkeys {ControlsExport.MappingFileName(name)}" });
        });

        // The template feed: opt-in, like every other.
        app.MapPost("/api/controls/templates/enable", async (JoystickTemplates templates, IHttpClientFactory httpFactory) =>
        {
            try
            {
                var count = await templates.EnableAsync(httpFactory.CreateClient("community"));
                return Results.Ok(new { enabled = true, templates = count });
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException)
            {
                return Results.Problem(title: "The template index could not be fetched.", detail: e.Message, statusCode: 502);
            }
        });

        app.MapPost("/api/controls/templates/disable", (JoystickTemplates templates) =>
        {
            templates.Disable();
            return Results.Ok(new { enabled = false });
        });

        app.MapPost("/api/controls/templates/folder", (FolderRequest body, JoystickTemplates templates) =>
        {
            if (!string.IsNullOrWhiteSpace(body.Folder) && !Directory.Exists(body.Folder))
                return Results.BadRequest(new { message = "No such folder." });
            templates.SetFolder(body.Folder);
            return Results.Ok(TemplatesState(templates));
        });

        app.MapPost("/api/controls/templates/assign", (AssignRequest body, JoystickTemplates templates) =>
        {
            if (string.IsNullOrWhiteSpace(body.Guid)) return Results.BadRequest(new { message = "Which device?" });
            templates.Assign(body.Guid, body.Key);
            return Results.Ok(TemplatesState(templates));
        });

        // The picture itself, as SVG the page reads and writes labels into.
        app.MapGet("/api/controls/template", async (string key, JoystickTemplates templates, IHttpClientFactory httpFactory, HttpContext ctx) =>
        {
            try
            {
                var svg = await templates.ReadAsync(key, templates.Enabled ? httpFactory.CreateClient("community") : null, ctx.RequestAborted);
                if (svg is null) return Results.NotFound(new { message = "No picture: the template feed is off and nothing is kept for this one." });
                // Also protect a template opened as a document, outside the
                // dashboard. Do not reuse responses cached before sanitizing.
                ctx.Response.Headers.CacheControl = "no-store";
                ctx.Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; img-src data:; style-src 'unsafe-inline'";
                ctx.Response.Headers.XContentTypeOptions = "nosniff";
                return Results.File(svg, "image/svg+xml");
            }
            catch (InvalidDataException)
            {
                return Results.Problem(title: "The picture is not a supported SVG template.", statusCode: 422);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                return Results.Problem(title: "The picture could not be fetched.", detail: e.Message, statusCode: 502);
            }
        });

        // A reference layout the game ships, read against the catalogue.
        // What the sticks are missing, against the layouts the game ships for
        // them. The three filters live in ControlsCheck; this only feeds it and
        // reports the counts, because a list of one has to be readable as a
        // list of one rather than as a check that did not run.
        app.MapGet("/api/controls/check", (LogLibrary lib, ControlsDismissals dismissals) =>
        {
            if (install is null || !File.Exists(ControlsWatchService.ProfilePath(install)))
                return Results.Ok(new { ready = false, reason = "The game's keybinding profile has not been read yet." });

            ControlProfile mine;
            try { mine = ControlProfile.Parse(File.ReadAllBytes(ControlsWatchService.ProfilePath(install))); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or IOException)
            { return Results.Ok(new { ready = false, reason = "The keybinding profile could not be read." }); }

            var controls = lib.GameCommodities.Controls;
            var references = controls.Layouts
                .Select(l => (Source: l.File, Profile: l.Profile))
                .ToList();

            var result = ControlsCheck.Against(mine, references, controls.Catalogue, dismissals.Keys);
            return Results.Ok(new
            {
                ready = true,
                result.Gaps,
                result.Counts,
                result.Sources,
                names = result.Sources.ToDictionary(
                    f => f,
                    f => controls.Layouts.FirstOrDefault(l => l.File == f)?.Profile.Name
                         ?? f),
            });
        });

        // Saying no is a preference, so it is a POST that stores a flag and
        // nothing else; the check is recomputed on the next read.
        app.MapPost("/api/controls/check/dismiss", (DismissRequest body, ControlsDismissals dismissals) =>
        {
            if (string.IsNullOrWhiteSpace(body.Key)) return Results.BadRequest(new { message = "No suggestion named." });
            dismissals.Set(body.Key, body.Dismissed);
            return Results.Ok(new { body.Key, body.Dismissed, dismissed = dismissals.Keys.Count });
        });

        app.MapPost("/api/controls/check/ask-again", (ControlsDismissals dismissals) =>
        {
            dismissals.Clear();
            return Results.Ok(new { dismissed = 0 });
        });

        app.MapGet("/api/controls/layouts/{file}", (string file, LogLibrary lib, JoystickTemplates templates) =>
        {
            var controls = lib.GameCommodities.Controls;
            var layout = controls.Layouts.FirstOrDefault(l => string.Equals(l.File, file, StringComparison.OrdinalIgnoreCase));
            return layout is null ? Results.NotFound() : Results.Ok(Describe(layout.Profile, controls, templates));
        });
    }

    private static object TemplatesState(JoystickTemplates templates) => new
    {
        templates.Enabled, templates.FetchedAt, templates.Folder,
        project = JoystickTemplates.ProjectUrl,
        available = templates.Available(),
        assignments = templates.Assignments,
    };

    /// <summary>A profile as the page draws it: devices with their pictures, bindings with their words.</summary>
    private static object Describe(ControlProfile profile, GameControlsData controls, JoystickTemplates templates)
    {
        var catalogue = controls.Catalogue;
        return new
        {
            profile.Name, profile.Label,
            devices = profile.Devices.Select(d => new
            {
                d.Key, d.Type, d.Instance, d.Product, d.Guid,
                usb = d.Usb is { } u ? new { vendor = u.Vendor, product = u.Product } : null,
                d.Deadzones, d.Curves,
                bindings = profile.Bindings.Count(b => b.Input.DeviceKey == d.Key && b.Input.Kind != InputKind.None),
                template = templates.For(d.Guid),
                layouts = d.Guid is null ? [] : controls.LayoutsFor(d.Guid).Select(l => l.File).ToList(),
            }),
            bindings = profile.Bindings.Select(b =>
            {
                var action = catalogue.Find(b.ActionMap, b.Action);
                var map = catalogue.ActionMaps.FirstOrDefault(m => m.Name == b.ActionMap);
                return new
                {
                    b.ActionMap, b.Action,
                    input = new
                    {
                        b.Input.Raw, b.Input.Device, b.Input.Instance, kind = b.Input.Kind.ToString().ToLowerInvariant(),
                        b.Input.Index, b.Input.Name, b.Input.Control, b.Input.Label, deviceKey = b.Input.DeviceKey,
                        modifiers = b.Input.Modifiers.Select(m => m.Raw),
                    },
                    activationMode = b.ActivationMode ?? action?.ActivationMode ?? "",
                    b.MultiTap,
                    label = action?.Label ?? b.Action,
                    description = action?.Description ?? "",
                    map = map?.Label ?? b.ActionMap,
                    category = map?.Category ?? "",
                    known = action is not null,
                };
            }),
        };
    }

    public sealed record ExportRequest(string? Source, string? Name, Dictionary<string, int>? Retarget, bool Install = false);

/// <summary>One suggestion the pilot is saying no to, or taking the no back on.</summary>
public sealed record DismissRequest(string? Key, bool Dismissed);
    public sealed record RestoreRequest(string? Source);
    public sealed record BindingChangeRequest(string? ActionMap, string? Action, string? Input, bool Remove = false, string? ActivationMode = null);
    public sealed record BindingsRequest(List<BindingChangeRequest>? Changes, string? How, string? Name);
    public sealed record CurveRequest(string? Option, double? Exponent, bool Inverted);
    public sealed record AxesRequest(int Instance, List<CurveRequest>? Curves, Dictionary<string, double>? Deadzones, string? How, string? Name);
    public sealed record FolderRequest(string? Folder);
    public sealed record AssignRequest(string? Guid, string? Key);
}

/// <summary>The bare server's answer: no sticks to read, and it says so.</summary>
public sealed class NoJoysticks : IJoystickReader
{
    public bool Available => false;
    public IReadOnlyList<JoystickDevice> Devices() => [];
    public IReadOnlyList<JoystickReading> Read() => [];
}
