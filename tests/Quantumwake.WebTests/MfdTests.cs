using Jint;

namespace Quantumwake.WebTests;

public class MfdTests
{
    private static Engine Engine()
    {
        var engine = new Engine();
        engine.Execute("var window = globalThis;");
        engine.Execute(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd-core.js")));
        engine.Execute("var monitors = [{id:'left',x:-1280,y:-200,width:1280,height:720}, {id:'main',x:0,y:0,width:1920,height:1080}];");
        return engine;
    }

    [Fact]
    public void DragBetweenMonitorsUsesDesktopCoordinatesAndStoresLocalPixels()
    {
        var e = Engine();
        e.Execute("var p = QwMfd.move({id:'left',monitor:'main',x:0,y:0,width:400,height:400}, -1100,-100,monitors);");
        Assert.Equal("left", e.Evaluate("p.monitor").AsString());
        Assert.Equal(180, e.Evaluate("p.x").AsNumber());
        Assert.Equal(100, e.Evaluate("p.y").AsNumber());
        Assert.Equal(3200, e.Evaluate("QwMfd.extent(monitors).width").AsNumber());
        Assert.Equal(1280, e.Evaluate("QwMfd.extent(monitors).height").AsNumber());
    }

    [Fact]
    public void DropInDesktopGapKeepsLastValidPlacement()
    {
        var e = Engine();
        Assert.True(e.Evaluate("var p = {monitor:'main',x:10,y:10,width:400,height:400}; QwMfd.move(p,5000,5000,monitors) === p").AsBoolean());
    }

    [Fact]
    public void ResizingAndMalformedValuesStayInsideMonitor()
    {
        var e = Engine();
        e.Execute("var p = QwMfd.fit({x:99999,y:-20,width:99999,height:NaN},monitors[0]);");
        Assert.Equal(0, e.Evaluate("p.x").AsNumber());
        Assert.Equal(0, e.Evaluate("p.y").AsNumber());
        Assert.Equal(1280, e.Evaluate("p.width").AsNumber());
        Assert.Equal(480, e.Evaluate("p.height").AsNumber());
    }

    [Theory]
    [InlineData(1, "page", 0)] [InlineData(3, "page", 2)]
    [InlineData(11, "cycle", 1)] [InlineData(15, "cycle", -1)]
    [InlineData(12, "scroll", 1)] [InlineData(14, "scroll", -1)]
    public void DefaultButtonsFollowCougarClockwiseNumbering(int button, string action, int value)
    {
        Assert.Equal(value, Engine().Evaluate($"QwMfd.action({button}).{action}").AsNumber());
    }

    [Fact]
    public void ReservedButtonsAndInvalidInputHaveNoAction()
    {
        var e = Engine();
        Assert.True(e.Evaluate("[0,9,10,17,18,29,1.5,'1'].every(n => QwMfd.action(n) === null)").AsBoolean());
    }

    /// <summary>
    /// The rockers are the one part of the frame a datasheet cannot number, so
    /// the shipped profile leaves them alone and a saved one can claim them.
    /// </summary>
    [Fact]
    public void RockersShipUnassignedAndAreBindableOnceTheTesterNamesThem()
    {
        var e = Engine();
        Assert.True(e.Evaluate("[21,22,27,28].every(n => QwMfd.action(n) === null)").AsBoolean());
        Assert.Equal(1, e.Evaluate("QwMfd.action(21,{21:'bright-up'}).brightness").AsNumber());
        Assert.True(e.Evaluate("QwMfd.action(29,{29:'nav'}) === null").AsBoolean());
    }

    /// <summary>
    /// A saved profile is the whole answer. Falling back per button would mean
    /// a button the pilot cleared quietly coming back on the next reload.
    /// </summary>
    [Fact]
    public void ASavedProfileReplacesTheDefaultsRatherThanPatchingThem()
    {
        var e = Engine();
        Assert.True(e.Evaluate("QwMfd.action(1,{2:'nav'}) === null").AsBoolean());
        Assert.Equal(0, e.Evaluate("QwMfd.action(2,{2:'nav'}).page").AsNumber());
        Assert.True(e.Evaluate("QwMfd.action(3,{3:'invented-command'}) === null").AsBoolean());
        Assert.Equal("nav", e.Evaluate("QwMfd.buttons(null)[1]").AsString());
        Assert.True(e.Evaluate("Object.keys(QwMfd.buttons({})).length === 0").AsBoolean());
    }

    [Fact]
    public void AbsentDataExplainsMissingSignalsInsteadOfDisplayingZero()
    {
        var e = Engine();
        e.Execute("var status = QwMfd.pageIds.indexOf('status');");
        Assert.Contains("not yet identified", e.Evaluate("JSON.stringify(QwMfd.rows(0,{}))").AsString());
        Assert.Contains("not supplied", e.Evaluate("JSON.stringify(QwMfd.rows(status,{}))").AsString());
        Assert.Contains("NO SCREENSHOT", e.Evaluate("JSON.stringify(QwMfd.rows(status,{}))").AsString());
        Assert.Contains("Loading", e.Evaluate("JSON.stringify(QwMfd.rows(1,{}))").AsString());
        Assert.Contains("NO OUTSTANDING", e.Evaluate("JSON.stringify(QwMfd.rows(1,{},{stops:[]}))").AsString());
    }

    [Fact]
    public void ScreenshotKeepsCaptureTimeAndDoesNotClaimLiveTelemetry()
    {
        var e = Engine();
        var json = e.Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('status'),{screen:{summary:'At Lorville',shotAt:'2026-09-09T12:00:00Z'}}))").AsString();
        Assert.Contains("2026-09-09T12:00:00Z", json);
        Assert.Contains("At Lorville", json);
        Assert.Contains("saved observations", json);
    }

    [Fact]
    public void TaskHudShowsOnlyNextUnfinishedActionAndPreservesPlannedUnits()
    {
        var json = Engine().Evaluate("JSON.stringify(QwMfd.rows(1,{},{tripTitle:'Medical run',stops:[{place:'Baijini Point',actions:[{done:true,text:'Old instruction'},{kind:'load',quantity:32,unit:'SCU',text:'Medical supplies',done:false},{kind:'sell',text:'Later instruction',done:false}]}]}))").AsString();
        Assert.Contains("load · 32 SCU · Medical supplies", json);
        Assert.Contains("Not a detected cargo manifest", json);
        Assert.DoesNotContain("Old instruction", json);
        Assert.DoesNotContain("Later instruction", json);
    }

    [Fact]
    public void QuantumDestinationWinsOverPlanAndFailedPlanIsExplicit()
    {
        var e = Engine();
        var json = e.Evaluate("JSON.stringify(QwMfd.rows(0,{travelling:true,travellingTo:'Everus Harbor'},{stops:[{place:'Baijini Point'}]}))").AsString();
        Assert.Contains("Everus Harbor", json);
        Assert.DoesNotContain("Baijini Point", json);
        Assert.Contains("PLAN UNAVAILABLE", e.Evaluate("JSON.stringify(QwMfd.rows(1,{},null,true))").AsString());
    }

    private const string Plan =
        "{tripId:'t1',tripTitle:'Ore run',stops:[{id:'s1',place:'Baijini Point',actions:["
        + "{id:'a0',kind:'load',text:'Already aboard',done:true},"
        + "{id:'a1',kind:'load',quantity:32,unit:'SCU',text:'Titanium',done:false},"
        + "{id:'a2',kind:'refuel',text:'Top up quantum',done:false}]},"
        + "{id:'s2',place:'Area18',actions:[{id:'b1',kind:'buy',quantity:64,unit:'SCU',text:'Agricium',done:false}]}]}";

    private static string Act(Engine e, string view = "{}") =>
        e.Evaluate($"JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('act'),{{}},{Plan},false,{view}))").AsString();

    [Fact]
    public void ActListsOnlyTheOutstandingWorkAtTheNextStopAndMarksTheCursor()
    {
        var e = Engine();
        var json = Act(e, "{selected:1}");
        Assert.Contains("TASK 1 OF 2", json);
        Assert.Contains("load · 32 SCU · Titanium", json);
        Assert.DoesNotContain("Already aboard", json);

        // Only the next stop is actionable; the one after it is not on this page.
        Assert.DoesNotContain("Agricium", json);

        // The cursor is on the second line, and nothing else carries a mark.
        Assert.Contains("\"cursor\"", json);
        Assert.Equal(1, e.Evaluate($"QwMfd.rows(QwMfd.pageIds.indexOf('act'),{{}},{Plan},false,{{selected:1}}).filter(r => r[3]).length").AsNumber());
        Assert.Equal("a2", e.Evaluate($"QwMfd.tasks({Plan})[1].actionId").AsString());
        Assert.Equal("t1", e.Evaluate($"QwMfd.tasks({Plan})[1].tripId").AsString());
    }

    /// <summary>
    /// A cursor past the end of a shrinking list must land on a real line: the
    /// list is re-read every time the plan does, and the pilot's next press
    /// confirms whatever it is pointing at.
    /// </summary>
    [Fact]
    public void CursorBeyondTheListFallsBackOntoTheLastLine()
    {
        Assert.Contains("\"cursor\"", Act(Engine(), "{selected:9}").Split("TASK 2")[1]);
        Assert.DoesNotContain("\"cursor\"", Act(Engine(), "{selected:0}").Split("TASK 2")[1]);
    }

    [Fact]
    public void AStopWithNothingOutstandingOffersItselfRatherThanAnEmptyPage()
    {
        var e = Engine();
        const string done = "{tripId:'t1',stops:[{id:'s1',place:'Everus Harbor',note:'Collect the armour',"
            + "actions:[{id:'a1',text:'Sold',done:true}]}]}";
        var json = e.Evaluate($"JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('act'),{{}},{done}))").AsString();
        Assert.Contains("CROSS OFF THE STOP", json);
        Assert.Contains("Collect the armour", json);
        Assert.Equal("stop", e.Evaluate($"QwMfd.tasks({done})[0].kind").AsString());
        Assert.True(e.Evaluate($"QwMfd.tasks({done})[0].actionId === undefined").AsBoolean());
        Assert.Contains("NOTHING TO CONFIRM",
            e.Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('act'),{},{tripId:'t1',stops:[]}))").AsString());
    }

    /// <summary>
    /// Confirming writes to the pilot's own plan, so the page has to say both
    /// that a press is armed and that the game is not being told anything.
    /// </summary>
    [Fact]
    public void ConfirmIsArmedBeforeItCommitsAndSaysWhatItWrites()
    {
        Assert.Contains("changes nothing in the game", Act(Engine()));
        var armed = Act(Engine(), "{selected:0,armed:true}");
        Assert.Contains("CONFIRM?", armed);
        Assert.Contains("Press DONE again", armed);
        Assert.Contains("\"armed\"", armed);
    }

    [Fact]
    public void CargoNeverPresentsAPlanOrAReceiptAsAManifest()
    {
        var e = Engine();
        const string bought = "{cargo:{boughtScu:96,soldScu:0,last:{sell:false,scu:96,"
            + "shop:'Area18 TDD',amount:182400,commodity:'Agricium',at:'2026-09-09T12:00:00Z'}}}";
        var json = e.Evaluate(
            "JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('cargo')," + bought + "," + Plan + "))").AsString();
        Assert.Contains("96 SCU across 2 stops · planned, not detected", json);
        Assert.Contains("Bought 96 SCU · Agricium · Area18 TDD", json);
        Assert.Contains("2026-09-09T12:00:00Z", json);
        Assert.Contains("96 SCU bought · 0 SCU sold", json);
        Assert.Contains("The game logs no cargo hold", json);
    }

    [Fact]
    public void CargoSaysNoCounterWasUsedRatherThanShowingZeroScu()
    {
        var json = Engine().Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('cargo'),{},{stops:[]}))").AsString();
        Assert.Contains("No commodity counter used in this session", json);
        Assert.Contains("Nothing bought or sold", json);
        Assert.Contains("No load or purchase planned", json);
        Assert.DoesNotContain("0 SCU bought", json);
    }

    /// <summary>
    /// Two units are two numbers. An instruction the pilot never put SCU on is
    /// counted beside the total rather than added into it.
    /// </summary>
    [Fact]
    public void PlannedLoadOnlyAddsUpWhatWasWrittenInScu()
    {
        var e = Engine();
        const string mixed = "{stops:[{actions:["
            + "{kind:'load',quantity:32,unit:'SCU',done:false},"
            + "{kind:'buy',quantity:5000,unit:'aUEC',done:false},"
            + "{kind:'load',text:'Whatever fits',done:false},"
            + "{kind:'sell',quantity:99,unit:'SCU',done:false},"
            + "{kind:'load',quantity:8,unit:'SCU',done:true}]}]}";
        Assert.Equal(32, e.Evaluate($"QwMfd.plannedLoad({mixed}).scu").AsNumber());
        Assert.Equal(2, e.Evaluate($"QwMfd.plannedLoad({mixed}).unmeasured").AsNumber());
        Assert.Contains("32 SCU + 2 with no SCU figure across 1 stop",
            e.Evaluate($"JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('cargo'),{{}},{mixed}))").AsString());
    }

    [Fact]
    public void ContractShowsTheOpenOneAndSaysWhenTheJournalWasQuiet()
    {
        var e = Engine();
        const string open = "{contracts:["
            + "{name:'Recover the cargo',issuer:'Hurston Dynamics',type:'Delivery',difficulty:'Medium',"
            + "steps:5,stepsDone:3,since:'2026-09-09T12:00:00Z'},"
            + "{name:'Second job',issuer:'Crusader',steps:0,stepsDone:0,since:'2026-09-09T11:00:00Z'}]}";
        var json = e.Evaluate($"JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('contract'),{open}))").AsString();
        Assert.Contains("Recover the cargo", json);
        Assert.Contains("3 of 5 done", json);
        Assert.Contains("Hurston Dynamics · Delivery · Medium", json);
        Assert.Contains("1 more still open", json);

        // The game's own title is already "issuer · type · difficulty" when no
        // text mod has replaced it, and printing that twice wastes the panel.
        var selfNaming = e.Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('contract'),"
            + "{contracts:[{name:'Red Wind · Recover Cargo · Easy',issuer:'Red Wind',"
            + "type:'Recover Cargo',difficulty:'Easy',steps:2,stepsDone:1}]}))").AsString();
        Assert.DoesNotContain("ISSUER", selfNaming);
        Assert.Equal(1, e.Evaluate("(JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('contract'),"
            + "{contracts:[{name:'Red Wind · Recover Cargo · Easy',issuer:'Red Wind',"
            + "type:'Recover Cargo',difficulty:'Easy'}]})).match(/Red Wind/g)||[]).length").AsNumber());

        var quiet = e.Evaluate($"JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('contract'),"
            + "{contracts:[{name:'Second job',issuer:'Crusader',steps:0,stepsDone:0}]}))").AsString();
        Assert.Contains("no objective steps", quiet);
        Assert.DoesNotContain("0 of 0", quiet);
    }

    private const string Atlas =
        "{positions:{stanton:{Hurston:{x:12850457093,y:0},Crusader:{x:0,y:19151568440},"
        + "microTech:{x:-43443771120,y:0},ArcCorp:{x:0,y:-28917482763}}},"
        + "nodes:[{rawId:'RR_MIC_LEO',name:'Port Tressler',body:'microTech'},"
        + "{rawId:'STAN_HUR_L1',name:'Everus Harbor',body:'Hurston'}]}";

    [Fact]
    public void MapPlotsRealBodyGeometryNormalisedToTheOutermostBody()
    {
        var e = Engine();
        e.Execute($"var v = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'Hurston'}});");
        Assert.Equal(4, e.Evaluate("v.bodies.length").AsNumber());

        // microTech is the far one, so it lands on the edge and everything else
        // falls inside it in the proportion the coordinates actually have.
        Assert.Equal(1, e.Evaluate("v.bodies.find(b => b.name === 'microTech').radius").AsNumber(), 6);
        Assert.Equal(-1, e.Evaluate("v.bodies.find(b => b.name === 'microTech').x").AsNumber(), 6);
        Assert.Equal(0.2958, e.Evaluate("v.bodies.find(b => b.name === 'Hurston').radius").AsNumber(), 3);
        Assert.True(e.Evaluate("v.bodies.every(b => Math.abs(b.x) <= 1 && Math.abs(b.y) <= 1)").AsBoolean());
        Assert.True(e.Evaluate("v.bodies.find(b => b.name === 'Hurston').here").AsBoolean());
    }

    /// <summary>
    /// The logs name the body, never a point on it. A quantum destination wins
    /// over the plan, exactly as the Nav rows have it.
    /// </summary>
    [Fact]
    public void MapMarksTheBodyAndPrefersAQuantumDestinationOverThePlan()
    {
        var e = Engine();
        const string plan = "{stops:[{placeId:'STAN_HUR_L1'}]}";
        e.Execute($"var v = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'ArcCorp',"
            + "travellingToId:'RR_MIC_LEO'}," + plan + ");");
        Assert.Equal("microTech", e.Evaluate("v.target").AsString());
        Assert.Contains("not a fix", e.Evaluate("v.note").AsString());

        e.Execute($"var p = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'ArcCorp'}},{plan});");
        Assert.Equal("Hurston", e.Evaluate("p.target").AsString());

        // Standing on the destination is not a leg to fly.
        e.Execute($"var s = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'Hurston'}},{plan});");
        Assert.True(e.Evaluate("s.bodies.every(b => !b.target)").AsBoolean());
    }

    [Fact]
    public void MapSaysWhatIsMissingRatherThanDrawingAnInventedSystem()
    {
        var e = Engine();
        Assert.Contains("Loading", e.Evaluate("QwMfd.mapView(null,{}).note").AsString());
        Assert.True(e.Evaluate($"QwMfd.mapView({Atlas},{{}}).bodies === undefined").AsBoolean());
        Assert.Contains("No system", e.Evaluate($"QwMfd.mapView({Atlas},{{}}).note").AsString());
        Assert.Contains("No body positions for Pyro",
            e.Evaluate($"QwMfd.mapView({Atlas},{{locationSystem:'Pyro'}}).note").AsString());
        Assert.Contains("not identified",
            e.Evaluate($"QwMfd.mapView({Atlas},{{locationSystem:'Stanton'}}).note").AsString());
    }

    /// <summary>
    /// Moons orbit within a rounding of their planet, and one ring each would
    /// draw the same circle four times over on a panel this size.
    /// </summary>
    [Fact]
    public void OrbitRingsCollapseBodiesSharingAnOrbit()
    {
        var e = Engine();
        const string moons = "{positions:{stanton:{Crusader:{x:0,y:19151568440},"
            + "Daymar:{x:0,y:19110000000},Cellin:{x:0,y:19180000000},"
            + "microTech:{x:-43443771120,y:0}}},nodes:[]}";
        e.Execute($"var v = QwMfd.mapView({moons},{{locationSystem:'Stanton',locationBody:'Daymar'}});");
        Assert.Equal(4, e.Evaluate("v.bodies.length").AsNumber());
        Assert.Equal(2, e.Evaluate("v.rings.length").AsNumber());
    }

    /// <summary>
    /// The panel keeps its own copy of the sixteen makers that have a logo,
    /// because it cannot load the dashboard to read them. This is what makes
    /// the copy safe: both files are loaded here, and a name changed in one
    /// and not the other fails rather than quietly dropping a badge.
    /// </summary>
    [Fact]
    public void TheMfdMakerTableMatchesTheDashboardsOwn()
    {
        var page = new Page();
        page.Do(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd-core.js")));
        Assert.Equal(
            page.Text("JSON.stringify(Object.entries(MANUFACTURERS).sort())"),
            page.Text("JSON.stringify(Object.entries(QwMfd.makers).sort())"));
        Assert.Equal(
            page.Text("JSON.stringify([...MANUFACTURER_LOGOS].sort())"),
            page.Text("JSON.stringify(Object.keys(QwMfd.makers).sort())"));
    }

    [Theory]
    [InlineData("DRAK Corsair", "DRAK")]              // the code, as the logs used to write it
    [InlineData("Drake Corsair", "DRAK")]             // the resolved name, as they do now
    [InlineData("Drake Interplanetary Cutter", "DRAK")]
    [InlineData("RSI Hermes", "RSI")]
    [InlineData("Consolidated Outland Nomad", "CNOU")] // must beat a bare "Consolidated"
    [InlineData("Origin 400i", "ORIG")]
    public void AShipFindsItsBadgeByCodeOrByName(string ship, string code)
    {
        Assert.Equal(code, Engine().Evaluate($"QwMfd.makerOf('{ship}').code").AsString());
    }

    [Fact]
    public void AnUnknownOrAbsentShipGetsNoBadgeRatherThanAWrongOne()
    {
        var e = Engine();
        Assert.True(e.Evaluate("QwMfd.makerOf('Greycat ROC') === null").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.makerOf(null) === null").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.makerOf('') === null").AsBoolean());

        // The community table may teach an alias, never a new logo.
        Assert.True(e.Evaluate("QwMfd.makerOf('Greycat ROC',{GRIN:'Greycat Industrial'}) === null").AsBoolean());
        Assert.Equal("MRAI", e.Evaluate("QwMfd.makerOf('Mirai Fury',{MRAI:'Mirai'}).code").AsString());
    }

    [Fact]
    public void EveryAssignedButtonHasAnIconAndAnUnassignedOneHasNothing()
    {
        var e = Engine();
        Assert.True(e.Evaluate("QwMfd.commands.every(c => typeof c.icon === 'string' && c.icon.length > 4)").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.icon('nav') !== null && QwMfd.caption('nav') !== null").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.icon(undefined) === null && QwMfd.caption(undefined) === null").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.icon('not-a-command') === null").AsBoolean());
    }

    [Fact]
    public void ContractPageSaysNothingIsOpenRatherThanLookingEmpty()
    {
        var json = Engine().Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('contract'),{}))").AsString();
        Assert.Contains("NO OPEN CONTRACT", json);
        Assert.Contains("Earlier contracts are in the dashboard logbook", json);
    }
}
