import io

def patch(path, pairs):
    s = io.open(path, encoding='utf-8-sig').read()
    for old, new in pairs:
        assert old in s, f'{path}: {old[:70]}'
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

# LibraryStats has required members, so a real empty one is the honest base.
patch('tests/Quantumwake.Tests/KitTests.cs', [(
"""    private static LibraryStats Stats(
        IReadOnlyList<LoadoutSlot>? loadout = null, IReadOnlyList<StashLocation>? stash = null) =>
        new() { Loadout = loadout ?? [], Stash = stash ?? [] };""",
"""    /// <summary>
    /// A real empty stats object with only the two lists a kit reads.
    /// </summary>
    /// <remarks>
    /// Built from an empty library rather than hand-rolled: LibraryStats has a
    /// dozen required members that have nothing to do with kits, and listing
    /// them here would be a second thing to update every time one is added.
    /// </remarks>
    private LibraryStats Stats(
        IReadOnlyList<LoadoutSlot>? loadout = null, IReadOnlyList<StashLocation>? stash = null)
    {
        using var sessions = new SessionStore(":memory:");

        return new LogLibrary(sessions).Stats() with
        {
            Loadout = loadout ?? [],
            Stash = stash ?? [],
        };
    }""")])

# The three suites that build a backup or a restore now need the kit store.
patch('tests/Quantumwake.Tests/BackupTests.cs', [(
"""        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root));""",
"""        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new KitStore(_root));""")])

patch('tests/Quantumwake.Tests/RestoreTests.cs', [
(   """        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new LogLibrary(_sessions));""",
    """        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new LogLibrary(_sessions), new KitStore(_root));"""),
(   """    private static ExportBackup Backup(params Job[] jobs) =>
        new(jobs, [], [], [], [], []);""",
    """    private static ExportBackup Backup(params Job[] jobs) =>
        new(jobs, [], [], [], [], [], []);"""),
(   """            Backup: new ExportBackup([], [], [], [], [], []));""",
    """            Backup: new ExportBackup([], [], [], [], [], [], []));"""),
])

patch('tests/Quantumwake.Tests/RestoreReviewFixTests.cs', [
(   """        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new LogLibrary(new SessionStore(":memory:")));""",
    """        new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root),
        new LogLibrary(new SessionStore(":memory:")), new KitStore(_root));"""),
(   """            new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root), library);""",
    """            new WipeStore(_root), new ItemLabelStore(_root), new TombstoneStore(_root), library,
            new KitStore(_root));"""),
(   """new ExportBackup([AJob("j1", "Buy better armour", Newer)], [], [], [], [], []);""",
    """new ExportBackup([AJob("j1", "Buy better armour", Newer)], [], [], [], [], [], []);"""),
(   """        var file = new ExportBackup([], [], [trip with { Title = "Ore run v2", ModifiedAt = Newer, Tracked = false }],
            [], [], []);""",
    """        var file = new ExportBackup([], [], [trip with { Title = "Ore run v2", ModifiedAt = Newer, Tracked = false }],
            [], [], [], []);"""),
(   """        var file = new ExportBackup([], [], [], [], [],
            [new Tombstone(TombstoneStore.Kinds.Jobs, "gone", Old)]);""",
    """        var file = new ExportBackup([], [], [], [], [],
            [new Tombstone(TombstoneStore.Kinds.Jobs, "gone", Old)], []);"""),
(   """        var file = new ExportBackup([], [], [], [], [],
            [new Tombstone(TombstoneStore.Kinds.Jobs, "j1", Old)]);""",
    """        var file = new ExportBackup([], [], [], [], [],
            [new Tombstone(TombstoneStore.Kinds.Jobs, "j1", Old)], []);"""),
(   """        var file = new ExportBackup([], [], [], [], [], [], Wipe: wiped);""",
    """        var file = new ExportBackup([], [], [], [], [], [], [], Wipe: wiped);"""),
(   """        var file = new ExportBackup(
            [AJob("j1", "Theirs", Newer)], [], [], [], [], []);""",
    """        var file = new ExportBackup(
            [AJob("j1", "Theirs", Newer)], [], [], [], [], [], []);"""),
])

print('call sites updated')
