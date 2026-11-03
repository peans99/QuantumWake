namespace Quantumwake.Core.GameData;

/// <summary>
/// The arithmetic the Armoury page runs on a gun: what a mode does a second,
/// a magazine, and how long the magazine lasts. Pure functions over the
/// records <see cref="GameArmoury"/> reads, so the numbers can be asserted.
/// </summary>
/// <remarks>
/// None of these is a figure the game publishes; each is the files' own
/// numbers combined the obvious way, and the page calls them derived. A
/// burst's sustained rate assumes the trigger is pulled again the moment the
/// gun allows it, a charge's assumes every shot is held to full charge, and
/// the drop curve is the game's straight line - so many per metre past a
/// start, down to a floor.
/// </remarks>
public static class Armoury
{
    /// <summary>
    /// Rounds a minute with the trigger held: the cyclic rate for automatic
    /// fire, the cap for semi, and for a burst its shots over the burst and
    /// the gap after it. A charge is one shot per charge; a beam has no rounds.
    /// </summary>
    public static double SustainedRoundsPerMinute(FireMode mode)
    {
        if (mode.Kind == "Beam") return 0;

        if (mode.Kind == "Charge")
            return mode.ChargeSeconds > 0
                ? 60 / (mode.ChargeSeconds + (mode.RoundsPerMinute > 0 ? 60 / mode.RoundsPerMinute : 0))
                : mode.RoundsPerMinute;

        if (mode.BurstShots > 1 && mode.RoundsPerMinute > 0)
        {
            var cyclic = 60 / mode.RoundsPerMinute;
            // A burst with no cooldown of its own still cannot start the next
            // burst before the cyclic gap the last round would have had.
            var burst = (mode.BurstShots - 1) * cyclic + (mode.BurstCooldown > 0 ? mode.BurstCooldown : cyclic);
            return mode.BurstShots * 60 / burst;
        }

        return mode.RoundsPerMinute;
    }

    /// <summary>One projectile's damage in this mode: the second load's where the mode fires that, the gun's otherwise.</summary>
    public static DamageKinds Hit(PersonalWeapon weapon, FireMode mode) => mode.Hit ?? weapon.Damage;

    /// <summary>One pull's damage at the muzzle: the hit, every pellet, and a full charge where there is one.</summary>
    public static double DamagePerShot(PersonalWeapon weapon, FireMode mode)
    {
        var pellets = mode.Kind == "Charge" && mode.ChargePellets > 0 ? mode.ChargePellets : Math.Max(1, mode.Pellets);
        return Hit(weapon, mode).Total * pellets * (mode.Kind == "Charge" ? mode.ChargeDamageMultiplier : 1);
    }

    /// <summary>Damage a second, holding the trigger, at the muzzle.</summary>
    public static double DamagePerSecond(PersonalWeapon weapon, FireMode mode) =>
        mode.Kind == "Beam"
            ? mode.BeamDamagePerSecond?.Total ?? 0
            : DamagePerShot(weapon, mode) * SustainedRoundsPerMinute(mode) / 60;

    /// <summary>Rounds one pull takes: the launcher's cost, times a charge's multiplier.</summary>
    private static double RoundsPerShot(FireMode mode) =>
        Math.Max(1, mode.AmmoPerShot) * (mode.Kind == "Charge" ? Math.Max(1, mode.ChargeAmmoMultiplier) : 1);

    /// <summary>What a whole magazine does, at the muzzle.</summary>
    public static double DamagePerMagazine(PersonalWeapon weapon, FireMode mode)
    {
        if (weapon.Magazine <= 0) return 0;
        if (mode.Kind == "Beam")
            return mode.BeamAmmoPerSecond > 0 ? (mode.BeamDamagePerSecond?.Total ?? 0) * weapon.Magazine / mode.BeamAmmoPerSecond : 0;
        return DamagePerShot(weapon, mode) * Math.Floor(weapon.Magazine / RoundsPerShot(mode));
    }

    /// <summary>Seconds of held trigger a magazine lasts.</summary>
    public static double SecondsToEmpty(PersonalWeapon weapon, FireMode mode)
    {
        if (weapon.Magazine <= 0) return 0;
        if (mode.Kind == "Beam") return mode.BeamAmmoPerSecond > 0 ? weapon.Magazine / mode.BeamAmmoPerSecond : 0;
        var rpm = SustainedRoundsPerMinute(mode);
        return rpm > 0 ? Math.Floor(weapon.Magazine / RoundsPerShot(mode)) * 60 / rpm : 0;
    }

    /// <summary>One projectile's damage at a distance, after the drop.</summary>
    public static double DamageAt(PersonalWeapon weapon, double metres)
    {
        var full = weapon.Damage.Total;
        if (weapon.DropStart <= 0 || weapon.DropPerMetre <= 0 || metres <= weapon.DropStart) return full;
        return Math.Max(weapon.DropFloor, full - (metres - weapon.DropStart) * weapon.DropPerMetre);
    }

    /// <summary>Metres past which a projectile does its least, or 0 when it never drops.</summary>
    public static double FloorAt(PersonalWeapon weapon)
    {
        if (weapon.DropStart <= 0 || weapon.DropPerMetre <= 0) return 0;
        var fall = weapon.Damage.Total - weapon.DropFloor;
        return fall <= 0 ? weapon.DropStart : weapon.DropStart + fall / weapon.DropPerMetre;
    }

    /// <summary>
    /// Whether an attachment goes on a gun's port, by the rule the files
    /// state: the slot's kind, a size inside the port's range, every tag the
    /// port asks for carried by the piece, and every tag the piece asks for
    /// offered by the port.
    /// </summary>
    /// <remarks>
    /// The tags are what size alone misses. A compensator for ballistic
    /// guns carries <c>ballistic_attach</c> and no <c>energy_attach</c>, and
    /// an energy rifle's barrel port asks for the second, so the two never
    /// meet however the sizes fall.
    /// </remarks>
    public static bool Fits(WeaponPort port, WeaponAttachment piece) =>
        port.Slot == piece.Kind
        && piece.Size >= port.MinSize && piece.Size <= port.MaxSize
        && port.RequiredTags.All(t => piece.Tags.Contains(t, StringComparer.OrdinalIgnoreCase))
        && piece.RequiredTags.All(t => port.PortTags.Contains(t, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Several attachments as one block: the multipliers multiplied, the
    /// pellets and burst shots added, the sight's zoom and zeroing kept.
    /// </summary>
    /// <remarks>
    /// Multiplying is the assumption, and the one place it matters is rare:
    /// a gun takes one piece a slot, and the pieces of different slots mostly
    /// touch different figures - a barrel the damage, the rate and the kick,
    /// a sight the zoom, an underbarrel laser the spread. Spread is the
    /// exception (a Stoic suppressor's ×1.2 and a laser's ×0.885), and
    /// whether the game multiplies those or adds what each takes off is not
    /// in the files.
    /// </remarks>
    public static AttachmentEffect Combine(IEnumerable<AttachmentEffect> effects)
    {
        var all = effects.ToList();
        double Product(Func<AttachmentEffect, double> f) => all.Aggregate(1.0, (p, e) => p * f(e));
        var sight = all.Where(e => e.Zoom > 0).MaxBy(e => e.Zoom);
        return new AttachmentEffect(
            Damage: Product(e => e.Damage), FireRate: Product(e => e.FireRate), Spread: Product(e => e.Spread),
            RecoilStrength: Product(e => e.RecoilStrength), RecoilTime: Product(e => e.RecoilTime), Sound: Product(e => e.Sound),
            Heat: Product(e => e.Heat), AmmoCost: Product(e => e.AmmoCost), ChargeTime: Product(e => e.ChargeTime),
            ProjectileSpeed: Product(e => e.ProjectileSpeed),
            Pellets: all.Sum(e => e.Pellets), BurstShots: all.Sum(e => e.BurstShots),
            Zoom: sight?.Zoom ?? 0, SecondZoom: sight?.SecondZoom ?? 0, ZoomTime: Product(e => e.ZoomTime),
            ZeroingMax: all.Max(e => (double?)e.ZeroingMax) ?? 0,
            ZeroingStep: all.Where(e => e.ZeroingMax > 0).Select(e => e.ZeroingStep).FirstOrDefault());
    }

    /// <summary>
    /// The gun with an attachment block applied to the figures this page
    /// derives from: every hit and a beam's damage a second, the cyclic rate,
    /// the pellets and burst shots, the rounds a shot costs, the charge time,
    /// the heat and the projectile's speed. The rest of the block - spread,
    /// recoil, sound, zoom - changes how the gun handles, not these figures,
    /// and the page lists it beside them.
    /// </summary>
    /// <remarks>
    /// The damage drop is left as the files write it. Whether a damage
    /// multiplier also lifts the drop's floor is not in the files, so the
    /// fitted gun is compared at the muzzle only.
    /// </remarks>
    public static PersonalWeapon Fitted(PersonalWeapon weapon, AttachmentEffect effect)
    {
        if (effect.IsNone) return weapon;
        var modes = weapon.Modes.Select(m => m with
        {
            RoundsPerMinute = m.RoundsPerMinute * effect.FireRate,
            // A pellet count never falls below one: a rifle's single round
            // with a shotgun choke on it is still a round.
            Pellets = Math.Max(1, m.Pellets + effect.Pellets),
            ChargePellets = m.ChargePellets > 0 ? Math.Max(1, m.ChargePellets + effect.Pellets) : 0,
            BurstShots = m.BurstShots > 0 ? Math.Max(1, m.BurstShots + effect.BurstShots) : 0,
            AmmoPerShot = Math.Max(1, (int)Math.Round(Math.Max(1, m.AmmoPerShot) * effect.AmmoCost)),
            ChargeSeconds = m.ChargeSeconds * effect.ChargeTime,
            HeatPerShot = m.HeatPerShot * effect.Heat,
            Hit = m.Hit?.Times(effect.Damage),
            BeamDamagePerSecond = m.BeamDamagePerSecond?.Times(effect.Damage),
            BeamAmmoPerSecond = m.BeamAmmoPerSecond * effect.AmmoCost,
        }).ToList();
        return weapon with
        {
            Damage = weapon.Damage.Times(effect.Damage),
            ProjectileSpeed = weapon.ProjectileSpeed * effect.ProjectileSpeed,
            Modes = modes,
        };
    }
}
