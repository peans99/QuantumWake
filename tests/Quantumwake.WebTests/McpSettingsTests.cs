namespace Quantumwake.WebTests;

/// <summary>
/// Settings → AI assistants (1.2.48): off by default, the changes switch only
/// under a yes to access, and the setup lines only once there is a door open.
/// </summary>
public sealed class McpSettingsTests
{
    private const string Off = """
        {"enabled":false,"allowChanges":false,"path":"/mcp","client":null,
         "stdio":{"command":"C:\\Games\\QuantumWake.exe","args":["--mcp-stdio"],"packaged":false},
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

    [Fact]
    public void A_prompt_in_the_log_is_named_by_its_title()
    {
        var page = new Page();
        page.Serve("/api/mcp", On(changes: false)
            .Replace("\"calls\":[]", "\"prompts\":[{\"name\":\"debrief_last_session\",\"title\":\"Debrief my last session\",\"open\":true}],"
                + "\"calls\":[{\"at\":\"2026-11-16T10:00:00Z\",\"tool\":\"debrief_last_session\",\"outcome\":\"ok\",\"wrote\":false}]"));
        page.Do("await loadMcp();");

        Assert.Contains("Debrief my last session", page.NodeText("#mcp-call-list"));
    }

    /// <summary>Claude Desktop starts the program itself, so its line is JSON with the real path, backslashes escaped.</summary>
    [Fact]
    public void Claude_Desktop_gets_a_config_naming_this_program_with_the_flag()
    {
        var page = new Page();
        page.Serve("/api/mcp", On(changes: false));
        page.Do("await loadMcp();");

        Assert.False(page.Truth("__dom.node('#mcp-desktop-block').hidden"));
        var config = page.NodeText("#mcp-desktop");
        Assert.Contains("\"mcpServers\"", config);
        Assert.Contains("\"command\": \"C:\\\\Games\\\\QuantumWake.exe\"", config);
        Assert.Contains("\"--mcp-stdio\"", config);
    }

    [Fact]
    public void A_server_without_the_relay_offers_no_Claude_Desktop_line()
    {
        var page = new Page();
        page.Serve("/api/mcp", On(changes: false).Replace("\"stdio\":{\"command\":\"C:\\\\Games\\\\QuantumWake.exe\",\"args\":[\"--mcp-stdio\"],\"packaged\":false},", ""));
        page.Do("await loadMcp();");

        Assert.True(page.Truth("__dom.node('#mcp-desktop-block').hidden"));
    }

    private static string Asking(bool enabled, bool changes, bool ships) => $$"""
        {"enabled":{{(enabled ? "true" : "false")}},"allowChanges":{{(changes ? "true" : "false")}},"path":"/mcp","client":null,"areas":[],"calls":[],
         "tools":[{"name":"trade_routes","title":"Trade routes","writes":false,"area":null,"open":true},
                  {"name":"where_to_buy","title":"Where to buy","writes":false,"area":null,"open":true},
                  {"name":"recent_sessions","title":"Recent sessions","writes":false,"area":"sessions","open":true},
                  {"name":"garage_options","title":"Parts for a slot","writes":false,"area":"ships","open":{{(ships ? "true" : "false")}}},
                  {"name":"my_items","title":"My items","writes":false,"area":"ships","open":{{(ships ? "true" : "false")}}},
                  {"name":"create_flight_plan","title":"Make a flight plan","writes":true,"area":"plans","open":{{(changes ? "true" : "false")}}}]}
        """;

    /// <summary>A guide to what to ask (1.2.57): only questions a tool on offer can answer.</summary>
    [Fact]
    public void Try_asking_offers_only_questions_whose_tool_is_on_offer()
    {
        var page = new Page();
        page.Serve("/api/mcp", Asking(enabled: true, changes: false, ships: false));
        page.Do("await loadMcp();");

        Assert.False(page.Truth("__dom.node('#mcp-try').hidden"));
        var asked = page.NodeText("#mcp-try-list");
        Assert.Contains("Plan a trade run", asked);
        Assert.Contains("Debrief my last session", asked);
        Assert.Contains("Where can I buy", asked);
        Assert.DoesNotContain("fit on my ship", asked);
        Assert.DoesNotContain("medpens", asked);
        Assert.DoesNotContain("flight plan", asked);

        page.Serve("/api/mcp", Asking(enabled: true, changes: true, ships: true));
        page.Do("await loadMcp();");
        asked = page.NodeText("#mcp-try-list");
        Assert.Contains("fit on my ship", asked);
        Assert.Contains("medpens", asked);
        Assert.Contains("then", asked);
        Assert.Contains("Save that route as a flight plan.", asked);
    }

    [Fact]
    public void Try_asking_is_hidden_while_access_is_off_and_copies_the_question()
    {
        var page = new Page();
        page.Serve("/api/mcp", Asking(enabled: false, changes: false, ships: true));
        page.Do("await loadMcp();");
        Assert.True(page.Truth("__dom.node('#mcp-try').hidden"));

        page.Serve("/api/mcp", Asking(enabled: true, changes: false, ships: true));
        page.Do("""
            await loadMcp();
            globalThis.__copied = null;
            navigator.clipboard = { writeText: async (t) => { __copied = t; } };
            const row = __dom.node('#mcp-try-list').children[0];
            await row.children[row.children.length - 1].fire('click');
            """);
        Assert.Equal("Plan a trade run for my ship with 100,000 aUEC.", page.Text("__copied"));
    }

    [Fact]
    public void An_area_title_and_its_description_are_apart()
    {
        var page = new Page();
        page.Serve("/api/mcp", On(changes: false));
        page.Do("await loadMcp();");

        Assert.Contains("Money Your ledger.", page.NodeText("#mcp-area-list"));
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
