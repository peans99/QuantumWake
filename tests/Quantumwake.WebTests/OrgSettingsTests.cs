namespace Quantumwake.WebTests;

/// <summary>
/// The Settings block that configures, links and joins - the whole doorway to
/// the org network.
/// </summary>
public class OrgSettingsTests
{
    private const string Unconfigured = """
        {"configured":false,"serverAddress":null,"linked":false,"displayName":null,
         "handle":null,"linking":null,"orgs":[],"activeOrgId":null,
         "lastContactAt":null,"lastError":null}
        """;

    private const string ConfiguredNotLinked = """
        {"configured":true,"serverAddress":"https://org.example","linked":false,
         "displayName":null,"handle":null,"linking":null,"orgs":[],"activeOrgId":null,
         "lastContactAt":null,"lastError":null}
        """;

    private const string Linking = """
        {"configured":true,"serverAddress":"https://org.example","linked":false,
         "displayName":null,"handle":null,
         "linking":{"code":"QWK7-3FXM","verifyUrl":"https://org.example/link?code=QWK7-3FXM",
                    "expiresAt":"2099-01-01T00:00:00+00:00","pollSeconds":3},
         "orgs":[],"activeOrgId":null,"lastContactAt":null,"lastError":null}
        """;

    private const string LinkedNoOrg = """
        {"configured":true,"serverAddress":"https://org.example","linked":true,
         "displayName":"Nekron","handle":"nekron","linking":null,"orgs":[],
         "activeOrgId":null,"lastContactAt":"2026-08-25T14:00:00+00:00","lastError":null}
        """;

    [Fact]
    public void Unconfigured_offers_nothing_but_the_address_field()
    {
        var page = new Page();
        page.Serve("/api/org", Unconfigured);
        page.Do("await renderOrgSettings();");

        Assert.True(page.Truth("__dom.node('#org-link-start').hidden"));
        Assert.True(page.Truth("__dom.node('#org-unlink').hidden"));
        Assert.True(page.Truth("__dom.node('#org-join-row').hidden"));
        Assert.Equal("not set up", page.NodeText("#org-settings-status"));
    }

    [Fact]
    public void A_configured_server_makes_linking_the_next_step()
    {
        var page = new Page();
        page.Serve("/api/org", ConfiguredNotLinked);
        page.Do("await renderOrgSettings();");

        Assert.False(page.Truth("__dom.node('#org-link-start').hidden"));
        Assert.Equal("not linked", page.NodeText("#org-settings-status"));
    }

    [Fact]
    public void Saving_the_address_posts_it_and_nothing_else()
    {
        var page = new Page();
        page.Serve("/api/org", ConfiguredNotLinked);
        page.Serve("/api/org/configure", ConfiguredNotLinked);
        page.Do("""
            __dom.node('#org-server').value = 'https://org.example';
            __dom.node('#org-server-save').fire('click');
            """);

        Assert.Contains("POST /api/org/configure", page.Fetched());
        Assert.Contains("https://org.example", page.BodyOf("/api/org/configure"));
    }

    [Fact]
    public void A_pending_code_is_shown_with_its_approval_link()
    {
        var page = new Page();
        page.Serve("/api/org", Linking);
        page.Do("await renderOrgSettings();");

        Assert.False(page.Truth("__dom.node('#org-linking').hidden"));
        Assert.Equal("QWK7-3FXM", page.NodeText("#org-link-code"));
        Assert.Contains("code=QWK7-3FXM", page.Text("__dom.node('#org-link-open').href"));

        // The start button hides while a code is pending - two codes at once
        // would leave the browser approving one and the app polling the other.
        Assert.True(page.Truth("__dom.node('#org-link-start').hidden"));
    }

    [Fact]
    public void The_check_button_asks_once_and_reports_a_still_pending_code()
    {
        var page = new Page();
        page.Serve("/api/org", Linking);
        page.Serve("/api/org/link/check", """{"status":"pending"}""");
        page.Do("__dom.node('#org-link-check').fire('click');");

        Assert.Contains("POST /api/org/link/check", page.Fetched());
        Assert.Contains("Not approved yet", page.NodeText("#org-settings-status"));
    }

    [Fact]
    public void Linked_shows_who_and_opens_the_join_row()
    {
        var page = new Page();
        page.Serve("/api/org", LinkedNoOrg);
        page.Do("await renderOrgSettings();");

        Assert.False(page.Truth("__dom.node('#org-unlink').hidden"));
        Assert.False(page.Truth("__dom.node('#org-join-row').hidden"));
        Assert.True(page.Truth("__dom.node('#org-link-start').hidden"));

        var status = page.NodeText("#org-settings-status");
        Assert.Contains("linked as Nekron", status);
        Assert.Contains("invite code", status);
        Assert.Contains("last heard from the server", status);
    }

    [Fact]
    public void Joining_posts_the_code_it_was_given()
    {
        var page = new Page();
        page.Serve("/api/org", LinkedNoOrg);
        page.Serve("/api/org/join", LinkedNoOrg);
        page.Do("""
            __dom.node('#org-join-code').value = 'ABCDE-FGHJK';
            __dom.node('#org-join').fire('click');
            """);

        Assert.Contains("POST /api/org/join", page.Fetched());
        Assert.Contains("ABCDE-FGHJK", page.BodyOf("/api/org/join"));
    }

    [Fact]
    public void A_refused_join_shows_the_servers_sentence()
    {
        var page = new Page();
        page.Serve("/api/org", LinkedNoOrg);
        // /api/org/join is not served: the stub answers 404 with no body, and
        // the page must still say something rather than swallow it.
        page.Do("""
            __dom.node('#org-join-code').value = 'STALE-CODE1';
            __dom.node('#org-join').fire('click');
            """);

        Assert.Contains("404", page.NodeText("#org-settings-status"));
    }

    [Fact]
    public void Unlinking_posts_and_the_block_returns_to_its_unlinked_shape()
    {
        var page = new Page();
        page.Serve("/api/org", ConfiguredNotLinked);
        page.Serve("/api/org/unlink", ConfiguredNotLinked);
        page.Do("__dom.node('#org-unlink').fire('click');");

        Assert.Contains("POST /api/org/unlink", page.Fetched());
        Assert.False(page.Truth("__dom.node('#org-link-start').hidden"));
    }

    [Fact]
    public void Rendering_settings_never_reaches_for_the_org_server()
    {
        var page = new Page();
        page.Serve("/api/org", LinkedNoOrg);
        page.Do("await renderOrgSettings();");

        // The settings block reads local state only; /api/org/remote is the
        // Org tab's call, made when entering it.
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/api/org/remote"));
    }
}
