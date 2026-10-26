using System.Diagnostics;
using System.Text;
using Quantumwake.Core.State;

namespace Quantumwake.Data;

/// <summary>One accepted hauling contract with everything known about where it goes.</summary>
/// <param name="Source">
/// Where the legs came from: <c>screenshot</c> when a Contracts-app frame
/// of this contract was read, <c>title</c> when only the title named an
/// end, <c>none</c> when nothing did.
/// </param>
/// <param name="Shot">The frame the legs were read from, when they were.</param>
/// <param name="Note">What the plan could not see about this contract, in words.</param>
public sealed record PlannedContract(
    string Title,
    string? MissionId,
    string? Commodity,
    HaulShape Shape,
    IReadOnlyList<HaulLeg> Legs,
    string Source,
    string? Shot,
    DateTimeOffset? ShotAt,
    int Pickups,
    int PickupsDone,
    int Deliveries,
    int DeliveriesDone,
    int? Scu,
    string? Note,
    IReadOnlyList<HaulDelivery>? DeliveryDetails = null,
    int? ScuDone = null,
    int? RemainingScu = null);

/// <summary>One destination's cargo total, including the amount its card says has already arrived.</summary>
public sealed record HaulDelivery(
    string Place,
    string? Body,
    string? Commodity,
    int? Scu,
    int? ScuDone,
    IReadOnlyList<string> LegIds);

/// <summary>Something to do at a stop: load or unload some cargo for one contract.</summary>
/// <param name="Scu">The count when the screen printed one for this leg; null when only the contract total is known.</param>
/// <param name="Note">Set on an unload whose contract has no pickup on the plan: it is placed last, and says why.</param>
public sealed record HaulAction(
    string Kind,
    string? Commodity,
    int? Scu,
    int Contract,
    string ContractTitle,
    string? Note = null,
    int? ScuDone = null,
    string? MissionId = null,
    IReadOnlyList<string>? LegIds = null);

/// <summary>Known cargo aboard after a stop; the amount is a floor when <see cref="AmountUnknown"/> is true.</summary>
public sealed record HaulCargo(string Commodity, int KnownScu, bool AmountUnknown, string? Note = null);

/// <summary>One visit on the run.</summary>
/// <param name="PlaceId">The map's id when the atlas knows the place; empty when it does not.</param>
/// <param name="Body">The body the place is on or above, from the atlas or from the contract text.</param>
public sealed record HaulStop(
    string Place,
    string PlaceId,
    string? Body,
    string? System,
    IReadOnlyList<HaulAction> Actions,
    string? Note,
    IReadOnlyList<HaulCargo>? Aboard = null);

/// <summary>The run: every contract, the stops in an order, and what the plan could not see.</summary>
/// <param name="KnownScu">SCU summed over the legs that printed a count, and delivery totals where they did not - a floor.</param>
/// <param name="Notes">What is missing and what a screenshot would add, for the page to say.</param>
public sealed record HaulPlan(
    IReadOnlyList<PlannedContract> Contracts,
    IReadOnlyList<HaulStop> Stops,
    int KnownScu,
    IReadOnlyList<string> Notes,
    ResolvedPlace? Start = null);

/// <summary>What the atlas says about a place name, when it says anything.</summary>
public sealed record ResolvedPlace(string Id, string Name, string? Body, string? System);

/// <summary>
/// Puts every open hauling contract on one route.
/// </summary>
/// <remarks>
/// <para>
/// A hauler with five contracts open sees five cards, each naming its own
/// destination and nothing else; where the cargo is collected is in each
/// card's detail panel, one card at a time. So the plan is assembled from
/// two sources of unequal reach. The logs know every open contract and,
/// with the text mod, one end of most (54 of 139 named a delivery on this
/// install, 41 a pickup, 14 both). A Contracts-app screenshot of a card
/// gives that card's every leg. Each contract says which it got.
/// </para>
/// <para>
/// A frame is matched to a contract by title, which is not unique - three
/// of the five cards on the 8 Sep frame read "to Stanton Gateway" - so the
/// cargo and the number of sources break the tie. A card that disagrees with
/// either known signal is not used: a route that admits it does not belong to
/// this contract. A frame already given to one compatible contract is only
/// reused when there is no other compatible card.
/// </para>
/// <para>
/// The order is pickups before deliveries, which is the one rule that is
/// always true, with stops on the same body kept together because a
/// quantum hop is the expensive part. It is a starting order, not the
/// answer: the plan is written into a flight plan the pilot can reorder.
/// Nothing here knows a distance - the atlas has no coordinates for a
/// station - so it does not pretend to.
/// </para>
/// </remarks>
public static class HaulPlanner
{
    public static HaulPlan Plan(
        IReadOnlyList<ContractRecord> contracts,
        IReadOnlyList<ScreenSighting> frames,
        Func<string, ResolvedPlace?> resolve,
        ResolvedPlace? start = null)
    {
        var hauling = contracts
            .Where(c => HaulingContract.IsHauling(c.Raw))
            .Where(c => c.CompletedAt is null && c.Outcome is ContractOutcome.Unknown or ContractOutcome.InProgress)
            .OrderBy(c => c.FirstSeen)
            .ToList();

        var bound = BoundFrames(contracts, frames);

        var planned = new List<PlannedContract>();
        var taken = new HashSet<string>(StringComparer.Ordinal);

        // One reading per frame, shared by every contract that considers it.
        // Keyed with the title's route as well, which fills in an end the
        // card was photographed before it finished printing; only contracts
        // whose title the card carries ask about it, so in practice the key
        // is the shot.
        var legsByShot = new Dictionary<string, IReadOnlyList<HaulLeg>>(StringComparer.Ordinal);
        IReadOnlyList<HaulLeg> LegsOf(ScreenSighting frame, HaulRoute? route)
        {
            var key = $"{frame.Shot}|{route?.Pickup}|{route?.Delivery}";
            if (legsByShot.TryGetValue(key, out var known)) return known;

            // A frame with no reading has no legs. Candidates only asks about
            // frames it has already filtered for one, so this is the memo
            // being honest rather than a case that happens.
            var legs = frame.Contracts is { } reading ? HaulLegs.From(reading, route).Legs : [];
            return legsByShot[key] = legs;
        }

        // Every contract's compatible frames before any is handed out. On
        // 21 Sep a fifth same-title Carbon contract was accepted a minute
        // after the fourth, and the card photographed after that fitted
        // both; the card photographed before it could only be the older
        // contract's. Handing the newest compatible frame to the first
        // contract gave both contracts the one card and lost the other. A
        // frame only one contract can claim goes to that contract first.
        var wanted = hauling.Select(contract =>
        {
            var title = ContractTags.Clean(contract.Name);
            var archetype = HaulingContract.FromArchetype(contract.Raw);
            var titleIsAmbiguous = hauling.Count(other =>
                ScreenFrames.SameContract(ContractTags.Clean(other.Name), title)) > 1;
            var route = HaulingContract.RouteFromTitle(contract.Title);
            return (title, archetype, candidates: Candidates(contract, title, archetype, titleIsAmbiguous, frames,
                frame => LegsOf(frame, route), bound));
        }).ToList();

        var claims = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, _, candidates) in wanted)
            foreach (var (frame, _) in candidates)
                claims[frame.Shot] = claims.GetValueOrDefault(frame.Shot) + 1;

        for (var index = 0; index < hauling.Count; index++)
        {
            var contract = hauling[index];
            var (title, archetype, candidates) = wanted[index];
            var route = HaulingContract.RouteFromTitle(contract.Title);

            var frame = Pick(candidates.Where(c => claims[c.Frame.Shot] == 1))
                ?? Pick(candidates.Where(c => !taken.Contains(c.Frame.Shot)))
                ?? Pick(candidates);
            var reused = frame is not null && taken.Contains(frame.Shot);

            if (frame is not null)
            {
                taken.Add(frame.Shot);
                var (readLegs, readDeliveries) = HaulLegs.From(frame.Contracts!, route);

                // The archetype spells the cargo whole; a part-written card
                // may have got as far as "Car".
                var legs = WithIds([.. readLegs.Select(leg => archetype?.Commodity is { } named
                    && (leg.Commodity is null || SameCargo(leg.Commodity, named)) ? leg with { Commodity = named } : leg)]);
                readDeliveries = [.. readDeliveries.Select(d => archetype?.Commodity is { } named
                    && (d.Commodity is null || SameCargo(d.Commodity, named)) ? d with { Commodity = named } : d)];
                var unnamed = legs.Where(l => l.Unnamed is not null).Select(l => l.Unnamed!).Distinct().ToList();
                var deliveries = DeliveryDetails(legs, readDeliveries);
                var scu = deliveries.Any(d => d.Scu is not null)
                    ? deliveries.Sum(d => d.Scu ?? 0)
                    : (int?)null;
                var scuDone = scu is null ? (int?)null : deliveries.Sum(d => d.ScuDone ?? 0);
                var remaining = scu is null ? (int?)null : Math.Max(0, scu.Value - (scuDone ?? 0));

                planned.Add(new PlannedContract(
                    title, contract.MissionId, archetype?.Commodity ?? legs.FirstOrDefault(l => l.Commodity is not null)?.Commodity,
                    archetype?.Shape ?? HaulShape.Unknown, legs, "screenshot", frame.Shot, frame.ShotAt,
                    contract.Pickups, contract.PickupsDone, contract.Deliveries, contract.DeliveriesDone,
                    scu,
                    string.Join("; ", new[]
                    {
                        reused ? "read from a frame that also matched another card with this title; photograph this card to be sure" : null,
                        unnamed.Count > 0
                            ? $"the game's own text leaves {(unnamed.Count == 1 ? "a place" : $"{unnamed.Count} places")} unnamed (it printed {string.Join(", ", unnamed)}); look at the marker in game to find it"
                            : null,
                    }.Where(note => note is not null)) is { Length: > 0 } note ? note : null,
                    deliveries, scuDone, remaining));
                continue;
            }

            // Only the title to go on: one leg with whatever end it named, and
            // the archetype's count of the other end as the note.
            var fromTitle = route is null ? [] : new List<HaulLeg>
            {
                new(route.Pickup, null, route.Delivery, null, archetype?.Commodity, null, null, "title-route"),
            };

            planned.Add(new PlannedContract(
                title, contract.MissionId, archetype?.Commodity, archetype?.Shape ?? HaulShape.Unknown,
                fromTitle, route is null ? "none" : "title", null, null,
                contract.Pickups, contract.PickupsDone, contract.Deliveries, contract.DeliveriesDone,
                null, MissingNote(route, archetype)));
        }

        var stops = Route(planned, resolve, start);

        var known = planned.Sum(c => c.RemainingScu ?? c.Scu ?? 0);
        var notes = new List<string>();

        var unread = planned.Count(c => c.Source != "screenshot");
        if (unread > 0)
            notes.Add(unread == 1
                ? $"1 of {planned.Count} contract{(planned.Count == 1 ? "" : "s")} has no screenshot of its card: open it on the Contracts app's Accepted tab and take one, and its pickups and SCU will be read."
                : $"{unread} of {planned.Count} contracts have no screenshot of their card: open each on the Contracts app's Accepted tab and take one, and its pickups and SCU will be read.");

        if (planned.Any(c => (c.RemainingScu ?? c.Scu) is null))
            notes.Add("The SCU total is a floor: it counts only contracts whose card was read.");

        var unplaced = stops.Where(s => s.PlaceId.Length == 0).Select(s => s.Place).Distinct().ToList();
        if (unplaced.Count > 0)
            notes.Add($"Not on the map yet: {string.Join(", ", unplaced)}. A place is on the map once the logs have seen you there; the stop keeps its name either way.");

        return new HaulPlan(planned, stops, known, notes, start);
    }

    private static string? MissingNote(HaulRoute? route, HaulArchetype? archetype)
    {
        if (route is null)
            return "the title names no place; only a screenshot of this card can say where it goes";

        if (route.Pickup is null)
            return archetype?.Pickups is { } n and > 1
                ? $"{n} pickups, places unknown until this card is photographed"
                : "pickup unknown until this card is photographed";

        if (route.Delivery is null)
            return archetype?.Deliveries is { } n and > 1
                ? $"{n} drop-offs, places unknown until this card is photographed"
                : "drop-off unknown until this card is photographed";

        return null;
    }

    /// <summary>
    /// A short stable name for the run a page was shown, so a plan can be
    /// committed as the one the pilot read rather than as whatever the next
    /// call computes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Over the stops and their actions, because those are what becomes the
    /// flight plan, and over which contracts are on the run, because the trip
    /// is named for how many there are - a contract that joins without moving
    /// a stop still changes what gets written.
    /// </para>
    /// <para>
    /// Not over the notes or the SCU floor: those move as cards are read
    /// without the route moving, and re-reading a card the pilot already has
    /// is no reason to refuse the run they chose.
    /// </para>
    /// </remarks>
    public static string Fingerprint(HaulPlan plan)
    {
        var text = string.Join('\n',
        [
            .. plan.Contracts.Select(c => $"c|{c.MissionId}|{c.Title}"),
            .. plan.Stops.Select(s =>
                $"s|{s.PlaceId}|{s.Place}|{string.Join(',', s.Actions.Select(a => $"{a.Kind}:{a.Commodity}:{a.Scu}:{a.MissionId}"))}"),
        ]);

        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>The best-scored frame, newest among equals; null when there is none.</summary>
    private static ScreenSighting? Pick(IEnumerable<(ScreenSighting Frame, int Score)> candidates) =>
        candidates.OrderByDescending(c => c.Score).ThenByDescending(c => c.Frame.ShotAt).Select(c => c.Frame).FirstOrDefault();

    /// <summary>How soon after an acceptance a card is taken to be the one just accepted.</summary>
    private static readonly TimeSpan JustAccepted = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Frames that can only be one contract's: the first card photographed
    /// within seconds of an acceptance, showing that contract's title, is the
    /// card the game opened on accepting it. Shot → mission id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every one of the nine 25 Oct frames came 1 to 10 seconds after an
    /// acceptance, and each showed the contract just taken: the right cargo,
    /// and as many pickups or drops as its archetype. Matching on those alone
    /// was not enough. Two Carbon hauls with two pickups each fit each other's
    /// cards, and once one was handed in the other took its card, because the
    /// newest compatible frame wins a tie - Gaslight and Checkmate became
    /// Starlight and Dudley &amp; Daughters.
    /// </para>
    /// <para>
    /// Asked of every contract the session took, handed in or not, so a card
    /// stays with its contract after that contract is gone rather than passing
    /// to a look-alike. Only the first frame after an acceptance: a batch of
    /// cards photographed after a batch of acceptances, as on 24 Oct, has the
    /// pilot clicking through the list, and only the first is the new one.
    /// </para>
    /// </remarks>
    private static Dictionary<string, string> BoundFrames(IReadOnlyList<ContractRecord> contracts, IReadOnlyList<ScreenSighting> frames)
    {
        var accepted = contracts
            .Where(c => c.MissionId is { Length: > 0 } && HaulingContract.IsHauling(c.Raw))
            .OrderBy(c => c.FirstSeen)
            .ToList();
        var shots = frames.Where(f => !f.Dismissed).OrderBy(f => f.ShotAt).ToList();
        var bound = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var contract in accepted)
        {
            var first = shots.FirstOrDefault(f => f.ShotAt >= contract.FirstSeen);
            if (first is null || first.ShotAt - contract.FirstSeen > JustAccepted) continue;

            // Another acceptance in between: the card is that one's, if anyone's.
            if (accepted.Any(other => other.FirstSeen > contract.FirstSeen && other.FirstSeen <= first.ShotAt)) continue;

            if (first.Contracts?.SelectedTitle is { } shown
                && ScreenFrames.SameContract(shown, ContractTags.Clean(contract.Name)))
                bound[first.Shot] = contract.MissionId!;
        }

        return bound;
    }

    /// <summary>
    /// Every frame showing this contract selected, taken after it was
    /// accepted, that the cargo and the source count do not rule out - scored
    /// by how many of those signals it matched. A card with no readable legs
    /// belongs to no plan: the title can still give a useful partial route.
    /// </summary>
    /// <param name="legsOf">
    /// Reads a frame's legs once and remembers them. Every open contract walks
    /// the same frame list, so without this the same card is parsed once per
    /// contract on every request the page makes.
    /// </param>
    private static List<(ScreenSighting Frame, int Score)> Candidates(
        ContractRecord contract, string title, HaulArchetype? archetype, bool titleIsAmbiguous,
        IReadOnlyList<ScreenSighting> frames, Func<ScreenSighting, IReadOnlyList<HaulLeg>> legsOf,
        IReadOnlyDictionary<string, string> bound)
    {
        var candidates = new List<(ScreenSighting, int)>();

        foreach (var frame in frames
            .Where(f => !f.Dismissed && f.Contracts?.SelectedTitle is { Length: > 0 })
            .Where(f => f.ShotAt >= contract.FirstSeen)
            .OrderByDescending(f => f.ShotAt))
        {
            if (!ScreenFrames.SameContract(frame.Contracts!.SelectedTitle!, title))
                continue;

            // The card the game opened on accepting another contract is that
            // contract's, whether or not it is still open.
            var mine = bound.TryGetValue(frame.Shot, out var owner);
            if (mine && owner != contract.MissionId)
                continue;

            var legs = legsOf(frame);
            if (legs.Count == 0)
                continue;

            // Not scored above the rest: a card photographed again later, with
            // some of its cargo delivered, is the newer word on the same
            // contract and should still win.
            var signals = 0;
            if (archetype?.Commodity is { } cargo && legs.Any(l => l.Commodity is not null))
            {
                // A same-title card for another cargo is not a weak match. It
                // is evidence this card belongs to a different contract.
                if (!legs.Any(l => SameCargo(l.Commodity, cargo)))
                    continue;

                signals += 2;
            }

            // A pickup the game left unnamed is still one of the contract's
            // sources: the 21:26:44 card listed two stations and a
            // "Location2Address" for a three-pickup Carbon haul.
            var pickupCount = legs
                .Where(l => l.Pickup is not null)
                .Select(l => ScreenInsight.Fold(l.Pickup!))
                .Distinct()
                .Count()
                + legs.Count(l => l.Pickup is null && l.Unnamed is not null);

            if (archetype?.Pickups is { } sources && pickupCount > 0)
            {
                if (pickupCount != sources)
                    continue;

                signals += 1;
            }

            // A shared title is not enough to give a card's route to one of
            // several contracts. Keep the safer title-only route until the
            // screenshot reads a signal that distinguishes its card.
            if (titleIsAmbiguous && signals == 0 && !mine)
                continue;

            candidates.Add((frame, signals));
        }

        return candidates;
    }

    /// <summary>"Aluminum" and "Aluminium" are one cargo; the engine's reading of either is close enough on six letters.</summary>
    internal static bool SameCargo(string? a, string? b)
    {
        if (a is null || b is null) return false;

        var x = ScreenInsight.Fold(a);
        var y = ScreenInsight.Fold(b);
        var n = Math.Min(6, Math.Min(x.Length, y.Length));

        return n > 0 && string.CompareOrdinal(x, 0, y, 0, n) == 0;
    }

    /// <summary>Number the card's legs within its mission, so a saved action can still say what it came from.</summary>
    private static IReadOnlyList<HaulLeg> WithIds(IReadOnlyList<HaulLeg> legs) =>
        [.. legs.Select((leg, index) => leg with { Id = $"leg-{index + 1}" })];

    /// <summary>
    /// The card lays a multi-pickup's total on its delivery parent, while a
    /// direct or multi-drop card lays one total on each leg. Keep both shapes
    /// as destination-and-cargo totals without inventing a split per pickup.
    /// </summary>
    private static IReadOnlyList<HaulDelivery> DeliveryDetails(
        IReadOnlyList<HaulLeg> legs, IReadOnlyList<HaulLegs.Delivery> parents)
    {
        var details = new List<HaulDelivery>();

        foreach (var parent in parents)
        {
            var linked = legs.Where(leg => SamePlace(leg.Delivery, parent.Place)
                    && (parent.Commodity is null || leg.Commodity is null || SameCargo(leg.Commodity, parent.Commodity)))
                .Select(leg => leg.Id)
                .ToList();
            details.Add(new HaulDelivery(parent.Place, parent.Body, parent.Commodity,
                parent.Scu, parent.ScuDone, linked));
        }

        // A collect parent has no Delivery record. Its delivery children each
        // carry their own count, so group only those counted legs.
        foreach (var group in legs
            .Where(leg => leg.Delivery is not null && leg.Scu is not null)
            .GroupBy(leg => (Place: ScreenInsight.Fold(leg.Delivery!), Cargo: ScreenInsight.Fold(leg.Commodity ?? string.Empty))))
        {
            var first = group.First();
            details.Add(new HaulDelivery(first.Delivery!, first.DeliveryBody, first.Commodity,
                group.Sum(leg => leg.Scu ?? 0),
                group.Any(leg => leg.ScuDone is not null) ? group.Sum(leg => leg.ScuDone ?? 0) : null,
                [.. group.Select(leg => leg.Id)]));
        }

        return details;
    }

    private static bool SamePlace(string? left, string? right) =>
        left is not null && right is not null && ScreenInsight.Fold(left) == ScreenInsight.Fold(right);

    private static int? Remaining(int? total, int? done) =>
        total is null ? null : Math.Max(0, total.Value - (done ?? 0));

    /// <summary>
    /// Stops in an order: every pickup before any delivery, same-body stops
    /// together, and a place that is both a source and a destination visited
    /// twice when its delivery depends on a pickup not yet made.
    /// </summary>
    private static List<HaulStop> Route(IReadOnlyList<PlannedContract> contracts, Func<string, ResolvedPlace?> resolve,
        ResolvedPlace? start)
    {
        var places = new Dictionary<string, Place>(StringComparer.Ordinal);

        Place At(string name, string? bodyHint)
        {
            var key = ScreenInsight.Fold(name);

            if (!places.TryGetValue(key, out var place))
            {
                var known = resolve(name);
                places[key] = place = new Place(
                    known?.Name ?? name,
                    known?.Id ?? string.Empty,
                    known?.Body ?? bodyHint,
                    known?.System);
            }

            place.Body ??= bodyHint;
            return place;
        }

        for (var i = 0; i < contracts.Count; i++)
        {
            var contract = contracts[i];
            var blind = !contract.Legs.Any(l => l.Pickup is not null);
            var completedLegs = (contract.DeliveryDetails ?? [])
                .Where(delivery => Remaining(delivery.Scu, delivery.ScuDone) == 0)
                .SelectMany(delivery => delivery.LegIds)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var leg in contract.Legs.Where(l => l.Pickup is not null && !completedLegs.Contains(l.Id)))
            {
                var remaining = Remaining(leg.Scu, leg.ScuDone);
                if (remaining == 0) continue;

                At(leg.Pickup!, leg.PickupBody).Loads.Add(new HaulAction(
                    "load", leg.Commodity, remaining, i, contract.Title,
                    ScuDone: leg.ScuDone, MissionId: contract.MissionId, LegIds: [leg.Id]));
            }

            var deliveries = contract.DeliveryDetails ?? [];
            if (deliveries.Count == 0)
            {
                // Title-only routes and old records do not have card details.
                // Keep those actions, but never collapse different cargo types
                // into one instruction at a shared destination.
                deliveries = [.. contract.Legs.Where(leg => leg.Delivery is not null)
                    .GroupBy(leg => (Place: ScreenInsight.Fold(leg.Delivery!), Cargo: ScreenInsight.Fold(leg.Commodity ?? string.Empty)))
                    .Select(group =>
                    {
                        var legs = group.ToList();
                        var first = legs[0];
                        var scu = legs.All(leg => leg.Scu is not null) ? legs.Sum(leg => leg.Scu ?? 0)
                            : legs.Count == contract.Legs.Count ? contract.Scu
                            : null;
                        var done = legs.Any(leg => leg.ScuDone is not null) ? legs.Sum(leg => leg.ScuDone ?? 0) : (int?)null;
                        return new HaulDelivery(first.Delivery!, first.DeliveryBody, first.Commodity, scu, done,
                            [.. legs.Select(leg => leg.Id)]);
                    })];
            }

            foreach (var delivery in deliveries)
            {
                var remaining = Remaining(delivery.Scu, delivery.ScuDone);
                if (remaining == 0) continue;

                At(delivery.Place, delivery.Body).Unloads.Add(new HaulAction(
                    "unload", delivery.Commodity, remaining, i, contract.Title,
                    blind ? "after its pickups, which are not on this plan" : null,
                    delivery.ScuDone, contract.MissionId, delivery.LegIds));
            }
        }

        // A delivery for contract i is ready once every pickup of i that the
        // plan knows about has been visited. A contract whose pickups it
        // cannot see at all waits for every pickup it can: the cargo has to
        // come from somewhere, and last is the only honest place for it.
        var pickupsLeft = contracts.Select((c, i) => (i, n: places.Values.Sum(p => p.Loads.Count(a => a.Contract == i)))).ToDictionary(x => x.i, x => x.n);
        var stops = new List<HaulStop>();
        var pending = places.Values.ToList();
        string? lastBody = start?.Body;
        string? lastSystem = start?.System;

        bool Ready(HaulAction unload) =>
            pickupsLeft[unload.Contract] == 0
            && (unload.Note is null || pending.All(p => p.Loads.Count == 0));

        while (pending.Count > 0)
        {
            var ready = pending.Where(p => p.Unloads.All(Ready)).ToList();

            // Sources first, and among them the ones whose deliveries are also
            // ready so the stop is done in one visit; same body as the last
            // stop before anything else.
            // The last fallback cannot come back empty - the loop runs only
            // while `pending` has something in it, and Prefer returns null
            // only for an empty list - but the compiler cannot see that, and
            // silencing it with `!` would hide a real null here later.
            // Being at a destination does not make its cargo collected. Only
            // work that can happen here may outrank the pickup dependencies.
            var here = stops.Count == 0 && start is not null
                ? pending.FirstOrDefault(p => (p.Loads.Count > 0 || p.Unloads.Any(Ready))
                    && ((start.Id.Length > 0 && string.Equals(p.Id, start.Id, StringComparison.OrdinalIgnoreCase))
                        || SamePlace(p.Name, start.Name)))
                : null;
            var next = here
                ?? Prefer(ready.Where(p => p.Loads.Count > 0), lastBody, lastSystem)
                ?? Prefer(pending.Where(p => p.Loads.Count > 0), lastBody, lastSystem)
                ?? Prefer(ready, lastBody, lastSystem)
                ?? Prefer(pending, lastBody, lastSystem)
                ?? throw new UnreachableException("pending is not empty, so a place is always preferred");

            pending.Remove(next);
            foreach (var load in next.Loads) pickupsLeft[load.Contract]--;

            var unloadsNow = next.Unloads.Where(Ready).ToList();
            var later = next.Unloads.Except(unloadsNow).ToList();
            var actions = next.Loads.Concat(unloadsNow).ToList();

            // The same place twice running is one visit: the second only
            // existed because its delivery was waiting on the first's pickup.
            if (stops.Count > 0 && stops[^1].Place == next.Name && stops[^1].PlaceId == next.Id)
            {
                var last = stops[^1];
                stops[^1] = last with { Actions = [.. last.Actions, .. actions], Note = later.Count > 0 ? last.Note : null };
            }
            else
            {
                stops.Add(new HaulStop(next.Name, next.Id, next.Body, next.System, actions,
                    later.Count > 0 ? "back here later to deliver, once its cargo is aboard" : null));
            }

            lastBody = next.Body;
            lastSystem = next.System;

            // The deliveries that could not happen yet get a second visit.
            if (later.Count > 0)
                pending.Add(new Place(next.Name, next.Id, next.Body, next.System) { Unloads = later });
        }

        return WithManifest(stops, contracts);
    }

    /// <summary>
    /// The game has no cargo manifest, so what is aboard is worked out per
    /// contract from the stops behind. A multi-pickup card names a total but
    /// not each source's share, so its commodity is unknown while a pickup is
    /// still ahead - but once the last one is behind the whole total is
    /// aboard, and once the last delivery is behind none of it is. Saying
    /// "unknown" past either point would be the plan forgetting what it read.
    /// </summary>
    private static List<HaulStop> WithManifest(List<HaulStop> stops, IReadOnlyList<PlannedContract> contracts)
    {
        // What each contract still has to move of each commodity, and how
        // many of its stops the route holds - so a contract's share can be
        // settled the moment its last pickup or last delivery is behind.
        var lots = new Dictionary<(int, string), Lot>();
        for (var i = 0; i < stops.Count; i++)
        {
            foreach (var action in stops[i].Actions)
            {
                var key = (action.Contract, ScreenInsight.Fold(action.Commodity ?? "Cargo"));
                if (!lots.TryGetValue(key, out var lot))
                    lots[key] = lot = new Lot(action.Commodity ?? "Cargo", Remaining(contracts[action.Contract], action.Commodity));
                if (action.Kind == "load") lot.Loads++; else lot.Unloads++;
            }
        }

        for (var i = 0; i < stops.Count; i++)
        {
            foreach (var action in stops[i].Actions)
            {
                var lot = lots[(action.Contract, ScreenInsight.Fold(action.Commodity ?? "Cargo"))];
                if (action.Kind == "load")
                {
                    lot.LoadsSeen++;
                    if (action.Scu is { } scu) lot.KnownLoaded += scu;
                    else lot.EveryLoadPrinted = false;
                }
                else
                {
                    lot.UnloadsSeen++;
                    lot.KnownUnloaded += action.Scu ?? 0;
                }
            }

            var aboard = new Dictionary<string, (string Name, int Known, bool Unknown, List<string> Notes)>(StringComparer.Ordinal);
            foreach (var ((contract, commodity), lot) in lots)
            {
                if (lot.Aboard() is not var (known, unknown)) continue;
                var cargo = aboard.TryGetValue(commodity, out var current) ? current
                    : (Name: lot.Name, Known: 0, Unknown: false, Notes: new List<string>());
                cargo.Known += known;
                cargo.Unknown |= unknown;
                if (unknown)
                {
                    var reason = lot.Remaining is { } total
                        ? $"{total} SCU remaining for this contract; the amount at each pickup was not read"
                        : "no cargo quantity was read; photograph this contract's cargo details";
                    if (lot.Unloads == 0) reason += "; drop-off unknown, so this plan cannot finish the delivery";
                    cargo.Notes.Add($"{contracts[contract].Title}: {reason}");
                }
                aboard[commodity] = cargo;
            }

            var manifest = aboard.Values
                .Where(cargo => cargo.Known > 0 || cargo.Unknown)
                .OrderBy(cargo => cargo.Name, StringComparer.OrdinalIgnoreCase)
                .Select(cargo => new HaulCargo(cargo.Name, cargo.Known, cargo.Unknown,
                    cargo.Notes.Count > 0 ? string.Join(" · ", cargo.Notes) : null))
                .ToList();
            stops[i] = stops[i] with { Aboard = manifest };
        }

        return stops;
    }

    /// <summary>One contract's share of one commodity, as the route passes its stops.</summary>
    private sealed class Lot(string name, int? remaining)
    {
        public string Name { get; } = name;
        public int? Remaining { get; } = remaining;
        public int Loads, LoadsSeen, Unloads, UnloadsSeen, KnownLoaded, KnownUnloaded;
        public bool EveryLoadPrinted = true;

        /// <summary>
        /// What this lot has aboard after the stops seen so far: null before
        /// any of its stops, otherwise the amount and whether it is a floor.
        /// </summary>
        public (int Known, bool Unknown)? Aboard()
        {
            if (LoadsSeen == 0 && UnloadsSeen == 0) return null;
            // Its last delivery is behind: nothing of it is left, whatever
            // the pickups printed.
            if (Unloads > 0 && UnloadsSeen == Unloads) return (0, false);
            if (LoadsSeen > 0 && EveryLoadPrinted) return (Math.Max(0, KnownLoaded - KnownUnloaded), false);
            // Every pickup is behind, so the card's total is aboard even
            // though no source printed its share.
            if (Loads > 0 && LoadsSeen == Loads && Remaining is { } total) return (Math.Max(0, total - KnownUnloaded), false);
            return (Math.Max(0, KnownLoaded - KnownUnloaded), true);
        }
    }

    /// <summary>What a contract still has to move of a commodity: its delivery line's total less what that line says has arrived.</summary>
    private static int? Remaining(PlannedContract contract, string? commodity)
    {
        var key = ScreenInsight.Fold(commodity ?? "Cargo");
        var lines = (contract.DeliveryDetails ?? [])
            .Where(d => d.Scu is not null && ScreenInsight.Fold(d.Commodity ?? contract.Commodity ?? "Cargo") == key)
            .ToList();
        if (lines.Count > 0) return lines.Sum(d => d.Scu!.Value - (d.ScuDone ?? 0));
        if (ScreenInsight.Fold(contract.Commodity ?? "Cargo") == key) return contract.RemainingScu ?? contract.Scu;
        return null;
    }

    private static Place? Prefer(IEnumerable<Place> candidates, string? body, string? system)
    {
        var list = candidates.ToList();
        if (list.Count == 0) return null;

        return list.OrderByDescending(p => body is not null && p.Body is not null
                && string.Equals(p.Body, body, StringComparison.OrdinalIgnoreCase)
                && (system is null || p.System is null || string.Equals(p.System, system, StringComparison.OrdinalIgnoreCase)))
            .ThenByDescending(p => system is not null && string.Equals(p.System, system, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p.Loads.Count + p.Unloads.Count).First();
    }

    private sealed class Place(string name, string id, string? body, string? system)
    {
        public string Name => name;
        public string Id => id;
        public string? Body { get; set; } = body;
        public string? System => system;
        public List<HaulAction> Loads { get; init; } = [];
        public List<HaulAction> Unloads { get; init; } = [];
    }
}
