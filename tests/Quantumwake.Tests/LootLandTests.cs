using System.Xml.Linq;
using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// 5.0's Starchitect places on Nyx I: each carries its loot preset on its own
/// entity and sits in a faction's land, which decides what that preset rolls.
/// The shapes are <c>nyx1.socpak</c>'s on 5.0 PTU 13284307.
/// </summary>
public class LootLandTests
{
    private static readonly Guid GenericHcd = Guid.Parse("72f8fc98-fee4-4068-a780-7fc08b3b5717");
    private static readonly Guid Shubin = Guid.Parse("3a1b12b2-74c6-4148-b147-65f0108a318c");
    private static readonly Guid Moraine = Guid.Parse("7d0b0d39-9d1e-4f1a-9a37-6c2f4b0a1e11");
    private static readonly Guid HappyCrest = Guid.Parse("400285e1-ef49-a8b2-c78c-a4cf719d6a83");
    private static readonly Guid Settlement = Guid.Parse("41d45b60-6fde-2176-c07c-20840a0c4a86");
    private static readonly Guid Campsite = Guid.Parse("4168636f-2bc6-1135-39d3-7a0ca1c23b91");
    private static readonly Guid Tile = Guid.Parse("411ea90f-fc31-7197-5224-699885cee3a9");

    /// <summary>
    /// The place's own entity names its preset as <c>SubConfigGUID</c>, not
    /// the crates' <c>subConfigRef</c>: read as a crate, all 1,230 Nyx I
    /// places came out with no loot at all.
    /// </summary>
    [Fact]
    public void A_place_names_its_own_preset_and_is_not_read_as_a_crate()
    {
        var doc = XDocument.Parse($"""
            <Entity Name="location_ht_a_habitation_s_003_high (Shubin Settlement DRL-444)" EntityClass="MissionLocationTemplate" EntityCryGUID="{Settlement}">
              <PropertiesDataCore __fullyInstanced="0" __version="2" />
              <HarvestableComponent SubConfigGUID="{GenericHcd}" />
              <HarvestableComponent SubConfigGUID="{GenericHcd}" />
            </Entity>
            """);

        var own = GameLootPlaces.OwnPreset(doc);

        Assert.Equal((Settlement, GenericHcd, "Shubin Settlement DRL-444"), own);
        Assert.Empty(GameLootPlaces.PresetsSet(doc));
    }

    /// <summary>A crate's own preset is still read the old way, and is no place's.</summary>
    [Fact]
    public void A_crate_is_not_taken_for_a_place()
    {
        var doc = XDocument.Parse($"""
            <Entity Name="Lootable_Container_Small" EntityCryGUID="{Tile}">
              <PropertiesDataCore>
                <HarvestableComponent __type="HarvestableComponentParams">
                  <looseSubConfigBase subConfigRef="{GenericHcd}" />
                </HarvestableComponent>
              </PropertiesDataCore>
            </Entity>
            """);

        Assert.Null(GameLootPlaces.OwnPreset(doc));
        Assert.Equal([GenericHcd], GameLootPlaces.PresetsSet(doc));
    }

    /// <summary>
    /// The number after the bracket tells the editor's copies apart: the
    /// 94 campsites all read "Campsite", so they merge into one row.
    /// </summary>
    [Theory]
    [InlineData("location_ht_a_habitation_s_003_high (Shubin Settlement DRL-444)", "Shubin Settlement DRL-444")]
    [InlineData("location_ht_a_campsite_s_001 (Campsite)-046", "Campsite")]
    [InlineData("location_ht_a_game_logic_xs_004_remote (Unoccupied Territory)-093", "Unoccupied Territory")]
    [InlineData("tile_ht_a_cover_005m_906", null)]
    [InlineData("location_ht_a_odd ()", null)]
    public void A_place_is_named_by_its_bracket(string entityName, string? expected) =>
        Assert.Equal(expected, GameLootPlaces.PlaceName(entityName));

    /// <summary>
    /// A cluster's land is not its places' land: Happy Crest is Shubin's and
    /// holds a Moraine campsite. The nearest land wins; a place without its own
    /// takes the one above it.
    /// </summary>
    [Fact]
    public void A_place_takes_its_own_land_before_the_cluster_it_sits_in()
    {
        var doc = XDocument.Parse($"""
            <ObjectContainer name="nyx1">
             <ChildObjectContainers>
              <Child name="" entityName="cluster_ht_a_habitation_l_007_remote_mining_high (Happy Crest)" guid="{HappyCrest}" hasHierarchicalContextData="1">
               <ChildObjectContainers>
                <Child name="" entityName="location_ht_a_habitation_s_003_high (Shubin Settlement DRL-444)" guid="{Settlement}" />
                <Child name="" entityName="location_ht_a_campsite_s_001 (Campsite)-046" guid="{Campsite}" hasHierarchicalContextData="1">
                 <ChildObjectContainers>
                  <Child name="Data/objectcontainers/pu/starchitect/tile/encounter/campsite/tile_encounter_campsite_020m_003.socpak" guid="{Tile}" />
                 </ChildObjectContainers>
                </Child>
               </ChildObjectContainers>
              </Child>
             </ChildObjectContainers>
            </ObjectContainer>
            """);

        var parents = GameLootPlaces.Nesting(doc).ToDictionary(n => n.Child, n => n.Parent);
        var lands = new Dictionary<Guid, Guid> { [HappyCrest] = Shubin, [Campsite] = Moraine };

        Assert.Equal(Shubin, GameLootPlaces.LandOf(Settlement, lands, parents));
        Assert.Equal(Moraine, GameLootPlaces.LandOf(Campsite, lands, parents));
        Assert.Equal(Moraine, GameLootPlaces.LandOf(Tile, lands, parents));
        Assert.Null(GameLootPlaces.LandOf(Guid.NewGuid(), lands, parents));
    }

    /// <summary>A loop in the nesting, which no file has, ends rather than spinning.</summary>
    [Fact]
    public void A_loop_in_the_nesting_finds_no_land()
    {
        var parents = new Dictionary<Guid, Guid> { [Settlement] = Campsite, [Campsite] = Settlement };
        Assert.Null(GameLootPlaces.LandOf(Settlement, new Dictionary<Guid, Guid>(), parents));
    }

    /// <summary>The land changes what the preset rolls, so the page names it.</summary>
    [Theory]
    [InlineData("V3SlotPreset_Loot_Generic_HCD@Faction_HCD_Shubin", "Loot · Generic · HCD · Shubin land")]
    [InlineData("V3SlotPreset_Loot_Generic_HCD@Faction_HCD_ShatteredBlade", "Loot · Generic · HCD · Shattered Blade land")]
    [InlineData("V3SlotPreset_Loot_Generic_HCD@Faction_HCD_GenericLawful", "Loot · Generic · HCD · Generic Lawful land")]
    [InlineData("V3SlotPreset_Loot_Generic_HCD", "Loot · Generic · HCD")]
    public void A_land_is_named_beside_its_preset(string preset, string label) =>
        Assert.Equal(label, LootLabels.Preset(preset));
}
