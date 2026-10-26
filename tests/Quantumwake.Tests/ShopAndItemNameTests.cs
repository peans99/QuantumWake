using Quantumwake.Core.GameData;
using Quantumwake.Core.State;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// Shops and items that reached the Stats page as their ids. Every id here was
/// logged on a 4.10.197 install; the key shapes are copied from its global.ini.
/// </summary>
public class ShopAndItemNameTests
{
    /// <summary>
    /// 22 of the 50 shop ids logged matched no <c>shop_name_</c> key. The food
    /// carts are named in the game's own words, filed under area names.
    /// </summary>
    [Theory]
    [InlineData("SCShop_Pyro_RStop_stall_med_001_rund_weaponsmith", "Weaponsmith Stall")]
    [InlineData("Pyro RStop stall med 001 rund weaponsmith", "Weaponsmith Stall")]
    [InlineData("SCShop_Pyro_RStop_base_stall_sml_001_friedgrub", "Fried Grub Stall")]
    [InlineData("SCShop_stall_rdwn_small_10_conveniance", "Convenience Stall")]
    [InlineData("SCShop_Pyro_RStop_stall_sml_rund_008_Chuan", "Chuan Cart")]
    [InlineData("SCShop_HotDogBar_Food_RestStop", "Hot Dogs")]
    [InlineData("SCShop_NoodleBar_A_Food_RestStop", "Noodle Stall")]
    [InlineData("SCShop_NoodleBar_B_Food_RestStop", "Noodle Shop")]
    [InlineData("SCShop_Ellroys_Food_RestStop", "Ellroy's Coffee")]
    [InlineData("SCShop_burrito_cart_a", "Burritos")]
    [InlineData("SCShop_Admin_lt_base_g", "Cargo terminal")]
    [InlineData("SCShop_AdminOffice_NewBabbage", "Cargo terminal")]
    [InlineData("SCShop_Pyro_RestStop_Rund_Admin", "Cargo terminal")]
    [InlineData("SCShop_CargoOfficePyroStation", "Cargo Services")]
    [InlineData("SCShop_Pyro_RestStop_BlackMarket_FPSItems", "Black Market")]
    [InlineData("SCShop_Levski_Refinery_Store", "Refinery Store")]
    [InlineData("SCShop_XS_MiningStall", "Mining Stall")]
    [InlineData("SCShop_FW_Rentals", "Ship Rentals")]
    [InlineData("SCShop_ht_delta_rayari_m_store", "Rayari Store")]
    public void Describes_shops_the_brand_table_does_not_name(string shop, string name) =>
        Assert.Equal(name, ShopNames.Describe(shop));

    [Fact]
    public void Leaves_a_shop_it_cannot_read_to_the_caller() =>
        Assert.Null(ShopNames.Describe("SCShop_SomethingNew_Lorville"));

    /// <summary>
    /// The game's key is misspelt, so the one name it does publish for this
    /// shop never matched the id - 26 visits on this install.
    /// </summary>
    [Fact]
    public void Matches_a_brand_the_game_misspells()
    {
        var names = GameNames.Parse("""
            shop_name_conscientousobjects=Conscientious Objects
            shop_name_casaba=Casaba Outlet
            """);

        Assert.Equal("Conscientious Objects", names.Shop("SCShop_ConscientiousObjects_Levski-001"));
        Assert.Equal("Casaba Outlet", names.Shop("SCShop_lt_a_casaba_small_base_a-002"));
    }

    [Theory]
    [InlineData("conscientiousobjects", "conscientousobjects", true)]
    [InlineData("livefireweapons", "livefireweapon", true)]
    [InlineData("pharmacy", "pharmacys", false)] // too short to trust one letter
    [InlineData("crusaderindustries", "crusaderindustriez", false)] // a swap, not a drop
    public void Allows_one_dropped_letter_in_a_long_word(string a, string b, bool same) =>
        Assert.Equal(same, GameNames.Misspelt(a, b));

    /// <summary>
    /// The item catalogue's text table kept ",P" on the key, so it never found
    /// the name the game files there.
    /// </summary>
    [Fact]
    public void Reads_names_filed_under_a_variant_key()
    {
        var text = GameCommodities.EnglishText("""
            item_Name_cds_combat_medium_helmet_01_02_01,P=<EM3>ORC-mkX Helmet SecondWind [*]</EM3>
            item_Name_cds_combat_medium_arms_01_02_01=<EM3>ORC-mkX Arms SecondWind [M*]</EM3>
            """);

        Assert.Equal("<EM3>ORC-mkX Helmet SecondWind [*]</EM3>", text["item_Name_cds_combat_medium_helmet_01_02_01"]);
        Assert.True(text.ContainsKey("item_Name_cds_combat_medium_arms_01_02_01"));
    }

    private static readonly DateTimeOffset At = new(2026, 10, 9, 1, 19, 21, TimeSpan.Zero);

    private static string Label(string shop) => ShopNames.Describe(shop) ?? shop;

    /// <summary>The builder stores the shop spaced out; the feed puts the name in.</summary>
    [Fact]
    public void The_feed_names_the_shop_in_a_purchase_line()
    {
        var entry = LiveSessionService.ShopNamed(
            new TimelineEntry(At, "bought", "Bought gmni_pistol_ballistic_01",
                "977 aUEC · Pyro RStop stall med 001 rund weaponsmith", "gmni_pistol_ballistic_01"),
            Label);

        Assert.Equal("977 aUEC · Weaponsmith Stall", entry.Detail);
        Assert.Equal("Bought gmni_pistol_ballistic_01", entry.Text);
    }

    [Fact]
    public void The_feed_names_the_terminal_in_a_cargo_sale()
    {
        var entry = LiveSessionService.ShopNamed(
            new TimelineEntry(At, "sold", "Sold 64 SCU", "185,728 aUEC · Admin lt base g"), Label);

        Assert.Equal("185,728 aUEC · Cargo terminal", entry.Detail);
    }

    [Fact]
    public void The_feed_leaves_other_lines_and_unknown_shops_alone()
    {
        var contract = new TimelineEntry(At, "contract", "Contract accepted", "Red Wind · Ruin Station");
        var unknown = new TimelineEntry(At, "bought", "Bought x", "10 aUEC · SomethingNew Lorville");

        Assert.Same(contract, LiveSessionService.ShopNamed(contract, Label));
        Assert.Same(unknown, LiveSessionService.ShopNamed(unknown, Label));
    }

    /// <summary>
    /// The beams inside a fitted flashlight sit on light_1 and light_2. The
    /// flashlight itself is the equipment; the beam has no name anywhere.
    /// </summary>
    [Theory]
    [InlineData("light_1", false)]
    [InlineData("light_2", false)]
    [InlineData("underbarrel_attach", true)]
    [InlineData("helmet_light", true)]
    public void Leaves_flashlight_beams_out_of_the_loadout(string port, bool equipment) =>
        Assert.Equal(equipment, LoadoutCategories.IsEquipment(port));
}
