namespace Quantumwake.WebTests;

/// <summary>
/// The Organisation tab, in each of its states.
/// </summary>
/// <remarks>
/// The rule that matters most is the quiet one: with nothing configured, no
/// request bound for an org server leaves the machine. The tab renders an
/// offer, not an error - a feature that vanishes reads as one that does not
/// exist.
/// </remarks>
public class OrgTabTests
{
    private const string Unconfigured = """
        {"configured":false,"serverAddress":null,"linked":false,"displayName":null,
         "handle":null,"linking":null,"orgs":[],"activeOrgId":null,
         "lastContactAt":null,"lastError":null}
        """;

    private static string Linked(string orgsJson, string? activeOrgId = "org1", string? lastError = null) =>
        $$"""
        {"configured":true,"serverAddress":"https://org.example","linked":true,
         "displayName":"Nekron","handle":"nekron","linking":null,
         "orgs":{{orgsJson}},"activeOrgId":{{(activeOrgId is null ? "null" : $"\"{activeOrgId}\"")}},
         "lastContactAt":"2026-08-25T14:00:00+00:00",
         "lastError":{{(lastError is null ? "null" : $"\"{lastError}\"")}}}
        """;

    [Fact]
    public void Unconfigured_shows_the_offer_and_asks_the_network_for_nothing()
    {
        var page = new Page();
        page.Serve("/api/org", Unconfigured);
        page.Do("await loadOrg();");

        Assert.False(page.Truth("__dom.node('#org-offer').hidden"));
        Assert.True(page.Truth("__dom.node('#org-members-panel').hidden"));
        Assert.True(page.Truth("__dom.node('#org-pending').hidden"));

        // The only request is the local snapshot - nothing that would make the
        // local server call out.
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/api/org/remote"));
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/api/org/members"));
    }

    [Fact]
    public void A_member_sees_the_roster_with_its_floor_stated()
    {
        var page = new Page();
        var snapshot = Linked("""[{"id":"org1","name":"Night Freight","status":"active","role":"member","modules":[]}]""");
        page.Serve("/api/org", snapshot);
        page.Serve("/api/org/remote", snapshot);
        page.Serve("/api/org/members", """
            [{"handle":"nekron","handleVerified":false,"displayName":"Nekron","role":"owner",
              "joinedAt":"2026-08-01T00:00:00+00:00"},
             {"handle":null,"handleVerified":false,"displayName":"Bob","role":"member",
              "joinedAt":"2026-08-20T00:00:00+00:00"}]
            """);
        page.Do("await loadOrg();");

        Assert.False(page.Truth("__dom.node('#org-members-panel').hidden"));
        Assert.False(page.Truth("__dom.node('#org-floor').hidden"));

        var members = page.NodeText("#org-members");
        Assert.Contains("nekron", members);
        Assert.Contains("Bob", members);
        Assert.Contains("no handle set", members);
        Assert.Contains("self-declared", members);

        // The floor's wording lives in the markup, which this harness does not
        // parse - visibility is the testable half.
        Assert.Contains("linked as Nekron", page.NodeText("#org-status-line"));
    }

    [Fact]
    public void A_pending_org_says_it_is_waiting_and_fetches_no_roster()
    {
        var page = new Page();
        var snapshot = Linked("""[{"id":"org1","name":"Hopefuls","status":"pending","role":"owner","modules":[]}]""");
        page.Serve("/api/org", snapshot);
        page.Serve("/api/org/remote", snapshot);
        page.Do("await loadOrg();");

        Assert.False(page.Truth("__dom.node('#org-pending').hidden"));
        Assert.Contains("waiting for the server admin's approval", page.NodeText("#org-pending-copy"));
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/api/org/members"));
    }

    [Fact]
    public void An_unreachable_server_is_named_with_a_time_and_nothing_stale_is_drawn()
    {
        var page = new Page();
        var snapshot = Linked(
            """[{"id":"org1","name":"Night Freight","status":"active","role":"member","modules":[]}]""",
            lastError: "Could not reach https://org.example at 14:02 - showing nothing rather than a stale guess.");
        page.Serve("/api/org", snapshot);
        page.Serve("/api/org/remote", snapshot);
        page.Do("await loadOrg();");

        Assert.False(page.Truth("__dom.node('#org-unreachable').hidden"));
        Assert.Contains("14:02", page.NodeText("#org-unreachable-copy"));
        Assert.True(page.Truth("__dom.node('#org-members-panel').hidden"));
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/api/org/members"));
    }

    [Fact]
    public void Linked_but_in_no_org_points_at_the_invite_code()
    {
        var page = new Page();
        var snapshot = Linked("[]", activeOrgId: null);
        page.Serve("/api/org", snapshot);
        page.Serve("/api/org/remote", snapshot);
        page.Do("await loadOrg();");

        Assert.False(page.Truth("__dom.node('#org-pending').hidden"));
        Assert.Contains("invite code", page.NodeText("#org-pending-copy"));
    }

    [Fact]
    public void Two_orgs_grow_a_switcher_and_switching_posts_the_choice()
    {
        var page = new Page();
        var snapshot = Linked("""
            [{"id":"org1","name":"Night Freight","status":"active","role":"member","modules":[]},
             {"id":"org2","name":"Day Shift","status":"active","role":"member","modules":[]}]
            """);
        page.Serve("/api/org", snapshot);
        page.Serve("/api/org/remote", snapshot);
        page.Serve("/api/org/members", "[]");
        page.Serve("/api/org/active", "{}");
        page.Do("await loadOrg();");

        Assert.False(page.Truth("__dom.node('#org-switch').hidden"));
        Assert.Equal(2, page.Count("__dom.node('#org-switch').options.length"));

        page.Do("""
            __switch = __dom.node('#org-switch');
            __switch.value = 'org2';
            __switch.fire('change');
            """);

        Assert.Contains("POST /api/org/active", page.Fetched());
        Assert.Contains("org2", page.BodyOf("/api/org/active"));
    }
}
