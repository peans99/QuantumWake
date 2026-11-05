using System.Text;
using Quantumwake.Core.GameData;

namespace Quantumwake.Data;

/// <summary>How the overlay should mark names.</summary>
/// <param name="Colour">
/// Whether gear nothing sells is wrapped in an emphasis tag as well as marked.
/// Off by default: it is the one part of this whose rendering cannot be checked
/// from here.
/// </param>
/// <param name="Level">
/// Which of the game's emphasis levels to use, 1 to 5. These are the game's own
/// styles rather than colours chosen here - what each looks like is its
/// stylesheet's business.
/// </param>
/// <param name="Facts">
/// Whether to add the size, grade and armour-class marks as well as the
/// sold/unsold one.
/// </param>
/// <param name="Routes">
/// Whether hauling contract titles ask the game to print their places -
/// <c>| Port Tressler &gt; NB Int. Spaceport</c> - which is the only way a log
/// ever names where a haul goes, and what the hauling planner reads.
/// </param>
/// <param name="Rewards">
/// Whether contract titles say what finishing them pays -
/// <c>[100 Rep]</c>, and <c>[BP]*</c> when one can award a blueprint - as
/// StarStrings' did; see <see cref="GameMissions.TitleRewards"/>.
/// </param>
/// <param name="Stats">
/// Whether a ship part's description gains the figures the game leaves out of
/// it - a shield's HP and regeneration, a cooler's output, a gun's damage and
/// range - from <see cref="GameShipParts"/>.
/// </param>
public sealed record TextOverlayOptions(bool Colour = false, int Level = 3, bool Facts = true, bool Routes = true, bool Rewards = true, bool Stats = true);

/// <summary>What a generated text overlay would change, and the file itself.</summary>
/// <param name="Marked">Item names that would gain the unsold mark.</param>
/// <param name="Sold">Names left alone because something is known to sell them.</param>
/// <param name="Skipped">Names left alone because they are not gear a player shops for.</param>
/// <param name="Annotated">Names that gained a size, grade or armour-class mark.</param>
/// <param name="Changes">
/// Every name that would actually be rewritten, for showing before installing.
/// Not a sample: it was capped at 25 of some 4,000, which is fine as an
/// illustration and useless as an answer to "what would happen to mine?" - the
/// question anybody deciding whether to write into their game folder is asking.
/// Lines the pass leaves alone are not here, because they have nothing to show.
/// </param>
/// <param name="Content">The whole localisation file, ready to write.</param>
/// <param name="Routed">Hauling contract titles that would ask the game to print their places.</param>
/// <param name="Rewarded">Contract titles that would say what finishing them pays.</param>
/// <param name="Described">Ship part descriptions that would gain their figures.</param>
public sealed record TextOverlayPlan(
    int Marked,
    int Sold,
    int Skipped,
    int Annotated,
    IReadOnlyList<TextOverlayLine> Changes,
    string Content,
    int Routed = 0,
    int Rewarded = 0,
    int Described = 0)
{
    public int Considered => Marked + Sold + Skipped;
}

/// <summary>One name the overlay would rewrite.</summary>
public sealed record TextOverlayLine(string ItemClass, string Was, string Becomes, string Category);

/// <summary>
/// Builds the game's English text table with what this app knows written into
/// the names themselves.
/// </summary>
/// <remarks>
/// <para>
/// The marks answer the questions a player asks in a kiosk or over a body: is
/// this worth carrying, and what am I actually looking at. They are deliberately
/// not prices - a price is stale the moment the game launches, because the table
/// is read once at startup and never again, while "is this sold at all" and
/// "what size is it" barely move between patches.
/// </para>
/// <para>
/// The sold mark is a floor and has to be worded as one everywhere it is
/// explained. Two sources say a thing is sold: this install's own receipts,
/// which cannot be wrong because the game charged for them, and UEX, which is
/// broad but crowd-sourced - it misses 29 of the 106 items these logs prove were
/// bought. Nothing enumerates what shops stock, so an unmarked item is "nobody
/// has told us otherwise", never "confirmed rare".
/// </para>
/// <para>
/// Size and grade come from the install itself, and both need gating rather than
/// printing: 25,944 of the 26,028 items carry a size and a grade, because 1/1 is
/// the default, so marking on their presence would mark almost everything. Only
/// a fitted ship component gets one, and only when the value is not the default.
/// A scope is not a part and does not get a size.
/// </para>
/// <para>
/// The grade ordinal is a letter, and the mapping was checked rather than
/// assumed: the AEGS coolers come out 1, 2, 3, 4 exactly where StarStrings
/// independently calls them A, B, C and D.
/// </para>
/// <para>
/// The budget is four characters of content inside one bracket pair. That is
/// measured, not chosen - the median item name is 21 characters and 30.8% are
/// already over 24 - so <c>[S2B*]</c> puts a median name inside the game's own
/// 75th percentile.
/// </para>
/// </remarks>
public static class TextOverlay
{
    /// <summary>The prefix the game keys item names under.</summary>
    private const string ItemNamePrefix = "item_Name";

    /// <summary>The prefix the game keys item descriptions under.</summary>
    private const string ItemDescPrefix = "item_Desc";

    /// <summary>The category a description that gained its figures is listed under.</summary>
    public const string PartStatsCategory = "Part figures";

    /// <summary>A line break inside one entry: the two characters backslash and n, not a newline.</summary>
    private const string Break = "\\n";

    /// <summary>Nothing known to sell it.</summary>
    private const char Unsold = '*';

    /// <summary>
    /// Categories worth marking: things sold over a counter and looted off the
    /// floor. <see cref="ItemCategories.Other"/> is excluded deliberately - it is
    /// where ship components and unclassifiable internals land.
    /// </summary>
    private static readonly HashSet<string> Shoppable = new(StringComparer.Ordinal)
    {
        ItemCategories.Weapons,
        ItemCategories.Ammo,
        ItemCategories.Attachments,
        ItemCategories.Throwables,
        ItemCategories.Armour,
        ItemCategories.Medical,
        ItemCategories.Tools,
        ItemCategories.Consumables,
    };

    /// <summary>
    /// Types whose size means what a player means by size.
    /// </summary>
    /// <remarks>
    /// Everything here is fitted to a ship. Personal weapons and weapon
    /// attachments carry a size too and are left out on purpose: a holographic
    /// sight is not a size 1 component, and saying so was the first thing this
    /// got wrong.
    /// </remarks>
    private static readonly HashSet<string> Fitted = new(StringComparer.OrdinalIgnoreCase)
    {
        "WeaponGun", "Turret", "TurretBase", "MainThruster", "ManneuverThruster",
        "MissileLauncher", "Missile", "BombLauncher", "PowerPlant", "Cooler", "Shield",
        "QuantumDrive", "QuantumFuelTank", "FuelTank", "ExternalFuelTank", "Radar",
        "WeaponDefensive", "Module",
    };

    /// <summary>Armour sub-types, as one letter.</summary>
    private static readonly Dictionary<string, char> ArmourClass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Light"] = 'L',
        ["LightArmor"] = 'L',
        ["Medium"] = 'M',
        ["Heavy"] = 'H',
    };

    /// <summary>
    /// Rewrites <paramref name="baseIni"/> with the marks turned on.
    /// </summary>
    /// <param name="baseIni">
    /// The table to build on. When a text mod is already installed this must be
    /// that mod's file rather than the game's, or installing this one silently
    /// reverts theirs.
    /// </param>
    /// <param name="isSold">Whether anything is known to sell an item class.</param>
    /// <param name="facts">What the install says each item is, keyed by class name.</param>
    /// <param name="options">Which marks to write, and whether to colour them.</param>
    /// <param name="routes">
    /// Hauling title keys and which ends each can name, from
    /// <see cref="GameReputationCatalogue.HaulTitles"/>.
    /// </param>
    /// <param name="rewards">
    /// What the contracts under each title key pay on completion, from
    /// <see cref="GameReputationCatalogue.TitleRewards"/>.
    /// </param>
    /// <param name="parts">Ship parts' figures by class, from <see cref="GameShipParts"/>.</param>
    public static TextOverlayPlan Build(
        string baseIni,
        Func<string, bool> isSold,
        IReadOnlyDictionary<string, GameItem>? facts = null,
        TextOverlayOptions? options = null,
        IReadOnlyDictionary<string, string>? routes = null,
        IReadOnlyDictionary<string, GameTitleReward>? rewards = null,
        IReadOnlyDictionary<string, ShipPartStats>? parts = null)
    {
        var settings = options ?? new TextOverlayOptions();
        var level = Math.Clamp(settings.Level, 1, 5);

        var lines = baseIni.Split('\n');
        var output = new StringBuilder(baseIni.Length + (lines.Length / 8));
        var changes = new List<TextOverlayLine>();

        int marked = 0, sold = 0, skipped = 0, annotated = 0, routed = 0, rewarded = 0, described = 0;
        var byKey = parts is not null && settings.Stats ? DescriptionKeys(parts) : null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // The source's own line endings are reproduced exactly, including
            // whether the last line had one: a file that differs from the game's
            // by a trailing byte is a file nobody can diff with confidence.
            var separator = i < lines.Length - 1;

            var split = line.IndexOf('=');
            var key = split > 0 ? line[..split].TrimStart('﻿') : string.Empty;

            if (split > 0)
            {
                // A hauling title can take both: its places, then what it pays.
                var titled = settings.Routes && routes is not null ? Route(key, line[(split + 1)..], routes) : null;
                var paid = settings.Rewards && rewards is not null
                    ? Reward(key, titled?.Becomes ?? line[(split + 1)..].TrimEnd(), rewards)
                    : null;

                if (titled is not null || paid is not null)
                {
                    var raw = line[(split + 1)..];
                    var was = raw.TrimEnd();
                    var becomes = paid ?? titled!.Value.Becomes;
                    if (titled is not null) routed++;
                    if (paid is not null) rewarded++;
                    changes.Add(new TextOverlayLine(key, was, becomes, titled is not null ? HaulTitleCategory : ContractTitleCategory));
                    Emit(key + "=" + becomes + raw[was.Length..]);
                    continue;
                }
            }

            if (split > 0 && settings.Stats && parts is not null && key.StartsWith(ItemDescPrefix, StringComparison.OrdinalIgnoreCase)
                && Described(key, line[(split + 1)..], parts, byKey!, facts) is { } desc)
            {
                described++;
                changes.Add(new TextOverlayLine(desc.Class, desc.Name, desc.Added, PartStatsCategory));
                Emit(key + "=" + desc.Becomes);
                continue;
            }

            if (split <= 0 || !key.StartsWith(ItemNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                Emit(line);
                continue;
            }

            var itemClass = key[ItemNamePrefix.Length..].TrimStart('_');
            var value = line[(split + 1)..].TrimEnd('\r');

            if (itemClass.Length == 0 || value.Trim().Length == 0)
            {
                Emit(line);
                continue;
            }

            var item = facts is not null && settings.Facts ? Facts(facts, itemClass) : null;
            var category = ItemCategories.Of(itemClass);
            var shoppable = Shoppable.Contains(category);
            var badge = item is null ? string.Empty : Badge(item);

            // A ship component is not something anybody shops for over a
            // counter, so it never earns the sold mark - but it can still say
            // what size it is, which is the whole reason to look at one.
            if (!shoppable && badge.Length == 0)
            {
                skipped++;
                Emit(line);
                continue;
            }

            var unsold = shoppable && !isSold(itemClass);

            if (!shoppable) skipped++;
            else if (unsold) marked++;
            else sold++;

            if (badge.Length > 0) annotated++;

            var suffix = badge + (unsold ? Unsold.ToString() : string.Empty);
            var trimmed = value.TrimEnd();
            var padding = value[trimmed.Length..];

            var rewritten = suffix.Length > 0 ? $"{trimmed} [{suffix}]" : trimmed;

            // The colour marks the same thing the star does, so it goes on for
            // the same reason and nowhere else.
            if (unsold && settings.Colour) rewritten = $"<EM{level}>{rewritten}</EM{level}>";

            if (rewritten == trimmed)
            {
                Emit(line);
                continue;
            }

            changes.Add(new TextOverlayLine(itemClass, trimmed, rewritten, Listed(category, facts, itemClass)));

            Emit(key + "=" + rewritten + padding);
            continue;

            void Emit(string text)
            {
                output.Append(text);
                if (separator)
                    output.Append('\n');
            }
        }

        return new TextOverlayPlan(marked, sold, skipped, annotated, changes, output.ToString(), routed, rewarded, described);
    }

    /// <summary>
    /// A part's description with its figures added, or null when it is not a
    /// part this knows or already says all of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game writes a part's description as a block of <c>Label: value</c>
    /// lines - <c>Item Type: Cooler</c>, <c>Size: 1</c>, <c>Grade: C</c> - then a
    /// blank line and the prose. The figures join the end of that block, worded
    /// the same way, so they read as the game's own rather than as a note pinned
    /// on. A description with no block gets one in front of the prose.
    /// </para>
    /// <para>
    /// A label the description already has is left out, so a base table from
    /// another mod that prints the same figure - or a patch that starts to -
    /// does not get it twice.
    /// </para>
    /// </remarks>
    private static (string Class, string Name, string Added, string Becomes)? Described(
        string key, string value, IReadOnlyDictionary<string, ShipPartStats> parts,
        IReadOnlyDictionary<string, ShipPartStats?> byKey, IReadOnlyDictionary<string, GameItem>? facts)
    {
        var bare = key.EndsWith(",P", StringComparison.OrdinalIgnoreCase) ? key[..^2] : key;
        ShipPartStats? stats;
        if (byKey.TryGetValue(bare, out var keyed))
        {
            // Parts under one description that would not print the same figures:
            // the description is left as the game wrote it.
            if (keyed is null) return null;
            stats = keyed;
        }
        else
        {
            var cls = bare[ItemDescPrefix.Length..].TrimStart('_');
            if (cls.Length == 0) return null;
            stats = parts.GetValueOrDefault(cls) ?? parts.GetValueOrDefault($"{cls}_SCItem");
            if (stats is null || stats.DescriptionKey is not null) return null;
        }
        var cls2 = stats.Class.EndsWith("_SCItem", StringComparison.OrdinalIgnoreCase) ? stats.Class[..^7] : stats.Class;

        var trimmed = value.TrimEnd();
        if (trimmed.Length == 0) return null;
        var padding = value[trimmed.Length..];

        var lines = PartLines(stats)
            .Where(l => !trimmed.Contains(Break + l[..(l.IndexOf(':') + 1)], StringComparison.OrdinalIgnoreCase)
                && !trimmed.StartsWith(l[..(l.IndexOf(':') + 1)], StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (lines.Count == 0) return null;

        var block = string.Join(Break, lines);
        var blank = trimmed.IndexOf(Break + Break, StringComparison.Ordinal);
        var becomes = blank >= 0
            ? trimmed[..blank] + Break + block + trimmed[blank..]
            : block + Break + Break + trimmed;

        var name = facts is not null && Facts(facts, cls2) is { Name.Length: > 0 } item ? item.Name : cls2;
        return (cls2, name, string.Join(" · ", lines), becomes + padding);
    }

    /// <summary>
    /// Each description key the parts name, with the part whose figures it
    /// gains - or null where the parts sharing it would not print the same.
    /// </summary>
    /// <remarks>
    /// A part's description is under the key its own entity names, which is
    /// not always its class: a capital ship's cannon can borrow a stock gun's
    /// text. Matching by class missed those, and sharing is only safe when
    /// every part under the key says the same thing - otherwise the figures
    /// would be one gun's under another's name. The exception is the part the
    /// key is named for: <c>item_DescMISL_S02_CS_FSKI_Tempest</c> is the Tempest
    /// II's own text, and the event copy that borrows it with other figures
    /// (<c>_CitCon</c>) does not take it away from the missile anybody buys. On
    /// this install the borrowers that differ are copies like that - _LowPoly,
    /// _Turret, _Idris_M, _CitCon - not parts a pilot fits. A part that names its
    /// key is never matched by class as well, so a key it does not use cannot
    /// catch it.
    /// </remarks>
    private static Dictionary<string, ShipPartStats?> DescriptionKeys(IReadOnlyDictionary<string, ShipPartStats> parts)
    {
        var byKey = new Dictionary<string, ShipPartStats?>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in parts.Values.Where(p => p.DescriptionKey is not null)
                     .GroupBy(p => p.DescriptionKey!, StringComparer.OrdinalIgnoreCase))
        {
            var named = group.Key[ItemDescPrefix.Length..].TrimStart('_');
            var owner = group.FirstOrDefault(p => p.Class.Equals(named, StringComparison.OrdinalIgnoreCase)
                || p.Class.Equals(named + "_SCItem", StringComparison.OrdinalIgnoreCase));
            var agreed = group.Select(p => string.Join("|", PartLines(p))).Distinct(StringComparer.Ordinal).Count() == 1;
            byKey[group.Key] = owner ?? (agreed ? group.OrderBy(p => p.Class, StringComparer.OrdinalIgnoreCase).First() : null);
        }
        return byKey;
    }

    /// <summary>The figures a part's description gains, in the game's own Label: value form.</summary>
    internal static IEnumerable<string> PartLines(ShipPartStats s)
    {
        if (s.ShieldHp is { } hp) yield return $"Shield HP: {N(hp)}";
        if (s.ShieldRegen is { } regen) yield return $"Regen Rate: {N(regen)}/s";
        if (s.DamagedDelay is { } damaged)
            yield return s.DownedDelay is { } downed ? $"Regen Delay: {N(damaged)} s (Downed: {N(downed)} s)" : $"Regen Delay: {N(damaged)} s";
        if (s.PowerOutput is { } power) yield return $"Power Output: {N(power)}";
        if (s.CoolingOutput is { } cooling) yield return $"Cooling Output: {N(cooling)}";
        if (s.QuantumSpeed is { } speed) yield return $"Quantum Speed: {N(speed / 1000, 0)} km/s";
        if (s.SpoolTime is { } spool) yield return $"Spool Time: {N(spool)} s";
        if (s.QuantumCooldown is { } cooldown) yield return $"Cooldown: {N(cooldown)} s";
        if (s.Alpha is { } alpha) yield return s.DamageKind is { } kind ? $"Damage: {N(alpha)} {kind}" : $"Damage: {N(alpha)}";
        if (s.RoundsPerMinute is { } rpm) yield return $"Fire Rate: {N(rpm)} rpm";
        if (s.Dps is { } dps) yield return $"DPS: {N(dps)}";
        if (s.ProjectileSpeed is { } projectile) yield return $"Projectile Speed: {N(projectile)} m/s";
        if (s.Range is { } range) yield return $"Range: {N(range, 0)} m";
        if (s.MissileDamage is { } blast) yield return $"Damage: {N(blast)}";
        if (s.MissileSpeed is { } missileSpeed) yield return $"Speed: {N(missileSpeed)} m/s";
        if (s.LockTime is { } lockTime) yield return $"Lock Time: {N(lockTime)} s";
        if (s.LockRangeMax is { } far)
            yield return s.LockRangeMin is { } near ? $"Lock Range: {N(near, 0)}-{N(far, 0)} m" : $"Lock Range: {N(far, 0)} m";
    }

    /// <summary>
    /// A figure as the game's own text writes one: thousands separated, no
    /// trailing zeros, and invariant - the table is English whatever the PC is set to.
    /// </summary>
    private static string N(double value, int decimals = 2) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero)
            .ToString(decimals == 0 ? "#,0" : "#,0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The category a rewritten hauling title is listed under.</summary>
    public const string HaulTitleCategory = "Hauling title";

    /// <summary>The category any other contract title that gained its pay is listed under.</summary>
    public const string ContractTitleCategory = "Contract title";

    /// <summary>
    /// A contract title with what finishing it pays, or null when the key is
    /// not a rewarded title or already says it.
    /// </summary>
    /// <remarks>
    /// Worded and styled as StarStrings wrote it - <c>&lt;EM4&gt;[100 Rep] [BP]*&lt;/EM4&gt;</c>,
    /// the star saying the blueprint is a chance - because that is what
    /// players learned to read. Contracts under one title that pay
    /// differently say the range, <c>[100-150 Rep]</c>.
    /// </remarks>
    private static string? Reward(string key, string title, IReadOnlyDictionary<string, GameTitleReward> rewards)
    {
        var bare = key.EndsWith(",P", StringComparison.OrdinalIgnoreCase) ? key[..^2] : key;
        if (!rewards.TryGetValue(bare, out var reward)) return null;

        var trimmed = title.TrimEnd();
        if (trimmed.Length == 0 || trimmed.Contains(" Rep]", StringComparison.Ordinal)) return null;

        var parts = new List<string>();
        if (reward.MaxRep > 0)
        {
            // A second ladder is said as an addition, "[200+60 Rep]", so the
            // figure StarStrings players knew (260) is still there to add up.
            var main = reward.MinRep == reward.MaxRep ? $"{reward.MaxRep}" : $"{reward.MinRep}-{reward.MaxRep}";
            parts.Add(reward.Also > 0 ? $"[{main}+{reward.Also} Rep]" : $"[{main} Rep]");
        }
        if (reward.Blueprint) parts.Add("[BP]*");
        if (parts.Count == 0) return null;

        return $"{trimmed} <EM4>{string.Join(' ', parts)}</EM4>";
    }

    /// <summary>
    /// What a ship component is listed as, by the type the install gives it.
    /// </summary>
    /// <remarks>
    /// <see cref="ItemCategories"/> sorts what a pilot carries, and puts every
    /// ship part in Other - 646 of this install's 4,548 renames, coolers,
    /// shields, missiles and guns in one heap nobody could browse. Only the list
    /// uses this; which items are marked, and how, still follows the category.
    /// </remarks>
    private static readonly Dictionary<string, string> ShipKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WeaponGun"] = "Ship weapons",
        ["Turret"] = "Turrets & mounts",
        ["TurretBase"] = "Turrets & mounts",
        ["Missile"] = "Missiles",
        ["MissileLauncher"] = "Missile racks",
        ["BombLauncher"] = "Missile racks",
        ["PowerPlant"] = "Power plants",
        ["Cooler"] = "Coolers",
        ["Shield"] = "Shields",
        ["QuantumDrive"] = "Quantum drives",
        ["Radar"] = "Radars",
        ["WeaponDefensive"] = "Countermeasures",
        ["MainThruster"] = "Thrusters",
        ["ManneuverThruster"] = "Thrusters",
        ["QuantumFuelTank"] = "Fuel tanks",
        ["FuelTank"] = "Fuel tanks",
        ["ExternalFuelTank"] = "Fuel tanks",
        ["Armor"] = "Ship armour",
        ["Module"] = "Modules",

        // A hand weapon the class-name rules missed - the Copperhead grenade
        // launcher's class reads like nothing they know.
        ["WeaponPersonal"] = ItemCategories.Weapons,
    };

    private static string Listed(string category, IReadOnlyDictionary<string, GameItem>? facts, string itemClass) =>
        category == ItemCategories.Other && facts is not null && Facts(facts, itemClass) is { } item
            && ShipKinds.TryGetValue(item.Type, out var kind)
            ? kind
            : category;

    /// <summary>
    /// The placeholders, worded exactly as StarStrings words them, so a title
    /// either wrote reads the same to <c>HaulingContract.RouteFromTitle</c> and
    /// to a pilot.
    /// </summary>
    private static readonly Dictionary<string, string> RouteText = new(StringComparer.Ordinal)
    {
        [GameMissions.BothEnds] = "~mission(Location|name) > ~mission(Destination|name)",
        [GameMissions.DeliveryEnd] = "to ~mission(Destination|name)",
        [GameMissions.PickupEnd] = "from ~mission(Location|name)",
    };

    /// <summary>
    /// A hauling title with its places asked for, or null when the key is not
    /// one or the title already asks.
    /// </summary>
    /// <remarks>
    /// Already asking is the layered case: StarStrings' table has its own
    /// places in these titles, and a second pair would print every place twice.
    /// The game's own key spelling sometimes carries <c>,P</c>; the contract
    /// names the key without it. The value comes with its own line ending, so
    /// a carriage return goes back after the addition rather than being lost:
    /// the game's table is bare LF, a mod's need not be.
    /// </remarks>
    private static (string Was, string Becomes, string Padding)? Route(
        string key, string value, IReadOnlyDictionary<string, string> routes)
    {
        var bare = key.EndsWith(",P", StringComparison.OrdinalIgnoreCase) ? key[..^2] : key;
        if (!routes.TryGetValue(bare, out var ends) || !RouteText.TryGetValue(ends, out var place)) return null;

        var trimmed = value.TrimEnd();
        if (trimmed.Length == 0
            || trimmed.Contains("~mission(Location", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("~mission(Destination", StringComparison.OrdinalIgnoreCase))
            return null;

        return (trimmed, $"{trimmed} | {place}", value[trimmed.Length..]);
    }

    /// <summary>
    /// What the install says an item is, however the two spell its class.
    /// </summary>
    /// <remarks>
    /// The localisation table keys a shield as <c>SHLD_BEHR_S02_7MA</c> and the
    /// entity that describes it is <c>SHLD_BEHR_S02_7MA_SCItem</c>. Looking up
    /// only the name's own spelling missed 203 items, shield generators among
    /// them, and missed them silently: they simply came out unmarked.
    /// </remarks>
    private static GameItem? Facts(IReadOnlyDictionary<string, GameItem> facts, string itemClass) =>
        facts.GetValueOrDefault(itemClass) ?? facts.GetValueOrDefault($"{itemClass}_SCItem");

    /// <summary>
    /// The size, grade or armour class an item earns, in at most three
    /// characters.
    /// </summary>
    /// <remarks>
    /// Nothing is invented here. A size appears only on a fitted component whose
    /// size is not the default 1; a grade only alongside a size; an armour class
    /// only where the game gives a sub-type it recognises.
    /// </remarks>
    private static string Badge(GameItem item)
    {
        if (ArmourClass.TryGetValue(item.SubType, out var armour)) return armour.ToString();

        // Size 1 is a real size for something fitted to a ship - 24 of the 73
        // shields are S1 - and treating it as "unset" left every one of them
        // unmarked. The default-1 problem it was guarding against belongs to
        // types that are not components at all, and those are already out.
        if (!Fitted.Contains(item.Type) || item.Size < 1) return string.Empty;

        var grade = item.Grade is >= 1 and <= 4 ? ((char)('A' + item.Grade - 1)).ToString() : string.Empty;

        return $"S{item.Size}{grade}";
    }
}
