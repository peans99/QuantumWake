using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Joining UEX's counter names to the map's place names. Being wrong here puts
/// a stop on the wrong moon, so the rule is narrow on purpose and these pin
/// what it refuses as much as what it matches.
/// </summary>
public class TerminalPlacesTests
{
    private static PlaceTotal Place(string rawId, string name) =>
        new(rawId, name, "Stanton", null, "Station", 1);

    private static readonly TerminalPlaces Atlas = new([
        Place("Stanton4_NewBabbage", "New Babbage"),
        Place("Stanton3_Area18", "Area18"),
        Place("Stanton3b_ArcCorp_Area061", "Area 061"),
        Place("RR_MIC_L1", "microTech L1 Rest Stop"),
        Place("RR_HUR_L5", "HUR-L5 High Course Station"),
        Place("RR_CRU_L4", "CRU-L4 Shallow Fields Station"),
        Place("Port_Tressler", "Port Tressler"),
        Place("GrimHEX", "GrimHEX"),
        Place("Stanton1_Lorville", "Lorville")
    ]);

    /// <summary>
    /// The shop chains name a counter after the station code alone, which
    /// neither contains nor is contained by the atlas name - so every item
    /// shop on a rest stop used to resolve to nothing.
    /// </summary>
    [Theory]
    [InlineData("Platinum HUR-L5", "RR_HUR_L5")]
    [InlineData("Dumper's CRU-L4", "RR_CRU_L4")]
    [InlineData("Platinum CRU-L4", "RR_CRU_L4")]
    public void A_shop_named_for_a_station_code_finds_that_station(string terminal, string expected)
    {
        Assert.Equal(expected, Atlas.IdFor(terminal));
    }

    /// <summary>
    /// A code has to be a code: matching on a leading word would let "Port
    /// Tressler" claim every counter with "port" in its name.
    /// </summary>
    [Fact]
    public void A_leading_word_without_a_number_is_not_a_code()
    {
        Assert.Equal(string.Empty, Atlas.IdFor("Portside Deli"));
    }

    [Theory]
    [InlineData("Admin - Port Tressler", "Port_Tressler")]
    [InlineData("TDD, Area 18", "Stanton3_Area18")]
    [InlineData("TDD - New Babbage", "Stanton4_NewBabbage")]
    [InlineData("Platinum Bay - GrimHEX", "GrimHEX")]
    [InlineData("Admin - microTech L1 Rest Stop", "RR_MIC_L1")]
    public void A_counter_belongs_to_the_place_its_name_carries(string terminal, string expected)
    {
        Assert.Equal(expected, Atlas.IdFor(terminal));
    }

    /// <summary>"Area 061" contains "Area 06"; the longer name is the answer.</summary>
    [Fact]
    public void The_longest_name_in_the_terminal_wins()
    {
        Assert.Equal("Stanton3b_ArcCorp_Area061", Atlas.IdFor("Admin - Area 061"));
    }

    /// <summary>
    /// UEX drops the suffix as often as it adds a prefix: "Seraphim" is where
    /// "Seraphim Station" is, and refusing that loses half the join.
    /// </summary>
    [Fact]
    public void A_terminal_named_for_part_of_a_place_still_finds_it()
    {
        var atlas = new TerminalPlaces([
            Place("RR_CRU_LEO", "Seraphim Station"),
            Place("GrimHEX", "GrimHEX")
        ]);

        Assert.Equal("RR_CRU_LEO", atlas.IdFor("Seraphim"));
    }

    [Fact]
    public void A_part_name_that_fits_two_places_finds_neither()
    {
        var atlas = new TerminalPlaces([
            Place("first", "Shubin SM0-13"),
            Place("second", "Shubin SM0-18")
        ]);

        Assert.Equal(string.Empty, atlas.IdFor("Shubin"));
    }

    [Theory]
    [InlineData("Ashland")]
    [InlineData("Endgame")]
    [InlineData("Rat's Nest")]
    [InlineData("")]
    [InlineData(null)]
    public void A_terminal_naming_no_place_we_know_matches_nothing(string? terminal)
    {
        Assert.Equal(string.Empty, Atlas.IdFor(terminal));
    }

    /// <summary>
    /// Two places of the same name length inside one terminal name identify
    /// nothing, and a guess would send someone to the wrong one.
    /// </summary>
    [Fact]
    public void An_ambiguous_terminal_matches_nothing()
    {
        var ambiguous = new TerminalPlaces([
            Place("first", "Shubin SM0-13"),
            Place("second", "Shubin SM0-18")
        ]);

        Assert.Equal(string.Empty, ambiguous.IdFor("Shubin SM0-13 and Shubin SM0-18 depot"));
    }

    /// <summary>A three-letter place name would match half the system.</summary>
    [Fact]
    public void Very_short_place_names_are_never_matched()
    {
        var tiny = new TerminalPlaces([Place("hex", "HEX")]);

        Assert.Equal(string.Empty, tiny.IdFor("Platinum Bay - GrimHEX"));
    }

    [Fact]
    public void Punctuation_and_case_do_not_matter()
    {
        Assert.Equal("Stanton1_Lorville", Atlas.IdFor("admin — LORVILLE (L19)"));
    }

    [Fact]
    public void The_matched_place_comes_back_whole()
    {
        var match = Atlas.Resolve("TDD, Area 18");

        Assert.NotNull(match);
        Assert.Equal("Area18", match.Name);
        Assert.Equal("Stanton", match.System);
    }

    /// <summary>
    /// Nothing in the logs or the price feed describes danger; which system a
    /// place is in is what can be known, and it decides whether UEE law reaches
    /// it at all.
    /// </summary>
    [Theory]
    [InlineData("Stanton", "monitored")]
    [InlineData("Pyro", "lawless")]
    [InlineData("Nyx", "lawless")]
    public void Security_is_read_from_the_system(string system, string expected)
    {
        Assert.Equal(expected, TerminalPlaces.SecurityOfSystem(system));
    }

    /// <summary>
    /// Being told somewhere is safe when nobody checked is the one answer worth
    /// refusing, so an unplaced terminal says it does not know.
    /// </summary>
    [Fact]
    public void A_place_we_cannot_name_is_not_called_safe()
    {
        Assert.Equal("unknown", TerminalPlaces.SecurityOfSystem(null));
        Assert.Equal("unknown", Atlas.SecurityOf("Ashland"));
    }
}
