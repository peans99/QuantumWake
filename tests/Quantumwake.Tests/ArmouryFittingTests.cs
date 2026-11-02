using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// A gun with attachments on it: which pieces go in which slot, and what the
/// pieces do to the figures the Armoury derives.
/// </summary>
/// <remarks>
/// The fixtures are this install's, read through <c>--armoury=ports</c> on
/// 2026-11-02: the P4-AR's barrel port takes size 2 only and asks for
/// <c>FPS_Barrel ballistic_attach</c>, its sight port sizes 1 to 2; the
/// Tacit Suppressor2 carries both the ballistic and the energy tag, the Emod
/// Stabilizer2 the energy one only; the Tacit is damage ×0.92, the Stoic
/// Suppressor1 spread ×1.2, the Tracer laser spread ×0.885, the Escalate
/// Stabilizer3 damage ×1.1, ammo ×2 and charge ×0.5, the Torrent
/// Compensator3 three pellets fewer.
/// </remarks>
public class ArmouryFittingTests
{
    private static readonly WeaponPort P4Barrel = new("Barrel", "barrel_attach", 2, 2, ["FPS_Barrel", "ballistic_attach"], []);
    private static readonly WeaponPort P4Sight = new("Sight", "optics_attach", 1, 2, [], []);

    private static WeaponAttachment Piece(string name, string kind, int size, AttachmentEffect effect, params string[] tags) =>
        new(name.ToLowerInvariant().Replace(' ', '_'), name, kind, "", size, "ArmaMod", 0.1, effect) { Tags = tags };

    private static readonly WeaponAttachment Tacit2 = Piece("Tacit Suppressor2", "Barrel", 2, new AttachmentEffect(Damage: 0.92, Sound: 0.4), "FPS_Barrel", "ballistic_attach", "energy_attach");
    private static readonly WeaponAttachment Tacit1 = Piece("Tacit Suppressor1", "Barrel", 1, new AttachmentEffect(Damage: 0.92, Sound: 0.66), "FPS_Barrel", "ballistic_attach", "energy_attach");
    private static readonly WeaponAttachment Emod2 = Piece("Emod Stabilizer2", "Barrel", 2, new AttachmentEffect(RecoilStrength: 0.7, Sound: 1.2), "FPS_Barrel", "energy_attach");
    private static readonly WeaponAttachment Ee04 = Piece("EE04", "Sight", 2, new AttachmentEffect(Zoom: 4, SecondZoom: 6, ZoomTime: 1.25, ZeroingMax: 500, ZeroingStep: 100));
    private static readonly WeaponAttachment Ee08 = Piece("EE08", "Sight", 3, new AttachmentEffect(Zoom: 8, ZeroingMax: 1000, ZeroingStep: 500));

    private static readonly PersonalWeapon P4 =
        new("behr_rifle_ballistic_01", "P4-AR Rifle", "Rifle", "Medium", 2, "Behring", new DamageKinds(Physical: 12), 550, 2, 40, 0.05, 10, 40,
            "behr_rifle_ballistic_01_mag", 2.65, [new FireMode("AUTO", "Auto", 810)])
        { Ports = [P4Sight, P4Barrel] };

    [Fact]
    public void A_piece_fits_by_slot_size_and_the_tags_the_port_asks_for()
    {
        Assert.True(Armoury.Fits(P4Barrel, Tacit2));
        // Size 1 on a size-2 port.
        Assert.False(Armoury.Fits(P4Barrel, Tacit1));
        // A stabiliser is for energy guns: it carries no ballistic tag.
        Assert.False(Armoury.Fits(P4Barrel, Emod2));
        // A sight in a barrel port, and a size-3 scope on a port that stops at 2.
        Assert.False(Armoury.Fits(P4Barrel, Ee04));
        Assert.True(Armoury.Fits(P4Sight, Ee04));
        Assert.False(Armoury.Fits(P4Sight, Ee08));
    }

    [Fact]
    public void A_piece_that_asks_a_tag_of_the_port_needs_the_port_to_offer_it()
    {
        var picky = Tacit2 with { RequiredTags = ["rail_long"] };

        Assert.False(Armoury.Fits(P4Barrel, picky));
        Assert.True(Armoury.Fits(P4Barrel with { PortTags = ["rail_long"] }, picky));
    }

    [Fact]
    public void A_suppressor_takes_its_damage_off_every_hit_and_so_off_the_dps_and_the_magazine()
    {
        var fitted = Armoury.Fitted(P4, Tacit2.Effect);
        var mode = fitted.Modes[0];

        Assert.Equal(11.04, fitted.Damage.Physical, 6);
        // 162 a second bare: 12 × 810 / 60. With the Tacit, 11.04 × 810 / 60.
        Assert.Equal(149.04, Armoury.DamagePerSecond(fitted, mode), 6);
        Assert.Equal(441.6, Armoury.DamagePerMagazine(fitted, mode), 6);
        Assert.Equal(810, mode.RoundsPerMinute);
    }

    [Fact]
    public void A_charge_piece_halves_the_charge_and_doubles_the_rounds_it_costs()
    {
        var zenith = P4 with { Modes = [new FireMode("CHARGE", "Charge", 325, ChargeSeconds: 3.5, ChargeDamageMultiplier: 2, ChargeAmmoMultiplier: 3)] };
        var escalate3 = new AttachmentEffect(Damage: 1.1, AmmoCost: 2, ChargeTime: 0.5);

        var mode = Armoury.Fitted(zenith, escalate3).Modes[0];

        Assert.Equal(1.75, mode.ChargeSeconds, 6);
        Assert.Equal(2, mode.AmmoPerShot);
    }

    [Fact]
    public void Fewer_pellets_never_leave_a_shot_with_none()
    {
        var torrent3 = new AttachmentEffect(Pellets: -3, Spread: 0.77);
        var shotgun = P4 with { Modes = [new FireMode("SEMI", "Semi", 90, Pellets: 8)] };

        Assert.Equal(5, Armoury.Fitted(shotgun, torrent3).Modes[0].Pellets);
        Assert.Equal(1, Armoury.Fitted(P4, torrent3).Modes[0].Pellets);
    }

    [Fact]
    public void Pieces_combine_by_multiplying_and_the_sight_keeps_its_zoom()
    {
        var stoic1 = new AttachmentEffect(Spread: 1.2, RecoilTime: 0.8, Sound: 0.66);
        var laser = new AttachmentEffect(Spread: 0.885);

        var all = Armoury.Combine([stoic1, laser, Ee04.Effect]);

        Assert.Equal(1.062, all.Spread, 6);
        Assert.Equal(0.66, all.Sound, 6);
        Assert.Equal(4, all.Zoom);
        Assert.Equal(6, all.SecondZoom);
        Assert.Equal(500, all.ZeroingMax);
        Assert.Equal(100, all.ZeroingStep);
        Assert.Equal(1.25, all.ZoomTime, 6);
    }

    [Fact]
    public void Nothing_fitted_is_the_gun_as_it_is()
    {
        Assert.Same(P4, Armoury.Fitted(P4, Armoury.Combine([])));
        Assert.True(Armoury.Combine([]).IsNone);
    }
}
