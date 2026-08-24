using Quantumwake.Data;
using System.Text.Json;

namespace Quantumwake.Tests;

/// <summary>
/// Files other people shared, kept where they can be taken away again.
/// </summary>
/// <remarks>
/// The promise this store makes is that an import can be undone completely and
/// that undoing it cannot touch the pilot's own work. That is why nothing is
/// ever folded into jobs.json, why removing a batch is one operation, and why
/// this is the one store that writes through a temporary file.
/// </remarks>
public class ImportStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-imports-{Guid.NewGuid():N}");

    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private ImportStore NewStore() => new(_directory);

    private static string Document(string handle = "nekron", string blueprint = "Omnisky IX") =>
        JsonSerializer.Serialize(
            new ExportFile(
                ExportDocument.Format, 1, 1, Now,
                new ExportProducer("Quantum Wake", "0.8.0"),
                [ExportDocument.Blueprints],
                handle, null,
                Blueprints: new ExportBlueprints(Now, Now, [],
                    [new ExportBlueprintRow(Now, blueprint)])),
            ExportDocument.Json);

    private ImportBatch Add(ImportStore store, string text, string name = "friend.json")
    {
        var (reading, problem) = ImportReader.Read(text, Now);
        Assert.Null(problem);
        return store.Add(reading!, ImportStore.FingerprintOf(text), name, Now);
    }

    [Fact]
    public void A_batch_survives_a_restart_with_what_it_came_from()
    {
        var text = Document();
        var added = Add(NewStore(), text);

        var batch = Assert.Single(NewStore().All());

        Assert.Equal(added.Id, batch.Id);
        Assert.Equal("nekron", batch.Handle);
        Assert.Equal("friend.json", batch.SourceName);
        Assert.Equal(1, batch.Counts.Blueprints);
        Assert.Equal("Omnisky IX", batch.Blueprints!.Rows[0].Name);
    }

    /// <summary>
    /// The same bytes under a different name are the same file, so a renamed
    /// copy is recognised too.
    /// </summary>
    [Fact]
    public void The_same_file_is_recognised_whatever_it_was_renamed_to()
    {
        var text = Document();
        var store = NewStore();
        Add(store, text, "friend.json");

        Assert.NotNull(store.Matching(ImportStore.FingerprintOf(text)));
        Assert.Null(store.Matching(ImportStore.FingerprintOf(Document(blueprint: "Something else"))));
    }

    /// <summary>
    /// Re-importing after a purge is legitimate, so a duplicate is a question
    /// the endpoint asks rather than a refusal the store enforces.
    /// </summary>
    [Fact]
    public void The_same_file_can_be_taken_twice_when_that_is_what_was_meant()
    {
        var text = Document();
        var store = NewStore();

        Add(store, text);
        Add(store, text);

        Assert.Equal(2, store.All().Count);
    }

    [Fact]
    public void Removing_one_batch_leaves_the_others_alone()
    {
        var store = NewStore();
        var first = Add(store, Document("nekron"));
        var second = Add(store, Document("someone-else"));

        Assert.True(store.Remove(first.Id));

        var left = Assert.Single(NewStore().All());
        Assert.Equal(second.Id, left.Id);
        Assert.False(store.Remove(first.Id));
    }

    /// <summary>
    /// An emptied batch is still the record that this file was read, and still
    /// holds the fingerprint, so the same file coming round again is noticed.
    /// </summary>
    [Fact]
    public void Dropping_the_last_class_leaves_the_batch_behind()
    {
        var text = Document();
        var store = NewStore();
        var batch = Add(store, text);

        Assert.True(store.RemoveClass(batch.Id, ExportDocument.Blueprints));

        var after = Assert.Single(store.All());
        Assert.Null(after.Blueprints);
        Assert.Empty(after.Classes);
        Assert.Equal(0, after.Counts.Blueprints);
        Assert.NotNull(store.Matching(ImportStore.FingerprintOf(text)));
    }

    [Fact]
    public void Hiding_is_not_deleting()
    {
        var store = NewStore();
        var batch = Add(store, Document());

        Assert.True(store.ToggleHidden(batch.Id));
        Assert.True(Assert.Single(store.All()).Hidden);

        Assert.True(store.ToggleHidden(batch.Id));
        Assert.False(Assert.Single(NewStore().All()).Hidden);
    }

    [Fact]
    public void Clearing_takes_everything_and_says_how_much()
    {
        var store = NewStore();
        Add(store, Document("a"));
        Add(store, Document("b"));

        Assert.Equal(2, store.Clear());
        Assert.Empty(NewStore().All());
    }

    /// <summary>
    /// A batch this build cannot read must not disappear. A session cache can be
    /// dropped because the logs rebuild it; an import cannot, because the file it
    /// came from is on somebody else's machine.
    /// </summary>
    [Fact]
    public void A_batch_from_a_newer_format_still_describes_itself()
    {
        var store = NewStore();
        var batch = Add(store, Document());

        // What a downgrade, or a later breaking bump, would leave on disk.
        var path = Path.Combine(_directory, "imports.json");
        File.WriteAllText(path,
            File.ReadAllText(path).Replace("\"FormatVersion\":1", "\"FormatVersion\":99"));

        var after = Assert.Single(NewStore().All());

        Assert.Equal(batch.Id, after.Id);
        Assert.False(after.Readable);
        Assert.Equal("nekron", after.Handle);
        Assert.Equal(1, after.Counts.Blueprints);
    }

    /// <summary>
    /// Falling back to empty and then saving would erase every import there is,
    /// and the files they came from are not here to be fetched again.
    /// </summary>
    [Fact]
    public void A_corrupt_file_is_moved_aside_rather_than_overwritten()
    {
        Add(NewStore(), Document());

        var path = Path.Combine(_directory, "imports.json");
        File.WriteAllText(path, "{ this is not json");

        var store = NewStore();

        Assert.Empty(store.All());
        Assert.NotNull(store.Quarantined);
        Assert.Contains("corrupt", store.Quarantined);
        Assert.Single(Directory.GetFiles(_directory, "imports.json.corrupt-*"));

        // And the next save does not resurrect the broken one.
        Add(store, Document("someone-else"));
        Assert.Single(NewStore().All());
    }

    /// <summary>
    /// The write goes through a temporary file, so a run that dies mid-save
    /// leaves the previous batches intact rather than a truncated file.
    /// </summary>
    [Fact]
    public void A_save_never_leaves_a_half_written_file()
    {
        var store = NewStore();
        Add(store, Document("a"));
        Add(store, Document("b"));

        var path = Path.Combine(_directory, "imports.json");

        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.Equal(2, JsonSerializer.Deserialize<List<ImportBatch>>(File.ReadAllText(path))!.Count);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }
}
