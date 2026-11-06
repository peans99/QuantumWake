namespace Quantumwake.Data;

/// <summary>One thing the combat watch said, or would have said with the voice on.</summary>
/// <param name="At">When.</param>
/// <param name="Kind">identified, likely, hostile, disabled.</param>
/// <param name="Text">The sentence, as spoken.</param>
public sealed record CombatCallout(DateTimeOffset At, string Kind, string Text);

/// <summary>The ship locked right now, as the watch has settled it.</summary>
/// <param name="Name">The name the panel printed, once it printed one.</param>
/// <param name="Candidates">
/// Rough odds for the hull while the panel says UNKNOWN - signature, ships
/// seen nearby, faction; see <see cref="ShipSignatureBook.Weigh"/>. Empty once named.
/// </param>
/// <param name="Faction">The faction line's value, when it had one.</param>
/// <param name="Stance">hostile, friendly, neutral or unread - the colour of its label in the world.</param>
/// <param name="SpeedMs">Its last speed.</param>
/// <param name="RangeKm">Its last range.</param>
/// <param name="Em">Electromagnetic signature.</param>
/// <param name="Ir">Infrared signature.</param>
/// <param name="CrossSection">Cross-section, the median of what this lock has read.</param>
/// <param name="Disabled">It was moving and has sat at 0 m/s since.</param>
/// <param name="Since">When this lock was first seen.</param>
/// <param name="SeenAt">When the panel was last read.</param>
public sealed record CombatTarget(
    string? Name,
    IReadOnlyList<HullOdds> Candidates,
    string? Faction,
    string Stance,
    double? SpeedMs,
    double? RangeKm,
    double? Em,
    double? Ir,
    double? CrossSection,
    bool Disabled,
    DateTimeOffset Since,
    DateTimeOffset SeenAt);

/// <summary>What the combat page shows.</summary>
/// <param name="Combat">A hostile lock has been seen in the last half minute.</param>
/// <param name="Target">The current lock, or null.</param>
/// <param name="Callouts">The last few things said, newest first.</param>
/// <param name="Locks">Locks seen since the app started.</param>
/// <param name="HostileLocks">Of those, how many were marked hostile.</param>
public sealed record CombatPicture(
    bool Combat,
    CombatTarget? Target,
    IReadOnlyList<CombatCallout> Callouts,
    int Locks,
    int HostileLocks);

/// <summary>
/// Turns target panels read a second apart into the few moments worth saying
/// out loud: a target named, a hostile lock, a target stopped dead.
/// </summary>
/// <remarks>
/// <para>
/// Speaks on changes, never on states. A panel read every second would
/// otherwise say "hostile" every second, and the engine misreads often enough
/// that anything said on one frame would be said wrong a few times a fight -
/// so each callout waits for two frames that agree.
/// </para>
/// <para>
/// "Disabled" is the claim most likely to be wrong, and it is held to the
/// most. A target at 0 m/s is not disabled on that alone: the Idris-P in the
/// 2026-11-05 recording sat at 0 m/s for the whole defence, and so did a
/// turret. So it is said only of a lock that was seen moving and then sits at
/// zero for two reads - which is what a ship with its engines shot out does,
/// and also what a ship that has simply stopped does. The voice says
/// "disabled" because that is the pilot's word for it; the page says what was
/// actually seen, stopped after moving.
/// </para>
/// </remarks>
public sealed class CombatWatch(ShipSignatureBook book)
{
    /// <summary>How long a lost panel is still the same lock: a glance away is not a new target.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(6);

    /// <summary>How long combat lasts after the last hostile lock was seen.</summary>
    private static readonly TimeSpan CombatFor = TimeSpan.FromSeconds(30);

    /// <summary>A target counts as having moved once it is read above this.</summary>
    private const double Moving = 15;

    private readonly List<CombatCallout> _said = [];
    private Lock? _lock;
    private DateTimeOffset? _lastHostile;
    private int _locks, _hostileLocks;

    public ShipSignatureBook Book { get; set; } = book;

    private sealed class Lock
    {
        public DateTimeOffset Since, SeenAt;
        public string? Name, PendingName, Faction;
        public int NameAgreed;
        public readonly List<double> CrossSections = [];
        public double? Em, Ir, Speed, Range;
        public bool Moved, SaidName, SaidLikely, SaidHostile, SaidDisabled, Disabled, CountedHostile;
        public int Zeroes, HostileReads, Off, Unnamed;
        public TargetStance Stance = TargetStance.Unread;

        public double? Cs => CrossSections.Count == 0 ? null
            : CrossSections.Order().ElementAt(CrossSections.Count / 2);
    }

    /// <summary>
    /// One frame's panel - null when the frame had none - and the colour of
    /// the lock's label. Returns what is to be said now, oldest first.
    /// </summary>
    public IReadOnlyList<CombatCallout> See(TargetReading? panel, TargetStance stance, DateTimeOffset at)
    {
        var now = new List<CombatCallout>();

        // A printed name snapped onto the hull it is, when one is near enough:
        // RSIPERSEUS is the RSI Perseus, and one hull is one lock.
        if (panel?.Name is { } printed && Book.Hull(printed) is { } hull)
            panel = panel with { Name = hull };

        if (panel is null)
        {
            if (_lock is not null && at - _lock.SeenAt > Grace)
                _lock = null;
            return now;
        }

        // A cross-section far off the lock's is a new ship only when the next
        // read agrees: one misread digit - 48K for 4.8K - is not a new target.
        // A frame whose figure went unread says nothing either way, and leaves
        // the count where it was: the Perseus-to-Blade switch at 01:59:30 had
        // an unread figure between its two Blade reads.
        var off = _lock is not null && OffSignature(_lock, panel);
        if (_lock is not null && panel.CrossSection is not null) _lock.Off = off ? _lock.Off + 1 : 0;

        // A named lock reading UNKNOWN is another ship: the game does not
        // forget a name it has printed. Twice, so a misread name is not one.
        if (_lock is not null)
            _lock.Unnamed = _lock.Name is not null && panel.Unknown && panel.Name is null ? _lock.Unnamed + 1 : 0;

        if (_lock is null || NewName(_lock, panel) || _lock.Off >= 2 || _lock.Unnamed >= 2)
        {
            _lock = new Lock { Since = at };
            _locks++;
            off = false;
        }

        var l = _lock;
        l.SeenAt = at;
        l.Em = panel.Em ?? l.Em;
        l.Ir = panel.Ir ?? l.Ir;
        l.Range = panel.RangeKm ?? l.Range;
        l.Faction = panel.Faction ?? l.Faction;
        if (panel.CrossSection is { } cs && !off) l.CrossSections.Add(cs);

        // ---- named ----
        if (panel.Name is { } name)
        {
            if (string.Equals(name, l.PendingName, StringComparison.OrdinalIgnoreCase)) l.NameAgreed++;
            else { l.PendingName = name; l.NameAgreed = 1; }

            if (l.NameAgreed >= 2 && !string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                l.Name = name;

                // The same hull named again inside half a minute is the lock
                // flickering, or the pilot cycling back to it - not news.
                var heard = _said.LastOrDefault(c => c.Kind == "identified");
                var repeat = heard is not null && at - heard.At < TimeSpan.FromSeconds(30)
                    && heard.Text.EndsWith(Spoken(name), StringComparison.Ordinal);

                if (!l.SaidName && !repeat)
                {
                    l.SaidName = true;
                    now.Add(Say(at, "identified", $"Target identified: {Spoken(name)}"));
                }
                l.SaidName = true;
            }
        }
        else if (!l.SaidName && !l.SaidLikely && l.CrossSections.Count >= 2 && l.Cs is { } median)
        {
            // Two reads of the same figure, and odds clear enough to be worth
            // saying: one hull at 60% or better, or two that between them
            // make 80%. Past that it is a list for the page, not a callout.
            var odds = Odds(l, median);
            if (panel.CrossSection is { } last && Math.Abs(last - median) < 120 && Spoken(odds) is { } guess)
            {
                l.SaidLikely = true;
                now.Add(Say(at, "likely", $"Unknown contact, {guess}"));
            }
        }

        // ---- hostile ----
        if (stance != TargetStance.Unread) l.Stance = stance;
        l.HostileReads = stance == TargetStance.Hostile ? l.HostileReads + 1 : stance == TargetStance.Unread ? l.HostileReads : 0;

        // One read is enough here, unlike the name: the label is only looked
        // at on whole-frame reads, a few seconds apart, and red over a third
        // of a label's lit pixels is not something a misread produces.
        if (l.HostileReads >= 1)
        {
            _lastHostile = at;
            if (!l.CountedHostile) { l.CountedHostile = true; _hostileLocks++; }

            if (!l.SaidHostile)
            {
                l.SaidHostile = true;
                // A printed name is said as a fact; a hull the signature fits is
                // a guess, and is said as one - "likely", never bare.
                var what = (l.Name ?? l.PendingName) is { } n ? Spoken(n)
                    : l.Cs is { } lockCs && Spoken(Odds(l, lockCs)) is { } guess ? $"contact, {guess}"
                    : "contact";
                now.Add(Say(at, "hostile", $"Hostile {what}"));

                // Said with its name, it has been identified too; said with a
                // likely hull, the signature callout would only repeat it.
                if (l.Name is not null || l.PendingName is not null) l.SaidName = true;
                else if (what != "contact") l.SaidLikely = true;
            }
        }

        // Named and found hostile in the same frame is one sentence, not the
        // name twice: the Vanduul's faction arrives with its name.
        if (now.FirstOrDefault(c => c.Kind == "identified") is { } named
            && now.FirstOrDefault(c => c.Kind == "hostile") is { } hostile)
        {
            now.Remove(named);
            now.Remove(hostile);
            _said.Remove(named);
            _said.Remove(hostile);
            now.Add(Say(at, "hostile", $"Hostile target identified: {named.Text["Target identified: ".Length..]}"));
        }

        // ---- stopped ----
        if (panel.SpeedMs is { } speed)
        {
            l.Speed = speed;
            if (speed >= Moving) { l.Moved = true; l.Zeroes = 0; l.Disabled = false; }
            else if (speed < 1) l.Zeroes++;
            else l.Zeroes = 0;

            if (l.Moved && l.Zeroes >= 2)
            {
                l.Disabled = true;
                if (!l.SaidDisabled)
                {
                    l.SaidDisabled = true;
                    now.Add(Say(at, "disabled", "Target disabled"));
                }
            }
        }

        return now;
    }

    /// <summary>The ship classes the log saw lately; empty when nothing supplies them.</summary>
    public Func<IReadOnlySet<string>> Nearby { get; set; } = () => new HashSet<string>();

    private IReadOnlyList<HullOdds> Odds(Lock l, double cs) => Book.Weigh(cs, l.Faction, Nearby());

    /// <summary>
    /// The guess as said: "likely X" at 60% or better, "X or Y" when the two
    /// make 80%, null when the odds are too spread to say anything.
    /// </summary>
    private static string? Spoken(IReadOnlyList<HullOdds> odds) =>
        odds.Count == 0 ? null
        : odds[0].Chance >= 0.6 ? $"likely {odds[0].Name}"
        : odds.Count >= 2 && odds[0].Chance + odds[1].Chance >= 0.8 ? $"{odds[0].Name} or {odds[1].Name}"
        : null;

    /// <summary>What the page shows at <paramref name="at"/>.</summary>
    public CombatPicture Picture(DateTimeOffset at)
    {
        var l = _lock is not null && at - _lock.SeenAt <= Grace ? _lock : null;

        var target = l is null ? null : new CombatTarget(
            l.Name,
            l.Name is null && l.Cs is { } cs ? Odds(l, cs) : [],
            l.Faction,
            l.Stance.ToString().ToLowerInvariant(),
            l.Speed, l.Range, l.Em, l.Ir, l.Cs, l.Disabled, l.Since, l.SeenAt);

        return new CombatPicture(
            _lastHostile is { } h && at - h <= CombatFor,
            target,
            [.. Enumerable.Reverse(_said).Take(12)],
            _locks,
            _hostileLocks);
    }

    /// <summary>
    /// A different printed name. UNKNOWN turning into a name is the same ship
    /// being identified, not a new one.
    /// </summary>
    private static bool NewName(Lock l, TargetReading panel) =>
        l.Name is { } had && panel.Name is { } now
        && TargetPanel.Distance(had.ToUpperInvariant(), now.ToUpperInvariant()) > 2;

    /// <summary>A cross-section further from the lock's than misreading explains.</summary>
    private static bool OffSignature(Lock l, TargetReading panel) =>
        l.Cs is { } cs && panel.CrossSection is { } read && l.CrossSections.Count >= 2
        // The figure is steady within a lock (23.9K on every frame of one, 30 s),
        // and the Scythe and Glaive are 12% apart: 5% is room for a misread
        // decimal without swallowing the next hull.
        && Math.Abs(read - cs) > Math.Max(250, cs * 0.05);

    private CombatCallout Say(DateTimeOffset at, string kind, string text)
    {
        var callout = new CombatCallout(at, kind, text);
        _said.Add(callout);
        if (_said.Count > 50) _said.RemoveAt(0);
        return callout;
    }

    /// <summary>"AEGIS IDRIS-P" read aloud as a name rather than spelled as capitals.</summary>
    private static string Spoken(string name) =>
        name.Any(char.IsLower) ? name
            : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
}
