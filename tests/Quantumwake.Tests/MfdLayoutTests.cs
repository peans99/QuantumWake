using Quantumwake.Overlay;

namespace Quantumwake.Tests;

public class MfdLayoutTests
{
    private static readonly MfdMonitor[] Monitors = [new("main", 0, 0, 1920, 1080, true), new("cockpit", -1280, -200, 1280, 720, false)];

    [Fact]
    public void DefaultIsOffAndUsesSecondaryMonitorWithSeparateCougars()
    {
        var layout = MfdLayout.Default(Monitors).Validate(Monitors);
        Assert.False(layout.Enabled);
        Assert.All(layout.Panels, p => Assert.Equal("cockpit", p.Monitor));
        Assert.Equal(new[] { 1, 2 }, layout.Panels.Select(p => p.Cougar));
        Assert.True(layout.Panels[0].X + layout.Panels[0].Width <= layout.Panels[1].X);
    }

    [Fact]
    public void SharedAndSeparateMonitorsKeepIndependentGeometry()
    {
        var layout = MfdLayout.Default(Monitors);
        layout.Panels[1] = layout.Panels[1] with { Monitor = "main", X = 1200, Y = 350 };
        var validated = layout.Validate(Monitors);
        Assert.Equal(-1280, Monitors[1].X + validated.Panels[0].X);
        Assert.Equal(1200, validated.Panels[1].X);
        Assert.Equal(350, validated.Panels[1].Y);
    }

    [Fact]
    public void ResolutionChangeClampsSizeAndPositionWithoutOverflow()
    {
        var layout = MfdLayout.Default(Monitors);
        layout.Panels[0] = layout.Panels[0] with { X = int.MaxValue, Y = -200, Width = int.MaxValue, Height = 1 };
        var panel = layout.Validate(Monitors).Panels[0];
        Assert.Equal(1280, panel.Width);
        Assert.Equal(220, panel.Height);
        Assert.Equal(0, panel.X);
        Assert.Equal(0, panel.Y);
    }

    [Fact]
    public void MissingMonitorRetainsItsPlacementInsteadOfMovingOntoGame()
    {
        var layout = MfdLayout.Default(Monitors);
        Assert.Equal(layout.Panels[1], layout.Validate([Monitors[0]]).Panels[1]);
    }

    [Fact]
    public void RejectsDuplicateAssignmentsAndMalformedPanels()
    {
        var layout = MfdLayout.Default(Monitors);
        layout.Panels[1] = layout.Panels[1] with { Cougar = 1 };
        Assert.Throws<ArgumentException>(() => layout.Validate(Monitors));
        Assert.Throws<ArgumentException>(() => new MfdLayout { Panels = null! }.Validate(Monitors));
        Assert.Throws<ArgumentException>(() => new MfdLayout { Panels = [null!, null!] }.Validate(Monitors));
    }

    /// <summary>
    /// Absent means "use the shipped profile"; present means the pilot's map is
    /// the whole answer, empty included. Handing the defaults back to someone
    /// who cleared every button would be the app overruling them silently.
    /// </summary>
    [Fact]
    public void OnlyAnAbsentButtonMapMeansTheShippedProfile()
    {
        var layout = MfdLayout.Default(Monitors);
        Assert.Null(layout.Validate(Monitors).Buttons);
        Assert.Empty((layout with { Buttons = [] }).Validate(Monitors).Buttons!);
    }

    /// <summary>
    /// Shape only: what a command name means is the display's business, so an
    /// unknown one survives the file and is dropped where it is read.
    /// </summary>
    [Fact]
    public void ButtonMapKeepsUnknownCommandsAndDropsImpossibleButtons()
    {
        var stored = new Dictionary<int, string>
        {
            [0] = "nav", [1] = "nav", [28] = "confirm", [29] = "nav",
            [7] = "  ", [8] = new string('x', 33), [9] = "a-command-added-later"
        };
        var kept = (MfdLayout.Default(Monitors) with { Buttons = stored }).Validate(Monitors).Buttons!;
        Assert.Equal([1, 9, 28], kept.Keys.Order());
        Assert.Equal("a-command-added-later", kept[9]);
    }

    /// <summary>
    /// The map is written by a web page and read back into a dictionary keyed
    /// by number, so the file's string keys have to survive the trip both ways.
    /// </summary>
    [Fact]
    public void ButtonMapSurvivesTheJsonRoundTripTheSetupPageUses()
    {
        var saved = (MfdLayout.Default(Monitors) with
        {
            Buttons = new() { [1] = "nav", [8] = "confirm", [21] = "bright-up" }
        }).Validate(Monitors);

        var json = System.Text.Json.JsonSerializer.Serialize(saved, MfdLayout.JsonOptions);
        Assert.Contains("\"21\": \"bright-up\"", json);

        var read = System.Text.Json.JsonSerializer
            .Deserialize<MfdLayout>(json, MfdLayout.JsonOptions)!.Validate(Monitors);
        Assert.Equal(saved.Buttons, read.Buttons);
        Assert.Equal("confirm", read.Buttons![8]);
    }

    [Fact]
    public void ButtonEdgesIgnoreInitialHeldButtonAndRepeatThenAcceptRepress()
    {
        var edges = new CougarEdges();
        Assert.Empty(edges.Read(1));
        Assert.Empty(edges.Read(1));
        Assert.Empty(edges.Read(0));
        Assert.Equal(new[] { 1, 20, 28 }, edges.Read(1u | (1u << 19) | (1u << 27)));
        Assert.Empty(edges.Read(1u | (1u << 19) | (1u << 27)));
        edges.Disconnect();
        Assert.Empty(edges.Read(1));
        Assert.Empty(edges.Read(0));
        Assert.Equal(new[] { 1 }, edges.Read(1));
    }

    [Fact]
    public void EachDeviceHasIndependentPressHistory()
    {
        var left = new CougarEdges(); var right = new CougarEdges();
        left.Read(0); right.Read(0);
        Assert.Equal(new[] { 3 }, left.Read(4));
        Assert.Equal(new[] { 3 }, right.Read(4));
        Assert.Empty(left.Read(4));
    }
}
