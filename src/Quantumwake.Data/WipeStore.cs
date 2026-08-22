using System.Text.Json;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>
/// What a wipe took, which is not always everything.
/// </summary>
/// <remarks>
/// CIG wipes to different depths. A patch may reset money and leave hangars
/// alone, clear inventories without touching balances, or take the lot. Filing
/// every one of them as "a wipe" and hiding all history is wrong in the common
/// case: after a money-only wipe your ships and the places you have been are
/// still yours, and blanking them costs the player real history for nothing.
/// </remarks>
[Flags]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum WipeScope
{
    None = 0,

    /// <summary>aUEC: spending, income, the ledger and cargo takings.</summary>
    Money = 1,

    /// <summary>The hangar: what you own, and the fleet count over time.</summary>
    Ships = 2,

    /// <summary>Stashes, the kit you are wearing, and what you have picked up.</summary>
    Inventory = 4,

    /// <summary>
    /// Where you have been and what you have flown - not account state at all,
    /// so this is only ticked for a wipe the player wants drawn as a clean
    /// start rather than as a reset of what they hold.
    /// </summary>
    History = 8,

    Everything = Money | Ships | Inventory | History,
}

/// <summary>When the game last wiped, what patch did it, and how deep it went.</summary>
/// <param name="At">
/// Sessions that started before this are still stored and still parsed - they
/// are simply not counted. Nothing about a wipe destroys the logs, only what
/// they add up to.
/// </param>
/// <param name="Patch">The patch the wipe came with, for the page to name.</param>
/// <param name="Scope">
/// Which totals begin again here. Anything outside it reaches back through the
/// wipe as if it had not happened, because for that number it did not.
/// </param>
public sealed record Wipe(DateTimeOffset At, string Patch, WipeScope Scope = WipeScope.Everything);

/// <summary>
/// The line a wipe draws under the player's history.
/// </summary>
/// <remarks>
/// <para>
/// A data wipe resets money, ships and inventory. Every total this app reports
/// - what you own, what you have earned, what is in your stashes - is answering
/// a question about the account you are playing now, and a wipe means the logs
/// before it describe a different one. Adding pre-wipe sales to post-wipe
/// income does not produce a bigger number, it produces a wrong one.
/// </para>
/// <para>
/// So the library reads only sessions from the wipe onwards, and the date is a
/// setting rather than a constant: CIG decides when wipes happen, players
/// disagree about whether a partial wipe counts, and someone reviewing an older
/// patch should be able to wind it back and see everything again. Nothing is
/// ever deleted - moving the date restores the history in full.
/// </para>
/// </remarks>
public sealed class WipeStore
{
    /// <summary>
    /// Alpha 4.8, which wiped, as dated from this install's own logs.
    /// </summary>
    /// <remarks>
    /// The last 4.7 session on this machine was 11 May 2026 and the first 4.8
    /// one was the 15th, so that is when the patch - and its wipe - landed.
    /// A default derived from evidence rather than a remembered date, and one
    /// the player can correct on the Settings page.
    /// </remarks>
    public static readonly Wipe Default = new(
        new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero),
        "Alpha 4.8",
        WipeScope.Everything);

    private readonly string _path;
    private readonly Lock _gate = new();
    private Wipe _wipe = Default;

    public WipeStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "wipe.json");
        Load();
    }

    public Wipe Current
    {
        get { lock (_gate) return _wipe; }
    }

    /// <summary>
    /// Moves the line, or removes it entirely when given no date.
    /// </summary>
    /// <remarks>
    /// A future date would hide everything and leave a dashboard of zeroes with
    /// no explanation, so it is refused; the caller is told what was kept.
    /// </remarks>
    /// <param name="scope">
    /// What the wipe took. <see cref="WipeScope.None"/> would be a wipe that
    /// changed nothing, which is a date with no meaning, so it is read as a
    /// full one - the same answer as leaving it unsaid.
    /// </param>
    public Wipe Set(DateTimeOffset? at, string? patch, WipeScope? scope = null)
    {
        var depth = scope is null or WipeScope.None ? WipeScope.Everything : scope.Value;

        var wanted = at is null
            ? new Wipe(DateTimeOffset.MinValue, "no wipe", depth)
            : new Wipe(
                at.Value > DateTimeOffset.UtcNow ? _wipe.At : at.Value,
                string.IsNullOrWhiteSpace(patch) ? "set by hand" : patch.Trim(),
                depth);

        lock (_gate)
        {
            _wipe = wanted;
            Save();
            return _wipe;
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _wipe = JsonSerializer.Deserialize<Wipe>(File.ReadAllText(_path)) ?? Default;
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // A corrupt file falls back to the known wipe rather than to none:
            // showing pre-wipe history as if it counted is the worse failure.
            _wipe = Default;
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_wipe));
    }
}
