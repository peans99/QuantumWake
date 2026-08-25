namespace Quantumwake.LogSim;

/// <summary>One isolated fake install participating in a coordinated scenario.</summary>
public sealed record MultiClientPilot(
    string Key,
    string Handle,
    string Role,
    string Geid,
    int SuggestedPort);

/// <summary>One observable checkpoint in a coordinated multi-client story.</summary>
public sealed record MultiClientStageDefinition(
    int Number,
    string Name,
    string Description,
    IReadOnlyList<string> ExpectedFacts);

/// <summary>A deterministic story spread across several fake game clients.</summary>
public sealed record MultiClientScenarioDefinition(
    string Name,
    string Description,
    IReadOnlyList<MultiClientPilot> Pilots,
    IReadOnlyList<MultiClientStageDefinition> Stages);

/// <summary>Scenarios intended to exercise several dashboards and the org network together.</summary>
public static class MultiClientScenarioCatalogue
{
    public static IReadOnlyList<MultiClientScenarioDefinition> All { get; } =
    [
        new(
            "org-activity",
            "Three pilots form a crew, trade, fight, recover, finish a contract, and stand down.",
            [
                new("captain", "D-Rud", "captain and contract lead", "204721322601", 31401),
                new("trader", "astro_ice", "cargo and equipment", "204721322602", 31402),
                new("medic", "Patchwork", "medical and inventory", "204721322603", 31403),
            ],
            [
                new(1, "Crew wakes",
                    "All three clients enter the universe at Port Tressler and report their fleet and loadout.",
                    ["3 active sessions", "3 fleet observations", "loadouts on every client"]),
                new(2, "Party forms",
                    "Each client sees the other pilots connect and D-Rud become party leader.",
                    ["party activity on all 3 clients", "D-Rud is the visible leader"]),
                new(3, "Prepare the run",
                    "D-Rud accepts a two-step contract while astro_ice buys cargo and Patchwork checks supplies.",
                    ["1 active contract", "32 SCU bought", "confirmed equipment spend", "2 stash locations"]),
                new(4, "Launch and jump",
                    "The crew retrieves ships and makes the same quantum trip to microTech L1.",
                    ["3 ship sorties", "3 quantum jumps", "all clients arrive at microTech L1"]),
                new(5, "Contact",
                    "D-Rud wins a firefight, a hostile ship is destroyed, and Patchwork becomes a casualty.",
                    ["1 player kill", "1 destroyed vehicle", "1 incapacitation", "1 death"]),
                new(6, "Recover and trade",
                    "Patchwork wakes in New Babbage while astro_ice sells the cargo at microTech L1.",
                    ["1 inferred respawn", "1 after-death medical visit", "32 SCU sold"]),
                new(7, "Finish the work",
                    "D-Rud completes both objectives and receives a blueprint; Patchwork later records an ordinary heal.",
                    ["1 completed contract", "2 of 2 steps complete", "1 blueprint", "heal separated from casualty care"]),
                new(8, "Stand down",
                    "The party separates, a remote timeout remains visible, and all three sessions end normally.",
                    ["party departures", "1 remote timeout", "3 completed sessions"]),
            ])
    ];

    public static MultiClientScenarioDefinition? Find(string name) =>
        All.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Writes a coordinated scenario one complete visual checkpoint at a time.</summary>
public static class MultiClientScenarioRunner
{
    private const string Mission = "31111111-2222-3333-4444-555555555555";
    private const string Cargo = "7f4599b0-a2b2-4178-8c7e-13292054ab20";

    public static void Run(
        MultiClientScenarioDefinition scenario,
        IReadOnlyDictionary<string, LogWriter> logs,
        DateTimeOffset start,
        Action<MultiClientStageDefinition>? beforeStage = null,
        Action<MultiClientStageDefinition>? afterStage = null)
    {
        if (!scenario.Name.Equals("org-activity", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(scenario), scenario.Name, "Unknown multi-client scenario.");

        var missing = scenario.Pilots.Where(p => !logs.ContainsKey(p.Key)).Select(p => p.Key).ToList();
        if (missing.Count > 0)
            throw new ArgumentException($"Missing log writers for: {string.Join(", ", missing)}", nameof(logs));

        var clients = scenario.Pilots
            .Select((pilot, index) => new
            {
                pilot.Key,
                Context = new ScenarioContext(
                    logs[pilot.Key],
                    start,
                    pilot.Handle,
                    pilot.Geid,
                    $"01234567-89ab-cdef-0123-456789abc{index + 1:x}",
                    400 + index * 100)
            })
            .ToDictionary(x => x.Key, x => x.Context, StringComparer.OrdinalIgnoreCase);

        foreach (var stage in scenario.Stages)
        {
            beforeStage?.Invoke(stage);
            RunStage(stage.Number, clients);
            afterStage?.Invoke(stage);
        }
    }

    private static void RunStage(int stage, IReadOnlyDictionary<string, ScenarioContext> clients)
    {
        var captain = clients["captain"];
        var trader = clients["trader"];
        var medic = clients["medic"];

        switch (stage)
        {
            case 1:
                foreach (var client in clients.Values)
                    client.Begin();

                captain.Log.FleetQuery(captain.Now, 8, 9);
                captain.Log.Attachment(captain.Now.AddSeconds(1), captain.Handle,
                    "rsi_odyssey_undersuit_01_01_01", "210000000001", "Armor_Undersuit");
                captain.Log.Attachment(captain.Now.AddSeconds(2), captain.Handle,
                    "behr_rifle_ballistic_01", "210000000002", "weapon_attach_hand_right");

                trader.Log.FleetQuery(trader.Now, 13, 14);
                trader.Log.Attachment(trader.Now.AddSeconds(1), trader.Handle,
                    "rsi_odyssey_undersuit_01_01_01", "220000000001", "Armor_Undersuit");

                medic.Log.FleetQuery(medic.Now, 5, 6);
                medic.Log.Attachment(medic.Now.AddSeconds(1), medic.Handle,
                    "rsi_odyssey_undersuit_01_01_01", "230000000001", "Armor_Undersuit");
                medic.Log.Attachment(medic.Now.AddSeconds(2), medic.Handle,
                    "behr_pistol_ballistic_01", "230000000002", "weapon_attach_hand_right");

                foreach (var client in clients.Values)
                    client.Advance(10);
                break;

            case 2:
                captain.Notify("Party astro_ice connected.:");
                captain.Notify("Party Patchwork connected.:");
                captain.Notify("New Party Leader D-Rud is now party leader.:");

                trader.Notify("Party D-Rud connected.:");
                trader.Notify("Party Patchwork connected.:");
                trader.Notify("New Party Leader D-Rud is now party leader.:");

                medic.Notify("Party D-Rud connected.:");
                medic.Notify("Party astro_ice connected.:");
                medic.Notify("New Party Leader D-Rud is now party leader.:");
                break;

            case 3:
                captain.Log.ContractMarker(captain.Now, Mission, "Covalex_RecoverCargo",
                    "Covalex_Stanton_VeryHard_RecoverCargo",
                    "caaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                    "79999999-8888-7777-6666-555555555555");
                captain.Advance(1);
                captain.Notify("Contract Accepted: Bulk Covalex Shipment Needs Recovering:", Mission);
                captain.Log.MissionObjective(captain.Now, Mission, "pickup_crate_0", "MISSION_OBJECTIVE_STATE_INPROGRESS");
                captain.Advance(10);

                trader.Log.CommodityTrade(trader.Now, trader.Geid, 18_400m, 32, Cargo, false, "Location");
                trader.Advance(2);
                trader.Log.ShopRequest(trader.Now, trader.Geid, "SCShop_CenterMass_NewBabbage",
                    "752023944375", "752023944372", 2_400m, "behr_rifle_ballistic_01", 1);
                trader.Advance(2);
                trader.Log.ShopResponse(trader.Now, "SCShop_CenterMass_NewBabbage", "752023944372", "Success");
                trader.Advance(10);

                medic.Log.InventoryQuery(medic.Now, medic.Geid, "Location", "3531251586");
                medic.Log.InventoryItem(medic.Now.AddSeconds(1), medic.Geid, "Location", "3531251586", "medpen_hemozal");
                medic.Log.InventoryItem(medic.Now.AddSeconds(2), medic.Geid, "Player", medic.Geid, "medpen_adrenaline");
                medic.Advance(5);
                medic.Location("Stanton4_NewBabbage");
                medic.Log.InventoryQuery(medic.Now, medic.Geid, "Location", "3531251587");
                medic.Log.InventoryItem(medic.Now.AddSeconds(1), medic.Geid, "Location", "3531251587", "behr_pistol_ballistic_01");
                medic.Advance(10);
                break;

            case 4:
                Retrieve(captain, "700000008001", "AEGS_Redeemer_700000008001");
                Retrieve(trader, "700000008002", "MISC_Freelancer_MAX_700000008002");
                Retrieve(medic, "700000008003", "ANVL_C8R_Pisces_700000008003");

                captain.Flight("LOC_RR_S4_L1", "RR_MIC_L1", "Port Tressler",
                    "AEGS_Redeemer_700000008001", "700000008001");
                trader.Flight("LOC_RR_S4_L1", "RR_MIC_L1", "Port Tressler",
                    "MISC_Freelancer_MAX_700000008002", "700000008002");
                medic.Flight("LOC_RR_S4_L1", "RR_MIC_L1", "Port Tressler",
                    "ANVL_C8R_Pisces_700000008003", "700000008003");
                break;

            case 5:
                captain.Log.ActorDeath(captain.Now, "AI_CRIM_Gunner_Medium_02", captain.Handle,
                    "behr_lmg_ballistic_01", "Bullet", "Stanton_Yela", "42001", "42002");
                captain.Advance(5);
                captain.Log.VehicleDestruction(captain.Now, "DRAK_Cutlass_Black", "AI_CRIM_Gunner_Medium_02",
                    captain.Handle, 0, 2, "Combat", "7200001");
                captain.Advance(10);

                medic.Log.Incapacitated(medic.Now, medic.NextNotificationId());
                medic.Advance(10);
                medic.Log.CorpseItem(medic.Now, "behr_pistol_ballistic_01", "Body_ItemPort");
                medic.Log.CorpseItem(medic.Now.AddMilliseconds(100), "medpen_hemozal", "Armor_ItemPort");
                medic.Advance(30);
                break;

            case 6:
                medic.Location("Stanton4_NewBabbage");
                medic.Advance(5);
                medic.Notify("Medical Bed: The bed has restored your health and reset your BDL.");

                trader.Log.CommodityTrade(trader.Now, trader.Geid, 23_680m, 32, Cargo, true, "ResourceContainer");
                trader.Advance(15);
                break;

            case 7:
                captain.Log.MissionObjective(captain.Now, Mission, "pickup_crate_0", "MISSION_OBJECTIVE_STATE_COMPLETED");
                captain.Advance(5);
                captain.Log.MissionObjective(captain.Now, Mission, "deliver_crate_0", "MISSION_OBJECTIVE_STATE_COMPLETED");
                captain.Advance(5);
                captain.Notify("Received Blueprint: Omnisky IX");

                medic.Advance(16 * 60);
                medic.Location("Stanton3_Area18");
                medic.Notify("Medical Bed: The bed has restored your health and reset your BDL.");
                break;

            case 8:
                captain.Notify("Party astro_ice disconnected.:");
                captain.Notify("Party Patchwork disconnected.:");
                captain.Notify("Party Disbanded The party has been disbanded.:");

                trader.Log.Disconnect(trader.Now, "Connection timeout", "SC_Default", remote: true);
                trader.Advance(10);

                foreach (var client in clients.Values)
                    client.End();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown multi-client stage.");
        }
    }

    private static void Retrieve(ScenarioContext client, string entity, string vehicle)
    {
        client.Log.VehicleSpawn(client.Now, entity, "LandingArea_ShipElevator_HangarMediumFront_Rund");
        client.Log.VehicleSpawn(client.Now.AddMilliseconds(200), entity, "LandingArea_ShipElevator_HangarMediumFront_Rund");
        client.Advance(2);
        client.Log.VehicleIdentity(client.Now, vehicle, entity);
        client.Advance(10);
    }
}
