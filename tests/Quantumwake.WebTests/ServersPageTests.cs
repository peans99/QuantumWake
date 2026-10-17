namespace Quantumwake.WebTests;

/// <summary>
/// The Servers page and the Now page's server card.
/// </summary>
/// <remarks>
/// Two things this must get right. A shard from a retired deployment cannot
/// be joined again, so it is history and must look like it - present, greyed,
/// never at the top. And the note is the point: it has to survive a round
/// trip and land on the Now card the moment the same shard comes round again.
/// </remarks>
public class ServersPageTests
{
    private const string Servers = """
        {"current":null,"newestDeployment":"12545750","servers":[
          {"shard":"pub_use1b_12545750_150","region":"US East","regionCode":"use1b","deployment":"12545750","number":"150",
           "current":true,"visits":3,"sessions":3,"time":7200,"first":"2026-09-01T00:00:00+00:00","last":"2026-09-13T00:00:00+00:00",
           "endings":{"Left":2,"LogEnded":1},"lastEnding":"LogEnded","lastSession":"a","note":"laggy elevators","favorite":false,
           "places":[{"name":"Levski","system":"Nyx","visits":1,"last":"2026-09-13T00:00:00+00:00"},{"name":"Port Tressler","system":"Stanton","visits":3,"last":"2026-09-12T00:00:00+00:00"},{"name":"Everus Harbor","system":"Stanton","visits":1,"last":"2026-09-11T00:00:00+00:00"},{"name":"Bloom LEO Rest Stop","system":"Stanton","visits":1,"last":"2026-09-10T00:00:00+00:00"}]},
          {"shard":"pub_euw1b_12545750_003","region":"EU West","regionCode":"euw1b","deployment":"12545750","number":"003",
           "current":true,"visits":1,"sessions":1,"time":600,"first":"2026-09-10T00:00:00+00:00","last":"2026-09-10T00:00:00+00:00",
           "endings":{"Left":1},"lastEnding":"Left","lastSession":"b","note":null,"favorite":true,"places":[]},
          {"shard":"pub_use1b_12326004_010","region":"US East","regionCode":"use1b","deployment":"12326004","number":"010",
           "current":false,"visits":6,"sessions":5,"time":36000,"first":"2026-07-01T00:00:00+00:00","last":"2026-08-01T00:00:00+00:00",
           "endings":{"Left":6},"lastEnding":"Left","lastSession":"c","note":"the good one","favorite":true}
        ]}
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/servers", Servers);
        page.Do("await loadServers();");
        return page;
    }

    private static string Rows(Page page) =>
        page.Text("__dom.node('#servers-table').querySelector('tbody').children.map(tr => tr.dataset.shard || '').join('|')");

    [Fact]
    public void Retired_deployments_are_hidden_by_default_and_a_tick_away()
    {
        var page = Loaded();

        Assert.Equal("pub_euw1b_12545750_003|pub_use1b_12545750_150", Rows(page));

        page.Do("__dom.node('#servers-current').checked = false; renderServers();");

        // Favourites first, then newest - the retired favourite comes second
        // because it is older, not because it is retired.
        Assert.Equal("pub_euw1b_12545750_003|pub_use1b_12326004_010|pub_use1b_12545750_150", Rows(page));
        Assert.Contains("retired", page.NodeText("#servers-table"));
    }

    [Fact]
    public void A_retired_shard_is_marked_gone()
    {
        var page = Loaded();
        page.Do("__dom.node('#servers-current').checked = false; renderServers();");

        var classes = page.Text(
            "__dom.node('#servers-table').querySelector('tbody').children.map(tr => tr.className).join('|')");

        Assert.Equal("|gone|", classes);
    }

    [Fact]
    public void The_last_ending_leads_and_the_tally_follows()
    {
        var page = Loaded();
        var text = page.NodeText("#servers-table");

        Assert.Contains("log ended", text);
        Assert.Contains("2× you left", text);
        Assert.Contains("1× log ended", text);
    }

    [Fact]
    public void Favourites_only_and_region_narrow_the_list()
    {
        var page = Loaded();

        page.Do("__dom.node('#servers-starred').checked = true; renderServers();");
        Assert.Equal("pub_euw1b_12545750_003", Rows(page));

        page.Do("__dom.node('#servers-starred').checked = false; __dom.node('#servers-region').value = 'US East'; renderServers();");
        Assert.Equal("pub_use1b_12545750_150", Rows(page));
    }

    [Fact]
    public void Search_reads_the_note_as_well_as_the_name()
    {
        var page = Loaded();
        page.Do("__dom.node('#servers-current').checked = false; __dom.node('#servers-search').value = 'good one'; renderServers();");

        Assert.Equal("pub_use1b_12326004_010", Rows(page));
    }

    [Fact]
    public void The_game_label_is_searchable_and_a_personal_marker_is_saved()
    {
        var page = new Page();
        page.Serve("/api/servers", Servers.Replace("\"note\":\"laggy elevators\",\"favorite\":false",
            "\"note\":\"laggy elevators\",\"favorite\":false,\"seenName\":\"amazing_view\",\"disposition\":\"avoid\""));
        page.Serve("/api/servers/pub_use1b_12545750_150/disposition",
            """{"shard":"pub_use1b_12545750_150","note":"laggy elevators","favorite":false,"seenName":"amazing_view","disposition":"good"}""");
        page.Do("await loadServers(); __dom.node('#servers-search').value = 'amazing_view'; renderServers();");

        Assert.Equal("pub_use1b_12545750_150", Rows(page));
        Assert.Contains("Seen in game: amazing_view", page.NodeText("#servers-table"));

        page.Do("""
            const marker = __dom.node('#servers-table').querySelector('tbody').children[0].descendants().find(n => n.tagName === 'select');
            marker.value = 'good';
            await marker.fire('change');
            """);

        Assert.Contains("PUT /api/servers/pub_use1b_12545750_150/disposition", page.Fetched());
        Assert.Contains("\"disposition\":\"good\"", page.BodyOf("/api/servers/pub_use1b_12545750_150/disposition"));
    }

    [Fact]
    public void Back_end_filter_and_summary_use_only_explicit_back_end_endings()
    {
        var page = new Page();
        page.Serve("/api/servers", Servers.Replace("\"endings\":{\"Left\":2,\"LogEnded\":1}",
            "\"endings\":{\"Left\":2,\"Backend\":1}"));
        page.Do("await loadServers(); __dom.node('#servers-troubled').checked = true; renderServers();");

        Assert.Equal("pub_use1b_12545750_150", Rows(page));
        Assert.Contains("Back-end ends", page.NodeText("#servers-summary"));
    }

    [Fact]
    public void A_seen_name_is_edited_in_place_and_the_report_uses_no_log_contents()
    {
        var page = Loaded();
        page.Serve("/api/servers/pub_euw1b_12545750_003/seen-name",
            """{"shard":"pub_euw1b_12545750_003","note":null,"favorite":true,"seenName":"amazing_view","disposition":null}""");

        page.Do("""
            const label = __dom.node('#servers-table').querySelector('tbody').children[0].descendants().find(n => n.classList.contains('seen-name'));
            label.fire('click');
            const box = __dom.node('#servers-table').querySelector('tbody').children[0].descendants().find(n => n.tagName === 'input');
            box.value = 'amazing_view';
            await box.fire('blur');
            """);

        Assert.Contains("\"name\":\"amazing_view\"", page.BodyOf("/api/servers/pub_euw1b_12545750_003/seen-name"));
        var report = page.Text("serverReport(serverRows.find(r => r.shard === 'pub_use1b_12545750_150'))");
        Assert.Contains("pub_use1b_12545750_150", report);
        Assert.Contains("US East", report);
        Assert.DoesNotContain("laggy elevators", report);
    }

    [Fact]
    public void The_summary_counts_what_is_still_running_apart_from_what_was()
    {
        var text = Loaded().NodeText("#servers-summary");

        Assert.Contains("Shards visited", text);
        Assert.Contains("3", text);
        Assert.Contains("Still running", text);
        Assert.Contains("12545750", text);
    }

    /// <summary>The star is a PUT, and the answer redraws the row without a reload.</summary>
    [Fact]
    public void Starring_a_shard_writes_it_and_redraws()
    {
        var page = Loaded();
        page.Serve("/api/servers/pub_use1b_12545750_150/favorite",
            """{"shard":"pub_use1b_12545750_150","note":"laggy elevators","favorite":true}""");

        page.Do("await __dom.node('#servers-table').querySelector('tbody').children[1].descendants().find(n => n.classList.contains('star')).fire('click');");

        Assert.Contains("PUT /api/servers/pub_use1b_12545750_150/favorite", page.Fetched());
        Assert.Contains("\"favorite\":true", page.BodyOf("/api/servers/pub_use1b_12545750_150/favorite"));

        // Now a favourite, it sorts to the front.
        Assert.StartsWith("pub_use1b_12545750_150|", Rows(page));
    }

    [Fact]
    public void A_note_is_edited_in_place_and_saved_on_blur()
    {
        var page = Loaded();
        page.Serve("/api/servers/pub_euw1b_12545750_003/note",
            """{"shard":"pub_euw1b_12545750_003","note":"two 30ks in an hour","favorite":true}""");

        page.Do("""
            const cell = __dom.node('#servers-table').querySelector('tbody').children[0].descendants().find(n => n.classList.contains('note-text'));
            cell.fire('click');
            const box = __dom.node('#servers-table').querySelector('tbody').children[0].descendants().find(n => n.tagName === 'textarea');
            box.value = 'two 30ks in an hour';
            await box.fire('blur');
            """);

        Assert.Contains("\"note\":\"two 30ks in an hour\"", page.BodyOf("/api/servers/pub_euw1b_12545750_003/note"));
        Assert.Contains("two 30ks in an hour", page.NodeText("#servers-table"));
    }

    /// <summary>A note restored from another machine names a shard these logs never joined; it is shown, with nothing counted against it.</summary>
    [Fact]
    public void A_note_without_visits_is_still_listed()
    {
        var page = new Page();
        page.Serve("/api/servers", """
            {"current":null,"newestDeployment":null,"servers":[
              {"shard":"pub_use1b_11111111_001","region":"US East","regionCode":"use1b","deployment":"11111111","number":"001",
               "current":false,"visits":0,"sessions":0,"time":0,"first":"2026-09-01T00:00:00+00:00","last":"2026-09-01T00:00:00+00:00",
               "endings":{},"lastEnding":"LogEnded","lastSession":null,"note":"from the other pc","favorite":false}]}
            """);
        page.Do("await loadServers(); __dom.node('#servers-current').checked = false; renderServers();");

        var text = page.NodeText("#servers-table");
        Assert.Contains("from the other pc", text);
        Assert.Contains("never seen here", text);
        Assert.DoesNotContain("log ended", text);
    }

    // ---- the Now card ----

    private static Page Now(string extra)
    {
        var page = new Page();
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");
        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[], {extra} }});");
        return page;
    }

    [Fact]
    public void The_card_stays_hidden_between_placements()
    {
        var page = Now("shard:null");
        Assert.True(page.Truth("__dom.node('#now-server-card').hidden"));
    }

    [Fact]
    public void The_card_names_the_shard_and_repeats_the_note()
    {
        var page = Now("shard:'pub_use1b_12545750_150', shardShort:'US East 150', shardNote:'laggy elevators', shardFavorite:true, shardVisitsBefore:2");

        Assert.False(page.Truth("__dom.node('#now-server-card').hidden"));
        Assert.Equal("US East 150", page.NodeText("#now-server"));
        Assert.Equal("pub_use1b_12545750_150", page.NodeText("#now-server-name"));

        var note = page.NodeText("#now-server-note");
        Assert.Contains("2 times before", note);
        Assert.Contains("laggy elevators", note);
        Assert.StartsWith("★", page.NodeText("#now-server-star"));
    }

    [Fact]
    public void The_card_prefers_the_name_seen_in_game_but_keeps_the_canonical_id()
    {
        var page = Now("shard:'pub_use1b_12545750_150', shardShort:'US East 150', shardSeenName:'amazing_view', shardDisposition:'avoid'");

        Assert.Equal("amazing_view", page.NodeText("#now-server"));
        Assert.Equal("pub_use1b_12545750_150", page.NodeText("#now-server-name"));
        Assert.Contains("marked this shard avoid", page.NodeText("#now-server-note"));
    }

    [Fact]
    public void A_first_visit_says_so_rather_than_showing_zero()
    {
        var page = Now("shard:'pub_use1b_12545750_150', shardShort:'US East 150', shardVisitsBefore:0");

        Assert.Contains("First time on this shard", page.NodeText("#now-server-note"));
        Assert.StartsWith("☆", page.NodeText("#now-server-star"));
    }

    // ---- where you went ----

    [Fact]
    public void Places_show_the_latest_and_unfold_the_rest_on_request()
    {
        var page = Loaded();
        var cell = "__dom.node('#servers-table').querySelector('tbody').children[1].descendants().find(n => n.classList.contains('places'))";

        // Latest first - Levski was yesterday - with the other three behind a button.
        Assert.Equal("Levski", page.Text($"{cell}.descendants().find(n => n.classList.contains('place')).textContent"));
        Assert.Equal("+3 more", page.Text($"{cell}.descendants().find(n => n.classList.contains('more')).textContent"));
        Assert.True(page.Truth($"{cell}.descendants().find(n => n.classList.contains('place-list')).hidden"));

        page.Do($"{cell}.descendants().find(n => n.classList.contains('more')).fire('click');");

        Assert.False(page.Truth($"{cell}.descendants().find(n => n.classList.contains('place-list')).hidden"));
        var list = page.Text($"{cell}.descendants().find(n => n.classList.contains('place-list')).textContent");
        Assert.Contains("Port Tressler ×3", list);
        Assert.Contains("Bloom LEO Rest Stop", list);
        Assert.Equal("fewer", page.Text($"{cell}.descendants().find(n => n.classList.contains('more')).textContent"));
    }

    /// <summary>A stay with no arrival says so, rather than showing a blank that could mean "not read".</summary>
    [Fact]
    public void A_shard_with_no_arrivals_says_nowhere_named()
    {
        var page = Loaded();
        var cell = page.Text("__dom.node('#servers-table').querySelector('tbody').children[0].descendants().find(n => n.classList.contains('places')).textContent");

        Assert.Equal("nowhere named", cell);
    }

    [Fact]
    public void Search_finds_a_shard_by_a_place_visited_on_it()
    {
        var page = Loaded();
        page.Do("__dom.node('#servers-search').value = 'levski'; renderServers();");

        Assert.Equal("pub_use1b_12545750_150", Rows(page));
    }

    // ---- the shard you are on ----

    /// <summary>The live shard leads the table and is marked, whatever the sort would otherwise say.</summary>
    [Fact]
    public void The_current_shard_leads_and_is_lit()
    {
        var page = new Page();
        page.Serve("/api/servers", Servers.Replace("\"current\":null", "\"current\":\"pub_use1b_12545750_150\""));
        page.Do("await loadServers();");

        // Not a favourite, so it would otherwise sort second - but it is where the client is.
        Assert.StartsWith("pub_use1b_12545750_150|", Rows(page));

        var first = "__dom.node('#servers-table').querySelector('tbody').children[0]";
        Assert.True(page.Truth($"{first}.classList.contains('here')"));
        Assert.Contains("on now", page.Text($"{first}.textContent"));
        Assert.Contains("pub_use1b_12545750_150", page.NodeText("#servers-summary"));
    }

    /// <summary>A placement arriving over the live stream moves the light without a reload.</summary>
    [Fact]
    public void A_new_placement_moves_the_light()
    {
        var page = Loaded();
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");

        page.Do("renderNow({ connected:true, inGame:true, confidence:'None', recentEvents:[], shard:'pub_euw1b_12545750_003' });");

        var first = "__dom.node('#servers-table').querySelector('tbody').children[0]";
        Assert.Equal("pub_euw1b_12545750_003", page.Text($"{first}.dataset.shard"));
        Assert.True(page.Truth($"{first}.classList.contains('here')"));
        Assert.False(page.Truth("__dom.node('#servers-table').querySelector('tbody').children[1].classList.contains('here')"));
    }

    /// <summary>The shard joined this session, absent from every stored one, still gets a row - and it says you are on it.</summary>
    [Fact]
    public void A_shard_first_seen_this_session_reads_as_on_it_now()
    {
        var page = new Page();
        page.Serve("/api/servers", """
            {"current":"pub_use1b_12545750_199","newestDeployment":"12545750","servers":[
              {"shard":"pub_use1b_12545750_199","region":"US East","regionCode":"use1b","deployment":"12545750","number":"199",
               "current":true,"visits":2,"sessions":1,"time":1800,"first":"2026-09-14T14:00:00+00:00","last":"2026-09-14T14:30:00+00:00",
               "endings":{"Left":1,"Open":1},"lastEnding":"Open","lastSession":"Game","note":null,"favorite":false,"places":[]}]}
            """);
        page.Do("await loadServers();");

        var row = "__dom.node('#servers-table').querySelector('tbody').children[0]";
        Assert.True(page.Truth($"{row}.classList.contains('here')"));

        var ending = page.Text($"{row}.descendants().find(n => n.classList.contains('ending')).textContent");
        Assert.StartsWith("on it now", ending);
        Assert.DoesNotContain("log ended", ending);
        Assert.DoesNotContain("on it now,", ending);
        Assert.Contains("1× you left", ending);
    }

    /// <summary>A note-only row has no visit; the pasted line must not invent one the way the table refuses to.</summary>
    [Fact]
    public void The_report_for_a_shard_never_joined_here_says_so()
    {
        var page = new Page();
        page.Serve("/api/servers", """
            {"current":null,"newestDeployment":null,"servers":[
              {"shard":"pub_use1b_11111111_001","region":"US East","regionCode":"use1b","deployment":"11111111","number":"001",
               "current":false,"visits":0,"sessions":0,"time":0,"first":"2026-09-01T00:00:00+00:00","last":"2026-09-01T00:00:00+00:00",
               "endings":{},"lastEnding":"LogEnded","lastSession":null,"note":"from the other pc","favorite":false,"places":[],"lastDuration":0}]}
            """);
        page.Do("await loadServers();");

        var report = page.Text("serverReport(serverRows[0])");
        Assert.Contains("no visit recorded on this machine", report);
        Assert.DoesNotContain("log ended", report);
        Assert.DoesNotContain("2026", report);
    }
}
