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
        Assert.True(e.Evaluate("[0,4,5,8,9,10,16,17,18,21,28,29,1.5,'1'].every(n => QwMfd.action(n) === null)").AsBoolean());
    }

    [Fact]
    public void AbsentDataExplainsMissingSignalsInsteadOfDisplayingZero()
    {
        var e = Engine();
        Assert.Contains("not yet identified", e.Evaluate("JSON.stringify(QwMfd.rows(0,{}))").AsString());
        Assert.Contains("not supplied", e.Evaluate("JSON.stringify(QwMfd.rows(2,{}))").AsString());
        Assert.Contains("NO SCREENSHOT", e.Evaluate("JSON.stringify(QwMfd.rows(2,{}))").AsString());
        Assert.Contains("Loading", e.Evaluate("JSON.stringify(QwMfd.rows(1,{}))").AsString());
        Assert.Contains("NO OUTSTANDING", e.Evaluate("JSON.stringify(QwMfd.rows(1,{},{stops:[]}))").AsString());
    }

    [Fact]
    public void ScreenshotKeepsCaptureTimeAndDoesNotClaimLiveTelemetry()
    {
        var e = Engine();
        var json = e.Evaluate("JSON.stringify(QwMfd.rows(2,{screen:{summary:'At Lorville',shotAt:'2026-09-09T12:00:00Z'}}))").AsString();
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
}
