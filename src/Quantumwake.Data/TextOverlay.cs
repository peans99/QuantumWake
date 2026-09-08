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
public sealed record TextOverlayOptions(bool Colour = false, int Level = 3, bool Facts = true);

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
public sealed record TextOverlayPlan(
    int Marked,
    int Sold,
    int Skipped,
    int Annotated,
    IReadOnlyList<TextOverlayLine> Changes,
    string Content)
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
    public static TextOverlayPlan Build(
        string baseIni,
        Func<string, bool> isSold,
        IReadOnlyDictionary<string, GameItem>? facts = null,
        TextOverlayOptions? options = null)
    {
        var settings = options ?? new TextOverlayOptions();
        var level = Math.Clamp(settings.Level, 1, 5);

        var lines = baseIni.Split('\n');
        var output = new StringBuilder(baseIni.Length + (lines.Length / 8));
        var changes = new List<TextOverlayLine>();

        int marked = 0, sold = 0, skipped = 0, annotated = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // The source's own line endings are reproduced exactly, including
            // whether the last line had one: a file that differs from the game's
            // by a trailing byte is a file nobody can diff with confidence.
            var separator = i < lines.Length - 1;

            var split = line.IndexOf('=');
            var key = split > 0 ? line[..split].TrimStart('﻿') : string.Empty;

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

            changes.Add(new TextOverlayLine(itemClass, trimmed, rewritten, category));

            Emit(key + "=" + rewritten + padding);
            continue;

            void Emit(string text)
            {
                output.Append(text);
                if (separator)
                    output.Append('\n');
            }
        }

        return new TextOverlayPlan(marked, sold, skipped, annotated, changes, output.ToString());
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
