namespace Quantumwake.Data;

/// <summary>Where a kit's item stands, as far as the logs can say.</summary>
public enum KitHolding
{
    /// <summary>On your character in the last loadout the game reported.</summary>
    Equipped,

    /// <summary>Seen in a stash recently. A sighting, not a stock level.</summary>
    Seen,

    /// <summary>Seen in a stash, but long enough ago to be worth checking.</summary>
    Stale,

    /// <summary>Never seen equipped or stored at all.</summary>
    Missing
}

/// <summary>One line of a kit, and what is known about having it.</summary>
/// <param name="Where">Where it was last seen, when it was seen anywhere.</param>
/// <param name="NeedsAsking">
/// Whether the app cannot answer this and the pilot must. True for anything
/// resting on a sighting, which is most of a stash.
/// </param>
/// <param name="Held">
/// How many the logs can account for. Only the loadout reports a count - a
/// stash entry is one sighting of a name and says nothing about how many are
/// in the box - so this is null wherever the answer rests on storage.
/// </param>
public sealed record KitLine(
    string Name,
    int Quantity,
    bool Optional,
    KitHolding Holding,
    string? Where,
    DateTimeOffset? LastSeen,
    bool NeedsAsking,
    int? Held = null)
{
    /// <summary>
    /// How many are still wanted, when that can be worked out at all.
    /// </summary>
    /// <remarks>
    /// A kit asking for four medpens with one on the character is short three,
    /// and calling that settled because the name appeared is how somebody
    /// undocks with a quarter of a kit.
    /// </remarks>
    public int? Short => Held is { } held ? Math.Max(0, Quantity - held) : null;
}

/// <summary>A kit measured against what the logs have seen.</summary>
/// <param name="Rule">The inference every uncertain line rests on, stated once.</param>
public sealed record KitPreparation(
    string KitId,
    string Name,
    IReadOnlyList<KitLine> Lines,
    string Rule)
{
    /// <summary>Lines the pilot has to settle before a shopping list means anything.</summary>
    public int ToConfirm => Lines.Count(l => l.NeedsAsking);

    /// <summary>Lines that are certainly not to hand.</summary>
    public int Missing => Lines.Count(l => l.Holding == KitHolding.Missing);
}

/// <summary>
/// Works out what a kit still needs, and what it cannot know.
/// </summary>
/// <remarks>
/// <para>
/// The whole difficulty is in one fact: <see cref="StashEntry"/> records an
/// item being <em>seen</em> in a container, and the game never logs one being
/// taken out or used up. So anything ever stashed looks present for ever, and a
/// kit that treated a sighting as a holding would cheerfully report a full
/// loadout to somebody standing in an empty hangar.
/// </para>
/// <para>
/// That is why the answer is three-valued rather than have/need, and why the
/// asking step is not a nicety to trim later: it is the only place the
/// uncertainty can be resolved, because nothing in the logs can resolve it.
/// </para>
/// </remarks>
public static class KitPreparer
{
    /// <summary>
    /// How old a sighting has to be before it is worth doubting out loud.
    /// </summary>
    /// <remarks>
    /// A guess, and admitted as one. Both sides of it still get asked about -
    /// the line only changes how loudly. It exists so a stash checked this
    /// morning and one checked last spring do not read identically.
    /// </remarks>
    public const int StaleAfterDays = 30;

    public const string Rule =
        "The game logs an item being seen in a container, never one being taken out or used up. "
        + "So anything stored is a sighting rather than a stock level, and only you can say "
        + "whether it is still there.";

    public static KitPreparation Prepare(Kit kit, LibraryStats stats, DateTimeOffset now)
    {
        // Names as the loadout and stash report them, which is what a kit line
        // is written against - somebody types "Pembroke helmet", not a class id.
        // Summed across slots: nine magazine ports holding the same magazine
        // is nine of them, and a kit asking for nine is satisfied.
        var equipped = stats.Loadout
            .SelectMany(slot => slot.Items)
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (Count: group.Sum(item => Math.Max(1, item.Count)),
                          LastSeen: group.Max(item => item.LastSeen)),
                StringComparer.OrdinalIgnoreCase);

        var stashed = new Dictionary<string, (DateTimeOffset At, string Where)>(StringComparer.OrdinalIgnoreCase);

        foreach (var place in stats.Stash)
        {
            foreach (var item in place.Groups.SelectMany(group => group.Items))
            {
                // The most recent sighting wins: the same helmet can sit in
                // three lockers, and the newest is the one worth asking about.
                if (stashed.TryGetValue(item.Name, out var seen) && seen.At >= item.LastSeen)
                    continue;

                stashed[item.Name] = (item.LastSeen, place.Name);
            }
        }

        var lines = kit.Items.Select(item => Line(item, equipped, stashed, now)).ToList();

        return new KitPreparation(kit.Id, kit.Name, lines, Rule);
    }

    private static KitLine Line(
        KitItem item,
        IReadOnlyDictionary<string, (int Count, DateTimeOffset LastSeen)> equipped,
        IReadOnlyDictionary<string, (DateTimeOffset At, string Where)> stashed,
        DateTimeOffset now)
    {
        // Worn beats stored, and is the one state the logs can be sure of: a
        // loadout is what the game reported was on the character, not a
        // sighting in a container it may have been taken out of since.
        if (equipped.TryGetValue(item.Name, out var worn))
        {
            // Counted, not just found. The loadout says how many slots hold
            // this, and a kit that wants four of something is not satisfied by
            // one of them.
            var enough = worn.Count >= item.Quantity;

            return new KitLine(
                item.Name, item.Quantity, item.Optional,
                enough ? KitHolding.Equipped : KitHolding.Missing,
                "on you", worn.LastSeen,

                // Nothing to ask: the count is a fact, and the shortfall
                // follows from it.
                NeedsAsking: false,
                Held: worn.Count);
        }

        if (stashed.TryGetValue(item.Name, out var stored))
        {
            var stale = now - stored.At > TimeSpan.FromDays(StaleAfterDays);

            return new KitLine(
                item.Name, item.Quantity, item.Optional,
                stale ? KitHolding.Stale : KitHolding.Seen,
                stored.Where, stored.At,

                // Both need asking. Staleness changes how loudly the page says
                // so, never whether it asks - a sighting from this morning is
                // still a sighting.
                NeedsAsking: true);
        }

        // Never seen is the one negative the logs can support: not "you do not
        // have it", but "nothing here has ever seen it".
        return new KitLine(item.Name, item.Quantity, item.Optional, KitHolding.Missing, null, null, false);
    }

    /// <summary>
    /// What to shop for, once the pilot has said which sightings are gone.
    /// </summary>
    /// <param name="gone">
    /// Names the pilot confirmed they no longer hold. Anything not named is
    /// taken as still held - the safe direction, since the cost of getting it
    /// wrong is a trip rather than a purchase nobody needed.
    /// </param>
    public static IReadOnlyList<KitLine> Shopping(KitPreparation prepared, IReadOnlyCollection<string> gone) =>
        [.. prepared.Lines
            .Where(line =>
                line.Holding == KitHolding.Missing
                || line.Short > 0
                || gone.Contains(line.Name, StringComparer.OrdinalIgnoreCase))

            // Ask for the shortfall, not the whole line. Somebody with one of
            // the four medpens their kit wants needs three.
            .Select(line => line.Short is { } want && want > 0 && want < line.Quantity
                ? line with { Quantity = want }
                : line)];
}
