using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>Which of the game's screens a frame turned out to be.</summary>
/// <remarks>
/// Decided from anchor text the game prints on the screen itself, never from
/// where the pilot said they were. Every value here was seen on a real frame
/// from this install; a screen nobody has photographed is <see cref="Unknown"/>
/// and stays that way until somebody has.
/// </remarks>
public enum ScreenKind
{
    /// <summary>Nothing on it this app knows how to read.</summary>
    Unknown,

    /// <summary>An item hovered somewhere, with the game's tooltip open.</summary>
    Tooltip,

    /// <summary>The Vehicle Loadout Manager: one ship, its ports, what is in them.</summary>
    Loadout,

    /// <summary>mobiGlas Maps, whose footer names the place and the position.</summary>
    Map,

    /// <summary>
    /// A mobiGlas app this app has no reader for yet. The app bar gives the
    /// family away; which app is open does not read from the bar alone.
    /// </summary>
    MobiGlas,

    /// <summary>The mobiGlas Contracts app, on its Accepted tab.</summary>
    Contracts,

    /// <summary>The Fleet Manager terminal, listing the ships and where they are.</summary>
    Fleet,

    /// <summary>The mobiGlas Rep app, one organisation open.</summary>
    Reputation,

    /// <summary>A commodity kiosk: what the shop stocks, and at what price.</summary>
    Kiosk,
}

/// <summary>One port on the loadout screen and what the frame says is in it.</summary>
/// <param name="Slot">The port, in the game's own label - "Cooler 2", "Missile Rack 4".</param>
/// <param name="Read">
/// The part's line as the engine returned it, decoration and all. Null when
/// nothing readable sat under the port - which is an empty port, or a turret
/// whose greyed heading the engine skipped and whose weapons are listed under
/// it. The frame does not say which, so neither does this.
/// </param>
/// <param name="Name">The catalogue's name for it, when exactly one name fits.</param>
/// <param name="ClassName">The class, when exactly one class fits. Three gimbal classes share one name.</param>
/// <param name="Tier">How the name was matched: Exact, Confusable, Decorated, None - or Empty, which is what the game prints in a port with nothing in it.</param>
/// <param name="Agrees">What the screen's own decoration vouched for: "size", "grade".</param>
/// <param name="Disagrees">What it contradicted. A kept candidate with a contradiction is shown, not hidden.</param>
/// <param name="Stock">Whether this is the part the ship ships with; null when nobody knows.</param>
public sealed record ScreenFitting(
    string Slot,
    string? Read,
    string? Name,
    string? ClassName,
    string Tier,
    IReadOnlyList<string> Agrees,
    IReadOnlyList<string> Disagrees,
    bool? Stock = null)
{
    public bool NothingRead => Read is null;

    /// <summary>The game said the port is empty, in so many words.</summary>
    public bool IsEmpty => Tier == "Empty";
}

/// <summary>What a Vehicle Loadout Manager frame said.</summary>
/// <param name="ShipRead">The ship's name as read from the dropdown, verbatim.</param>
/// <param name="Ship">The fleet's name for it when the read is exact or confusably so.</param>
/// <param name="LooksLike">Ships whose model word matched when the whole name did not. Offered, never asserted.</param>
/// <param name="Scope">The game's own caveat - "Only showing ships and equipment located in Nyx." - carried through as written.</param>
public sealed record LoadoutReading(
    string? ShipRead,
    string? Ship,
    IReadOnlyList<string> LooksLike,
    string? Scope,
    IReadOnlyList<ScreenFitting> Fittings);

/// <summary>What the map footer said.</summary>
/// <remarks>
/// The footer prints the system, the place and three figures in one line:
/// <c>PYRO &gt; DUDLEY &amp; DAUGHTERS &gt; 0.00° -155.25° 68.33GM</c>. The engine
/// folds the degree signs into trailing zeroes and reads G as 6, both of which
/// are undone here on the strength of the format being fixed: the game prints
/// two decimals and a degree sign, and nothing else can sit there.
/// </remarks>
/// <param name="PathRead">The whole trail between the system and the figures, as printed: "TERMINUS > RUIN STATION".</param>
/// <param name="AcceptedContracts">
/// True when the map lists accepted contracts, false when it says there are
/// none, null when it says neither. Both wordings were seen on real frames.
/// </param>
public sealed record MapReading(
    string? SystemRead,
    string? PlaceRead,
    double? Latitude,
    double? Longitude,
    double? Gigametres,
    bool? AcceptedContracts,
    string? PathRead = null)
{
    public bool NoAcceptedContracts => AcceptedContracts == false;
}

/// <summary>What the mobiGlas bar said the wallet holds.</summary>
/// <param name="Balance">The figure, or null when the engine returned none.</param>
/// <param name="Trouble">
/// Why there is no figure. Measured twice on this install: the balance is set
/// in an italic display face the in-box engine does not read at any size, so
/// the usual answer is a sentence rather than a number.
/// </param>
public sealed record WalletReading(long? Balance, string? Trouble);

/// <summary>Everything one frame was understood to say.</summary>
public sealed record ScreenFrame(
    ScreenKind Kind,
    IReadOnlyList<string> Lines,
    ScreenMatchResult? Tooltip,
    LoadoutReading? Loadout,
    MapReading? Map,
    WalletReading? Wallet,
    ContractsReading? Contracts = null,
    FleetReading? Fleet = null,
    ReputationReading? Reputation = null,
    KioskReading? Kiosk = null);

/// <summary>
/// Sorts a frame into the screen it is and reads what that screen carries.
/// </summary>
/// <remarks>
/// <para>
/// Built the same way <see cref="ScreenInsight"/> was: every rule below comes
/// from a measured frame in <c>docs/screen-insight.md</c>, distances are in
/// line heights so the rules hold at any resolution, and where the engine
/// misreads, the misreadings that were actually seen are folded away and no
/// others.
/// </para>
/// <para>
/// Reading the whole frame costs a fifth of a second, so the screen is
/// decided after the read rather than before it. The anchors are the game's
/// own headings, which read perfectly because they are set large.
/// </para>
/// </remarks>
public static partial class ScreenFrames
{
    /// <summary>The mobiGlas app bar, which every mobiGlas frame carries.</summary>
    private static readonly string[] AppBar =
    [
        "HOME", "HEALTH", "COMMS", "CONTRACTS", "MAPS", "JOURNAL",
        "ASSETS", "REP", "WALLET", "LANDING", "VEHICLES",
    ];

    /// <summary>How many of the app bar's words must sit on one row to count.</summary>
    /// <remarks>Eleven are printed; the engine returned all eleven on every frame measured. Six leaves room for a tooltip to cover half.</remarks>
    private const int EnoughOfTheBar = 6;

    /// <summary>The tabs across the top of the loadout tree, which anchor its column.</summary>
    private static readonly string[] LoadoutTabs =
    ["Liveries", "Propulsion", "Systems", "Vhcl. Wpns.", "Avionics", "Misc."];

    /// <summary>
    /// How far below a port label its part sits, in the label's line heights.
    /// </summary>
    /// <remarks>
    /// Measured 20 to 22 pixels under labels 14 to 19 pixels tall, so about
    /// 1.2 to 1.5. An empty port has 85 pixels to the next label, about five.
    /// Two sits between them with room either way.
    /// </remarks>
    private const double PartBelow = 2.0;

    /// <summary>
    /// How far off the tab row's left edge the tree's lines may sit, in line heights.
    /// </summary>
    /// <remarks>
    /// The tree indents: left edges wandered from 724 to 807 across the frames
    /// measured, against a Liveries tab at 730 - eighty pixels, five heights.
    /// The detail panel starts at 1211, thirty heights away, so ten is safe.
    /// </remarks>
    private const double TreeColumn = 10.0;

    /// <summary>Reads a frame whose boxes were kept.</summary>
    /// <param name="commodityNames">
    /// What the game calls its commodities, for a kiosk's list. Defaulted
    /// because every other screen is read without them.
    /// </param>
    public static ScreenFrame Read(
        IReadOnlyList<ScreenTextLine> lines,
        IReadOnlyList<ItemReference> items,
        IReadOnlyList<string> shipNames,
        string? handle,
        IReadOnlyList<string>? commodityNames = null)
    {
        var all = lines
            .Select(line => line with { Text = line.Text.Trim() })
            .Where(line => line.Text.Length > 0)
            .ToList();

        var texts = all.Select(line => line.Text).ToList();
        var bar = HasAppBar(all);
        var wallet = bar ? ReadWallet(all, handle) : null;

        // A loadout frame can carry a tooltip as well - the component the
        // pilot is hovering - so the tooltip is read on every frame and kept
        // wherever it says something.
        var tooltip = ScreenInsight.Look(all, items);
        var hasTooltip = tooltip.Reading.Name is not null || tooltip.Reading.Fields.Count > 0;

        if (all.Any(line => Is(line.Text, "Vehicle Loadout Manager")))
        {
            return new ScreenFrame(ScreenKind.Loadout, texts, hasTooltip ? tooltip : null,
                ReadLoadout(all, items, shipNames), null, wallet);
        }

        // The Fleet Manager's estimate reads as a loadout: same facts, other shape.
        if (ReadManifest(all, items, shipNames) is { } manifest)
            return new ScreenFrame(ScreenKind.Loadout, texts, null, manifest, null, wallet);

        if (ReadFleet(all, shipNames) is { } fleet)
            return new ScreenFrame(ScreenKind.Fleet, texts, null, null, null, wallet, Fleet: fleet);

        if (ReadMap(all) is { } map)
            return new ScreenFrame(ScreenKind.Map, texts, null, null, map, wallet);

        if (ReadContracts(all) is { } contracts)
            return new ScreenFrame(ScreenKind.Contracts, texts, null, null, null, wallet, Contracts: contracts);

        if (ReadReputation(all) is { } reputation)
            return new ScreenFrame(ScreenKind.Reputation, texts, null, null, null, wallet, Reputation: reputation);

        if (ReadKiosk(all, commodityNames ?? [], shipNames) is { } kiosk)
            return new ScreenFrame(ScreenKind.Kiosk, texts, null, null, null, wallet, Kiosk: kiosk);

        if (hasTooltip)
            return new ScreenFrame(ScreenKind.Tooltip, texts, tooltip, null, null, wallet);

        return new ScreenFrame(bar ? ScreenKind.MobiGlas : ScreenKind.Unknown, texts, null, null, null, wallet);
    }

    /// <summary>Whether a line reads as an anchor, with the measured confusions folded on both sides.</summary>
    /// <remarks>
    /// Both sides, always. The fold turns D into O and L into I, so a literal
    /// written in capitals never equals its own folded reading - which is how
    /// the first version of this file failed to recognise a single loadout frame.
    /// </remarks>
    private static bool Is(string text, string anchor) =>
        ScreenInsight.Fold(text) == ScreenInsight.Fold(anchor);

    private static bool Opens(string text, string anchor) =>
        ScreenInsight.Fold(text).StartsWith(ScreenInsight.Fold(anchor), StringComparison.Ordinal);

    /// <summary>Whether the mobiGlas app bar is on the frame.</summary>
    /// <remarks>
    /// The bar's words sit on one row - Tops within a line height of each
    /// other on every frame measured - which is what separates the bar from
    /// the same words scattered through a contract's text.
    /// </remarks>
    private static bool HasAppBar(IReadOnlyList<ScreenTextLine> lines)
    {
        var words = lines
            .Where(line => AppBar.Any(word => Is(line.Text, word)))
            .ToList();

        if (words.Count < EnoughOfTheBar) return false;

        return words.Any(anchor =>
            words.Count(w => Math.Abs(w.Top - anchor.Top) <= anchor.Height * 1.5) >= EnoughOfTheBar);
    }

    // ---- the wallet ----

    /// <summary>The balance beside the handle on the mobiGlas bar.</summary>
    /// <remarks>
    /// The figure is printed directly above the handle, about a line and a
    /// half up and starting at the same left edge. When the handle is on the
    /// frame and no figure is, the figure was there and did not read - which
    /// is what was measured, twice, and is said as such.
    /// </remarks>
    private static WalletReading ReadWallet(IReadOnlyList<ScreenTextLine> lines, string? handle)
    {
        if (handle is not { Length: > 0 })
            return new WalletReading(null, "the app does not know your handle yet, so it cannot find the wallet on the bar");

        var folded = ScreenInsight.Fold(handle);

        var name = lines.FirstOrDefault(line => ScreenInsight.Fold(line.Text) == folded);

        if (name is null)
            return new WalletReading(null, "your handle is not on this frame, so the wallet is not either");

        var figure = lines
            .Where(line => line.Top < name.Top && name.Top - line.Top <= name.Height * 4)
            .Where(line => Math.Abs(line.Left - name.Left) <= name.Height * 4)
            .Select(line => Figure(line.Text))
            .FirstOrDefault(value => value is not null);

        return figure is { } balance
            ? new WalletReading(balance, null)
            : new WalletReading(null,
                "the balance is printed in a face this engine does not read - the handle beside it read fine");
    }

    /// <summary>A whole number with or without thousands separators, or null.</summary>
    private static long? Figure(string text)
    {
        var match = FigureRegex().Match(text);
        if (!match.Success) return null;

        var digits = match.Groups["n"].Value.Replace(",", "").Replace(".", "").Replace(" ", "");
        return long.TryParse(digits, out var value) ? value : null;
    }

    [GeneratedRegex(@"^\D{0,2}(?<n>\d{1,3}(?:[,. ]\d{3})+|\d{4,})\s*$")]
    private static partial Regex FigureRegex();

    // ---- the map ----

    /// <summary>The map footer's place and position, or null when the frame has no footer.</summary>
private static MapReading? ReadMap(IReadOnlyList<ScreenTextLine> lines)
    {
        // The figures anchor the footer. The engine returned the whole footer
        // as one line on one frame and as five on the next - "p YRO",
        // "TERMINUS", ">", "RUIN STATION", "0.000 134.770 68.32GM" - so the
        // row the figures sit on is gathered left to right and read as one.
        var figures = lines.FirstOrDefault(line => FiguresRegex().IsMatch(line.Text));
        if (figures is null) return null;

        var row = lines
            .Where(line => Math.Abs(line.Top - figures.Top) <= figures.Height * 1.2)
            .OrderBy(line => line.Left)
            .ToList();

        var match = FooterRegex().Match(string.Join(' ', row.Select(line => line.Text)));
        if (!match.Success) return null;

        var path = match.Groups["place"].Value.Trim();
        string? system = null;

        if (LeadingSystem(path) is { } inline)
        {
            system = inline.System;
            path = inline.Remainder;
        }

        // What is left is the trail down to the place: "TERMINUS > RUIN
        // STATION", or just "DUDLEY & DAUGHTERS". The place is its last step.
        var steps = path.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TidyPlace)
            .Where(step => step.Length > 0)
            .ToList();

        bool? accepted =
            lines.Any(line => Is(line.Text, "NO ACCEPTED CONTRACTS")) ? false
            : lines.Any(line => Is(line.Text, "ACCEPTED CONTRACTS")) ? true
            : null;

        return new MapReading(
            system,
            steps.LastOrDefault(),
            Degrees(match.Groups["lat"].Value),
            Degrees(match.Groups["lon"].Value),
            double.TryParse(match.Groups["gm"].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var gm) ? gm : null,
            accepted,
            steps.Count > 1 ? string.Join(" > ", steps) : null);
    }

    /// <summary>The systems the game has, as their names read.</summary>
    private static readonly string[] Systems = ["STANTON", "PYRO", "NYX"];

    private static string? SystemNamed(string text)
    {
        var folded = ScreenInsight.Fold(text);
        return Systems.FirstOrDefault(s => ScreenInsight.Fold(s) == folded);
    }

    private static (string System, string Remainder)? LeadingSystem(string place)
    {
        var words = place.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // A stray glyph before the system - the pin icon read as "9" - is
        // skipped, but only one, and only if the system follows it.
        for (var i = 0; i < Math.Min(2, words.Length); i++)
        {
            if (SystemNamed(words[i]) is { } system && i + 1 < words.Length)
                return (system, string.Join(' ', words[(i + 1)..]));

            // "p YRO": the system split in two by the pin beside it.
            if (i + 2 < words.Length && SystemNamed(words[i] + words[i + 1]) is { } joined)
                return (joined, string.Join(' ', words[(i + 2)..]));
        }

        return null;
    }

    /// <summary>
    /// The place with the ampersand put back.
    /// </summary>
    /// <remarks>
    /// "DUDLEY &amp; DAUGHTERS" came back as "DUDLEY B DAUGHTERS" once and
    /// "DUDLEY 6 DAUGHTERS" the next time. A lone B, 6 or 8 between two words
    /// is never a word in a place name, so it is the ampersand and nothing else.
    /// </remarks>
    private static string TidyPlace(string place)
    {
        var words = place.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        for (var i = 1; i < words.Count - 1; i++)
        {
            if (words[i] is "B" or "6" or "8" or "&")
                words[i] = "&";
        }

        // The pin icon, when it reads as a digit and no system followed it.
        if (words.Count > 1 && words[0].Length == 1 && char.IsDigit(words[0][0]))
            words.RemoveAt(0);

        return string.Join(' ', words);
    }

    /// <summary>A figure printed with two decimals and a degree sign the engine turned into a trailing zero.</summary>
    private static double? Degrees(string text) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;

    // "DUDLEY 6 DAUGHTERS > 0.000 -155.250 68.336M": the place, then two
    // angles with the degree sign flattened to a zero, then a distance whose
    // G came back as a 6. Two decimals are what the game prints, so the third
    // digit is the degree sign and is dropped.
    [GeneratedRegex(
        @"^(?<place>.*?)\s*>?\s*(?<lat>-?\d+\.\d{2})[°0oO]?\s+(?<lon>-?\d+\.\d{2})[°0oO]?\s+(?<gm>\d+\.\d{2})\s*[G6]?M\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex FooterRegex();

    [GeneratedRegex(@"-?\d+\.\d{2}[°0oO]?\s+-?\d+\.\d{2}[°0oO]?\s+\d+\.\d{2}\s*[G6]?M\b", RegexOptions.IgnoreCase)]
    private static partial Regex FiguresRegex();

    // ---- the loadout ----

    private static LoadoutReading ReadLoadout(
        IReadOnlyList<ScreenTextLine> lines,
        IReadOnlyList<ItemReference> items,
        IReadOnlyList<string> shipNames)
    {
        var scope = lines
            .Select(line => line.Text)
            .FirstOrDefault(text => Opens(text, "Only showing"));

        var tabs = lines
            .Where(line => LoadoutTabs.Any(tab => Is(line.Text, tab)))
            .OrderBy(line => line.Left)
            .ToList();

        var (shipRead, ship, looksLike) = ReadShip(lines, tabs, shipNames);

        if (tabs.Count == 0)
            return new LoadoutReading(shipRead, ship, looksLike, scope, []);

        var column = tabs[0];

        // The tree: everything under the tab row in the tab row's own column.
        var tree = lines
            .Where(line => line.Top > column.Top + column.Height * 2)
            .Where(line => line.Left >= column.Left - column.Height
                && line.Left <= column.Left + column.Height * TreeColumn)
            .Select(line => line with { Text = TreeText(line.Text) })
            .Where(line => line.Text.Length >= 3)
            .OrderBy(line => line.Top)
            .ToList();

        var fittings = new List<ScreenFitting>();

        for (var i = 0; i < tree.Count; i++)
        {
            var label = tree[i];
            var next = i + 1 < tree.Count ? tree[i + 1] : null;

            if (next is not null && next.Top - label.Top <= label.Height * PartBelow)
            {
                fittings.Add(Fitting(label.Text, next.Text, items));
                i++;
                continue;
            }

            // A line on its own has nothing read under it - unless it is
            // itself a part, in which case the label above it did not read.
            // Both happened on the frames measured, and the second must not be
            // filed as a bare port called "MBA Cannon".
            var alone = Fitting("(port label did not read)", label.Text, items);

            // Anything the catalogue recognises at all is a part, including a
            // reading two parts fit equally - that is a part with no name yet,
            // not a port with nothing in it.
            fittings.Add(alone.Tier != "None"
                ? alone
                : new ScreenFitting(label.Text, null, null, null, "None", [], []));
        }

        return new LoadoutReading(shipRead, ship, looksLike, scope, fittings);
    }

    /// <summary>The ship in the dropdown: above the tabs, to the right, in capitals.</summary>
    private static (string? Read, string? Ship, IReadOnlyList<string> LooksLike) ReadShip(
        IReadOnlyList<ScreenTextLine> lines,
        IReadOnlyList<ScreenTextLine> tabs,
        IReadOnlyList<string> shipNames)
    {
        var tabTop = tabs.Count > 0 ? tabs[0].Top : double.MaxValue;
        var heading = lines.FirstOrDefault(line => Is(line.Text, "Vehicle Loadout Manager"));

        var read = lines
            .Where(line => line.Top < tabTop)
            .Where(line => heading is null || line.Left > heading.Left + heading.Height * 12)
            .Where(line => line.Text.Length >= 4 && line.Text == line.Text.ToUpperInvariant())
            .Where(line => line.Text.Any(char.IsLetter))
            .Where(line => !Opens(line.Text, "Only showing"))
            .OrderByDescending(line => line.Left)
            .FirstOrDefault()?.Text;

        if (read is null) return (null, null, []);

        var folded = ScreenInsight.Fold(read);
        var plain = ScreenInsight.Plain(read);

        var exact = shipNames
            .Where(name => string.Equals(ScreenInsight.Plain(name), plain, StringComparison.OrdinalIgnoreCase)
                || ScreenInsight.Fold(name) == folded)
            .Distinct()
            .ToList();

        if (exact.Count == 1) return (read, exact[0], []);
        if (exact.Count > 1) return (read, null, exact);

        // "DUKE CORSAIR" was a real reading of the Corsair. The maker's word
        // went wrong and the model's did not, and the model is the rarer
        // word - so it is offered, as a resemblance, and nothing is built on it.
        var model = read.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        if (model is null || ScreenInsight.Plain(model).Length < 5) return (read, null, []);

        var modelFolded = ScreenInsight.Fold(model);

        var alike = shipNames
            .Where(name => name.Split(' ').Any(word => ScreenInsight.Fold(word) == modelFolded))
            .Distinct()
            .ToList();

        return (read, null, alike);
    }

    /// <summary>
    /// A tree line with the connector glyphs stripped.
    /// </summary>
    /// <remarks>
    /// The tree draws its branches, and the engine reads them: a bare "L", or
    /// an "L " or "I—" in front of a label. Only the shapes actually seen are
    /// removed, and only in front of a capital, so a part called "L-Series"
    /// would keep its name.
    /// </remarks>
    private static string TreeText(string text)
    {
        var cleaned = ConnectorRegex().Replace(text, "");
        return cleaned.Trim();
    }

    [GeneratedRegex(@"^(?:[LI|]\s*[—–-]?\s*|[—–-]+\s*)(?=[A-Z@(])")]
    private static partial Regex ConnectorRegex();

    /// <summary>A port and the part read under it, matched to the catalogue.</summary>
    private static ScreenFitting Fitting(string slot, string read, IReadOnlyList<ItemReference> items)
    {
        // The game prints "Empty" in a port with nothing in it - measured on
        // the Hermes' flair ports - and that is a claim, not a part.
        if (Is(read, "Empty"))
            return new ScreenFitting(slot, read, null, null, "Empty", [], []);

        var (name, size, grade) = Undecorate(read);

        var hits = new List<(ItemReference Item, string Tier)>();

        var plain = ScreenInsight.Plain(name);
        var folded = ScreenInsight.Fold(name);

        if (plain.Length < 3)
            return new ScreenFitting(slot, read, null, null, "None", [], []);

        foreach (var item in items)
        {
            if (item.Name is not { Length: > 0 } known) continue;

            if (string.Equals(ScreenInsight.Plain(known), plain, StringComparison.OrdinalIgnoreCase))
                hits.Add((item, "Exact"));
            else if (ScreenInsight.Fold(known) == folded)
                hits.Add((item, "Confusable"));
            else if (ScreenInsight.Fold(WithoutNumeral(known)) == folded && WithoutNumeral(known) != known)
                hits.Add((item, "Decorated"));
            else if (Truncated(folded, ScreenInsight.Fold(known), size, grade))
                hits.Add((item, "Truncated"));
        }

        if (hits.Count == 0)
            return new ScreenFitting(slot, read, null, null, "None", [], []);

        // What the screen's own prefix says about the part, checked against
        // the catalogue the same way a tooltip's volume is: same fact, other
        // source. This is what tells the Torrent quantum drive from the
        // Torrent gun, when the name alone cannot.
        var scored = hits
            .Select(hit =>
            {
                var agrees = new List<string>();
                var disagrees = new List<string>();

                if (size is { } s) (s == hit.Item.Size ? agrees : disagrees).Add("size");
                if (grade is { } g) (g == hit.Item.Grade ? agrees : disagrees).Add("grade");

                return (hit.Item, hit.Tier, Agrees: agrees, Disagrees: disagrees);
            })
            .OrderBy(h => h.Disagrees.Count)
            .ThenBy(h => TierRank(h.Tier))
            .ThenByDescending(h => h.Agrees.Count)
            .ToList();

        var top = scored[0];

        var kept = scored
            .Where(h => h.Disagrees.Count == top.Disagrees.Count
                && h.Tier == top.Tier
                && h.Agrees.Count == top.Agrees.Count)
            .ToList();

        var names = kept.Select(h => h.Item.Name!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return new ScreenFitting(
            slot,
            read,
            names.Count == 1 ? names[0] : null,
            kept.Count == 1 ? kept[0].Item.ClassName : null,
            top.Tier,
            top.Agrees,
            top.Disagrees);
    }

    private static int TierRank(string tier) => tier switch
    {
        "Exact" => 0,
        "Confusable" => 1,
        "Decorated" => 2,
        "Truncated" => 3,
        _ => 4,
    };

    /// <summary>
    /// Whether a read name is the start of a catalogue name, cut short by
    /// the column it was printed in.
    /// </summary>
    /// <remarks>
    /// The Fleet Manager's estimate prints "VariPuck S4 Gimbal" for the
    /// VariPuck S4 Gimbal Mount and "Civ/2/A 7MA" for the 7MA 'Lorica'. A
    /// prefix is a weak claim on its own, so it counts only when it is long,
    /// or when the screen's own size or grade vouches for it - and what it
    /// yields is still subject to the tie rule, so "7MA" against two 7MAs
    /// names neither.
    /// </remarks>
    private static bool Truncated(string folded, string known, int? size, int? grade)
    {
        if (folded.Length < 3 || folded.Length >= known.Length) return false;
        if (!known.StartsWith(folded, StringComparison.Ordinal)) return false;

        return folded.Length >= 10 || size is not null || grade is not null;
    }

    /// <summary>
    /// The part's name with the screen's decoration taken off, and what the
    /// decoration said.
    /// </summary>
    /// <remarks>
    /// Components are printed as <c>Civ/2/C Frost-Star EX</c>: the grade class,
    /// the size and the grade letter in front of the name the catalogue uses.
    /// Missiles are printed as <c>[IR3] 'Chaos' Missile</c>, where the tag
    /// carries the seeker and the size and the catalogue spells the size out as
    /// a numeral instead: <c>'Chaos' III Missile</c>. Neither is a misreading,
    /// and no amount of glyph folding bridges either.
    /// </remarks>
    internal static (string Name, int? Size, int? Grade) Undecorate(string read)
    {
        var component = ComponentPrefixRegex().Match(read);

        if (component.Success)
        {
            var size = Digit(component.Groups["size"].Value);
            var grade = component.Groups["grade"].Value.ToUpperInvariant() switch
            {
                "A" => 1, "B" => 2, "C" => 3, "D" => 4, _ => (int?)null,
            };

            return (read[component.Length..].Trim(), size, grade);
        }

        var missile = MissileTagRegex().Match(read);

        if (missile.Success)
            return (read[missile.Length..].Trim(), Digit(missile.Groups["size"].Value), null);

        return (read.Trim(), null, null);
    }

    /// <summary>A size digit, with the one confusion measured on it undone.</summary>
    private static int? Digit(string text) => text switch
    {
        "I" or "l" => 1,
        "O" or "o" => 0,
        _ => int.TryParse(text, out var n) ? n : null,
    };

    /// <summary>The catalogue name without its roman-numeral size.</summary>
    private static string WithoutNumeral(string name) =>
        NumeralRegex().Replace(name, " ").Trim();

    [GeneratedRegex(@"\s+(?:I|II|III|IV|V|VI|VII|VIII|IX|X)\s+")]
    private static partial Regex NumeralRegex();

    // "Civ/2/C ", "Ind/3/A ". The size is a digit the engine has read as a
    // capital I, and the slashes have held on every frame.
    [GeneratedRegex(@"^(?<class>Civ|Ind|Mil|Stl|Cmp)\s*/\s*(?<size>[0-9IlO])\s*/\s*(?<grade>[A-Da-d])\s+", RegexOptions.IgnoreCase)]
    private static partial Regex ComponentPrefixRegex();

    // "[IR3] ", read as "[IR31 ", "tCS31 " and "tlR31 " on the frames measured:
    // the opening bracket as t, the closing one as 1. The size is the digit
    // before whatever the closing bracket became.
    [GeneratedRegex(@"^[\[t]?[A-Za-z]{2,3}(?<size>\d)[\]1I]?\s+")]
    private static partial Regex MissileTagRegex();
}
