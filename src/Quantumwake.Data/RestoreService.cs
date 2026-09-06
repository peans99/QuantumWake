using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// Works out what restoring a backup would change, and then does exactly that.
/// </summary>
/// <remarks>
/// <para>
/// Two methods and one rule between them: <see cref="Plan"/> writes nothing,
/// and <see cref="Apply"/> does nothing the plan did not describe. The plan
/// carries the hash of the file it read, and applying demands it back, so a
/// file swapped between the two is refused rather than quietly restored.
/// </para>
/// <para>
/// There is no common ancestor to compare against, so "conflict" here is not
/// the version-control sense. It means the record exists on both sides, differs,
/// and the copy on this machine is not older - which is exactly when guessing
/// is worst and asking is cheapest. The default is to keep what is here,
/// because that is the work somebody did most recently.
/// </para>
/// </remarks>
public sealed class RestoreService(
    JobStore jobs,
    ChecklistStore checklists,
    TripStore trips,
    MiningLogStore mining,
    MapNoteStore notes,
    GoalStore goals,
    WipeStore wipe,
    ItemLabelStore labels,
    TombstoneStore deleted)
{
    /// <summary>Ids for the things there is only ever one of.</summary>
    private static class Single
    {
        public const string Goal = "goal";
        public const string Wipe = "wipe";
        public const string Labels = "labels";
    }

    public RestorePlan Plan(ExportBackup file, string hash)
    {
        var lines = new List<RestoreLine>();
        var gone = deleted.All();

        // Compared as text, and Bare() first. Record equality is reference
        // equality for the lists these carry - stops, items, attachments - so
        // == reports two identical records as different and every restore would
        // be one long list of conflicts. Bare() drops the change stamp and the
        // view state, so "different" means different in a way a backup can see.
        static bool Same<T>(T a, T b) where T : IStamped<T> =>
            JsonSerializer.Serialize(a.Bare()) == JsonSerializer.Serialize(b.Bare());

        void Compare<T>(string store, IReadOnlyList<T> theirs, IReadOnlyList<T> yours,
            Func<T, string> id, Func<T, string> label, Func<T, DateTimeOffset> changed)
            where T : IStamped<T>
        {
            var mine = yours.ToDictionary(id, StringComparer.Ordinal);

            foreach (var record in theirs)
            {
                var key = id(record);

                if (!mine.TryGetValue(key, out var here))
                {
                    // Deleted here beats absent here: the pilot did something
                    // deliberate, and a restore that silently undoes it is the
                    // one failure this whole feature exists to prevent.
                    var action = gone.Any(t => t.Store == store && t.Id == key)
                        ? RestoreAction.Deleted
                        : RestoreAction.Add;

                    lines.Add(new RestoreLine(store, key, label(record), action, null, changed(record)));
                    continue;
                }

                if (Same(here, record))
                {
                    lines.Add(new RestoreLine(store, key, label(record),
                        RestoreAction.Same, changed(here), changed(record)));
                    continue;
                }

                lines.Add(new RestoreLine(store, key, label(record),
                    changed(here) < changed(record) ? RestoreAction.Replace : RestoreAction.Conflict,
                    changed(here), changed(record)));
            }
        }

        Compare(TombstoneStore.Kinds.Jobs, file.Jobs, jobs.All(),
            j => j.Id, j => j.Title, j => j.ChangedAt);

        Compare(TombstoneStore.Kinds.Checklists, file.Checklists, checklists.All(),
            c => c.Id, c => c.Title, c => c.ChangedAt);

        Compare(TombstoneStore.Kinds.Trips, file.Trips, trips.All(),
            t => t.Id, t => t.Title, t => t.ChangedAt);

        Compare(TombstoneStore.Kinds.Mining, file.MiningRuns, mining.All(),
            r => r.Id, r => $"{r.Resource} at {r.Place}", r => r.ChangedAt);

        Compare(TombstoneStore.Kinds.Notes, file.Notes, notes.All(),
            n => n.Id, n => n.Title, n => n.ChangedAt);

        // The singular settings. There is only one of each, so there is nothing
        // to match on - the question is only whether the file's differs.
        if (file.Goal is { } goal)
            lines.Add(Settingal(Single.Goal, goal.Name, goals.Current, goal, goals.Current?.SetAt, goal.SetAt));

        if (file.Wipe is { } wiped)
            lines.Add(Settingal(Single.Wipe, $"History counted from {wiped.At:d}", wipe.Current, wiped, null, wiped.At));

        if (file.Labels is { } options)
            lines.Add(Settingal(Single.Labels, "Item label settings", labels.Current, options, null, null));

        return new RestorePlan(hash, lines, IgnoredCount());
    }

    /// <summary>
    /// Applies a plan, and only a plan.
    /// </summary>
    /// <returns>
    /// What was done, or null when the hash does not match the file - which
    /// means the reader approved something other than what is being applied.
    /// </returns>
    public RestoreResult? Apply(ExportBackup file, RestorePlan plan, string hash, RestoreChoices choices)
    {
        if (!string.Equals(plan.Hash, hash, StringComparison.Ordinal))
            return null;

        var restored = 0;
        var skipped = 0;

        bool Wanted(string store, string id)
        {
            var line = plan.Lines.FirstOrDefault(l => l.Store == store && l.Id == id);

            // A record the plan never mentioned is not restored. The reader
            // approved a list, and anything outside it was never shown to them.
            if (line is null) return false;

            if (choices.Runs(line)) return true;

            skipped++;
            return false;
        }

        foreach (var job in file.Jobs.Where(j => Wanted(TombstoneStore.Kinds.Jobs, j.Id)))
        {
            jobs.Put(job);
            deleted.Forget(TombstoneStore.Kinds.Jobs, job.Id);
            restored++;
        }

        foreach (var list in file.Checklists.Where(c => Wanted(TombstoneStore.Kinds.Checklists, c.Id)))
        {
            checklists.Put(list);
            deleted.Forget(TombstoneStore.Kinds.Checklists, list.Id);
            restored++;
        }

        foreach (var trip in file.Trips.Where(t => Wanted(TombstoneStore.Kinds.Trips, t.Id)))
        {
            trips.Put(trip);
            deleted.Forget(TombstoneStore.Kinds.Trips, trip.Id);
            restored++;
        }

        foreach (var run in file.MiningRuns.Where(r => Wanted(TombstoneStore.Kinds.Mining, r.Id)))
        {
            mining.Put(run);
            deleted.Forget(TombstoneStore.Kinds.Mining, run.Id);
            restored++;
        }

        foreach (var note in file.Notes.Where(n => Wanted(TombstoneStore.Kinds.Notes, n.Id)))
        {
            notes.Put(note);
            deleted.Forget(TombstoneStore.Kinds.Notes, note.Id);
            restored++;
        }

        if (file.Goal is { } goal && Wanted(Single.Goal, Single.Goal))
        {
            goals.Save(goal);
            restored++;
        }

        if (file.Wipe is { } wiped && Wanted(Single.Wipe, Single.Wipe))
        {
            wipe.Set(wiped.At, wiped.Patch, wiped.Scope);
            restored++;
        }

        if (file.Labels is { } options && Wanted(Single.Labels, Single.Labels))
        {
            labels.Save(options);
            restored++;
        }

        return new RestoreResult(restored, skipped, plan.Ignored);
    }

    private static RestoreLine Settingal<T>(
        string id, string label, T? here, T there, DateTimeOffset? yours, DateTimeOffset? theirs)
        where T : class
    {
        var action = here is null
            ? RestoreAction.Add
            : Equals(here, there)
                ? RestoreAction.Same

                // No id to match on and no history to compare, so a setting that
                // differs is always a question rather than an assumption.
                : RestoreAction.Conflict;

        return new RestoreLine(id, id, label, action, yours, theirs);
    }

    /// <summary>
    /// How many pins and tracked plans are being left alone, so the preview can
    /// say so rather than leave somebody wondering.
    /// </summary>
    private int IgnoredCount() =>
        jobs.All().Count(j => j.Pinned)
        + checklists.All().Count(c => c.Pinned)
        + trips.All().Count(t => t.Tracked);
}
