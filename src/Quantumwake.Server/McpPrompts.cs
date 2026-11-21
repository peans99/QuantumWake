using System.Text;
using System.Text.Json.Nodes;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>One argument a prompt takes; MCP prompt arguments are always text.</summary>
public sealed record McpPromptArgument(string Name, string Description, bool Required = false);

/// <summary>
/// A ready-made request an MCP client offers the pilot by name (1.2.52) -
/// "Plan a trade run for my ship" in Claude's attachment menu - which
/// expands to a message that walks the assistant through the right tools.
/// </summary>
/// <param name="Needs">Areas it cannot be done without; it is not offered while any is closed.</param>
/// <param name="Build">
/// The message, written against the switches as they are: a step that needs a
/// closed area is left out or turned into a question for the pilot, and saving
/// is offered only with changes allowed. A prompt naming a tool the assistant
/// was not given reads as a fault, and the model will try to call it anyway.
/// </param>
public sealed record McpPrompt(
    string Name,
    string Title,
    string Description,
    IReadOnlyList<McpPromptArgument> Arguments,
    IReadOnlyList<string> Needs,
    Func<IReadOnlyDictionary<string, string>, McpSettings, string> Build)
{
    public bool OpenUnder(McpSettings settings) => Needs.All(settings.Allows);

    public JsonObject Describe() => new()
    {
        ["name"] = Name,
        ["title"] = Title,
        ["description"] = Description,
        ["arguments"] = new JsonArray([.. Arguments.Select(a => (JsonNode)new JsonObject
        {
            ["name"] = a.Name,
            ["description"] = a.Description,
            ["required"] = a.Required,
        })]),
    };
}

/// <summary>
/// The prompts (1.2.52): plan a trade run, choose a ship's fit, debrief the
/// last session - the three questions the tools were most often strung
/// together to answer.
/// </summary>
/// <remarks>
/// <para>
/// A prompt here is instructions, not data. It could have run the tools
/// itself and pasted the answers in, but then a closed area or a slow feed
/// would fail inside prompts/get where the client shows nothing, and the
/// pilot would lose the call log's record of what was read. Left to the
/// assistant, every read goes through tools/call and is checked, logged and
/// refused there like any other.
/// </para>
/// <para>
/// Every one ends by telling the assistant to say what the tools could not
/// tell rather than fill it in - the rule the dashboard keeps, carried into
/// the one place it is not the app doing the talking.
/// </para>
/// </remarks>
public static class McpPrompts
{
    public static readonly IReadOnlyList<McpPrompt> All =
    [
        new("plan_trade_run", "Plan a trade run for my ship",
            "Finds the best cargo routes for the pilot's ship and budget from UEX prices, and offers to save one as a flight plan.",
            [
                new("ship", "The ship to fly; leave out to use the one the pilot is in or to be asked."),
                new("budget", "aUEC to spend, e.g. 250000; leave out for a full hold."),
                new("from", "Where to start, e.g. 'Area18'; leave out for anywhere."),
            ],
            [], TradeRun),

        new("fit_my_ship", "What should I fit on this ship",
            "Reads a ship's fit in the Garage and recommends parts slot by slot, with where each is sold.",
            [
                new("ship", "The ship; leave out to use the one the pilot is in or to be asked."),
                new("goal", "What the fit is for, e.g. 'bounty hunting', 'mining', 'faster quantum travel'."),
            ],
            [McpArea.Ships], Fit),

        new("debrief_last_session", "Debrief my last session",
            "A short debrief of the pilot's last play session from their logs: what they did, what paid, what cost them.",
            [],
            [McpArea.Sessions], Debrief),
    ];

    /// <summary>The prompts these settings offer.</summary>
    public static IEnumerable<McpPrompt> OpenUnder(McpSettings settings) => All.Where(p => p.OpenUnder(settings));

    private const string Honesty =
        "Only state what the tools returned. Where they cannot tell, say so rather than guessing.";

    private static string TradeRun(IReadOnlyDictionary<string, string> a, McpSettings s)
    {
        var text = new StringBuilder("Plan a cargo trade run for me in Star Citizen, using the Quantum Wake tools.\n\n");

        text.Append("1. ").Append(a.TryGetValue("ship", out var ship)
            ? $"I will fly the {ship}. Use ship_details for its cargo capacity in SCU."
            : s.Allows(McpArea.Ships)
                ? (s.Allows(McpArea.Sessions) ? "Use whats_happening for the ship I am in now; if I am not in one, " : "")
                  + "use garage to see the ships I fly and their cargo. If it is not clear which one, ask me before going on."
                : "Ask me which ship I am flying, then use ship_details for its cargo capacity in SCU.")
            .Append('\n');

        text.Append("2. Call trade_routes with that hold in SCU")
            .Append(a.TryGetValue("budget", out var budget) ? $", a budget of {budget} aUEC" : "")
            .Append(a.TryGetValue("from", out var from) ? $", starting from {from}" : "")
            .Append(", and safety avoid-lawless unless I say otherwise.")
            .Append(a.ContainsKey("budget") ? "\n" : " I gave no budget, so plan for a full hold and say what it costs.\n");

        text.Append("3. Pick the best one or two routes. For each give the commodity, where to buy and where to sell, ")
            .Append("the price per SCU at each end, how many SCU, the outlay, the profit, the security at each end and ")
            .Append("how old the prices are. They are the UEX community's last reports, not live: say so, and point out ")
            .Append("any more than a day old.\n");

        text.Append("4. ").Append(s.Writes
            ? "If I pick one, offer to save it with create_flight_plan: find_places first for the exact names, then a "
              + "buy stop and a sell stop, each with the commodity and the SCU. Save nothing until I say yes."
            : "Changes are not allowed in Quantum Wake, so finish with the stops to fly rather than saving a plan.")
            .Append("\n\n").Append(Honesty);
        return text.ToString();
    }

    private static string Fit(IReadOnlyDictionary<string, string> a, McpSettings s)
    {
        var named = a.TryGetValue("ship", out var ship);
        var text = new StringBuilder($"Help me decide what to fit on {(named ? $"my {ship}" : "my ship")} in Star Citizen, using the Quantum Wake tools.\n\n");

        text.Append("1. ").Append(named
            ? $"Call garage with ship \"{ship}\"."
            : (s.Allows(McpArea.Sessions) ? "whats_happening says which ship I am in now; " : "")
              + "garage with no ship lists the ships I fly. If it is not clear which one, ask me.")
            .Append(" Read its fit: every slot, what is fitted, its size and grade, and whether it is still stock.\n");

        text.Append("2. ").Append(a.TryGetValue("goal", out var goal)
            ? $"The fit is for: {goal}."
            : s.Allows(McpArea.Work)
                ? "I did not say what it is for. Look at my_contracts for what I mostly do, and tell me what you assumed."
                : "I did not say what it is for: ask me before recommending anything.")
            .Append('\n');

        text.Append("3. For the slots that matter for that, call garage_options to see what fits and where it is sold, ")
            .Append("and item_details when you need a part's numbers.\n");

        text.Append("4. Recommend changes slot by slot: what to fit, why, its size and grade, where to buy it and for how ")
            .Append("much. Say which parts are fine as they are. A number no tool gave you is not known - do not supply one.\n");

        text.Append("5. ").Append(s.Writes
            ? "Offer to save the fit as a Garage build with save_garage_build, and to put the parts on a shopping "
              + "list with create_shopping_list. Do either only when I say yes."
            : "Changes are not allowed in Quantum Wake, so finish with a plain list of what to buy and where.")
            .Append("\n\n").Append(Honesty);
        return text.ToString();
    }

    private static string Debrief(IReadOnlyDictionary<string, string> a, McpSettings s)
    {
        var text = new StringBuilder("Debrief my last Star Citizen play session from what Quantum Wake read in my logs.\n\n");
        var step = 1;

        text.Append(step++).Append(". Call recent_sessions with limit 1: when it was, how long I was in game, the ships, ")
            .Append("places and jumps. Every later step is about that session only - keep to its start and end.\n");
        text.Append(step++).Append(". Call logbook for what happened in it, and deaths_and_medical if I died.\n");
        if (s.Allows(McpArea.Work))
            text.Append(step++).Append(". Call my_contracts with days reaching back to the session's start, for how each of ")
                .Append("its contracts ended and the reputation it paid.\n");
        if (s.Allows(McpArea.Money))
            text.Append(step++).Append(". Call my_ledger with days reaching back to the session's start, for what I earned ")
                .Append("and spent in it.\n");

        text.Append("\nThen write a short debrief: what I did, what paid and what cost me, anything that went wrong, and ")
            .Append("one or two things to try next time. Kills are not in the logs any more, so do not count them. ")
            .Append(Honesty);
        return text.ToString();
    }
}
