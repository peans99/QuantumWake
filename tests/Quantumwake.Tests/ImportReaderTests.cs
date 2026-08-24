using Quantumwake.Core.State;
using Quantumwake.Data;
using System.Text.Json;

namespace Quantumwake.Tests;

/// <summary>
/// Reading a file somebody else wrote.
/// </summary>
/// <remarks>
/// This is the only place in the app that takes a whole document from a
/// stranger, so it is the only place that has to assume one is hostile. The
/// page draws with textContent throughout, so the worry is not script: it is
/// sinking under a file too big to hold, and poisoning the reader's own view of
/// their own data - a date in 2074 pins itself to the top of a list sorted by
/// date, and one Infinity in a quantity makes a progress bar read NaN% on Now
/// and in the overlay.
/// </remarks>
public class ImportReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sane = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    private static string Wrap(string classes, string body) =>
        "{\"format\":\"quantumwake.export\",\"formatVersion\":1,\"contentVersion\":1,"
        + "\"exportedAt\":\"2026-08-24T12:00:00+00:00\","
        + "\"producer\":{\"app\":\"Quantum Wake\",\"version\":\"0.8.0\"},"
        + $"\"classes\":[{classes}],{body}}}";

    private static ImportReading Read(string text)
    {
        var (reading, problem) = ImportReader.Read(text, Now);
        Assert.Null(problem);
        return reading!;
    }

    private static ImportProblem Refuse(string text)
    {
        var (reading, problem) = ImportReader.Read(text, Now);
        Assert.Null(reading);
        return problem!;
    }

    /* ---------- refused whole ---------- */

    [Fact]
    public void Something_that_is_not_an_export_is_named_as_such()
    {
        // A file picker accepts anything, and jobs.json is the file most likely
        // to be picked by mistake: it lives beside the exports and ends in json.
        var problem = Refuse("[{\"Id\":\"abc\",\"Title\":\"Craft it\"}]");

        Assert.Contains("not readable as JSON", problem.Message);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public void A_json_object_without_the_discriminator_is_not_an_export()
    {
        var problem = Refuse("{\"formatVersion\":1,\"classes\":[\"receipts\"]}");

        Assert.Contains("not a Quantum Wake export", problem.Message);
    }

    /// <summary>
    /// Half a document presented as a friend's data is worse than none, so a
    /// newer format is refused rather than read for the parts that still fit.
    /// </summary>
    [Fact]
    public void A_file_from_a_newer_build_is_refused_whole_and_says_which()
    {
        var problem = Refuse(
            "{\"format\":\"quantumwake.export\",\"formatVersion\":99,\"contentVersion\":1,"
            + "\"exportedAt\":\"2026-08-24T12:00:00+00:00\","
            + "\"producer\":{\"app\":\"Quantum Wake\",\"version\":\"9.0.0\"},"
            + "\"classes\":[\"receipts\"]}");

        Assert.Contains("newer Quantum Wake", problem.Message);
        Assert.Contains("format 99", problem.Message);
        Assert.Contains("this build reads 1", problem.Message);
    }

    [Fact]
    public void A_file_carrying_no_class_this_build_knows_is_refused()
    {
        var problem = Refuse(Wrap("\"telemetry\"", "\"receipts\":null"));

        Assert.Contains("does not carry anything", problem.Message);
    }

    [Fact]
    public void An_empty_file_is_refused_rather_than_read_as_nothing()
    {
        Assert.Contains("empty", Refuse("   ").Message);
    }

    [Fact]
    public void A_file_past_the_size_cap_is_refused_before_it_is_parsed()
    {
        Assert.Equal(413, ImportReader.TooBig(ImportReader.MaxBytes + 1)!.Status);
        Assert.Null(ImportReader.TooBig(ImportReader.MaxBytes));

        var huge = Wrap("\"blueprints\"",
            "\"blueprints\":{\"caveats\":[],\"rows\":[{\"at\":\"2026-08-20T09:00:00+00:00\",\"name\":\""
            + new string('x', ImportReader.MaxBytes + 16) + "\"}]}");

        Assert.Equal(413, Refuse(huge).Status);
    }

    /* ---------- rows dropped ---------- */

    /// <summary>
    /// Needed is a double, and 1e400 is valid JSON that parses to Infinity. It
    /// would reach jobProgress as NaN% on the Jobs page, on Now, and in the
    /// overlay - the one number in this format that poisons something a reader
    /// looks at rather than merely being wrong.
    /// </summary>
    [Fact]
    public void An_unbounded_quantity_never_reaches_a_progress_bar()
    {
        var reading = Read(Wrap("\"authored\"",
            "\"authored\":{\"jobs\":[{\"id\":\"abc123\",\"title\":\"Craft it\",\"kind\":\"craft\","
            + "\"createdAt\":\"2026-08-20T09:00:00+00:00\",\"done\":false,\"items\":["
            + "{\"name\":\"Runaway\",\"needed\":1e400,\"unit\":\"SCU\"},"
            + "{\"name\":\"Negative\",\"needed\":-5,\"unit\":\"SCU\"},"
            + "{\"name\":\"Agricium\",\"needed\":4,\"unit\":\"SCU\"}]}],"
            + "\"checklists\":[],\"trips\":[]}"));

        var job = Assert.Single(reading.Document.Authored!.Jobs);
        var item = Assert.Single(job.Items);

        Assert.Equal("Agricium", item.Name);
        Assert.All(job.Items, i => Assert.True(double.IsFinite(i.Needed)));
    }

    /// <summary>
    /// Every list this joins is sorted by date, so one row from 2074 sits at the
    /// top of the Logbook for ever and no amount of scrolling gets past it.
    /// </summary>
    [Fact]
    public void A_date_outside_living_memory_is_dropped_and_counted()
    {
        var reading = Read(Wrap("\"receipts\"",
            "\"receipts\":{\"windowDays\":7,\"caveats\":[],\"rows\":["
            + "{\"at\":\"2074-01-01T00:00:00+00:00\",\"isSell\":true,\"place\":\"TDD\",\"scu\":1,\"amount\":1,\"unitPrice\":1},"
            + "{\"at\":\"0001-01-01T00:00:00+00:00\",\"isSell\":true,\"place\":\"TDD\",\"scu\":1,\"amount\":1,\"unitPrice\":1},"
            + "{\"at\":\"2026-08-20T09:00:00+00:00\",\"isSell\":true,\"place\":\"TDD\",\"scu\":96,\"amount\":288000,\"unitPrice\":3000}]}"));

        var row = Assert.Single(reading.Document.Receipts!.Rows);
        Assert.Equal(Sane, row.At);
        Assert.Equal(2, reading.Rejected.Receipts);
        Assert.Equal(1, reading.Counts.Receipts);
    }

    [Fact]
    public void An_impossible_hold_or_a_negative_sale_is_dropped()
    {
        var reading = Read(Wrap("\"receipts\"",
            "\"receipts\":{\"windowDays\":7,\"caveats\":[],\"rows\":["
            + "{\"at\":\"2026-08-20T09:00:00+00:00\",\"isSell\":true,\"place\":\"TDD\",\"scu\":9999999,\"amount\":1,\"unitPrice\":1},"
            + "{\"at\":\"2026-08-20T09:00:00+00:00\",\"isSell\":true,\"place\":\"TDD\",\"scu\":10,\"amount\":-500,\"unitPrice\":1},"
            + "{\"at\":\"2026-08-20T09:00:00+00:00\",\"isSell\":true,\"place\":\"TDD\",\"scu\":96,\"amount\":288000,\"unitPrice\":3000}]}"));

        Assert.Single(reading.Document.Receipts!.Rows);
        Assert.Equal(2, reading.Rejected.Receipts);
    }

    /* ---------- values clipped ---------- */

    [Fact]
    public void An_over_long_name_is_cut_on_a_character_boundary()
    {
        var name = new string('a', Sanitise.Title - 1) + "\U0001F680\U0001F680";

        var reading = Read(Wrap("\"blueprints\"",
            "\"blueprints\":{\"caveats\":[],\"rows\":[{\"at\":\"2026-08-20T09:00:00+00:00\",\"name\":\""
            + name + "\"}]}"));

        var row = Assert.Single(reading.Document.Blueprints!.Rows);
        Assert.True(row.Name.Length <= Sanitise.Title);
        Assert.DoesNotContain(row.Name, char.IsSurrogate);
    }

    /// <summary>
    /// A title of a carriage return and two hundred spaces draws as a blank row
    /// nobody can click, which is a cheap way to make a page look broken with
    /// data somebody accepted from a friend.
    /// </summary>
    [Fact]
    public void Control_characters_do_not_survive_into_a_title()
    {
        var reading = Read(Wrap("\"blueprints\"",
            "\"blueprints\":{\"caveats\":[],\"rows\":[{\"at\":\"2026-08-20T09:00:00+00:00\","
            + "\"name\":\"Omni\\u0007sky\\u0000 IX\"}]}"));

        var row = Assert.Single(reading.Document.Blueprints!.Rows);
        Assert.Equal("Omnisky IX", row.Name);
    }

    [Fact]
    public void More_rows_than_the_cap_are_kept_to_the_cap_and_the_rest_reported()
    {
        var rows = string.Join(",", Enumerable.Range(0, ImportReader.MaxBlueprints + 25)
            .Select(i => $"{{\"at\":\"2026-08-20T09:00:00+00:00\",\"name\":\"Blueprint {i}\"}}"));

        var reading = Read(Wrap("\"blueprints\"", "\"blueprints\":{\"caveats\":[],\"rows\":[" + rows + "]}"));

        Assert.Equal(ImportReader.MaxBlueprints, reading.Counts.Blueprints);
        Assert.Equal(25, reading.Truncated.Blueprints);
    }

    /* ---------- what must never come through ---------- */

    /// <summary>
    /// The render guard refuses anything that is not http(s), but the boundary
    /// is where it should be decided: a scheme that never enters the store
    /// cannot be reached by a later view that forgets to ask.
    /// </summary>
    [Fact]
    public void An_attachment_pointing_somewhere_that_is_not_the_web_is_dropped()
    {
        var reading = Read(Wrap("\"authored\"",
            "\"authored\":{\"jobs\":[],\"trips\":[],\"checklists\":[{\"id\":\"c1\",\"title\":\"Departure\","
            + "\"createdAt\":\"2026-08-20T09:00:00+00:00\",\"items\":[{\"id\":\"i1\",\"text\":\"Refuel\","
            + "\"done\":false,\"attachments\":["
            + "{\"kind\":\"url\",\"label\":\"Free ships\",\"target\":\"javascript:alert(1)\"},"
            + "{\"kind\":\"url\",\"label\":\"Also this\",\"target\":\"file:///C:/Windows\"},"
            + "{\"kind\":\"url\",\"label\":\"A real one\",\"target\":\"https://uexcorp.space\"}]}]}]}"));

        var item = Assert.Single(reading.Document.Authored!.Checklists[0].Items);
        var attachment = Assert.Single(item.Attachments);

        Assert.Equal("https://uexcorp.space", attachment.Target);
    }

    [Fact]
    public void An_id_that_is_not_shaped_like_one_is_dropped_rather_than_carried()
    {
        var reading = Read(Wrap("\"authored\"",
            "\"authored\":{\"checklists\":[],\"trips\":[],\"jobs\":[{\"id\":\"../../etc/passwd\","
            + "\"title\":\"Craft it\",\"kind\":\"craft\",\"createdAt\":\"2026-08-20T09:00:00+00:00\","
            + "\"done\":false,\"items\":[]}]}"));

        var job = Assert.Single(reading.Document.Authored!.Jobs);
        Assert.Equal(string.Empty, job.Id);
    }

    /// <summary>This machine decides what is pinned, whatever a file claims.</summary>
    [Fact]
    public void A_file_cannot_pin_anything_on_the_machine_that_reads_it()
    {
        var reading = Read(Wrap("\"authored\"",
            "\"authored\":{\"checklists\":[],\"trips\":[],\"jobs\":[{\"id\":\"abc123\",\"title\":\"Mine now\","
            + "\"kind\":\"craft\",\"createdAt\":\"2026-08-20T09:00:00+00:00\",\"done\":false,"
            + "\"items\":[],\"pinned\":true}]}"));

        Assert.False(Assert.Single(reading.Document.Authored!.Jobs).Pinned);
    }

    [Fact]
    public void A_shared_run_sheet_keeps_safe_manual_actions()
    {
        var reading = Read(Wrap("\"authored\"",
            "\"authored\":{\"jobs\":[],\"checklists\":[],\"trips\":[{\"id\":\"t1\",\"title\":\"Cargo run\","
            + "\"createdAt\":\"2026-08-20T09:00:00+00:00\",\"stops\":[{\"id\":\"s1\",\"placeId\":\"RR_MIC_LEO\","
            + "\"place\":\"Port Tressler\",\"done\":true,\"actions\":[{\"id\":\"a1\",\"kind\":\"load\","
            + "\"text\":\"Agricium\",\"quantity\":96,\"unit\":\"SCU\",\"done\":false}]}]}]}"));

        var action = Assert.Single(Assert.Single(Assert.Single(reading.Document.Authored!.Trips).Stops).Actions!);
        Assert.Equal("load", action.Kind);
        Assert.Equal("Agricium", action.Text);
        Assert.Equal(96, action.Quantity);
        Assert.Equal("SCU", action.Unit);
    }

    /* ---------- the ordinary case ---------- */

    [Fact]
    public void A_document_this_app_wrote_reads_back_whole()
    {
        var written = new ExportFile(
            ExportDocument.Format, 1, 1, Now,
            new ExportProducer("Quantum Wake", "0.8.0"),
            [ExportDocument.Receipts],
            "nekron", "Tuesday hauling",
            new ExportReceipts(7, Sane, Sane, [ExportCaveats.PlaceInferred],
                [new ExportReceiptRow(Sane, true, "Port Tressler", "RR_MIC_LEO", 96, 288000m, 3000m,
                    "Cargo", "Agricium", "b999ef65-35be-45bf-908a-5eac6e06ba12")]));

        var reading = Read(JsonSerializer.Serialize(written, ExportDocument.Json));

        Assert.Equal("nekron", reading.Document.Handle);
        Assert.Equal(1, reading.Counts.Receipts);
        Assert.False(reading.Rejected.Any);
        Assert.False(reading.Truncated.Any);

        var row = Assert.Single(reading.Document.Receipts!.Rows);
        Assert.Equal("Agricium", row.Commodity);

        // The id survives even though a dash is not a letter or a digit.
        Assert.Equal("b999ef65-35be-45bf-908a-5eac6e06ba12", row.ResourceId);
        Assert.Equal(3000m, row.UnitPrice);
    }

    /// <summary>
    /// A document that lies about its own age is not one whose lie is worth
    /// keeping: the span is recomputed from the rows that survived.
    /// </summary>
    [Fact]
    public void The_observed_span_is_taken_from_the_rows_not_from_the_claim()
    {
        var reading = Read(Wrap("\"receipts\"",
            "\"receipts\":{\"windowDays\":7,\"observedFrom\":\"2020-01-01T00:00:00+00:00\","
            + "\"observedTo\":\"2074-01-01T00:00:00+00:00\",\"caveats\":[],\"rows\":["
            + "{\"at\":\"2026-08-20T09:00:00+00:00\",\"isSell\":true,\"place\":\"TDD\",\"scu\":1,\"amount\":1,\"unitPrice\":1}]}"));

        Assert.Equal(Sane, reading.Document.Receipts!.ObservedFrom);
        Assert.Equal(Sane, reading.Document.Receipts.ObservedTo);
    }

    /// <summary>
    /// Comparisons against local rows have to be like for like, and a file may
    /// legitimately have been written in any offset on earth.
    /// </summary>
    [Fact]
    public void Every_date_arrives_in_utc_whatever_offset_it_was_written_in()
    {
        var reading = Read(Wrap("\"blueprints\"",
            "\"blueprints\":{\"caveats\":[],\"rows\":[{\"at\":\"2026-08-20T23:00:00+14:00\",\"name\":\"Omnisky IX\"}]}"));

        var row = Assert.Single(reading.Document.Blueprints!.Rows);
        Assert.Equal(TimeSpan.Zero, row.At.Offset);
    }
}
