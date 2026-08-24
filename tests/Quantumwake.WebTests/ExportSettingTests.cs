namespace Quantumwake.WebTests;

/// <summary>
/// The Settings block that hands a pilot a file of their own.
/// </summary>
/// <remarks>
/// The rule this pins is that nothing leaves without a click, and that the click
/// follows seeing what would go. The preview is counts only; the save is a POST,
/// never a link, because the LAN rule lets reads through and this is the one
/// response that carries a whole history.
/// </remarks>
public class ExportSettingTests
{
    private const string Preview = """
        {"receipts":41,"blueprints":7,"jobs":3,"checklists":2,"trips":1,
         "days":7,"defaultDays":7}
        """;

    private static Page Ready()
    {
        var page = new Page();
        page.Serve("/api/export/preview?receipts=true&blueprints=true&authored=true&days=7", Preview);
        return page;
    }

    [Fact]
    public void The_preview_counts_what_would_go_before_anything_goes()
    {
        var page = Ready();
        page.Do("await renderExportPreview();");

        var line = page.NodeText("#export-preview");
        Assert.Contains("41 trades", line);
        Assert.Contains("last 7 days", line);
        Assert.Contains("7 blueprints", line);
        Assert.Contains("3 jobs", line);

        // Counts only: the preview endpoint must never hand back rows.
        Assert.Contains(page.Fetched(), url => url.StartsWith("GET /api/export/preview"));
    }

    [Fact]
    public void Ticking_nothing_says_so_and_asks_for_nothing()
    {
        var page = Ready();
        page.Do("""
            __dom.node('#export-receipts').checked = false;
            __dom.node('#export-blueprints').checked = false;
            __dom.node('#export-authored').checked = false;
            await renderExportPreview();
            """);

        Assert.Contains("nothing to save", page.NodeText("#export-preview"));
    }

    [Fact]
    public void Saving_posts_the_choice_and_writes_the_file_the_server_named()
    {
        var page = Ready();
        page.Serve("/api/export", "{\"format\":\"quantumwake.export\"}");
        page.Do("""
            __fetch.headers['/api/export'] =
              { 'content-disposition': 'attachment; filename="quantumwake-nekron-20260824-1200.json"' };
            __dom.node('#export-days').value = '30';
            await saveExport();
            """);

        // A POST, so the LAN guard refuses it off-machine without a deny-list.
        Assert.Contains("POST /api/export", page.Fetched());

        var sent = page.BodyOf("/api/export");
        Assert.Contains("\"receipts\":true", sent);
        Assert.Contains("\"days\":30", sent);
        Assert.Contains("\"handle\":true", sent);

        Assert.Equal("quantumwake-nekron-20260824-1200.json",
            page.Text("__downloads[0].name"));
        Assert.Contains("Saved quantumwake-nekron", page.NodeText("#export-status"));
    }

    /// <summary>
    /// The failure has to be visible and the button has to come back: this form
    /// stays on screen when the request fails, unlike most of the app's.
    /// </summary>
    [Fact]
    public void A_refused_export_says_why_and_saves_nothing()
    {
        var page = Ready();
        page.Do("""
            __fetch.unreachable.push('/api/export');
            await saveExport();
            """);

        Assert.Equal(0, page.Count("__downloads.length"));
        Assert.Contains("could not be built", page.NodeText("#export-status"));
        Assert.False(page.Truth("__dom.node('#export-save').disabled"));
    }

    /// <summary>
    /// Zero is a real answer here - it means all time - so the absent case must
    /// not arrive at it by accident and send a hundred times what was asked for.
    /// </summary>
    [Fact]
    public void An_empty_window_falls_back_to_the_week_rather_than_to_all_time()
    {
        var page = Ready();
        page.Serve("/api/export", "{}");
        page.Do("""
            __dom.node('#export-days').value = '';
            await saveExport();
            """);

        Assert.Contains("\"days\":7", page.BodyOf("/api/export"));
    }

    [Fact]
    public void Saving_with_nothing_ticked_never_reaches_the_server()
    {
        var page = Ready();
        page.Do("""
            __dom.node('#export-receipts').checked = false;
            __dom.node('#export-blueprints').checked = false;
            __dom.node('#export-authored').checked = false;
            await saveExport();
            """);

        Assert.DoesNotContain("POST /api/export", page.Fetched());
        Assert.Contains("Tick at least one", page.NodeText("#export-status"));
    }
}
