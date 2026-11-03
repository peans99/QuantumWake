using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Quantumwake.Core;
using Quantumwake.Core.GameData;
using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// Reads models, materials and textures out of the install for the 3D view,
/// opened on first use.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here runs at start-up. The archive index takes about 1.4 s and the
/// catalogue 2.8 s the first time (then it is a cached file), and most visits
/// never open a model, so both wait for the first one that does.
/// </para>
/// <para>
/// A packed mesh is kept on disk under <c>models\</c>, named for the archive's
/// stamp and the path, so a ship opens from a file the second time. Textures
/// are not kept: reading one out of the archive is a ZStd inflate and a
/// concatenation, and the browser keeps them itself.
/// </para>
/// </remarks>
public sealed class ModelLibrary(GameInstall? install)
{
    private readonly object _gate = new();
    private ModelArchive? _archive;
    private GameModelCatalogue? _catalogue;
    private readonly ConcurrentDictionary<string, IReadOnlyList<ModelSubMaterial>> _materials = new(StringComparer.OrdinalIgnoreCase);

    public bool Available => install is not null && P4kArchive.Exists(install.RootPath);

    /// <summary>Changes whenever the game patches, so a page can cache by it.</summary>
    public string Stamp => Available
        ? new FileInfo(P4kArchive.PathFor(install!.RootPath)).LastWriteTimeUtc.Ticks.ToString("x")
        : "none";

    public ModelArchive? Archive
    {
        get
        {
            if (!Available) return null;
            lock (_gate)
                return _archive ??= ModelArchive.Open(P4kArchive.PathFor(install!.RootPath));
        }
    }

    public GameModelCatalogue Catalogue
    {
        get
        {
            if (!Available) return GameModelCatalogue.Empty;
            lock (_gate)
                return _catalogue ??= GameModelCatalogue.Load(
                    P4kArchive.PathFor(install!.RootPath), AppPaths.In("models", "catalogue.json"));
        }
    }

    public IReadOnlyList<ModelSubMaterial>? Material(string path)
    {
        if (Archive is not { } archive) return null;
        if (_materials.TryGetValue(path, out var known)) return known;
        var bytes = archive.TryRead(path);
        if (bytes is null) return null;
        var read = ModelMaterials.Read(bytes, layer => archive.TryRead(
            layer.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) ? layer : layer + ".mtl"));
        _materials[path] = read;
        return read;
    }

    public byte[]? Mesh(string path)
    {
        if (Archive is not { } archive) return null;
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant().Replace('\\', '/'))))[..24];
        var file = AppPaths.In("models", $"{Stamp}-{ModelMesh.Version}", name + ".qwm");
        try
        {
            if (File.Exists(file)) return File.ReadAllBytes(file);
        }
        catch (IOException) { }

        var mesh = ModelMesh.Build(archive, path, Material);
        if (mesh is null) return null;
        try
        {
            Prune(Path.GetDirectoryName(file)!);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            // Two pages asking at once would race on one temporary name.
            var temp = $"{file}.{Guid.NewGuid():N}.tmp";
            File.WriteAllBytes(temp, mesh);
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return mesh;
    }

    private int _pruned;

    /// <summary>
    /// Meshes packed for an earlier patch, or by an earlier build of the
    /// packer, are never asked for again; a hull is 50 MB, so they go.
    /// </summary>
    private void Prune(string current)
    {
        if (Interlocked.Exchange(ref _pruned, 1) == 1) return;
        var parent = Path.GetDirectoryName(current)!;
        if (!Directory.Exists(parent)) return;
        foreach (var dir in Directory.EnumerateDirectories(parent))
        {
            if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(current), StringComparison.OrdinalIgnoreCase)) continue;
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}

public static partial class ServerHost
{
    /// <summary>
    /// The kinds of fitted part the view draws: what shows on the outside. A
    /// Corsair's loadout has 110 entries and most are inside the hull - power
    /// plants, coolers, dashboards, the flight controller's box - or have no
    /// model at all. Parts whose model sits in the hull's own exterior folder
    /// (the Corsair's refuelling port) are drawn whatever their kind.
    /// </summary>
    static readonly HashSet<string> VisibleKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "WeaponGun", "WeaponMining", "WeaponTractorBeam", "TractorBeam", "Turret", "TurretBase", "UtilityTurret",
        "MissileLauncher", "BombLauncher", "Missile", "Bomb", "WeaponDefensive", "MainThruster", "ManneuverThruster",
        "ToolArm", "SalvageHead", "SalvageModifier", "MiningModifier", "WeaponAttachment", "DockingCollar",
    };

    static bool Visible(string? type, string? geometry) =>
        type?.Split('/')[0] is { } kind && VisibleKinds.Contains(kind)
        || geometry?.Replace('\\', '/').Contains("/exterior/", StringComparison.OrdinalIgnoreCase) == true;

    static void MapModels(WebApplication app, GameInstall? install)
    {
        app.MapGet("/api/models/status", (ModelLibrary models) => Results.Ok(new
        {
            available = models.Available,
            stamp = models.Stamp,
        }));

        // What the 3D models page lists: the hulls a pilot can fly, and the
        // personal weapons that have a name in the game's text - the rest are
        // NPC variants and test pieces sharing a model with a named one.
        app.MapGet("/api/models/list", (ModelLibrary models, LogLibrary lib) =>
        {
            if (!models.Available) return Results.NotFound(new { message = "No game install to read models from." });
            var names = lib.GameCommodities;
            var entities = models.Catalogue.Entities.Values;
            var vehicles = entities
                .Where(e => e.Vehicle && !Unflown(e.Class))
                .Select(e => new
                {
                    cls = e.Class,
                    name = names.Vehicle(e.Class)?.Name is { Length: > 0 } n ? n : lib.ItemName(e.Class),
                    maker = e.Class.Split('_')[0],
                    paints = e.Paints.Count,
                })
                .OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var weapons = entities
                .Where(e => e.Type?.StartsWith("WeaponPersonal/", StringComparison.OrdinalIgnoreCase) == true && !Unflown(e.Class))
                .Select(e => new
                {
                    cls = e.Class,
                    name = lib.ItemName(e.Class),
                    maker = e.Class.Split('_')[0].ToUpperInvariant(),
                    paints = e.Paints.Count,
                })
                .Where(e => !e.name.Equals(e.cls, StringComparison.OrdinalIgnoreCase))
                // A finish is the same gun in another colour, on the same
                // model: list the gun once, under its plainest class.
                .GroupBy(e => entities.First(x => x.Class == e.cls).Geometry, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(e => e.cls.Length).First())
                .OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Results.Ok(new { stamp = models.Stamp, vehicles, weapons });
        });

        app.MapGet("/api/models/scene/{cls}", (string cls, ModelLibrary models, LogLibrary lib) =>
        {
            if (!models.Available) return Results.NotFound(new { message = "No game install to read models from." });
            var catalogue = models.Catalogue;
            if (catalogue.Find(cls) is not { } entity)
                return Results.NotFound(new { message = $"The install describes no model for {cls}." });

            var palettes = new Dictionary<string, ModelPalette>(StringComparer.OrdinalIgnoreCase);
            void Keep(string? palette)
            {
                if (palette is not null && catalogue.Palettes.TryGetValue(palette, out var p)) palettes[palette] = p;
            }
            Keep(entity.Palette);

            var paintNames = lib.GameCommodities.Paints
                .GroupBy(p => p.Item, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
            var paints = entity.Paints.Select(p =>
            {
                Keep(p.Palette);
                return new
                {
                    tag = p.Tag,
                    name = paintNames.GetValueOrDefault(p.Tag) ?? PaintName(p.Tag, cls),
                    palette = p.Palette,
                    material = p.Material,
                };
            }).ToList();

            object? Part(ModelLoadout entry, int depth, IReadOnlyDictionary<string, string>? helpers)
            {
                var item = entry.Class is null ? null : catalogue.Find(entry.Class);
                var type = item?.Type;
                var children = depth < 6
                    ? entry.Children.Select(c => Part(c, depth + 1, item?.Helpers)).Where(c => c is not null).ToList()
                    : [];
                if (item is null && children.Count == 0) return null;
                if (item is not null && !Visible(type, item.Geometry) && children.Count == 0) return null;
                if (item is not null) Keep(item.Palette);
                return new
                {
                    port = entry.Port,
                    helper = helpers?.GetValueOrDefault(entry.Port),
                    helpers = item?.Helpers,
                    cls = entry.Class,
                    name = entry.Class is null ? null : lib.ItemName(entry.Class),
                    type,
                    geometry = item?.Geometry,
                    material = item?.Material,
                    palette = item?.Palette,
                    children,
                };
            }

            var name = entity.Vehicle
                ? lib.GameCommodities.Vehicle(cls)?.Name is { Length: > 0 } n ? n : lib.ItemName(cls)
                : lib.ItemName(cls);

            return Results.Ok(new
            {
                stamp = models.Stamp,
                cls = entity.Class,
                name,
                vehicle = entity.Vehicle,
                type = entity.Type,
                geometry = entity.Geometry,
                material = entity.Material,
                palette = entity.Palette,
                helpers = entity.Helpers,
                paints,
                parts = entity.Loadout.Select(l => Part(l, 0, entity.Helpers)).Where(p => p is not null),
                palettes,
            });
        });

        app.MapGet("/api/models/mesh", (string path, ModelLibrary models, HttpContext ctx) =>
        {
            if (!Readable(path)) return Results.BadRequest();
            var mesh = models.Mesh(path);
            if (mesh is null) return Results.NotFound();
            Immutable(ctx);
            return Results.Bytes(mesh, "application/octet-stream");
        });

        app.MapGet("/api/models/material", (string path, ModelLibrary models, HttpContext ctx) =>
        {
            if (!Readable(path)) return Results.BadRequest();
            var file = path.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) ? path : path + ".mtl";
            var material = models.Material(file);
            if (material is null) return Results.NotFound();
            Immutable(ctx);
            return Results.Ok(material);
        });

        app.MapGet("/api/models/texture", (string path, ModelLibrary models, HttpContext ctx, bool? gloss, int? max) =>
        {
            if (!Readable(path) || models.Archive is not { } archive) return Results.BadRequest();
            var dds = archive.TryReadTexture(path, Math.Clamp(max ?? 2048, 64, 4096), gloss ?? false);
            if (dds is null) return Results.NotFound();
            Immutable(ctx);
            return Results.Bytes(dds, "image/vnd-ms.dds");
        });

        // Every answer above is a function of the game files and the path, and
        // the page asks with the archive's stamp in the query, so a patch is a
        // new URL rather than a stale cache.
        static void Immutable(HttpContext ctx) =>
            ctx.Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        // Paths come from the page; the archive index only holds the model,
        // material and texture folders, so anything else misses anyway. This
        // keeps the obvious nonsense out of the logs.
        static bool Readable(string? path) =>
            !string.IsNullOrWhiteSpace(path) && path.Length < 400 && !path.Contains("..", StringComparison.Ordinal);

        // Test beds, AI-only hulls and the like carry vehicle records too; a
        // pilot browsing hulls wants the ones they can fly.
        static bool Unflown(string cls)
        {
            var c = cls.ToLowerInvariant();
            return c.Contains("_ai_") || c.EndsWith("_ai", StringComparison.Ordinal) || c.Contains("test") || c.Contains("template")
                || c.Contains("_pu_") || c.Contains("_npc") || c.Contains("_unmanned") || c.Contains("_derelict")
                || c.Contains("_wreck") || c.Contains("_dummy") || c.Contains("_tutorial") || c.Contains("_eaobjective")
                || c.Contains("_s42") || c.Contains("_fw_") || c.Contains("_drone") || c.Contains("_hijacked")
                || c.Contains("_boarded") || c.Contains("_crash") || c.Contains("_salvage") || c.Contains("_indestructible")
                || c.Contains("_display") || c.Contains("debris") || c.Contains("salvageable") || c.Contains("_hangar")
                || c.EndsWith("_pu", StringComparison.Ordinal) || c.Contains("_temp");
        }

        // Paint_Corsair_Black_Black_Red -> "Black Black Red".
        static string PaintName(string tag, string cls)
        {
            var parts = tag.Split('_', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count > 0 && parts[0].Equals("Paint", StringComparison.OrdinalIgnoreCase)) parts.RemoveAt(0);
            var model = cls.Split('_').Skip(1).FirstOrDefault();
            if (parts.Count > 1 && model is not null && parts[0].Equals(model, StringComparison.OrdinalIgnoreCase)) parts.RemoveAt(0);
            return string.Join(' ', parts);
        }
    }
}
