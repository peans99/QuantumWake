using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Ship part descriptions that gain the figures the game leaves out of them.
/// </summary>
/// <remarks>
/// The descriptions are this install's own, out of its global.ini; the figures
/// are what <see cref="GameShipParts"/> reads for those parts, and each one was
/// checked against the community dataset (see the reader's remarks).
/// </remarks>
public class PartFiguresOverlayTests
{
    private static readonly Dictionary<string, ShipPartStats> Parts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SHLD_BEHR_S02_7MA_SCItem"] = new("SHLD_BEHR_S02_7MA_SCItem", "Shield", ShieldHp: 10000, ShieldRegen: 2200, DamagedDelay: 4.58, DownedDelay: 9.17),
        ["COOL_AEGS_S01_Bracer_SCItem"] = new("COOL_AEGS_S01_Bracer_SCItem", "Cooler", CoolingOutput: 34),
        ["QDRV_RSI_S01_Eos_SCItem"] = new("QDRV_RSI_S01_Eos_SCItem", "QuantumDrive", QuantumSpeed: 199272700, SpoolTime: 5.1, QuantumCooldown: 10.8),
        ["BEHR_LaserCannon_S2"] = new("BEHR_LaserCannon_S2", "WeaponGun", Alpha: 273.4, DamageKind: "Energy", RoundsPerMinute: 100, Dps: 455.6, ProjectileSpeed: 1196, Range: 2165),
        ["BEHR_BallisticRepeater_S1"] = new("BEHR_BallisticRepeater_S1", "WeaponGun", Alpha: 18, DamageKind: "Physical", ProjectileSpeed: 1600, Range: 2400),
        ["MISL_S02_CS_FSKI_Tempest"] = new("MISL_S02_CS_FSKI_Tempest", "Missile", MissileDamage: 1700, MissileSpeed: 1029, LockTime: 0.1, LockRangeMin: 1800, LockRangeMax: 10000, Tracking: "CrossSection"),
    };

    private const string Vanilla =
        "item_DescSHLD_BEHR_S02_7MA=Item Type: Shield Generator\\nManufacturer: Behring \\nSize: 2\\nGrade: A\\nClass: Military\\n\\nBehring's 7MA 'Lorica' holds.\n" +
        "item_DescCOOL_AEGS_S01_Bracer=Item Type: Cooler\\nManufacturer: Aegis Dynamics \\nSize: 1\\nGrade: C\\nClass: Military\\n\\nFor those looking for a solid cooler.\r\n" +
        "item_DescQDRV_RSI_S01_Eos=Item Type: Quantum Drive\\nManufacturer: RSI \\nSize: 1\\nGrade: C\\nClass: Civilian\\n\\nThe Eos.\n" +
        "item_DescBEHR_LaserCannon_S2=Manufacturer: Behring\\nItem Type: Laser Cannon\\nSize: 2\\n\\nThe M4A is Behring's second tier laser autocannon.\n" +
        "item_DescBEHR_BallisticRepeater_S1=Manufacturer: Behring\\nItem Type: Ballistic Repeater\\nSize: 1\\n\\nA repeater.\n" +
        "item_DescMISL_S02_CS_FSKI_Tempest=Manufacturer: Firestorm Kinetics\\nTracking Signal: Cross Section\\nSize: 2\\n\\nThe Tempest.\n" +
        "item_NameCOOL_AEGS_S01_Bracer=Bracer\n" +
        "item_DescUnknown_Part=Item Type: Cooler\\n\\nNothing read for this one.\n";

    private static TextOverlayPlan Build(TextOverlayOptions? options = null, string? ini = null) =>
        TextOverlay.Build(ini ?? Vanilla, _ => true, null, options, parts: Parts);

    private static string Line(TextOverlayPlan plan, string key) =>
        plan.Content.Split('\n').Single(l => l.StartsWith(key + "=", StringComparison.Ordinal));

    [Fact]
    public void A_shield_gains_its_pool_regeneration_and_delays_at_the_end_of_the_games_block()
    {
        Assert.Equal(
            "item_DescSHLD_BEHR_S02_7MA=Item Type: Shield Generator\\nManufacturer: Behring \\nSize: 2\\nGrade: A\\nClass: Military" +
            "\\nShield HP: 10,000\\nRegen Rate: 2,200/s\\nRegen Delay: 4.58 s (Downed: 9.17 s)" +
            "\\n\\nBehring's 7MA 'Lorica' holds.",
            Line(Build(), "item_DescSHLD_BEHR_S02_7MA"));
    }

    /// <summary>The table's entries are one a line: a figure joins with the two characters, never a real newline.</summary>
    [Fact]
    public void The_figures_add_no_lines_to_the_table()
    {
        var plan = Build();
        Assert.Equal(Vanilla.Split('\n').Length, plan.Content.Split('\n').Length);
    }

    [Fact]
    public void A_line_ending_after_the_entry_is_kept()
    {
        Assert.EndsWith("Class: Military\\nCooling Output: 34\\n\\nFor those looking for a solid cooler.\r", Line(Build(), "item_DescCOOL_AEGS_S01_Bracer"));
    }

    [Fact]
    public void A_quantum_drive_reads_in_kilometres_a_second()
    {
        Assert.Contains("\\nQuantum Speed: 199,273 km/s\\nSpool Time: 5.1 s\\nCooldown: 10.8 s\\n\\n", Line(Build(), "item_DescQDRV_RSI_S01_Eos"));
    }

    [Fact]
    public void A_gun_says_its_shot_rate_damage_a_second_and_reach()
    {
        Assert.Contains("Size: 2\\nDamage: 273.4 Energy\\nFire Rate: 100 rpm\\nDPS: 455.6\\nProjectile Speed: 1,196 m/s\\nRange: 2,165 m\\n\\nThe M4A",
            Line(Build(), "item_DescBEHR_LaserCannon_S2"));
    }

    /// <summary>
    /// A gun whose rate the files leave open is not given one - nor a damage a
    /// second, which would be wrong by the same factor.
    /// </summary>
    [Fact]
    public void A_gun_with_no_settled_rate_says_no_rate_and_no_damage_a_second()
    {
        var line = Line(Build(), "item_DescBEHR_BallisticRepeater_S1");
        Assert.Contains("Damage: 18 Physical", line);
        Assert.DoesNotContain("Fire Rate", line);
        Assert.DoesNotContain("DPS", line);
    }

    [Fact]
    public void A_missile_says_its_lock_and_not_the_tracking_signal_the_game_already_prints()
    {
        var line = Line(Build(), "item_DescMISL_S02_CS_FSKI_Tempest");
        Assert.Contains("Size: 2\\nDamage: 1,700\\nSpeed: 1,029 m/s\\nLock Time: 0.1 s\\nLock Range: 1,800-10,000 m\\n\\nThe Tempest.", line);
        Assert.Equal(2, line.Split("Tracking Signal").Length);
    }

    /// <summary>
    /// A base table that already says a figure - another mod's, or a patch's -
    /// does not get it a second time; the figures it lacks are still added.
    /// </summary>
    [Fact]
    public void A_label_the_description_already_has_is_not_written_twice()
    {
        const string modded = "item_DescCOOL_AEGS_S01_Bracer=Item Type: Cooler\\nCooling Output: 34\\n\\nAlready says it.\n" +
            "item_DescSHLD_BEHR_S02_7MA=Item Type: Shield\\nShield HP: 10000\\n\\nHalf said.\n";
        var plan = Build(ini: modded);

        Assert.Equal("item_DescCOOL_AEGS_S01_Bracer=Item Type: Cooler\\nCooling Output: 34\\n\\nAlready says it.", Line(plan, "item_DescCOOL_AEGS_S01_Bracer"));
        Assert.Equal("item_DescSHLD_BEHR_S02_7MA=Item Type: Shield\\nShield HP: 10000\\nRegen Rate: 2,200/s\\nRegen Delay: 4.58 s (Downed: 9.17 s)\\n\\nHalf said.",
            Line(plan, "item_DescSHLD_BEHR_S02_7MA"));
    }

    [Fact]
    public void Building_twice_on_its_own_output_changes_nothing()
    {
        var once = Build().Content;
        Assert.Equal(once, Build(ini: once).Content);
    }

    [Fact]
    public void A_part_nothing_was_read_for_and_a_name_are_left_alone()
    {
        var plan = Build();
        Assert.Equal("item_DescUnknown_Part=Item Type: Cooler\\n\\nNothing read for this one.", Line(plan, "item_DescUnknown_Part"));
        Assert.Equal("item_NameCOOL_AEGS_S01_Bracer=Bracer", Line(plan, "item_NameCOOL_AEGS_S01_Bracer"));
    }

    [Fact]
    public void Every_described_part_is_counted_and_listed_under_one_category()
    {
        var plan = Build();
        Assert.Equal(6, plan.Described);
        var listed = plan.Changes.Where(c => c.Category == TextOverlay.PartStatsCategory).ToList();
        Assert.Equal(6, listed.Count);
        Assert.Contains(listed, c => c.ItemClass == "COOL_AEGS_S01_Bracer" && c.Becomes == "Cooling Output: 34");
    }

    [Fact]
    public void Turning_the_figures_off_leaves_every_description_as_it_was()
    {
        var plan = Build(new TextOverlayOptions(Stats: false));
        Assert.Equal(0, plan.Described);
        Assert.Equal(Vanilla, plan.Content);
    }

    private const string Shared =
        "item_DescKLWE_LaserRepeater_S3=Item Type: Laser Repeater\\nSize: 3\\n\\nThe Panther.\n" +
        "item_DescStock_Cannon=Item Type: Laser Cannon\\nSize: 7\\n\\nA big gun.\n" +
        "item_DescNobody_Owns=Item Type: Laser Cannon\\nSize: 8\\n\\nTwo guns, two answers.\n" +
        "item_DescBengal_BallisticCannon_S7=Item Type: Ballistic Cannon\\n\\nUnused.\n";

    private static ShipPartStats Gun(string cls, double alpha, string key) =>
        new(cls, "WeaponGun", Alpha: alpha, DamageKind: "Energy", ProjectileSpeed: 1480, Range: 1924, DescriptionKey: key);

    private static TextOverlayPlan BuildShared(params ShipPartStats[] parts) =>
        TextOverlay.Build(Shared, _ => true, null, null, parts: parts.ToDictionary(p => p.Class, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// A part's description is under the key its entity names, which need not
    /// be its class: a Bengal's cannon reads another gun's text.
    /// </summary>
    [Fact]
    public void A_part_is_matched_by_the_description_key_it_names_not_by_its_class()
    {
        var plan = BuildShared(Gun("Bengal_BallisticCannon_S7", 9000, "item_DescStock_Cannon"));

        Assert.Contains("Damage: 9,000 Energy", Line(plan, "item_DescStock_Cannon"));
        Assert.Equal("item_DescBengal_BallisticCannon_S7=Item Type: Ballistic Cannon\\n\\nUnused.", Line(plan, "item_DescBengal_BallisticCannon_S7"));
    }

    [Fact]
    public void Parts_sharing_a_description_and_agreeing_fill_it()
    {
        var plan = BuildShared(Gun("A_Cannon", 500, "item_DescStock_Cannon"), Gun("B_Cannon", 500, "item_DescStock_Cannon"));
        Assert.Contains("Damage: 500 Energy", Line(plan, "item_DescStock_Cannon"));
    }

    /// <summary>
    /// Two guns under one text that would say different things leave it as the
    /// game wrote it: either figure would be wrong for the other gun.
    /// </summary>
    [Fact]
    public void Parts_sharing_a_description_and_disagreeing_leave_it_alone()
    {
        var plan = BuildShared(Gun("A_Cannon", 500, "item_DescNobody_Owns"), Gun("B_Cannon", 900, "item_DescNobody_Owns"));
        Assert.Equal("item_DescNobody_Owns=Item Type: Laser Cannon\\nSize: 8\\n\\nTwo guns, two answers.", Line(plan, "item_DescNobody_Owns"));
        Assert.Equal(0, plan.Described);
    }

    /// <summary>The Panther keeps its own text when a turret copy that borrows it fires differently.</summary>
    [Fact]
    public void The_part_a_description_is_named_for_keeps_it_over_a_copy_that_borrows_it()
    {
        var plan = BuildShared(
            Gun("KLWE_LaserRepeater_S3", 43.7, "item_DescKLWE_LaserRepeater_S3"),
            Gun("KLWE_LaserRepeater_S3_Turret", 60, "item_DescKLWE_LaserRepeater_S3"));
        Assert.Contains("Damage: 43.7 Energy", Line(plan, "item_DescKLWE_LaserRepeater_S3"));
    }

    /// <summary>A preference stored before the switch existed reads as on, the way the others did when they came.</summary>
    [Fact]
    public void A_stored_preference_without_the_switch_reads_it_as_on()
    {
        var stored = System.Text.Json.JsonSerializer.Deserialize<TextOverlayOptions>("{\"Colour\":false,\"Level\":3,\"Facts\":true,\"Routes\":true,\"Rewards\":true}")!;
        Assert.True(stored.Stats);
    }
}
