using System.Globalization;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>One line of the combat log: a lock that ended, or a callout made in a fight.</summary>
public sealed record CombatEntry(LockRecord? Lock = null, CombatCallout? Said = null);

/// <summary>
/// Locks and callouts close enough together to be one engagement, as the
/// Sessions debrief shows it.
/// </summary>
/// <param name="Start">The first lock or callout.</param>
/// <param name="End">The last.</param>
/// <param name="Locks">Every lock kept, in order.</param>
/// <param name="Said">Every callout made in it, in order - the HUD's as well as the panel's.</param>
/// <param name="Named">Locks whose hull the panel printed.</param>
/// <param name="Likely">Locks left unnamed that the signature favoured one hull for.</param>
/// <param name="Hostile">Locks marked hostile.</param>
/// <param name="Stopped">Locks seen moving and then sat at 0 m/s.</param>
/// <param name="Missiles">Missile warnings seen.</param>
/// <param name="LowestObjective">The lowest band an escort or objective's health was called below, in percent; null when none was.</param>
public sealed record Fight(
    DateTimeOffset Start,
    DateTimeOffset End,
    IReadOnlyList<LockRecord> Locks,
    IReadOnlyList<CombatCallout> Said,
    int Named,
    int Likely,
    int Hostile,
    int Stopped,
    int Missiles,
    int? LowestObjective);

/// <summary>
/// What the live reader saw of each fight, kept for the Sessions debrief once
/// the pilot has turned the record on (1.2.21).
/// </summary>
/// <remarks>
/// <para>
/// Until 1.2.21 the combat watch held only the current lock and the last few
/// callouts, in memory, and a fight was gone when it ended - 1,390 target-panel
/// frames read on this install between 6 and 9 November 2026, and nothing of
/// them afterwards. The log cannot stand in for it: <c>Game.log</c> names the
/// hulls that streamed in, never the one locked, whether it was hostile, or what
/// the HUD warned of.
/// </para>
/// <para>
/// Readings, never pictures, and only the game's words: a name is kept only
/// when it is a hull and a faction only when it is one of the game's (see
/// <see cref="CombatWatch"/>), because the panel labels a player's ship with
/// the player. One JSON line an entry, a file a local day in the data folder's
/// <c>combat</c> folder, appended so a crash loses at most the line being
/// written.
/// </para>
/// </remarks>
public sealed partial class CombatLog(string folder)
{
    /// <summary>Further apart than this, two locks are two fights.</summary>
    public static readonly TimeSpan Gap = TimeSpan.FromMinutes(2);

    private readonly DailyJsonLines<CombatEntry> _days = new(folder, At);

    /// <summary>A lock that ended.</summary>
    public void Add(LockRecord ended) => _days.Add(new CombatEntry(Lock: ended));

    /// <summary>A callout made in a fight.</summary>
    public void Add(CombatCallout said) => _days.Add(new CombatEntry(Said: said));

    /// <summary>Every entry between two times, oldest first.</summary>
    public IReadOnlyList<CombatEntry> Between(DateTimeOffset from, DateTimeOffset to) => _days.Between(from, to);

    /// <summary>Whether anything has been kept at all.</summary>
    public bool Any() => _days.Any();

    /// <summary>Deletes every fight kept; returns how many days of them went.</summary>
    public int Forget() => _days.Forget();

    /// <summary>
    /// Entries grouped into fights. A fight is anchored on what only a fight
    /// produces - a lock, a missile, an objective's health falling - and the
    /// territory banner joins one only when it falls inside: hostile territory
    /// is also where rocks are mined in peace.
    /// </summary>
    public static IReadOnlyList<Fight> Fights(IEnumerable<CombatEntry> entries)
    {
        var ordered = entries.OrderBy(At).ToList();
        var groups = new List<List<CombatEntry>>();
        List<CombatEntry>? current = null;
        DateTimeOffset end = default;

        foreach (var entry in ordered.Where(Anchors))
        {
            if (current is null || At(entry) - end > Gap)
            {
                current = [];
                groups.Add(current);
            }
            current.Add(entry);
            var until = entry.Lock?.Until ?? At(entry);
            if (until > end) end = until;
        }

        var fights = new List<Fight>();
        foreach (var group in groups)
        {
            var start = group.Min(At);
            var last = group.Max(e => e.Lock?.Until ?? At(e));

            // The banner and anything else said within the fight's span joins it.
            var said = ordered
                .Where(e => e.Said is not null && At(e) >= start - TimeSpan.FromSeconds(30) && At(e) <= last)
                .Select(e => e.Said!)
                .ToList();
            var locks = group.Where(e => e.Lock is not null).Select(e => e.Lock!).ToList();

            int? lowest = said
                .Where(s => s.Kind == "objective-health")
                .Select(s => Percent().Match(s.Text))
                .Where(m => m.Success)
                .Select(m => (int?)int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                .Min();

            fights.Add(new Fight(
                said.Count > 0 && said.Min(s => s.At) < start ? said.Min(s => s.At) : start,
                last,
                locks,
                said,
                locks.Count(l => l.Name is not null),
                locks.Count(l => l.Name is null && l.Likely is not null),
                locks.Count(l => l.Stance == "hostile"),
                locks.Count(l => l.Stopped),
                said.Count(s => s.Kind == "missile"),
                lowest));
        }

        return fights;
    }

    private static bool Anchors(CombatEntry entry) =>
        entry.Lock is not null || entry.Said?.Kind is "missile" or "objective-health";

    private static DateTimeOffset At(CombatEntry entry) => entry.Lock?.Since ?? entry.Said?.At ?? default;

    [GeneratedRegex(@"(\d+)\s*percent")]
    private static partial Regex Percent();
}
