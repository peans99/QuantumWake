using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quantumwake.Server;

/// <summary>
/// The second set of tools (1.2.50): money, the Garage and gear, contracts and
/// reputation, what happened in play, and the small acts a pilot does on Now
/// and Shopping - pin, tick, add a stop, track.
/// </summary>
/// <remarks>
/// <para>
/// The crew list is deliberately not here. It is 131 other players' handles
/// on the install this was written against, and an assistant's answers leave
/// this PC; that is somebody else's name, not the pilot's data to hand over.
/// </para>
/// <para>
/// The acting tools set a state rather than flip one: the app's own routes
/// toggle, because a button is pressed once, but an assistant asked twice
/// would undo itself. Each reads the current state first and changes it only
/// when it differs, so "pin it" is the same answer however often it is said.
/// </para>
/// </remarks>
public static partial class McpTools
{
    private static readonly IReadOnlyList<McpTool> Pilot =
    [
        new("my_spending", "My spending",
            "What the pilot has bought at shops, from the purchases their logs record: the total, the shops used "
            + "most and the items spent most on. Fees, fines and anything bought outside a shop terminal are not in it.",
            Schema(("limit", Count("How many items, 1-25.", 15), false)), false, MySpending),

        new("my_ledger", "My ledger",
            "The pilot's money as the logs record it, newest first: each purchase, reward or charge with what, "
            + "where, the amount and a running total. Filter by kind or recent days.",
            Schema(("kind", Text("Only entries whose kind contains this, e.g. 'bought', 'reward'."), false),
                   ("days", Number("Only the last this many days."), false),
                   ("limit", Count("How many entries, 1-25.", 20), false)), false, MyLedger),

        new("my_earnings", "My earnings",
            "aUEC earned, time in game and the hourly rate over a window of days, and over the pilot's whole history.",
            Schema(("days", Number("The window, in days; 30 when left out."), false)), false,
            async (a, api, t) => await api.Get($"/api/earnings?days={(int)(Num(a, "days") ?? 30)}", t)),

        new("garage", "Garage",
            "The ships the pilot has flown, or one ship's fit: every slot with what is fitted, its size and grade, "
            + "whether it is stock, and the ship's speeds, fuel, cargo and power. Takes a name or class.",
            Schema(("ship", Text("A ship's name or class; leave out for the list of the pilot's ships."), false)), false,
            Garage),

        new("garage_options", "Parts for a slot",
            "What can be fitted in one slot of a ship, with where each part is sold. Use garage first for the slot's id.",
            Schema(("ship", Text("The ship's name or class."), true),
                   ("slot", Text("The slot's portId from garage, or its hardpoint name."), true),
                   ("limit", Count("How many parts, 1-25.", 15), false)), false, GarageOptions),

        new("my_loadout", "What I'm carrying",
            "The pilot's own loadout as the logs last saw it - armour, weapons, tools, consumables - slot by slot.",
            Schema(), false, MyLoadout),

        new("armoury", "Armoury",
            "FPS weapons, armour, attachments, knives and grenades from the game files: damage, magazine, "
            + "resistances, temperature range, mass, and where each is sold.",
            Schema(("query", Text("A name, family or maker, e.g. 'Arrowhead', 'Pembroke', 'Behring'."), true),
                   ("kind", Choice("Which part of the Armoury.", "weapons", "armour", "attachments", "melee", "throwables"), false),
                   ("limit", Count("How many, 1-25.", 10), false)), false, Armoury),

        new("my_contracts", "My contracts",
            "Contracts from the pilot's logs, newest first: name, contractor, type, system, outcome, steps done, "
            + "minutes and reputation, with totals by outcome. Filter by outcome, contractor, system or days.",
            Schema(("outcome", Choice("Only this outcome.", "Completed", "Failed", "Abandoned", "InProgress"), false),
                   ("contractor", Text("Only this contractor, e.g. 'Head Hunters'."), false),
                   ("system", Text("Only this system, e.g. 'Pyro'."), false),
                   ("days", Number("Only the last this many days."), false),
                   ("limit", Count("How many, 1-25.", 20), false)), false, MyContracts),

        new("my_reputation", "My reputation",
            "The pilot's standing with each organisation read off the game's reputation screen - rank, progress "
            + "and what the next rank needs - and each contractor's record from the logs.",
            Schema(), false, MyReputation),

        new("events", "Events",
            "The game's events with the pilot's points on each track, the next reward tier and how far it is, "
            + "and the contracts open now that pay into one.",
            Schema(), false, Events),

        new("wikelo", "Wikelo trades",
            "Wikelo's emporium trades: what each needs, what it gives, and which of the needed things the pilot "
            + "has been seen holding. 'have' is presence in a stash, never a count.",
            Schema(("query", Text("Words in the trade or a needed item."), false),
                   ("ready_only", new JsonObject { ["type"] = "boolean", ["description"] = "Only trades where every needed thing has been seen held." }, false),
                   ("limit", Count("How many, 1-25.", 10), false)), false, Wikelo),

        new("logbook", "Logbook",
            "What happened in the pilot's play, newest first: sessions, jumps, deaths, purchases, contracts, "
            + "each with when and where.",
            Schema(("kind", Text("Only entries of this kind, e.g. 'session', 'death', 'contract'."), false),
                   ("limit", Count("How many, 1-25.", 20), false)), false, Logbook),

        new("refinery", "Refinery orders",
            "Refinery orders read off the pilot's screenshots or live reading: station, method, cost, what goes "
            + "in and comes out, and when each is due or whether it is ready.",
            Schema(), false, Refinery),

        new("deaths_and_medical", "Deaths and med beds",
            "How often the pilot has died, where they woke, the med beds used, and an estimate of the fees.",
            Schema(), false, Casualties),

        // Behind its own switch, off until turned on (1.2.51): see McpArea.Crew.
        new("my_crew", "People I've played with",
            "Other players the party channel has named while grouped with the pilot: sessions together, first "
            + "and last seen, party lead, and the ships shared. A floor - someone who never joined, dropped or led "
            + "while the pilot watched is not in it.",
            Schema(("handle", Text("Only handles containing this."), false),
                   ("days", Number("Only the last this many days; all when left out."), false),
                   ("limit", Count("How many people, 1-25.", 20), false)), false, MyCrew),
    ];

    private static readonly IReadOnlyList<McpTool> Acting =
    [
        new("pin_to_now", "Pin to Now",
            "Puts a shopping list or a checklist on the Now page and the overlay, or takes it off. Several can be "
            + "pinned. Use my_plans for the id.",
            Schema(("what", Choice("What to pin.", "shopping_list", "checklist"), true),
                   ("id", Text("The list's id from my_plans."), true),
                   ("pinned", new JsonObject { ["type"] = "boolean", ["description"] = "true to pin, false to unpin; true when left out." }, false)), true,
            PinToNow),

        new("tick", "Tick a line",
            "Marks a shopping-list line in hand or a checklist task done - or not - by its words. Use my_plans "
            + "for the list's id and the line as written there.",
            Schema(("what", Choice("Which kind of line.", "shopping_line", "checklist_task"), true),
                   ("list_id", Text("The list's id from my_plans."), true),
                   ("line", Text("The line's name or the task's words, as my_plans shows them."), true),
                   ("done", new JsonObject { ["type"] = "boolean", ["description"] = "true for in hand or done, false to undo; true when left out." }, false)), true,
            Tick),

        new("add_stop", "Add a stop",
            "Adds one stop, with optional actions, to the end of an existing flight plan. Use my_plans for the "
            + "plan's id and find_places for the place.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["plan_id"] = Text("The flight plan's id from my_plans."),
                    ["place"] = Text("The place, as find_places names it."),
                    ["note"] = Text("A note for the stop."),
                    ["actions"] = ActionList(),
                },
                ["required"] = new JsonArray("plan_id", "place"),
            }, true, AddStop),

        new("track_flight_plan", "Track a flight plan",
            "Makes a flight plan the one the map, Now and the overlay follow, or stops following it.",
            Schema(("plan_id", Text("The flight plan's id from my_plans."), true),
                   ("tracked", new JsonObject { ["type"] = "boolean", ["description"] = "true to follow it, false to stop; true when left out." }, false)), true,
            TrackPlan),

        new("track_event", "Keep an event on Now",
            "Keeps one event on the Now page whatever is being played, or - with no id - lets Now follow play again.",
            Schema(("event_id", Text("The event's id from the events tool; leave out to follow play."), false)), true,
            TrackEvent),

        new("plan_haul_run", "Plan the hauling run",
            "Turns the hauling contracts open in the current session into a flight plan, the same as the button "
            + "on Contracts. Needs the game running with hauling contracts that name their places.",
            Schema(), true,
            async (a, api, t) =>
            {
                var trip = await api.Post("/api/haul/plan/trip", new { }, t);
                return new JsonObject
                {
                    ["id"] = trip["id"]?.DeepClone() ?? trip["trip"]?["id"]?.DeepClone(),
                    ["answer"] = trip.DeepClone(),
                };
            }),
    ];

    // ---- money ----

    private static async Task<JsonNode> MySpending(JsonElement a, McpApi api, CancellationToken t)
    {
        var spending = (JsonObject)await api.Get("/api/spending", t);
        if (spending["items"] is JsonArray items) spending["items"] = Take(items, Limit(a, 15));
        if (spending["shops"] is JsonArray shops) spending["shops"] = Take(shops, 15);
        spending["note"] = "From the shop purchases the logs record. Fees, fines and trades between players are not in it.";
        return spending;
    }

    private static async Task<JsonNode> MyLedger(JsonElement a, McpApi api, CancellationToken t)
    {
        var all = (await api.Get("/api/ledger", t) as JsonArray ?? []).OfType<JsonObject>().ToList();
        var kind = Str(a, "kind");
        var since = Num(a, "days") is { } days ? DateTimeOffset.UtcNow.AddDays(-days) : (DateTimeOffset?)null;
        var keep = new[] { "at", "kind", "what", "where", "shop", "amount", "quantity", "confirmed", "running" };

        var found = all
            .Where(e => kind is null || e["kind"]?.GetValue<string>() is { } k && k.Contains(kind, StringComparison.OrdinalIgnoreCase))
            .Where(e => since is null || When(e["at"]) is { } at && at >= since)
            .ToList();

        return new JsonObject
        {
            ["kinds"] = new JsonArray([.. all.Select(e => e["kind"]?.GetValue<string>()).Where(k => k is not null).Distinct()
                .Select(k => (JsonNode)JsonValue.Create(k)!)]),
            ["found"] = found.Count,
            ["entries"] = new JsonArray([.. found.Take(Limit(a, 20)).Select(e => (JsonNode)Pick(e, keep))]),
            ["note"] = "Amounts are aUEC, negative when spent. The running total starts at the first entry the logs hold, not at the wallet's balance.",
        };
    }

    // ---- the Garage and gear ----

    private static async Task<JsonNode> Garage(JsonElement a, McpApi api, CancellationToken t)
    {
        var garage = (JsonObject)await api.Get("/api/garage", t);
        var wanted = Str(a, "ship");
        if (wanted is null)
            return new JsonObject
            {
                ["flown"] = garage["mine"]?.DeepClone(),
                ["shipsInTheGame"] = (garage["all"] as JsonArray)?.Count,
                ["gameData"] = garage["dump"]?.DeepClone(),
            };

        var cls = await ShipClass(garage, wanted)
            ?? throw new McpToolException($"No ship called '{wanted}' in the Garage. Call garage with no ship for the list.");
        var ship = (JsonObject)await api.Get($"/api/garage/{Uri.EscapeDataString(cls)}", t);

        var ports = new JsonArray();
        foreach (var port in (ship["ports"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var fitted = port["fitted"] as JsonObject;
            ports.Add(new JsonObject
            {
                ["portId"] = port["portId"]?.DeepClone(),
                ["hardpoint"] = port["hardpoint"]?.DeepClone(),
                ["group"] = port["group"]?.DeepClone(),
                ["size"] = port["maxSize"]?.DeepClone(),
                ["fitted"] = port["name"]?.DeepClone(),
                ["grade"] = fitted?["grade"]?.DeepClone(),
                ["maker"] = fitted?["manufacturer"]?.DeepClone(),
                ["stock"] = port["stockName"]?.DeepClone(),
                ["changed"] = port["changed"]?.DeepClone(),
                ["dps"] = fitted?["weapon"]?["dps"]?.DeepClone(),
            });
        }

        var sheet = ship["sheet"] as JsonObject;
        return new JsonObject
        {
            ["ship"] = ship["ship"]?.DeepClone(),
            ["ports"] = ports,
            ["weapons"] = sheet?["weapons"]?.DeepClone(),
            ["shield"] = sheet?["shield"]?.DeepClone(),
            ["power"] = sheet?["power"]?.DeepClone(),
            ["cooling"] = sheet?["cooling"]?.DeepClone(),
            ["quantumDrive"] = sheet?["quantumDrive"]?.DeepClone(),
            ["gameData"] = ship["dump"]?.DeepClone(),
        };
    }

    private static async Task<JsonNode> GarageOptions(JsonElement a, McpApi api, CancellationToken t)
    {
        var garage = (JsonObject)await api.Get("/api/garage", t);
        var wanted = Need(a, "ship");
        var cls = await ShipClass(garage, wanted) ?? throw new McpToolException($"No ship called '{wanted}' in the Garage.");
        var slot = Need(a, "slot");

        // A portId is taken as given; a hardpoint name is looked up on the ship.
        var port = slot;
        if (!slot.Contains('.'))
        {
            var ship = (JsonObject)await api.Get($"/api/garage/{Uri.EscapeDataString(cls)}", t);
            port = (ship["ports"] as JsonArray ?? []).OfType<JsonObject>()
                .FirstOrDefault(p => string.Equals(p["hardpoint"]?.GetValue<string>(), slot, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p["portId"]?.GetValue<string>(), slot, StringComparison.OrdinalIgnoreCase))?["portId"]?.GetValue<string>()
                ?? throw new McpToolException($"The {cls} has no slot '{slot}'. Use garage for its portIds.");
        }

        var options = await api.Get($"/api/garage/{Uri.EscapeDataString(cls)}/options?port={Uri.EscapeDataString(port)}", t);
        return Trim(options, Limit(a, 15));
    }

    private static async Task<JsonNode> MyLoadout(JsonElement a, McpApi api, CancellationToken t)
    {
        var slots = new JsonArray();
        foreach (var slot in (await api.Get("/api/loadout", t) as JsonArray ?? []).OfType<JsonObject>())
            slots.Add(new JsonObject
            {
                ["category"] = slot["category"]?.DeepClone(),
                ["slot"] = slot["label"]?.DeepClone(),
                ["items"] = new JsonArray([.. (slot["items"] as JsonArray ?? []).OfType<JsonObject>().Select(i => (JsonNode)new JsonObject
                {
                    ["name"] = i["name"]?.DeepClone(),
                    ["count"] = i["count"]?.DeepClone(),
                    ["type"] = i["reference"]?["type"]?.DeepClone(),
                    ["size"] = i["reference"]?["size"]?.DeepClone(),
                    ["grade"] = i["reference"]?["grade"]?.DeepClone(),
                    ["lastSeen"] = i["lastSeen"]?.DeepClone(),
                })]),
            });
        return new JsonObject { ["slots"] = slots, ["note"] = "As the logs last listed the pilot's own loadout." };
    }

    private static async Task<JsonNode> Armoury(JsonElement a, McpApi api, CancellationToken t)
    {
        var query = Need(a, "query");
        var armoury = (JsonObject)await api.Get("/api/armoury", t);
        var kinds = Str(a, "kind") is { } k ? [k] : new[] { "weapons", "armour", "attachments", "melee", "throwables" };
        var limit = Limit(a, 10);
        var answer = new JsonObject();

        foreach (var kind in kinds)
        {
            var hits = (armoury[kind] as JsonArray ?? []).OfType<JsonObject>()
                .Where(x => new[] { "name", "family", "manufacturer", "kind", "class" }
                    .Any(f => x[f] is JsonValue v && v.TryGetValue<string>(out var s) && s.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (hits.Count == 0) continue;

            answer[kind] = new JsonObject
            {
                ["found"] = hits.Count,
                ["items"] = new JsonArray([.. hits.Take(limit).Select(x => (JsonNode)Gear(x))]),
            };
        }

        if (answer.Count == 0)
            throw new McpToolException($"Nothing in the Armoury matches '{query}'.");
        return answer;
    }

    /// <summary>One Armoury record without its picture and finish lists, its shops cut to five.</summary>
    private static JsonObject Gear(JsonObject x)
    {
        var copy = (JsonObject)x.DeepClone();
        foreach (var drop in new[] { "uuid", "finishes", "pictured", "picture", "meleeConfigs" }) copy.Remove(drop);
        if (copy["market"] is JsonObject market && market["shops"] is JsonArray shops)
            market["shops"] = Take(shops, 5);
        return copy;
    }

    // ---- contracts and reputation ----

    private static async Task<JsonNode> MyContracts(JsonElement a, McpApi api, CancellationToken t)
    {
        var all = (await api.Get("/api/contracts", t) as JsonArray ?? []).OfType<JsonObject>().ToList();
        var outcome = Str(a, "outcome");
        var contractor = Str(a, "contractor");
        var system = Str(a, "system");
        var since = Num(a, "days") is { } days ? DateTimeOffset.UtcNow.AddDays(-days) : (DateTimeOffset?)null;

        bool Is(JsonObject c, string key, string? want) =>
            want is null || c[key]?.GetValue<string>() is { } v && v.Contains(want, StringComparison.OrdinalIgnoreCase);

        var found = all
            .Where(c => Is(c, "outcome", outcome) && Is(c, "issuer", contractor) && Is(c, "system", system))
            .Where(c => since is null || When(c["at"]) is { } at && at >= since)
            .ToList();

        var keep = new[] { "at", "name", "issuer", "type", "system", "outcome", "steps", "stepsDone", "minutes", "rep", "hauling" };
        return new JsonObject
        {
            ["found"] = found.Count,
            ["byOutcome"] = new JsonObject([.. found.GroupBy(c => c["outcome"]?.GetValue<string>() ?? "Unknown")
                .Select(g => KeyValuePair.Create(g.Key, (JsonNode?)JsonValue.Create(g.Count())))]),
            ["contracts"] = new JsonArray([.. found.Take(Limit(a, 20)).Select(c => (JsonNode)Pick(c, keep))]),
        };
    }

    private static async Task<JsonNode> MyReputation(JsonElement a, McpApi api, CancellationToken t)
    {
        var rep = (JsonObject)await api.Get("/api/reputation", t);
        var standing = await api.Get("/api/standing", t) as JsonArray ?? [];

        var organisations = new JsonArray();
        foreach (var org in (rep["organisations"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var place = org["place"] as JsonObject;
            organisations.Add(new JsonObject
            {
                ["name"] = org["name"]?.DeepClone(),
                ["section"] = org["section"]?.DeepClone(),
                ["rank"] = org["rank"]?.DeepClone(),
                ["progress"] = org["progress"]?.DeepClone(),
                ["rankNumber"] = place?["rankIndex"] is { } i ? i.GetValue<int>() + 1 : null,
                ["ranks"] = place?["rankCount"]?.DeepClone(),
                ["estimate"] = place?["estimate"]?.DeepClone(),
                ["nextRankAt"] = place?["next"]?.DeepClone(),
            });
        }

        return new JsonObject
        {
            ["organisations"] = organisations,
            ["contractors"] = new JsonArray([.. standing.OfType<JsonObject>()
                .Select(s => (JsonNode)Pick(s, ["issuer", "contracts", "completed", "failed", "abandoned", "last", "rep"]))]),
            ["note"] = "Ranks are what the game's reputation screen showed when last read; the contractor record is counted from the logs.",
        };
    }

    private static async Task<JsonNode> Events(JsonElement a, McpApi api, CancellationToken t)
    {
        var data = (JsonObject)await api.Get("/api/events", t);
        var events = new JsonArray();
        foreach (var ev in (data["events"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var tracks = new JsonArray();
            foreach (var track in (ev["tracks"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var points = track["points"]?.GetValue<double>() ?? 0;
                var tiers = (track["tiers"] as JsonArray ?? []).OfType<JsonObject>().ToList();
                var next = tiers.FirstOrDefault(x => (x["minPoints"]?.GetValue<double>() ?? 0) > points);
                tracks.Add(new JsonObject
                {
                    ["name"] = track["name"]?.DeepClone(),
                    ["points"] = points,
                    ["tiersReached"] = tiers.Count(x => (x["minPoints"]?.GetValue<double>() ?? 0) <= points),
                    ["tiers"] = tiers.Count,
                    ["nextTierAt"] = next?["minPoints"]?.DeepClone(),
                    ["pointsToNext"] = next is null ? null : (next["minPoints"]?.GetValue<double>() ?? 0) - points,
                });
            }
            events.Add(new JsonObject
            {
                ["id"] = ev["id"]?.DeepClone(),
                ["title"] = ev["title"]?.DeepClone(),
                ["contractsCompleted"] = ev["completed"]?.DeepClone(),
                ["lastSeen"] = ev["lastSeen"]?.DeepClone(),
                ["tracks"] = tracks,
            });
        }

        return new JsonObject
        {
            ["tracked"] = data["tracked"]?.DeepClone(),
            ["events"] = events,
            ["openContractsPaying"] = data["open"]?.DeepClone(),
            ["note"] = "Points are a floor: counted from the contracts the logs saw finished.",
        };
    }

    private static async Task<JsonNode> Wikelo(JsonElement a, McpApi api, CancellationToken t)
    {
        var data = (JsonObject)await api.Get("/api/wikelo", t);
        var query = Str(a, "query");
        var ready = a.ValueKind == JsonValueKind.Object && a.TryGetProperty("ready_only", out var r) && r.ValueKind == JsonValueKind.True;

        // What the pilot holds is the stashes' business; the trades are the game's.
        var holdings = api.Allows(Data.McpArea.Ships);
        if (ready && !holdings)
            throw new McpToolException("The pilot keeps their ships, gear and stashes from assistants, so which trades are ready cannot be told.");
        string[] needKeys = holdings ? ["name", "count", "unit", "have", "where"] : ["name", "count", "unit"];

        var found = (data["trades"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(x => x["retired"]?.GetValue<bool>() != true)
            .Where(x => query is null
                || (x["title"]?.GetValue<string>() ?? "").Contains(query, StringComparison.OrdinalIgnoreCase)
                || (x["requirements"] as JsonArray ?? []).OfType<JsonObject>().Concat((x["rewards"] as JsonArray ?? []).OfType<JsonObject>())
                    .Any(q => (q["name"]?.GetValue<string>() ?? "").Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Where(x => !ready || (x["requirements"] as JsonArray ?? []).OfType<JsonObject>().All(q => q["have"]?.GetValue<bool>() == true))
            .ToList();

        return new JsonObject
        {
            ["found"] = found.Count,
            ["trades"] = new JsonArray([.. found.Take(Limit(a, 10)).Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x["id"]?.DeepClone(),
                ["title"] = x["title"]?.DeepClone(),
                ["reputation"] = x["reputation"]?.DeepClone(),
                ["gives"] = new JsonArray([.. (x["rewards"] as JsonArray ?? []).OfType<JsonObject>().Select(q => (JsonNode)Pick(q, ["name", "count", "unit"]))]),
                ["needs"] = new JsonArray([.. (x["requirements"] as JsonArray ?? []).OfType<JsonObject>().Select(q => (JsonNode)Pick(q, needKeys))]),
                ["trackedAsList"] = api.Allows(Data.McpArea.Plans) ? x["trackedJobId"]?.DeepClone() : null,
            })]),
            ["note"] = holdings ? "'have' means seen in a stash, never how many." : "What the pilot holds is not shared.",
        };
    }

    // ---- what happened ----

    private static async Task<JsonNode> Logbook(JsonElement a, McpApi api, CancellationToken t)
    {
        // The logbook threads money and pickups through the sessions; each goes
        // out only with its own area, so closing Money closes it here too.
        var money = api.Allows(Data.McpArea.Money);
        var gear = api.Allows(Data.McpArea.Ships);
        var all = (await api.Get("/api/logbook", t) as JsonArray ?? []).OfType<JsonObject>()
            .Where(e => e["kind"]?.GetValue<string>() switch
            {
                "bought" or "sold" => money,
                "loot" => gear,
                _ => true,
            })
            .ToList();
        var kind = Str(a, "kind");
        var found = all.Where(e => kind is null || string.Equals(e["kind"]?.GetValue<string>(), kind, StringComparison.OrdinalIgnoreCase)).ToList();
        return new JsonObject
        {
            ["kinds"] = new JsonArray([.. all.Select(e => e["kind"]?.GetValue<string>()).Where(k => k is not null).Distinct()
                .Select(k => (JsonNode)JsonValue.Create(k)!)]),
            ["found"] = found.Count,
            ["entries"] = new JsonArray([.. found.Take(Limit(a, 20)).Select(e => e.DeepClone())]),
        };
    }

    private static async Task<JsonNode> Refinery(JsonElement a, McpApi api, CancellationToken t)
    {
        var data = (JsonObject)await api.Get("/api/mining/refinery", t);
        var now = DateTimeOffset.UtcNow;
        var orders = new JsonArray();
        foreach (var order in (data["orders"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var due = When(order["dueAt"]);
            orders.Add(new JsonObject
            {
                ["station"] = order["station"]?.DeepClone(),
                ["method"] = order["method"]?.DeepClone(),
                ["cost"] = order["cost"]?.DeepClone(),
                ["dueAt"] = order["dueAt"]?.DeepClone(),
                ["ready"] = due is { } d ? d <= now : null,
                ["lots"] = new JsonArray([.. (order["lots"] as JsonArray ?? []).OfType<JsonObject>()
                    .Select(l => (JsonNode)Pick(l, ["mineral", "quality", "quantity", "yield"]))]),
            });
        }
        return new JsonObject
        {
            ["orders"] = orders,
            ["note"] = "Read off refinery screens the pilot photographed or the live reader saw; an order never shown to the app is not here.",
        };
    }

    private static async Task<JsonNode> Casualties(JsonElement a, McpApi api, CancellationToken t) =>
        Trim(await api.Get("/api/casualties", t), 10);

    // ---- acting ----

    private static async Task<JsonNode> PinToNow(JsonElement a, McpApi api, CancellationToken t)
    {
        var shopping = Need(a, "what") == "shopping_list";
        var id = Need(a, "id");
        var want = Bool(a, "pinned") ?? true;
        var lists = await api.Get(shopping ? "/api/jobs" : "/api/checklists", t) as JsonArray ?? [];
        var list = lists.OfType<JsonObject>().FirstOrDefault(l => l["id"]?.GetValue<string>() == id && l["imported"] is null)
            ?? throw new McpToolException($"No {(shopping ? "shopping list" : "checklist")} with id '{id}'. Use my_plans.");

        if (list["pinned"]?.GetValue<bool>() != want)
            await api.Post($"/api/{(shopping ? "jobs" : "checklists")}/{Uri.EscapeDataString(id)}/pin", new { }, t);
        return new JsonObject { ["title"] = list["title"]?.DeepClone(), ["pinned"] = want };
    }

    private static async Task<JsonNode> Tick(JsonElement a, McpApi api, CancellationToken t)
    {
        var shopping = Need(a, "what") == "shopping_line";
        var id = Need(a, "list_id");
        var line = Need(a, "line");
        var want = Bool(a, "done") ?? true;
        var lists = await api.Get(shopping ? "/api/jobs" : "/api/checklists", t) as JsonArray ?? [];
        var list = lists.OfType<JsonObject>().FirstOrDefault(l => l["id"]?.GetValue<string>() == id && l["imported"] is null)
            ?? throw new McpToolException($"No {(shopping ? "shopping list" : "checklist")} with id '{id}'. Use my_plans.");

        var field = shopping ? "name" : "text";
        var items = (list["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var matches = items.Where(i => string.Equals(i[field]?.GetValue<string>(), line, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
            matches = [.. items.Where(i => (i[field]?.GetValue<string>() ?? "").Contains(line, StringComparison.OrdinalIgnoreCase))];
        if (matches.Count != 1)
            throw new McpToolException(matches.Count == 0
                ? $"'{list["title"]}' has no line '{line}'."
                : $"'{line}' matches {matches.Count} lines on '{list["title"]}'; give the line as written.");

        var item = matches[0];
        var name = item[field]!.GetValue<string>();
        if (shopping)
        {
            // A line the logs prove is in hand already reads as done; only the pilot's own tick can be set or cleared.
            var got = item["got"]?.GetValue<bool>() == true;
            if (want && item["have"]?.GetValue<bool>() == true && !got)
                return new JsonObject { ["line"] = name, ["done"] = true, ["note"] = "Already in hand by the logs; nothing to tick." };
            if (got != want)
                await api.Post($"/api/jobs/{Uri.EscapeDataString(id)}/got", new { name }, t);
        }
        else if ((item["done"]?.GetValue<bool>() == true) != want)
        {
            await api.Post($"/api/checklists/{Uri.EscapeDataString(id)}/items/{Uri.EscapeDataString(item["id"]!.GetValue<string>())}/toggle", new { }, t);
        }
        return new JsonObject { ["list"] = list["title"]?.DeepClone(), ["line"] = name, ["done"] = want };
    }

    private static async Task<JsonNode> AddStop(JsonElement a, McpApi api, CancellationToken t)
    {
        var id = Need(a, "plan_id");
        var place = Need(a, "place");
        var known = (await Places(api, t)).FirstOrDefault(n => string.Equals(n["name"]?.GetValue<string>(), place, StringComparison.OrdinalIgnoreCase));
        var trip = await api.Post($"/api/trips/{Uri.EscapeDataString(id)}/stops", new
        {
            placeId = known?["rawId"]?.GetValue<string>() ?? "",
            place = known?["name"]?.GetValue<string>() ?? place,
            note = Str(a, "note"),
            actions = Actions(a),
        }, t);
        return new JsonObject
        {
            ["plan"] = trip["title"]?.DeepClone(),
            ["stops"] = (trip["stops"] as JsonArray)?.Count ?? trip["stops"]?.DeepClone(),
            ["onTheMap"] = known is not null,
        };
    }

    private static async Task<JsonNode> TrackPlan(JsonElement a, McpApi api, CancellationToken t)
    {
        var id = Need(a, "plan_id");
        var want = Bool(a, "tracked") ?? true;
        var trip = (await api.Get("/api/trips", t) as JsonArray ?? []).OfType<JsonObject>()
            .FirstOrDefault(x => x["id"]?.GetValue<string>() == id && x["imported"] is null)
            ?? throw new McpToolException($"No flight plan with id '{id}'. Use my_plans.");
        if (trip["tracked"]?.GetValue<bool>() != want)
            await api.Post($"/api/trips/{Uri.EscapeDataString(id)}/track", new { }, t);
        return new JsonObject { ["plan"] = trip["title"]?.DeepClone(), ["tracked"] = want };
    }

    private static async Task<JsonNode> TrackEvent(JsonElement a, McpApi api, CancellationToken t)
    {
        var id = Str(a, "event_id");
        var answer = await api.Post(id is null ? "/api/events/track" : $"/api/events/track?id={Q(id)}", new { }, t);
        return new JsonObject { ["tracked"] = answer["tracked"]?.DeepClone(), ["following"] = id is null ? "play" : "this event" };
    }

    // ---- helpers for these ----

    /// <summary>A ship's class from the Garage lists: an exact class or name first, then a name containing the words.</summary>
    private static Task<string?> ShipClass(JsonObject garage, string wanted)
    {
        var ships = (garage["mine"] as JsonArray ?? []).Concat(garage["all"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var exact = ships.FirstOrDefault(s => string.Equals(s["class"]?.GetValue<string>(), wanted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s["name"]?.GetValue<string>(), wanted, StringComparison.OrdinalIgnoreCase));
        var loose = exact ?? ships.FirstOrDefault(s => (s["name"]?.GetValue<string>() ?? "").Contains(wanted, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(loose?["class"]?.GetValue<string>());
    }

    private static List<object> Actions(JsonElement a) =>
        a.TryGetProperty("actions", out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Take(20).Select(x => (object)new
            {
                kind = Str(x, "kind") ?? "do",
                text = Str(x, "text") ?? "",
                quantity = Num(x, "quantity"),
                unit = Str(x, "unit"),
            })]
            : [];

    private static JsonObject ActionList() => new()
    {
        ["type"] = "array",
        ["items"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["kind"] = Choice("What to do.", "buy", "sell", "load", "unload", "collect", "refuel", "repair", "do"),
                ["text"] = Text("What, e.g. 'Laranite'."),
                ["quantity"] = Number("How much."),
                ["unit"] = Text("'SCU' for cargo; leave out for a count."),
            },
            ["required"] = new JsonArray("kind", "text"),
        },
    };

    private static bool? Bool(JsonElement a, string name) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v)
            ? v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
            : null;

    private static DateTimeOffset? When(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var at) ? at : null;

    /// <summary>Cuts every array in an answer, at any depth, to <paramref name="count"/>.</summary>
    private static JsonNode Trim(JsonNode node, int count)
    {
        switch (node)
        {
            case JsonArray array:
                return new JsonArray([.. array.Take(count).Select(x => x is null ? null : Trim(x, count))]);
            case JsonObject obj:
                var copy = new JsonObject();
                foreach (var (key, value) in obj) copy[key] = value is null ? null : Trim(value, count);
                return copy;
            default:
                return node.DeepClone();
        }
    }
}
