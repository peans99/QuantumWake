using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>One card in the Contracts app's accepted list.</summary>
/// <param name="Reward">As printed on the card - "215k" - because the card rounds and the detail panel does not.</param>
public sealed record ContractCard(string Title, string? Reward, string? Issuer);

/// <summary>What the mobiGlas Contracts app said, on its Accepted tab.</summary>
/// <param name="Accepted">The count in the tab itself: "ACCEPTED (5/10)".</param>
/// <param name="SelectedReward">The detail panel's figure, in aUEC, with the ¤ read as whatever the engine made of it.</param>
public sealed record ContractsReading(
    int? Accepted,
    int? Capacity,
    IReadOnlyList<ContractCard> Cards,
    string? SelectedTitle,
    long? SelectedReward,
    string? SelectedIssuer,
    IReadOnlyList<string> Objectives);

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
/// <param name="Standing">"NEUTRAL", as printed under the name.</param>
/// <param name="Rank">
/// Always null, and worth a field so the absence is a statement: the current
/// rank is a highlighted card among eight identical ones, and a highlight is
/// not text.
/// </param>
public sealed record ReputationReading(
    string? Organisation,
    string? Standing,
    string? Rank,
    IReadOnlyList<string> Organisations);

public static partial class ScreenFrames
{
    // ---- the Contracts app ----

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

        var selected = lines
            .Where(line => line.Left > panelLeft - tab.Height * 2 && line.Top > tab.Top)
            .Where(line => line.Height >= tab.Height * 2)
            .OrderBy(line => line.Top)
            .FirstOrDefault()?.Text;

        var reward2 = Beside(lines, "Reward") is { } figure ? Figure(figure) : null;
        var issuer2 = Beside(lines, "Contracted By");

        var objectivesHead = lines.FirstOrDefault(line => Is(line.Text, "PRIMARY OBJECTIVES"));

        var objectives = objectivesHead is null ? [] : lines
            .Where(line => line.Top > objectivesHead.Top)
            .Where(line => line.Left >= objectivesHead.Left - objectivesHead.Height)
            .Select(line => ObjectiveRegex().Match(line.Text))
            .Where(m => m.Success)
            .Select(m => m.Groups["text"].Value.Trim())
            .ToList();

        return new ContractsReading(accepted, capacity, cards, selected, reward2, issuer2, objectives);
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
    /// </remarks>
    public static bool SameContract(string a, string b) =>
        TitleKey(a) == TitleKey(b) && TitleKey(a).Length > 0;

    private static string TitleKey(string title) =>
        ScreenInsight.Fold(string.Concat(title
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => ScreenInsight.Plain(word) is not ("I" or "l" or "L" or "1" or ""))));

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
        var plain = ScreenInsight.Plain(read);
        var folded = ScreenInsight.Fold(read);

        var exact = shipNames
            .Where(name => string.Equals(ScreenInsight.Plain(name), plain, StringComparison.OrdinalIgnoreCase)
                || ScreenInsight.Fold(name) == folded)
            .Distinct()
            .ToList();

        if (exact.Count == 1) return (exact[0], []);
        if (exact.Count > 1) return (null, exact);

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
    /// set large, and the standing under it reads. The rank does not: it is
    /// eight identical cards with one highlighted, and the engine returns all
    /// eight labels and no highlight. So the reading carries the rank as null
    /// and the page says why.
    /// </remarks>
    private static ReputationReading? ReadReputation(IReadOnlyList<ScreenTextLine> lines)
    {
        var career = lines.FirstOrDefault(line => Is(line.Text, "CAREER"));
        var dossier = lines.FirstOrDefault(line => Is(line.Text, "DOSSIER"));
        var search = lines.FirstOrDefault(line => Is(line.Text, "SEARCH"));

        if (career is null || dossier is null || Math.Abs(career.Top - dossier.Top) > career.Height) return null;

        var org = lines
            .Where(line => line.Top > career.Top + career.Height && line.Left >= career.Left - career.Height)
            .Where(line => line.Height >= career.Height * 1.6)
            .OrderBy(line => line.Top)
            .FirstOrDefault();

        var standing = org is null ? null : lines
            .Where(line => line != org && line.Top > org.Top && line.Top - org.Top <= org.Height * 2.5)
            .Where(line => Math.Abs(line.Left - org.Left) <= org.Height)
            .OrderBy(line => line.Top)
            .FirstOrDefault()?.Text;

        var organisations = search is null ? [] : lines
            .Where(line => line.Top > search.Top + search.Height)
            .Where(line => line.Left >= search.Left + search.Height * 2 && line.Left <= search.Left + search.Height * 8)
            .Where(line => line.Height >= search.Height * 0.8)
            .Where(line => line.Text.Length >= 4)
            .OrderBy(line => line.Top)
            .Select(line => line.Text)
            .ToList();

        return new ReputationReading(org?.Text, standing, null, organisations);
    }
}
