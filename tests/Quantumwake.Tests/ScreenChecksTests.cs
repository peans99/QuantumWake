using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// What a frame said, set beside what the logs believed.
/// </summary>
/// <remarks>
/// The beliefs are written by hand so each verdict can be earned on purpose.
/// What is defended is that a check that could not run says so, that the
/// screen wins over the factory loadout, and that the wallet is only ever a
/// baseline until there is a second reading to move from.
/// </remarks>
public class ScreenChecksTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 7, 19, 30, 17, TimeSpan.Zero);

    private sealed class Beliefs : IScreenBeliefs
    {
        public (string Id, string Name, string? System)? Where { get; init; }
        public Dictionary<string, (string Id, string Name, string? System)> Atlas { get; init; } = [];
        public IReadOnlyList<string>? Open { get; init; }
        public Func<DateTimeOffset, decimal?> Running { get; init; } = _ => null;
        public IReadOnlyList<string> Stock { get; init; } = [];

        public (string Id, string Name, string? System)? WhereAt(DateTimeOffset at) => Where;
        public (string Id, string Name, string? System)? PlaceNamed(string read) =>
            Atlas.TryGetValue(read, out var place) ? place : null;
        public IReadOnlyList<string>? OpenContractsAt(DateTimeOffset at) => Open;
        public decimal? LedgerRunningAt(DateTimeOffset at) => Running(at);
        public IReadOnlyList<string> StockParts(string ship) => Stock;
        public IReadOnlyList<string> FlownShips() => [];
    }

    /// <param name="accepted">What the map said about contracts: false is "NO ACCEPTED CONTRACTS", null is nothing at all.</param>
    private static ScreenFrame MapFrame(bool? accepted = false) =>
        new(ScreenKind.Map, [], null, null,
            new MapReading("PYRO", "DUDLEY & DAUGHTERS", 0, -155.25, 68.33, accepted), null);

    private static ScreenFrame LoadoutFrame(string? ship, params (string Slot, string? Name)[] parts) =>
        new(ScreenKind.Loadout, [], null,
            new LoadoutReading("DRAKE CORSAIR", ship, ship is null ? ["Drake Corsair"] : [], null,
                [.. parts.Select(p => new ScreenFitting(p.Slot, p.Name ?? "?", p.Name, null, "Exact", [], []))]),
            null, null);

    private static ScreenFrame WalletFrame(long? balance) =>
        new(ScreenKind.MobiGlas, [], null, null, null,
            new WalletReading(balance, balance is null ? "the balance is printed in a face this engine does not read" : null));

    private static ScreenCheck Only(IReadOnlyList<ScreenCheck> checks, string subject) =>
        Assert.Single(checks, c => c.Subject == subject);

    // ---- where you were ----

    [Fact]
    public void The_footer_agrees_with_the_inference_when_the_atlas_puts_both_at_the_same_place()
    {
        var beliefs = new Beliefs
        {
            Where = ("Pyro_Dudley", "Dudley & Daughters", "Pyro"),
            Atlas = { ["DUDLEY & DAUGHTERS"] = ("Pyro_Dudley", "Dudley & Daughters", "Pyro") },
            Open = [],
        };

        var check = Only(ScreenChecks.Check(MapFrame(), At, beliefs, null), "Where you were");

        Assert.Equal("agrees", check.Verdict);
        Assert.Equal("PYRO > DUDLEY & DAUGHTERS", check.Claim);
        Assert.Equal("Pyro > Dudley & Daughters", check.Belief);
    }

    [Fact]
    public void The_footer_differs_when_the_logs_had_the_pilot_somewhere_else()
    {
        var beliefs = new Beliefs
        {
            Where = ("Pyro_Ruin", "Ruin Station", "Pyro"),
            Atlas = { ["DUDLEY & DAUGHTERS"] = ("Pyro_Dudley", "Dudley & Daughters", "Pyro") },
            Open = [],
        };

        var check = Only(ScreenChecks.Check(MapFrame(), At, beliefs, null), "Where you were");

        Assert.Equal("differs", check.Verdict);
        Assert.Contains("Dudley & Daughters", check.Note);
    }

    [Fact]
    public void A_moment_no_session_covers_is_unchecked_and_says_so()
    {
        var check = Only(ScreenChecks.Check(MapFrame(), At, new Beliefs(), null), "Where you were");

        Assert.Equal("unchecked", check.Verdict);
        Assert.Contains("no session", check.Note);
    }

    [Fact]
    public void A_place_the_atlas_cannot_name_still_has_its_system_compared()
    {
        var beliefs = new Beliefs { Where = ("Stanton_Lorville", "Lorville", "Stanton"), Open = [] };

        var check = Only(ScreenChecks.Check(MapFrame(), At, beliefs, null), "Where you were");

        Assert.Equal("differs", check.Verdict);
        Assert.Contains("only the system", check.Note);
    }

    // ---- contracts ----

    [Fact]
    public void No_accepted_contracts_agrees_when_the_logs_have_none_open()
    {
        var beliefs = new Beliefs { Where = ("x", "x", "Pyro"), Open = [] };

        Assert.Equal("agrees", Only(ScreenChecks.Check(MapFrame(), At, beliefs, null), "Contracts").Verdict);
    }

    [Fact]
    public void No_accepted_contracts_differs_and_names_what_the_logs_thought_was_open()
    {
        var beliefs = new Beliefs { Where = ("x", "x", "Pyro"), Open = ["Bounty: Vaughn", "Deliver to Orison"] };

        var check = Only(ScreenChecks.Check(MapFrame(), At, beliefs, null), "Contracts");

        Assert.Equal("differs", check.Verdict);
        Assert.Contains("Bounty: Vaughn", check.Belief);
        Assert.Contains("missed an ending", check.Note);
    }

    [Fact]
    public void A_map_that_does_not_mention_contracts_is_not_checked_for_them()
    {
        var checks = ScreenChecks.Check(MapFrame(accepted: null), At, new Beliefs { Open = [] }, null);

        Assert.DoesNotContain(checks, c => c.Subject == "Contracts");
    }

    // ---- fitted parts ----

    [Fact]
    public void A_ship_carrying_its_factory_parts_agrees()
    {
        var beliefs = new Beliefs { Stock = ["Frost-Star EX", "Genoa", "Torrent"] };
        var frame = LoadoutFrame("Drake Corsair", ("Cooler 1", "Frost-Star EX"), ("Power Plant 1", "Genoa"));

        var check = Only(ScreenChecks.Check(frame, At, beliefs, null), "Fitted parts");

        Assert.Equal("agrees", check.Verdict);
        Assert.Equal("2 parts named", check.Claim);
    }

    [Fact]
    public void A_part_the_factory_did_not_fit_means_the_ship_is_not_stock_and_the_screen_wins()
    {
        var beliefs = new Beliefs { Stock = ["Frost-Star EX", "Regulus"] };
        var frame = LoadoutFrame("Drake Corsair", ("Cooler 1", "Frost-Star EX"), ("Power Plant 1", "Genoa"));

        var check = Only(ScreenChecks.Check(frame, At, beliefs, null), "Fitted parts");

        Assert.Equal("differs", check.Verdict);
        Assert.Contains("Genoa in Power Plant 1", check.Note);
    }

    [Fact]
    public void A_ship_that_did_not_read_leaves_the_parts_unchecked_and_offers_the_resemblance()
    {
        var frame = LoadoutFrame(null, ("Radar", "Chernykh"));

        var check = Only(ScreenChecks.Check(frame, At, new Beliefs { Stock = ["Chernykh"] }, null), "Fitted parts");

        Assert.Equal("unchecked", check.Verdict);
        Assert.Contains("DRAKE CORSAIR", check.Note);
        Assert.Contains("looks like Drake Corsair", check.Note);
    }

    [Fact]
    public void A_ship_with_no_factory_loadout_on_file_is_unchecked()
    {
        var frame = LoadoutFrame("Drake Corsair", ("Radar", "Chernykh"));

        var check = Only(ScreenChecks.Check(frame, At, new Beliefs(), null), "Fitted parts");

        Assert.Equal("unchecked", check.Verdict);
        Assert.Contains("no factory loadout", check.Belief);
    }

    // ---- the wallet ----

    [Fact]
    public void A_wallet_that_did_not_read_is_unchecked_with_the_reason()
    {
        var check = Only(ScreenChecks.Check(WalletFrame(null), At, new Beliefs(), null), "Wallet");

        Assert.Equal("unchecked", check.Verdict);
        Assert.Contains("face", check.Note);
    }

    [Fact]
    public void The_first_wallet_read_is_a_baseline_because_the_logs_never_carry_a_balance()
    {
        var check = Only(ScreenChecks.Check(WalletFrame(1_971_263), At, new Beliefs(), null), "Wallet");

        Assert.Equal("new", check.Verdict);
        Assert.Contains("baseline", check.Note);
    }

    [Fact]
    public void The_second_wallet_read_agrees_when_the_ledger_accounts_for_the_movement()
    {
        var earlier = At.AddHours(-2);
        var beliefs = new Beliefs { Running = at => at == earlier ? 100_000m : 60_000m };

        var check = Only(ScreenChecks.Check(WalletFrame(1_931_263), At, beliefs, new WalletBaseline(earlier, 1_971_263)), "Wallet");

        Assert.Equal("agrees", check.Verdict);
    }

    [Fact]
    public void The_second_wallet_read_says_how_much_moved_without_a_line_in_the_log()
    {
        var earlier = At.AddHours(-2);
        var beliefs = new Beliefs { Running = at => at == earlier ? 100_000m : 60_000m };

        var check = Only(ScreenChecks.Check(WalletFrame(1_900_000), At, beliefs, new WalletBaseline(earlier, 1_971_263)), "Wallet");

        Assert.Equal("differs", check.Verdict);
        Assert.Contains("31,263 aUEC left", check.Note);
    }
}
