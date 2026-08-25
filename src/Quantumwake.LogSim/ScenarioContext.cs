namespace Quantumwake.LogSim;

/// <summary>Mutable clock and repeated log shapes shared by deterministic scenarios.</summary>
internal sealed class ScenarioContext
{
    private readonly string _sessionId;
    private int _notificationId;
    private int _flightId;

    public ScenarioContext(
        LogWriter log,
        DateTimeOffset start,
        string handle,
        string geid,
        string sessionId = "01234567-89ab-cdef-0123-456789abcdef",
        int firstNotificationId = 400)
    {
        Log = log;
        Now = start;
        Handle = handle;
        Geid = geid;
        _sessionId = sessionId;
        _notificationId = firstNotificationId;
    }

    public LogWriter Log { get; }
    public DateTimeOffset Now { get; private set; }
    public string Handle { get; }
    public string Geid { get; }

    public void Advance(int seconds) => Now = Now.AddSeconds(seconds);

    public int NextNotificationId() => _notificationId++;

    public void Begin()
    {
        Log.Header(Now, "12344265", "4.9.188.23497");
        Advance(1);
        Log.Character(Now, Handle, Geid);
        Log.Login(Now.AddMilliseconds(120), Handle);
        Advance(2);
        Log.Context(Now, "SC_Frontend", _sessionId);
        Log.LoadingScreen(Now.AddSeconds(1), "Frontend_Main", "SC_Frontend", 3.44);
        Advance(10);
        Log.Context(Now, "SC_Default", _sessionId);
        Log.LoadingScreen(Now.AddSeconds(1), "PU_Megamap", "SC_Default", 21.30);
        Advance(25);
        Log.Spawned(Now);
        Location("RR_MIC_LEO");
    }

    public void End()
    {
        Log.Disconnect(Now, "Remote Disconnect - Player requested disconnect", "SC_Default");
        Advance(2);
        Log.Context(Now, "SC_Frontend", _sessionId);
        Log.LoadingScreen(Now.AddSeconds(1), "Frontend_Main", "SC_Frontend", 2.10);
        Advance(5);
        Log.Disconnect(Now, "Nub destroyed", "SC_Frontend");
    }

    public void Location(string id)
    {
        Log.LocationInventory(Now, Handle, id);
        Log.LocationInventory(Now.AddMilliseconds(200), Handle, id);
        Log.SpamDuplicate(Now.AddMilliseconds(400), Handle, id);
        Advance(2);
    }

    public void Notify(string text, string? missionId = null)
    {
        Log.Notification(Now, text, NextNotificationId(), missionId);
        // Notification follow-ups reach 9.4 seconds; keep entries chronological.
        Advance(10);
    }

    public void Flight(
        string routeDestination,
        string arrival,
        string origin,
        string? vehicle = null,
        string? entity = null)
    {
        var suffix = (++_flightId).ToString("D3");
        entity ??= $"700000000{suffix}";
        vehicle ??= $"MISC_Freelancer_MAX_{entity}";

        Log.QuantumTarget(Now, vehicle, entity, routeDestination);
        Log.RouteWithOrigin(Now.AddSeconds(1), vehicle, entity, origin, routeDestination);
        Advance(60);
        Log.VehicleRelease(Now, Geid, vehicle, entity);
        Advance(5);
        Location(arrival);
    }
}
