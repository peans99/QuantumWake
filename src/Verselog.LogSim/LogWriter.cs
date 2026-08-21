using System.Globalization;
using System.Text;

namespace Verselog.LogSim;

/// <summary>
/// Emits lines in Star Citizen's exact Game.log format.
/// </summary>
/// <remarks>
/// <para>
/// Every template here is copied from real log lines captured on a 4.9.188.23497
/// install and documented in <c>docs/log-format-reference.md</c>. That fidelity
/// is the whole point: a generator that emits tidy, well-behaved lines would
/// prove nothing, because the parser's hard cases are all quirks.
/// </para>
/// <para>
/// The awkward shapes are reproduced deliberately:
/// notifications fire three to five times with differing <c>Action:</c> values,
/// some entries split across physical lines with the continuation carrying its
/// own timestamp, <c>[SPAM nnn]</c> duplicates shadow real lines, and route
/// calculation uses both of its two forms.
/// </para>
/// </remarks>
public sealed class LogWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public LogWriter(string path, bool append = false)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var stream = new FileStream(
            path,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            // Match the game: readers must be able to follow while we write.
            FileShare.ReadWrite | FileShare.Delete);

        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    /// <summary>Writes a raw line with a timestamp envelope.</summary>
    public void Line(DateTimeOffset at, string body) =>
        _writer.WriteLine($"<{Stamp(at)}> {body}");

    /// <summary>Writes a line with no envelope, for continuation fragments.</summary>
    public void Raw(string line) => _writer.WriteLine(line);

    private static string Stamp(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    // ---------------- session header ----------------

    public void Header(DateTimeOffset at, string build, string version)
    {
        Line(at, $"BackupNameAttachment=\" Build({build}) {at:dd MMM yy} ({at:HH mm ss})\"  -- used by backup system");
        Line(at, $"Log started on {at.UtcDateTime:ddd MMM dd HH:mm:ss yyyy}");
        Line(at, "Built on Jul 29 2026 15:21:13");
        Line(at, "Running 64 bit version");
        Line(at, @"Executable: C:\Program Files\Roberts Space Industries\StarCitizen\LIVE\Bin64\StarCitizen.exe");
        Line(at, $"FileVersion: {version}");
        Line(at, $"ProductVersion: {version}");
        Line(at, "Using Microsoft (tm) C++ Standard Library implementation");
        Line(at, "Host CPU: AMD Ryzen 7 9800X3D 8-Core Processor");
        Line(at, "Logical CPU Count: 16");
        Line(at, "[Trace] Environment:   PUB");
    }

    public void Login(DateTimeOffset at, string handle) =>
        Line(at, $"[Notice] <Legacy login response> [CIG-net] User Login Success - " +
                 $"Handle[{handle}] - Time[177332566] [Team_GameServices][Login]");

    public void Character(DateTimeOffset at, string handle, string geid) =>
        Line(at, $"[Notice] <AccountLoginCharacterStatus_Character> Character: " +
                 $"createdAt 1784476187540 - updatedAt 1786844282957 - geid {geid} - " +
                 $"accountId 51915 - name {handle} - state STATE_CURRENT [Team_GameServices][Login]");

    public void Context(DateTimeOffset at, string gameRules, string sessionId) =>
        Line(at, $"[Notice] <Context Establisher Done> establisher=\"Game\" runningTime=1.980013 " +
                 $"map=\"megamap\" gamerules=\"{gameRules}\" sessionId=\"{sessionId}\" " +
                 $"[Team_Network][Network][Replication][Loading][Persistence]");

    /// <summary>Loading screens carry no severity tag at all - a real parsing trap.</summary>
    public void LoadingScreen(DateTimeOffset at, string screen, string gameRules, double seconds) =>
        Line(at, $"Loading screen for {screen} : {gameRules} closed after " +
                 $"{seconds.ToString("F2", CultureInfo.InvariantCulture)} seconds");

    public void Spawned(DateTimeOffset at) =>
        Line(at, "[CSessionManager::OnClientSpawned] Spawned!");

    // ---------------- gameplay ----------------

    public void LocationInventory(DateTimeOffset at, string handle, string locationId) =>
        Line(at, $"[Notice] <RequestLocationInventory> Player[{handle}] requested inventory " +
                 $"for Location[{locationId}] [Team_CoreGameplayFeatures][Inventory]");

    /// <summary>The "no inventory here" variant, which is expected and not a failure.</summary>
    public void LocationNoInventory(DateTimeOffset at, string handle) =>
        Line(at, $"[Notice] <RequestLocationInventory> Player[{handle}] requested " +
                 $"Location[INVALID_LOCATION_ID] doesn't have inventory. [Team_CoreGameplayFeatures][Inventory]");

    /// <summary>A duplicate shadowed by a [SPAM nnn] tag; parsers must not double-count.</summary>
    public void SpamDuplicate(DateTimeOffset at, string handle, string locationId) =>
        Line(at, $"[SPAM 299][Notice] <RequestLocationInventory> Player[{handle}] requested " +
                 $"inventory for Location[{locationId}] [Team_CoreGameplayFeatures][Inventory]");

    public void VehicleRelease(DateTimeOffset at, string geid, string vehicleId, string entityId) =>
        Line(at, $"[Notice] <Vehicle Control Flow> CVehicleMovementBase::ClearDriver: " +
                 $"Local client node [{geid}] releasing control token for '{vehicleId}' " +
                 $"[{entityId}] [Team_CGP4][Vehicle]");

    /// <summary>Route form one: names both endpoints.</summary>
    public void RouteWithOrigin(DateTimeOffset at, string vehicleId, string entityId, string origin, string destination) =>
        Line(at, $"[Notice] <Calculate Route> [ItemNavigation][CL][35872] | NOT AUTH | " +
                 $"{vehicleId}[{entityId}]|CSCItemNavigation::CalculateRoute|" +
                 $"Projected Start Location is {origin} for route to destination {destination} " +
                 $"[Team_CGP4][QuantumTravel]");

    /// <summary>Route form two: destination only. Half of all routes use this shape.</summary>
    public void RouteDestinationOnly(DateTimeOffset at, string vehicleId, string entityId, string destination) =>
        Line(at, $"[Notice] <Calculate Route> [ItemNavigation][CL][35872] | NOT AUTH | " +
                 $"{vehicleId}[{entityId}]|CSCItemNavigation::CalculateRoute|" +
                 $"Successfully calculated route to {destination} [Team_CGP4][QuantumTravel]");

    public void QuantumTarget(DateTimeOffset at, string vehicleId, string entityId, string destination) =>
        Line(at, $"[Notice] <Player Selected Quantum Target - Local> [ItemNavigation][CL][35872] | " +
                 $"NOT AUTH | {vehicleId}[{entityId}]|CSCItemNavigation::OnPlayerSelectedQuantumTarget|" +
                 $"Player has selected point {destination} as their destination, routing locally " +
                 $"[Team_CGP4][QuantumTravel]");

    public void ContractMarker(DateTimeOffset at, string missionId, string generator, string contract, string definitionId) =>
        Line(at, $"[Notice] <SMarkerHandler_Base::CreateMissionObjectiveMarker> Creating objective marker: " +
                 $"missionId [{missionId}], generator name [{generator}], " +
                 $"contract [{contract}][{Guid.NewGuid()}], contractDefinitionId[{definitionId}] " +
                 $"[Team_Missions]");

    /// <summary>
    /// A notification and its follow-up Action lines. The repeats are the point:
    /// counting them naively inflates every statistic three to five times.
    /// </summary>
    public void Notification(DateTimeOffset at, string text, int id, string? missionId = null)
    {
        var mission = missionId ?? "00000000-0000-0000-0000-000000000000";

        Line(at, $"[Notice] <SHUDEvent_OnNotification> Added notification \"{text}\" [{id}] to queue. " +
                 $"New queue size: 1, MissionId: [{mission}], ObjectiveId: [] " +
                 $"[Team_CoreGameplayFeatures][Missions][Comms]");

        foreach (var (action, offset) in new[] { ("Next", 40), ("StartFade", 9000), ("Remove", 9400) })
        {
            Line(at.AddMilliseconds(offset),
                $"[Notice] <UpdateNotificationItem> Notification \"{text}\" [{id}], Action: {action} " +
                $"[Team_CoreGameplayFeatures][Missions][Comms]");
        }
    }

    /// <summary>
    /// A notification whose text contains a newline. The continuation carries its
    /// own identical timestamp, so nothing about the prefix distinguishes it from
    /// a new entry - only the unbalanced quote does.
    /// </summary>
    public void SplitNotification(DateTimeOffset at, string firstHalf, string secondHalf, int id)
    {
        Line(at, $"[Notice] <SHUDEvent_OnNotification> Added notification \"{firstHalf}");
        Line(at, $"{secondHalf}\" [{id}] to queue. New queue size: 1, " +
                 $"MissionId: [00000000-0000-0000-0000-000000000000], ObjectiveId: [] " +
                 $"[Team_CoreGameplayFeatures][Missions][Comms]");
    }

    public void Incapacitated(DateTimeOffset at, int id) =>
        Notification(at,
            "Incapacitated: While incapacitated, ask others in your party, in chat, or through " +
            "rescue service beacons to revive you before the 'Time to Death' timer expires.",
            id);

    public void Disconnect(DateTimeOffset at, string reason, string gameRules) =>
        Line(at, $"[Notice] <Channel Disconnected> cause=30010 reason=\"{reason}\" frame=10136 " +
                 $"isRemote=0 viewState=eCVS_InGame map=\"megamap\" gamerules=\"{gameRules}\" " +
                 $"hostType=\"Replicant\" remoteAddr=<local>:12300 localAddr=<local>:16");

    // ---------------- dormant combat ----------------

    /// <summary>
    /// Emits the archived <c>&lt;Actor Death&gt;</c> format. Star Citizen 4.9 does
    /// not produce this any more; the simulator can, which is the only way to
    /// exercise the dormant combat parser end to end.
    /// </summary>
    public void ActorDeath(
        DateTimeOffset at, string victim, string killer, string weapon, string damageType, string zone) =>
        Line(at, $"[Notice] <Actor Death> CActor::Kill: '{victim}' [{Random.Shared.Next(10000, 99999)}] " +
                 $"in zone '{zone}' killed by '{killer}' [{Random.Shared.Next(10000, 99999)}] " +
                 $"using '{weapon}' [Class {weapon}] with damage type '{damageType}' " +
                 $"from direction x: 0.512, y: -0.234, z: 0.100 [Team_ActorFeatures][Actor]");

    public void VehicleDestruction(
        DateTimeOffset at, string vehicle, string driver, string attacker, int from, int to, string cause) =>
        Line(at, $"[Notice] <Vehicle Destruction> CVehicle::OnAdvanceDestroyLevel: Vehicle '{vehicle}' " +
                 $"[{Random.Shared.Next(1000000, 9999999)}] in zone 'Stanton_Yela' " +
                 $"[pos x: 1.0, y: 2.0, z: 3.0 vel x: 0.0, y: 0.0, z: 0.0] " +
                 $"driven by '{driver}' [999] advanced from destroy level {from} to {to} " +
                 $"caused by '{attacker}' [888] with '{cause}' [Team_VehicleFeatures][Vehicle]");

    // ---------------- filler ----------------

    /// <summary>
    /// Background chatter. Real logs are over 95% noise, so including it keeps
    /// the generated file representative of what the parser actually faces.
    /// </summary>
    public void Noise(DateTimeOffset at, int index)
    {
        switch (index % 5)
        {
            case 0:
                Line(at, "[Notice] <InvalidateAllTerrainCells> Invalidating all terrain cells [Team_Graphics]");
                break;
            case 1:
                Line(at, $"[Notice] <CSCLoadingPlatformManager::LoadEntitiesReference> Loading entities {index}");
                break;
            case 2:
                Line(at, "[Notice] <Local Route Guard - Server Rerouted> [CL][35872] | NULL ENTITY|" +
                         "CSCItemNavigation::PostInitialize::<lambda_1>::operator ()|FinalStop=0 [Team_CGP4][QuantumTravel]");
                break;
            case 3:
                Line(at, "[Error] <Actor Physics> CSCActorPhysicsController::Physicalize: " +
                         $"Failed to physicalize 'ui_entity_000{index % 10} (comms_user)' [23316] [Team_ActorFeatures][Actor]");
                break;
            default:
                Line(at, $"[Notice] <UpdateNotificationItem> Notification \"System status {index}\" [{900 + index % 90}], Action: Next");
                break;
        }
    }

    public void Dispose() => _writer.Dispose();
}
