namespace Quantumwake.WebTests;

/// <summary>
/// The parts reference can come from the install or from the download.
/// </summary>
/// <remarks>
/// Unlike the Market page these two agree item for item — all 10,843 of the
/// download's items match the install on type, sub-type, size and grade — so
/// the caption is not a warning. What differs is how much is listed: the
/// install describes 26,028, and a reader comparing counts should be able to
/// see which source produced the one in front of them.
/// </remarks>
public class PartsSourceTests
{
    private static string Catalogue(string source) => $$"""
        [{"className":"maxlift_01","name":"MaxLift Tractor Beam","type":"WeaponPersonal",
          "subType":"Utility","size":1,"grade":1,"manufacturer":"Greycat Industrial",
          "source":"{{source}}","price":19175,"stockedAt":2,"cheapestAt":"Area18","terminals":null}]
        """;

    private static Page Loaded(string body)
    {
        var page = new Page();
        page.Serve("/api/reference/items", body);
        page.Do("await loadPartsRef();");
        return page;
    }

    [Fact]
    public void The_install_caption_says_where_it_read_them()
    {
        Assert.Contains(
            "read from your game install", Loaded(Catalogue("install")).NodeText("#parts-caption"));
    }

    [Fact]
    public void The_download_caption_still_names_the_digest()
    {
        var caption = Loaded(Catalogue("dataset")).NodeText("#parts-caption");

        Assert.Contains("community digest", caption);
        Assert.DoesNotContain("game install", caption);
    }

    /// <summary>
    /// The maker comes through resolved rather than as the four-letter code the
    /// install stores, because GRIN is not a name anybody would recognise.
    /// </summary>
    [Fact]
    public void The_row_shows_the_makers_full_name()
    {
        Assert.Contains(
            "Greycat Industrial", Loaded(Catalogue("install")).NodeText("#parts-table tbody"));
    }

    /// <summary>
    /// The game says "no sub-type" as the literal UNDEFINED, which the reader
    /// drops. What arrives here is an empty string, and an empty string must
    /// still reach the table as the dash every other blank cell uses — a
    /// shouted word in that column reads as a lookup that failed.
    /// </summary>
    [Fact]
    public void An_item_with_no_subtype_shows_a_dash()
    {
        var body = Loaded("""
            [{"className":"jacket","name":"Legion Jacket","type":"Char_Clothing_Torso_1",
              "subType":"","size":1,"grade":1,"manufacturer":"987","source":"install",
              "price":1320,"stockedAt":2,"cheapestAt":"KC Trending","terminals":null}]
            """).NodeText("#parts-table tbody");

        Assert.Contains("—", body);
        Assert.DoesNotContain("UNDEFINED", body);
    }
}
