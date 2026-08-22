using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Reading a ship's changeable ports out of the loadout tree.
/// </summary>
/// <remarks>
/// The fixture mirrors the real scunpacked shape: a port carries both the part
/// fitted in it and the rule for what may replace it, and children hang off
/// their parent - a jump drive inside a quantum drive, guns inside a turret.
/// </remarks>
public class ShipSlotTests
{
    private const string Ships =
        """
        [
          {"ClassName":"DRAK_Corsair","Name":"Corsair","Loadout":[
            {"HardpointName":"hardpoint_quantum_drive","ClassName":"QDRV_ARCC_S02_Torrent_SCItem",
             "UUID":"d1b9fbc4-797b-4777-a9da-f2e58c095922","Name":"Torrent","Type":"QuantumDrive.UNDEFINED","Grade":3,
             "Loadout":[
               {"HardpointName":"hardpoint_Jump_Drive","Name":"Excelsior","Type":"JumpDrive.UNDEFINED",
                "Editable":true,"CompatibleTypes":[{"Type":"JumpDrive","SubTypes":["JumpDrive"]}],
                "MaxSize":2,"MinSize":2}
             ],
             "Editable":true,"CompatibleTypes":[{"Type":"QuantumDrive","SubTypes":["QDrive"]}],
             "MaxSize":2,"MinSize":2,"PortId":"loadout.0"},

            {"HardpointName":"hardpoint_quantum_fuel_tank_a","Name":"Internal Tank",
             "Type":"QuantumFuelTank.QuantumFuel","Editable":false,
             "CompatibleTypes":[{"Type":"QuantumFuelTank"}],"MaxSize":2,"MinSize":2},

            {"HardpointName":"hardpoint_manned_turret_left","Name":"Manned Turret","Type":"TurretBase.MannedTurret",
             "Loadout":[
               {"HardpointName":"hardpoint_class_2","Name":"CF-227 Badger Repeater","Type":"WeaponGun.Gun","Grade":1,
                "Editable":true,"CompatibleTypes":[{"Type":"WeaponGun","SubTypes":["Gun"]}],
                "MaxSize":3,"MinSize":1,"PortId":"loadout.2.loadout.0"}
             ],
             "Editable":false,"CompatibleTypes":[{"Type":"TurretBase"}],"MaxSize":4,"MinSize":4},

            {"HardpointName":"hardpoint_shield_generator","Name":"<= PLACEHOLDER =>","Type":"Shield.UNDEFINED",
             "Editable":true,"CompatibleTypes":[{"Type":"Shield"}],"MaxSize":2,"MinSize":2,"PortId":"loadout.3"},

            {"HardpointName":"door_left","Name":"Door","Type":"Door.UNDEFINED",
             "Editable":true,"CompatibleTypes":[{"Type":"Door"}],"MaxSize":1,"MinSize":1}
          ]},

          {"ClassName":"ARGO_ATLS","Name":"ATLS","Loadout":[
            {"HardpointName":"seat","Name":"Seat","Type":"Seat.UNDEFINED",
             "Editable":true,"CompatibleTypes":[{"Type":"Seat"}],"MaxSize":1,"MinSize":1}
          ]}
        ]
        """;

    private static IReadOnlyList<ShipSlot> Corsair() =>
        CommunityData.DigestShipSlots(Ships)["DRAK_Corsair"];

    [Fact]
    public void An_editable_port_becomes_a_slot_with_what_is_in_it()
    {
        var drive = Corsair().Single(s => s.Kind == "QuantumDrive");

        Assert.Equal(2, drive.Size);
        Assert.Equal("Torrent", drive.Fitted);
        Assert.Equal("hardpoint_quantum_drive", drive.Hardpoint);
    }

    [Fact]
    public void A_fixed_port_is_not_a_decision_and_is_left_out()
    {
        Assert.DoesNotContain(Corsair(), s => s.Kind == "QuantumFuelTank");
    }

    [Fact]
    public void Guns_hanging_off_a_turret_are_still_ports()
    {
        Assert.Contains(Corsair(), s => s.Kind == "WeaponGun");
    }

    /// <summary>The question a shop is asked is "what size", so a range is split.</summary>
    [Fact]
    public void A_port_taking_a_range_becomes_one_slot_per_size()
    {
        var guns = Corsair().Where(s => s.Kind == "WeaponGun").ToList();

        Assert.Equal([1, 2, 3], guns.Select(g => g.Size));
    }

    [Fact]
    public void Kinds_nobody_buys_are_left_out()
    {
        Assert.DoesNotContain(Corsair(), s => s.Kind is "Door" or "JumpDrive");
    }

    [Fact]
    public void A_ship_with_nothing_worth_shopping_for_is_absent_entirely()
    {
        Assert.False(CommunityData.DigestShipSlots(Ships).ContainsKey("ARGO_ATLS"));
    }

    /// <summary>
    /// A port that takes three sizes is three rows here and one hole in the
    /// ship, so anything counting holes has to count ports.
    /// </summary>
    [Fact]
    public void Every_size_of_one_port_shares_that_port_s_identity()
    {
        var guns = Corsair().Where(s => s.Kind == "WeaponGun").ToList();

        Assert.Single(guns.Select(g => g.Port).Distinct());
    }

    [Fact]
    public void A_placeholder_name_is_not_a_fitted_part()
    {
        Assert.Null(Corsair().Single(s => s.Kind == "Shield").Fitted);
    }
}
