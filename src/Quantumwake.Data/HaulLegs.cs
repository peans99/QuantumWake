using Quantumwake.Core.State;

namespace Quantumwake.Data;

/// <summary>One movement of cargo a contract asks for: from a place, to a place, how much.</summary>
/// <param name="Scu">The leg's own count when the screen printed one; null on a multi-pickup, where only the delivery total is printed.</param>
/// <param name="ScuDone">The first figure of the count - what the screen said was already delivered.</param>
/// <param name="Unnamed">
/// What the game printed where the missing end's name belongs -
/// "Location2Address" - when the letter lists a place it has no name for.
/// </param>
public sealed record HaulLeg(
    string? Pickup,
    string? PickupBody,
    string? Delivery,
    string? DeliveryBody,
    string? Commodity,
    int? Scu,
    int? ScuDone,
    string Id = "",
    string? Unnamed = null);

/// <summary>
/// Turns the objectives the Contracts app printed into legs.
/// </summary>
/// <remarks>
/// <para>
/// The indent is the structure. A step at depth 0 opens a group and every
/// depth-1 step under it belongs to it, until the next depth-0 step. Which
/// end is the parent depends on the contract's shape, and both occur:
/// </para>
/// <list type="bullet">
/// <item>Collect parent, Deliver children - a direct haul, or a multi-drop
/// where each source has its own destination. One leg per child, the cargo
/// from whichever line named it.</item>
/// <item>Deliver parent, Collect children - a multi-pickup. One leg per
/// child, all to the parent's place; the parent's count is the delivery
/// total and no child carries its own share, so the leg's count is null and
/// the total rides on <see cref="Delivery"/>.</item>
/// </list>
/// <para>
/// A step with no children is a leg on its own with one end missing, which is
/// what the app shows rather than pairing it with a neighbour by guesswork.
/// </para>
/// <para>
/// The objectives panel is the shorter list and the one that gets cut off:
/// the 21 Sep Potassium card read "Deliver 0/213 SCU" and one Collect line
/// where the letter above it listed two pickups. On a multi-pickup the two
/// lists name the same places, so a site the objectives missed is added as
/// a leg of its own - a card read as one pickup short is rejected as
/// another contract's, which is worse than a leg without a count.
/// </para>
/// <para>
/// The objectives can also be part-written. The game types them in when a
/// card opens, and six of the nine 25 Oct frames caught at least one line
/// before its full stop - "Collect Carbon from Dudley e D", "Deliver 0/189
/// SCU of Car", "Collect Potassium from". The letter beside them is printed
/// whole, so when it lists the pickups a part-written Collect is left to it,
/// and a part-written destination is taken from the contract's own title
/// when the title names it.
/// </para>
/// </remarks>
public static partial class HaulLegs
{
    /// <summary>A delivery the screen printed a total for, before it is split into legs.</summary>
    public sealed record Delivery(string Place, string? Body, string? Commodity, int? Scu, int? ScuDone);

    /// <summary>The legs, and the delivery totals the multi-pickup shape prints on the parent.</summary>
    /// <param name="route">
    /// What the contract's title says of its ends - "to Ruin Station" - for
    /// the end a part-written objective did not finish naming.
    /// </param>
    public static (IReadOnlyList<HaulLeg> Legs, IReadOnlyList<Delivery> Deliveries) From(ContractsReading reading, HaulRoute? route = null)
    {
        var (sites, unnamedSites) = Split(reading.PickupSites);
        var (drops, unnamedDrops) = Split(reading.DropSites);
        var steps = Settled(reading.Steps ?? [], sites, drops, route);
        var legs = new List<HaulLeg>();
        var deliveries = new List<Delivery>();
        var cargo = (reading.Steps ?? []).Select(s => s.Commodity).FirstOrDefault(c => c is not null);

        for (var i = 0; i < steps.Count; i++)
        {
            var parent = steps[i];
            if (parent.Depth != 0 || parent.Kind == "other") continue;

            var children = new List<ContractStep>();
            for (var j = i + 1; j < steps.Count && steps[j].Depth > 0; j++)
                if (steps[j].Kind != "other") children.Add(steps[j]);

            if (parent.Kind == "collect")
            {
                if (children.Count == 0)
                {
                    legs.Add(new HaulLeg(parent.Place, BodyOf(parent, sites), null, null, parent.Commodity, null, null));
                    continue;
                }

                foreach (var child in children.Where(c => c.Kind == "deliver"))
                    legs.Add(new HaulLeg(
                        parent.Place, BodyOf(parent, sites),
                        child.Place, child.Body,
                        child.Commodity ?? parent.Commodity,
                        child.Total, child.Done));
            }
            else
            {
                deliveries.Add(new Delivery(parent.Place!, parent.Body, parent.Commodity, parent.Total, parent.Done));

                // No Collect under it - unless the letter lists the pickups
                // the panel had not printed yet, the delivery stands alone.
                if (children.Count == 0 && sites.Count + unnamedSites.Count == 0)
                {
                    legs.Add(new HaulLeg(null, null, parent.Place, parent.Body, parent.Commodity, parent.Total, parent.Done));
                    continue;
                }

                foreach (var child in children.Where(c => c.Kind == "collect"))
                    legs.Add(new HaulLeg(
                        child.Place, BodyOf(child, sites),
                        parent.Place, parent.Body,
                        child.Commodity ?? parent.Commodity,
                        null, null));

                // Only when this is the card's one delivery: with several,
                // which one a missed site belongs under is a guess.
                if (steps.Count(s => s.Depth == 0 && s.Kind == "deliver") == 1)
                {
                    foreach (var site in sites.Where(site => !children.Any(c => c.Place is not null && SamePlace(c.Place, site.Place))))
                        legs.Add(new HaulLeg(site.Place, site.Body, parent.Place, parent.Body, parent.Commodity ?? cargo, null, null));

                    foreach (var unnamed in unnamedSites)
                        legs.Add(new HaulLeg(null, null, parent.Place, parent.Body, parent.Commodity ?? cargo, null, null, Unnamed: unnamed));
                }
            }
        }

        // Nothing on the panel finished naming the destination, and the title
        // does: every pickup the letter lists goes there. The 21:26:22 frame
        // read three "Collect Potassium from" and nothing else.
        if (!steps.Any(s => s.Depth == 0 && s.Kind == "deliver") && sites.Count > 0
            && route?.Delivery is { } titled && !legs.Any(l => l.Delivery is not null))
        {
            legs.RemoveAll(l => l.Delivery is null);

            // A count read off a part-written Deliver is still the total.
            if (steps.FirstOrDefault(s => s.Kind == "deliver" && s.Total is not null) is { } counted)
                deliveries.Add(new Delivery(titled, null, counted.Commodity ?? cargo, counted.Total, counted.Done));

            foreach (var site in sites)
                legs.Add(new HaulLeg(site.Place, site.Body, titled, null, cargo, null, null));
            foreach (var unnamed in unnamedSites)
                legs.Add(new HaulLeg(null, null, titled, null, cargo, null, null, Unnamed: unnamed));
        }

        // One source, several destinations, and the objectives panel showing
        // the first of them: the 21 Sep Waste card read "Deliver 0/61 SCU to
        // Starlight Service Station" over "Collect Waste from Ruin Station"
        // where the letter listed three drop-offs. A destination the panel
        // did not reach is a leg from that one source, with no count. With no
        // source read at all, the title's "from Ruin Station" is the source.
        var sources = legs.Where(l => l.Pickup is not null).Select(l => l.Pickup!).Distinct().ToList();
        if (drops.Count + unnamedDrops.Count > 0 && (sources.Count == 1 || (sources.Count == 0 && route?.Pickup is not null)))
        {
            var source = sources.Count == 1
                ? legs.First(l => l.Pickup == sources[0])
                : new HaulLeg(route!.Pickup, null, null, null, cargo, null, null);
            if (sources.Count == 0) legs.RemoveAll(l => l.Pickup is null);

            foreach (var drop in drops.Where(drop => !legs.Any(l => l.Delivery is not null && SamePlace(l.Delivery, drop.Place))))
                legs.Add(new HaulLeg(source.Pickup, source.PickupBody, drop.Place, drop.Body, source.Commodity ?? cargo, null, null));
            foreach (var unnamed in unnamedDrops)
                legs.Add(new HaulLeg(source.Pickup, source.PickupBody, null, null, source.Commodity ?? cargo, null, null, Unnamed: unnamed));
        }

        return (legs, deliveries);
    }

    /// <summary>
    /// The steps with what a part-written line cannot be trusted for taken
    /// out: a Collect the letter's list will supply, and a destination the
    /// title names in full.
    /// </summary>
    private static List<ContractStep> Settled(
        IReadOnlyList<ContractStep> steps, IReadOnlyList<PickupSite> sites, IReadOnlyList<PickupSite> drops, HaulRoute? route)
    {
        var settled = new List<ContractStep>();

        foreach (var read in steps)
        {
            var step = read.Place is null ? read : read with { Place = Ampersand(read.Place) };

            if (ScreenFrames.Whole(step))
            {
                settled.Add(step);
                continue;
            }

            switch (step.Kind)
            {
                // A multi-pickup's source, cut off: the letter names them all.
                case "collect" when sites.Count > 0 && step.Depth > 0:
                case "collect" when sites.Count > 0 && drops.Count == 0:
                    break;

                // "Collect Waste from Ruin" heading a multi-drop: the title
                // says where from in full.
                case "collect" when route?.Pickup is { } from && (step.Place is null || SamePlace(step.Place, from)):
                    settled.Add(step with { Place = from, Body = null });
                    break;

                case "deliver" when route?.Delivery is { } to && (step.Place is null || SamePlace(step.Place, to)):
                    settled.Add(step with { Place = to, Body = step.Body });
                    break;

                // "Deliver 0/189 SCU of Car": the count is read, the place is
                // the title's.
                case "other" when step.Total is not null && route?.Delivery is { } to:
                    settled.Add(step with { Kind = "deliver", Place = to, Body = null });
                    break;

                default:
                    settled.Add(step);
                    break;
            }
        }

        return settled;
    }

    /// <summary>
    /// The list with each Lagrange station's tail taken off, and the places the
    /// game left unnamed set apart. A reading stored before the reader did
    /// that keeps "Rat's Nest at the L5 Lagrange of Pyro V" as the place;
    /// splitting again is idempotent and spares a re-read.
    /// </summary>
    private static (List<PickupSite> Named, List<string> Unnamed) Split(IReadOnlyList<PickupSite>? sites)
    {
        var named = new List<PickupSite>();
        var unnamed = new List<string>();

        foreach (var site in sites ?? [])
        {
            if (ScreenFrames.IsPlaceholder(site.Place))
            {
                unnamed.Add(site.Place.Trim());
                continue;
            }

            var (place, body) = ScreenFrames.SplitBody(site.Place);
            named.Add(new PickupSite(Ampersand(place), site.Body ?? body));
        }

        return (named, unnamed);
    }

    /// <summary>
    /// "CRU-L5 Beautiful Glen Station" and "Beautiful Glen Station" are one
    /// place; the engine's reading of either has the same letters inside.
    /// </summary>
    /// <remarks>
    /// Containment alone is not enough, because the two panels do not write a
    /// place the same way: the objectives line abbreviates - "Deliver 0/13 SCU
    /// to NB Int. Spaceport." - where the letter's DROP OFF list spells out
    /// "New Babbage International Spaceport". Folded, neither contains the
    /// other, so the drop read as one the plan had missed and a second leg was
    /// appended for the same place under its long name. That became an extra
    /// stop on the run and an extra stop in the flight plan, and nothing
    /// flagged it: a backfilled leg carries no SCU, so the totals still added
    /// up. Hence the abbreviation test as well.
    /// </remarks>
    private static bool SamePlace(string a, string b)
    {
        var x = ScreenInsight.Fold(a);
        var y = ScreenInsight.Fold(b);
        if (x.Length == 0 || y.Length == 0) return false;

        return x.Contains(y) || y.Contains(x) || Abbreviates(a, b) || Abbreviates(b, a)
            || (Long(a) is { Length: > 0 } p && p == Long(b));
    }

    /// <summary>
    /// The name without its one-letter words. The engine reads the "&amp;" of
    /// "Dudley &amp; Daughters" as 6, 8, S, e or nothing - all five on the
    /// 25 Oct frames - so the two panels only agree once it is gone.
    /// </summary>
    private static string Long(string name) => string.Concat(Words(name).Where(w => w.Length > 1));

    /// <summary>"Dudley 6 Daughters" as the map spells it, when a lone character between two words is what the engine made of an ampersand.</summary>
    private static string Ampersand(string place) => AmpersandRegex().Replace(place, " & ");

    [System.Text.RegularExpressions.GeneratedRegex(@"(?<=\b\p{Lu}\p{Ll}+)\s+[68Se&]\s+(?=\p{Lu}\p{Ll}+\b)")]
    private static partial System.Text.RegularExpressions.Regex AmpersandRegex();

    /// <summary>
    /// Whether <paramref name="shortForm"/> is the same place name as
    /// <paramref name="longForm"/> written shorter: each of its words either
    /// starts one of the long form's words - "Int." for "International" - or
    /// spells the initials of a run of them - "NB" for "New Babbage". The
    /// whole long form has to be accounted for, so "Port Olisar" cannot match
    /// "Port Tressler", and an initialism must be two letters or more, so a
    /// stray "A" cannot swallow a pair of words.
    /// </summary>
    private static bool Abbreviates(string shortForm, string longForm)
    {
        var shorts = Words(shortForm);
        var longs = Words(longForm);
        if (shorts.Count == 0 || longs.Count == 0 || shorts.Count > longs.Count) return false;

        // A name that is not actually shorter is the containment test's job;
        // matching it here would only add ways to be wrong.
        if (shorts.Count == longs.Count && shorts.Sum(w => w.Length) >= longs.Sum(w => w.Length)) return false;

        var at = 0;
        foreach (var word in shorts)
        {
            if (at < longs.Count && longs[at].StartsWith(word, StringComparison.Ordinal))
            {
                at++;
                continue;
            }

            // The initials of as many words as the abbreviation has letters.
            if (word.Length >= 2 && at + word.Length <= longs.Count
                && !word.Where((letter, i) => longs[at + i][0] != letter).Any())
            {
                at += word.Length;
                continue;
            }

            return false;
        }

        return at == longs.Count;
    }

    /// <summary>The name's words, folded one at a time so the gaps between them survive.</summary>
    private static List<string> Words(string text) =>
        [.. text.Split([' ', '\t', '-', '·', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Select(ScreenInsight.Fold)
            .Where(w => w.Length > 0)];

    /// <summary>The body a pickup is on: from the objective if it said, else from the contract text's own list.</summary>
    private static string? BodyOf(ContractStep step, IReadOnlyList<PickupSite> sites)
    {
        if (step.Body is not null) return step.Body;
        if (step.Place is null) return null;

        return sites.FirstOrDefault(s => ScreenInsight.Fold(s.Place) == ScreenInsight.Fold(step.Place))?.Body;
    }
}
