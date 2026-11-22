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

        new("garage_builds", "Saved builds",
            "The fits the pilot has saved in the Garage, for one ship or for every ship: each build's id, name and "
            + "note, and the slots it changes from stock with the part it puts there.",
            Schema(("ship", Text("A ship's name or class; leave out for every saved build."), false)), false,
            GarageBuilds),

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

        new("choose_store", "Choose where to buy",
            "Says where one line of an existing shopping list is to be bought, or - with no line - where the whole "
            + "list is to be shopped. A line's store wins over the list's, so a run split between places is one store "
            + "per line. Leave the store out to clear it. Use my_plans for the list's id and the line as written there.",
            Schema(("list_id", Text("The list's id from my_plans."), true),
                   ("line", Text("The line's name as my_plans shows it; leave out to set the whole list's destination."), false),
                   ("store", Text("A terminal as where_to_buy names it, or a place (a line takes its cheapest counter "
                       + "there; the list takes the place itself). Leave out to clear."), false)), true,
            ChooseStore),

        new("save_garage_build", "Save a Garage build",
            "Saves a fit for a ship under a name, as the Garage's own save does, and answers with what it does to the "
            + "ship's power, cooling, shields and guns. With build_id it changes that build instead: the slots named "
            + "change and the rest stay. Use garage for the ship's slots and garage_options for what fits one; a part "
            + "that does not fit its slot is refused.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["ship"] = Text("The ship's name or class. Not needed with build_id."),
                    ["name"] = Text("The build's name; needed for a new one."),
                    ["note"] = Text("A note kept with the build, such as what it is for."),
                    ["build_id"] = Text("An existing build's id from garage_builds, to change it rather than make a new one."),
                    ["swaps"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 40,
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["slot"] = Text("The slot's portId from garage, or its hardpoint name."),
                                ["part"] = Text("The part's name or class as garage_options gives it; 'stock' for what the "
                                    + "ship comes with, 'empty' to leave the slot empty."),
                            },
                            ["required"] = new JsonArray("slot", "part"),
                        },
                    },
                },
            }, true, SaveGarageBuild),

        new("set_goal", "Set the savings goal",
            "Sets what the pilot is saving towards - a ship or anything with a price - which Now shows with the hours "
            + "of trading it is away; or, with nothing given, clears it. One goal at a time, so a new one replaces the "
            + "last. my_earnings shows the current one; ship_details or where_to_buy give a price.",
            Schema(("name", Text("What it is, such as a ship's name. Leave out with the target to clear the goal."), false),
                   ("target", Number("What it costs, in aUEC."), false)), true,
            SetGoal),

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

    private static async Task<JsonNode> GarageBuilds(JsonElement a, McpApi api, CancellationToken t)
    {
        string? cls = null;
        if (Str(a, "ship") is { } wanted)
            cls = await ShipClass((JsonObject)await api.Get("/api/garage", t), wanted)
                ?? throw new McpToolException($"No ship called '{wanted}' in the Garage. Call garage with no ship for the list.");

        var builds = await api.Get(cls is null ? "/api/garage/builds" : $"/api/garage/builds?ship={Q(cls)}", t) as JsonArray ?? [];
        var shown = new JsonArray();
        foreach (var build in builds.OfType<JsonObject>().Take(20))
        {
            // A build holds port ids and classes; the sheet says them as the
            // Garage does - hardpoints and part names - and is recomputed from
            // the reference, so it is what the build does today.
            var shipClass = build["shipClass"]?.GetValue<string>() ?? "";
            JsonObject? sheet = null;
            try { sheet = await Sheet(api, shipClass, Swaps(build), t); }
            catch (McpToolException) { /* a ship the reference no longer has: the build is still listed */ }

            shown.Add(new JsonObject
            {
                ["id"] = build["id"]?.DeepClone(),
                ["name"] = build["name"]?.DeepClone(),
                ["note"] = build["note"]?.DeepClone(),
                ["ship"] = sheet?["ship"]?["name"]?.DeepClone() ?? shipClass,
                ["shipClass"] = shipClass,
                ["updatedAt"] = build["updatedAt"]?.DeepClone(),
                ["changes"] = sheet is null ? null : Changes(sheet),
            });
        }

        var answer = new JsonObject { ["builds"] = shown, ["count"] = builds.Count };
        if (builds.Count == 0) answer["note"] = "No saved builds. save_garage_build makes one.";
        return answer;
    }

    private static async Task<JsonNode> SaveGarageBuild(JsonElement a, McpApi api, CancellationToken t)
    {
        JsonObject? existing = null;
        if (Str(a, "build_id") is { } id)
            existing = (await api.Get("/api/garage/builds", t) as JsonArray ?? []).OfType<JsonObject>()
                .FirstOrDefault(b => b["id"]?.GetValue<string>() == id)
                ?? throw new McpToolException($"No saved build with id '{id}'. Use garage_builds.");

        var name = Str(a, "name");
        if (existing is null && name is null)
            throw new McpToolException("A new build needs a name.");

        var cls = existing?["shipClass"]?.GetValue<string>()
            ?? await ShipClass((JsonObject)await api.Get("/api/garage", t), Need(a, "ship"))
            ?? throw new McpToolException($"No ship called '{Str(a, "ship")}' in the Garage. Call garage with no ship for the list.");

        var ship = (JsonObject)await api.Get($"/api/garage/{Uri.EscapeDataString(cls)}", t);
        var shipName = ship["ship"]?["name"]?.GetValue<string>() ?? cls;
        var ports = (ship["ports"] as JsonArray ?? []).OfType<JsonObject>().ToList();

        // Changing a build changes the slots named; the rest of it stands.
        var swaps = existing is null ? new Dictionary<string, string?>(StringComparer.Ordinal) : Swaps(existing);

        if (a.TryGetProperty("swaps", out var asked) && asked.ValueKind == JsonValueKind.Array)
            foreach (var swap in asked.EnumerateArray().Take(40))
            {
                var slot = Str(swap, "slot") ?? throw new McpToolException("Every swap needs a slot.");
                var part = Str(swap, "part") ?? throw new McpToolException($"The swap for '{slot}' needs a part.");
                var port = ports.FirstOrDefault(p => string.Equals(p["portId"]?.GetValue<string>(), slot, StringComparison.OrdinalIgnoreCase))
                    ?? ports.FirstOrDefault(p => string.Equals(p["hardpoint"]?.GetValue<string>(), slot, StringComparison.OrdinalIgnoreCase))
                    ?? throw new McpToolException($"The {shipName} has no slot '{slot}'. Use garage for its portIds.");
                var portId = port["portId"]!.GetValue<string>();

                if (part.Equals("stock", StringComparison.OrdinalIgnoreCase)) swaps.Remove(portId);
                else if (part.Equals("empty", StringComparison.OrdinalIgnoreCase) || part.Equals("none", StringComparison.OrdinalIgnoreCase)) swaps[portId] = null;
                else swaps[portId] = await PartFor(api, cls, shipName, portId, slot, part, t);
            }

        var saved = existing is null
            ? await api.Post("/api/garage/builds", new { shipClass = cls, name, swaps, note = Str(a, "note") }, t)
            : await api.Put($"/api/garage/builds/{Uri.EscapeDataString(existing["id"]!.GetValue<string>())}",
                new { name, swaps, note = Str(a, "note") }, t);

        var sheet = await Sheet(api, cls, swaps, t);
        var fit = sheet["sheet"] as JsonObject;
        return new JsonObject
        {
            ["id"] = saved["id"]?.DeepClone(),
            ["name"] = saved["name"]?.DeepClone(),
            ["ship"] = shipName,
            ["changed"] = existing is not null,
            ["changes"] = Changes(sheet),
            ["power"] = fit?["power"]?.DeepClone(),
            ["cooling"] = fit?["cooling"]?.DeepClone(),
            ["shield"] = fit?["shield"]?.DeepClone(),
            ["weapons"] = fit?["weapons"]?.DeepClone(),
            ["note"] = $"Saved in the Garage among the {shipName}'s builds. Nothing is fitted in game; the figures are what the fit would do.",
        };
    }

    /// <summary>
    /// The class of the part asked for one slot, from what the Garage offers
    /// for that slot - so a part of the wrong kind or size is refused here,
    /// with the reason, rather than saved as a build the bench cannot show.
    /// </summary>
    private static async Task<string> PartFor(McpApi api, string cls, string shipName, string portId, string slot, string part, CancellationToken t)
    {
        var offered = (JsonObject)await api.Get($"/api/garage/{Uri.EscapeDataString(cls)}/options?port={Uri.EscapeDataString(portId)}", t);
        if (offered["fixedReason"]?.GetValue<string>() is { } why)
            throw new McpToolException($"'{slot}' on the {shipName} cannot be changed: {why}");

        var parts = (offered["options"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(o => o["part"] as JsonObject).OfType<JsonObject>()
            .Select(p => (Class: p["class"]?.GetValue<string>() ?? "", Name: p["name"]?.GetValue<string>() ?? ""))
            .ToList();

        bool Is(string text) => string.Equals(text, part.Trim(), StringComparison.OrdinalIgnoreCase);
        var matches = parts.Where(p => Is(p.Class) || Is(p.Name)).ToList();
        if (matches.Count == 0)
            matches = [.. parts.Where(p => p.Name.Contains(part.Trim(), StringComparison.OrdinalIgnoreCase))];

        return matches.Select(m => m.Class).Distinct(StringComparer.OrdinalIgnoreCase).Count() switch
        {
            1 => matches[0].Class,
            0 => throw new McpToolException($"'{part}' does not fit '{slot}' on the {shipName}. garage_options lists what does."),
            _ => throw new McpToolException($"'{part}' matches {matches.Count} parts for '{slot}': "
                + $"{string.Join(", ", matches.Take(6).Select(m => m.Name))}. Give the name as written."),
        };
    }

    /// <summary>A saved build's swaps as the routes take them: port id to class, null for an empty port.</summary>
    private static Dictionary<string, string?> Swaps(JsonObject build) =>
        (build["swaps"] as JsonObject ?? []).ToDictionary(
            kv => kv.Key,
            kv => kv.Value is JsonValue v && v.TryGetValue<string>(out var s) ? s : null,
            StringComparer.Ordinal);

    /// <summary>The Garage's sheet for a ship with these parts fitted. Nothing is stored by asking.</summary>
    private static async Task<JsonObject> Sheet(McpApi api, string cls, Dictionary<string, string?> swaps, CancellationToken t) =>
        (JsonObject)await api.Post($"/api/garage/{Uri.EscapeDataString(cls)}/sheet", new { swaps }, t);

    /// <summary>The slots a fit changes from stock, said the way the Garage says them.</summary>
    private static JsonArray Changes(JsonObject sheet) =>
        new([.. (sheet["ports"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(p => p["changed"]?.GetValue<bool>() == true)
            .Select(p => (JsonNode)new JsonObject
            {
                ["portId"] = p["portId"]?.DeepClone(),
                ["hardpoint"] = p["hardpoint"]?.DeepClone(),
                ["part"] = p["name"]?.DeepClone() ?? "empty",
                ["stock"] = p["stockName"]?.DeepClone(),
            })]);

    private static async Task<JsonNode> SetGoal(JsonElement a, McpApi api, CancellationToken t)
    {
        var name = Str(a, "name");
        var target = Num(a, "target");

        if (name is null && target is null)
        {
            await api.Post("/api/goal", null, t);
            return new JsonObject { ["goal"] = null, ["note"] = "Cleared; Now no longer shows a goal." };
        }

        if (name is null) throw new McpToolException("A goal needs a name: what the pilot is saving for.");
        if (target is not > 0) throw new McpToolException("A goal needs its price, in aUEC, above zero.");

        await api.Post("/api/goal", new { name, target, setAt = DateTimeOffset.UtcNow }, t);
        var earnings = await api.Get("/api/earnings?days=30", t);
        return new JsonObject
        {
            ["goal"] = earnings["goal"]?.DeepClone(),
            ["hoursToGoal"] = earnings["hoursToGoal"]?.DeepClone(),
            ["basis"] = earnings["basis"]?.DeepClone(),
            ["note"] = "Shown on Now. Hours are of trading at the pilot's recent rate, or their lifetime one when recent "
                + "flying is too thin; there is none when the logs hold no trading to divide by.",
        };
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

    private static async Task<JsonNode> ChooseStore(JsonElement a, McpApi api, CancellationToken t)
    {
        var id = Need(a, "list_id");
        var wanted = Str(a, "store");
        var lists = await api.Get("/api/jobs", t) as JsonArray ?? [];
        var list = lists.OfType<JsonObject>().FirstOrDefault(l => l["id"]?.GetValue<string>() == id && l["imported"] is null)
            ?? throw new McpToolException($"No shopping list with id '{id}'. Use my_plans.");
        var path = $"/api/jobs/{Uri.EscapeDataString(id)}";

        if (Str(a, "line") is not { } line)
        {
            var (place, placeId) = wanted is null ? (null, null) : await new PlaceBook(api).Find(wanted, t);
            await api.Post($"{path}/destination", new { place, placeId }, t);
            return new JsonObject { ["list"] = list["title"]?.DeepClone(), ["destination"] = place };
        }

        // The same matching as tick: as written, else the one line containing it.
        var items = (list["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var matches = items.Where(i => string.Equals(i["name"]?.GetValue<string>(), line, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
            matches = [.. items.Where(i => (i["name"]?.GetValue<string>() ?? "").Contains(line, StringComparison.OrdinalIgnoreCase))];
        if (matches.Count != 1)
            throw new McpToolException(matches.Count == 0
                ? $"'{list["title"]}' has no line '{line}'."
                : $"'{line}' matches {matches.Count} lines on '{list["title"]}'; give the line as written.");

        var name = matches[0]["name"]!.GetValue<string>();
        var store = wanted is null ? null : await ResolveStore(api, name, wanted, new PlaceBook(api), t);
        await api.Post($"{path}/store", new { name, store = store?.Store, storeId = store?.StoreId }, t);

        var answer = store?.Said(name) ?? new JsonObject { ["line"] = name, ["store"] = null };
        answer["list"] = list["title"]?.DeepClone();
        return answer;
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
