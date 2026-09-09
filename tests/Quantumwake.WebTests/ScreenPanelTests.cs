namespace Quantumwake.WebTests;

/// <summary>
/// The panel that reads the clipboard and the last screenshot.
/// </summary>
/// <remarks>
/// What is being defended here is mostly restraint: two permissions rather than
/// one, both off until asked for, controls that appear with the mode that
/// allows them, and a finding that goes away again rather than sitting there
/// looking current.
/// </remarks>
public class ScreenPanelTests
{
    private const string Location =
        """
        {"found":true,"x":-9641671346.9,"y":-11490734321.2,"z":-91805.1,
         "gigametresFromCentre":14.99996,"trouble":null}
        """;

    private static Page Panel(string mode = "Off", bool desktop = true, bool watch = false)
    {
        var page = new Page();

        page.Serve("/api/screen/settings", $$"""
            {"mode":"{{mode}}","watch":{{(watch ? "true" : "false")}},
             "canReadScreenshots":{{(desktop ? "true" : "false")}},
             "canReadClipboard":{{(desktop ? "true" : "false")}}}
            """);

        page.Serve("/api/screen/readings?take=50", """{"readings":[],"clipboard":[],"total":0,"pastes":0}""");
        page.Do("await renderScreenPanel();");
        return page;
    }

    [Fact]
    public void Everything_is_off_and_hidden_until_it_is_asked_for()
    {
        var page = Panel();

        Assert.True(page.Truth("__dom.node('#screen-controls').hidden"));
        Assert.Contains("off", page.NodeText("#screen-mode-status"));
    }

    [Fact]
    public void Copy_only_offers_the_clipboard_and_not_screenshots()
    {
        var page = Panel("CopyOnly");

        Assert.False(page.Truth("__dom.node('#screen-controls').hidden"));
        Assert.False(page.Truth("__dom.node('#screen-parse').hidden"));

        // Not greyed out - absent. A control that is there and refuses is
        // worse than one that is not there yet.
        Assert.True(page.Truth("__dom.node('#screen-scan').hidden"));
        Assert.Contains("clipboard only", page.NodeText("#screen-mode-status"));
    }

    [Fact]
    public void The_screenshot_button_arrives_with_the_mode_that_allows_it()
    {
        var page = Panel("Screenshots");

        Assert.False(page.Truth("__dom.node('#screen-scan').hidden"));
        Assert.Contains("clipboard and screenshots", page.NodeText("#screen-mode-status"));
    }

    /// <summary>
    /// A dashboard opened in a browser against the bare server has no desktop
    /// to read from, and says so rather than offering buttons that fail.
    /// </summary>
    [Fact]
    public void A_server_with_no_desktop_says_so()
    {
        var page = Panel(desktop: false);

        Assert.False(page.Truth("__dom.node('#screen-unavailable').hidden"));
        Assert.True(page.Truth("__dom.node('#screen-mode').disabled"));
    }

    [Fact]
    public void A_location_is_shown_in_gigametres_with_the_raw_numbers_kept()
    {
        var page = Panel("CopyOnly");
        page.Serve("/api/screen/clipboard", Location);

        page.Do("await parseClipboard();");

        var result = page.NodeText("#screen-result");

        Assert.Contains("15.0000 Gm", result);
        Assert.Contains("-9,641,671,347", result);
    }

    /// <summary>
    /// The reading says where and not which system, and the panel says as much
    /// rather than letting the number look more complete than it is.
    /// </summary>
    [Fact]
    public void The_panel_admits_the_reading_does_not_name_a_system()
    {
        var page = Panel("CopyOnly");
        page.Serve("/api/screen/clipboard", Location);

        page.Do("await parseClipboard();");

        Assert.Contains("from your logs", page.NodeText("#screen-result"));
    }

    [Fact]
    public void Copying_something_that_is_not_a_location_says_what_to_do()
    {
        var page = Panel("CopyOnly");
        page.Serve("/api/screen/clipboard",
            """{"found":false,"trouble":"what you copied is not a location - type /showlocation in the game first"}""");

        page.Do("await parseClipboard();");

        Assert.Contains("/showlocation", page.NodeText("#screen-status"));
    }

    [Fact]
    public void A_scan_names_the_item_and_says_what_vouched_for_it()
    {
        var page = Panel("Screenshots");
        page.Serve("/api/screen/scan", """
            {"shot":"ScreenShot-2026-09-07_21-00-35-8E4.jpg","shotAt":"2026-09-08T01:00:35Z",
             "kind":"Tooltip","summary":"Arlington Rifle","tookMs":170,"checks":[],"lines":[],
             "item":{"name":"Arlington Rifle",
                     "fields":{"Manufacturer":"Hedeby Gunworks"},
                     "matches":[{"name":"Arlington Rifle","className":"hdgw_rifle_ballistic_01",
                                 "tier":"Exact","agrees":["manufacturer","volume"],"disagrees":[]}],
                     "named":[],"certain":true,"trouble":null}}
            """);

        page.Do("await scanScreenshot();");

        var result = page.NodeText("#screen-result");

        Assert.Contains("Arlington Rifle", result);
        Assert.Contains("Hedeby Gunworks", result);
        Assert.Contains("manufacturer, volume agree", result);
        Assert.Contains("170 ms", page.NodeText("#screen-status"));
    }

    /// <summary>
    /// A frame with no tooltip still named things, and that is an answer
    /// rather than a failure.
    /// </summary>
    [Fact]
    public void A_frame_with_no_tooltip_still_reports_what_it_named()
    {
        var page = Panel("Screenshots");
        page.Serve("/api/screen/scan", """
            {"shot":"ScreenShot.jpg","shotAt":"2026-09-08T01:00:35Z","kind":"Tooltip",
             "summary":"1 things named","tookMs":180,"checks":[],"lines":[],
             "item":{"name":null,"fields":{},"matches":[],
                     "named":[{"text":"MSD-423 Missile Rack",
                               "candidates":["MSD-423 Missile Rack"],"exact":true}],
                     "certain":false,"trouble":null}}
            """);

        page.Do("await scanScreenshot();");

        Assert.Contains("MSD-423 Missile Rack", page.NodeText("#screen-result"));
    }

    /// <summary>
    /// Trouble is said out loud. A panel that goes blank is indistinguishable
    /// from one that is broken.
    /// </summary>
    [Fact]
    public void Having_taken_no_screenshots_says_what_to_press()
    {
        var page = Panel("Screenshots");
        page.Serve("/api/screen/scan", """
            {"shot":"","shotAt":"2026-09-08T01:00:35Z","kind":"Unknown","tookMs":0,"checks":[],"lines":[],
             "summary":"no screenshots yet - press Print Screen in the game and try again"}
            """);

        page.Do("await scanScreenshot();");

        Assert.Contains("Print Screen", page.NodeText("#screen-status"));
    }

    /// <summary>
    /// A finding replaces the one before it. The panel answers the last thing
    /// asked; a pile of old answers is a log, which this is not.
    /// </summary>
    [Fact]
    public void A_second_reading_replaces_the_first()
    {
        var page = Panel("CopyOnly");
        page.Serve("/api/screen/clipboard", Location);
        page.Do("await parseClipboard();");

        page.Serve("/api/screen/clipboard",
            """{"found":true,"x":1,"y":0,"z":0,"gigametresFromCentre":0.000000001,"trouble":null}""");
        page.Do("await parseClipboard();");

        var result = page.NodeText("#screen-result");

        Assert.DoesNotContain("15.0000 Gm", result);
    }
}
