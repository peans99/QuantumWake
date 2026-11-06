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
             "target":{"name":null,"candidates":[{"name":"Anvil Paladin","crossSection":28580}],"faction":"Outlaws",
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
        Assert.Equal("Unknown · likely Anvil Paladin", page.NodeText("#now-target"));
        Assert.Contains("Hostile", page.NodeText("#now-target-detail"));
        Assert.Contains("109 m/s", page.NodeText("#now-target-detail"));
        Assert.Contains("CS 28.5K", page.NodeText("#now-target-detail"));
        Assert.Contains("in combat", page.NodeText("#now-target-label"));
        Assert.True(page.Truth("__dom.node('#now-target-card').classList.contains('target-hostile')"));
        Assert.Contains("Hostile Anvil Paladin", page.NodeText("#now-target-calls"));
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
        Assert.Contains("Unknown · likely Anvil Paladin", json);
        Assert.Contains("HOSTILE · Outlaws", json);
        Assert.Contains("109 m/s · 2.43 km", json);
        Assert.Contains("CS 28.5K", json);
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
    public void Combat_is_a_category_on_home_holding_both_pages()
    {
        var e = Mfd();
        Assert.Equal("home/combat/target", e.Evaluate("QwMfd.trail('target').join('/')").AsString());
        Assert.Equal("home/combat/calls", e.Evaluate("QwMfd.trail('calls').join('/')").AsString());
        Assert.Equal(5, e.Evaluate("QwMfd.menuItems('home').length").AsNumber());
        Assert.Equal("target", e.Evaluate("QwMfd.resolveCommand('menu-1','combat')").AsString());
    }
}
