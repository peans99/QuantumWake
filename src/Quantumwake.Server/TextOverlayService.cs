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
    IReadOnlyList<TextOverlayLine> Changes,
    string? Problem,
    int Annotated = 0,
    TextOverlayOptions? Options = null,
    int Filled = 0);

/// <summary>Whether the loose text file the game reads has fallen behind the game's own.</summary>
/// <param name="Present">Whether there is a loose file at all; with none the game uses its own and nothing is behind.</param>
/// <param name="Owner">Whose file it is: "overlay", "overlay+StarStrings", "StarStrings" or "unknown".</param>
/// <param name="Missing">Keys the game's table has and the file lacks.</param>
/// <param name="Sample">A few of those keys, so the notice can show what kind of thing is missing.</param>
public sealed record TextFreshness(bool Present, string Owner, int Missing, IReadOnlyList<string> Sample);

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
    ItemLabelStore options,
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
    /// <summary>Builds against the player's own choice of marks.</summary>
    private TextOverlayPlan Plan(string ini) =>
        TextOverlay.Build(ini, SoldTest(), library.GameCommodities.ItemFacts, options.Current);

    private Func<string, bool> SoldTest()
    {
        var receipts = library.Receipts();

        return itemClass =>
            receipts.ContainsKey(itemClass)
            || uex.ItemMarket(library.ItemUuid(itemClass)).Count > 0;
    }

    /// <summary>Reads the table the overlay should be built on.</summary>
    /// <returns>The text and a human name for where it came from, or a problem.</returns>
    private (string? Ini, string Source, string? Problem) BaseTable(GameInstall game)
    {
        // What is underneath OUR file, when ours is the one installed. The live
        // table is not the base in that case - it is this build's own output,
        // and reading it would preview a second set of marks on every name.
        // Install never reaches this, because it takes itself out first; Status
        // must not, so it asks the backup instead.
        if (store.StillPresent()
            && store.Current?.Files.FirstOrDefault(f => f.Backup is { Length: > 0 }) is { } ours
            && File.Exists(ours.Backup!))
        {
            try
            {
                return (GameText.WithoutBom(File.ReadAllText(ours.Backup!)),
                    store.Current.Layered ? "StarStrings" : "the game", null);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log.LogWarning(e, "displaced table unreadable");
            }
        }

        // Layered: an installed text mod's file is the base, so both survive.
        if (starStrings.StillPresent()
            && starStrings.Current?.Files.FirstOrDefault(f =>
                f.Path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)) is { } theirs)
        {
            try
            {
                return (GameText.WithoutBom(File.ReadAllText(theirs.Path)), "StarStrings", null);
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
            : (GameText.WithoutBom(Encoding.UTF8.GetString(raw)), "the game", null);
    }

    // The game's own table, read once per archive: it is 10 MB out of a 150 GB
    // archive, and both the notice and the page ask for it.
    private readonly Lock _gameTableGate = new();
    private (long Stamp, string Ini)? _gameTable;
    private (string Key, TextFreshness Result)? _freshness;

    private string? GameTable(GameInstall game)
    {
        var archive = P4kArchive.PathFor(game.RootPath);
        if (!File.Exists(archive)) return null;

        var stamp = new FileInfo(archive).LastWriteTimeUtc.Ticks;

        lock (_gameTableGate)
        {
            if (_gameTable is { } cached && cached.Stamp == stamp) return cached.Ini;
        }

        try
        {
            var raw = new P4kArchive(archive).TryRead(LocalisationEntry);
            if (raw is null) return null;

            var ini = GameText.WithoutBom(Encoding.UTF8.GetString(raw));
            lock (_gameTableGate) _gameTable = (stamp, ini);
            return ini;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            log.LogWarning(e, "game text table unreadable");
            return null;
        }
    }

    /// <summary>
    /// A base that is not the game's own - StarStrings, or the table ours
    /// displaced - with the game's current strings it lacks added. Without
    /// this, reinstalling over a text mod written before a patch rebuilt the
    /// same gap: 4.10.2's new contract titles stayed raw keys however often
    /// the marks were reapplied.
    /// </summary>
    private (string Ini, int Filled) Filled(GameInstall game, string ini, string source)
    {
        if (source == "the game" || GameTable(game) is not { } gameIni) return (ini, 0);
        return (TextTables.WithMissing(ini, gameIni, out var added), added);
    }

    /// <summary>
    /// Whether the loose file the game reads lacks strings the game now has.
    /// Cached on the archive's and the file's write times, so asking on every
    /// page load costs nothing after the first.
    /// </summary>
    public TextFreshness Freshness(GameInstall? game)
    {
        if (game is null) return new(false, "unknown", 0, []);

        var loose = Path.Combine(game.RootPath, LooseRelative);
        if (!File.Exists(loose)) return new(false, "unknown", 0, []);

        var owner = store.StillPresent()
            ? (store.Current?.Layered == true ? "overlay+StarStrings" : "overlay")
            : starStrings.StillPresent() ? "StarStrings" : "unknown";

        var archive = P4kArchive.PathFor(game.RootPath);
        var info = new FileInfo(loose);
        var key = $"{(File.Exists(archive) ? new FileInfo(archive).LastWriteTimeUtc.Ticks : 0)}|{info.LastWriteTimeUtc.Ticks}|{info.Length}|{owner}";

        lock (_gameTableGate)
        {
            if (_freshness is { } cached && cached.Key == key) return cached.Result;
        }

        if (GameTable(game) is not { } gameIni) return new(true, owner, 0, []);

        TextFreshness result;
        try
        {
            var missing = TextTables.Missing(GameText.WithoutBom(File.ReadAllText(loose)), gameIni);
            result = new(true, owner, missing.Count,
                missing.Take(5).Select(l => TextTables.KeyOf(l) ?? "").Where(k => k.Length > 0).ToList());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The game holds the file open while it runs; unreadable is not stale.
            log.LogWarning(e, "loose text table unreadable");
            return new(true, owner, 0, []);
        }

        lock (_gameTableGate) _freshness = (key, result);
        return result;
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

        var (complete, filled) = Filled(game, ini, source);
        var plan = Plan(complete);

        return new(installed, install?.InstalledAt, install?.Layered ?? false,
            source, plan.Marked, plan.Sold, plan.Skipped, plan.Changes, null, plan.Annotated,
            options.Current, filled);
    }

    /// <summary>Writes the overlay into the game folder.</summary>
    /// <returns>What was installed, or a sentence saying why nothing was.</returns>
    public (TextOverlayInstall? Install, string? Problem) Install(GameInstall? game)
    {
        if (game is null)
            return (null, "No game install was found, so there is nothing to write to.");

        // The same fence StarStrings is held to: judged on the path it resolves
        // to, and refused if it lands anywhere but the two allowed places.
        var target = StarStringsArchive.TargetFor(LooseRelative, game.RootPath);

        if (target is null)
            return (null, "The localisation path did not resolve inside the game folder, so nothing was written.");

        // Out first, and before the base is read. Reading first meant a rebuild
        // took its own last output as the table to mark up, and marked it again:
        // the file StarStrings is recorded at is the live one, which by then had
        // these marks in it.
        if (!Remove())
        {
            return (null,
                "The file this replaced could not be put back, so nothing new was written. "
                + "The marks are still installed and can be removed again.");
        }

        var (ini, source, problem) = BaseTable(game);

        if (ini is null)
            return (null, problem);

        // Built on the complete table, so a string the base was missing is
        // there to be marked as well as to be read.
        var plan = Plan(Filled(game, ini, source).Ini);

        if (plan.Marked == 0 && plan.Annotated == 0)
            return (null, "Nothing would be marked, so there is no reason to write a file.");

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

            // UTF-8 with the byte order mark, because that is what the game's
            // own file is and this one replaces it. The BOM used to depend on
            // where the base table came from: read out of Data.p4k with
            // Encoding.UTF8.GetString it survived into the text and was written
            // back, but File.ReadAllText - the path taken whenever StarStrings
            // is installed or our own backup is the base - strips the preamble,
            // so those installs wrote a file whose first three bytes differed
            // from the game's. Emitting it here makes the result the same
            // whichever base was used; GameText.WithoutBom keeps the source
            // from contributing a second one.
            File.WriteAllText(target, plan.Content, new UTF8Encoding(true));

            /*
             * Fingerprinted after the write, so a later mod overwriting this
             * path shows up as gone rather than as still installed - and taken
             * with TryFingerprint, because the empty string Fingerprint returns
             * on a locked file is not a fingerprint. Recorded as one it matched
             * nothing ever after, so Presence() answered Ours for ever and a
             * later Remove would have copied our backup over another mod's
             * file. Fingerprint's own doc says callers deciding whether to
             * discard a record must not accept it; this was one.
             */
            install = install with
            {
                Fingerprint = TextOverlayStore.TryFingerprint(target, out var written) ? written : null,
            };

            store.Record(install);

            return (install, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(e, "text overlay install failed");
            Remove();
            return (null, "The file could not be written, so anything already changed was put back.");
        }
    }

    /// <summary>
    /// Runs something that rewrites the localisation file with our layer lifted
    /// out of the way, then puts our layer back on top of whatever it left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both this and StarStrings write one file, and each backs up whatever it
    /// finds there. Installing StarStrings while our marks are down therefore
    /// records the <em>marked</em> file as "the original" - so removing both
    /// afterwards restores the marked file and leaves the game permanently
    /// marked, with both stores reporting nothing installed. It is not
    /// recoverable through the UI, because neither store believes it has
    /// anything left to undo.
    /// </para>
    /// <para>
    /// StarStrings already lifts its own previous install for exactly this
    /// reason - see the comment in <c>StarStrings.InstallAsync</c>. This is the
    /// same rule applied across the two mods rather than within one.
    /// </para>
    /// <para>
    /// Failing to lift is fatal to the operation rather than a warning: going
    /// ahead is what creates the unrecoverable state.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The work's own problem, if any, and whether our marks went back on.
    /// </returns>
    public async Task<(string? Problem, bool Relabelled)> WhileLiftedAsync(
        GameInstall? game, Func<Task<string?>> work)
    {
        var live = store.StillPresent();

        if (live && !Remove())
        {
            return ("The item labels could not be taken off first, so nothing was changed. "
                + "Close the game and anything else reading its localisation file, then try again.", false);
        }

        /*
         * try/finally, because the marks have already come off. StarStrings'
         * InstallAsync throws InvalidDataException on a corrupt archive -
         * outside its own catch - and without this the request 500s with the
         * pilot's labels uninstalled and their record deleted. Whatever the
         * work does, the layer that was lifted goes back on.
         */
        string? problem;

        try
        {
            problem = await work();
        }
        catch
        {
            if (live) Install(game);
            throw;
        }

        if (!live)
            return (problem, false);

        var (again, trouble) = Install(game);

        return (problem, trouble is null && again is not null);
    }

    /// <summary>
    /// Puts back whatever this displaced.
    /// </summary>
    /// <returns>
    /// False when something could not be put back. The record is kept in that
    /// case, because forgetting it would leave a changed file in somebody's game
    /// folder with nothing left that knows how to undo it.
    /// </returns>
    public bool Remove()
    {
        var install = store.Current;

        if (install is null)
            return true;

        var presence = store.Presence();

        // Not knowing is not the same as knowing it is gone. A file held open -
        // by the game, a text editor, a virus scanner mid-pass - reads exactly
        // like a file somebody replaced, and forgetting the record on that
        // leaves a marked table in the game folder with nothing left that can
        // undo it. So the record stays and the caller is told to try again.
        if (presence == OverlayPresence.Unreadable)
        {
            log.LogWarning("could not read {Path}; keeping the removal record", install.Files[0].Path);
            return false;
        }

        // Somebody else's file is there now - StarStrings installed over this
        // one, or a patch replaced it. The backup describes what was under OUR
        // file, which is no longer what is under theirs, so restoring it would
        // undo their install rather than ours.
        if (presence != OverlayPresence.Ours)
        {
            store.Forget();
            return true;
        }

        var restored = true;

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
                restored = false;
            }
        }

        if (restored) store.Forget();

        return restored;
    }
}
