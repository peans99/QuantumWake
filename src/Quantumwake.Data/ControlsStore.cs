using System.Security.Cryptography;
using Quantumwake.Core;
using Quantumwake.Core.Controls;

namespace Quantumwake.Data;

/// <summary>One kept copy of the game's keybinding profile.</summary>
/// <param name="Id">The file name under the backups folder - <c>20260916-235612-417-a1b2c3d4</c>: when it was taken to the millisecond, then the hash, so the names sort as the history.</param>
/// <param name="TakenAt">When the copy was made, which is when the change was noticed, not when the game wrote it.</param>
/// <param name="WrittenAt">The file's own last-write time, which is when the game wrote it.</param>
/// <param name="Hash">The first eight hex digits of the content's SHA-256; two copies with one hash are the same profile.</param>
public sealed record ControlsBackup(string Id, DateTimeOffset TakenAt, DateTimeOffset WrittenAt, long Bytes, string Hash);

/// <summary>
/// Keeps every distinct version of <c>actionmaps.xml</c> the app has seen.
/// </summary>
/// <remarks>
/// <para>
/// The game rewrites the file whenever a binding changes and keeps no
/// history; a re-enumerated stick or a slip in the keybinding screen is
/// simply the new truth. Each version is kept whole, named by when it was
/// taken and a hash of its content, so a version is never kept twice and
/// the list reads as a history.
/// </para>
/// <para>
/// Read-only towards the game: nothing here writes under the install. A
/// restore is a file written where the game imports from, by the pilot's
/// hand, in <see cref="ControlsExport"/>.
/// </para>
/// </remarks>
public sealed class ControlsStore
{
    private readonly string _directory;
    private readonly Lock _gate = new();

    public ControlsStore(string? directory = null)
    {
        _directory = directory ?? AppPaths.In("controls", "backups");
    }

    public string Directory => _directory;

    /// <summary>
    /// Keeps the file if its content is new. Returns the backup, and whether
    /// this call made it: false when the latest kept copy already has this
    /// content, which is the case on every start where nothing changed.
    /// </summary>
    public (ControlsBackup Backup, bool Taken)? Snapshot(string profilePath)
    {
        byte[] bytes;
        DateTimeOffset writtenAt;
        try
        {
            if (!File.Exists(profilePath)) return null;
            bytes = File.ReadAllBytes(profilePath);
            writtenAt = new DateTimeOffset(File.GetLastWriteTimeUtc(profilePath), TimeSpan.Zero);
        }
        catch (IOException)
        {
            // The game had it open mid-write; the next look will get it whole.
            return null;
        }
        if (bytes.Length == 0) return null;

        return Keep(bytes, writtenAt, againstLatestOnly: true);
    }

    /// <summary>
    /// Keeps bytes as a version - the profile read from disk, or a file the
    /// pilot brought back.
    /// </summary>
    /// <param name="againstLatestOnly">
    /// The watch compares with the latest copy only, so that going back to
    /// an older version is kept again and the list stays a history of what
    /// the game had. A file brought back compares with every copy, so the
    /// same file added twice is one entry.
    /// </param>
    public (ControlsBackup Backup, bool Taken) Keep(byte[] bytes, DateTimeOffset writtenAt, bool againstLatestOnly = false)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes))[..8].ToLowerInvariant();

        lock (_gate)
        {
            var kept = Backups();
            var same = againstLatestOnly ? kept.FirstOrDefault() : kept.FirstOrDefault(b => b.Hash == hash);
            if (same is not null && same.Hash == hash) return (same, false);

            // Ids sort as text, and two copies kept in one millisecond would
            // sort by hash rather than by order - so "the latest" could name
            // the older one, and the watch would refuse a version the game had
            // genuinely gone back to. A fast machine keeps three in 2 ms; the
            // Linux runner did. The clock is nudged past the latest id instead.
            var takenAt = DateTimeOffset.UtcNow;
            // At the id's own precision, or a clock a few ticks past the
            // latest millisecond still formats to the same one.
            takenAt = takenAt.AddTicks(-(takenAt.Ticks % TimeSpan.TicksPerMillisecond));
            var latest = kept.FirstOrDefault();
            if (latest is not null && takenAt <= latest.TakenAt) takenAt = latest.TakenAt.AddMilliseconds(1);
            var id = $"{takenAt:yyyyMMdd-HHmmss-fff}-{hash}";
            System.IO.Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, id + ".xml");
            File.WriteAllBytes(path, bytes);
            File.SetLastWriteTimeUtc(path, writtenAt.UtcDateTime);
            return (new ControlsBackup(id, takenAt, writtenAt, bytes.Length, hash), true);
        }
    }

    /// <summary>Every kept version, newest first.</summary>
    public IReadOnlyList<ControlsBackup> Backups()
    {
        if (!System.IO.Directory.Exists(_directory)) return [];
        var list = new List<ControlsBackup>();
        foreach (var path in System.IO.Directory.EnumerateFiles(_directory, "*.xml"))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            // yyyyMMdd-HHmmss-fff-hash: anything else in the folder is not ours.
            if (id.Length != 28 || id[8] != '-' || id[15] != '-' || id[19] != '-') continue;
            if (!DateTimeOffset.TryParseExact(id[..19], "yyyyMMdd-HHmmss-fff", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var takenAt)) continue;
            var info = new FileInfo(path);
            list.Add(new ControlsBackup(id, takenAt, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), info.Length, id[20..]));
        }
        list.Sort((a, b) => string.CompareOrdinal(b.Id, a.Id));
        return list;
    }

    /// <summary>The kept bytes, or null for an id the folder does not hold.</summary>
    public byte[]? Read(string id)
    {
        if (id.Contains('/') || id.Contains('\\') || id.Contains("..")) return null;
        var path = Path.Combine(_directory, id + ".xml");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}

/// <summary>What changed between two profiles, binding by binding.</summary>
/// <param name="Action">The action, as <c>actionmap/action</c>.</param>
/// <param name="Before">The inputs the older profile bound it to; empty when it was not bound.</param>
/// <param name="After">The inputs the newer profile binds it to; empty when the binding went.</param>
public sealed record ControlsChange(string ActionMap, string Action, IReadOnlyList<string> Before, IReadOnlyList<string> After);

public static class ControlsDiff
{
    /// <summary>
    /// The actions whose bindings differ, oldest profile first. Inputs are
    /// compared as the file writes them, so a changed activation mode on the
    /// same input does not count - the page shows modes beside the input.
    /// </summary>
    public static IReadOnlyList<ControlsChange> Between(ControlProfile older, ControlProfile newer)
    {
        static Dictionary<(string, string), List<string>> Index(ControlProfile p)
        {
            var map = new Dictionary<(string, string), List<string>>();
            foreach (var b in p.Bindings)
            {
                if (!map.TryGetValue((b.ActionMap, b.Action), out var list)) map[(b.ActionMap, b.Action)] = list = [];
                list.Add(b.Input.Raw);
            }
            foreach (var list in map.Values) list.Sort(StringComparer.Ordinal);
            return map;
        }

        var before = Index(older);
        var after = Index(newer);
        var changes = new List<ControlsChange>();
        foreach (var key in before.Keys.Union(after.Keys).OrderBy(k => k.Item1).ThenBy(k => k.Item2))
        {
            var was = before.GetValueOrDefault(key) ?? [];
            var now = after.GetValueOrDefault(key) ?? [];
            if (was.SequenceEqual(now, StringComparer.Ordinal)) continue;
            changes.Add(new ControlsChange(key.Item1, key.Item2, was, now));
        }
        return changes;
    }
}
