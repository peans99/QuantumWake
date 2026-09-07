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
    TombstoneStore deleted,
    LogLibrary library,
    KitStore kits)
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

        Compare(TombstoneStore.Kinds.Kits, file.Kits, kits.All(),
            k => k.Id, k => k.Name, k => k.ChangedAt);

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
    /// Applies a plan, and only a plan - all of it or none of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every store is photographed before anything is written, and put back if
    /// any write fails. Without that, a disk that fills up halfway leaves some
    /// records replaced and some not, with no record of which - and the page
    /// says "nothing was restored", which is the one thing that is certainly
    /// untrue.
    /// </para>
    /// <para>
    /// The rollback is itself best-effort: putting records back needs the same
    /// disk that just refused a write. So the result says what happened rather
    /// than promising the machine is untouched.
    /// </para>
    /// </remarks>
    /// <returns>
    /// What was done, or null when the hash does not match the file - which
    /// means the reader approved something other than what is being applied.
    /// </returns>
    public RestoreResult? Apply(ExportBackup file, RestorePlan plan, string hash, RestoreChoices choices)
    {
        if (!string.Equals(plan.Hash, hash, StringComparison.Ordinal))
            return null;

        var before = Photograph();
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

        try
        {
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

            foreach (var kit in file.Kits.Where(k => Wanted(TombstoneStore.Kinds.Kits, k.Id)))
            {
                kits.Put(kit);
                deleted.Forget(TombstoneStore.Kinds.Kits, kit.Id);
                restored++;
            }

            if (file.Goal is { } goal && Wanted(Single.Goal, Single.Goal))
            {
                goals.Save(goal);
                restored++;
            }

            if (file.Wipe is { } wiped && Wanted(Single.Wipe, Single.Wipe))
            {
                // The library holds its own copy and every page counts against
                // it, so setting only the store moves the line on the settings
                // screen and nowhere else.
                library.Wipe = wipe.Set(wiped.At, wiped.Patch, wiped.Scope);
                restored++;
            }

            if (file.Labels is { } options && Wanted(Single.Labels, Single.Labels))
            {
                labels.Save(options);
                restored++;
            }

            AdoptDeletions(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var undone = PutBack(before);

            return new RestoreResult(0, skipped, plan.Ignored, Failed: true, RolledBack: undone);
        }

        return new RestoreResult(restored, skipped, plan.Ignored);
    }

    /// <summary>
    /// Takes on the deletions the file remembers, where they do not contradict
    /// what is here.
    /// </summary>
    /// <remarks>
    /// Without this a fresh machine forgets every deletion the moment it is set
    /// up: restore a recent backup, then an older one, and everything thrown
    /// away in between walks back in with nothing to say it was ever deleted.
    ///
    /// A tombstone for a record that is present here is dropped rather than
    /// acted on. The record exists, the reader was never shown a line proposing
    /// to remove it, and a restore that deletes something it did not mention is
    /// exactly the failure this whole feature was built to prevent.
    /// </remarks>
    private void AdoptDeletions(ExportBackup file)
    {
        var here = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in jobs.All().Select(j => $"{TombstoneStore.Kinds.Jobs}:{j.Id}")) here.Add(id);
        foreach (var id in checklists.All().Select(c => $"{TombstoneStore.Kinds.Checklists}:{c.Id}")) here.Add(id);
        foreach (var id in trips.All().Select(t => $"{TombstoneStore.Kinds.Trips}:{t.Id}")) here.Add(id);
        foreach (var id in mining.All().Select(r => $"{TombstoneStore.Kinds.Mining}:{r.Id}")) here.Add(id);
        foreach (var id in notes.All().Select(n => $"{TombstoneStore.Kinds.Notes}:{n.Id}")) here.Add(id);
        foreach (var id in kits.All().Select(k => $"{TombstoneStore.Kinds.Kits}:{k.Id}")) here.Add(id);

        foreach (var stone in file.Deleted)
        {
            if (here.Contains($"{stone.Store}:{stone.Id}")) continue;

            deleted.Record(stone.Store, stone.Id);
        }
    }

    /// <summary>Everything that could be written, as it stands right now.</summary>
    private (IReadOnlyList<Job> Jobs, IReadOnlyList<Checklist> Lists, IReadOnlyList<Trip> Trips,
        IReadOnlyList<MiningRun> Runs, IReadOnlyList<MapNote> Notes, Goal? Goal, Wipe? Wipe,
        TextOverlayOptions Labels, IReadOnlyList<Tombstone> Deleted, IReadOnlyList<Kit> Kits) Photograph() =>
        (jobs.All(), checklists.All(), trips.All(), mining.All(), notes.All(),
         goals.Current, wipe.Current, labels.Current, deleted.All(), kits.All());

    /// <summary>
    /// Puts a photograph back, and says whether all of it landed.
    /// </summary>
    /// <remarks>
    /// Best-effort by nature: this needs the same disk that just refused a
    /// write. Anything that fails here is left rather than retried, because a
    /// rollback that loops on a full disk is worse than one that stops and says
    /// so.
    /// </remarks>
    private bool PutBack((IReadOnlyList<Job> Jobs, IReadOnlyList<Checklist> Lists, IReadOnlyList<Trip> Trips,
        IReadOnlyList<MiningRun> Runs, IReadOnlyList<MapNote> Notes, Goal? Goal, Wipe? Wipe,
        TextOverlayOptions Labels, IReadOnlyList<Tombstone> Deleted, IReadOnlyList<Kit> Kits) before)
    {
        var whole = true;

        // Anything the failed run added is not in the photograph, so putting
        // the photograph back leaves it behind. A rollback that reports success
        // over a record the reader never agreed to is the same lie the failure
        // reporting was fixed to stop telling.
        void Drop<T>(IEnumerable<T> now, IReadOnlyList<T> before, Func<T, string> id, Action<string> remove)
        {
            var kept = before.Select(id).ToHashSet(StringComparer.Ordinal);

            foreach (var added in now.Select(id).Where(x => !kept.Contains(x)).ToList())
            {
                try
                {
                    remove(added);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    whole = false;
                }
            }
        }

        void Try(Action write)
        {
            try
            {
                write();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                whole = false;
            }
        }

        Drop(jobs.All(), before.Jobs, j => j.Id, id => jobs.Remove(id));
        Drop(checklists.All(), before.Lists, c => c.Id, id => checklists.Remove(id));
        Drop(trips.All(), before.Trips, t => t.Id, id => trips.Remove(id));
        Drop(mining.All(), before.Runs, r => r.Id, id => mining.RemoveLoudly(id));
        Drop(notes.All(), before.Notes, n => n.Id, id => notes.Remove(id));
        Drop(kits.All(), before.Kits, k => k.Id, id => kits.Remove(id));

        foreach (var job in before.Jobs) Try(() => jobs.Put(job));
        foreach (var list in before.Lists) Try(() => checklists.Put(list));
        foreach (var trip in before.Trips) Try(() => trips.Put(trip));
        foreach (var run in before.Runs) Try(() => mining.Put(run));
        foreach (var note in before.Notes) Try(() => notes.Put(note));
        foreach (var kit in before.Kits) Try(() => kits.Put(kit));

        Try(() => goals.Save(before.Goal));
        Try(() => labels.Save(before.Labels));

        if (before.Wipe is { } wiped)
            Try(() => library.Wipe = wipe.Set(wiped.At, wiped.Patch, wiped.Scope));

        // Deletions the run had forgotten on the way through.
        foreach (var stone in before.Deleted) Try(() => deleted.Record(stone.Store, stone.Id));

        return whole;
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
