using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The cockpit target panel, the hulls a cross-section fits, and what the
/// combat watch says. The lines are the engines' own output on frames of the
/// 2026-11-05 recording, read at 3440x1440, misreadings and all.
/// </summary>
public sealed class CombatTargetTests
{
    private static ScreenTextLine L(double left, double top, double height, string text) => new(text, left, top, height);

    // ---- the panel ----

    /// <summary>00:30:00, the loose HUD: an UNKNOWN fighter, EM 7.9K, IR 29.6K, CS 17.8K.</summary>
    private static readonly ScreenTextLine[] Loose =
    [
        L(2856, 304, 20, "PARTY"), L(2840, 354, 26, "KISHINEREN"),
        L(2848, 440, 17, "17.8K"), L(2728, 446, 21, "29.6K"), L(2617, 452, 19, "7.9K"),
        L(2858, 455, 19, "0.0"), L(2728, 460, 20, "276.0"), L(2621, 467, 17, "3.6"),
        L(2699, 497, 19, "UNKNOWH"),
        L(2664, 513, 33, "FACTION UNKNOWN"),
        L(2630, 534, 33, "VELOCITY>10M/S 3.13KM)"),
    ];

    [Fact]
    public void The_loose_panel_reads_its_figures_and_an_unknown_name()
    {
        var panel = TargetPanel.Read(Loose, 1440);

        Assert.NotNull(panel);
        Assert.True(panel.Unknown);
        Assert.Null(panel.Name);
        Assert.Null(panel.Faction);
        Assert.Equal(10, panel.SpeedMs);
        Assert.Equal(3.13, panel.RangeKm);
        Assert.Equal(7900, panel.Em);
        Assert.Equal(29600, panel.Ir);
        Assert.Equal(17800, panel.CrossSection);
    }

    [Fact]
    public void Velocity_and_faction_read_as_one_line_still_give_speed_and_range()
    {
        // 00:35:00: the engine ran the two rows together, and read 4.8K as 48K.
        var lines = new[]
        {
            L(2852, 390, 22, "7.4K"), L(2728, 397, 20, "13.0K"), L(2615, 404, 20, "48K"),
            L(2700, 448, 25, "UNKNOWN"),
            L(2630, 464, 33, "VELOCTY88M/S 1.1KM) FACTION UNKNOWN"),
        };

        var panel = TargetPanel.Read(lines, 1440);

        Assert.NotNull(panel);
        Assert.True(panel.Unknown);
        Assert.Null(panel.Faction);
        Assert.Equal(88, panel.SpeedMs);
        Assert.Equal(1.1, panel.RangeKm);
        Assert.Equal(7400, panel.CrossSection);
    }

    [Fact]
    public void The_boxed_panel_reads_its_name_faction_and_a_misread_figure()
    {
        // 01:59:24, the other HUD: label and value in separate boxes, and
        // the Blade's 10.4K read 10.AK.
        var lines = new[]
        {
            L(2714, 340, 18, "PARTY"), L(2834, 374, 23, "KISHINEREN"),
            L(2866, 440, 16, "10.AK"), L(2740, 446, 14, "13K"), L(2618, 452, 16, "3.8K"),
            L(2866, 468, 14, "O.0"), L(2739, 472, 14, "LAK"), L(2618, 478, 14, "19.0"),
            L(2564, 520, 25, "VANDUUL BLADE"),
            L(2577, 556, 24, "FACTION VANDUUL"),
            L(2579, 582, 25, "VELOCITY 18M [1.28KM]"),
        };

        var panel = TargetPanel.Read(lines, 1440);

        Assert.NotNull(panel);
        Assert.Equal("VANDUUL BLADE", panel.Name);
        Assert.Equal("Vanduul", panel.Faction);
        Assert.Equal(18, panel.SpeedMs);
        Assert.Equal(1.28, panel.RangeKm);
        Assert.Equal(3800, panel.Em);
        Assert.Equal(13000, panel.Ir);
        Assert.Equal(10400, panel.CrossSection);
    }

    [Fact]
    public void Windows_engine_without_the_labels_is_found_by_the_panel_range()
    {
        // The same frame through Windows' engine: no VELOCITY, no FACTION, the
        // speed's slash gone and a letter for a digit.
        var lines = new[]
        {
            L(2866, 468, 13, "0.0"),
            L(2564, 520, 20, "VANDUUL BLADE"),
            L(2684, 579, 25, "1B3M�0.28KM)"),
        };

        var panel = TargetPanel.Read(lines, 1440);

        Assert.NotNull(panel);
        Assert.Equal("VANDUUL BLADE", panel.Name);
        Assert.Equal(183, panel.SpeedMs);
        Assert.Equal(0.28, panel.RangeKm);
    }

    [Theory]
    [InlineData("FAETION UNKNOWN")]
    [InlineData("PACTION")]
    [InlineData("AGTON")]
    [InlineData("STION UNKNOWN")]
    public void A_misread_faction_label_is_never_taken_for_the_name(string label)
    {
        var lines = new[]
        {
            L(2564, 397, 42, "RSI PERSEUS"),
            L(2606, 438, 21, label),
            L(2583, 463, 29, "VELOCITY OM/S (5.28KM)"),
        };

        var panel = TargetPanel.Read(lines, 1440);

        Assert.NotNull(panel);
        Assert.Equal("RSI PERSEUS", panel.Name);
        Assert.Equal(0, panel.SpeedMs);
    }

    [Fact]
    public void The_flight_huds_speed_limiter_is_not_the_panel()
    {
        Assert.Null(TargetPanel.Read([L(2147, 1123, 16, "VEL LIM OFF")], 1440));
        Assert.Null(TargetPanel.Locate([L(2147, 1123, 16, "VEL LIM OFF")], 3440, 1440));
    }

    [Fact]
    public void The_two_rightmost_figures_are_ir_and_cs_only_when_they_sit_there()
    {
        // 00:34:30: the Idris-P's CS column read B8.DK and dropped out, and
        // counting from the left put its IR in CS - and called it a Polaris.
        var lines = new[]
        {
            L(2848, 390, 21, "B8.DK"), L(2727, 398, 18, "42.6K"), L(2618, 404, 19, "7.6K"),
            L(2680, 448, 27, "AEGIS IDRIS-P"),
            L(2702, 472, 21, "FACTION"),
            L(2640, 485, 27, "VELOCITY 0M/S (0.93KM)"),
        };

        var panel = TargetPanel.Read(lines, 1440);

        Assert.NotNull(panel);
        Assert.Equal("AEGIS IDRIS-P", panel.Name);
        Assert.Equal(42600, panel.Ir);
        Assert.Null(panel.CrossSection);
    }

    [Theory]
    [InlineData("31m (88m/e", true)]
    [InlineData("25km [-8m/s]", true)]
    [InlineData("2.4km f-125m/c]", true)]
    [InlineData("3.0{m[75m/s]", true)]
    [InlineData("2.4km", false)]
    [InlineData("VELOCITY 43M/S (2.55KM)", false)]
    public void Only_the_locked_targets_label_carries_a_speed_in_brackets(string text, bool locked) =>
        Assert.Equal(locked, TargetPanel.LockLabel([L(1600, 800, 18, text)]) is not null);

    [Fact]
    public void The_label_colour_says_hostile_friendly_or_neutral()
    {
        var label = L(10, 10, 10, "1.2km (26m/s)");

        Assert.Equal(TargetStance.Hostile, TargetPanel.Stance(Paint(230, 50, 50), label));
        Assert.Equal(TargetStance.Friendly, TargetPanel.Stance(Paint(80, 150, 240), label));
        Assert.Equal(TargetStance.Neutral, TargetPanel.Stance(Paint(220, 220, 225), label));
        Assert.Equal(TargetStance.Unread, TargetPanel.Stance(Paint(20, 20, 20), label));
        Assert.Equal(TargetStance.Unread, TargetPanel.Stance(null, label));
    }

    /// <summary>A label's box: every other pixel lit in the colour, the rest dark sky.</summary>
    private static ColourPatch Paint(byte r, byte g, byte b)
    {
        const int w = 120, h = 30;
        var bgra = new byte[w * h * 4];
        for (var i = 0; i < w * h; i++)
        {
            var lit = i % 2 == 0;
            bgra[i * 4] = lit ? b : (byte)15;
            bgra[i * 4 + 1] = lit ? g : (byte)20;
            bgra[i * 4 + 2] = lit ? r : (byte)25;
            bgra[i * 4 + 3] = 255;
        }
        return new ColourPatch(0, 0, w, h, bgra);
    }

    [Fact]
    public void Locate_scales_the_patch_with_the_frame()
    {
        var big = TargetPanel.Locate(Loose, 3440, 1440);
        var half = TargetPanel.Locate([.. Loose.Select(l => l with { Left = l.Left / 2, Top = l.Top / 2, Height = l.Height / 2 })], 1720, 720);

        Assert.NotNull(big);
        Assert.NotNull(half);
        Assert.Equal(big.Width / 2, half.Width, 3);
        Assert.True(big.Left < 2630 && big.Left + big.Width > 2860, "the patch covers the figures");
        Assert.True(big.Top < 440 && big.Top + big.Height > 534, "and the rows");
    }

    // ---- hulls by cross-section ----

    private static readonly ShipSignatureBook Book = ShipSignatureBook.Of(
    [
        new("Aegis Gladius", 9779, "AEGS_Gladius"), new("Aegis Gladius Valiant", 9779, "AEGS_Gladius_Valiant"),
        new("C.O. Nomad", 9712, "CNOU_Nomad"), new("Drake Herald", 9648, "DRAK_Herald"),
        new("Anvil Paladin", 28580, "ANVL_Paladin"), new("MISC Starfarer Gemini", 28193, "MISC_Starfarer_Gemini"),
        new("Vanduul Blade", 10418, "VNCL_Blade"), new("Origin 85X Limited", 10444, "ORIG_85X_Limited"),
        new("Vanduul Glaive", 11922, "VNCL_Glaive"), new("C.O. Mustang Gamma", 11984, "CNOU_Mustang_Gamma"),
        new("RSI Scorpius", 13591, "RSI_Scorpius"), new("RSI Scorpius Antares", 13597, "RSI_Scorpius_Antares"),
        new("RSI Perseus", 35430, "RSI_Perseus"), new("Aegis Idris-P", 60000, "AEGS_Idris_P"),
        new("Origin M50 Interceptor", 5365, "ORIG_m50"),
    ]);

    private static readonly IReadOnlySet<string> NoneSeen = new HashSet<string>();

    [Fact]
    public void Odds_from_the_signature_alone_split_evenly_between_its_hulls()
    {
        var odds = Book.Weigh(10400, null, NoneSeen);

        Assert.Equal(["Origin 85X Limited", "Vanduul Blade"], odds.Select(o => o.Name).Order());
        Assert.Equal(0.5, odds[0].Chance);
        Assert.Equal(0.5, odds[1].Chance);
    }

    [Fact]
    public void A_hull_the_log_saw_nearby_takes_most_of_the_odds()
    {
        // 2026-11-05: VNCL_Blade streamed in 21 times, the 85X never.
        var odds = Book.Weigh(10400, null, new HashSet<string> { "VNCL_Blade" });

        Assert.Equal("Vanduul Blade", odds[0].Name);
        Assert.Equal(0.9, odds[0].Chance);
        Assert.Contains("seen nearby", odds[0].Because);
    }

    [Fact]
    public void A_vanduul_faction_all_but_settles_it_and_any_other_rules_vanduul_out()
    {
        Assert.Equal(1.0, Book.Weigh(11900, "Vanduul", NoneSeen)[0].Chance);
        Assert.Equal("Vanduul Glaive", Book.Weigh(11900, "Vanduul", NoneSeen)[0].Name);

        Assert.Equal("C.O. Mustang Gamma", Book.Weigh(11900, "Outlaws", NoneSeen)[0].Name);
    }

    [Fact]
    public void A_variant_named_apart_from_its_hull_folds_by_its_class()
    {
        var book = ShipSignatureBook.Of(
        [
            new("Drake Corsair", 29125, "DRAK_Corsair"),
            new("Corsair PYAM Exec", 29125, "DRAK_Corsair_PYAM_Exec"),
        ]);

        Assert.Equal(["Drake Corsair"], book.Near(29100).Select(h => h.Name));
    }

    [Fact]
    public void A_variant_a_few_short_of_its_hull_is_the_hull()
    {
        // The Scorpius and the Antares are six apart; they split 45/45 before.
        var odds = Book.Weigh(13500, null, NoneSeen);
        Assert.Equal(["RSI Scorpius"], odds.Select(o => o.Name));
    }

    [Fact]
    public void The_log_lists_the_ship_classes_it_streamed_in_lately()
    {
        var nearby = new NearbyHulls();
        var at = new DateTimeOffset(2026, 11, 6, 3, 50, 0, TimeSpan.Zero);

        nearby.Line("[Notice] <ATC_Reset_VehiclesBeingStoredCleared> ATC 'VNCL_Blade_ShipATCDataManager_917287752642' [917287752642] clearing 0 entries from vehicles-being-stored list on Reset", at);
        nearby.Line("[Notice] <ATC_Reset_VehiclesBeingStoredCleared> ATC 'ANVL_Paladin_ShipATCDataManager_914901457784' [914901457784] clearing 0 entries", at.AddMinutes(-30));
        nearby.Line("[Notice] <Query Inventory> nothing about ships", at);

        Assert.Equal(["VNCL_Blade"], nearby.Classes(at.AddMinutes(5)));
    }

    [Fact]
    public void A_named_lock_that_reads_unknown_twice_is_a_new_ship()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel("VANDUUL SCYTHE", 200, 12500), TargetStance.Unread, T0);
        watch.See(Panel("VANDUUL SCYTHE", 200, 12500), TargetStance.Unread, T0.AddSeconds(1));

        watch.See(Panel(speed: 290), TargetStance.Unread, T0.AddSeconds(2));
        Assert.Equal(1, watch.Picture(T0.AddSeconds(2)).Locks);
        watch.See(Panel(speed: 290, cs: 11900), TargetStance.Unread, T0.AddSeconds(3));

        var picture = watch.Picture(T0.AddSeconds(3));
        Assert.Equal(2, picture.Locks);
        Assert.Null(picture.Target!.Name);
    }

    [Fact]
    public void An_unread_figure_between_two_off_ones_still_counts_as_a_switch()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel("RSI PERSEUS", 0, 35400), TargetStance.Unread, T0);
        watch.See(Panel("RSI PERSEUS", 0, 35400), TargetStance.Unread, T0.AddSeconds(1));

        watch.See(Panel("RSI PERSEUS", 250, 10400), TargetStance.Unread, T0.AddSeconds(2));
        watch.See(Panel("RSI PERSEUS", 250), TargetStance.Unread, T0.AddSeconds(3));
        watch.See(Panel("RSI PERSEUS", 250, 10400), TargetStance.Unread, T0.AddSeconds(4));

        Assert.Equal(2, watch.Picture(T0.AddSeconds(4)).Locks);
    }

    [Fact]
    public void A_figure_is_cut_not_rounded_so_it_names_the_hulls_just_above_it()
    {
        // The Paladin's 28,580 read 28.5K: rounding would have said 28.6K.
        var near = Book.Near(28500);
        Assert.Equal("Anvil Paladin", near[0].Name);
        Assert.DoesNotContain(near, h => h.Name == "MISC Starfarer Gemini");

        // The 85X Limited is 26 from the Blade: one figure names both.
        Assert.Equal(["Origin 85X Limited", "Vanduul Blade"], Book.Near(10400).Select(h => h.Name));
        Assert.Equal(["Vanduul Glaive", "C.O. Mustang Gamma"], Book.Near(11900).Select(h => h.Name));
    }

    [Fact]
    public void Variants_of_one_hull_on_one_figure_are_the_hull()
    {
        var near = Book.Near(9700).Select(h => h.Name).ToList();

        Assert.Contains("Aegis Gladius", near);
        Assert.Contains("C.O. Nomad", near);
        Assert.DoesNotContain("Aegis Gladius Valiant", near);
        Assert.DoesNotContain("Drake Herald", near);
    }

    [Theory]
    [InlineData("RSIPERSEUS", "RSI Perseus")]
    [InlineData("VANOUUL BLADE", "Vanduul Blade")]
    [InlineData("ORIGIN M50", "Origin M50 Interceptor")]
    [InlineData("AEGIS IDRIS-P", "Aegis Idris-P")]
    [InlineData("UNKNOWN", null)]
    [InlineData("KISHINEREN", null)]
    public void A_printed_name_snaps_to_its_hull(string printed, string? hull) =>
        Assert.Equal(hull, Book.Hull(printed));

    // ---- what is said ----

    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 21, 30, 0, TimeSpan.Zero);

    private static TargetReading Panel(string? name = null, double? speed = null, double? cs = null, string? faction = null) =>
        new(name, name is null, faction, speed, 2.0, null, null, cs, new ScreenPatch(2400, 300, 640, 250));

    [Fact]
    public void A_name_is_said_once_after_two_frames_agree()
    {
        var watch = new CombatWatch(Book);

        Assert.Empty(watch.See(Panel("VANDUUL BLADE", 200, 10400), TargetStance.Unread, T0));
        var said = watch.See(Panel("VANDUUL BLADE", 210, 10400), TargetStance.Unread, T0.AddSeconds(1));
        Assert.Equal(["Target identified: Vanduul Blade"], said.Select(c => c.Text));

        Assert.Empty(watch.See(Panel("VANDUUL BLADE", 220, 10400), TargetStance.Unread, T0.AddSeconds(2)));
    }

    [Fact]
    public void An_unknown_with_a_clear_signature_is_named_as_likely()
    {
        var watch = new CombatWatch(Book);

        Assert.Empty(watch.See(Panel(cs: 28500, speed: 120), TargetStance.Unread, T0));
        var said = watch.See(Panel(cs: 28500, speed: 110), TargetStance.Unread, T0.AddSeconds(1));

        Assert.Equal(["Unknown contact, likely Anvil Paladin"], said.Select(c => c.Text));
        Assert.Equal("Anvil Paladin", watch.Picture(T0.AddSeconds(1)).Target!.Candidates[0].Name);
    }

    [Fact]
    public void Disabled_needs_a_target_seen_moving_and_then_stopped()
    {
        // The Idris-P sat at 0 m/s for the whole defence: never disabled.
        var idris = new CombatWatch(Book);
        for (var i = 0; i < 5; i++)
            Assert.DoesNotContain(idris.See(Panel("AEGIS IDRIS-P", 0, 60000), TargetStance.Unread, T0.AddSeconds(i)),
                c => c.Kind == "disabled");

        var fighter = new CombatWatch(Book);
        fighter.See(Panel(speed: 180, cs: 10400), TargetStance.Unread, T0);
        Assert.DoesNotContain(fighter.See(Panel(speed: 0, cs: 10400), TargetStance.Unread, T0.AddSeconds(1)), c => c.Kind == "disabled");
        var said = fighter.See(Panel(speed: 0, cs: 10400), TargetStance.Unread, T0.AddSeconds(2));

        Assert.Contains(said, c => c.Text == "Target disabled");
        Assert.True(fighter.Picture(T0.AddSeconds(2)).Target!.Disabled);
    }

    [Fact]
    public void Named_and_hostile_on_one_frame_is_one_sentence_and_combat()
    {
        var watch = new CombatWatch(Book);

        watch.See(Panel("VANDUUL GLAIVE", 250, 11900, "Vanduul"), TargetStance.Hostile, T0);
        var said = watch.See(Panel("VANDUUL GLAIVE", 240, 11900, "Vanduul"), TargetStance.Hostile, T0.AddSeconds(1));
        Assert.Empty(said);

        var all = watch.Picture(T0.AddSeconds(1));
        Assert.Contains(all.Callouts, c => c.Text == "Hostile Vanduul Glaive");
        Assert.True(all.Combat);
        Assert.Equal(1, all.HostileLocks);
        Assert.False(watch.Picture(T0.AddMinutes(2)).Combat);
        Assert.NotNull(said);
    }

    [Fact]
    public void One_misread_signature_is_not_a_new_lock()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel(speed: 200, cs: 10400), TargetStance.Unread, T0);
        watch.See(Panel(speed: 200, cs: 10400), TargetStance.Unread, T0.AddSeconds(1));

        // 10.4K read as 104K once.
        watch.See(Panel(speed: 200, cs: 104000), TargetStance.Unread, T0.AddSeconds(2));
        watch.See(Panel(speed: 200, cs: 10400), TargetStance.Unread, T0.AddSeconds(3));
        Assert.Equal(1, watch.Picture(T0.AddSeconds(3)).Locks);

        // A different hull, twice, is.
        watch.See(Panel(speed: 200, cs: 35400), TargetStance.Unread, T0.AddSeconds(4));
        watch.See(Panel(speed: 200, cs: 35400), TargetStance.Unread, T0.AddSeconds(5));
        Assert.Equal(2, watch.Picture(T0.AddSeconds(5)).Locks);
    }

    [Fact]
    public void A_glance_away_keeps_the_lock_and_a_long_one_drops_it()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel("RSI PERSEUS", 0, 35400), TargetStance.Unread, T0);

        watch.See(null, TargetStance.Unread, T0.AddSeconds(3));
        Assert.NotNull(watch.Picture(T0.AddSeconds(3)).Target);

        watch.See(null, TargetStance.Unread, T0.AddSeconds(10));
        Assert.Null(watch.Picture(T0.AddSeconds(10)).Target);
    }

    [Fact]
    public void A_hostile_named_only_by_its_signature_is_said_as_likely()
    {
        var watch = new CombatWatch(Book);

        var said = watch.See(Panel(cs: 28500, speed: 120), TargetStance.Hostile, T0);
        said = [.. said, .. watch.See(Panel(cs: 28500, speed: 110), TargetStance.Hostile, T0.AddSeconds(1))];

        Assert.Equal(["Hostile contact, likely Anvil Paladin"], said.Select(c => c.Text));
    }

    // ---- the voice switch ----

    [Fact]
    public void The_voice_is_off_until_switched_on_and_survives_the_reading_answer()
    {
        var root = Path.Combine(Path.GetTempPath(), "qw-voice-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LiveScreenStore(root);
            Assert.False(store.Current.Voice);

            store.Answer(true);
            Assert.False(store.Current.Voice);

            store.SetVoice(true);
            store.Answer(true);
            Assert.True(new LiveScreenStore(root).Current.Voice);
            Assert.True(new LiveScreenStore(root).Current.Enabled);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}
