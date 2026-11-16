namespace Quantumwake.WebTests;

/// <summary>
/// Settings → AI assistants (1.2.48): off by default, the changes switch only
/// under a yes to access, and the setup lines only once there is a door open.
/// </summary>
public sealed class McpSettingsTests
{
    private const string Off = """
        {"enabled":false,"allowChanges":false,"path":"/mcp","client":null,
         "areas":[{"key":"money","title":"Money","description":"Your ledger.","defaultOn":true,"open":true,"tools":["my_ledger"]},
                  {"key":"crew","title":"People you have played with","description":"Other players.","defaultOn":false,"open":false,"tools":["my_crew"]}],
         "tools":[{"name":"find_places","title":"Find places","writes":false,"area":null,"open":true},
                  {"name":"my_ledger","title":"My ledger","writes":false,"area":"money","open":true},
                  {"name":"my_crew","title":"People I've played with","writes":false,"area":"crew","open":false},
                  {"name":"create_checklist","title":"Make a checklist","writes":true,"area":"plans","open":false}],
         "calls":[]}
        """;

    private static string On(bool changes) => Off
        .Replace("\"enabled\":false", "\"enabled\":true")
        .Replace("\"allowChanges\":false", $"\"allowChanges\":{(changes ? "true" : "false")}");

    [Fact]
    public void Off_says_so_and_offers_no_setup()
    {
        var page = new Page();
        page.Serve("/api/mcp", Off);
        page.Do("await loadMcp();");

        Assert.False(page.Truth("__dom.node('#mcp-enabled').checked"));
        Assert.True(page.Truth("__dom.node('#mcp-changes').disabled"));
        Assert.True(page.Truth("__dom.node('#mcp-setup').hidden"));
        Assert.Contains("No assistant can reach", page.NodeText("#mcp-state"));
    }

    [Fact]
    public void On_shows_how_to_connect_and_counts_only_reading_tools_while_read_only()
    {
        var page = new Page();
        page.Serve("/api/mcp", On(changes: false));
        page.Do("await loadMcp();");

        Assert.False(page.Truth("__dom.node('#mcp-setup').hidden"));
        Assert.Contains("claude mcp add --transport http quantumwake http://localhost:", page.NodeText("#mcp-claude"));
        Assert.Contains("/mcp", page.NodeText("#mcp-claude"));
        Assert.Contains("[mcp_servers.quantumwake]", page.NodeText("#mcp-codex"));
        Assert.Contains("read-only: 2 things it can ask", page.NodeText("#mcp-state"));
    }

    [Fact]
    public void The_areas_are_offered_only_with_access_on_and_crew_starts_unticked()
    {
        var page = new Page();
        page.Serve("/api/mcp", Off);
        page.Do("await loadMcp();");
        Assert.True(page.Truth("__dom.node('#mcp-areas').hidden"));

        page.Serve("/api/mcp", On(changes: false));
        page.Do("await loadMcp();");

        Assert.False(page.Truth("__dom.node('#mcp-areas').hidden"));
        Assert.Contains("People you have played with", page.NodeText("#mcp-area-list"));
        Assert.True(page.Truth(Box("money") + ".checked"));
        Assert.False(page.Truth(Box("crew") + ".checked"));
    }

    [Fact]
    public void Ticking_an_area_posts_that_area_alone()
    {
        var page = new Page();
        page.Serve("/api/mcp", On(changes: false));
        page.Do("await loadMcp();");

        page.Do("const box = " + Box("money") + "; box.checked = false; await box.fire('change', { target: box });");

        Assert.Equal("""{"areas":{"money":false}}""", page.BodyOf("/api/mcp"));
    }

    private static string Box(string area) =>
        $"__dom.node('#mcp-area-list').descendants().find(n => n.tagName === 'input' && n.dataset.area === '{area}')";

    [Fact]
    public void The_switches_post_on_their_own()
    {
        var page = new Page();
        page.Serve("/api/mcp", Off);
        page.Do("await loadMcp();");
        page.Serve("/api/mcp", On(changes: false));

        page.Do("""
            __dom.node('#mcp-enabled').checked = true;
            await __dom.node('#mcp-enabled').fire('change', { target: __dom.node('#mcp-enabled') });
            """);

        Assert.Equal("""{"enabled":true}""", page.BodyOf("/api/mcp"));
        Assert.False(page.Truth("__dom.node('#mcp-changes').disabled"));
    }

    [Fact]
    public void What_was_asked_is_listed_with_who_asked()
    {
        var page = new Page();
        page.Serve("/api/mcp", On(changes: true).Replace("\"calls\":[]",
            "\"calls\":[{\"at\":\"2026-11-15T21:14:00Z\",\"tool\":\"create_checklist\",\"outcome\":\"ok\",\"wrote\":true,\"client\":\"claude-code\"}]"));
        page.Do("await loadMcp();");

        Assert.False(page.Truth("__dom.node('#mcp-calls').hidden"));
        Assert.Contains("Make a checklist", page.NodeText("#mcp-call-list"));
        Assert.Contains("claude-code", page.NodeText("#mcp-call-list"));
    }
}
