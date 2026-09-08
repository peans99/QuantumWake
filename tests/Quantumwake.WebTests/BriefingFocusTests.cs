namespace Quantumwake.WebTests;

/// <summary>
/// The Now briefing rearranging itself around the ship in the hangar.
/// </summary>
/// <remarks>
/// Two things have to hold at once. The card has to lead with what the pilot
/// came out to do, and it has to say that it guessed — the ship is a statement
/// of intent, not a record of work, and a dashboard that silently reorders is
/// one people stop trusting. The chooser exists so the guess can be wrong
/// without being annoying.
/// </remarks>
public class BriefingFocusTests
{
    private const string Common = """
        "locationId":"Area18","location":"Area18","tripId":null,"tripTitle":null,
        "stops":[],"shopping":[],"services":[],"stash":[],
        "trade":[{"commodity":"Laranite","buyHere":25,"sellThere":31,"sellTerminal":"TDD, Lorville","marginPerScu":6}]
        """;

    private const string Hermes = """
        "focus":{"key":"freight","label":"Freight","ship":"RSI Hermes",
                 "career":"Transporter","role":"Medium Freight"}
        """;

    private const string Hornet = """
        "focus":{"key":"combat","label":"Combat","ship":"ANVL Hornet F7CM Mk2",
                 "career":"Combat","role":"Medium Fighter"}
        """;

    private const string Rocks = """
        "mining":[{"place":"Hurston","system":"Stanton","perRock":186144,"ore":72.9,
                   "best":"Hadanite","here":true}]
        """;

    private const string ClaimFee = """
        "claim":{"ship":"Anvil F7C-M Super Hornet Mk II","expeditedCost":15000,
                 "expeditedMinutes":6,"standardMinutes":30}
        """;

    private const string Freight = $$"""{ {{Common}}, {{Hermes}}, "mining":[], "claim":null }""";

    private const string Combat = $$"""{ {{Common}}, {{Hornet}}, "mining":[], {{ClaimFee}} }""";

    private const string Mining = $$"""
        { {{Common}},
          "focus":{"key":"mining","label":"Mining","ship":"MISC Prospector",
                   "career":"Industrial","role":"Light Mining"},
          {{Rocks}}, "claim":null }
        """;

    private const string MiningElsewhere = $$"""
        { {{Common}},
          "focus":{"key":"mining","label":"Mining","ship":"MISC Prospector",
                   "career":"Industrial","role":"Light Mining"},
          "mining":[{"place":"Aaron Halo","system":null,"perRock":120000,"ore":60,
                     "best":"Quantanium","here":false}],
          "claim":null }
        """;

    private const string NoFocus = $$"""{ {{Common}}, "focus":null, "mining":[], "claim":null }""";

    private static Page At(string briefing)
    {
        var page = new Page();
        page.Do("localStorage.store = {}; briefingFocus = '';");
        page.Serve("/api/briefing", briefing);
        page.Serve("/api/trips", "[]");
        page.Do("renderNow({ connected:true, inGame:true, locationId:'Area18', location:'Area18', confidence:'High', recentEvents:[] });");
        return page;
    }

    /* ---------- the order ---------- */

    /// <summary>
    /// Partial by design: a focus names only the sections that change the
    /// answer, and everything else keeps the markup's order. A section added in
    /// a later version must not be dropped by every focus written before it.
    /// </summary>
    [Fact]
    public void A_focus_leads_with_its_own_sections_and_keeps_the_rest()
    {
        var page = new Page();

        Assert.Equal(
            "trade,stops,shopping,services,stash,mining,claim",
            page.Text("briefingOrder('freight').join(',')"));

        Assert.Equal(
            "mining,stops,services,shopping,trade,stash,claim",
            page.Text("briefingOrder('mining').join(',')"));
    }

    [Fact]
    public void No_focus_leaves_the_markup_order_alone()
    {
        var page = new Page();

        Assert.Equal(
            "stops,shopping,trade,services,stash,mining,claim",
            page.Text("briefingOrder(null).join(',')"));
    }

    /* ---------- saying so ---------- */

    [Fact]
    public void The_card_says_which_ship_gave_it_the_focus()
    {
        var why = At(Freight).NodeText("#briefing-why");

        Assert.Contains("Freight", why);
        Assert.Contains("RSI Hermes", why);
        Assert.Contains("a medium freight ship", why);
    }

    /// <summary>
    /// The dataset's roles are noun phrases, and a fair few are compound:
    /// "Starter / Pathfinder" has to come out the other side as a sentence
    /// rather than as the field it came from.
    /// </summary>
    [Fact]
    public void A_compound_role_still_reads_as_english()
    {
        var page = At($$"""
            { {{Common}},
              "focus":{"key":"explore","label":"Exploration","ship":"Drake Cutter",
                       "career":"Exploration","role":"Starter / Pathfinder"},
              "mining":[], "claim":null }
            """);

        Assert.Contains("a starter or pathfinder ship", page.NodeText("#briefing-why"));
    }

    [Fact]
    public void A_ship_that_says_nothing_leaves_the_card_plain()
    {
        var page = At(NoFocus);

        Assert.True(page.Truth("__dom.node('#briefing-why').hidden"));
        Assert.True(page.Truth("__dom.node('#briefing-mining-section').hidden"));
        Assert.True(page.Truth("__dom.node('#briefing-claim-section').hidden"));
    }

    /* ---------- the mining lane ---------- */

    [Fact]
    public void A_mining_ship_is_told_where_the_rocks_are()
    {
        var page = At(Mining);

        Assert.False(page.Truth("__dom.node('#briefing-mining-section').hidden"));

        var rocks = page.NodeText("#briefing-mining");
        Assert.Contains("Hurston", rocks);
        Assert.Contains("Hadanite", rocks);
        Assert.Contains("186,144 aUEC/rock", rocks);
    }

    /// <summary>
    /// The deposit tables name a system for a body and nothing at all for the
    /// Aaron Halo, so "best here" quietly becomes "best anywhere". Which of the
    /// two questions was answered has to be on the card.
    /// </summary>
    [Fact]
    public void It_says_when_the_best_rocks_are_not_in_this_system()
    {
        Assert.Contains("best anywhere", At(MiningElsewhere).NodeText("#briefing-mining"));
        Assert.DoesNotContain("best anywhere", At(Mining).NodeText("#briefing-mining"));
    }

    /* ---------- the combat lane ---------- */

    /// <summary>
    /// The one honest number for a fighter pilot. 4.9 logs no killer and deaths
    /// are inferred, so a scoreboard would be invention; a claim fee is in the
    /// game's own tables and is what is worth knowing before undocking.
    /// </summary>
    [Fact]
    public void A_combat_ship_is_told_what_losing_it_costs()
    {
        var claim = At(Combat).NodeText("#briefing-claim");

        Assert.Contains("Anvil F7C-M Super Hornet Mk II", claim);
        Assert.Contains("15,000 aUEC", claim);
        Assert.Contains("standard ~30m", claim);
        Assert.Contains("expedited ~6m", claim);
        Assert.Contains("not one in progress", claim);
    }

    [Fact]
    public void A_freight_ship_is_not_shown_claim_fees_or_ore()
    {
        var page = At(Freight);

        Assert.True(page.Truth("__dom.node('#briefing-claim-section').hidden"));
        Assert.True(page.Truth("__dom.node('#briefing-mining-section').hidden"));
    }

    /* ---------- keeping up with the ship ---------- */

    /// <summary>
    /// The refresh fix behind 0.9.23.
    /// </summary>
    /// <remarks>
    /// Swapping ships is something you do standing still in your own hangar, so
    /// keying the card on the place alone meant the most common way to change
    /// the focus was the one way that could not refresh it. The card kept the
    /// last ship's lane until the pilot happened to fly somewhere else.
    /// </remarks>
    [Fact]
    public void Swapping_ships_without_moving_refreshes_the_focus()
    {
        var page = At(Freight);
        Assert.Contains("RSI Hermes", page.NodeText("#briefing-why"));

        page.Serve("/api/briefing", Combat);
        page.Do("renderNow({ connected:true, inGame:true, locationId:'Area18', location:'Area18', ship:'ANVL Hornet F7CM Mk2', confidence:'High', recentEvents:[] });");

        Assert.Contains("Combat", page.NodeText("#briefing-why"));
        Assert.False(page.Truth("__dom.node('#briefing-claim-section').hidden"));
    }

    /// <summary>
    /// And the card is still not refetched on every frame: the live state
    /// arrives once a second, and a briefing joins the whole stash, every
    /// shopping list and the market to the place it describes.
    /// </summary>
    [Fact]
    public void Standing_still_in_the_same_ship_does_not_refetch()
    {
        var page = At(Freight);
        var before = page.Count("__fetch.calls.filter(c => c.url.indexOf('/api/briefing') === 0).length");

        page.Do("renderNow({ connected:true, inGame:true, locationId:'Area18', location:'Area18', confidence:'High', recentEvents:[] });");

        Assert.Equal(before, page.Count("__fetch.calls.filter(c => c.url.indexOf('/api/briefing') === 0).length"));
    }

    /* ---------- overruling it ---------- */

    /// <summary>
    /// The choice goes back to the server rather than being applied here: the
    /// extras are built for whichever focus asked for them, so an override the
    /// server never heard about would open a section with nothing in it.
    /// </summary>
    [Fact]
    public void Choosing_a_focus_asks_the_server_for_that_focus()
    {
        var page = At(Combat);
        page.Serve("/api/briefing?focus=mining", $$"""{ {{Common}}, {{Hornet}}, {{Rocks}}, "claim":null }""");

        page.Do("await chooseBriefingFocus('mining');");

        Assert.Contains("Hurston", page.NodeText("#briefing-mining"));
        Assert.True(page.Truth("__dom.node('#briefing-claim-section').hidden"));
    }

    /// <summary>
    /// And it says the focus is no longer the ship's doing, so a pilot who set
    /// it once and forgot can see why the card is arranged this way.
    /// </summary>
    [Fact]
    public void An_overruled_card_says_the_choice_was_the_pilots()
    {
        var page = At(Combat);
        page.Serve("/api/briefing?focus=mining", $$"""{ {{Common}}, {{Hornet}}, {{Rocks}}, "claim":null }""");

        page.Do("await chooseBriefingFocus('mining');");

        var why = page.NodeText("#briefing-why");
        Assert.Contains("Mining", why);
        Assert.Contains("your choice", why);
        Assert.DoesNotContain("Hornet", why);
    }

    /// <summary>
    /// "Off" has to be a different answer from "not set". A pilot who wants the
    /// plain card must not have the next ship swap hand them a focus back.
    /// </summary>
    [Fact]
    public void Switching_the_focus_off_survives_a_ship_that_would_set_one()
    {
        var page = At(Combat);
        page.Serve("/api/briefing?focus=off", $$"""{ {{Common}}, {{Hornet}}, "mining":[], "claim":null }""");

        page.Do("await chooseBriefingFocus('off');");

        Assert.True(page.Truth("__dom.node('#briefing-why').hidden"));
        Assert.True(page.Truth("__dom.node('#briefing-claim-section').hidden"));
        Assert.Equal("off", page.Text("localStorage.getItem('qw-briefing-focus')"));
        Assert.Equal("null", page.Text("String(focusInForce({ focus: { key: 'combat' } }))"));
    }

    [Fact]
    public void The_choice_is_remembered()
    {
        var page = At(Freight);
        page.Serve("/api/briefing?focus=combat", Combat);

        page.Do("await chooseBriefingFocus('combat');");

        Assert.Equal("combat", page.Text("localStorage.getItem('qw-briefing-focus')"));
        Assert.Equal("combat", page.Text("focusInForce({ focus: { key: 'freight' } })"));
    }
}
