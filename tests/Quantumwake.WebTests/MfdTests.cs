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
    [InlineData(1, "slot", 1)] [InlineData(3, "slot", 3)] [InlineData(5, "slot", 5)]
    [InlineData(12, "scroll", 1)] [InlineData(14, "scroll", -1)]
    public void DefaultButtonsFollowCougarClockwiseNumbering(int button, string action, int value)
    {
        Assert.Equal(value, Engine().Evaluate($"QwMfd.action({button}).{action}").AsNumber());
    }

    /// <summary>Cycling still works; it is simply nobody's default any more.</summary>
    [Theory]
    [InlineData("next", "cycle", 1)] [InlineData("prev", "cycle", -1)]
    [InlineData("bright-up", "brightness", 1)] [InlineData("text-down", "text", -1)]
    public void TheCommandsOffTheShippedProfileStillWorkWhenBound(string id, string action, int value)
    {
        Assert.Equal(value, Engine().Evaluate($"QwMfd.action(21,{{21:'{id}'}}).{action}").AsNumber());
    }

    [Fact]
    public void ReservedButtonsAndInvalidInputHaveNoAction()
    {
        var e = Engine();
        Assert.True(e.Evaluate("[0,6,7,9,11,16,20,21,28,29,1.5,'1',-1].every(n => QwMfd.action(n) === null)").AsBoolean());
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
        Assert.Equal("menu-1", e.Evaluate("QwMfd.buttons(null)[1]").AsString());
        Assert.True(e.Evaluate("Object.keys(QwMfd.buttons({})).length === 0").AsBoolean());
    }

    [Fact]
    public void AbsentDataExplainsMissingSignalsInsteadOfDisplayingZero()
    {
        var e = Engine();
        e.Execute("var status = QwMfd.pageIds.indexOf('status');");
        Assert.Contains("not yet identified", e.Evaluate("JSON.stringify(QwMfd.rows(0,{}))").AsString());
        Assert.Contains("NO SCREENSHOT", e.Evaluate("JSON.stringify(QwMfd.rows(status,{}))").AsString());
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
        Assert.Contains("SCREEN READING", json);
    }

    [Fact]
    public void TaskHudShowsOnlyNextUnfinishedActionAndPreservesPlannedUnits()
    {
        var json = Engine().Evaluate("JSON.stringify(QwMfd.rows(1,{},{tripTitle:'Medical run',stops:[{place:'Baijini Point',actions:[{done:true,text:'Old instruction'},{kind:'load',quantity:32,unit:'SCU',text:'Medical supplies',done:false},{kind:'sell',text:'Later instruction',done:false}]}]}))").AsString();
        Assert.Contains("load · 32 SCU · Medical supplies", json);
        Assert.Contains("Baijini Point", json);
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
        // The wording moved to the pinned strip - see the action-strip test.
        // What the list itself still has to do is say which line is which.
        Assert.Contains("\"cursor\"", Act(Engine()));
        var armed = Act(Engine(), "{selected:0,armed:true}");
        Assert.Contains("\"armed\"", armed);
        Assert.DoesNotContain("\"cursor\"", armed);
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
        Assert.Contains("Counter receipts and your plan. Never the hold.", json);
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
        Assert.Contains("1 more", json);

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
        // travelling as well as travellingToId: the destination id outlives the
        // jump, and a stale one must not push itself in front of the plan.
        e.Execute($"var v = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'ArcCorp',"
            + "travelling:true,travellingToId:'RR_MIC_LEO'}," + plan + ");");
        Assert.Equal("microTech", e.Evaluate("v.next").AsString());
        Assert.Equal("Hurston", e.Evaluate("v.target").AsString());
        Assert.Contains("not the route flown", e.Evaluate("v.note").AsString());

        e.Execute($"var stale = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'ArcCorp',"
            + "travellingToId:'RR_MIC_LEO'}," + plan + ");");
        Assert.Equal("Hurston", e.Evaluate("stale.next").AsString());

        e.Execute($"var p = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'ArcCorp'}},{plan});");
        Assert.Equal("Hurston", e.Evaluate("p.next").AsString());

        // Standing on the destination is not a leg to fly.
        e.Execute($"var s = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'Hurston'}},{plan});");
        Assert.True(e.Evaluate("s.legs.length === 0 && s.next === null").AsBoolean());
    }

    /// <summary>
    /// The whole plan, leg by leg, rather than only the next hop - and the
    /// distance is straight line between body centres, which is all the
    /// coordinates support. The game plots quantum routes and never writes one
    /// down, so the caption says what the number is not.
    /// </summary>
    [Fact]
    public void RouteChainsEveryPlannedStopAndMeasuresEachLeg()
    {
        var e = Engine();
        const string plan = "{stops:[{placeId:'STAN_HUR_L1',place:'Everus Harbor'},{placeId:'RR_MIC_LEO'}]}";
        e.Execute($"var v = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'ArcCorp'}},{plan});");

        Assert.Equal(2, e.Evaluate("v.legs.length").AsNumber());
        Assert.Equal("Hurston", e.Evaluate("v.next").AsString());
        Assert.Equal("microTech", e.Evaluate("v.target").AsString());

        // ArcCorp (0,-28.9) to Hurston (12.85,0), then Hurston to microTech.
        Assert.Equal(31.65, e.Evaluate("v.legs[0].gm").AsNumber(), 1);
        Assert.Equal(56.29, e.Evaluate("v.legs[1].gm").AsNumber(), 1);
        Assert.Equal(87.94, e.Evaluate("v.gm").AsNumber(), 1);
        Assert.Equal("87.9 Gm to microTech over 2 legs", e.Evaluate("QwMfd.routeLine(v)").AsString());
        Assert.True(e.Evaluate("v.bodies.find(b => b.name === 'microTech').onRoute").AsBoolean());
        Assert.Contains("direct line, not the route flown", e.Evaluate("v.note").AsString());

        // And the Nav headline quotes the same view rather than measuring again,
        // on the destination's own line rather than a row below it.
        Assert.Contains("Everus Harbor · 87.9 Gm over 2 legs",
            e.Evaluate($"JSON.stringify(QwMfd.rows(0,{{}},{plan},false,{{map:v}}))").AsString());
        Assert.DoesNotContain("Gm", e.Evaluate("JSON.stringify(QwMfd.rows(0,{}))").AsString());
    }

    /// <summary>
    /// Two jobs at one body is two stops and one arrival, so a leg of no length
    /// never reaches the plan.
    /// </summary>
    [Fact]
    public void RepeatedAndUnreachableStopsDoNotBecomeLegs()
    {
        var e = Engine();
        e.Execute($"var v = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'ArcCorp'}},"
            + "{stops:[{placeId:'STAN_HUR_L1'},{placeId:'STAN_HUR_L1'},{placeId:'NOWHERE'}]});");
        Assert.Equal(1, e.Evaluate("v.legs.length").AsNumber());
        Assert.Equal(1, e.Evaluate("v.elsewhere").AsNumber());
        Assert.Contains("1 stop outside this system", e.Evaluate("v.note").AsString());
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

    private static string Page(Engine e, string id, string state = "{}", string plan = "null", string view = "{}") =>
        e.Evaluate($"JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('{id}'),{state},{plan},false,{view}))").AsString();

    [Fact]
    public void EveryPageIsReachableThroughADeepMenuAndHasAWayBack()
    {
        var e = Engine();
        Assert.Equal(16, e.Evaluate("QwMfd.pageIds.length").AsNumber());
        Assert.True(e.Evaluate(
            "QwMfd.pageIds.every(id => QwMfd.trail(id)[0] === 'home'"
            + " && QwMfd.trail(id)[QwMfd.trail(id).length - 1] === id"
            + " && QwMfd.menuItems(QwMfd.parent(id)).includes(id))").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.pageIds.every(id => QwMfd.icon(id) && QwMfd.caption(id))").AsBoolean());

        Assert.True(e.Evaluate("['prev','next','text-up','text-down','bright-up','bright-down']"
            + ".every(id => QwMfd.commands.some(c => c.id === id)"
            + " && !Object.values(QwMfd.defaults).includes(id))").AsBoolean());
        Assert.Equal("back", e.Evaluate("QwMfd.defaults[15]").AsString());
        Assert.Equal("home", e.Evaluate("QwMfd.defaults[13]").AsString());
        Assert.True(e.Evaluate("QwMfd.groups.every(g => QwMfd.parent(g.id) === 'home')").AsBoolean());
        // Exactly three: home, a category, the screen. Nothing is more than two
        // presses away, which is the whole point of collapsing the middle tier.
        Assert.True(e.Evaluate("QwMfd.pageIds.every(id => QwMfd.trail(id).length === 3)").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.groups.every(g => QwMfd.menuItems(g.id).length <= 5)").AsBoolean());
    }

    [Fact]
    public void MenuKeysFollowTheVisibleCategoryIncludingEmptySlots()
    {
        var e = Engine();
        Assert.Equal("flight", e.Evaluate("QwMfd.resolveCommand('menu-1','home')").AsString());
        Assert.Equal("map", e.Evaluate("QwMfd.resolveCommand('menu-2','flight')").AsString());
        Assert.Equal("here", e.Evaluate("QwMfd.resolveCommand('menu-4','flight')").AsString());

        // Standing on a screen, the row still offers its siblings.
        Assert.Equal("map", e.Evaluate("QwMfd.resolveCommand('menu-2','nav')").AsString());

        // Navigation rides Home's fifth slot, which the four categories left empty.
        Assert.Equal("nav", e.Evaluate("QwMfd.resolveCommand('menu-5','home')").AsString());
        Assert.Equal("log", e.Evaluate("QwMfd.resolveCommand('menu-4','pilot')").AsString());
        Assert.Equal("cargo", e.Evaluate("QwMfd.resolveCommand('cargo','pilot')").AsString());
        Assert.Equal("back", e.Evaluate("QwMfd.effect('back').menu").AsString());
    }

    /// <summary>
    /// One category between Home and any screen. The middle tier that used to
    /// sit here split two screens apiece, so it cost a press on the way to
    /// everything and sorted nothing.
    /// </summary>
    [Fact]
    public void EveryScreenSitsOneCategoryBelowHome()
    {
        var e = Engine();
        Assert.Equal("flight", e.Evaluate("QwMfd.parent('nav')").AsString());
        Assert.Equal("home", e.Evaluate("QwMfd.parent('flight')").AsString());
        Assert.Equal("nav", e.Evaluate("QwMfd.previewPage('flight')").AsString());
        Assert.Equal("map", e.Evaluate("QwMfd.previewPage('map')").AsString());
        Assert.Equal("home/flight/nav", e.Evaluate("QwMfd.trail('nav').join('/')").AsString());
        Assert.Equal("home/pilot/crew", e.Evaluate("QwMfd.trail('crew').join('/')").AsString());
    }

    /// <summary>
    /// A frame nobody has used yet opens on something worth reading. Two frames
    /// both starting at Home showed the same list twice, which wastes the second
    /// one - and having two is the whole point of a Cougar pair.
    /// </summary>
    [Fact]
    public void AFreshFrameOpensOnItsCockpitDefaultRatherThanTheMenu()
    {
        var e = Engine();
        Assert.Equal("nav", e.Evaluate("QwMfd.restoreScreen(null,'nav')").AsString());
        Assert.Equal("task", e.Evaluate("QwMfd.restoreScreen({},'task')").AsString());

        // What the frame remembers still wins over the default.
        Assert.Equal("ledger", e.Evaluate("QwMfd.restoreScreen({screen:'ledger'},'nav')").AsString());

        // A fallback that is not a screen, and no fallback at all, both land Home.
        Assert.Equal("home", e.Evaluate("QwMfd.restoreScreen({},'flight-route')").AsString());
        Assert.Equal("home", e.Evaluate("QwMfd.restoreScreen({})").AsString());
    }

    [Fact]
    public void OldPagePreferencesMigrateAndInvalidScreensReturnHome()
    {
        var e = Engine();
        Assert.Equal("home", e.Evaluate("QwMfd.restoreScreen({})").AsString());
        Assert.Equal("act", e.Evaluate("QwMfd.restoreScreen({page:2})").AsString());
        Assert.Equal("pilot", e.Evaluate("QwMfd.restoreScreen({screen:'pilot',page:2})").AsString());
        Assert.Equal("home", e.Evaluate("QwMfd.restoreScreen({screen:'missing',page:900})").AsString());
    }

    [Fact]
    public void OverviewAndDetailsKeepEveryReadingAndTheCargoQualifier()
    {
        var e = Engine();
        e.Execute("var cargo = QwMfd.rows(QwMfd.pageIds.indexOf('cargo'),{}, {stops:[]});"
            + "var brief = QwMfd.readingView('cargo',cargo,false); var detail = QwMfd.readingView('cargo',cargo,true);");
        Assert.True(e.Evaluate("brief.more && detail.more").AsBoolean());
        Assert.True(e.Evaluate("[brief,detail].every(v => v.rows.some(r => r[1].includes('Never the hold')))").AsBoolean());
        Assert.True(e.Evaluate("cargo.every(r => [...brief.rows,...detail.rows].some(v => JSON.stringify(v) === JSON.stringify(r)))").AsBoolean());
        Assert.True(e.Evaluate($"!QwMfd.readingView('act',QwMfd.rows(2,{{}},{Plan}),false).more").AsBoolean());
    }

    /// <summary>
    /// A frame that lights a button doing nothing has to be learned; one that
    /// dims it can be read. Only Up, Down and Done are ever in doubt.
    /// </summary>
    [Fact]
    public void OnlyTheThreeContextualButtonsEverGoDormant()
    {
        var e = Engine();
        string Idle(string page, string ctx = "{}") =>
            e.Evaluate($"JSON.stringify(QwMfd.dormant('{page}',{ctx}))").AsString();

        // A page that fits, with no cursor: nothing to move, nothing to confirm.
        Assert.Equal("[\"up\",\"down\",\"confirm\"]", Idle("nav"));

        // Long enough to scroll, so up and down wake - but Done still cannot act.
        Assert.Equal("[\"confirm\"]", Idle("feed", "{scrollable:true}"));

        // Act with work on it: Done is live, and a cursor needs two to move between.
        Assert.Equal("[\"up\",\"down\"]", Idle("act", "{tasks:1}"));
        Assert.Equal("[]", Idle("act", "{tasks:3}"));
        Assert.Equal("[\"up\",\"down\",\"confirm\"]", Idle("act", "{tasks:0}"));

        // A page button is never dimmed: a way out that vanishes is worse than
        // a redundant one.
        Assert.True(e.Evaluate(
            "QwMfd.pageIds.every(id => !QwMfd.dormant('nav',{}).includes(id))").AsBoolean());
    }

    [Fact]
    public void FeedShowsTheNewestEntriesAndSaysWhenTheLogHasBeenQuiet()
    {
        var e = Engine();
        const string feed = "{recentEvents:[{at:'2026-09-09T12:02:00Z',kind:'contract-done',"
            + "text:'Contract completed',detail:'Recover the cargo'},"
            + "{at:'2026-09-09T12:00:00Z',kind:'bought',text:'Bought 96 SCU',detail:'Area18 TDD'}]}";
        var json = Page(e, "feed", feed);
        Assert.Contains("CONTRACT DONE", json);
        Assert.Contains("Contract completed · Recover the cargo", json);
        Assert.Contains("2026-09-09T12:02:00Z", json);
        Assert.Contains("NOTHING YET", Page(e, "feed"));
    }

    [Fact]
    public void LogMixesScreenshotsAndCopiedLocationsAndMarksPinnedPoints()
    {
        var e = Engine();
        const string log = "{readings:[{shot:'map.jpg',shotAt:'2026-09-09T12:00:00Z',kind:'Map',summary:'PYRO > RUIN STATION'}],"
            + "clipboard:[{at:'2026-09-09T12:02:00Z',gigametres:14.99996,believed:'Ruin Station',system:'Pyro'}],"
            + "pins:[{sourceAt:'2026-09-09T12:02:00Z'}]}";

        var json = Page(e, "log", view: "{extra:{screenLog:" + log + "}}");

        Assert.Contains("PINNED POI", json);
        Assert.Contains("15.0000 Gm · Pyro · Ruin Station", json);
        Assert.Contains("SCREEN · MAP", json);
        Assert.Contains("PYRO > RUIN STATION", json);
        Assert.Contains("NO LOG ENTRIES", Page(e, "log", view: "{extra:{screenLog:{}}}"));
    }

    /// <summary>
    /// The party channel names people; it does not enumerate them. The page has
    /// to say that, because a silent crewmate looks identical to no crewmate.
    /// </summary>
    [Fact]
    public void CrewIsAlwaysLabelledAFloorRatherThanARoster()
    {
        var e = Engine();
        Assert.Contains("Absence means nothing", Page(e, "crew"));
        Assert.Contains("NOBODY NAMED", Page(e, "crew"));

        var json = Page(e, "crew", "{party:[{handle:'nekron',moment:'joined',at:'2026-09-09T12:00:00Z'}],"
            + "partyDisbanded:true}");
        Assert.Contains("NEKRON", json);
        Assert.Contains("joined", json);
        Assert.Contains("DISBANDED", json);
        Assert.Contains("Absence means nothing", json);
    }

    [Fact]
    public void MoneyQuotesTheRateItUsedAndWhatItLeavesOut()
    {
        var e = Engine();
        const string earnings = "{extra:{earnings:{basis:'recent',"
            + "window:{earned:250000,perHour:24215.86,days:30},"
            + "lifetime:{earned:3865786,perHour:100,days:0},"
            + "goal:{name:'A Cutlass',target:1200000},hoursToGoal:49.55}}}";
        var json = Page(e, "money", "{}", "null", earnings);
        Assert.Contains("24,216 aUEC per hour", json);
        Assert.Contains("the last 30 days", json);
        Assert.Contains("A Cutlass · 1,200,000 aUEC", json);
        Assert.Contains("50 h of flying", json);
        Assert.Contains("TRADING RATE", json);

        // No rate at all is said, not shown as zero.
        Assert.Contains("Too little recorded flying time",
            Page(e, "money", "{}", "null", "{extra:{earnings:{basis:'lifetime',lifetime:{earned:0,perHour:0,days:0}}}}"));
        Assert.Contains("NO GOAL SET",
            Page(e, "money", "{}", "null", "{extra:{earnings:{basis:'lifetime',lifetime:{earned:1,perHour:5,days:0}}}}"));
        Assert.Contains("Reading what the ledger recorded", Page(e, "money"));
    }

    [Fact]
    public void ListShowsProgressAndSaysHeldIsNotACount()
    {
        var e = Engine();
        const string jobs = "{extra:{jobs:["
            + "{title:'Mining kit',done:false,pinned:true,haveCount:2,totalCount:5,destination:'Lorville'},"
            + "{title:'Finished list',done:true,haveCount:3,totalCount:3},"
            + "{title:'Armour',done:false,haveCount:0,totalCount:2}]}}";
        var json = Page(e, "list", "{}", "null", jobs);
        Assert.Contains("★ MINING KIT", json);
        Assert.Contains("2 of 5 seen · Lorville", json);
        Assert.Contains("ARMOUR", json);
        Assert.DoesNotContain("FINISHED LIST", json);
        // "seen" rather than "held": a stash listing records presence, never a count,
        // and the word carries that now the qualifier row is gone.
        Assert.DoesNotContain("held", json);

        Assert.Contains("NO LIST IN HAND", Page(e, "list", "{}", "null", "{extra:{jobs:[]}}"));
        Assert.Contains("Reading your lists", Page(e, "list"));
    }

    /// <summary>
    /// Session, Handle, "This session" and "Wake up at" all landed on Status
    /// rather than each taking a page of their own.
    /// </summary>
    [Fact]
    public void StatusCarriesTheFoldedInNowCards()
    {
        var e = Engine();
        const string now = "{sessionStarted:'2026-09-09T10:00:00Z',handle:'nekron',deaths:1,incapacitations:3}";
        const string view = "{now:1788956400000,extra:{respawn:{known:true,place:'Seraphim Station',"
            + "at:'2026-08-22T04:23:57Z',agreeing:1,of:4,"
            + "bed:{place:'Pyro Gateway',at:'2026-09-09T02:35:00Z',times:203}}}}";
        var json = Page(e, "status", now, "null", view);
        Assert.Contains("2h 20m", json);
        Assert.Contains("1 death · 3 incapacitations", json);

        // The bed is the newer sighting, so it is the answer - and the page
        // says how thin that evidence is either way.
        Assert.Contains("Pyro Gateway", json);
        Assert.Contains("A medical bed used 203 times", json);
        Assert.Contains("never states a regen point", json);

        // Older bed than the last death: the death wins, with its own tally.
        Assert.Contains("1 of 4 deaths woke there", Page(e, "status", now, "null",
            "{extra:{respawn:{known:true,place:'Seraphim Station',at:'2026-09-09T04:00:00Z',"
            + "agreeing:1,of:4,bed:{place:'Pyro Gateway',at:'2026-08-01T02:35:00Z',times:203}}}}"));

        // Nothing known, nothing claimed.
        Assert.DoesNotContain("WAKE UP AT", Page(e, "status", now, "null", "{extra:{respawn:{known:false}}}"));
        Assert.DoesNotContain("WAKE UP AT", Page(e, "status", now));
        Assert.Contains("No session start recorded", Page(e, "status"));
    }

    /// <summary>
    /// An index is not an identity. The plan is re-read every five seconds, so
    /// the line the cursor was on when DONE was armed can be a different job by
    /// the time DONE is pressed again - and the second press must not tick off
    /// whatever happens to be sitting there now.
    /// </summary>
    [Fact]
    public void AConfirmationIsTiedToTheTaskItWasArmedAgainst()
    {
        var e = Engine();
        e.Execute($"var before = QwMfd.tasks({Plan});");

        // The same plan re-read: same identities, so the press still stands.
        Assert.True(e.Evaluate($"QwMfd.sameTask(before[0], QwMfd.tasks({Plan})[0])").AsBoolean());

        // The first instruction got done elsewhere, so index 0 is now the second
        // one. Same cursor, different job - and sameTask sees it.
        const string moved = "{tripId:'t1',stops:[{id:'s1',place:'Baijini Point',actions:["
            + "{id:'a1',kind:'load',quantity:32,unit:'SCU',text:'Titanium',done:true},"
            + "{id:'a2',kind:'refuel',text:'Top up quantum',done:false}]}]}";
        Assert.False(e.Evaluate($"QwMfd.sameTask(before[0], QwMfd.tasks({moved})[0])").AsBoolean());

        // A stop crossed off is identified by its stop, not by a null action id.
        const string bare = "{tripId:'t1',stops:[{id:'s1',place:'Baijini Point',actions:[]}]}";
        Assert.True(e.Evaluate($"QwMfd.sameTask(QwMfd.tasks({bare})[0], QwMfd.tasks({bare})[0])").AsBoolean());
        Assert.False(e.Evaluate($"QwMfd.sameTask(QwMfd.tasks({bare})[0], before[0])").AsBoolean());

        // Nothing is never the same as anything, including nothing.
        Assert.True(e.Evaluate("QwMfd.sameTask(null, null) === false").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.sameTask(undefined, before[0]) === false").AsBoolean());
    }

    /// <summary>
    /// The confirmation used to be the last row of the very list it was asking
    /// about, so it scrolled out of sight. It has a strip of its own now, and
    /// the strip is silent anywhere it would have nothing to say.
    /// </summary>
    [Fact]
    public void TheActionStripNamesTheTaskAtEveryStage()
    {
        var e = Engine();
        string Strip(string view) => e.Evaluate($"JSON.stringify(QwMfd.actionLine('act',{view}))").AsString();

        var ready = Strip($"{{briefing:{Plan},selected:0}}");
        Assert.Contains("\"ready\"", ready);
        Assert.Contains("DONE marks: load · 32 SCU · Titanium", ready);
        Assert.Contains("Tells the game nothing", ready);

        var armed = Strip($"{{briefing:{Plan},selected:1,armed:true}}");
        Assert.Contains("\"armed\"", armed);
        Assert.Contains("Confirm: refuel · Top up quantum?", armed);

        var saved = Strip($"{{briefing:{Plan},saved:'Marked done: refuel · Top up quantum'}}");
        Assert.Contains("\"saved\"", saved);
        Assert.Contains("Marked done: refuel", saved);

        // Nothing to act on, and nowhere but Act.
        Assert.Equal("null", Strip("{briefing:{stops:[]}}"));
        Assert.Equal("null", e.Evaluate($"JSON.stringify(QwMfd.actionLine('nav',{{briefing:{Plan}}}))").AsString());

        // The confirmation is no longer a row that can scroll away.
        Assert.DoesNotContain("CONFIRM", Act(e));
    }

    /// <summary>
    /// A nav page exists to say where you are going. It was saying it third,
    /// under a map, off the bottom of a 480 px panel.
    /// </summary>
    [Fact]
    public void NavLeadsWithTheDestinationAndItsDistance()
    {
        var e = Engine();
        e.Execute($"var v = QwMfd.mapView({Atlas},{{locationSystem:'Stanton',locationBody:'ArcCorp'}},"
            + "{stops:[{placeId:'STAN_HUR_L1',place:'Everus Harbor'}]});");
        var rows = e.Evaluate("QwMfd.rows(0,{location:'Area18',ship:'RSI Hermes'},"
            + "{stops:[{placeId:'STAN_HUR_L1',place:'Everus Harbor'}]},false,{map:v})").AsArray();

        Assert.Equal("NEXT STOP", rows[0].AsArray()[0].AsString());
        Assert.Equal("Everus Harbor · 31.6 Gm", rows[0].AsArray()[1].AsString());
        Assert.Equal("LOCATION", rows[1].AsArray()[0].AsString());

        // A quantum destination still takes the headline, and multi-leg says so.
        var json = e.Evaluate("JSON.stringify(QwMfd.rows(0,{travelling:true,travellingTo:'Port Tressler'},"
            + "{stops:[]},false,{map:{legs:[1,2],gm:87.94,target:'microTech'}}))").AsString();
        Assert.Contains("QUANTUM DESTINATION", json);
        Assert.Contains("Port Tressler · 87.9 Gm over 2 legs", json);

        // No plan, no invented distance.
        Assert.Contains("No outstanding stop",
            e.Evaluate("JSON.stringify(QwMfd.rows(0,{},{stops:[]}))").AsString());
    }

    [Fact]
    public void ASmallOpeningGetsShortCaptionsRatherThanClippedOnes()
    {
        var e = Engine();
        Assert.Equal("CNTRCT", e.Evaluate("QwMfd.caption('contract')").AsString());
        Assert.Equal("CNTR", e.Evaluate("QwMfd.caption('contract', true)").AsString());
        Assert.Equal("CRGO", e.Evaluate("QwMfd.caption('cargo', true)").AsString());
        Assert.Equal("STAT", e.Evaluate("QwMfd.caption('status', true)").AsString());

        // Every command has one, and none of them is longer than the frame fits.
        Assert.True(e.Evaluate("QwMfd.commands.every(c => c.short && c.short.length <= 4)").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.commands.every(c => QwMfd.caption(c.id, true).length <= 4)").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.caption('nope', true) === null").AsBoolean());
    }

    /// <summary>
    /// Six of the briefing's nine fields were being fetched every five seconds
    /// and discarded. These pages read them; none of them needed a new call.
    /// </summary>
    [Fact]
    public void ShipReadsTheFocusAndClaimTheBriefingAlreadyCarried()
    {
        var e = Engine();
        const string full = "{focus:{label:'Freight',career:'Transporter',role:'Medium Freight'},"
            + "claim:{ship:'RSI Hermes',expeditedCost:12500,expeditedMinutes:5,standardMinutes:22}}";
        var json = Page(e, "ship", "{ship:'RSI Hermes'}", full);
        Assert.Contains("Freight · Transporter · Medium Freight", json);
        Assert.Contains("12,500 aUEC expedited · 5 min", json);
        Assert.Contains("22 min and no fee", json);
        Assert.Contains("CLAIM, PER THE TABLES", json);

        // No claim tables for this hull: said, not implied by an empty row.
        Assert.Contains("Nothing for this hull",
            Page(e, "ship", "{ship:'Greycat ROC'}", "{focus:null}"));
        Assert.Contains("No ship identified", Page(e, "ship"));
    }

    [Fact]
    public void HereAnswersWhatThisPlaceOffersAndWhatWasLeftAtIt()
    {
        var e = Engine();
        const string place = "{location:'Port Tressler',"
            + "services:[{name:'Refuel',status:'listed'},{name:'Repair',status:'not listed'}],"
            + "shopping:[{name:'Medical supplies',needed:4,unit:'units',price:2100,jobTitle:'Kit'}],"
            + "stash:[{name:'MedPen',category:'Medical',lastSeen:'2026-09-08T22:39:22Z'}]}";
        var json = Page(e, "here", "{}", place);
        Assert.Contains("Port Tressler", json);
        // Only what is actually here, not five statuses saying nothing is.
        Assert.Contains("Refuel", json);
        Assert.DoesNotContain("Repair", json);
        Assert.Contains("Medical supplies · 4 units · 2,100 aUEC", json);
        Assert.Contains("MedPen", json);
        Assert.Contains("2026-09-08T22:39:22Z", json);
        Assert.Contains("SEEN HERE BEFORE", json);

        // A place the installed data cannot describe says so.
        Assert.Contains("Nothing the installed data can identify",
            Page(e, "here", "{}", "{location:'Somewhere'}"));
    }

    [Fact]
    public void LedgerShowsOnlyWhatTheServerConfirmed()
    {
        var e = Engine();
        const string money = "{extra:{ledger:[{at:'2026-09-08T22:39:22Z',kind:'Item bought',"
            + "what:'MedPen (Hemozal)',where:'Pyro Gateway',amount:-1855,confirmed:true}]}}";
        var json = Page(e, "ledger", "{}", "null", money);
        Assert.Contains("ITEM BOUGHT", json);
        Assert.Contains("MedPen (Hemozal) · -1,855 aUEC · Pyro Gateway", json);
        Assert.Contains("MedPen (Hemozal)", json);

        Assert.Contains("NOTHING PRICED", Page(e, "ledger", "{}", "null", "{extra:{ledger:[]}}"));
        Assert.Contains("Reading what the logs priced", Page(e, "ledger"));
    }

    [Fact]
    public void MineRanksPlacesAndSaysTheyAreTablesRatherThanSightings()
    {
        var e = Engine();
        const string rocks = "{mining:[{place:'Aaron Halo',system:'Stanton',perRock:18400,best:'Quantainium',here:true},"
            + "{place:'Yela belt',system:'Stanton',perRock:9200,best:'Bexalite',here:false}]}";
        var json = Page(e, "mine", "{}", rocks);
        Assert.Contains("AARON HALO", json);
        Assert.Contains("18,400 aUEC a rock · Quantainium", json);
        Assert.Contains("YELA BELT · Stanton", json);
        Assert.Contains("YELA BELT", json);
        Assert.Contains("18,400 aUEC a rock", json);

        Assert.Contains("NOTHING RANKED", Page(e, "mine", "{}", "{mining:[]}"));
    }

    [Fact]
    public void CargoCarriesTradeLeadsWithoutCallingThemCargo()
    {
        var e = Engine();
        const string plan = "{stops:[],trade:[{commodity:'Agricium',marginPerScu:412,sellTerminal:'Area18 TDD'},"
            + "{commodity:'Titanium',marginPerScu:88,sellTerminal:'Lorville'},"
            + "{commodity:'Gold',marginPerScu:12,sellTerminal:'Orison'}]}";
        var json = Page(e, "cargo", "{}", plan);
        Assert.Contains("TRADE FROM HERE", json);
        Assert.Contains("Agricium · +412/SCU at Area18 TDD", json);
        Assert.DoesNotContain("Gold", json);
        Assert.Contains("Never the hold", json);
        Assert.DoesNotContain("TRADE FROM HERE", Page(e, "cargo", "{}", "{stops:[]}"));
    }

    [Fact]
    public void ContractPageSaysNothingIsOpenRatherThanLookingEmpty()
    {
        var json = Engine().Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('contract'),{}))").AsString();
        Assert.Contains("NO OPEN CONTRACT", json);
        Assert.Contains("Earlier ones are in the logbook", json);
    }

    /// <summary>
    /// Off is the shipped answer, so a zero here has to mean never rather than
    /// immediately - the difference between a setting nobody asked for and a
    /// frame that dims the moment it is left alone.
    /// </summary>
    [Fact]
    public void IdleDimmingOnlyHappensAfterATimeThePilotPicked()
    {
        var e = Engine();
        Assert.False(e.Evaluate("QwMfd.dozed(0, 60 * 60000)").AsBoolean());
        Assert.False(e.Evaluate("QwMfd.dozed(5, 4.9 * 60000)").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.dozed(5, 5 * 60000)").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.dozed(5, 90 * 60000)").AsBoolean());

        // Dim, never dark: a frame you can still read beats one that has to be
        // woken before it can be, which is the whole reason it is not a blank.
        Assert.InRange(e.Evaluate("QwMfd.DOZE").AsNumber(), .2, .5);
    }

    /// <summary>
    /// The cycling button wraps so no press is ever dead; the rocker stops at
    /// the ends, because holding one down should not roll off the bottom into
    /// full brightness.
    /// </summary>
    [Fact]
    public void BrightnessMovesInFourLevelsAndOnlyTheCyclingButtonWraps()
    {
        var e = Engine();
        Assert.Equal("0.4,0.6,0.8,1", e.Evaluate("QwMfd.BRIGHTNESS.join(',')").AsString());
        Assert.Equal(3, e.Evaluate("QwMfd.brightnessLevel(.8)").AsNumber());
        Assert.Equal(4, e.Evaluate("QwMfd.brightnessLevel(undefined)").AsNumber());

        Assert.Equal(.4, e.Evaluate("QwMfd.cycleBrightness(1)").AsNumber());
        Assert.Equal(.6, e.Evaluate("QwMfd.cycleBrightness(.4)").AsNumber());

        Assert.Equal(1, e.Evaluate("QwMfd.stepBrightness(1, 1)").AsNumber());
        Assert.Equal(.4, e.Evaluate("QwMfd.stepBrightness(.4, -1)").AsNumber());
        Assert.Equal(.8, e.Evaluate("QwMfd.stepBrightness(.6, 1)").AsNumber());

        // A button that lands somewhere unreachable would be worse than one that
        // did nothing, so every route out of a stray value is one of the four.
        Assert.True(e.Evaluate("[0,.35,.7,.9,5,NaN].every(v => QwMfd.BRIGHTNESS.includes(QwMfd.cycleBrightness(v)) && QwMfd.BRIGHTNESS.includes(QwMfd.stepBrightness(v,-1)))").AsBoolean());

        // The pair and the cycler are separate bindings: the frame has a rocker
        // printed BRT, and taking that away to save a button would be a trade.
        Assert.True(e.Evaluate("['bright','bright-up','bright-down'].every(id => QwMfd.commands.some(c => c.id === id))").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.effect('bright').cycleBright").AsBoolean());
    }

    /// <summary>
    /// The ledger showed the first few entries and said there were more, with
    /// nothing that could reach them: a cockpit frame has no scrollbar, so a
    /// list longer than the panel has to be paged or it is simply truncated.
    /// </summary>
    [Fact]
    public void TheLedgerPagesThroughEveryEntryAndStopsAtBothEnds()
    {
        var e = Engine();
        e.Execute("var entries = Array.from({length: 8}, (_, i) => ({ kind: 'bought', what: 'item ' + i, amount: -i, at: 'then' }));");
        e.Execute("var view = offset => ({ extra: { ledger: entries }, offset });");

        Assert.Equal(3, e.Evaluate("QwMfd.LEDGER_PAGE").AsNumber());
        Assert.Equal("item 0,item 1,item 2", e.Evaluate("QwMfd.pageOf(entries, 0, 3).items.map(x => x.what).join(',')").AsString());
        Assert.Equal("item 6,item 7", e.Evaluate("QwMfd.pageOf(entries, 6, 3).items.map(x => x.what).join(',')").AsString());

        // Where you are, which a scroll position cannot state.
        Assert.Equal("1-3 of 8", e.Evaluate("QwMfd.pageLabel('ledger', view(0))").AsString());
        Assert.Equal("7-8 of 8", e.Evaluate("QwMfd.pageLabel('ledger', view(6))").AsString());

        Assert.Equal(3, e.Evaluate("QwMfd.pageStep('ledger', view(0), 1)").AsNumber());
        Assert.Equal(0, e.Evaluate("QwMfd.pageStep('ledger', view(3), -1)").AsNumber());

        // Clamped rather than wrapped: running off the end of a ledger back to
        // today reads exactly like nothing having happened.
        Assert.Equal(0, e.Evaluate("QwMfd.pageStep('ledger', view(0), -1)").AsNumber());
        Assert.Equal(6, e.Evaluate("QwMfd.pageStep('ledger', view(6), 1)").AsNumber());

        // Every entry is reachable, which is the whole point of the change.
        Assert.Equal(8, e.Evaluate("(() => { const seen = new Set(); for (let o = 0; o <= 6; o += 3) for (const x of QwMfd.pageOf(entries, o, 3).items) seen.add(x.what); return seen.size; })()").AsNumber());

        // Not a paged screen: null tells the panel to scroll instead.
        Assert.True(e.Evaluate("QwMfd.pageStep('nav', view(0), 1) === null && QwMfd.pageLabel('nav', view(0)) === null").AsBoolean());

        // One page needs no label and must not light Up and Down.
        Assert.True(e.Evaluate("QwMfd.pageLabel('ledger', { extra: { ledger: entries.slice(0, 2) }, offset: 0 }) === null").AsBoolean());
        Assert.Contains("up", (object[])e.Evaluate("QwMfd.dormant('ledger', { scrollable: false, paged: false })").ToObject()!);
        Assert.DoesNotContain("up", (object[])e.Evaluate("QwMfd.dormant('ledger', { scrollable: false, paged: true })").ToObject()!);
    }
}
