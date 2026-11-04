using System.Xml.Linq;
using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// How a crate preset set down in a building module becomes a named place.
/// The shapes are this install's: Hurston's container lists its distribution
/// centres under location entities that carry a star-map record, and its caves
/// under ones that do not.
/// </summary>
public class LootPlacesTests
{
    private static readonly Guid Dupree = Guid.Parse("e188371f-7aaa-4ed3-b396-294dc874119a");
    private static readonly Guid Olliff = Guid.Parse("ecbe605d-19a8-4712-b415-f08f6a0e8875");

    /// <summary>
    /// A child placed inside a location entity is under that entity's marker;
    /// the entity itself names no file, so it is not a child of its own.
    /// </summary>
    [Fact]
    public void A_child_inherits_the_marker_of_the_location_it_sits_under()
    {
        var doc = XDocument.Parse($"""
            <ObjectContainer name="stanton1">
             <ChildObjectContainers>
              <Child name="" entityName="s1_dc_dpre_imf" class="RastarLocationEntity" starMapRecord="{Dupree}">
               <ChildObjectContainers>
                <Child name="Data/objectcontainers/pu/loc/mod/ugf/util/layout/dc_final/final_s1_dc_dpre_imf.socpak" entityName="final_s1_dc_dpre_imf" />
               </ChildObjectContainers>
              </Child>
              <Child name="" entityName="Stanton1_sand01_unoc_001_size05_004_001" class="RastarLocationEntity">
               <ChildObjectContainers>
                <Child name="objectcontainers/pu/loc/mod/cave/sand01/system/sand01_unoc_001_size05_004.socpak" />
               </ChildObjectContainers>
              </Child>
             </ChildObjectContainers>
            </ObjectContainer>
            """);

        var children = GameLootPlaces.Children(doc).ToList();

        Assert.Equal(2, children.Count);
        Assert.Equal((@"data\objectcontainers\pu\loc\mod\ugf\util\layout\dc_final\final_s1_dc_dpre_imf.socpak", Dupree), children[0]);
        Assert.Equal((@"data\objectcontainers\pu\loc\mod\cave\sand01\system\sand01_unoc_001_size05_004.socpak", null), children[1]);
    }

    [Fact]
    public void An_instance_names_its_preset_by_single_or_multi_reference()
    {
        var multi = Guid.Parse("b6d5d91f-3aca-4e31-bdcf-2ca347554b0a");
        var entxml = System.Text.Encoding.UTF8.GetBytes($"""
            <Entity Name="ht_xs_003a_ext-007" EntityClass="HarvestableStreamableObjectContainer">
              <PropertiesDataCore>
                <HarvestableComponent __type="HarvestableParams">
                  <looseSubConfigBase __type="SubHarvestableMultiConfigRef" multiConfigRef="{multi}" />
                </HarvestableComponent>
                <Elsewhere subConfigRef="{Guid.NewGuid()}" />
              </PropertiesDataCore>
            </Entity>
            """);

        // Only the harvestable component counts: a reference elsewhere in the
        // instance is some other system's.
        Assert.Equal([multi], GameLootPlaces.PresetsSet(entxml));
    }

    [Fact]
    public void A_broken_instance_file_sets_nothing()
    {
        Assert.Empty(GameLootPlaces.PresetsSet([0x43, 0x72, 0x79, 0x58]));
    }

    /// <summary>
    /// A module is walked up until a marker or a body. The distribution
    /// centre stops at its marker; the cave interior goes through its cave and
    /// lands on Pyro I and on Hurston as unmarked spots; a module nothing
    /// places goes nowhere rather than everywhere.
    /// </summary>
    [Fact]
    public void A_module_walks_up_to_its_marker_or_its_body()
    {
        const string hurston = @"data\objectcontainers\pu\system\stanton\stanton1.socpak";
        const string pyro1 = @"data\objectcontainers\pu\system\pyro\pyro1.socpak";
        const string dc = @"data\objectcontainers\pu\loc\mod\dc\final_s1_dc_dpre_imf.socpak";
        const string cave = @"data\objectcontainers\pu\loc\mod\cave\sand01_unoc_001_size06_001.socpak";
        const string caveInside = @"data\objectcontainers\pu\loc\mod\cave\sand01_unoc_001_size06_001_int.socpak";
        const string ugf = @"data\objectcontainers\pu\loc\mod\ugf\ugf_lgcy_structure_002.socpak";

        var containers = new List<(string, IEnumerable<(string, Guid?)>)>
        {
            (hurston, [(dc, Dupree), (cave, null), (ugf, Olliff)]),
            (pyro1, [(cave, null)]),
            (cave, [(caveInside, null)]),
        };

        var walk = GameLootPlaces.Walker(
            containers,
            id => id == Dupree || id == Olliff,
            path => path.EndsWith(@"\stanton1.socpak") ? "Stanton1" : path.EndsWith(@"\pyro1.socpak") ? "Pyro1" : null);

        Assert.Equal([new GameLootPlaces.PlaceKey(Dupree, null)], walk(dc));
        Assert.Equal([new GameLootPlaces.PlaceKey(Olliff, null)], walk(ugf));
        Assert.Equal(
            new HashSet<GameLootPlaces.PlaceKey> { new(null, "Stanton1"), new(null, "Pyro1") },
            walk(caveInside));
        Assert.Empty(walk(@"data\objectcontainers\pu\loc\mod\unused.socpak"));
    }

    [Fact]
    public void A_loop_in_the_container_graph_ends()
    {
        var walk = GameLootPlaces.Walker(
            [("a", [("b", null)]), ("b", [("a", null)])],
            _ => false,
            _ => null);

        Assert.Empty(walk("a"));
    }

    [Theory]
    [InlineData("Data/objectcontainers/pu/loc/mod/x.socpak", @"data\objectcontainers\pu\loc\mod\x.socpak")]
    [InlineData("objectcontainers/pu/loc/mod/x.socpak", @"data\objectcontainers\pu\loc\mod\x.socpak")]
    [InlineData(@"Data\ObjectContainers\PU\loc\mod\X.socpak", @"data\objectcontainers\pu\loc\mod\x.socpak")]
    public void Paths_compare_however_the_files_spell_them(string path, string expected)
    {
        Assert.Equal(expected, GameLootPlaces.Normalise(path));
    }

    /// <summary>
    /// Markers sharing a name and a body are one row, whatever order the
    /// archive lists them in. The LIVE delta patch of 2026-11-04 reordered
    /// the archive without changing its data, and the row's id moved with it.
    /// </summary>
    [Fact]
    public void A_merged_place_keeps_one_id_whatever_order_the_archive_lists_it_in()
    {
        LootPlace[] outposts =
        [
            new("Derelict Outpost", "Monox", 13, "Pyro2_Outpost_col_m_drlct_otpst_occ_003"),
            new("Derelict Outpost", "Monox", 13, "Pyro2_Outpost_col_m_drlct_otpst_occ_001"),
            new("Derelict Outpost", "Monox", 13, null),
        ];

        var listed = GameLootPlaces.Merge(outposts);
        var reordered = GameLootPlaces.Merge(outposts.Reverse());

        var place = Assert.Single(listed);
        Assert.Equal(39, place.Spots);
        Assert.Equal("Pyro2_Outpost_col_m_drlct_otpst_occ_001", place.Id);
        Assert.Equal(listed, reordered);
    }

    /// <summary>A spot whose marker has no id still counts; it only cannot name the row.</summary>
    [Fact]
    public void A_place_with_no_id_anywhere_is_kept_without_one()
    {
        var place = Assert.Single(GameLootPlaces.Merge([new(null, "Monox", 4, null), new(null, "Monox", 2, null)]));

        Assert.Equal(6, place.Spots);
        Assert.Null(place.Id);
    }
}
