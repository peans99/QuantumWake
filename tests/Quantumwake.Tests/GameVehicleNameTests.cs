using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Which of a vehicle's two name keys is shown: <c>vehicleName</c> or the attach
/// definition's.
/// </summary>
/// <remarks>
/// The keys are the installs' own. The Wikelo specials are from 5.0 PTU
/// (5.0.202.46035) and LIVE 4.10.201, and the PYAM Exec and derelict rows are
/// from LIVE, where 44 vehicles disagree between their two keys; the text is
/// what LIVE's global.ini says. Over both installs (1,097 + 1,105 vehicles) the
/// rule changes eight names - the Polaris Wikelo Special on each, and the six
/// new 5.0 specials - and no other.
/// </remarks>
public class GameVehicleNameTests
{
    private static readonly Dictionary<string, string> Text = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vehicle_NameDRAK_Pitbull"] = "Drake Pitbull",
        ["vehicle_NameDRAK_Pitbull_Collector_Stealth"] = "Drake Pitbull Wikelo Sneak Special",
        ["vehicle_NameRSI_Polaris"] = "RSI Polaris",
        ["vehicle_NameRSI_Polaris_Collector_Military"] = "RSI Polaris Wikelo Special",
        ["vehicle_NameDRAK_Corsair"] = "Drake Corsair",
        ["vehicle_NameDRAK_Corsair_PYAM_Exec"] = "Corsair PYAM Exec",
        ["vehicle_NameANVL_Lightning_F8C_PYAM_Exec"] = "F8C Lightning PYAM Exec",
        ["vehicle_NameANVL_Lightning_F8C_Exec"] = "Anvil F8C Lightning Executive Edition",
        ["vehicle_NameDRAK_Cutlass_Black"] = "Drake Cutlass Black",
        ["hud_wreckage"] = "Wreckage",
    };

    [Theory]
    [InlineData("@vehicle_NameDRAK_Pitbull", "@vehicle_NameDRAK_Pitbull_Collector_Stealth", "@vehicle_NameDRAK_Pitbull_Collector_Stealth")]
    [InlineData("@vehicle_NameRSI_Polaris", "@vehicle_NameRSI_Polaris_Collector_Military", "@vehicle_NameRSI_Polaris_Collector_Military")]
    public void A_Wikelo_special_takes_the_attach_name_that_extends_the_base_hulls(string vehicle, string attach, string expected)
    {
        Assert.Equal(expected, GameVehicles.NameKey(Text, vehicle, attach));
    }

    [Fact]
    public void A_PYAM_Exec_keeps_the_vehicle_name_that_extends_the_generic_attach_name()
    {
        Assert.Equal("@vehicle_NameDRAK_Corsair_PYAM_Exec",
            GameVehicles.NameKey(Text, "@vehicle_NameDRAK_Corsair_PYAM_Exec", "@vehicle_NameDRAK_Corsair"));
    }

    [Theory]
    // Neither extends the other: _F8C_Exec is not a prefix of _F8C_PYAM_Exec.
    [InlineData("@vehicle_NameANVL_Lightning_F8C_PYAM_Exec", "@vehicle_NameANVL_Lightning_F8C_Exec")]
    // A derelict is wreckage in the game, not the hull it was.
    [InlineData("@hud_wreckage", "@vehicle_NameDRAK_Cutlass_Black")]
    public void Unrelated_keys_keep_the_vehicle_name(string vehicle, string attach)
    {
        Assert.Equal(vehicle, GameVehicles.NameKey(Text, vehicle, attach));
    }

    [Fact]
    public void A_longer_key_with_no_text_does_not_replace_one_that_has_it()
    {
        Assert.Equal("@vehicle_NameDRAK_Pitbull",
            GameVehicles.NameKey(Text, "@vehicle_NameDRAK_Pitbull", "@vehicle_NameDRAK_Pitbull_Unwritten"));
    }

    [Fact]
    public void A_prefix_that_stops_mid_word_is_not_an_extension()
    {
        // DRAK_Pitbull against DRAK_Pitbullx: same letters, different hull.
        var text = new Dictionary<string, string>(Text) { ["vehicle_NameDRAK_Pitbullx"] = "Not a Pitbull" };

        Assert.Equal("@vehicle_NameDRAK_Pitbull",
            GameVehicles.NameKey(text, "@vehicle_NameDRAK_Pitbull", "@vehicle_NameDRAK_Pitbullx"));
    }
}
