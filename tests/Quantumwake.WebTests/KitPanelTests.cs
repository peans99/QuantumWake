namespace Quantumwake.WebTests;

/// <summary>
/// The saved-kits panel, and the one thing it must not say.
/// </summary>
/// <remarks>
/// The server refuses to treat a sighting as a holding. The page can undo that
/// in two ways that would both look fine: by wording a sighting as a fact, and
/// by sending an unanswered line as "gone" so it lands on a shopping list
/// nobody asked for. Both are tested here, because neither shows up as a
/// broken-looking screen.
/// </remarks>
public class KitPanelTests
{
    private const string Prepared = """
        {"kitId":"k1","name":"Bounty kit",
         "rule":"The game logs an item being seen in a container, never one being taken out or used up.",
         "lines":[
          {"name":"Pembroke helmet","quantity":1,"optional":false,"holding":"Equipped",
           "where":"on you","lastSeen":"2026-09-06T10:00:00+00:00","needsAsking":false},
          {"name":"MedPen","quantity":4,"optional":false,"holding":"Seen",
           "where":"Port Tressler","lastSeen":"2026-09-04T10:00:00+00:00","needsAsking":true},
          {"name":"Multi-tool","quantity":1,"optional":false,"holding":"Stale",
           "where":"Everus Harbor","lastSeen":"2026-06-01T10:00:00+00:00","needsAsking":true},
          {"name":"Railgun","quantity":1,"optional":true,"holding":"Missing",
           "where":null,"lastSeen":null,"needsAsking":false}]}
        """;

    private static Page Preparing()
    {
        var page = new Page();
        page.Serve("/api/kits", """[{"id":"k1","name":"Bounty kit","items":[{"name":"MedPen","quantity":4}]}]""");
        page.Serve("/api/kits/k1/prepare", Prepared);
        page.Do("await loadKits(); await prepareKit('k1');");
        return page;
    }

    [Fact]
    public void A_saved_kit_is_listed_with_what_it_holds()
    {
        var page = new Page();
        page.Serve("/api/kits", """[{"id":"k1","name":"Bounty kit","items":[{"name":"MedPen","quantity":4}]}]""");
        page.Do("await loadKits();");

        var text = page.NodeText("#kit-list");

        Assert.Contains("Bounty kit", text);
        Assert.Contains("1 item", text);
    }

    [Fact]
    public void With_no_kits_it_says_how_to_make_one()
    {
        var page = new Page();
        page.Serve("/api/kits", "[]");
        page.Do("await loadKits();");

        Assert.Contains("Save what you are wearing", page.NodeText("#kit-list"));
    }

    /// <summary>
    /// The rule is on the page rather than in the code's head. Without it the
    /// questions look like fussiness instead of the only honest option.
    /// </summary>
    [Fact]
    public void Preparing_states_why_it_has_to_ask()
    {
        Assert.Contains("never one being taken out", Preparing().NodeText("#kit-prepare-rule"));
    }

    /// <summary>
    /// A sighting must never be worded as a fact. "In your stash" is the claim
    /// the whole feature exists to avoid making.
    /// </summary>
    [Fact]
    public void A_sighting_is_worded_as_a_sighting()
    {
        var text = Preparing().NodeText("#kit-prepare-table");

        Assert.Contains("Last seen in storage — Port Tressler", text);
        Assert.Contains("Not seen for a while — Everus Harbor", text);
        Assert.Contains("You are wearing it", text);
        Assert.Contains("Never seen anywhere", text);
    }

    /// <summary>
    /// Only the uncertain lines get a control. Asking about something the game
    /// actually reported invites an answer the app should not take, and asking
    /// about "never seen" asks about nothing.
    /// </summary>
    [Fact]
    public void Only_the_lines_that_need_asking_can_be_answered()
    {
        var page = Preparing();

        Assert.Equal(2, page.Count("__dom.node('#kit-prepare-table').querySelectorAll('button').length"));
        Assert.Contains("settled", page.NodeText("#kit-prepare-table"));
        Assert.Contains("on the list", page.NodeText("#kit-prepare-table"));
    }

    /// <summary>
    /// Silence means held. Sending an unanswered sighting as gone would put
    /// something on a shopping list nobody asked for.
    /// </summary>
    [Fact]
    public void An_unanswered_sighting_is_not_sent_as_gone()
    {
        var page = Preparing();
        page.Serve("/api/kits/k1/shopping", """{"job":"j1","items":1}""");
        page.Do("await makeKitShopping();");

        var call = Assert.Single(page.Fetched(), u => u.Contains("/shopping"));

        Assert.DoesNotContain("gone=", call);
    }

    [Fact]
    public void Saying_something_is_gone_sends_it()
    {
        var page = Preparing();
        page.Serve("/api/kits/k1/shopping?gone=MedPen", """{"job":"j1","items":2}""");
        page.Do("__dom.node('#kit-prepare-table').querySelectorAll('button')[0].click(); await makeKitShopping();");

        Assert.Contains(page.Fetched(), u => u.Contains("gone=MedPen"));
    }

    /// <summary>
    /// The answer is a toggle, so somebody who taps the wrong row can take it
    /// back before the list is made.
    /// </summary>
    [Fact]
    public void An_answer_can_be_taken_back()
    {
        var page = Preparing();
        page.Serve("/api/kits/k1/shopping", """{"job":null,"items":0}""");
        page.Do("const b = __dom.node('#kit-prepare-table').querySelectorAll('button')[0];"
            + " b.click(); b.click(); await makeKitShopping();");

        Assert.DoesNotContain(page.Fetched(), u => u.Contains("gone="));
    }

    [Fact]
    public void A_kit_with_nothing_to_buy_says_so_rather_than_making_an_empty_list()
    {
        var page = Preparing();
        page.Serve("/api/kits/k1/shopping", """{"job":null,"items":0}""");
        page.Do("await makeKitShopping();");

        Assert.Contains("Nothing to buy", page.NodeText("#kit-status"));
        Assert.True(page.Truth("__dom.node('#kit-prepare').hidden"));
    }

    /// <summary>
    /// A known count is stronger than either a stash sighting or an absence.
    /// The page must retain both halves: one medpen is on the character, and
    /// three more are needed for this kit.
    /// </summary>
    [Fact]
    public void A_known_shortfall_says_what_is_held_and_what_is_still_needed()
    {
        const string shortfall = """
            {"kitId":"k1","name":"Bounty kit","rule":"rule","lines":[
              {"name":"MedPen","quantity":4,"optional":false,"holding":"Missing",
               "where":"on you","lastSeen":"2026-09-06T10:00:00+00:00","needsAsking":false,
               "held":1,"short":3}]}
            """;

        var page = new Page();
        page.Serve("/api/kits/k1/prepare", shortfall);
        page.Do("await prepareKit('k1');");

        var text = page.NodeText("#kit-prepare-table");

        Assert.Contains("1 on you · 3 still needed", text);
        Assert.DoesNotContain("Never seen anywhere", text);
        Assert.Contains("on the list", text);
    }
}
