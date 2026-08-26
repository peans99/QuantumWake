namespace Quantumwake.WebTests;

/// <summary>
/// The Loot page's two filters: what kind of thing, and where you were.
/// </summary>
/// <remarks>
/// Both are built from the rows actually loaded rather than from every category
/// the classifier knows, because a dropdown offering something this install has
/// never seen is a filter that can only disappoint. They are also built from
/// everything in the window rather than from what survives the other filter, or
/// choosing a kind would empty the place list and strand the reader with no way
/// back.
/// </remarks>
public class LootFilterTests
{
    private const string Pickups = """
        [{"at":"2026-08-20T09:00:00+00:00","item":"P4-AR","itemClass":"behr_rifle_ballistic_01",
          "place":"Port Tressler","category":"Weapons"},
         {"at":"2026-08-19T09:00:00+00:00","item":"MedPen","itemClass":"crlf_consumable_healing_01",
          "place":"Lorville","category":"Medical"},
         {"at":"2026-08-18T09:00:00+00:00","item":"Rifle magazine","itemClass":"behr_rifle_mag_01",
          "place":"Port Tressler","category":"Ammo"}]
        """;

    private static Page Loaded(string rows = Pickups)
    {
        var page = new Page();
        page.Serve("/api/loot?days=0", rows);
        page.Do("__dom.node('#loot-period').value = '0'; await loadLoot();");
        return page;
    }

    private static string Options(Page page, string id) =>
        page.Text($"__dom.node('{id}').options.map(o => o.value).join('|')");

    [Fact]
    public void Both_filters_offer_only_what_the_rows_actually_contain()
    {
        var page = Loaded();

        Assert.Equal("|Ammo|Medical|Weapons", Options(page, "#loot-kind"));
        Assert.Equal("|Lorville|Port Tressler", Options(page, "#loot-place"));
    }

    [Fact]
    public void Choosing_a_kind_shows_only_that_kind()
    {
        var page = Loaded();
        page.Do("__dom.node('#loot-kind').value = 'Weapons'; renderLoot(lastLootRows);");

        var rows = page.NodeText("#loot-table tbody");
        Assert.Contains("P4-AR", rows);
        Assert.DoesNotContain("MedPen", rows);
        Assert.DoesNotContain("Rifle magazine", rows);
    }

    [Fact]
    public void Choosing_a_place_shows_only_what_was_found_there()
    {
        var page = Loaded();
        page.Do("__dom.node('#loot-place').value = 'Lorville'; renderLoot(lastLootRows);");

        var rows = page.NodeText("#loot-table tbody");
        Assert.Contains("MedPen", rows);
        Assert.DoesNotContain("P4-AR", rows);
    }

    [Fact]
    public void The_two_filters_narrow_together()
    {
        var page = Loaded();
        page.Do("""
            __dom.node('#loot-kind').value = 'Weapons';
            __dom.node('#loot-place').value = 'Lorville';
            renderLoot(lastLootRows);
            """);

        // A rifle, but not at Lorville.
        Assert.DoesNotContain("P4-AR", page.NodeText("#loot-table tbody"));
    }

    /// <summary>
    /// Narrowing by one must not shorten the other, or the reader picks a kind
    /// and finds the place they wanted has vanished from the list.
    /// </summary>
    [Fact]
    public void Choosing_one_filter_does_not_shorten_the_other()
    {
        var page = Loaded();
        page.Do("__dom.node('#loot-kind').value = 'Medical'; renderLoot(lastLootRows);");

        Assert.Equal("|Lorville|Port Tressler", Options(page, "#loot-place"));
        Assert.Equal("Medical", page.Text("__dom.node('#loot-kind').value"));
    }

    /// <summary>
    /// "Nothing in that range" is the wrong explanation for a table a dropdown
    /// emptied, and the same mistake the routes table used to make.
    /// </summary>
    [Fact]
    public void An_empty_table_names_the_filter_that_emptied_it()
    {
        var page = Loaded();
        page.Do("""
            __dom.node('#loot-kind').value = 'Weapons';
            __dom.node('#loot-place').value = 'Lorville';
            renderLoot(lastLootRows);
            """);

        var text = page.NodeText("#loot-table tbody");
        Assert.Contains("Weapons", text);
        Assert.Contains("Lorville", text);
        Assert.DoesNotContain("Nothing in that range.", text);
    }

    /// <summary>
    /// A window that moves can take the last of something with it. The filter
    /// falls back to everything rather than silently showing nothing for ever.
    /// </summary>
    [Fact]
    public void A_chosen_value_that_no_longer_exists_falls_back_to_everything()
    {
        var page = Loaded();
        page.Do("__dom.node('#loot-kind').value = 'Weapons'; renderLoot(lastLootRows);");

        page.Do("""
            renderLoot([{ at: '2026-08-19T09:00:00+00:00', item: 'MedPen',
                          itemClass: 'crlf_consumable_healing_01', place: 'Lorville',
                          category: 'Medical' }]);
            """);

        Assert.Equal("", page.Text("__dom.node('#loot-kind').value"));
        Assert.Contains("MedPen", page.NodeText("#loot-table tbody"));
    }

    [Fact]
    public void The_kind_is_shown_on_the_row_as_well_as_filtered_by()
    {
        Assert.Contains("Weapons", Loaded().NodeText("#loot-table tbody"));
    }
}
