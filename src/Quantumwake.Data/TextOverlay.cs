using System.Text;

namespace Quantumwake.Data;

/// <summary>What a generated text overlay would change, and the file itself.</summary>
/// <param name="Marked">Item names that would gain the mark.</param>
/// <param name="Sold">Names left alone because something is known to sell them.</param>
/// <param name="Skipped">Names left alone because they are not gear a player shops for.</param>
/// <param name="Samples">A few of the marked names, for showing before installing.</param>
/// <param name="Content">The whole localisation file, ready to write.</param>
public sealed record TextOverlayPlan(
    int Marked,
    int Sold,
    int Skipped,
    IReadOnlyList<TextOverlayLine> Samples,
    string Content)
{
    public int Considered => Marked + Sold + Skipped;
}

/// <summary>One name the overlay would rewrite.</summary>
public sealed record TextOverlayLine(string ItemClass, string Was, string Becomes, string Category);

/// <summary>
/// Builds the game's English text table with a mark against gear nothing is
/// known to sell.
/// </summary>
/// <remarks>
/// <para>
/// The mark answers one question a player asks while looting: is this worth
/// carrying, or can I buy another whenever I like? It is deliberately not a
/// price - a price is stale the moment the game launches, because the table is
/// read once at startup and never again, while "is this sold at all" barely
/// moves between patches.
/// </para>
/// <para>
/// It is a floor and has to be worded as one everywhere it is explained. Two
/// sources say a thing is sold: this install's own receipts, which cannot be
/// wrong because the game charged for them, and UEX, which is broad but
/// crowd-sourced - it misses 29 of the 106 items these logs prove were bought.
/// Nothing enumerates what shops stock, so an unmarked item is "nobody has told
/// us otherwise", never "confirmed rare".
/// </para>
/// <para>
/// Only gear the player shops for is considered: 5,528 of the names on this
/// install are ship internals and unclassifiable ids nobody browses a kiosk
/// for. What is left is still not sparse - 3,116 of 4,047 gear names are
/// marked, because UEX lists only 931 of them - so across the whole table the
/// mark mostly reports UEX's coverage rather than rarity.
///
/// It is far better on gear this install has actually handled: 39 of 109
/// looted items, a bit over a third. UEX knows the things players commonly
/// meet and is thin on everything else, so the mark is worth most exactly
/// where the player is looking. Whether to narrow the file to that set is a
/// decision for whoever installs it, not one this class should make quietly.
/// </para>
/// </remarks>
public static class TextOverlay
{
    /// <summary>The prefix the game keys item names under.</summary>
    private const string ItemNamePrefix = "item_Name";

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
    /// Rewrites <paramref name="baseIni"/>, marking gear nothing is known to sell.
    /// </summary>
    /// <param name="baseIni">
    /// The table to build on. When a text mod is already installed this must be
    /// that mod's file rather than the game's, or installing this one silently
    /// reverts theirs.
    /// </param>
    /// <param name="isSold">Whether anything is known to sell an item class.</param>
    /// <param name="mark">Appended to the displayed name. Kept short; it lands in every list the name appears in.</param>
    public static TextOverlayPlan Build(
        string baseIni,
        Func<string, bool> isSold,
        string mark = " *")
    {
        var lines = baseIni.Split('\n');
        var output = new StringBuilder(baseIni.Length + (lines.Length / 8));
        var samples = new List<TextOverlayLine>();

        int marked = 0, sold = 0, skipped = 0;

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

            var category = ItemCategories.Of(itemClass);

            if (!Shoppable.Contains(category))
            {
                skipped++;
                Emit(line);
                continue;
            }

            if (isSold(itemClass))
            {
                sold++;
                Emit(line);
                continue;
            }

            marked++;

            if (samples.Count < 25)
                samples.Add(new TextOverlayLine(itemClass, value.Trim(), value.Trim() + mark, category));

            Emit(key + "=" + value + mark);
            continue;

            void Emit(string text)
            {
                output.Append(text);
                if (separator)
                    output.Append('\n');
            }
        }

        return new TextOverlayPlan(marked, sold, skipped, samples, output.ToString());
    }
}
