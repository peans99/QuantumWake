namespace Quantumwake.Data;

/// <summary>What one screenshot's text was understood to say.</summary>
/// <param name="Name">
/// The line taken to be the thing's name - the unlabelled line directly above
/// the first labelled one, which is where the game puts it in an item tooltip.
/// Null when nothing on the frame looked like a tooltip at all.
/// </param>
/// <param name="Fields">
/// The labelled lines, by label. Values are kept exactly as read: correcting
/// them here would hide the correction from everything downstream.
/// </param>
public sealed record ScreenReading(
    string? Name,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<string> Lines);

/// <summary>How alike a read name and a catalogue name are.</summary>
/// <remarks>
/// Three named steps rather than a score, because the difference between them
/// is a difference in kind. <see cref="Exact"/> is the same string.
/// <see cref="Confusable"/> is the same string once the errors this engine was
/// measured making are undone. <see cref="Partial"/> is one name inside the
/// other, which is what a clipped tooltip looks like and is never on its own
/// enough to name a thing.
/// </remarks>
public enum ScreenMatchTier { Exact, Confusable, Partial }

/// <summary>One thing the read name might be.</summary>
/// <param name="Agrees">
/// Tooltip fields that hold for this candidate, worded for a reader:
/// "manufacturer", "volume". The whole point of the design - a name alone
/// cannot be trusted at the lower tiers, and the tooltip carries four more
/// facts that the catalogue also holds.
/// </param>
/// <param name="Disagrees">
/// Fields that plainly contradict it. A candidate is kept and shown rather
/// than dropped, because the contradiction is more useful on screen than off
/// it: it is usually the reading that is wrong and not the catalogue, and a
/// reader who can see which field disagreed can tell at a glance which.
/// </param>
public sealed record ScreenCandidate(
    ItemReference Item,
    ScreenMatchTier Tier,
    IReadOnlyList<string> Agrees,
    IReadOnlyList<string> Disagrees);

/// <summary>One line of a frame, and what the catalogue made of it.</summary>
/// <param name="Named">
/// One candidate and it read perfectly. Anything less is a line worth showing
/// with its candidates beside it, not a thing this app will say it saw.
/// </param>
public sealed record ScreenLine(string Text, IReadOnlyList<ScreenCandidate> Candidates)
{
    public bool Named =>
        Candidates.Count == 1 && Candidates[0].Tier == ScreenMatchTier.Exact;
}

/// <summary>What a screenshot's text was matched to, and how sure that is.</summary>
public sealed record ScreenMatchResult(
    ScreenReading Reading,
    IReadOnlyList<ScreenCandidate> Candidates)
{
    /// <summary>
    /// One candidate, nothing contradicting it, and either the name read
    /// perfectly or another field vouched for it.
    /// </summary>
    /// <remarks>
    /// Deliberately hard to earn. A confusable-tier name on its own is a guess
    /// with a good story, and this app does not present those as answers.
    /// </remarks>
    public bool Certain =>
        Candidates.Count == 1
        && Candidates[0].Disagrees.Count == 0
        && (Candidates[0].Tier == ScreenMatchTier.Exact || Candidates[0].Agrees.Count > 0);

    /// <summary>
    /// Why there is no answer, in the reader's words, or null when there is one.
    /// </summary>
    public string? Trouble =>
        Reading.Name is null ? "nothing on this frame looked like an item tooltip"
        : Candidates.Count == 0 ? $"nothing in the catalogue reads like \"{Reading.Name}\""
        : Candidates.Count > 1 ? $"{Candidates.Count} things in the catalogue read the same"
        : Candidates[0].Disagrees.Count > 0 ? "the name matched but the details do not"
        : Certain ? null
        : "the name is close but nothing else vouches for it";
}

/// <summary>
/// Turns the text an OCR engine returned into a thing from the catalogue.
/// </summary>
/// <remarks>
/// <para>
/// Built on what step 1 measured rather than on what fuzzy matching usually
/// does. Two findings shape all of it. Item names come back character-perfect
/// most of the time, and where they do not the errors are a short, closed list
/// of glyph shapes the engine confuses under thirteen pixels. And the tooltip
/// carries four more facts beside the name - manufacturer, type, class and
/// volume - every one of which the catalogue also holds.
/// </para>
/// <para>
/// So this does not score edit distance. A scorer loose enough to turn
/// <c>MSO-423</c> back into <c>MSD-423</c> is loose enough to read
/// <c>P4-AR</c> as <c>P8-AR</c>, and confidently naming the wrong weapon is
/// worse than admitting to two candidates. Instead the confusions that were
/// actually observed are folded away and the comparison stays an equality;
/// everything else is left to disagree. What buys the certainty back is the
/// corroboration, which is free, and which no amount of string cleverness is
/// a substitute for.
/// </para>
/// </remarks>
public static class ScreenInsight
{
    /// <summary>Tooltip labels, as the game writes them.</summary>
    /// <remarks>
    /// Compared after folding, so <c>Manufacturer.</c> - the reading the engine
    /// actually returned, its colon flattened to a full stop - still lands.
    /// </remarks>
    /// <remarks>
    /// There is more than one tooltip in this game. A looted weapon is
    /// described with <c>Item Type</c> and <c>Class</c>; a ship component in
    /// the loadout manager uses a bare <c>Type</c>, and carries no volume and
    /// no name the engine could read at all. Both layouts are in the list.
    /// </remarks>
    private static readonly string[] Labels =
    [
        "Volume", "Manufacturer", "Item Type", "Class", "Type",
        "Magazine Size", "Rate Of Fire", "Effective Range", "Attachments",
    ];

    /// <summary>
    /// The glyph confusions this engine was measured making, and no others.
    /// </summary>
    /// <remarks>
    /// Every pair here came out of a real screenshot from a real install:
    /// <c>]</c> read as <c>1</c>, <c>[</c> as <c>t</c>, <c>D</c> as <c>O</c>,
    /// <c>1</c> as <c>I</c>, <c>5</c> as <c>S</c>, <c>&amp;</c> as <c>B</c>,
    /// and the micro sign as <c>p</c>. Adding a pair nobody has seen is how
    /// this stops being a bounded correction and starts being a guess, so the
    /// list grows from evidence and from nothing else.
    /// </remarks>
    private static readonly Dictionary<char, char> Confusions = new()
    {
        ['O'] = 'O', ['0'] = 'O', ['D'] = 'O', ['Q'] = 'O',
        ['I'] = 'I', ['1'] = 'I', ['L'] = 'I', [']'] = 'I', ['|'] = 'I',
        ['S'] = 'S', ['5'] = 'S',
        ['B'] = 'B', ['8'] = 'B', ['&'] = 'B',
        ['T'] = 'T', ['['] = 'T',
        ['U'] = 'U', ['V'] = 'U', ['μ'] = 'U', ['µ'] = 'U',
        ['Z'] = 'Z', ['2'] = 'Z',
    };

    /// <summary>
    /// Fields that can vouch for a candidate but never convict it.
    /// </summary>
    /// <remarks>
    /// See <see cref="Corroborate"/>: these are looked for in the class name,
    /// which is an id rather than a description, so its silence proves nothing.
    /// </remarks>
    private static readonly string[] Vouching = ["Item Type", "Class", "Type"];

    /// <summary>Shorter than this and a partial match means nothing.</summary>
    /// <remarks>
    /// "Rifle" inside "Arlington Rifle" is not a sighting of the Arlington, and
    /// at five characters a great many catalogue names contain one another.
    /// </remarks>
    private const int LongEnoughToContain = 6;

    /// <summary>Reads the lines an engine returned as a tooltip.</summary>
    public static ScreenReading Read(IEnumerable<string> lines)
    {
        var all = lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var firstLabelled = -1;

        for (var i = 0; i < all.Count; i++)
        {
            if (LabelOn(all[i]) is not { } found) continue;

            if (firstLabelled < 0) firstLabelled = i;

            // First wins. A frame can hold two tooltips - a hovered item and
            // the one already equipped, shown for comparison - and taking the
            // later would describe the thing the reader did not point at.
            if (!fields.ContainsKey(found.Label)) fields[found.Label] = found.Value;
        }

        // The name sits directly above the stats, so an unlabelled line further
        // up is a panel title - "LOOTING VIEW" - rather than the thing itself.
        //
        // With no labelled line at all there is no tooltip, and a whole frame
        // of loadout text has no one thing it is about: taking its first line
        // would name the panel. One line on its own is different - that is a
        // crop of a name, and the only thing it can be.
        var name =
            firstLabelled > 0 ? all[firstLabelled - 1]
            : firstLabelled < 0 && all.Count == 1 ? all[0]
            : null;

        return new ScreenReading(name, fields, all);
    }

    /// <summary>Everything in the catalogue the read name could be.</summary>
    public static ScreenMatchResult Match(ScreenReading reading, IReadOnlyList<ItemReference> items)
    {
        if (reading.Name is null) return new ScreenMatchResult(reading, []);

        var candidates = Candidates(reading.Name, items)
            .Select(hit => new ScreenCandidate(
                hit.Item,
                hit.Tier,
                [.. Corroborate(reading, hit.Item, agreeing: true)],
                [.. Corroborate(reading, hit.Item, agreeing: false)]))
            .ToList();

        if (candidates.Count == 0) return new ScreenMatchResult(reading, []);

        // A field that plainly contradicts costs more than a name that only
        // nearly fits, so a clean partial outranks a contradicted exact.
        var ranked = candidates
            .OrderBy(c => c.Disagrees.Count)
            .ThenBy(c => (int)c.Tier)
            .ThenByDescending(c => c.Agrees.Count)
            .ToList();

        // Everything as good as the best is kept. Trimming ties down to one is
        // the single thing this must never do: two candidates is a useful
        // answer, and a coin toss dressed as an answer is not.
        var top = ranked[0];

        return new ScreenMatchResult(reading, [.. ranked.Where(c =>
            c.Disagrees.Count == top.Disagrees.Count
            && c.Tier == top.Tier
            && c.Agrees.Count == top.Agrees.Count)]);
    }

    /// <summary>Reads and matches in one go.</summary>
    public static ScreenMatchResult Look(IEnumerable<string> lines, IReadOnlyList<ItemReference> items)
        => Match(Read(lines), items);

    /// <summary>
    /// Every line of a frame that names something in the catalogue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half of the problem, and the commoner one. A tooltip is one
    /// thing described in depth; a loadout screen is thirty things named and
    /// nothing else - no labels to anchor on, no stats to corroborate with,
    /// and no single thing the frame is about.
    /// </para>
    /// <para>
    /// So this asks a smaller question of every line and accepts a smaller
    /// answer. Nothing corroborates here, which is why partial matches are
    /// refused outright: with no manufacturer and no volume to check a guess
    /// against, half a name is not evidence of anything.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ScreenLine> Sweep(
        IEnumerable<string> lines, IReadOnlyList<ItemReference> items)
    {
        var found = new List<ScreenLine>();

        foreach (var line in lines.Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            // Panel furniture - "Misc.", "Systems", a stray "L" - is shorter
            // than anything the catalogue calls a thing.
            if (Plain(line).Length < LongEnoughToContain) continue;

            // A line ending in a colon introduces something; it does not name
            // it. Worth a rule of its own because the catalogue holds entries
            // called "Available" - empty rack slots - so the loadout screen's
            // "Available:" header matched three of them exactly.
            if (line.EndsWith(':')) continue;

            var hits = Candidates(line, items)
                .Where(hit => hit.Tier != ScreenMatchTier.Partial)
                .Select(hit => new ScreenCandidate(hit.Item, hit.Tier, [], []))
                .ToList();

            if (hits.Count == 0) continue;

            // An exact reading beats the confusable ones it sits beside, the
            // same way it does for a tooltip.
            var best = hits.Min(h => (int)h.Tier);

            found.Add(new ScreenLine(line, [.. hits.Where(h => (int)h.Tier == best)]));
        }

        return found;
    }

    private static IEnumerable<(ItemReference Item, ScreenMatchTier Tier)> Candidates(
        string name, IReadOnlyList<ItemReference> items)
    {
        var plain = Plain(name);
        if (plain.Length == 0) yield break;

        var folded = Fold(name);

        foreach (var item in items)
        {
            if (item.Name is not { Length: > 0 } known) continue;

            var theirs = Plain(known);
            if (theirs.Length == 0) continue;

            if (string.Equals(plain, theirs, StringComparison.OrdinalIgnoreCase))
            {
                yield return (item, ScreenMatchTier.Exact);
                continue;
            }

            var theirsFolded = Fold(known);

            if (folded == theirsFolded)
            {
                yield return (item, ScreenMatchTier.Confusable);
                continue;
            }

            // A tooltip clipped by the edge of the panel, or a name the frame
            // only showed half of.
            if (folded.Length >= LongEnoughToContain
                && theirsFolded.Length >= LongEnoughToContain
                && (theirsFolded.Contains(folded) || folded.Contains(theirsFolded)))
            {
                yield return (item, ScreenMatchTier.Partial);
            }
        }
    }

    /// <summary>
    /// Tooltip fields that hold, or that contradict, for one candidate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A field the tooltip did not carry is neither. Silence is not agreement,
    /// and counting it as either would make a two-line reading look as well
    /// evidenced as a full one.
    /// </para>
    /// <para>
    /// Two of the four fields can convict and two can only vouch, and which is
    /// which was settled by asking this install rather than by guessing. The
    /// Arlington's tooltip says <c>Item Type: Rifle</c> and <c>Class:
    /// Ballistic</c>; the catalogue's own <c>Type</c> and <c>SubType</c> for
    /// the same weapon are <c>WeaponPersonal</c> and <c>Medium</c>. They are
    /// not the same vocabulary and never line up, so comparing them directly
    /// reported a contradiction on a candidate that was in fact correct.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> Corroborate(
        ScreenReading reading, ItemReference item, bool agreeing)
    {
        // The manufacturer is the one field where the tooltip and the
        // catalogue use the same words for the same thing.
        if (reading.Fields.TryGetValue("Manufacturer", out var maker)
            && item.Manufacturer is { Length: > 0 } theirs
            && (Fold(maker) == Fold(theirs)) == agreeing)
        {
            yield return "manufacturer";
        }

        // Volume is the one number worth trusting, because the catalogue holds
        // it in the unit the tooltip prints - micro-SCU, no conversion, and no
        // rounding to argue about. It is also what separates a weapon from its
        // magazine: 13000 against 480, same maker and nearly the same name.
        if (reading.Fields.TryGetValue("Volume", out var volume)
            && Micro(volume) is { } read
            && item.MicroScu > 0
            && (read == item.MicroScu) == agreeing)
        {
            yield return "volume";
        }

        // The tooltip's words for what a thing *is* do turn up in the
        // catalogue, but in the class name - hdgw_rifle_ballistic_01 carries
        // both "rifle" and "ballistic". Finding them there is worth something.
        // Not finding them is worth nothing: a class name is an id, and owes
        // no particular words to anybody. So these can only ever vouch.
        if (!agreeing) yield break;

        foreach (var label in Vouching)
        {
            if (!reading.Fields.TryGetValue(label, out var word)) continue;

            var folded = Fold(word);
            if (folded.Length > 0 && Fold(item.ClassName).Contains(folded))
                yield return label.ToLowerInvariant();
        }
    }

    /// <summary>The leading whole number, or null when there is not one.</summary>
    /// <remarks>
    /// Null rather than zero, and never a repair. <c>SO m</c> was a real
    /// reading of <c>50 m</c>, and a parser that helpfully turned the S into a
    /// 5 would be inventing a measurement. A number that does not parse is a
    /// number this app does not have.
    /// </remarks>
    private static long? Micro(string value)
    {
        var digits = new string([.. value.TrimStart().TakeWhile(char.IsAsciiDigit)]);
        return long.TryParse(digits, out var number) ? number : null;
    }

    private static (string Label, string Value)? LabelOn(string line)
    {
        // The separator is a colon the engine sometimes reads as a full stop,
        // so both count - and nothing else does, or every sentence of the lore
        // paragraph underneath becomes a field.
        var cut = line.IndexOfAny([':', '.']);
        if (cut <= 0 || cut == line.Length - 1) return null;

        var label = Fold(line[..cut]);

        foreach (var known in Labels)
        {
            if (label == Fold(known)) return (known, line[(cut + 1)..].Trim());
        }

        return null;
    }

    /// <summary>Case and punctuation removed, nothing else.</summary>
    private static string Plain(string text) =>
        new([.. text.Where(char.IsLetterOrDigit)]);

    /// <summary>Plain, with the measured confusions collapsed.</summary>
    private static string Fold(string text) =>
        new([.. Plain(text).ToUpperInvariant()
            .Select(c => Confusions.TryGetValue(c, out var to) ? to : c)]);
}
