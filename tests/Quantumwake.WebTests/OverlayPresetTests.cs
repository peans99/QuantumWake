namespace Quantumwake.WebTests;

/// <summary>The named layouts must work from the overlay shell as well as Settings.</summary>
public class OverlayPresetTests
{
    private const string Layout = """
        {"current":{"tabs":["now"],"cards":["location"],"density":"normal"},
         "tabs":["now","jobs","map","commodities","market","loadout","stash","logbook","fleet","places"],
         "cards":["location","briefing","ship","session","handle","feed","stats","party","respawn","job","checklist","trip","trade"],
         "reloadToken":0}
        """;

    [Fact]
    public void Flight_preset_keeps_navigation_and_the_flight_log_ready()
    {
        var page = new Page();
        page.Serve("/api/overlay/layout", Layout);

        page.Do("await window.scOverlayPreset('flight');");

        var saved = page.BodyOf("/api/overlay/layout");
        Assert.Contains("\"map\"", saved);
        Assert.Contains("\"logbook\"", saved);
        Assert.Contains("\"feed\"", saved);
    }

    [Fact]
    public void Mining_preset_keeps_ore_finding_and_selling_close()
    {
        var page = new Page();
        page.Serve("/api/overlay/layout", Layout);

        page.Do("await window.scOverlayPreset('mining');");

        var saved = page.BodyOf("/api/overlay/layout");
        Assert.Contains("\"tabs\":[\"now\",\"map\",\"commodities\",\"market\"]", saved);
        Assert.Contains("\"trade\"", saved);
        Assert.Contains("\"density\":\"compact\"", saved);
    }

    [Fact]
    public void Combat_preset_keeps_loadout_and_survival_readings_ready()
    {
        var page = new Page();
        page.Serve("/api/overlay/layout", Layout);

        page.Do("await window.scOverlayPreset('combat');");

        var saved = page.BodyOf("/api/overlay/layout");
        Assert.Contains("\"loadout\"", saved);
        Assert.Contains("\"stash\"", saved);
        Assert.Contains("\"respawn\"", saved);
    }

    [Fact]
    public void Full_preset_uses_every_option_the_server_currently_offers()
    {
        var page = new Page();
        page.Serve("/api/overlay/layout", Layout);

        page.Do("await window.scOverlayPreset('full');");

        var saved = page.BodyOf("/api/overlay/layout");
        Assert.Contains("\"fleet\"", saved);
        Assert.Contains("\"checklist\"", saved);
        Assert.Contains("\"density\":\"normal\"", saved);
    }
}
