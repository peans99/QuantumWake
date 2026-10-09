using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// A loose text file against the game's own: which strings it lacks, and
/// filling them without touching a mod's wording.
/// </summary>
/// <remarks>
/// The keys are 4.10.2's: a Discovery Month contract title and a new item,
/// beside strings every table has. A loose file written before the patch lacks
/// the first two, and the game then shows them as their keys.
/// </remarks>
public class TextTablesTests
{
    private const string Game = "﻿item_NameCOOL_IASI_S01=BriskAir IC-10\r\n"
        + "iasi_Patrol_E_title,P=RSI Disc. Month: Yellow Lvl. - Patrol System\r\n"
        + "vehicle_NameRSI_Constellation_Andromeda=RSI Constellation Mk IV Andromeda\r\n"
        + "Shared_Cancel=Cancel\r\n";

    private const string OldMod = "vehicle_NameRSI_Constellation_Andromeda=RSI Constellation Andromeda [Mod]\r\n"
        + "Shared_Cancel=Cancel\r\n";

    [Fact]
    public void The_strings_a_file_lacks_are_the_games_keys_it_does_not_define()
    {
        var missing = TextTables.Missing(OldMod, Game);

        Assert.Equal(["item_NameCOOL_IASI_S01", "iasi_Patrol_E_title,P"], missing.Select(l => TextTables.KeyOf(l)));
    }

    /// <summary>
    /// Only keys the file has no line for are added; a key it has keeps the
    /// mod's words, even where the game's have changed since.
    /// </summary>
    [Fact]
    public void Filling_adds_only_missing_keys_and_never_rewrites_a_mods_wording()
    {
        var filled = TextTables.WithMissing(OldMod, Game, out var added);

        Assert.Equal(2, added);
        Assert.Contains("RSI Constellation Andromeda [Mod]", filled);
        Assert.DoesNotContain("RSI Constellation Mk IV Andromeda", filled);
        Assert.Contains("iasi_Patrol_E_title,P=RSI Disc. Month: Yellow Lvl. - Patrol System", filled);
        Assert.Empty(TextTables.Missing(filled, Game));
    }

    [Fact]
    public void A_file_with_every_key_is_left_exactly_as_it_was()
    {
        var filled = TextTables.WithMissing(Game, Game, out var added);

        Assert.Equal(0, added);
        Assert.Same(Game, filled);
    }

    /// <summary>
    /// The game's own file starts with a byte order mark; a key read with it
    /// attached would never match, and every first line would count as missing.
    /// </summary>
    [Fact]
    public void A_byte_order_mark_and_comments_do_not_make_keys()
    {
        Assert.Equal("item_NameCOOL_IASI_S01", TextTables.KeyOf("﻿item_NameCOOL_IASI_S01=BriskAir IC-10"));
        Assert.Null(TextTables.KeyOf("; a comment = with an equals"));
        Assert.Null(TextTables.KeyOf("no equals here"));
        Assert.Empty(TextTables.Missing(Game.Replace("﻿", ""), Game));
    }
}
