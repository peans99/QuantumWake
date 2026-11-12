namespace Quantumwake.WebTests;
public class StreamDeckPageTests
{
    /// <summary>
    /// The harness strips <c>boot()</c>, so it cannot replay the load order that
    /// broke: boot opens the fragment's view before its first await, and the
    /// Stream Deck's state was declared below the call. Opening the app on
    /// #streamdeck threw on <c>deckRestored</c> and left the page blank. Keeping
    /// the call as the file's last statement is what prevents the whole class.
    /// </summary>
    [Fact]
    public void Boot_runs_after_every_top_level_declaration()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "app.js")).TrimEnd();
        Assert.EndsWith("\nboot();", source.Replace("\r\n", "\n"));
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(source, @"^boot\(\);", System.Text.RegularExpressions.RegexOptions.Multiline).Count);
    }

    [Theory]
    [InlineData("f5+lalt", "Alt + F5")]
    [InlineData("rctrl+np_8", "R Ctrl + Num 8")]
    [InlineData("lalt+mwheel_up", "Alt + Wheel ↑")]
    public void Key_hints_put_modifiers_first_and_distinguish_right_hand_and_mouse_inputs(string binding, string expected)
    {
        var page = new Page();
        Assert.Equal(expected, page.Text("QWDeckArt.prettyKey(" + System.Text.Json.JsonSerializer.Serialize(binding) + ")"));
    }
    [Fact] public void Page_explains_missing_profiles_and_refuses_empty_exports()
    {
        var page = new Page();
        page.Serve("/api/streamdeck?defaults=false", "{\"ready\":true,\"profileFound\":false,\"commands\":[]}");
        page.Do("await loadStreamDeck();");
        Assert.Contains("No saved keybinding profile", page.NodeText("#deck-status"));
        Assert.True(page.Truth("__dom.node('#deck-download').disabled && __dom.node('#deck-open').disabled"));
    }
    [Fact] public void Existing_categories_start_selected_and_extended_is_optional()
    {
        var page = new Page();
        page.Serve("/api/streamdeck?defaults=false", "{\"ready\":true,\"profileFound\":true,\"commands\":[{\"id\":\"preset/flight\",\"group\":\"Flight\",\"label\":\"Gear\",\"key\":\"n\",\"preset\":true},{\"id\":\"preset/extra\",\"group\":\"Extended\",\"label\":\"Pins\",\"key\":\"0\",\"preset\":true}]}");
        page.Do("await loadStreamDeck();");
        Assert.True(page.Truth("deckSelection.has('preset/flight') && !deckSelection.has('preset/extra')"));
        Assert.Contains("Flight",page.NodeText("#deck-categories"));
        Assert.Contains("Extended",page.NodeText("#deck-categories"));
    }
    [Fact] public void Search_matches_commands_and_unbound_actions_are_not_selected_for_export()
    {
        var page=new Page();
        page.Do("deckCommands = [{id:'gear',group:'Flight',label:'Landing Gear',key:'n',problem:null},{id:'door',group:'Flight',label:'Door',key:'',problem:'Unbound'}]; deckSelection = new Set(['gear','door']);");
        Assert.True(page.Truth("deckSelected().length === 1 && deckSelected()[0].id === 'gear'"));
        page.Do("__dom.node('#deck-search').value = 'landing';");
        Assert.True(page.Truth("deckShown().length === 1 && deckShown()[0].id === 'gear'"));
    }
    [Fact] public void Commands_the_game_fires_late_say_how_long_to_hold()
    {
        var page = new Page();
        page.Serve("/api/streamdeck?defaults=false", "{\"ready\":true,\"profileFound\":true,\"commands\":["
            + "{\"id\":\"destruct\",\"group\":\"Ship\",\"label\":\"Self Destruct\",\"key\":\"backspace\",\"activation\":\"delayed_press_medium\",\"holdFor\":0.5,\"tapOrHold\":false},"
            + "{\"id\":\"decoy\",\"group\":\"Ship\",\"label\":\"Decoy\",\"key\":\"h\",\"activation\":\"\",\"holdFor\":0.25,\"tapOrHold\":true},"
            + "{\"id\":\"boost\",\"group\":\"Ship\",\"label\":\"Boost\",\"key\":\"lshift\",\"activation\":\"hold\",\"holdFor\":0},"
            + "{\"id\":\"gear\",\"group\":\"Ship\",\"label\":\"Gear\",\"key\":\"n\",\"activation\":\"\"}]}");
        page.Do("__dom.node('#deck-scope').value = 'all'; await loadStreamDeck();");
        var list = page.NodeText("#deck-commands");
        Assert.Contains("Bksp", list);
        Assert.Contains("Hold 0.5 s before it fires", list);
        Assert.Contains("Tap, or hold 0.25 s for its hold action", list);
        Assert.Contains("Shift", list);
        Assert.Contains("Hold the button", list);

    }

    [Theory]
    [InlineData("15", 15)]
    [InlineData("xl", 32)]
    public void Preview_keys_use_the_plugin_art_and_expose_the_full_label_and_binding(string model, int count)
    {
        var page = new Page();
        page.Do($$$"""
            deckCommands = [{id:'gear',group:'Flight',label:'Landing\nGear',key:'rctrl+n',appearance:{icon:'gear',color:'#77d8f0'}}];
            deckSelection = new Set(['gear']); deckPreviewGroup = 'Flight';
            __dom.node('#deck-model').value = '{{{model}}}'; renderDeckPreview();
            """);

        Assert.Equal(count.ToString(), page.Text("String(__dom.node('#deck-preview').children.length)"));
        Assert.Equal("Landing Gear · R Ctrl + N", page.Text("__dom.node('#deck-preview').children[1].getAttribute('aria-label')"));
        Assert.True(page.Truth("__dom.node('#deck-preview').children[1].children[0].src === QWDeckArt.svgDataUrl(QWDeckArt.renderKey(deckArt(deckCommands[0])))"));
        page.Do("__dom.node('#deck-preview').children[1].click();");
        Assert.Contains("Landing Gear", page.NodeText("#deck-key-detail"));
        Assert.Contains("R Ctrl + N", page.NodeText("#deck-key-detail"));
    }

    [Fact]
    public void More_and_back_return_to_the_previous_page_of_the_same_folder()
    {
        var page = new Page();
        page.Do("""
            deckCommands = Array.from({length:30}, (_,i) => ({id:'action'+i,group:'Flight',label:'Action '+i,key:'n'}));
            deckSelection = new Set(deckCommands.map(c => c.id));
            __dom.node('#deck-model').value = '15'; deckPreviewGroup = 'Flight'; renderDeckPreview();
            __dom.node('#deck-preview').children[14].click();
            """);
        Assert.Equal("13", page.Text("String(deckPreviewOffset)"));
        page.Do("__dom.node('#deck-preview').children[0].click();");
        Assert.Equal("Flight", page.Text("deckPreviewGroup"));
        Assert.Equal("0", page.Text("String(deckPreviewOffset)"));
    }

    [Fact]
    public void Key_art_escapes_labels_and_preserves_the_original_text_for_long_labels()
    {
        var page = new Page();
        var art = page.Text("QWDeckArt.renderKey({label:'<Shield> & Supercalifragilisticexpialidocious',keyText:'R Ctrl + R Alt + Num 8',icon:'shield',color:'red\" onload=\"bad'})");
        var xml = System.Xml.Linq.XDocument.Parse(art);
        System.Xml.Linq.XNamespace svg = "http://www.w3.org/2000/svg";
        Assert.Contains("<Shield> & Supercalifragilisticexpialidocious", xml.Root!.Element(svg + "title")!.Value);
        Assert.DoesNotContain("onload=", art);
        Assert.DoesNotContain("<Shield>", art);
        Assert.All(xml.Descendants(svg + "text"), text => Assert.NotEmpty(text.Value));
    }

    [Fact]
    public void Short_words_shrink_to_fit_before_being_broken_into_fragments()
    {
        var page = new Page();
        var xml = System.Xml.Linq.XDocument.Parse(page.Text("QWDeckArt.renderKey({label:'Decouple',keyText:'C',icon:'flight'})"));
        System.Xml.Linq.XNamespace svg = "http://www.w3.org/2000/svg";
        Assert.Equal(new[] { "Decouple", "C" }, xml.Descendants(svg + "text").Select(text => text.Value));
    }

    // Each label is drawn without a key hint, so every text line is the label.
    private static string[] Lines(Page page, string label)
    {
        var xml = System.Xml.Linq.XDocument.Parse(page.Text("QWDeckArt.renderKey({label:" + System.Text.Json.JsonSerializer.Serialize(label) + ",icon:'flight'})"));
        System.Xml.Linq.XNamespace svg = "http://www.w3.org/2000/svg";
        return xml.Descendants(svg + "text").Select(text => text.Value).ToArray();
    }

    [Theory]
    // Words that do not fit even at the smallest size: a size smaller, not cut.
    [InlineData("IFCS - Gravity Compensation - Toggle", "Compensation")]
    [InlineData("Focus Disintegration tool", "Disintegration")]
    [InlineData("Countermeasure Launch Decoy", "Countermeasure")]
    [InlineData("[Experimental] VR - Recenter Device", "[Experimental]")]
    public void A_word_too_long_for_the_key_is_kept_whole(string label, string word)
    {
        Assert.Contains(Lines(new Page(), label), line => line.Split(' ').Contains(word));
    }

    [Theory]
    // Untranslated game ids have no spaces: they break where the id itself does.
    [InlineData("character_customizer_yaw_left", new[] { "character_", "customizer_", "yaw_left" })]
    [InlineData("remote_moveForward", new[] { "remote_move", "Forward" })]
    public void Identifiers_break_at_underscores_and_capitals(string label, string[] expected)
    {
        Assert.Equal(expected, Lines(new Page(), label));
    }

    [Fact]
    public void A_word_with_nowhere_to_break_is_hyphenated_and_loses_no_letters()
    {
        var lines = Lines(new Page(), "togglepdrawhelpers");
        Assert.True(lines.Length > 1);
        Assert.All(lines[..^1], line => Assert.EndsWith("-", line));
        Assert.Equal("togglepdrawhelpers", string.Concat(lines.Select(line => line.TrimEnd('-'))));
    }
    [Fact] public void Stream_deck_is_a_settings_tab_beside_controls()
    {
        // The stub seeds attributes, not nesting, so the placement is read from the markup.
        var markup = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html")).Replace("\r\n", "\n");
        var menu = markup[markup.IndexOf("data-glyph=\"✥\">Settings", StringComparison.Ordinal)..markup.IndexOf("</nav>", StringComparison.Ordinal)];
        Assert.Contains("<button data-view=\"controls\">Controls</button>\n        <button data-view=\"streamdeck\">Stream Deck</button>", menu);
        // One way in: the General group that led here, and its Back button, are gone.
        Assert.DoesNotContain("settings-streamdeck", markup);
        Assert.DoesNotContain("deck-back", markup);
        Assert.Contains("settings-group-index\">04</span>\n        <span class=\"settings-group-copy\"><strong>Log maintenance", markup);
        // No special case left in the router: as a menu tab it routes and
        // lights like Controls. The stub has no menu to walk, so this is read
        // from the source; NavigationTests holds every tab to its section.
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "app.js"));
        Assert.DoesNotContain("name === 'streamdeck' ? 'settings'", script);
        Assert.DoesNotContain("name === 'streamdeck') return name", script);
    }
}
