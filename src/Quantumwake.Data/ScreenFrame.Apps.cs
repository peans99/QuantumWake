using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>One card in the Contracts app's accepted list.</summary>
/// <param name="Reward">As printed on the card - "215k" - because the card rounds and the detail panel does not.</param>
public sealed record ContractCard(string Title, string? Reward, string? Issuer);

/// <summary>What the mobiGlas Contracts app said, on its Accepted tab.</summary>
/// <param name="Accepted">The count in the tab itself: "ACCEPTED (5/10)".</param>
/// <param name="SelectedReward">The detail panel's figure, in aUEC, with the ¤ read as whatever the engine made of it.</param>
/// <param name="Objectives">Each objective line as read, continuation lines joined.</param>
/// <param name="Steps">The same lines, read: verb, cargo, place, count and how deep the diamond sits.</param>
/// <param name="PickupSites">
/// The "PICK UP LOCATIONS (ANY ORDER)" list from the contract's own text,
/// which is where the game says what body a pickup is on - "Freight elevator
/// at Fallow Field on Pyro IV" - and the objectives never do.
/// </param>
/// <param name="DropSites">
/// The "DROP OFF LOCATIONS (ANY ORDER)" list, the same list for a contract
/// with one source and several destinations; the objectives panel shows
/// the first two or three and the rest are below the fold.
/// </param>
/// <param name="Reader">
/// Which objectives reader produced this - 0 for every reading stored before
/// 0.19.4, <see cref="ScreenFrames.ContractsReader"/> since - so a frame read
/// before a reader fix is read once more, and only once.
/// </param>
public sealed record ContractsReading(
    int? Accepted,
    int? Capacity,
    IReadOnlyList<ContractCard> Cards,
    string? SelectedTitle,
    long? SelectedReward,
    string? SelectedIssuer,
    IReadOnlyList<string> Objectives,
    IReadOnlyList<ContractStep>? Steps = null,
    IReadOnlyList<PickupSite>? PickupSites = null,
    IReadOnlyList<PickupSite>? DropSites = null,
    int Reader = 0);

/// <summary>One objective of the selected contract, as the mobiGlas prints it.</summary>
/// <remarks>
/// Three shapes have been measured, and the indent is what pairs them. A
/// direct haul prints <c>Collect Agricultural Supplies from Port Tressler.</c>
/// with <c>Deliver 0/13 SCU to NB Int. Spaceport.</c> indented under it - the
/// cargo named only on the Collect line. A multi-pickup prints a
/// <c>Deliver 0/18 SCU of Aluminum to Stanton Gateway.</c> parent with one
/// indented <c>Collect Aluminum from Fallow Field.</c> per source. And a
/// multi-drop prints a Collect parent per source, each with its own indented
/// Deliver. So a step is a verb, a cargo, a place and a count, plus how far
/// in its diamond sits; the legs are worked out from that afterwards.
/// </remarks>
/// <param name="Kind"><c>collect</c>, <c>deliver</c>, or <c>other</c> for a line that read but did not parse.</param>
/// <param name="Place">The place as printed, with any "above Crusader" / "on Pyro IV" tail moved to <paramref name="Body"/>.</param>
/// <param name="Done">The count's first figure - <c>0</c> of <c>0/18 SCU</c> - and <paramref name="Total"/> the second.</param>
/// <param name="Depth">0 for a diamond on the heading's own margin, 1 for one indented under it.</param>
public sealed record ContractStep(
    string Text,
    string Kind,
    string? Commodity,
    string? Place,
    string? Body,
    int? Done,
    int? Total,
    int Depth);

/// <summary>One line of the contract text's pickup list: the place, and the body the text says it is on.</summary>
public sealed record PickupSite(string Place, string? Body);

/// <summary>One row of the Fleet Manager terminal.</summary>
/// <param name="Read">The name as the terminal's CRT face read, which is badly: two of five on the measured frame.</param>
/// <param name="Ship">The fleet's name for it when the read is exact or confusably so.</param>
/// <param name="State">"Stored" or "Deliverable", as printed.</param>
public sealed record FleetRow(
    string Read,
    string? Ship,
    IReadOnlyList<string> LooksLike,
    string? Location,
    string? State,
    string? Focus,
    int? Cargo);

/// <summary>What the Fleet Manager terminal listed.</summary>
public sealed record FleetReading(IReadOnlyList<FleetRow> Ships);

/// <summary>What the mobiGlas Rep app said about one organisation.</summary>
/// <param name="Standing">
/// "NEUTRAL", as printed under the name. Not the rank: Wikelo prints NEUTRAL
/// there while the rank held is NEW CUSTOMER.
/// </param>
/// <param name="Rank">
/// The rank held, from the bars - see <see cref="RepBars"/>. Null when the
/// bars were not measured, which is every reading taken before 0.18.22 and
/// any frame read without pixels.
/// </param>
/// <param name="Section">The ladder's heading - "HAULING", "STANDING", "BARTER & TRADE".</param>
/// <param name="Ranks">Every card on the ladder, in layout order, with its bar when measured.</param>
/// <param name="Progress">How far into <paramref name="Rank"/>, 0 to 1, read off its bar.</param>
public sealed record ReputationReading(
    string? Organisation,
    string? Standing,
    string? Rank,
    IReadOnlyList<string> Organisations,
    string? Section = null,
    IReadOnlyList<RepRankCard>? Ranks = null,
    double? Progress = null)
{
    /// <summary>The same reading with its bars measured and the rank they show.</summary>
    public ReputationReading WithBars(IReadOnlyList<RepRankCard> measured)
    {
        if (measured.Count == 0) return this;

        return RepBars.Held(measured) is var (index, progress)
            ? this with { Ranks = measured, Rank = measured[index].Name, Progress = progress }
            : this with { Ranks = measured };
    }
}

public static partial class ScreenFrames
{
    // ---- the Contracts app ----

    /// <summary>
    /// The objectives reader's version. 1 is 0.19.4's: diamonds read apart
    /// from their lines, and objectives that open with their verb.
    /// </summary>
    public const int ContractsReader = 1;

    /// <summary>The Contracts app's Accepted tab, or null when this is not it.</summary>
    /// <remarks>
    /// Anchored on the tab that names its own count. The list is one column of
    /// cards, each a title of one or two lines, then the reputation bracket
    /// the game prints under every hauling contract, then the issuer in a
    /// smaller face. The bracket is the reliable part: it closes a title the
    /// same way on every card measured.
    /// </remarks>
    private static ContractsReading? ReadContracts(IReadOnlyList<ScreenTextLine> lines)
    {
        ScreenTextLine? tab = null;
        Match? count = null;

        foreach (var line in lines)
        {
            var m = AcceptedTabRegex().Match(line.Text);
            if (!m.Success) continue;
            tab = line;
            count = m;
            break;
        }

        if (tab is null || count is null) return null;

        var accepted = int.TryParse(count.Groups["n"].Value, out var n) ? n : (int?)null;
        var capacity = int.TryParse(count.Groups["of"].Value, out var of) ? of : (int?)null;

        // The list column is anchored on its own button, which reads perfectly
        // because it is set large; the detail panel starts at the tab row.
        var anchor = lines.FirstOrDefault(line => Is(line.Text, "MARK ALL READ"));
        var cards = new List<ContractCard>();

        if (anchor is not null)
        {
            var column = lines
                .Where(line => line.Top > anchor.Top + anchor.Height)
                .Where(line => Math.Abs(line.Left - anchor.Left) <= anchor.Height * 3)
                .OrderBy(line => line.Top)
                .ToList();

            var rewards = lines
                .Where(line => line.Left > anchor.Left + anchor.Height * 10 && line.Left < tab.Left)
                .Where(line => CardRewardRegex().IsMatch(line.Text))
                .ToList();

            for (var i = 0; i < column.Count; i++)
            {
                if (!BracketRegex().IsMatch(column[i].Text)) continue;

                // The title is the run of lines directly above the bracket,
                // each within two heights of the next. The group heading
                // above the first card sits four heights off and is left out.
                var title = new List<ScreenTextLine>();

                for (var j = i - 1; j >= 0; j--)
                {
                    var below = j == i - 1 ? column[i] : column[j + 1];
                    if (below.Top - column[j].Top > column[j].Height * 2.2) break;
                    if (BracketRegex().IsMatch(column[j].Text) || IsRepTag(column[j].Text)) break;
                    title.Insert(0, column[j]);
                }

                if (title.Count == 0) continue;

                var issuer = column
                    .Skip(i + 1)
                    .TakeWhile(line => line.Top - column[i].Top <= column[i].Height * 4)
                    .FirstOrDefault(line => !IsRepTag(line.Text))?.Text;

                var reward = rewards
                    .FirstOrDefault(r => r.Top >= title[0].Top - title[0].Height && r.Top <= column[i].Top + column[i].Height)
                    ?.Text;

                cards.Add(new ContractCard(string.Join(' ', title.Select(t => t.Text)), reward, issuer));
            }
        }

        // The detail panel starts under the leftmost tab; its title is the one
        // line set twice the size of anything else.
        var panelLeft = lines
            .Where(line => Math.Abs(line.Top - tab.Top) <= tab.Height)
            .Where(line => anchor is null || line.Left > anchor.Left + anchor.Height * 6)
            .Min(line => (double?)line.Left) ?? tab.Left;

        // A long title wraps - "Member | Stellar Medium Haul | from Ruin" then
        // "Station" - at the same size directly under; the 21 Sep Waste card
        // lost its last word and matched no contract. Every large line within
        // two heights of the one above is the same title.
        var headline = lines
            .Where(line => line.Left > panelLeft - tab.Height * 2 && line.Top > tab.Top)
            .Where(line => line.Height >= tab.Height * 2)
            .OrderBy(line => line.Top)
            .ToList();
        string? selected = null;
        for (var i = 0; i < headline.Count; i++)
        {
            if (i > 0 && headline[i].Top - headline[i - 1].Top > headline[i - 1].Height * 2) break;
            selected = selected is null ? headline[i].Text : selected + " " + headline[i].Text.Trim();
        }

        var reward2 = Beside(lines, "Reward") is { } figure ? Figure(figure) : null;
        var issuer2 = Beside(lines, "Contracted By");

        var objectivesHead = lines.FirstOrDefault(line => Is(line.Text, "PRIMARY OBJECTIVES"));
        var steps = objectivesHead is null ? [] : ReadObjectives(lines, objectivesHead);

        return new ContractsReading(accepted, capacity, cards, selected, reward2, issuer2,
            [.. steps.Select(s => s.Text)], steps, ReadSites(lines, "PICK UP LOCATIONS"), ReadSites(lines, "DROP OFF LOCATIONS"),
            ContractsReader);
    }

    /// <summary>
    /// The objectives column: every diamond line, with the lines a long one
    /// wrapped onto joined back, and the indent measured against the heading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A wrapped objective - "Deliver 0/2 SCU of Waste to Seraphim Station
    /// above" then "Crusader." on the next line - returns as two lines, the
    /// second without a diamond. It is taken as a continuation when it sits
    /// within two line heights below the diamond line and no further left
    /// than its text: a new objective always brings its own diamond. Reading
    /// stops at the first gap of more than four heights, which is where the
    /// TRACK and SHARE buttons at the panel's foot begin.
    /// </para>
    /// <para>
    /// The engine does not always read the diamond as part of its line. On
    /// the 25 Oct 21:25:59 Potassium card it read the three indented diamonds
    /// as lines of their own ("o") and the three Collect lines without them,
    /// and every Collect was joined onto the Deliver above it - one objective
    /// reading "Deliver 0/249 SCU of Potassium to Ruin Station above Pyro VI.
    /// Collect Potassium from Dudley S Daughters. Collect...", which put a
    /// drop-off at Megumi Refueling. On the 21:26:12 Waste card it did the
    /// same to the Deliver parents, and two of three drops were lost. An
    /// objective always opens with its verb, so a line that does is a new
    /// objective whatever happened to its diamond; and a diamond read alone
    /// gives its indent to the text beside it.
    /// </para>
    /// </remarks>
    private static List<ContractStep> ReadObjectives(IReadOnlyList<ScreenTextLine> lines, ScreenTextLine head)
    {
        var column = lines
            .Where(line => line.Top > head.Top)
            .Where(line => line.Left >= head.Left - head.Height)
            .OrderBy(line => line.Top)
            .ToList();

        var diamonds = column.Where(line => LoneDiamondRegex().IsMatch(line.Text.Trim())).ToList();

        // Where a line's diamond sits: on the line when the engine kept it,
        // else a lone diamond on the same row just to its left, else where
        // one would be - the text starts a diamond and a space further in,
        // 33 px at a 22 px line on the 21:26:12 frame.
        double DiamondLeft(ScreenTextLine line) => diamonds
            .Where(d => d.Left < line.Left && line.Left - d.Left <= line.Height * 3)
            .Where(d => Math.Abs(d.Top - line.Top) <= Math.Max(d.Height, line.Height) * 0.8)
            .Select(d => (double?)d.Left)
            .FirstOrDefault() ?? line.Left - line.Height * 1.5;

        var steps = new List<(double Left, double Height, string Text)>();
        ScreenTextLine? last = null;

        foreach (var line in column)
        {
            if (last is not null && line.Top - last.Top > last.Height * 4) break;
            if (diamonds.Contains(line)) continue;

            var m = ObjectiveRegex().Match(line.Text);

            if (m.Success)
            {
                steps.Add((line.Left, line.Height, m.Groups["text"].Value.Trim()));
                last = line;
                continue;
            }

            if (ObjectiveVerbRegex().IsMatch(line.Text.Trim()))
            {
                steps.Add((DiamondLeft(line), line.Height, line.Text.Trim()));
                last = line;
                continue;
            }

            if (last is null || steps.Count == 0) continue;

            if (line.Top - last.Top <= last.Height * 2.2 && line.Left >= last.Left)
            {
                var (left, height, text) = steps[^1];
                steps[^1] = (left, height, text + " " + line.Text.Trim());
                last = line;
            }
        }

        // The shallowest diamond is the margin; a step more than half a height
        // further in is indented under the one before it.
        var margin = steps.Count == 0 ? 0 : steps.Min(s => s.Left);

        return [.. steps.Select(s => Step(s.Text, s.Left - margin > s.Height * 0.5 ? 1 : 0))];
    }

    /// <summary>One objective line read into its parts, or filed as "other" with its text intact.</summary>
    internal static ContractStep Step(string text, int depth)
    {
        var deliver = DeliverRegex().Match(text);

        if (deliver.Success)
        {
            var (place, body) = SplitBody(deliver.Groups["place"].Value);

            return new ContractStep(text, "deliver",
                Clean(deliver.Groups["cargo"]), place, body,
                Count(deliver.Groups["done"]), Count(deliver.Groups["total"]), depth);
        }

        var collect = CollectRegex().Match(text);

        if (collect.Success)
        {
            var (place, body) = SplitBody(collect.Groups["place"].Value);
            return new ContractStep(text, "collect", Clean(collect.Groups["cargo"]), place, body, null, null, depth);
        }

        // The game types an objective in a letter at a time when the card
        // opens, and a screenshot taken in that second gets "Deliver 0/189
        // SCU of Car" - three of the nine 25 Oct frames did. No place, but
        // the count and sometimes the cargo are already there to keep.
        var cut = CutObjectiveRegex().Match(text);

        return cut.Success
            ? new ContractStep(text, "other", Clean(cut.Groups["cargo"]), null, null,
                Count(cut.Groups["done"]), Count(cut.Groups["total"]), depth)
            : new ContractStep(text, "other", null, null, null, null, null, depth);
    }

    /// <summary>
    /// Whether an objective was read whole. The game ends every objective with
    /// a full stop and types it in when the card opens, so one without was
    /// photographed part-written - "Collect Carbon from Dudley e D" - and its
    /// place is the first letters of a name, not a name.
    /// </summary>
    public static bool Whole(ContractStep step) => step.Text.TrimEnd().EndsWith('.');

    /// <summary>
    /// "Location2Address", "Destination2Address": what the game prints in a
    /// contract's letter when it has no name for a place. Two of the nine
    /// 25 Oct frames listed one. It is a place the contract wants, unnamed,
    /// not a station called that.
    /// </summary>
    public static bool IsPlaceholder(string? place) =>
        place is not null && PlaceholderRegex().IsMatch(place.Trim());

    private static string? Clean(Group group) =>
        group.Success && group.Value.Trim().Length > 0 ? group.Value.Trim() : null;

    private static int? Count(Group group) =>
        group.Success && int.TryParse(group.Value.Replace('O', '0').Replace('o', '0'), out var n) ? n : null;

    /// <summary>
    /// "Seraphim Station above Crusader" is a place and the body it orbits;
    /// "The Golden Riviera on Pyro III" is a place and the body it stands on.
    /// The tail is taken only when it is short enough to be a body's name.
    /// A Lagrange station is printed either way round - "Patch City at the
    /// L3 Lagrange of Pyro III", "Beautiful Glen Station at Crusader's L5
    /// Lagrange point" - and the objective names it bare, so the tail has
    /// to come off for the two to be the same place.
    /// </summary>
    internal static (string Place, string? Body) SplitBody(string value)
    {
        var text = Whitespace().Replace(value, " ").Trim().TrimEnd('.').Trim();

        var lagrange = LagrangeRegex().Match(text);
        if (lagrange.Success)
            return (lagrange.Groups["place"].Value.Trim(),
                (lagrange.Groups["of"].Success ? lagrange.Groups["of"] : lagrange.Groups["whose"]).Value.Trim());

        foreach (var joint in new[] { " above ", " on " })
        {
            var at = text.LastIndexOf(joint, StringComparison.OrdinalIgnoreCase);
            if (at <= 0) continue;

            var body = text[(at + joint.Length)..].Trim();
            if (body.Split(' ').Length <= 3 && body.Length > 0)
                return (text[..at].Trim(), body);
        }

        return (text, null);
    }

    /// <summary>
    /// The contract text's own place list, when the selected contract prints
    /// one: "PICK UP LOCATIONS (ANY ORDER)" or "DROP OFF LOCATIONS (ANY
    /// ORDER)", then a dash per place.
    /// </summary>
    private static List<PickupSite> ReadSites(IReadOnlyList<ScreenTextLine> lines, string heading)
    {
        var head = lines.FirstOrDefault(line => Opens(line.Text, heading));
        if (head is null) return [];

        var sites = new List<PickupSite>();
        var raw = new List<string>();
        ScreenTextLine? last = head;

        foreach (var line in lines
            .Where(line => line.Top > head.Top)
            .Where(line => Math.Abs(line.Left - head.Left) <= head.Height * 2)
            .OrderBy(line => line.Top))
        {
            // The list ends at the first line that is neither a dash nor the
            // tail of a wrapped one, or at a paragraph gap; the letter's own
            // prose follows it.
            if (line.Top - last!.Top > last.Height * 2.5) break;

            var m = PickupSiteRegex().Match(line.Text);

            if (m.Success)
                raw.Add(m.Groups["place"].Value);
            else if (raw.Count > 0 && line.Top - last.Top <= last.Height * 1.4)
                raw[^1] += " " + line.Text.Trim();
            else
                break;

            last = line;
        }

        foreach (var text in raw)
        {
            var (place, body) = SplitBody(text);
            sites.Add(new PickupSite(place, body));
        }

        return sites;
    }

    /// <summary>The value printed on the same row as a label, to its right.</summary>
    private static string? Beside(IReadOnlyList<ScreenTextLine> lines, string label)
    {
        var at = lines.FirstOrDefault(line => Is(line.Text, label));
        if (at is null) return null;

        return lines
            .Where(line => line != at && line.Left > at.Left)
            .Where(line => Math.Abs(line.Top - at.Top) <= at.Height * 1.2)
            .OrderBy(line => line.Left)
            .FirstOrDefault()?.Text;
    }

    private static bool IsRepTag(string text)
    {
        var folded = ScreenInsight.Fold(text);
        return folded is "REP" or "REPI" or "REPL" or "REPT";
    }

    /// <summary>
    /// Whether two contract titles are the same contract.
    /// </summary>
    /// <remarks>
    /// The game's titles carry bars - "Junior | Stellar Small Haul | to Stanton
    /// Gateway" - which the screen sets in capitals and the engine reads as an
    /// I. A lone I is never a word in a title, so it goes from both sides.
    /// The rep bracket goes too: with StarStrings the detail panel sets
    /// "[50/200/250/500/1000/2000/4000" on the line under the title at the
    /// title's own size, so the headline reads it as part of the title - three
    /// of the nine 25 Oct frames did, and none of them matched its contract.
    /// </remarks>
    public static bool SameContract(string a, string b) =>
        TitleKey(a) == TitleKey(b) && TitleKey(a).Length > 0;

    private static string TitleKey(string title) =>
        ScreenInsight.Fold(string.Concat(TitleBracketRegex().Replace(title, "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => ScreenInsight.Plain(word) is not ("I" or "l" or "L" or "1" or ""))));

    // The rep ladder from its opening bracket - read as [, t, ( or nothing -
    // to the end of the title.
    [GeneratedRegex(@"\s*[\[t(lI]?\s*[0-9SsOo]{2,4}/[0-9SsOo]{3,4}/.*$")]
    private static partial Regex TitleBracketRegex();

    [GeneratedRegex(@"^ACCEPTED\s*\(?(?<n>\d+)\s*/\s*(?<of>\d+)\)?", RegexOptions.IgnoreCase)]
    private static partial Regex AcceptedTabRegex();

    // "[50/200/250/500/1000/2000/4000" - the bracket read as [, t or (, and
    // sometimes the figures with an S for a 5.
    [GeneratedRegex(@"^[\[t(lI]?\s*[0-9SsOo]{2,4}/[0-9SsOo]{3,4}/")]
    private static partial Regex BracketRegex();

    [GeneratedRegex(@"^\d+(?:[.,]\d+)?\s*[kKmM]$")]
    private static partial Regex CardRewardRegex();

    // The objective diamond reads as O, 0, o or a bullet.
    [GeneratedRegex(@"^[Oo0•◇◆]\s+(?<text>\S.*)$")]
    private static partial Regex ObjectiveRegex();

    // The same diamond read as a line of its own.
    [GeneratedRegex(@"^[Oo0•◇◆]$")]
    private static partial Regex LoneDiamondRegex();

    // An objective whose diamond went missing still opens with its verb.
    [GeneratedRegex(@"^(?:Deliver|Collect)\s")]
    private static partial Regex ObjectiveVerbRegex();

    // A part-written objective: "Deliver 0/189 SCU of Car", "Collect Potassium from".
    [GeneratedRegex(@"^(?:Deliver\s+(?<done>[0-9Oo]+)\s*/\s*(?<total>[0-9Oo]+)\s*SCU(?:\s+of\s+(?<cargo>\S.*?))?|Collect\s+(?<cargo>\S.*?)\s+from)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CutObjectiveRegex();

    [GeneratedRegex(@"^(?:Location|Destination|Pickup|Dropoff|Drop-off)\d*Address$", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderRegex();

    // "Deliver 0/18 SCU of Aluminum to Stanton Gateway." and, on a direct
    // haul, "Deliver 0/13 SCU to NB Int. Spaceport." with no cargo named. The
    // zero reads as O often enough that Count folds it back.
    [GeneratedRegex(@"^Deliver\s+(?<done>[0-9Oo]+)\s*/\s*(?<total>[0-9Oo]+)\s*SCU(?:\s+of\s+(?<cargo>.+?))?\s+to\s+(?<place>.+?)\.?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DeliverRegex();

    // "Collect Aluminum from Fallow Field."
    [GeneratedRegex(@"^Collect\s+(?<cargo>.+?)\s+from\s+(?<place>.+?)\.?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CollectRegex();

    // "- Freight elevator at Fallow Field on Pyro IV"; the dash reads as any
    // of its lookalikes and the elevator is not always mentioned.
    [GeneratedRegex(@"^[-–—•·]\s*(?:Freight\s+elevator\s+at\s+)?(?<place>.+?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PickupSiteRegex();

    // "Patch City at the L3 Lagrange of Pyro III" and "Beautiful Glen Station
    // at Crusader's L5 Lagrange point": the L reads as I or 1 as often as
    // not, and the 21 Sep frames read L5 as "LS" and L1 as "LI". The 25 Oct
    // frames add "1.4" for L4, "L 3" for L3 and "LSE" for L5.
    [GeneratedRegex(@"^(?<place>.+?)\s+at\s+(?:the\s+[LI1][.,]?\s?[\dSOIl][A-Z]?\s+Lagrange\s+(?:point\s+)?of\s+(?<of>.+?)|(?<whose>.+?)'?s\s+[LI1][.,]?\s?[\dSOIl][A-Z]?\s+Lagrange(?:\s+point)?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex LagrangeRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Whitespace();

    // ---- the Fleet Manager ----

    /// <summary>The Fleet Manager terminal, or null when this is not it.</summary>
    /// <remarks>
    /// A table with its own column headings, which is the best kind of screen
    /// to read: every field is found by the column it sits under and the row
    /// it shares. The names are the weak part - the terminal's CRT face read
    /// two of five cleanly on the measured frame - and a name that does not
    /// read is kept as read, not guessed.
    /// </remarks>
    private static FleetReading? ReadFleet(IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<string> shipNames)
    {
        if (!lines.Any(line => Is(line.Text, "FLEET MANAGER"))) return null;

        var vehicle = lines.FirstOrDefault(line => Is(line.Text, "VEHICLE") || Is(line.Text, "UEHICLE"));
        var location = lines.FirstOrDefault(line => Is(line.Text, "LOCATION"));
        var info = lines.FirstOrDefault(line => Is(line.Text, "INFO"));
        var focus = lines.FirstOrDefault(line => Is(line.Text, "FOCUS"));
        var cargo = lines.FirstOrDefault(line => Opens(line.Text, "CARGO"));

        if (vehicle is null) return new FleetReading([]);

        var names = lines
            .Where(line => line.Top > vehicle.Top + vehicle.Height)
            .Where(line => Math.Abs(line.Left - vehicle.Left) <= vehicle.Height * 3)
            .Where(line => line.Text.Length >= 4)
            .OrderBy(line => line.Top)
            .ToList();

        var rows = new List<FleetRow>();

        foreach (var name in names)
        {
            // A row is as tall as its name and the two lines a wrapped
            // focus takes beneath it.
            bool InRow(ScreenTextLine line) =>
                line.Top >= name.Top - name.Height * 1.5 && line.Top <= name.Top + name.Height * 3.5;

            string? Under(ScreenTextLine? header, bool join = false)
            {
                if (header is null) return null;

                var found = lines
                    .Where(InRow)
                    .Where(line => line.Left >= header.Left - header.Height && line.Left <= header.Left + header.Height * 4)
                    .OrderBy(line => line.Top)
                    .Select(line => line.Text.TrimStart('-', '–', '—', ' '))
                    .ToList();

                return found.Count == 0 ? null : join ? string.Join(' ', found) : found[0];
            }

            var (ship, alike) = NameShip(name.Text, shipNames);

            var cargoText = Under(cargo);

            rows.Add(new FleetRow(
                name.Text, ship, alike,
                Under(location),
                Under(info),
                Under(focus, join: true),
                cargoText is not null && int.TryParse(cargoText, out var scu) ? scu : null));
        }

        return new FleetReading(rows);
    }

    /// <summary>A ship name as read, matched the way the loadout's dropdown is.</summary>
    private static (string? Ship, IReadOnlyList<string> LooksLike) NameShip(string read, IReadOnlyList<string> shipNames)
    {
        // The Fleet Manager draws a glyph in the row's margin that the engine
        // reads as a letter - "V Drake Ironclad" on the frame of 2026-09-15 -
        // and a one-letter first word is never a ship's. Dropped before the
        // names are tried, not from the text kept.
        var words = read.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 2 && ScreenInsight.Plain(words[0]).Length <= 1) read = string.Join(' ', words[1..]);

        var plain = ScreenInsight.Plain(read);
        var folded = ScreenInsight.Fold(read);

        var exact = shipNames
            .Where(name => string.Equals(ScreenInsight.Plain(name), plain, StringComparison.OrdinalIgnoreCase)
                || ScreenInsight.Fold(name) == folded)
            .Distinct()
            .ToList();

        if (exact.Count == 1) return (exact[0], []);
        if (exact.Count > 1) return (null, exact);

        // A name the read carries whole, word for word, with something around
        // it - a state word, what the glyph became. When the read holds both
        // "Drake Ironclad" and "Drake Ironclad Assault" it said the longer one;
        // when it holds only the shorter, the longer is not what it said.
        var readWords = words.Select(ScreenInsight.Fold).ToList();
        var contained = shipNames
            .Where(name =>
            {
                var nameWords = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(ScreenInsight.Fold).ToList();
                return nameWords.Count > 0 && Enumerable.Range(0, Math.Max(0, readWords.Count - nameWords.Count + 1))
                    .Any(start => nameWords.Select((w, i) => readWords[start + i] == w).All(same => same));
            })
            .Distinct()
            .OrderByDescending(name => name.Length)
            .ToList();

        if (contained.Count > 0) return (contained[0], []);

        var model = read.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (model is null || ScreenInsight.Plain(model).Length < 5) return (null, []);

        var modelFolded = ScreenInsight.Fold(model);

        return (null, [.. shipNames
            .Where(name => name.Split(' ').Any(word => ScreenInsight.Fold(word) == modelFolded))
            .Distinct()]);
    }

    // ---- the Loadout Estimate ----

    /// <summary>The Fleet Manager's loadout estimate, read as a loadout.</summary>
    /// <remarks>
    /// The same facts as the Vehicle Loadout Manager in a different shape: a
    /// table of parts with quantities and types rather than a tree of ports.
    /// It reads as a <see cref="LoadoutReading"/> so the fleet page and the
    /// factory comparison need no second path, with the type standing in for
    /// the port and the quantity carried in its label.
    /// </remarks>
    private static LoadoutReading? ReadManifest(
        IReadOnlyList<ScreenTextLine> lines,
        IReadOnlyList<ItemReference> items,
        IReadOnlyList<string> shipNames)
    {
        if (!lines.Any(line => Is(line.Text, "LOADOUT ESTIMATE"))) return null;

        var nameHead = lines.FirstOrDefault(line => Is(line.Text, "NAME"));
        var qtyHead = lines.FirstOrDefault(line => Is(line.Text, "QTY"));
        var typeHead = lines.FirstOrDefault(line => Is(line.Text, "TYPE"));

        // The ship is printed beside the table's header row, in capitals.
        var shipRead = nameHead is null ? null : lines
            .Where(line => Math.Abs(line.Top - nameHead.Top) <= nameHead.Height * 2)
            .Where(line => typeHead is null || line.Left > typeHead.Left + typeHead.Height * 3)
            .Where(line => line.Text.Length >= 4 && line.Text == line.Text.ToUpperInvariant() && line.Text.Any(char.IsLetter))
            .OrderBy(line => line.Top)
            .Select(line => line.Text)
            .FirstOrDefault(text => NameShip(text, shipNames).Ship is not null)
            ?? lines
                .Where(line => nameHead is not null && Math.Abs(line.Top - nameHead.Top) <= nameHead.Height * 2)
                .Where(line => typeHead is null || line.Left > typeHead.Left + typeHead.Height * 3)
                .OrderBy(line => line.Top)
                .Select(line => line.Text)
                .FirstOrDefault();

        var (ship, alike) = shipRead is null ? (null, []) : NameShip(shipRead, shipNames);

        var fittings = new List<ScreenFitting>();

        if (nameHead is not null)
        {
            var rows = lines
                .Where(line => line.Top > nameHead.Top + nameHead.Height)
                .Where(line => Math.Abs(line.Left - nameHead.Left) <= nameHead.Height * 2)
                .OrderBy(line => line.Top)
                .ToList();

            foreach (var row in rows)
            {
                string? Under(ScreenTextLine? header) => header is null ? null : lines
                    .Where(line => Math.Abs(line.Top - row.Top) <= row.Height * 1.2)
                    .Where(line => Math.Abs(line.Left - header.Left) <= header.Height * 2)
                    .Select(line => line.Text)
                    .FirstOrDefault();

                var type = Under(typeHead) ?? "(type did not read)";
                var qty = Under(qtyHead) is { } q && int.TryParse(q, out var count) ? count : (int?)null;
                var slot = qty is > 1 ? $"{type} ×{qty}" : type;

                fittings.Add(Fitting(slot, row.Text, items));
            }
        }

        var total = Beside(lines, "TOTAL ITEMS");
        var fee = Beside(lines, "REPLACEMENT") is { } feeText ? Figure(feeText) : null;

        var scope = "The loadout estimate at the Fleet Manager"
            + (total is not null ? $": {total} items" : "")
            + (fee is { } f ? $", replacement fee {f:N0} aUEC" : "")
            + ".";

        return new LoadoutReading(shipRead, ship, alike, scope, fittings);
    }

    // ---- the Rep app ----

    /// <summary>The mobiGlas Rep app, or null when this is not it.</summary>
    /// <remarks>
    /// The list of organisations reads, the selected one reads because it is
    /// set large, and the standing under it reads. So do the rank cards'
    /// labels, all of them alike; which one is held is drawn rather than
    /// written, so the rank comes later from the bars - see
    /// <see cref="RepBars"/> - and a reading made from text alone carries it
    /// as null.
    /// </remarks>
    private static ReputationReading? ReadReputation(IReadOnlyList<ScreenTextLine> lines)
    {
        if (RepHead(lines) is not var (_, org, standing, lone)) return null;

        // One tab is a weaker sign than two, so it takes the ladder to make
        // the case: a lone DOSSIER with no rank cards under it is not this app.
        if (lone && RepCards(lines).Count < 3) return null;

        var search = lines.FirstOrDefault(line => Is(line.Text, "SEARCH"));

        var organisations = search is null ? [] : lines
            .Where(line => line.Top > search.Top + search.Height)
            .Where(line => line.Left >= search.Left + search.Height * 2 && line.Left <= search.Left + search.Height * 8)
            .Where(line => line.Height >= search.Height * 0.8)
            .Where(line => line.Text.Length >= 4)
            .OrderBy(line => line.Top)
            .Select(line => line.Text)
            .ToList();

        var cards = RepCards(lines);

        return new ReputationReading(org?.Text, standing?.Text, null, organisations,
            RepSection(lines, org, standing)?.Text is { } section ? Section(section) : null,
            cards.Count > 0 ? [.. cards.Select(c => new RepRankCard(c.Name, c.Perk, null))] : null);
    }

    /// <summary>
    /// The Rep app's tabs, the organisation open and the standing under it;
    /// null when this is not the app. <c>Lone</c> when only one tab read.
    /// </summary>
    /// <remarks>
    /// CAREER is white on a green pill, and on one frame of fifteen - Red Wind
    /// at 16:25 on 24 October, a flare across the left of the panel - the
    /// engine returned DOSSIER and not CAREER, and the frame was filed as a
    /// mobiGlas screen nobody can read. So either tab anchors, measured from
    /// where the tabs sit: the name starts just left of CAREER, about nine of
    /// DOSSIER's heights left of DOSSIER.
    /// </remarks>
    private static (ScreenTextLine Tab, ScreenTextLine? Org, ScreenTextLine? Standing, bool Lone)? RepHead(IReadOnlyList<ScreenTextLine> lines)
    {
        var career = lines.FirstOrDefault(line => Is(line.Text, "CAREER"));
        var dossier = lines.FirstOrDefault(line => Is(line.Text, "DOSSIER"));

        if (career is not null && dossier is not null && Math.Abs(career.Top - dossier.Top) > career.Height) return null;
        if ((career ?? dossier) is not { } tab) return null;

        var lone = career is null || dossier is null;
        var reach = career is not null ? career.Height : 10 * tab.Height;

        var org = lines
            .Where(line => line.Top > tab.Top + tab.Height && line.Left >= tab.Left - reach)
            .Where(line => line.Height >= tab.Height * 1.6)
            .OrderBy(line => line.Top)
            .FirstOrDefault();

        var standing = org is null ? null : lines
            .Where(line => line != org && line.Top > org.Top && line.Top - org.Top <= org.Height * 2.5)
            .Where(line => Math.Abs(line.Left - org.Left) <= org.Height)
            .OrderBy(line => line.Top)
            .FirstOrDefault();

        return (tab, org, standing, lone);
    }

    /// <summary>
    /// The ladder's heading: the first line under the standing, in line with
    /// the name and set larger than the standing - 22 px against 17 on a
    /// 3440-wide frame.
    /// </summary>
    private static ScreenTextLine? RepSection(IReadOnlyList<ScreenTextLine> lines, ScreenTextLine? org, ScreenTextLine? standing)
    {
        if (org is null || standing is null) return null;

        return lines
            .Where(line => line.Top > standing.Top + standing.Height)
            .Where(line => Math.Abs(line.Left - org.Left) <= org.Height)
            .Where(line => line.Height >= standing.Height * 1.1 && line.Height < org.Height)
            .OrderBy(line => line.Top)
            .FirstOrDefault();
    }

    /// <summary>
    /// The engine reads the ampersand of BARTER &amp; TRADE as a lone "e". A
    /// heading the game never translated is shown as its key -
    /// "@REPSCOPE_SHIPCOMBAT_HEADHUNTERS_NAME" on the Headhunters' page in
    /// 4.10.2 - and is kept as read, because the key is exactly what names
    /// the ladder; see <see cref="ReputationLadders"/>.
    /// </summary>
    private static string Section(string read) => read.Replace(" e ", " & ");

    /// <summary>
    /// The rank cards' labels, grouped a card at a time, in the order the app
    /// lays them out: across, then down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A card's label is left-aligned and runs to two lines - "JR." over
    /// "CONTRACTOR" - and some carry a perk under it in a smaller face:
    /// "SPECIAL WOLF" / "BARTER CONTRACT" under Wikelo's ranks, "ADDITIONAL" /
    /// "CONTRACTS" under the Headhunters'. The perk is told from the name by
    /// height, 11-12 px against 13-16; the closest pair measured was RANK V
    /// at 14 over ADDITIONAL at 12.
    /// </para>
    /// <para>
    /// The grid sits right of the organisation's name, below the standing,
    /// and above the mobiGlas's own bar of app names, which are set at much
    /// the same size and would otherwise read as one more row of ranks.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<RepBars.Card> RepCards(IReadOnlyList<ScreenTextLine> lines)
    {
        if (RepHead(lines) is not (_, { } org, { } standing, _)) return [];

        var below = RepSection(lines, org, standing) ?? standing;

        var dock = lines
            .Where(line => DockApps.Contains(line.Text.Trim()))
            .Select(line => line.Top)
            .DefaultIfEmpty(double.MaxValue)
            .Min();

        var floor = Math.Min(dock - org.Height, org.Top + 22 * org.Height);

        var candidates = lines
            .Where(line => line.Top > below.Top + below.Height * 2 && line.Top < floor)
            .Where(line => line.Left >= org.Left - org.Height)
            .Where(line => line.Height < standing.Height * 1.1)
            .Where(line => line.Text.Count(char.IsLetter) >= 2)
            .OrderBy(line => line.Top)
            .ThenBy(line => line.Left)
            .ToList();

        if (candidates.Count == 0) return [];

        // Only a line set at a label's size starts a card; a smaller one can
        // only follow one. The Bounty Hunters' VETERAN GUILD MEMBER card has
        // "GUILD" / "STEWARD" printed over its corner in the perk's face -
        // the game's, not the engine's - and as the topmost line it took the
        // card's name and its place in the order. Labels measure 13-16 px,
        // perks and that caption 10-12, and the frames that set the bar
        // tightest are Intersec's, CONTRACTOR at 16 beside NEUTRAL at 13.
        var labelSize = candidates.Max(line => line.Height) * 0.8;
        var groups = new List<List<ScreenTextLine>>();

        foreach (var line in candidates)
        {
            var group = groups.FirstOrDefault(g =>
                Math.Abs(g[0].Left - line.Left) <= 1.5 * g[0].Height
                && line.Top - g[^1].Top > 0
                && line.Top - g[^1].Top <= 2.2 * g[^1].Height);

            if (group is not null) group.Add(line);
            else if (line.Height >= labelSize) groups.Add([line]);
        }

        var typical = groups.Select(g => g[0].Height).Order().ElementAt(groups.Count / 2);

        var cards = new List<RepBars.Card>();

        foreach (var group in groups)
        {
            var label = group[0];
            var name = group.TakeWhile(line => line.Height >= label.Height * 0.88).ToList();
            var perk = group.Skip(name.Count).ToList();

            cards.Add(new RepBars.Card(
                RankName(string.Join(" ", name.Select(line => line.Text.Trim()))),
                perk.Count > 0 ? string.Join(" ", perk.Select(line => line.Text.Trim())) : null,
                label));
        }

        // Rows by the first label's line, then across; the labels of one
        // row sit within a few pixels of each other and rows are twenty
        // label heights apart.
        var rows = new List<List<RepBars.Card>>();

        foreach (var card in cards.OrderBy(c => c.Label.Top))
        {
            if (rows.Count > 0 && card.Label.Top - rows[^1][0].Label.Top <= 2 * typical)
                rows[^1].Add(card);
            else
                rows.Add([card]);
        }

        return [.. rows.SelectMany(row => row.OrderBy(c => c.Label.Left))];
    }

    /// <summary>
    /// The mobiGlas's app bar, along the bottom of every app. Not CONTRACTS,
    /// which is also the second line of the Headhunters' perk.
    /// </summary>
    private static readonly HashSet<string> DockApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "HOME", "HEALTH", "COMMS", "MAPS", "JOURNAL", "ASSETS", "WALLET", "LANDING", "VEHICLES",
    };

    /// <summary>"RANK Ill" is RANK III: the engine reads a roman I as a lower-case l.</summary>
    private static string RankName(string read) =>
        read.StartsWith("RANK ", StringComparison.OrdinalIgnoreCase)
            ? "RANK " + read[5..].Replace('l', 'I')
            : read;
}
