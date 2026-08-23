namespace Quantumwake.LogSim;

/// <summary>Universe fixtures the simulator draws from, all seen in real logs.</summary>
internal static class Fixtures
{
    public static readonly (string Id, string Name)[] Locations =
    [
        ("RR_MIC_LEO", "microTech LEO"),
        ("RR_MIC_L1", "microTech L1"),
        ("RR_CRU_LEO", "Crusader LEO"),
        ("Stanton4_NewBabbage", "New Babbage"),
        ("Stanton1_Lorville", "Lorville"),
        ("Stanton2_Orison", "Orison"),
        ("Stanton3_Area18", "Area18"),
        ("Stanton4b_RayariHydro_Cantwell", "Rayari Cantwell"),
        ("Stanton3b_ArcCorp_Area061", "Area 061"),
        ("Stanton4_DistributionCentre_Covalex_S4DC05", "Covalex DC"),
        ("Pyro2_Outpost_col_m_scrp_indy_001", "Monox scrapyard"),
        ("RR_JP_StantonPyro", "Stanton-Pyro jump"),
        ("RR_P5_L2", "Pyro V L2"),
        ("GrimHEX", "GrimHEX"),
        ("Stanton4_Shubin_SM0_22", "Shubin SM0-22")
    ];

    /// <summary>
    /// Cargo the simulated pilot moves, as the ids the game logs.
    /// </summary>
    /// <remarks>
    /// Real resource ids from the community digest, so a simulated install
    /// exercises the name lookup rather than dodging it - the whole point of
    /// the cargo map is that a receipt can be tied to a named commodity. The
    /// base price, combined with a steady per-place multiplier, is what makes
    /// one terminal genuinely the best place to sell a given commodity.
    /// </remarks>
    public static readonly (string Resource, int BasePrice)[] Commodities =
    [
        ("bde5a2c8-2ef4-46ac-9403-2fcb79e4016c", 1540),  // Quantainium
        ("7f4599b0-a2b2-4178-8c7e-13292054ab20", 452),   // Laranite
        ("dc6fbcbb-5990-4ed5-82ee-93152dab7845", 268),   // Agricium
        ("accacd33-3a1a-4ec7-8b4a-14b9f028047c", 88)     // Processed Food
    ];

    public static readonly string[] Destinations =
    [
        "Stanton4_NewBabbage", "OOC_Stanton_4_Microtech", "LOC_rs_ext_stan-pyro_jp1",
        "ObjectContainer_RestStop", "LOC_RR_S4_L1", "Area18_City_objectContainer",
        "rs_ext_cru-leo1", "NavPoint_Dynamic_759722455016", "OOC_Stanton_2_Crusader",
        "ObjectContainer_Lorville_City", "rs_ext_pyro3_l3"
    ];

    public static readonly string[] Origins =
    [
        "Port Tressler", "New Babbage", "Seraphim Station", "Area18", "Gaslight", "Everus Harbor"
    ];

    public static readonly (string Prefix, string Model)[] Ships =
    [
        ("MISC", "Starlancer_Max"),
        ("ANVL", "Hornet_F7CM_Mk2"),
        ("DRAK", "Corsair"),
        ("RSI", "Hermes"),
        ("MISC", "Freelancer_MAX"),
        ("RSI", "Aurora_Mk2"),
        ("DRAK", "Cutter"),
        ("ORIG", "325a"),
        ("DRAK", "Clipper")
    ];

    public static readonly (string Generator, string Contract)[] Contracts =
    [
        ("Covalex_RecoverCargo", "Covalex_Stanton_VeryHard_RecoverCargo"),
        ("Ling_RecoverCargo", "Ling_Stanton_VeryEasy_RecoverCargo"),
        ("RedWind_RecoverCargo", "RedWind_Stanton_Easy_RecoverCargo"),
        ("FTL_Courier", "FTL_Courier_Stanton_AmmoCrate_Rank0_2"),
        ("EchhartSecurity", "EchhartSecurity_Stanton_VeryEasy_RecoverCargo"),
        ("HaulCargo", "HaulCargo_AToB_Interstellar_Bulk_DistSp_Dia_FresFoo_Gol_Aphor")
    ];

    public static readonly string[] ContractTitles =
    [
        "Bulk Covalex Shipment Needs Recovering",
        "Small Covalex Shipment Needs Recovering",
        "Cargo Retrieval Required",
        "Urgent Delivery Contract"
    ];

    public static readonly string[] Npcs =
    [
        "PU_Pilots-Human-NPC_Pilot_Criminal_Gunner_Light_01",
        "AI_CRIM_Gunner_Medium_02",
        "PU_Pilots-Human-NPC_Pilot_Pirate_Heavy_03",
        "Kopion_Ranger_01"
    ];

    public static readonly string[] Weapons =
    [
        "behr_lmg_ballistic_01", "klwe_laser_repeater_s3", "apar_special_ballistic_gatling_s4",
        "gemi_ballistic_cannon_s5"
    ];
}

/// <summary>
/// Generates one plausible play session as a sequence of log events.
/// </summary>
/// <remarks>
/// The session follows a believable arc - menu, spawn, travel between locations
/// by quantum, ship swaps, contracts, the occasional incapacitation and
/// disconnect - so the dashboard has a coherent story to render rather than
/// random noise.
/// </remarks>
internal sealed class Simulation
{
    private readonly LogWriter _log;
    private readonly Random _random;
    private readonly SimOptions _options;

    private DateTimeOffset _now;
    private int _notificationId = 30;
    private int _noiseCounter;
    private string _currentLocation = "RR_MIC_LEO";

    public Simulation(LogWriter log, SimOptions options, DateTimeOffset start, int seed)
    {
        _log = log;
        _options = options;
        _random = new Random(seed);
        _now = start;
    }

    /// <summary>Real time elapsed so far, for live pacing.</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Called after each beat so live mode can sleep proportionally.</summary>
    public event Action<TimeSpan>? Beat;

    private void Advance(int minSeconds, int maxSeconds)
    {
        var span = TimeSpan.FromSeconds(_random.Next(minSeconds, maxSeconds + 1));
        _now += span;
        Elapsed += span;
        Beat?.Invoke(span);
    }

    private void Noise(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _log.Noise(_now.AddMilliseconds(i * 37), _noiseCounter++);
        }
    }

    private T Pick<T>(T[] items) => items[_random.Next(items.Length)];

    /// <summary>Writes a complete session.</summary>
    public void Run()
    {
        var sessionId = Guid.NewGuid().ToString();

        _log.Header(_now, "12344265", "4.9.188.23497");
        Advance(1, 3);

        _log.Character(_now, _options.Handle, _options.Geid);
        _log.Login(_now.AddMilliseconds(120), _options.Handle);
        Advance(1, 2);

        // Menus first.
        _log.Context(_now, "SC_Frontend", sessionId);
        _log.LoadingScreen(_now.AddSeconds(1), "Frontend_Main", "SC_Frontend", 3.44);
        Noise(6);
        Advance(_options.MenuSeconds / 2, _options.MenuSeconds);

        // Into the persistent universe.
        _log.Context(_now, "SC_Default", sessionId);
        _log.LoadingScreen(_now.AddSeconds(2), "PU_Megamap", "SC_Default", 21.3);
        Advance(20, 30);
        _log.Spawned(_now);

        VisitLocation(_currentLocation);

        // A chat notification that splits across lines - the multi-line quirk.
        Advance(5, 15);
        _log.SplitNotification(
            _now,
            $"You have joined channel 'Origin 325a : {_options.Handle}'.",
            ": ",
            _notificationId++);

        for (var leg = 0; leg < _options.Legs; leg++)
            RunLeg(leg);

        // Wind down.
        Advance(10, 30);
        _log.Disconnect(_now, "Remote Disconnect - Player requested disconnect", "SC_Default");
        _log.Context(_now.AddSeconds(2), "SC_Frontend", sessionId);
        _log.LoadingScreen(_now.AddSeconds(3), "Frontend_Main", "SC_Frontend", 2.10);
        Noise(4);
        Advance(5, 20);
        _log.Disconnect(_now, "Nub destroyed", "SC_Frontend");
    }

    /// <summary>One trip: pick a ship, take a contract, fly somewhere, land.</summary>
    private void RunLeg(int leg)
    {
        var (prefix, model) = Pick(Fixtures.Ships);
        var entityId = _random.NextInt64(700000000000, 799999999999).ToString();
        var vehicleId = $"{prefix}_{model}_{entityId}";

        // Contract, sometimes.
        if (_random.NextDouble() < 0.6)
        {
            Advance(20, 90);
            var (generator, contract) = Pick(Fixtures.Contracts);
            var missionId = Guid.NewGuid().ToString();

            _log.ContractMarker(_now, missionId, generator, contract, Guid.NewGuid().ToString());
            _log.Notification(
                _now.AddSeconds(1),
                $"Contract Accepted:  {Pick(Fixtures.ContractTitles)}: ",
                _notificationId++,
                missionId);
        }

        Noise(_random.Next(4, 12));

        // Quantum travel to somewhere new.
        Advance(30, 180);
        var destination = Pick(Fixtures.Destinations);

        _log.QuantumTarget(_now, vehicleId, entityId, destination);

        // Alternate the two route forms so both parser paths are exercised.
        if (leg % 2 == 0)
            _log.RouteWithOrigin(_now.AddSeconds(2), vehicleId, entityId, Pick(Fixtures.Origins), destination);
        else
            _log.RouteDestinationOnly(_now.AddSeconds(2), vehicleId, entityId, destination);

        Noise(_random.Next(3, 9));
        Advance(_options.FlightSeconds / 2, _options.FlightSeconds);

        // Combat, when enabled. Absent from real 4.9 logs by default.
        if (_options.Combat && _random.NextDouble() < 0.5)
            RunCombat();

        // Occasionally go down.
        if (_random.NextDouble() < _options.IncapacitationChance)
        {
            _log.Incapacitated(_now, _notificationId++);
            Advance(20, 60);
        }

        // Land and leave the ship.
        _log.VehicleRelease(_now, _options.Geid, vehicleId, entityId);
        Advance(5, 20);

        VisitLocation(Pick(Fixtures.Locations).Id);

        // Cargo, at about half the stops. Buying and selling both happen, so
        // the map has two sides of the counter to shade.
        if (_random.NextDouble() < 0.55)
        {
            Advance(30, 240);
            RunTrade();
        }
    }

    /// <summary>One kiosk trade at wherever the pilot is standing.</summary>
    /// <remarks>
    /// The log never says where a trade happened - every cargo terminal reports
    /// the same shop id - so this deliberately writes no location of its own.
    /// Recovering the place from the last arrival is the app's job, and leaving
    /// the line bare is what keeps that path honest.
    /// </remarks>
    private void RunTrade()
    {
        var (resource, basePrice) = Pick(Fixtures.Commodities);
        var selling = _random.NextDouble() < 0.65;

        // Whole boxes, as the kiosk deals in.
        var quantity = _random.Next(1, 21) * 16;

        // Buying costs less per SCU than selling pays, or there would be no
        // trade to plan; the jitter stops every visit reading the same price.
        var unit = basePrice
            * PriceFactor(_currentLocation, resource)
            * (selling ? 1.0 : 0.78)
            * (0.96 + _random.NextDouble() * 0.08);

        _log.CommodityTrade(
            _now,
            _options.Geid,
            Math.Round((decimal)(unit * quantity), 2),
            quantity,
            resource,
            selling,
            _random.NextDouble() < 0.5 ? "Location" : "ResourceContainer");

        Noise(_random.Next(2, 6));
    }

    /// <summary>
    /// How good one place is for one commodity, steady across sessions.
    /// </summary>
    /// <remarks>
    /// Rolled from the ids rather than stored, so it survives a reseed: a map
    /// whose best terminal moved on every run would be untestable.
    /// </remarks>
    private static double PriceFactor(string place, string resource)
    {
        var hash = 17u;

        foreach (var c in $"{place}|{resource}")
            hash = unchecked(hash * 31 + c);

        return 0.78 + hash % 45 / 100.0;
    }

    private void RunCombat()
    {
        var kills = _random.Next(1, 4);

        for (var i = 0; i < kills; i++)
        {
            Advance(10, 60);

            if (_random.NextDouble() < 0.75)
            {
                // The player scores a kill.
                _log.ActorDeath(_now, Pick(Fixtures.Npcs), _options.Handle,
                    Pick(Fixtures.Weapons), _random.NextDouble() < 0.5 ? "Bullet" : "Combat", "Stanton_Yela");

                if (_random.NextDouble() < 0.5)
                {
                    _log.VehicleDestruction(_now.AddMilliseconds(150),
                        $"{Pick(Fixtures.Ships).Prefix}_Paladin_6763231335005",
                        Pick(Fixtures.Npcs), _options.Handle, 0, _random.Next(1, 3), "Combat");
                }
            }
            else
            {
                // The player is on the receiving end.
                _log.ActorDeath(_now, _options.Handle, Pick(Fixtures.Npcs),
                    Pick(Fixtures.Weapons), "Bullet", "Stanton_Yela");
            }
        }
    }

    private void VisitLocation(string locationId)
    {
        _currentLocation = locationId;
        _log.LocationInventory(_now, _options.Handle, locationId);

        // Real logs repeat these constantly, and shadow some with SPAM tags.
        _log.LocationInventory(_now.AddSeconds(3), _options.Handle, locationId);
        _log.SpamDuplicate(_now.AddSeconds(4), _options.Handle, locationId);

        if (_random.NextDouble() < 0.15)
            _log.LocationNoInventory(_now.AddSeconds(5), _options.Handle);

        Noise(_random.Next(3, 8));
    }
}

/// <summary>Knobs for a generated session.</summary>
internal sealed record SimOptions
{
    public string Handle { get; init; } = "testpilot";
    public string Geid { get; init; } = "204721322607";

    /// <summary>Trips per session.</summary>
    public int Legs { get; init; } = 6;

    public int MenuSeconds { get; init; } = 300;
    public int FlightSeconds { get; init; } = 600;
    public double IncapacitationChance { get; init; } = 0.2;

    /// <summary>
    /// Emit combat events. Off by default because SC 4.9 does not produce them;
    /// turning it on is the only way to see the dormant parser light up.
    /// </summary>
    public bool Combat { get; init; }
}
