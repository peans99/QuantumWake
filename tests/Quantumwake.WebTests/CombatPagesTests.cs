using Jint;

namespace Quantumwake.WebTests;

/// <summary>
/// The target panel read live, on the Now page's Target card and the MFD's
/// Combat pages, and the voice switch under the live one.
/// </summary>
public class CombatPagesTests
{
    private const string Hostile = """
        {"available":true,"unavailable":null,"asked":true,"enabled":true,"answeredAt":null,"ask":false,
         "voice":false,"canSpeak":true,
         "status":{"state":"reading","frames":40,"kept":0,"lastReadMs":120,
           "combat":{"combat":true,"locks":3,"hostileLocks":2,
             "target":{"name":null,"candidates":[{"name":"Anvil Paladin","chance":0.85,"because":["signature","seen nearby"]},{"name":"MISC Starfarer Gemini","chance":0.15,"because":["signature, just outside"]}],"faction":"Outlaws",
               "stance":"hostile","speedMs":109,"rangeKm":2.43,"em":9100,"ir":39400,"crossSection":28500,
               "disabled":false,"since":"2026-11-05T21:30:00Z","seenAt":"2026-11-05T21:30:20Z"},
             "callouts":[{"at":"2026-11-05T21:30:05Z","kind":"hostile","text":"Hostile Anvil Paladin"},
                         {"at":"2026-11-05T21:30:02Z","kind":"likely","text":"Unknown contact, signature matches Anvil Paladin"}]}}}
        """;

    private static Page Loaded(string json)
    {
        var page = new Page();
        page.Serve("/api/live", json);
        page.Do("await loadLiveScreen();");
        return page;
    }

    // ---- the Now page ----

    [Fact]
    public void An_unknown_hostile_is_named_by_its_signature_and_marked()
    {
        var page = Loaded(Hostile);

        Assert.False(page.Truth("__dom.node('#now-target-card').hidden"));
        Assert.Equal("Unknown · likely Anvil Paladin (85%)", page.NodeText("#now-target"));
        Assert.Contains("Rough odds: Anvil Paladin 85% · MISC Starfarer Gemini 15%", page.NodeText("#now-target-odds"));
        Assert.Contains("Hostile", page.NodeText("#now-target-detail"));
        Assert.Contains("109 m/s", page.NodeText("#now-target-detail"));
        Assert.Contains("CS 28.5K", page.NodeText("#now-target-detail"));
        Assert.Contains("in combat", page.NodeText("#now-target-label"));
        Assert.True(page.Truth("__dom.node('#now-target-card').classList.contains('target-hostile')"));
        Assert.Contains("Hostile Anvil Paladin", page.NodeText("#now-target-calls"));
    }

    [Fact]
    public void Spread_odds_leave_the_title_unknown_and_list_them()
    {
        var page = Loaded(Hostile
            .Replace("\"chance\":0.85", "\"chance\":0.45")
            .Replace("\"chance\":0.15", "\"chance\":0.55"));

        Assert.Equal("Unknown", page.NodeText("#now-target"));
        Assert.Contains("Anvil Paladin 45%", page.NodeText("#now-target-odds"));
    }

    [Fact]
    public void A_target_that_stopped_after_moving_says_so_instead_of_zero()
    {
        var page = Loaded(Hostile.Replace("\"disabled\":false", "\"disabled\":true").Replace("\"speedMs\":109", "\"speedMs\":0"));

        Assert.Contains("stopped after moving", page.NodeText("#now-target-detail"));
        Assert.DoesNotContain("0 m/s", page.NodeText("#now-target-detail"));
    }

    [Fact]
    public void With_live_reading_off_the_card_stays_hidden()
    {
        var page = Loaded(Hostile.Replace("\"enabled\":true", "\"enabled\":false"));
        Assert.True(page.Truth("__dom.node('#now-target-card').hidden"));
    }

    // ---- the voice ----

    [Fact]
    public void The_voice_switch_shows_only_under_a_yes_on_a_copy_that_can_speak()
    {
        Assert.False(Loaded(Hostile).Truth("__dom.node('#live-voice-row').hidden"));
        Assert.True(Loaded(Hostile.Replace("\"canSpeak\":true", "\"canSpeak\":false")).Truth("__dom.node('#live-voice-row').hidden"));
        Assert.True(Loaded(Hostile.Replace("\"enabled\":true", "\"enabled\":false")).Truth("__dom.node('#live-voice-row').hidden"));
    }

    [Fact]
    public void Turning_the_voice_on_posts_it_on_its_own_endpoint()
    {
        var page = Loaded(Hostile);
        page.Serve("/api/live/voice", Hostile.Replace("\"voice\":false", "\"voice\":true"));
        page.Do("""
            const box = __dom.node('#live-voice');
            box.checked = true;
            box.fire('change');
            await new Promise(r => setTimeout(r, 0));
            """);

        Assert.Contains("\"enabled\":true", page.BodyOf("/api/live/voice"));
    }

    [Fact]
    public void The_kind_switches_show_under_a_voice_that_is_on()
    {
        Assert.True(Loaded(Hostile).Truth("__dom.node('#live-voice-kinds').hidden"));

        var on = Loaded(Hostile.Replace("\"voice\":false", "\"voice\":true,\"voiceCombat\":true,\"voiceMining\":false"));
        Assert.False(on.Truth("__dom.node('#live-voice-kinds').hidden"));
        Assert.True(on.Truth("__dom.node('#live-voice-combat').checked"));
        Assert.False(on.Truth("__dom.node('#live-voice-mining').checked"));

        // An older server's answer has neither: it spoke both.
        var older = Loaded(Hostile.Replace("\"voice\":false", "\"voice\":true"));
        Assert.True(older.Truth("__dom.node('#live-voice-mining').checked"));
    }

    [Fact]
    public void Quieting_mining_posts_that_kind_alone_and_keeps_the_voice_on()
    {
        var voiced = Hostile.Replace("\"voice\":false", "\"voice\":true");
        var page = Loaded(voiced);
        page.Serve("/api/live/voice", voiced.Replace("\"voice\":true", "\"voice\":true,\"voiceMining\":false"));
        page.Do("""
            const box = __dom.node('#live-voice-mining');
            box.checked = false;
            box.fire('change');
            await new Promise(r => setTimeout(r, 0));
            """);

        var body = page.BodyOf("/api/live/voice");
        Assert.Contains("\"enabled\":true", body);
        Assert.Contains("\"mining\":false", body);
        Assert.DoesNotContain("combat", body);
    }

    [Fact]
    public void The_card_says_how_the_hull_looks_never_a_figure()
    {
        var page = Loaded(Hostile.Replace("\"disabled\":false", "\"disabled\":false,\"hull\":\"critical\""));
        Assert.Contains("hull looks critical", page.NodeText("#now-target-detail"));
        Assert.DoesNotContain("hull looks", Loaded(Hostile).NodeText("#now-target-detail"));
    }

    [Fact]
    public void A_voice_that_could_not_speak_says_why()
    {
        var silent = Loaded(Hostile.Replace("\"voice\":false", "\"voice\":true,\"voiceTrouble\":\"the last callout could not be played (NoAudioDevice)\""));
        Assert.False(silent.Truth("__dom.node('#live-voice-trouble').hidden"));
        Assert.Contains("NoAudioDevice", silent.NodeText("#live-voice-trouble"));

        Assert.True(Loaded(Hostile.Replace("\"voice\":false", "\"voice\":true")).Truth("__dom.node('#live-voice-trouble').hidden"));
    }

    // ---- the MFD ----

    private static Engine Mfd()
    {
        var engine = new Engine();
        engine.Execute("var window = globalThis;");
        engine.Execute(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd-core.js")));
        engine.Execute($"var live = {Hostile};");
        return engine;
    }

    [Fact]
    public void The_target_page_reads_the_lock_row_by_row()
    {
        var json = Mfd().Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('target'),{},null,false,{extra:{live}}))").AsString();

        Assert.Contains("TARGET · IN COMBAT", json);
        Assert.Contains("Unknown · likely Anvil Paladin (85%)", json);
        Assert.Contains("ROUGH ODDS", json);
        Assert.Contains("HOSTILE · Outlaws", json);
        Assert.Contains("109 m/s · 2.43 km", json);
        Assert.Contains("CS 28.5K", json);
        Assert.DoesNotContain("HULL", json);
    }

    [Fact]
    public void The_target_page_gives_the_holograms_look_as_a_band()
    {
        var json = Mfd().Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('target'),{},null,false,{extra:{live:{...live,status:{combat:{...live.status.combat,target:{...live.status.combat.target,hull:'damaged'}}}}}}))").AsString();
        Assert.Contains("HULL · LOOKS", json);
        Assert.Contains("DAMAGED", json);
    }

    [Fact]
    public void The_target_page_says_why_it_is_empty()
    {
        var e = Mfd();
        Assert.Contains("Live reading is off",
            e.Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('target'),{},null,false,{extra:{live:{...live,enabled:false}}}))").AsString());
        Assert.Contains("Only the Windows app",
            e.Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('target'),{},null,false,{extra:{live:{...live,available:false}}}))").AsString());
        Assert.Contains("3 locks this session, 2 hostile",
            e.Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('target'),{},null,false,{extra:{live:{...live,status:{combat:{...live.status.combat,target:null}}}}}))").AsString());
    }

    [Fact]
    public void The_callouts_page_lists_what_was_said_newest_first()
    {
        var json = Mfd().Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('calls'),{},null,false,{extra:{live}}))").AsString();

        Assert.True(json.IndexOf("Hostile Anvil Paladin", StringComparison.Ordinal)
            < json.IndexOf("Unknown contact", StringComparison.Ordinal));
        Assert.Contains("HOSTILE", json);
    }

    [Fact]
    public void The_target_page_draws_the_hull_as_the_garage_does()
    {
        var e = Mfd();
        // Unknown, and no hull ahead on the odds: the plain mark, never a
        // picture of the screen, which reloaded on every read.
        Assert.Equal("mark", e.Evaluate("QwMfd.targetPicture(live).kind").AsString());
        Assert.Equal("Hull not identified yet", e.Evaluate("QwMfd.targetPicture(live).note").AsString());
        Assert.DoesNotContain("hologram", e.Evaluate("JSON.stringify(QwMfd.targetPicture(live))").AsString());

        // The picture is the same object on the next read of the same lock -
        // nothing in it carries the read's time.
        e.Execute("var later = {...live, status:{combat:{...live.status.combat, target:{...live.status.combat.target, seenAt:'2099-01-01T00:00:00Z'}}}};");
        Assert.True(e.Evaluate("JSON.stringify(QwMfd.targetPicture(live)) === JSON.stringify(QwMfd.targetPicture(later))").AsBoolean());

        // A likely hull: pictured, marked as a guess, red while hostile.
        e.Execute("var likely = {...live, status:{combat:{...live.status.combat, target:{...live.status.combat.target, class:'ANVL_Paladin', likely:true}}}};");
        Assert.Equal("silhouette", e.Evaluate("QwMfd.targetPicture(likely).kind").AsString());
        Assert.Equal("ANVL_Paladin", e.Evaluate("QwMfd.targetPicture(likely).cls").AsString());
        Assert.Equal("/api/fleet/icons/ANVL_Paladin", e.Evaluate("QwMfd.targetPicture(likely).src").AsString());
        Assert.True(e.Evaluate("QwMfd.targetPicture(likely).likely").AsBoolean());
        Assert.Equal("hostile", e.Evaluate("QwMfd.targetPicture(likely).tone").AsString());
        Assert.Equal("/api/fleet/paints/ANVL_Paladin", e.Evaluate("QwMfd.targetPicture(likely).paints").AsString());

        // The order is the Garage's: its render first - the paint picked
        // there, else the game's first - and the silhouette after.
        string Steps(string garage) => e.Evaluate($"JSON.stringify(QwMfd.targetSteps(QwMfd.targetPicture(likely), {garage}))").AsString();
        Assert.Equal("""["stock","silhouette","mark"]""", Steps("{}"));
        Assert.Equal("""["stock","silhouette","mark"]""", Steps("{style:'paint', paints:{AEGS_Gladius:'x'}}"));
        Assert.Equal("""[{"paint":"Paint_Paladin_Black"},"stock","silhouette","mark"]""", Steps("{style:'paint', paints:{ANVL_Paladin:'Paint_Paladin_Black'}}"));
        // Silhouettes chosen in the Garage, for every hull or this one.
        Assert.Equal("""["silhouette","stock","mark"]""", Steps("{style:'tint'}"));
        Assert.Equal("""["silhouette","stock","mark"]""", Steps("{style:'paint', paints:{ANVL_Paladin:'silhouette'}}"));
        // Nothing named: the mark, whatever the Garage says.
        Assert.Equal("""["mark"]""", e.Evaluate("JSON.stringify(QwMfd.targetSteps(QwMfd.targetPicture(live), {}))").AsString());

        // Nothing to draw with reading off or nothing locked.
        Assert.True(e.Evaluate("QwMfd.targetPicture({...live, enabled:false}) === null").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.targetPicture({...live, status:{combat:{target:null}}}) === null").AsBoolean());
    }

    [Fact]
    public void Combat_is_a_category_on_home_holding_both_pages()
    {
        var e = Mfd();
        Assert.Equal("home/combat/target", e.Evaluate("QwMfd.trail('target').join('/')").AsString());
        Assert.Equal("home/combat/calls", e.Evaluate("QwMfd.trail('calls').join('/')").AsString());
        Assert.Equal(5, e.Evaluate("QwMfd.menuItems('home').length").AsNumber());
        Assert.Equal("target", e.Evaluate("QwMfd.resolveCommand('menu-1','combat')").AsString());
    }
}
