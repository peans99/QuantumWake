using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The locked ship's hologram judged for damage, the "Target critical" callout
/// it feeds, and the NPC hulls the log names. Colours are the ones measured on
/// the 2026-11-05 recording at 3440x1440: a whole ship drawn near-white, hurt
/// parts orange, critical red, over a teal nebula.
/// </summary>
public sealed class CombatHullTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 21, 30, 0, TimeSpan.Zero);

    /// <summary>A hologram cut: the first pixels white, then orange, then red, the rest nebula.</summary>
    private static ColourPatch Hologram(int white, int orange, int red, int width = 420, int height = 190)
    {
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            (byte r, byte g, byte b) c = i < white ? ((byte)215, (byte)225, (byte)228)
                : i < white + orange ? ((byte)230, (byte)140, (byte)50)
                : i < white + orange + red ? ((byte)210, (byte)40, (byte)35)
                : ((byte)70, (byte)150, (byte)140);
            bgra[i * 4] = c.b;
            bgra[i * 4 + 1] = c.g;
            bgra[i * 4 + 2] = c.r;
            bgra[i * 4 + 3] = 255;
        }
        return new ColourPatch(0, 0, width, height, bgra);
    }

    [Fact]
    public void A_white_hologram_is_sound_and_an_all_red_one_is_wholly_damaged()
    {
        Assert.Equal(0, HologramDamage.Read(Hologram(3000, 0, 0), 1440)!.Damage);
        Assert.Equal(1, HologramDamage.Read(Hologram(0, 0, 3000), 1440)!.Damage);

        // Orange counts half: an all-orange ship is the "damaged" band, not critical.
        Assert.Equal(0.5, HologramDamage.Read(Hologram(0, 3000, 0), 1440)!.Damage);
    }

    [Fact]
    public void A_teal_nebula_behind_the_glass_is_not_hull()
    {
        var reading = HologramDamage.Read(Hologram(1000, 0, 0), 1440);
        Assert.NotNull(reading);
        Assert.Equal(1000, reading.Lit);
    }

    [Fact]
    public void Too_few_lit_pixels_are_not_judged()
    {
        Assert.Null(HologramDamage.Read(Hologram(100, 50, 50), 1440));
    }

    [Fact]
    public void The_hologram_sits_under_the_panel_and_scales_with_the_frame()
    {
        var panel = new ScreenPatch(2400, 300, 640, 250);
        Assert.Equal(new ScreenPatch(2520, 530, 420, 190), HologramDamage.Area(panel, 1440));
        Assert.Equal(new ScreenPatch(2460, 415, 210, 95), HologramDamage.Area(panel, 720));
    }

    private static TargetReading Panel(double speed) =>
        new("VANDUUL BLADE", false, "Vanduul", speed, 1.2, null, null, 10400, new ScreenPatch(2400, 300, 640, 250));

    private static HologramReading Looks(double damage) => new(damage, 100, 100, 100);

    private static readonly ShipSignatureBook Book = ShipSignatureBook.Of([new("Vanduul Blade", 10418, "VNCL_Blade")]);

    [Fact]
    public void Critical_is_said_once_when_the_median_of_three_reads_is_red()
    {
        var watch = new CombatWatch(Book);
        var said = new List<string>();
        // As the Blade fight read: one frame swings, the median holds.
        double[] reads = [0.2, 0.9, 0.46, 0.87, 0.91, 0.95, 0.93];
        for (var i = 0; i < reads.Length; i++)
            said.AddRange(watch.See(Panel(200), TargetStance.Hostile, T0.AddSeconds(i), Looks(reads[i])).Select(c => c.Text));

        Assert.Single(said, s => s == "Target critical");
        Assert.Equal("critical", watch.Picture(T0.AddSeconds(reads.Length)).Target!.Hull);
    }

    [Fact]
    public void One_red_frame_among_white_ones_is_not_critical()
    {
        var watch = new CombatWatch(Book);
        var said = new List<string>();
        double[] reads = [0.05, 0.95, 0.1, 0.0, 0.9, 0.05];
        for (var i = 0; i < reads.Length; i++)
            said.AddRange(watch.See(Panel(200), TargetStance.Hostile, T0.AddSeconds(i), Looks(reads[i])).Select(c => c.Text));

        Assert.DoesNotContain("Target critical", said);
        Assert.Equal("sound", watch.Picture(T0.AddSeconds(6)).Target!.Hull);
    }

    [Fact]
    public void A_wreck_locked_at_rest_is_red_but_not_news()
    {
        var watch = new CombatWatch(Book);
        var said = new List<string>();
        for (var i = 0; i < 4; i++)
            said.AddRange(watch.See(Panel(0), TargetStance.Unread, T0.AddSeconds(i), Looks(0.95)).Select(c => c.Text));

        Assert.DoesNotContain("Target critical", said);
        Assert.Equal("critical", watch.Picture(T0.AddSeconds(4)).Target!.Hull);
    }

    [Fact]
    public void The_hull_band_waits_for_three_reads()
    {
        var watch = new CombatWatch(Book);
        watch.See(Panel(200), TargetStance.Unread, T0, Looks(0.5));
        watch.See(Panel(200), TargetStance.Unread, T0.AddSeconds(1), Looks(0.5));
        Assert.Null(watch.Picture(T0.AddSeconds(1)).Target!.Hull);

        watch.See(Panel(200), TargetStance.Unread, T0.AddSeconds(2), Looks(0.5));
        Assert.Equal("damaged", watch.Picture(T0.AddSeconds(2)).Target!.Hull);
    }

    // ---- the NPC ships the log names ----

    [Fact]
    public void Npc_ships_are_read_from_any_line_that_names_one()
    {
        var nearby = new NearbyHulls();
        var at = new DateTimeOffset(2026, 11, 6, 2, 30, 0, TimeSpan.Zero);

        // Lines from the 2026-11-05 session's log, shortened.
        nearby.Line("[Notice] <CEntity::OnOwnerRemoved - Item> Force detaching ITEM id = 916483000198 name = COOL_AEGS_S02_Arctic_SCItem_916483000198 to unblock removal of parent id = 916483015360 name = SCItem_Debris_916483015360_CRUS_Starfighter_Ion_PU_AI_CRIM_916483000013 [Team_EntitySystemTech]", at);
        nearby.Line("CPhysicalProxy::OnPhysicsPostStep is trying to set position in the grid (RSI_Scorpius_Antares_PU_AI_CRIM_916483015387) that's different from the grid of the current Zone (tsg_gascloud_006) the Entity is in: PU_Pilots-Human-Criminal-Pilot_Light_916483015706", at);
        nearby.Line("Max Impacts: 'VNCL_Glaive_PU_AI_VAN_916483121253' | ('1') : Number Of Impacts: '1' , Culled Impacts:'' [Team_VFX][VFX]", at);
        nearby.Line("name = SCItem_Debris_916483255900_RSI_Perseus_AI_Super_Crim_916483255771 [Team_EntitySystemTech]", at);

        Assert.Equal(["CRUS_Starfighter_Ion", "RSI_Perseus", "RSI_Scorpius_Antares", "VNCL_Glaive"], nearby.Npcs(at).Order());
        Assert.Equal(nearby.Npcs(at).Order(), nearby.Classes(at).Order());
        Assert.Empty(nearby.Npcs(at.AddMinutes(21)));
    }

    [Fact]
    public void A_streamed_in_hull_is_nearby_but_not_an_npc()
    {
        var nearby = new NearbyHulls();
        var at = new DateTimeOffset(2026, 11, 6, 2, 30, 0, TimeSpan.Zero);
        nearby.Line("[Notice] <ATC_Reset_VehiclesBeingStoredCleared> ATC 'ANVL_Paladin_ShipATCDataManager_914901457784' [914901457784] clearing 0 entries", at);

        Assert.Equal(["ANVL_Paladin"], nearby.Classes(at));
        Assert.Empty(nearby.Npcs(at));
    }

    [Fact]
    public void An_npc_in_the_log_outweighs_a_hull_merely_streamed_in()
    {
        var book = ShipSignatureBook.Of(
        [
            new("Vanduul Blade", 10418, "VNCL_Blade"), new("Origin 85X Limited", 10444, "ORIG_85X_Limited"),
        ]);

        // Both streamed in; only the 85X named as an NPC.
        var odds = book.Weigh(10400, null, new HashSet<string> { "VNCL_Blade", "ORIG_85X" },
            npcs: new HashSet<string> { "ORIG_85X_Limited" });

        Assert.Equal("Origin 85X Limited", odds[0].Name);
        Assert.Contains("NPC in the log", odds[0].Because);
        Assert.Contains("seen nearby", odds[1].Because);
    }
}
