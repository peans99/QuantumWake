using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Reading an NPC loadout file, in the shape this install's decrypted
/// <c>Lorville_Guard_Heavy.xml</c> has.
/// </summary>
public class NpcOutfitTests
{
    [Fact]
    public void A_loadout_file_names_every_item_at_any_depth()
    {
        var xml = System.Text.Encoding.UTF8.GetBytes("""
            <Loadout>
              <Items>
                <Item portName="Body_ItemPort" itemName="" tag="Char_Body">
                  <Items>
                    <Item portName="Armor_Undersuit" itemName="cds_undersuit_01_01_12" tag="Char_Armor_Undersuit">
                      <Items>
                        <Item portName="Armor_Helmet" itemName="cds_legacy_armor_heavy_helmet_01_01_hd_sec" tag="Char_Armor_Helmet" />
                        <Item portName="Armor_Torso" itemName="cds_legacy_armor_heavy_core_01_01_hd_sec" tag="Char_Armor_Torso" />
                      </Items>
                    </Item>
                    <Item portName="mobiglas_attach" itemName="MobiGlas" tag="MobiGlas" />
                  </Items>
                </Item>
              </Items>
              <Quality wear="0.5" dirt="0.60000002" />
            </Loadout>
            """);

        Assert.Equal(
            ["cds_undersuit_01_01_12", "cds_legacy_armor_heavy_helmet_01_01_hd_sec", "cds_legacy_armor_heavy_core_01_01_hd_sec", "MobiGlas"],
            GameNpcOutfits.ItemsOf(xml));
    }

    [Fact]
    public void An_unreadable_file_names_nothing_rather_than_failing_the_read()
    {
        Assert.Null(GameNpcOutfits.ItemsOf([0x28, 0xB5, 0x2F, 0xFD, 0x00]));
    }

    [Theory]
    [InlineData("Char_Armor_Helmet", true)]
    [InlineData("Char_Clothing_Feet", true)]
    [InlineData("WeaponPersonal", true)]
    [InlineData("FPS_Consumable", true)]
    // The character itself, and its phone.
    [InlineData("Char_Body", false)]
    [InlineData("MobiGlas", false)]
    [InlineData(null, false)]
    public void Only_what_a_body_yields_is_kept(string? type, bool kept)
    {
        Assert.Equal(kept, GameNpcOutfits.Kept(type));
    }
}
