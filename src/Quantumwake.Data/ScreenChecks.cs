namespace Quantumwake.Data;

/// <summary>
/// What the app believed at a moment, asked for by the checks.
/// </summary>
/// <remarks>
/// An interface so the checks can be tested against a belief written by hand,
/// and so the one place that knows how to walk the sessions is not the one
/// place that knows how to compare them. <see cref="LibraryBeliefs"/> is the
/// real one.
/// </remarks>
public interface IScreenBeliefs
{
    /// <summary>Where the logs put the pilot at that moment, or null when no session covers it.</summary>
    (string Id, string Name, string? System)? WhereAt(DateTimeOffset at);

    /// <summary>The atlas entry a place name reads as, or null when nothing fits.</summary>
    (string Id, string Name, string? System)? PlaceNamed(string read);

    /// <summary>Contracts accepted and still open at that moment, by name; null when no session covers it.</summary>
    IReadOnlyList<string>? OpenContractsAt(DateTimeOffset at);

    /// <summary>The ledger's net movement from the wipe line to that moment; null with nothing counted yet.</summary>
    decimal? LedgerRunningAt(DateTimeOffset at);

    /// <summary>The parts a ship ships with, by name; empty when the reference data has none.</summary>
    IReadOnlyList<string> StockParts(string ship);
}

/// <summary>
/// One thing a frame claimed, set beside what the app believed.
/// </summary>
/// <param name="Subject">What is being checked: "Where you were", "Wallet".</param>
/// <param name="Claim">What the screen said, in its words.</param>
/// <param name="Belief">What the app thought, or why it thought nothing.</param>
/// <param name="Verdict">
/// <c>agrees</c>, <c>differs</c>, <c>new</c> when the screen says something
/// the logs never could, or <c>unchecked</c> when the comparison could not be
/// made - and <see cref="Note"/> then says why, because a check that quietly
/// did not happen looks the same as one that passed.
/// </param>
public sealed record ScreenCheck(string Subject, string Claim, string Belief, string Verdict, string? Note = null);

/// <summary>The last wallet figure read, for the next one to be checked against.</summary>
public sealed record WalletBaseline(DateTimeOffset At, long Balance);

/// <summary>
/// Compares what a frame said with what the logs had led the app to believe.
/// </summary>
/// <remarks>
/// <para>
/// The interesting output is the disagreement. The logs record what was spent
/// and never the total, where the pilot arrived and never where they stood,
/// and nothing whatever about what is bolted to their ship. A screenshot is a
/// moment of truth on all three, and the comparison is worth more than either
/// side alone: it says where the inference is right and where it drifts.
/// </para>
/// <para>
/// Every verdict names what it could not do. The app's location carries a
/// confidence, the crew page calls its count a floor, and a check that could
/// not run says so rather than passing by default.
/// </para>
/// </remarks>
public static class ScreenChecks
{
    public static IReadOnlyList<ScreenCheck> Check(
        ScreenFrame frame,
        DateTimeOffset shotAt,
        IScreenBeliefs beliefs,
        WalletBaseline? lastWallet)
    {
        var checks = new List<ScreenCheck>();

        if (frame.Map is { } map)
        {
            checks.Add(Place(map, shotAt, beliefs));

            if (map.NoAcceptedContracts)
                checks.Add(Contracts(shotAt, beliefs));
        }

        if (frame.Loadout is { } loadout)
            checks.Add(Fittings(loadout, beliefs));

        if (frame.Wallet is { } wallet)
            checks.Add(Wallet(wallet, shotAt, beliefs, lastWallet));

        return checks;
    }

    /// <summary>The map footer against the location inference at that second.</summary>
    private static ScreenCheck Place(MapReading map, DateTimeOffset at, IScreenBeliefs beliefs)
    {
        const string subject = "Where you were";

        var claim = map.PlaceRead is { Length: > 0 } place
            ? map.SystemRead is { Length: > 0 } system ? $"{system} > {place}" : place
            : "(the footer named no place)";

        var believed = beliefs.WhereAt(at);

        if (believed is null)
            return new ScreenCheck(subject, claim, "nothing - no session covers that moment", "unchecked",
                "the logs have no session running when this was taken");

        var belief = believed.Value.System is { Length: > 0 } sys
            ? $"{sys} > {believed.Value.Name}"
            : believed.Value.Name;

        if (map.PlaceRead is not { Length: > 0 })
            return new ScreenCheck(subject, claim, belief, "unchecked", "the footer did not read");

        var named = beliefs.PlaceNamed(map.PlaceRead);

        if (named is null)
        {
            // The system is a smaller claim and is still worth checking: a
            // place the atlas cannot name in a system the logs disagree with
            // is a location inference gone wrong, and worth hearing about.
            if (map.SystemRead is { Length: > 0 } sysRead && believed.Value.System is { Length: > 0 } sysBelieved)
            {
                var same = string.Equals(sysRead, sysBelieved, StringComparison.OrdinalIgnoreCase);
                return new ScreenCheck(subject, claim, belief, same ? "agrees" : "differs",
                    $"nothing in the atlas reads like \"{map.PlaceRead}\", so only the system was compared");
            }

            return new ScreenCheck(subject, claim, belief, "unchecked",
                $"nothing in the atlas reads like \"{map.PlaceRead}\"");
        }

        var agrees = string.Equals(named.Value.Id, believed.Value.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(named.Value.Name, believed.Value.Name, StringComparison.OrdinalIgnoreCase);

        return new ScreenCheck(subject, claim, belief, agrees ? "agrees" : "differs",
            agrees ? null : $"the footer reads as {named.Value.Name}");
    }

    /// <summary>"No accepted contracts" against the contracts the logs had open.</summary>
    private static ScreenCheck Contracts(DateTimeOffset at, IScreenBeliefs beliefs)
    {
        const string subject = "Contracts";
        const string claim = "no accepted contracts";

        var open = beliefs.OpenContractsAt(at);

        if (open is null)
            return new ScreenCheck(subject, claim, "nothing - no session covers that moment", "unchecked",
                "the logs have no session running when this was taken");

        if (open.Count == 0)
            return new ScreenCheck(subject, claim, "none open", "agrees");

        return new ScreenCheck(subject, claim,
            $"{open.Count} open: {string.Join(", ", open.Take(4))}{(open.Count > 4 ? ", …" : "")}",
            "differs",
            "the game says none; the logs may have missed an ending");
    }

    /// <summary>The parts read off the loadout screen against the ship's factory fit.</summary>
    /// <remarks>
    /// A preference over provenance, not a switch: the factory loadout is the
    /// same for every pilot who owns the ship, so where the screen contradicts
    /// it the screen wins and the ship is not stock. Matching is by part name
    /// and not by port, because the screen's port labels are not the data's
    /// port ids and half a join would put a real part in the wrong hole.
    /// </remarks>
    private static ScreenCheck Fittings(LoadoutReading loadout, IScreenBeliefs beliefs)
    {
        const string subject = "Fitted parts";

        var named = loadout.Fittings.Where(f => f.Name is not null).ToList();
        var unread = loadout.Fittings.Count(f => !f.NothingRead && f.Name is null);
        var bare = loadout.Fittings.Count(f => f.NothingRead);

        var claim = $"{named.Count} part{(named.Count == 1 ? "" : "s")} named"
            + (bare > 0 ? $", {bare} port{(bare == 1 ? "" : "s")} with nothing read under {(bare == 1 ? "it" : "them")}" : "")
            + (unread > 0 ? $", {unread} not matched" : "");

        if (loadout.Ship is null)
        {
            var read = loadout.ShipRead ?? "(no ship read)";
            var alike = loadout.LooksLike.Count > 0
                ? $" - looks like {string.Join(" or ", loadout.LooksLike)}, which is not the same as reading it"
                : "";

            return new ScreenCheck(subject, claim, "nothing - the ship did not read", "unchecked",
                $"the dropdown read \"{read}\"{alike}");
        }

        var stock = beliefs.StockParts(loadout.Ship);

        if (stock.Count == 0)
            return new ScreenCheck(subject, claim, $"nothing - no factory loadout on file for the {loadout.Ship}", "unchecked");

        var changed = named
            .Where(f => !stock.Contains(f.Name!, StringComparer.OrdinalIgnoreCase))
            .Select(f => $"{f.Name} in {f.Slot}")
            .ToList();

        if (changed.Count == 0)
            return new ScreenCheck(subject, claim, $"the {loadout.Ship} as it ships", "agrees",
                named.Count == 0 ? "nothing named, so nothing compared" : null);

        return new ScreenCheck(subject, claim, $"the {loadout.Ship} as it ships", "differs",
            $"not stock: {string.Join(", ", changed.Take(5))}{(changed.Count > 5 ? ", …" : "")}");
    }

    /// <summary>The wallet against the ledger's movement since the last wallet read.</summary>
    /// <remarks>
    /// The logs carry no balance, so a first reading can only be a baseline.
    /// From the second on, the figure the ledger predicts is the last reading
    /// plus every movement it recorded between the two, and the difference is
    /// money that moved without leaving a line in the log: insurance claims,
    /// hangar fees, a trade the parser missed. That number is the one the
    /// Ledger has never been able to show.
    /// </remarks>
    private static ScreenCheck Wallet(WalletReading wallet, DateTimeOffset at, IScreenBeliefs beliefs, WalletBaseline? last)
    {
        const string subject = "Wallet";

        if (wallet.Balance is not { } balance)
            return new ScreenCheck(subject, "(did not read)", "the logs never carry a balance", "unchecked", wallet.Trouble);

        var claim = $"{balance:N0} aUEC";

        if (last is null)
            return new ScreenCheck(subject, claim, "nothing - the logs never carry a balance", "new",
                "kept as a baseline; the next wallet read is checked against the ledger's movement since this one");

        var now = beliefs.LedgerRunningAt(at);
        var then = beliefs.LedgerRunningAt(last.At);

        if (now is null || then is null)
            return new ScreenCheck(subject, claim, "nothing counted in the ledger yet", "unchecked");

        var expected = last.Balance + (long)Math.Round(now.Value - then.Value);
        var drift = balance - expected;

        if (drift == 0)
            return new ScreenCheck(subject, claim, $"{expected:N0} aUEC from the ledger", "agrees");

        return new ScreenCheck(subject, claim, $"{expected:N0} aUEC from the ledger", "differs",
            $"{Math.Abs(drift):N0} aUEC {(drift > 0 ? "arrived" : "left")} without a line in the log since {last.At:d MMM HH:mm}");
    }
}
