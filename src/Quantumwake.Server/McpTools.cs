using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quantumwake.Server;

/// <summary>One thing an assistant can ask of the app.</summary>
/// <param name="Writes">Whether it changes the pilot's data; offered and run only with changes allowed.</param>
public sealed record McpTool(
    string Name,
    string Title,
    string Description,
    JsonObject Input,
    bool Writes,
    Func<JsonElement, McpApi, CancellationToken, Task<JsonNode>> Run)
{
    public JsonObject Describe() => new()
    {
        ["name"] = Name,
        ["title"] = Title,
        ["description"] = Description,
        ["inputSchema"] = Input.DeepClone(),
        // Hints a client may show or act on; the switch is what enforces it.
        ["annotations"] = new JsonObject
        {
            ["title"] = Title,
            ["readOnlyHint"] = !Writes,
            ["destructiveHint"] = false,
            ["idempotentHint"] = !Writes,
            ["openWorldHint"] = false,
        },
    };
}

/// <summary>
/// What an assistant may ask (1.2.48): about the pilot's play, their ships,
/// items and plans, the game's catalogue, prices and routes - and, with
/// changes allowed, to make a flight plan, a shopping list or a checklist.
/// </summary>
/// <remarks>
/// <para>
/// Each answer is trimmed to what a question needs. The routes table runs to
/// 45 KB, the item catalogue to 10 MB and the mission list to 1.5 MB on this
/// install, and an assistant handed all of it reads less well, not better;
/// the tools search and cut, and say how many there were.
/// </para>
/// <para>
/// Nothing here deletes, and nothing edits what the pilot wrote: the writing
/// tools only add a new plan, list or checklist, or a line to a list. Those
/// are the changes a pilot can look over and throw away in one click, which is
/// the right size of thing to let an assistant do on its own.
/// </para>
/// </remarks>
public static class McpTools
{
    public static readonly IReadOnlyList<McpTool> All =
    [
        new("whats_happening", "What is happening now",
            "The pilot's game right now, as their log tells it: whether Star Citizen is running, where they are "
            + "(with how sure the app is), the ship they are in, their party and the contracts open in this session.",
            Schema(), false, Now),

        new("search", "Search everything",
            "Searches the pilot's logs, what they wrote in the app (plans, lists, checklists) and the game's "
            + "catalogue of ships, items and places. Use it to find the exact name or id of something first.",
            Schema(("query", Text("What to look for, e.g. 'Arrow', 'Laranite', 'Port Tressler'."), true)), false,
            async (a, api, t) => await api.Get($"/api/search?q={Q(Need(a, "query"))}", t)),

        new("item_details", "Item details",
            "What an item is (type, size, grade, maker), where it is sold and for how much, and where it can be "
            + "found in the game (loot). Takes a name or the catalogue id search returned.",
            Schema(("item", Text("The item's name or catalogue id, e.g. 'JS-400' or 'POWR_AMRS_S02_JS400_SCItem'."), true)), false,
            ItemDetails),

        new("ship_details", "Ship details",
            "A ship's role, crew, cargo, speed, claim times, its parts, and where it can be bought or rented.",
            Schema(("ship", Text("The ship's name or class, e.g. 'Anvil Arrow' or 'ANVL_Arrow'."), true)), false,
            ShipDetails),

        new("where_to_buy", "Where to buy",
            "The terminals and shops that sell a commodity or an item, cheapest first, with the stock seen and "
            + "when. Prices are the UEX community's last reports and need its feed on in the app.",
            Schema(("name", Text("A commodity or item name, e.g. 'Laranite' or 'JS-400'."), true),
                   ("limit", Count("How many sellers, 1-25.", 10), false)), false,
            WhereToBuy),

        new("trade_routes", "Trade routes",
            "The best buy-here-sell-there cargo routes for a hold size and budget, from UEX prices: commodity, "
            + "buy and sell terminals, margin per SCU, units, profit, and the security at each end.",
            Schema(("cargo_scu", Number("The hold to fill, in SCU."), true),
                   ("budget", Number("aUEC available to spend; leave out for no limit."), false),
                   ("from", Text("Only routes starting at this terminal or place."), false),
                   ("safety", Choice("Which ends to allow.", "any", "avoid-lawless", "monitored", "prefer-monitored"), false),
                   ("limit", Count("How many routes, 1-25.", 10), false)), false,
            TradeRoutes),

        new("trade_here", "Trade from a place",
            "What can be bought at one place and sold for a margin elsewhere, from UEX prices.",
            Schema(("place", Text("A place or terminal name, e.g. 'Area18'."), true)), false,
            async (a, api, t) => await api.Get($"/api/trade/advice?place={Q(Need(a, "place"))}", t)),

        new("my_ships", "My ships",
            "The ships the pilot has flown, with hours, sorties, first and last flown, role and cargo, and how many "
            + "ships the Fleet Manager last listed as owned. A ship never flown is not in the flown list.",
            Schema(), false, MyShips),

        new("my_items", "My items",
            "Where the pilot's things are, from the stash listings in their log: each place with how many items "
            + "and when it was last seen, or - with an item named - every place that item was seen. A listing says "
            + "an item was there, never how many, and a removal is never logged.",
            Schema(("item", Text("Only places holding this item; leave out for the summary."), false)), false,
            MyItems),

        new("my_plans", "My plans",
            "The pilot's flight plans with their stops and run sheets, open shopping lists with what is still "
            + "missing, and checklists with their open tasks.",
            Schema(), false, MyPlans),

        new("find_missions", "Find missions",
            "Searches the contracts the installed game can offer: title, contractor, type, systems, the reputation "
            + "they need, and whether the pilot has taken or completed one before.",
            Schema(("query", Text("Words in the title or contractor, e.g. 'salvage', 'Covalex'."), false),
                   ("system", Text("Only contracts offered in this system, e.g. 'Pyro'."), false),
                   ("limit", Count("How many, 1-25.", 15), false)), false,
            FindMissions),

        new("find_places", "Find places",
            "Stations, cities, outposts and other places the app knows, with system, body, kind and how often "
            + "the pilot has been. Use it for the exact place name before making a flight plan.",
            Schema(("query", Text("Part of a name, e.g. 'Tressler', 'Lorville', 'Levski'."), true),
                   ("limit", Count("How many, 1-25.", 15), false)), false,
            FindPlaces),

        new("recent_sessions", "Recent sessions",
            "The pilot's latest play sessions from their logs - when, how long in game, ships, places, jumps, "
            + "contracts - and their earnings over the last week.",
            Schema(("limit", Count("How many sessions, 1-10.", 5), false)), false,
            RecentSessions),

        new("create_flight_plan", "Make a flight plan",
            "Adds a new flight plan to the Itinerary, with stops in order and, for each stop, optional actions "
            + "(buy, sell, load, unload, collect, refuel, repair, or anything else as 'do'). The plan becomes the "
            + "tracked one. Use find_places first: a stop the app knows by name is drawn on its map.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["title"] = Text("What the plan is for, e.g. 'Laranite run'."),
                    ["stops"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["minItems"] = 1,
                        ["maxItems"] = 30,
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["place"] = Text("The place, as find_places names it."),
                                ["note"] = Text("A note for the stop."),
                                ["actions"] = new JsonObject
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
                                },
                            },
                            ["required"] = new JsonArray("place"),
                        },
                    },
                },
                ["required"] = new JsonArray("title", "stops"),
            }, true, CreateFlightPlan),

        new("create_shopping_list", "Make a shopping list",
            "Adds a new shopping list on the Shopping page, which then shows where each line is sold and what the "
            + "pilot already holds. Lines are things to buy, with an amount if there is one.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["title"] = Text("What the list is for."),
                    ["items"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["minItems"] = 1,
                        ["maxItems"] = 60,
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["name"] = Text("The item or commodity, by its catalogue name."),
                                ["needed"] = Number("How many, or how much; leave out for any amount."),
                                ["unit"] = Text("'SCU' for cargo; leave out for a count."),
                            },
                            ["required"] = new JsonArray("name"),
                        },
                    },
                    ["destination"] = Text("Where the pilot means to shop, as find_places names it; leave out for anywhere."),
                },
                ["required"] = new JsonArray("title", "items"),
            }, true, CreateShoppingList),

        new("add_to_shopping_list", "Add to a shopping list",
            "Adds one thing to a shopping list: the one named by id (from my_plans), else the list the pilot is "
            + "filling. Asking for something already on it adds to its amount.",
            Schema(("name", Text("The item or commodity."), true),
                   ("needed", Number("How many, or how much."), false),
                   ("unit", Text("'SCU' for cargo; leave out for a count."), false),
                   ("list_id", Text("The list's id from my_plans."), false)), true,
            AddToShoppingList),

        new("create_checklist", "Make a checklist",
            "Adds a new checklist on the Checklists page: tasks the pilot ticks off themselves, such as a "
            + "departure check.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["title"] = Text("What the checklist is for."),
                    ["tasks"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["minItems"] = 1,
                        ["maxItems"] = 40,
                        ["items"] = Text("One task."),
                    },
                },
                ["required"] = new JsonArray("title", "tasks"),
            }, true, CreateChecklist),
    ];

    /// <summary>What every list the assistant made says it came from, on Shopping.</summary>
    public const string Source = "an AI assistant";

    // ---- the reading tools ----

    private static async Task<JsonNode> Now(JsonElement a, McpApi api, CancellationToken t)
    {
        var now = (JsonObject)await api.Get("/api/now", t);
        // The screen's own checks and older events are the dashboard's detail.
        now.Remove("screen");
        if (now["recentEvents"] is JsonArray events)
            now["recentEvents"] = Take(events, 10);
        return now;
    }

    private static async Task<JsonNode> ItemDetails(JsonElement a, McpApi api, CancellationToken t)
    {
        var wanted = Need(a, "item");
        var item = (JsonObject)await api.Get($"/api/reference/item?id={Q(wanted)}", t);
        if (item["card"] is null)
        {
            // Not an id, nor a name the item lookup takes: the catalogue search knows more spellings.
            var id = await CatalogueId(api, wanted, "part", t);
            if (id is not null) item = (JsonObject)await api.Get($"/api/reference/item?id={Q(id)}", t);
        }
        if (item["card"] is null)
            throw new McpToolException($"The catalogue has no item called '{wanted}'. Try search first.");

        foreach (var list in new[] { "shops", "stockOn" })
            if (item[list] is JsonArray rows) item[list] = Take(rows, 15);
        if (item["where"] is JsonObject where)
            foreach (var list in new[] { "places", "hits", "ships", "npcs" })
                if (where[list] is JsonArray rows) where[list] = Take(rows, 10);
        return item;
    }

    private static async Task<JsonNode> ShipDetails(JsonElement a, McpApi api, CancellationToken t)
    {
        var wanted = Need(a, "ship");
        var ship = (JsonObject)await api.Get($"/api/reference/ship?id={Q(wanted)}", t);
        if (ship["name"] is null)
            throw new McpToolException($"The catalogue has no ship called '{wanted}'. Try search first.");
        foreach (var list in new[] { "shops", "rentals" })
            if (ship[list] is JsonArray rows) ship[list] = Take(rows, 10);
        if (ship["parts"] is JsonArray parts) ship["parts"] = Take(parts, 40);

        // The NPC variants are 35 KB for a Cutlass: who flies one is the
        // question, and their names answer it.
        if (ship["npcVersions"] is JsonArray npcs)
            ship["npcVersions"] = new JsonObject
            {
                ["count"] = npcs.Count,
                ["names"] = new JsonArray([.. npcs.OfType<JsonObject>().Take(15)
                    .Select(n => n["name"]?.DeepClone() ?? n["className"]?.DeepClone())]),
            };
        return ship;
    }

    private static async Task<JsonNode> WhereToBuy(JsonElement a, McpApi api, CancellationToken t)
    {
        var name = Need(a, "name");
        var limit = Limit(a, 10);
        var sellers = (JsonObject)await api.Get($"/api/shopping/sellers?name={Q(name)}", t);
        var rows = sellers["sellers"] as JsonArray ?? [];
        var answer = new JsonObject
        {
            ["name"] = sellers["name"]?.DeepClone(),
            ["kind"] = sellers["kind"]?.DeepClone(),
            ["sellerCount"] = rows.Count,
            ["sellers"] = Take(rows, limit),
        };
        if (rows.Count == 0)
            answer["note"] = "No seller known. The UEX price feed may be off in the app, or nothing sells this.";
        return answer;
    }

    private static async Task<JsonNode> TradeRoutes(JsonElement a, McpApi api, CancellationToken t)
    {
        var scu = Num(a, "cargo_scu") ?? throw new McpToolException("cargo_scu is needed: the hold to fill, in SCU.");
        var query = $"/api/routes?scu={scu.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (Num(a, "budget") is { } budget) query += $"&capital={budget.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (Str(a, "from") is { } from) query += $"&from={Q(from)}";
        if (Str(a, "safety") is { } safety) query += $"&safety={Q(safety)}";

        var routes = await api.Get(query, t) as JsonArray ?? [];
        var keep = new[]
        {
            "commodity", "buyAt", "buyPrice", "buySecurity", "sellAt", "sellPrice", "sellSecurity", "marginPerScu",
            "units", "profit", "outlay", "limitedBy", "buyStockScu", "sellDemandScu", "freshness", "buySeenAt", "sellSeenAt",
        };
        var shown = new JsonArray();
        foreach (var route in routes.Take(Limit(a, 10)).OfType<JsonObject>())
            shown.Add(Pick(route, keep));
        return new JsonObject
        {
            ["routeCount"] = routes.Count,
            ["routes"] = shown,
            ["note"] = routes.Count == 0 ? "No route. The UEX price feed may be off in the app." : "Prices are UEX's last reports, not live.",
        };
    }

    private static async Task<JsonNode> MyShips(JsonElement a, McpApi api, CancellationToken t)
    {
        var fleet = (JsonObject)await api.Get("/api/fleet", t);
        var flown = new JsonArray();
        foreach (var ship in (fleet["flown"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var reference = ship["reference"] as JsonObject;
            flown.Add(new JsonObject
            {
                ["name"] = ship["name"]?.DeepClone(),
                ["hoursInGame"] = Hours(ship["estimatedTime"]?.GetValue<string>()),
                ["sorties"] = ship["sorties"]?.DeepClone(),
                ["firstFlown"] = ship["firstFlown"]?.DeepClone(),
                ["lastFlown"] = ship["lastFlown"]?.DeepClone(),
                ["career"] = reference?["career"]?.DeepClone(),
                ["role"] = reference?["role"]?.DeepClone(),
                ["crew"] = reference?["crew"]?.DeepClone(),
                ["cargoScu"] = reference?["cargoScu"]?.DeepClone(),
            });
        }
        return new JsonObject
        {
            ["ownedAtFleetManager"] = fleet["owned"]?.DeepClone(),
            ["flown"] = flown,
            ["note"] = "Owned is what the Fleet Manager last listed in the logs; flown is every ship the logs saw the pilot in.",
        };
    }

    private static async Task<JsonNode> MyItems(JsonElement a, McpApi api, CancellationToken t)
    {
        var stash = await api.Get("/api/stash", t) as JsonArray ?? [];
        var wanted = Str(a, "item");
        var places = new JsonArray();

        foreach (var place in stash.OfType<JsonObject>())
        {
            if (wanted is null)
            {
                places.Add(Pick(place, ["name", "locationId", "itemCount", "lastSeen"]));
                continue;
            }

            var matches = (place["groups"] as JsonArray ?? []).OfType<JsonObject>()
                .SelectMany(g => (g["items"] as JsonArray ?? []).OfType<JsonObject>())
                .Where(i => i["name"]?.GetValue<string>() is { } n && n.Contains(wanted, StringComparison.OrdinalIgnoreCase))
                .Select(i => (JsonNode)new JsonObject { ["item"] = i["name"]!.DeepClone(), ["lastSeen"] = i["lastSeen"]?.DeepClone() })
                .ToArray();
            if (matches.Length > 0)
                places.Add(new JsonObject { ["place"] = place["name"]?.DeepClone(), ["items"] = new JsonArray(matches) });
        }

        return new JsonObject
        {
            ["places"] = places,
            ["note"] = "From stash listings in the logs: they say an item was there, never how many, and removals are not logged.",
        };
    }

    private static async Task<JsonNode> MyPlans(JsonElement a, McpApi api, CancellationToken t)
    {
        var trips = await api.Get("/api/trips", t) as JsonArray ?? [];
        var jobs = await api.Get("/api/jobs", t) as JsonArray ?? [];
        var lists = await api.Get("/api/checklists", t) as JsonArray ?? [];

        var plans = new JsonArray();
        foreach (var trip in trips.OfType<JsonObject>().Where(x => x["imported"] is null).Take(15))
        {
            var stops = new JsonArray();
            foreach (var stop in (trip["stops"] as JsonArray ?? []).OfType<JsonObject>())
                stops.Add(new JsonObject
                {
                    ["place"] = stop["place"]?.DeepClone(),
                    ["done"] = stop["done"]?.DeepClone(),
                    ["actions"] = new JsonArray([.. (stop["actions"] as JsonArray ?? []).OfType<JsonObject>()
                        .Select(x => (JsonNode)Pick(x, ["kind", "text", "quantity", "unit", "done"]))]),
                });
            plans.Add(new JsonObject
            {
                ["id"] = trip["id"]?.DeepClone(),
                ["title"] = trip["title"]?.DeepClone(),
                ["tracked"] = trip["tracked"]?.DeepClone(),
                ["stops"] = stops,
            });
        }

        var shopping = new JsonArray();
        foreach (var job in jobs.OfType<JsonObject>().Where(x => x["imported"] is null && x["done"]?.GetValue<bool>() != true))
            shopping.Add(new JsonObject
            {
                ["id"] = job["id"]?.DeepClone(),
                ["title"] = job["title"]?.DeepClone(),
                ["kind"] = job["kind"]?.DeepClone(),
                ["destination"] = job["destination"]?.DeepClone(),
                ["pinned"] = job["pinned"]?.DeepClone(),
                ["inHand"] = job["haveCount"]?.DeepClone(),
                ["lines"] = job["totalCount"]?.DeepClone(),
                ["items"] = new JsonArray([.. (job["items"] as JsonArray ?? []).OfType<JsonObject>()
                    .Select(x => (JsonNode)Pick(x, ["name", "needed", "unit", "have", "buyAt", "buyPrice"]))]),
            });

        var checklists = new JsonArray();
        foreach (var list in lists.OfType<JsonObject>().Where(x => x["imported"] is null))
            checklists.Add(new JsonObject
            {
                ["id"] = list["id"]?.DeepClone(),
                ["title"] = list["title"]?.DeepClone(),
                ["pinned"] = list["pinned"]?.DeepClone(),
                ["openTasks"] = new JsonArray([.. (list["items"] as JsonArray ?? []).OfType<JsonObject>()
                    .Where(x => x["done"]?.GetValue<bool>() != true)
                    .Select(x => (JsonNode)JsonValue.Create(x["text"]?.GetValue<string>() ?? ""))]),
            });

        return new JsonObject { ["flightPlans"] = plans, ["shoppingLists"] = shopping, ["checklists"] = checklists };
    }

    private static async Task<JsonNode> FindMissions(JsonElement a, McpApi api, CancellationToken t)
    {
        var all = (JsonObject)await api.Get("/api/reference/missions", t);
        var query = Str(a, "query");
        var system = Str(a, "system");
        var keep = new[] { "id", "title", "contractor", "type", "systems", "rep", "rank", "illegal", "taken", "completed" };

        var found = (all["missions"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(m => m["unreleased"]?.GetValue<bool>() != true)
            .Where(m => query is null || new[] { "title", "contractor", "type" }
                .Any(k => m[k]?.GetValue<string>() is { } v && v.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Where(m => system is null || (m["systems"] as JsonArray ?? []).Any(s => string.Equals(s?.GetValue<string>(), system, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new JsonObject
        {
            ["found"] = found.Count,
            ["missions"] = new JsonArray([.. found.Take(Limit(a, 15)).Select(m => (JsonNode)Pick(m, keep))]),
        };
    }

    private static async Task<JsonNode> FindPlaces(JsonElement a, McpApi api, CancellationToken t)
    {
        var query = Need(a, "query");
        var nodes = await Places(api, t);
        var found = nodes.Where(n => n["name"]?.GetValue<string>() is { } name && name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n["visits"]?.GetValue<int>() ?? 0)
            .ToList();
        return new JsonObject
        {
            ["found"] = found.Count,
            ["places"] = new JsonArray([.. found.Take(Limit(a, 15))
                .Select(n => (JsonNode)Pick(n, ["name", "system", "body", "kind", "visits", "lastVisit"]))]),
        };
    }

    private static async Task<JsonNode> RecentSessions(JsonElement a, McpApi api, CancellationToken t)
    {
        var sessions = await api.Get("/api/sessions", t) as JsonArray ?? [];
        var earnings = await api.Get("/api/earnings?days=7", t);
        return new JsonObject
        {
            ["sessions"] = Take(sessions, Math.Min(Limit(a, 5), 10)),
            ["lastWeek"] = earnings["window"]?.DeepClone(),
        };
    }

    // ---- the writing tools ----

    private static async Task<JsonNode> CreateFlightPlan(JsonElement a, McpApi api, CancellationToken t)
    {
        var title = Need(a, "title");
        if (!a.TryGetProperty("stops", out var stops) || stops.ValueKind != JsonValueKind.Array || stops.GetArrayLength() == 0)
            throw new McpToolException("A flight plan needs at least one stop.");

        var places = await Places(api, t);
        var unknown = new List<string>();
        var body = new List<object>();
        foreach (var stop in stops.EnumerateArray().Take(30))
        {
            var place = Str(stop, "place") ?? throw new McpToolException("Every stop needs a place.");
            var known = places.FirstOrDefault(n => string.Equals(n["name"]?.GetValue<string>(), place, StringComparison.OrdinalIgnoreCase));
            if (known is null) unknown.Add(place);

            var actions = stop.TryGetProperty("actions", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Take(20).Select(x => (object)new
                {
                    kind = Str(x, "kind") ?? "do",
                    text = Str(x, "text") ?? "",
                    quantity = Num(x, "quantity"),
                    unit = Str(x, "unit"),
                }).ToList()
                : [];

            body.Add(new
            {
                placeId = known?["rawId"]?.GetValue<string>() ?? "",
                place = known?["name"]?.GetValue<string>() ?? place,
                note = Str(stop, "note"),
                actions,
            });
        }

        var trip = await api.Post("/api/trips", new { title, stops = body }, t);
        return new JsonObject
        {
            ["id"] = trip["id"]?.DeepClone(),
            ["title"] = trip["title"]?.DeepClone(),
            ["stops"] = (trip["stops"] as JsonArray)?.Count ?? body.Count,
            ["notOnTheMap"] = new JsonArray([.. unknown.Select(u => (JsonNode)JsonValue.Create(u)!)]),
            ["note"] = "Added to the Itinerary as the tracked plan. Stops the app does not know by name are kept as written but cannot be drawn on its map.",
        };
    }

    private static async Task<JsonNode> CreateShoppingList(JsonElement a, McpApi api, CancellationToken t)
    {
        var title = Need(a, "title");
        if (!a.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
            throw new McpToolException("A shopping list needs at least one line.");

        var lines = items.EnumerateArray().Take(60)
            .Select(x => new { name = Str(x, "name"), needed = Num(x, "needed") ?? 0, unit = Str(x, "unit") ?? "" })
            .Where(x => x.name is not null)
            .ToList();
        if (lines.Count == 0) throw new McpToolException("Every line needs a name.");

        string? destination = null, destinationId = null;
        if (Str(a, "destination") is { } wanted)
        {
            var known = (await Places(api, t)).FirstOrDefault(n => string.Equals(n["name"]?.GetValue<string>(), wanted, StringComparison.OrdinalIgnoreCase));
            destination = known?["name"]?.GetValue<string>() ?? wanted;
            destinationId = known?["rawId"]?.GetValue<string>();
        }

        var job = await api.Post("/api/jobs", new { title, kind = "list", source = Source, items = lines, destination, destinationId }, t);
        return new JsonObject
        {
            ["id"] = job["id"]?.DeepClone(),
            ["title"] = job["title"]?.DeepClone(),
            ["lines"] = lines.Count,
            ["note"] = "Added on the Shopping page, marked as from an AI assistant. The page shows where each line is sold and what the pilot already holds.",
        };
    }

    private static async Task<JsonNode> AddToShoppingList(JsonElement a, McpApi api, CancellationToken t)
    {
        var landed = await api.Post("/api/jobs/collect", new
        {
            name = Need(a, "name"),
            needed = Num(a, "needed") ?? 1,
            unit = Str(a, "unit") ?? "",
            jobId = Str(a, "list_id"),
        }, t);
        return new JsonObject { ["addedTo"] = landed["title"]?.DeepClone(), ["listId"] = landed["id"]?.DeepClone() };
    }

    private static async Task<JsonNode> CreateChecklist(JsonElement a, McpApi api, CancellationToken t)
    {
        var title = Need(a, "title");
        if (!a.TryGetProperty("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array || tasks.GetArrayLength() == 0)
            throw new McpToolException("A checklist needs at least one task.");

        var list = await api.Post("/api/checklists", new { title }, t);
        var id = list["id"]?.GetValue<string>() ?? throw new McpToolException("The checklist was not made.");
        var added = 0;
        foreach (var task in tasks.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Take(40))
        {
            await api.Post($"/api/checklists/{Uri.EscapeDataString(id)}/items", new { text = task.GetString(), attachments = Array.Empty<object>() }, t);
            added++;
        }
        return new JsonObject { ["id"] = id, ["title"] = list["title"]?.DeepClone(), ["tasks"] = added };
    }

    // ---- helpers ----

    private static async Task<List<JsonObject>> Places(McpApi api, CancellationToken t) =>
        [.. ((await api.Get("/api/map", t))["nodes"] as JsonArray ?? []).OfType<JsonObject>()];

    /// <summary>The catalogue id search offers for a name, of the kind wanted, preferring an exact name.</summary>
    private static async Task<string?> CatalogueId(McpApi api, string name, string kind, CancellationToken t)
    {
        var found = await api.Get($"/api/search?q={Q(name)}", t);
        var hits = (found["groups"] as JsonArray ?? []).OfType<JsonObject>()
            .SelectMany(g => (g["hits"] as JsonArray ?? []).OfType<JsonObject>())
            .Where(h => h["kind"]?.GetValue<string>() == kind)
            .ToList();
        var exact = hits.FirstOrDefault(h => string.Equals(h["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase));
        return (exact ?? hits.FirstOrDefault())?["id"]?.GetValue<string>();
    }

    private static JsonObject Pick(JsonObject from, IEnumerable<string> keys)
    {
        var picked = new JsonObject();
        foreach (var key in keys)
            if (from[key] is { } value) picked[key] = value.DeepClone();
        return picked;
    }

    private static JsonArray Take(JsonArray from, int count) =>
        new([.. from.Take(count).Select(x => x?.DeepClone())]);

    /// <summary>"12:21:26.67" or "2.20:04:42" to hours, rounded; null when it does not parse.</summary>
    private static double? Hours(string? span) =>
        TimeSpan.TryParse(span, System.Globalization.CultureInfo.InvariantCulture, out var t) ? Math.Round(t.TotalHours, 1) : null;

    private static string Q(string value) => Uri.EscapeDataString(value);

    private static string? Str(JsonElement a, string name) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
        && v.GetString() is { } s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    private static string Need(JsonElement a, string name) =>
        Str(a, name) ?? throw new McpToolException($"'{name}' is needed.");

    private static double? Num(JsonElement a, string name) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
        && v.TryGetDouble(out var d) && double.IsFinite(d) && d >= 0 ? d : null;

    private static int Limit(JsonElement a, int fallback) =>
        Num(a, "limit") is { } n ? Math.Clamp((int)n, 1, 25) : fallback;

    private static JsonObject Schema(params (string Name, JsonObject Type, bool Required)[] properties)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, type, need) in properties)
        {
            props[name] = type;
            if (need) required.Add(name);
        }
        var schema = new JsonObject { ["type"] = "object", ["properties"] = props };
        if (required.Count > 0) schema["required"] = required;
        return schema;
    }

    private static JsonObject Text(string description) => new() { ["type"] = "string", ["description"] = description };
    private static JsonObject Number(string description) => new() { ["type"] = "number", ["description"] = description };
    private static JsonObject Count(string description, int fallback) =>
        new() { ["type"] = "integer", ["description"] = description, ["minimum"] = 1, ["maximum"] = 25, ["default"] = fallback };
    private static JsonObject Choice(string description, params string[] values) =>
        new() { ["type"] = "string", ["description"] = description, ["enum"] = new JsonArray([.. values.Select(v => (JsonNode)JsonValue.Create(v)!)]) };
}
