using Quantumwake.Core.State;
using Quantumwake.Data;
using System.Text.Json;

namespace Quantumwake.Tests;

/// <summary>
/// The shape of a file one pilot hands another.
/// </summary>
/// <remarks>
/// This is the one format in the project that leaves the machine, so a field
/// renamed by accident is not a refactor - it is every file already sent
/// becoming unreadable, on installs nobody here controls. These tests exist to
/// make that a failing build rather than a support thread.
/// </remarks>
public class ExportDocumentTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-export-{Guid.NewGuid():N}");

    private readonly SessionStore _sessions = new(":memory:");
    private readonly LogLibrary _library;
    private readonly JobStore _jobs;
    private readonly ChecklistStore _checklists;
    private readonly TripStore _trips;
    private readonly WipeStore _wipe;
    private readonly ExportBuilder _builder;

    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    public ExportDocumentTests()
    {
        _library = new LogLibrary(_sessions);
        _jobs = new JobStore(_directory);
        _checklists = new ChecklistStore(_directory);
        _trips = new TripStore(_directory);
        _wipe = new WipeStore(_directory);
        _builder = new ExportBuilder(_library, _jobs, _checklists, _trips, _wipe);
    }

    private void SaveSession(string id, DateTimeOffset started,
        IEnumerable<CommodityTrade>? trades = null,
        IEnumerable<BlueprintReceipt>? blueprints = null) =>
        _sessions.Save(
            new SessionSummary
            {
                Id = id,
                SourceFile = $"{id}.log",
                StartedAt = started,
                EndedAt = started.AddHours(3),
                Handle = "nekron",
                Trades = [.. trades ?? []],
                Blueprints = [.. blueprints ?? []],
            },
            $"fingerprint:{id}");

    private ExportFile Build(ExportChoice choice) =>
        _builder.Build(choice, new ExportProducer("Quantum Wake", "0.8.0"), Now);

    [Fact]
    public void An_empty_choice_is_a_document_that_claims_nothing()
    {
        var file = Build(new ExportChoice());

        Assert.Equal(ExportDocument.Format, file.Format);
        Assert.Equal(1, file.FormatVersion);
        Assert.Empty(file.Classes);
        Assert.Null(file.Receipts);
        Assert.Null(file.Blueprints);
        Assert.Null(file.Authored);
    }

    /// <summary>
    /// An empty window and a withheld class must not look alike: one is a fact
    /// about the sender's week, the other about their consent.
    /// </summary>
    [Fact]
    public void A_class_asked_for_but_empty_is_still_declared()
    {
        var file = Build(new ExportChoice(Receipts: true));

        Assert.Contains(ExportDocument.Receipts, file.Classes);
        Assert.NotNull(file.Receipts);
        Assert.Empty(file.Receipts.Rows);
        Assert.Null(file.Receipts.ObservedFrom);
    }

    [Fact]
    public void Receipts_carry_the_window_the_dates_they_span_and_their_caveats()
    {
        var now = DateTimeOffset.UtcNow;
        SaveSession("s1", now.AddDays(-3),
        [
            new CommodityTrade(now.AddDays(-3), "TDD", 288_000m, 96, true, "Cargo", "guid-a"),
            new CommodityTrade(now.AddDays(-1), "TDD", 100_000m, 50, false, "Cargo", "guid-b"),
        ]);

        var receipts = Build(new ExportChoice(Receipts: true)).Receipts!;

        Assert.Equal(7, receipts.WindowDays);
        Assert.Equal(2, receipts.Rows.Count);

        // Oldest first, and the span is read off the rows rather than the clock.
        Assert.True(receipts.ObservedFrom < receipts.ObservedTo);
        Assert.Equal(receipts.Rows[0].At, receipts.ObservedFrom);
        Assert.Equal(receipts.Rows[^1].At, receipts.ObservedTo);

        Assert.Contains(ExportCaveats.PlaceInferred, receipts.Caveats);
        Assert.Contains(ExportCaveats.RequestedNotConfirmed, receipts.Caveats);
        Assert.Equal("guid-a", receipts.Rows[0].ResourceId);
    }

    /// <summary>A file written today can carry a price from March.</summary>
    [Fact]
    public void The_age_of_the_data_is_not_the_age_of_the_file()
    {
        var now = DateTimeOffset.UtcNow;
        SaveSession("s1", now.AddDays(-4),
            [new CommodityTrade(now.AddDays(-4), "TDD", 1m, 1, true, "Cargo", "guid-a")]);

        var file = Build(new ExportChoice(Receipts: true));

        Assert.Equal(Now, file.ExportedAt);
        Assert.NotEqual(file.ExportedAt, file.Receipts!.ObservedTo);
    }

    [Fact]
    public void This_machines_view_state_does_not_travel()
    {
        var job = _jobs.Add("Craft it", "craft", null, [new JobItem("Agricium", 4, "SCU")]);
        _jobs.TogglePin(job.Id);
        var list = _checklists.Add("Departure");
        _checklists.TogglePin(list.Id);

        var authored = Build(new ExportChoice(Authored: true)).Authored!;

        Assert.All(authored.Jobs, j => Assert.False(j.Pinned));
        Assert.All(authored.Checklists, c => Assert.False(c.Pinned));
        Assert.All(authored.Trips, t => Assert.False(t.Tracked));
    }

    [Fact]
    public void Blueprints_are_a_name_and_a_date_and_say_which_date_it_is()
    {
        var now = DateTimeOffset.UtcNow;
        SaveSession("s1", now.AddDays(-40), blueprints:
            [new BlueprintReceipt(now.AddDays(-40), "Omnisky IX")]);

        var blueprints = Build(new ExportChoice(Blueprints: true)).Blueprints!;

        var row = Assert.Single(blueprints.Rows);
        Assert.Equal("Omnisky IX", row.Name);
        Assert.Contains(ExportCaveats.EarliestSighting, blueprints.Caveats);
    }

    /// <summary>
    /// The wire format is camelCase and must stay that way: the stores' PascalCase
    /// is JsonSerializer's default rather than anybody's decision, and an accident
    /// is not worth carrying across a machine boundary.
    /// </summary>
    [Fact]
    public void The_document_is_written_in_camel_case()
    {
        var now = DateTimeOffset.UtcNow;
        SaveSession("s1", now.AddDays(-2),
            [new CommodityTrade(now.AddDays(-2), "TDD", 288_000m, 96, true, "Cargo", "guid-a")]);

        var json = JsonSerializer.Serialize(
            Build(new ExportChoice(Receipts: true)), ExportDocument.Json);

        Assert.Contains("\"formatVersion\": 1", json);
        Assert.Contains("\"contentVersion\": 1", json);
        Assert.Contains("\"exportedAt\"", json);
        Assert.Contains("\"observedTo\"", json);
        Assert.Contains("\"resourceId\": \"guid-a\"", json);
        Assert.Contains("\"quantumwake.export\"", json);

        Assert.DoesNotContain("\"FormatVersion\"", json);
        Assert.DoesNotContain("\"ObservedTo\"", json);

        // A class nobody asked for is absent, not a null the reader has to handle.
        Assert.DoesNotContain("\"blueprints\":", json);
        Assert.DoesNotContain("null", json);
    }

    /// <summary>A document must survive the trip out and back unchanged.</summary>
    [Fact]
    public void A_document_reads_back_as_what_was_written()
    {
        var now = DateTimeOffset.UtcNow;
        SaveSession("s1", now.AddDays(-2),
            [new CommodityTrade(now.AddDays(-2), "TDD", 288_000m, 96, true, "Cargo", "guid-a")]);
        _jobs.Add("Craft it", "craft", null, [new JobItem("Agricium", 4, "SCU")]);

        var written = Build(new ExportChoice(Receipts: true, Authored: true));
        var json = JsonSerializer.Serialize(written, ExportDocument.Json);
        var read = JsonSerializer.Deserialize<ExportFile>(json, ExportDocument.Json)!;

        Assert.Equal(written.Classes, read.Classes);
        Assert.Equal(written.Receipts!.Rows[0].ResourceId, read.Receipts!.Rows[0].ResourceId);
        Assert.Equal(written.Receipts.Rows[0].UnitPrice, read.Receipts.Rows[0].UnitPrice);
        Assert.Equal(written.Authored!.Jobs[0].Title, read.Authored!.Jobs[0].Title);
    }

    /// <summary>A file from any build reads, whatever casing it was written in.</summary>
    [Fact]
    public void A_pascal_case_file_still_reads()
    {
        var pascal =
            "{\"Format\":\"quantumwake.export\",\"FormatVersion\":1,\"ContentVersion\":1,"
            + "\"ExportedAt\":\"2026-08-24T12:00:00+00:00\","
            + "\"Producer\":{\"App\":\"Quantum Wake\",\"Version\":\"0.8.0\"},"
            + "\"Classes\":[\"blueprints\"],"
            + "\"Blueprints\":{\"Caveats\":[],\"Rows\":"
            + "[{\"At\":\"2026-08-01T00:00:00+00:00\",\"Name\":\"Omnisky IX\"}]}}";

        var read = JsonSerializer.Deserialize<ExportFile>(pascal, ExportDocument.Json)!;

        Assert.Equal(ExportDocument.Format, read.Format);
        Assert.Equal("Omnisky IX", read.Blueprints!.Rows[0].Name);
    }

    /// <summary>
    /// A cut that lands inside a surrogate pair leaves half a character, which
    /// is not text any more. Unreachable from the app's own forms; an imported
    /// file chooses its own lengths.
    /// </summary>
    [Fact]
    public void Clipping_never_splits_a_character_in_half()
    {
        var text = new string('a', Sanitise.Title - 1) + "\U0001F680\U0001F680";

        var clipped = Sanitise.Clip(text, Sanitise.Title);

        Assert.Equal(Sanitise.Title - 1, clipped.Length);
        Assert.DoesNotContain(clipped, char.IsSurrogate);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _sessions.Dispose();
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }
}
