using System.Text;
using Quantumwake.Core.GameData;
using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>The overlay's state, and what installing would change.</summary>
public sealed record TextOverlayStatus(
    bool Installed,
    DateTimeOffset? InstalledAt,
    bool Layered,
    string BaseSource,
    int Marked,
    int Sold,
    int Skipped,
    IReadOnlyList<TextOverlayLine> Samples,
    string? Problem);

/// <summary>
/// Builds and installs the in-game text overlay.
/// </summary>
/// <remarks>
/// <para>
/// This and <see cref="StarStrings"/> write the same file, so the base is chosen
/// rather than assumed: when StarStrings is installed the overlay is layered on
/// top of its table, and when it is not the game's own is read out of
/// <c>Data.p4k</c>. Building on the game's file while StarStrings is present
/// would silently revert their mod, which is the sort of thing nobody notices
/// until a contract stops carrying its reputation tag.
/// </para>
/// <para>
/// Nothing is written by asking what would change. The page shows the plan and
/// installing is a separate, explicit act - the file lands in someone else's
/// game folder, so it is not a thing to do on the way past.
/// </para>
/// </remarks>
public sealed class TextOverlayService(
    LogLibrary library,
    UexData uex,
    TextOverlayStore store,
    StarStringsStore starStrings,
    ILogger<TextOverlayService> log)
{
    private const string LocalisationEntry = @"Data\Localization\english\global.ini";

    /// <summary>Where the game reads a loose table from, relative to the install.</summary>
    private const string LooseRelative = @"data\localization\english\global.ini";

    /// <summary>
    /// Whether anything is known to sell an item, in confidence order.
    /// </summary>
    /// <remarks>
    /// A receipt settles it: the game charged for the thing. UEX is broader and
    /// crowd-sourced, and misses 29 of the 106 items this install's logs prove
    /// were bought at a kiosk - which is exactly why the receipts are consulted
    /// and not merely the market table.
    /// </remarks>
    private Func<string, bool> SoldTest()
    {
        var receipts = library.Receipts();

        return itemClass =>
            receipts.ContainsKey(itemClass)
            || uex.ItemMarket(library.Community.Item(itemClass)?.Uuid).Count > 0;
    }

    /// <summary>Reads the table the overlay should be built on.</summary>
    /// <returns>The text and a human name for where it came from, or a problem.</returns>
    private (string? Ini, string Source, string? Problem) BaseTable(GameInstall game)
    {
        // Layered: an installed text mod's file is the base, so both survive.
        if (starStrings.StillPresent()
            && starStrings.Current?.Files.FirstOrDefault(f =>
                f.Path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)) is { } theirs)
        {
            try
            {
                return (File.ReadAllText(theirs.Path), "StarStrings", null);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log.LogWarning(e, "StarStrings table unreadable");
                return (null, "StarStrings", "StarStrings looks installed but its text file could not be read.");
            }
        }

        var archive = P4kArchive.PathFor(game.RootPath);

        if (!File.Exists(archive))
            return (null, "the game", $"The game's data archive is not where this app expects it: {archive}");

        var raw = new P4kArchive(archive).TryRead(LocalisationEntry);

        return raw is null
            ? (null, "the game", "The game's text table could not be read out of Data.p4k.")
            : (Encoding.UTF8.GetString(raw), "the game", null);
    }

    /// <summary>What installing would change. Writes nothing.</summary>
    public TextOverlayStatus Status(GameInstall? game)
    {
        var install = store.Current;
        var installed = store.StillPresent();

        if (game is null)
            return new(installed, install?.InstalledAt, install?.Layered ?? false,
                "the game", 0, 0, 0, [], "No game install was found, so there is nothing to build against.");

        var (ini, source, problem) = BaseTable(game);

        if (ini is null)
            return new(installed, install?.InstalledAt, install?.Layered ?? false,
                source, 0, 0, 0, [], problem);

        var plan = TextOverlay.Build(ini, SoldTest());

        return new(installed, install?.InstalledAt, install?.Layered ?? false,
            source, plan.Marked, plan.Sold, plan.Skipped, plan.Samples, null);
    }

    /// <summary>Writes the overlay into the game folder.</summary>
    /// <returns>What was installed, or a sentence saying why nothing was.</returns>
    public (TextOverlayInstall? Install, string? Problem) Install(GameInstall? game)
    {
        if (game is null)
            return (null, "No game install was found, so there is nothing to write to.");

        var (ini, source, problem) = BaseTable(game);

        if (ini is null)
            return (null, problem);

        var plan = TextOverlay.Build(ini, SoldTest());

        if (plan.Marked == 0)
            return (null, "Nothing would be marked, so there is no reason to write a file.");

        // The same fence StarStrings is held to: judged on the path it resolves
        // to, and refused if it lands anywhere but the two allowed places.
        var target = StarStringsArchive.TargetFor(LooseRelative, game.RootPath);

        if (target is null)
            return (null, "The localisation path did not resolve inside the game folder, so nothing was written.");

        // Take our own previous install out first, so this one's backup is of
        // whatever is genuinely underneath rather than of our own last file.
        Remove();

        var layered = source == "StarStrings";
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        string? backup = null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (File.Exists(target))
            {
                backup = Path.Combine(store.BackupRoot, stamp, "global.ini");
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(target, backup, overwrite: true);
            }

            // Recorded before the write: a write that fails partway through has
            // still changed the file, and a record added only on success would
            // leave that file - and its backup - outside the rollback.
            var install = new TextOverlayInstall(
                DateTimeOffset.UtcNow, game.RootPath, plan.Marked, layered,
                [new InstalledFile(target, backup)]);

            store.Record(install);

            File.WriteAllText(target, plan.Content, new UTF8Encoding(false));

            return (install, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(e, "text overlay install failed");
            Remove();
            return (null, "The file could not be written, so anything already changed was put back.");
        }
    }

    /// <summary>Puts back whatever the overlay displaced.</summary>
    public void Remove()
    {
        var install = store.Current;

        if (install is null)
            return;

        foreach (var file in install.Files)
        {
            try
            {
                if (file.Backup is { } backup && File.Exists(backup))
                    File.Copy(backup, file.Path, overwrite: true);
                else if (File.Exists(file.Path))
                    File.Delete(file.Path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log.LogWarning(e, "could not restore {Path}", file.Path);
            }
        }

        store.Forget();
    }
}
