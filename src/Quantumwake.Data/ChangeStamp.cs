namespace Quantumwake.Data;

/// <summary>
/// An authored record that can say when it last changed.
/// </summary>
/// <remarks>
/// Implemented by the records a backup has to reconcile. <see cref="Bare"/> is
/// the record with its own stamp removed, so comparing two of them asks "did
/// the content change" rather than "is the timestamp different", which would
/// always be true.
/// </remarks>
public interface IStamped<T>
{
    /// <summary>Identity within its own store. Ids are not unique across stores.</summary>
    string StampId { get; }

    /// <summary>This record with its change stamp cleared, for comparison only.</summary>
    T Bare();

    /// <summary>This record, stamped as having changed at the given moment.</summary>
    T Stamped(DateTimeOffset at);
}

/// <summary>
/// Stamps <c>ModifiedAt</c> on the records whose content actually changed.
/// </summary>
/// <remarks>
/// <para>
/// The alternative was stamping by hand in every mutator, and there are 27 of
/// them across the five authored stores - 27 chances to forget, in a codebase
/// where the same worry is already written down about parser rules kept in two
/// places. A restore preview built on timestamps that are right most of the
/// time quietly resolves conflicts the wrong way, and nothing about the page
/// would look broken.
/// </para>
/// <para>
/// So the rule lives here and runs at the one point every mutator already has
/// to reach: the store's own save. Content is compared against what was last
/// written, so toggling a checkbox back and forth leaves the record where it
/// started rather than looking newer than a backup that agrees with it.
/// </para>
/// <para>
/// The comparison is on serialised text rather than record equality because
/// these records hold lists - stops, items, actions - and record equality is
/// reference equality for those, which reports every save as a change.
/// </para>
/// </remarks>
public sealed class ChangeStamp<T> where T : IStamped<T>
{
    private readonly Func<T, string> _serialise;
    private readonly Dictionary<string, string> _written = new(StringComparer.Ordinal);

    /// <param name="serialise">
    /// How to render one record for comparison. Passed in rather than fixed so
    /// each store uses the same serialiser options it saves with.
    /// </param>
    public ChangeStamp(Func<T, string> serialise) => _serialise = serialise;

    /// <summary>
    /// Takes what was loaded from disk as the baseline, so the first save after
    /// startup does not stamp every record as changed.
    /// </summary>
    public void Loaded(IReadOnlyList<T> records)
    {
        _written.Clear();

        foreach (var record in records)
            _written[record.StampId] = _serialise(record.Bare());
    }

    /// <summary>
    /// Takes a record as already current, without stamping it.
    /// </summary>
    /// <remarks>
    /// For a restore, which is the one write that must not count as an edit.
    /// A restored record carries the change time it had when it was backed up;
    /// stamping it "now" would make every restored record look freshly edited
    /// and win the next conflict against the machine it came from.
    /// </remarks>
    public void Adopt(T record) => _written[record.StampId] = _serialise(record.Bare());

    /// <summary>
    /// Stamps whatever changed, in place, and returns whether anything did.
    /// </summary>
    /// <remarks>
    /// Records that have gone are dropped from the baseline: an id reused after
    /// a delete is a new record and must not inherit the old one's comparison.
    /// </remarks>
    public bool Apply(IList<T> records, DateTimeOffset at)
    {
        var changed = false;
        var live = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < records.Count; i++)
        {
            var id = records[i].StampId;
            live.Add(id);

            var bare = _serialise(records[i].Bare());

            if (_written.TryGetValue(id, out var before) && before == bare)
                continue;

            _written[id] = bare;
            records[i] = records[i].Stamped(at);
            changed = true;
        }

        foreach (var gone in _written.Keys.Where(k => !live.Contains(k)).ToList())
            _written.Remove(gone);

        return changed;
    }
}
