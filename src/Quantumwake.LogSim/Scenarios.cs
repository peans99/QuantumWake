namespace Quantumwake.LogSim;

/// <summary>A reproducible log story and the facts it is expected to produce.</summary>
public sealed record ScenarioDefinition(
    string Name,
    string Description,
    IReadOnlyList<string> ExpectedFacts);

/// <summary>Named scenarios intended for parser, API, and UI regression testing.</summary>
public static class ScenarioCatalogue
{
    public static IReadOnlyList<ScenarioDefinition> All { get; } =
    [
        new("cargo-run",
            "Buy cargo at Port Tressler, fly to New Babbage, and sell it.",
            ["2 cargo requests", "16 SCU bought for 9,600 aUEC", "16 SCU sold for 12,480 aUEC", "1 ship sortie"]),
        new("spending",
            "Complete one equipment purchase and reject another.",
            ["1 confirmed purchase", "2,400 aUEC confirmed spend", "failed purchase excluded"]),
        new("medical-respawn",
            "Become incapacitated at Port Tressler and wake at New Babbage.",
            ["1 incapacitation", "1 inferred respawn", "1 after-death medical-bed visit"]),
        new("crew-flight",
            "Receive party changes, fly together, and see one member disconnect.",
            ["4 party notes", "D-Rud becomes leader", "1 ship sortie", "1 quantum jump"]),
        new("contract-complete",
            "Accept a two-step Covalex contract, complete it, and receive a blueprint.",
            ["1 completed contract", "2 of 2 visible steps complete", "1 blueprint received"]),
        new("combat",
            "Exercise the archived kill, death, and vehicle-destruction formats.",
            ["1 player kill", "1 player death", "1 destroyed vehicle timeline entry"]),
        new("all",
            "Run every focused scenario in one deterministic session.",
            [
                "2 cargo requests and 1 confirmed item purchase",
                "1 incapacitation, 1 inferred respawn, and 1 medical-bed visit",
                "4 party notes and 2 ship sorties",
                "1 completed two-step contract and 1 blueprint",
                "1 player kill, 1 player death, and 1 destroyed vehicle"
            ])
    ];

    public static ScenarioDefinition? Find(string name) =>
        All.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Writes a named scenario using only log shapes understood from real captures.</summary>
public static class ScenarioRunner
{
    private static readonly string[] FocusedScenarioNames =
    [
        "cargo-run",
        "spending",
        "medical-respawn",
        "crew-flight",
        "contract-complete",
        "combat"
    ];

    public static void Run(
        LogWriter log,
        ScenarioDefinition scenario,
        DateTimeOffset start,
        string handle = "testpilot",
        string geid = "204721322607")
    {
        var context = new ScenarioContext(log, start, handle, geid);
        context.Begin();

        if (scenario.Name.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var name in FocusedScenarioNames)
                RunBody(name, context);
        }
        else
        {
            RunBody(scenario.Name, context);
        }

        context.End();
    }

    private static void RunBody(string name, ScenarioContext context)
    {
        switch (name.ToLowerInvariant())
        {
            case "cargo-run":
                CargoRun(context);
                break;
            case "spending":
                Spending(context);
                break;
            case "medical-respawn":
                MedicalRespawn(context);
                break;
            case "crew-flight":
                CrewFlight(context);
                break;
            case "contract-complete":
                ContractComplete(context);
                break;
            case "combat":
                Combat(context);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown log scenario.");
        }
    }

    private static void CargoRun(ScenarioContext c)
    {
        c.Location("RR_MIC_LEO");
        c.Advance(20);
        c.Log.CommodityTrade(c.Now, c.Geid, 9_600m, 16,
            "bde5a2c8-2ef4-46ac-9403-2fcb79e4016c", false, "Location");
        c.Advance(30);
        c.Flight("Stanton4_NewBabbage", "Stanton4_NewBabbage", "Port Tressler");
        c.Advance(20);
        c.Log.CommodityTrade(c.Now, c.Geid, 12_480m, 16,
            "bde5a2c8-2ef4-46ac-9403-2fcb79e4016c", true, "ResourceContainer");
        c.Advance(15);
    }

    private static void Spending(ScenarioContext c)
    {
        c.Location("Stanton4_NewBabbage");
        c.Advance(10);
        c.Log.ShopRequest(c.Now, c.Geid, "SCShop_CenterMass_NewBabbage",
            "752023944375", "752023944372", 2_400m, "behr_rifle_ballistic_01", 1);
        c.Advance(2);
        c.Log.ShopResponse(c.Now, "SCShop_CenterMass_NewBabbage", "752023944372", "Success");
        c.Advance(10);
        c.Log.ShopRequest(c.Now, c.Geid, "SCShop_OmegaPro_NewBabbage",
            "752023944376", "752023944373", 475_200m, "POWR_JUST_S02_Genoa_SCItem", 1);
        c.Advance(2);
        c.Log.ShopResponse(c.Now, "SCShop_OmegaPro_NewBabbage", "752023944373", "InsufficientFunds");
        c.Advance(10);
    }

    private static void MedicalRespawn(ScenarioContext c)
    {
        c.Location("RR_MIC_LEO");
        c.Advance(10);
        c.Log.Incapacitated(c.Now, c.NextNotificationId());
        c.Advance(30);
        c.Location("Stanton4_NewBabbage");
        c.Advance(5);
        c.Notify("Medical Bed: The bed has restored your health and reset your BDL.");
    }

    private static void CrewFlight(ScenarioContext c)
    {
        c.Notify("Party D-Rud connected.:");
        c.Notify("Party astro_ice connected.:");
        c.Notify("New Party Leader D-Rud is now party leader.:");
        c.Flight("LOC_RR_S4_L1", "RR_MIC_L1", "New Babbage");
        c.Notify("Party astro_ice disconnected.:");
    }

    private static void ContractComplete(ScenarioContext c)
    {
        const string mission = "11111111-2222-3333-4444-555555555555";

        c.Log.ContractMarker(c.Now, mission, "Covalex_RecoverCargo",
            "Covalex_Stanton_VeryHard_RecoverCargo",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "99999999-8888-7777-6666-555555555555");
        c.Advance(1);
        c.Notify("Contract Accepted: Bulk Covalex Shipment Needs Recovering:", mission);
        c.Log.MissionObjective(c.Now, mission, "pickup_crate_0", "MISSION_OBJECTIVE_STATE_INPROGRESS");
        c.Advance(20);
        c.Log.MissionObjective(c.Now, mission, "pickup_crate_0", "MISSION_OBJECTIVE_STATE_COMPLETED");
        c.Advance(5);
        c.Log.MissionObjective(c.Now, mission, "deliver_crate_0", "MISSION_OBJECTIVE_STATE_COMPLETED");
        c.Advance(5);
        c.Notify("Received Blueprint: Omnisky IX");
    }

    private static void Combat(ScenarioContext c)
    {
        c.Log.ActorDeath(c.Now, "AI_CRIM_Gunner_Medium_02", c.Handle,
            "behr_lmg_ballistic_01", "Bullet", "Stanton_Yela", "41001", "41002");
        c.Advance(5);
        c.Log.VehicleDestruction(c.Now, "DRAK_Cutlass_Black", "AI_CRIM_Gunner_Medium_02",
            c.Handle, 0, 2, "Combat", "7100001");
        c.Advance(5);
        c.Log.ActorDeath(c.Now, c.Handle, "AI_CRIM_Gunner_Medium_02",
            "behr_lmg_ballistic_01", "Bullet", "Stanton_Yela", "41003", "41004");
        c.Advance(10);
    }

    private sealed class ScenarioContext
    {
        private const string SessionId = "01234567-89ab-cdef-0123-456789abcdef";
        private int _notificationId = 400;
        private int _flightId;

        public ScenarioContext(LogWriter log, DateTimeOffset start, string handle, string geid)
        {
            Log = log;
            Now = start;
            Handle = handle;
            Geid = geid;
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
            Log.Context(Now, "SC_Frontend", SessionId);
            Log.LoadingScreen(Now.AddSeconds(1), "Frontend_Main", "SC_Frontend", 3.44);
            Advance(10);
            Log.Context(Now, "SC_Default", SessionId);
            Log.LoadingScreen(Now.AddSeconds(1), "PU_Megamap", "SC_Default", 21.30);
            Advance(25);
            Log.Spawned(Now);
            Location("RR_MIC_LEO");
        }

        public void End()
        {
            Log.Disconnect(Now, "Remote Disconnect - Player requested disconnect", "SC_Default");
            Advance(2);
            Log.Context(Now, "SC_Frontend", SessionId);
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

        public void Flight(string routeDestination, string arrival, string origin)
        {
            var suffix = (++_flightId).ToString("D3");
            var entity = $"700000000{suffix}";
            var vehicle = $"MISC_Freelancer_MAX_{entity}";

            Log.QuantumTarget(Now, vehicle, entity, routeDestination);
            Log.RouteWithOrigin(Now.AddSeconds(1), vehicle, entity, origin, routeDestination);
            Advance(60);
            Log.VehicleRelease(Now, Geid, vehicle, entity);
            Advance(5);
            Location(arrival);
        }
    }
}
