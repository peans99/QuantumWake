using System.Globalization;
using System.Text.RegularExpressions;
using SCCompanion.Core.Events;
using SCCompanion.Core.Logging;

namespace SCCompanion.Core.Parsing;

/// <summary>
/// Turns <see cref="LogLine"/> envelopes into semantic <see cref="GameEvent"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Every pattern here was derived from real log lines on a 4.9.188.23497 install
/// and is documented in <c>docs/log-format-reference.md</c>. Patterns are matched
/// against the <see cref="LogLine.Body"/> after dispatching on
/// <see cref="LogLine.Tag"/>, which keeps the hot path cheap: most lines fail the
/// tag lookup and never touch a regex.
/// </para>
/// <para>
/// This type is stateful and not thread-safe. It expects lines in file order,
/// because session headers span several lines. Use one instance per file or per
/// tailed stream.
/// </para>
/// </remarks>
public sealed partial class LogEventParser
{
    private string? _pendingBuildTag;
    private DateTimeOffset? _pendingSessionStart;

    /// <summary>Entity ids already named, so repeat sightings are ignored.</summary>
    private readonly HashSet<string> _identifiedVehicles = [];

    /// <summary>
    /// The local player's handle, learned from the login line. Used to tell a
    /// kill from a death when combat events are present.
    /// </summary>
    public string? LocalHandle { get; private set; }

    private static double ParseDouble(string value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;

    /// <summary>Per-kind match counts, for the parser-health panel.</summary>
    public Dictionary<string, int> MatchCounts { get; } = [];

    /// <summary>Lines that carried a known tag but failed their pattern.</summary>
    public int UnmatchedKnownTags { get; private set; }

    /// <summary>
    /// Unmatched counts broken down by tag, and one sample body per tag.
    /// This is the diagnostic that turns "1,928 lines failed" into an actionable
    /// list of patterns to fix.
    /// </summary>
    public Dictionary<string, (int Count, string Sample)> UnmatchedByTag { get; } = [];

    private void RecordUnmatched(LogLine line)
    {
        UnmatchedKnownTags++;

        var key = line.Tag ?? line.Severity ?? "(untagged)";
        if (UnmatchedByTag.TryGetValue(key, out var existing))
            UnmatchedByTag[key] = (existing.Count + 1, existing.Sample);
        else
            UnmatchedByTag[key] = (1, Truncate(line.Body));
    }

    private static string Truncate(string value) =>
        value.Length <= 160 ? value : value[..160] + "...";

    /// <summary>
    /// Attempts to extract an event. Returns null for the vast majority of lines,
    /// which is normal and never an error.
    /// </summary>
    /// <param name="line">A parsed envelope.</param>
    /// <param name="includeSpam">
    /// When false (the default) <c>[SPAM nnn]</c> lines are skipped. They duplicate
    /// content that also appears untagged, so counting both double-counts events.
    /// </param>
    public GameEvent? Parse(LogLine line, bool includeSpam = false)
    {
        if (line.IsSpam && !includeSpam)
            return null;

        var ev = Dispatch(line) ?? IdentifyVehicle(line);
        if (ev is not null)
            MatchCounts[ev.Kind] = MatchCounts.GetValueOrDefault(ev.Kind) + 1;

        return ev;
    }

    private GameEvent? Dispatch(LogLine line)
    {
        // Untagged header and loading lines.
        if (line.Tag is null)
            return ParseUntagged(line);

        // The corpse tag embeds the calling method, so it is matched by substring
        // rather than by an exact name.
        if (line.Tag.Contains("CSCActorCorpseUtils", StringComparison.Ordinal))
        {
            return TryMatch(CorpseItemRegex, line, m =>
                new CorpseItemEvent(
                    line.Timestamp,
                    m.Groups["class"].Value,
                    m.Groups["port"].Value));
        }

        return line.Tag switch
        {
            "Legacy login response" => Match(LoginRegex, line, m =>
            {
                LocalHandle ??= m.Groups["handle"].Value;
                return new LoginEvent(line.Timestamp, m.Groups["handle"].Value);
            }),

            "AccountLoginCharacterStatus_Character" => Match(CharacterRegex, line, m =>
                new CharacterEvent(
                    line.Timestamp,
                    m.Groups["name"].Value,
                    m.Groups["geid"].Value,
                    m.Groups["account"].Value,
                    m.Groups["state"].Value)),

            "Context Establisher Done" => Match(ContextRegex, line, m =>
                new ContextEvent(
                    line.Timestamp,
                    m.Groups["establisher"].Value,
                    m.Groups["map"].Value,
                    m.Groups["gamerules"].Value,
                    m.Groups["session"].Value)),

            "Vehicle Control Flow" => Match(VehicleControlRegex, line, m =>
            {
                var vehicleId = m.Groups["vehicle"].Value;
                var (manufacturer, model) = SplitVehicleId(vehicleId);
                var change = m.Groups["method"].Value.Contains("Clear", StringComparison.OrdinalIgnoreCase)
                    ? SeatChange.Left
                    : SeatChange.Entered;

                return new VehicleControlEvent(
                    line.Timestamp, vehicleId, model, manufacturer,
                    m.Groups["entity"].Value, change);
            }),

            "RequestLocationInventory" => ParseLocationInventory(line),

            "Calculate Route" => ParseQuantumRoute(line),

            "Player Selected Quantum Target - Local" => Match(QuantumTargetRegex, line, m =>
                new QuantumTargetEvent(
                    line.Timestamp,
                    ExtractVehicle(line.Body),
                    m.Groups["dest"].Value)),

            "SMarkerHandler_Base::CreateMissionObjectiveMarker" or
            "CLocalMissionPhaseMarker::CreateMarker" => Match(ContractRegex, line, m =>
                new ContractEvent(
                    line.Timestamp,
                    m.Groups["mission"].Value,
                    m.Groups["gen"].Success ? m.Groups["gen"].Value : null,
                    m.Groups["contract"].Value,
                    m.Groups["cdef"].Success ? m.Groups["cdef"].Value : null)),

            "SHUDEvent_OnNotification" => Match(NotificationRegex, line, m =>
                new NotificationEvent(
                    line.Timestamp,
                    m.Groups["text"].Value.Trim(),
                    m.Groups["id"].Value,
                    m.Groups["mission"].Success && m.Groups["mission"].Value.Length > 0
                        ? m.Groups["mission"].Value
                        : null)),

            // Dormant on SC 4.9: these events are no longer emitted. Implemented
            // from the archived format so the feature revives automatically if
            // CIG restores them. See docs/findings.md.
            "Actor Death" => Match(ActorDeathRegex, line, m =>
            {
                var victim = m.Groups["victim"].Value;
                var killer = m.Groups["killer"].Value;

                return new ActorDeathEvent(
                    line.Timestamp,
                    victim,
                    m.Groups["victimId"].Value,
                    m.Groups["zone"].Value,
                    killer,
                    m.Groups["killerId"].Value,
                    m.Groups["weapon"].Value,
                    m.Groups["class"].Value,
                    m.Groups["damage"].Value,
                    ParseDouble(m.Groups["x"].Value),
                    ParseDouble(m.Groups["y"].Value),
                    ParseDouble(m.Groups["z"].Value),
                    NpcNames.Classify(victim, killer, LocalHandle));
            }),

            "Vehicle Destruction" => Match(VehicleDestructionRegex, line, m =>
                new VehicleDestructionEvent(
                    line.Timestamp,
                    m.Groups["vehicle"].Value,
                    m.Groups["vehicleId"].Value,
                    m.Groups["zone"].Value,
                    m.Groups["driver"].Value,
                    m.Groups["attacker"].Value,
                    (DestroyLevel)int.Parse(m.Groups["from"].ValueSpan),
                    (DestroyLevel)int.Parse(m.Groups["to"].ValueSpan),
                    m.Groups["cause"].Value)),

            "CEntityComponentShopUIProvider::SendShopBuyRequest" => Match(ShopRequestRegex, line, m =>
                new ShopRequestEvent(
                    line.Timestamp,
                    m.Groups["shop"].Value,
                    m.Groups["shopId"].Value,
                    m.Groups["kiosk"].Value,
                    m.Groups["item"].Value,
                    decimal.Parse(m.Groups["price"].ValueSpan, System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(m.Groups["qty"].ValueSpan))),

            "CEntityComponentShopUIProvider::RmShopFlowResponse" => Match(ShopResponseRegex, line, m =>
                new ShopFlowResponseEvent(
                    line.Timestamp,
                    m.Groups["shop"].Value,
                    m.Groups["kiosk"].Value,
                    m.Groups["state"].Value,
                    m.Groups["result"].Value,
                    m.Groups["type"].Value)),

            "ObjectiveUpserted" => Match(ObjectiveRegex, line, m =>
                new MissionObjectiveEvent(
                    line.Timestamp,
                    m.Groups["mission"].Value,
                    m.Groups["objective"].Value,
                    ParseObjectiveState(m.Groups["state"].Value))),

            "CEntityComponentShipListProvider::SetVehicleSpawnedInformations" =>
                Match(VehicleSpawnRegex, line, m =>
                    new VehicleSpawnEvent(
                        line.Timestamp,
                        m.Groups["entity"].Value,
                        m.Groups["area"].Success ? m.Groups["area"].Value.Trim() : null)),

            "AttachmentReceived" => Match(AttachmentRegex, line, m =>
                new AttachmentEvent(
                    line.Timestamp,
                    m.Groups["player"].Value,
                    m.Groups["class"].Value.Trim(),
                    m.Groups["entity"].Value,
                    m.Groups["port"].Value,
                    m.Groups["status"].Value)),

            "VehicleListQuery" => ParseFleetQuery(line),

            // Only the scope-carrying variant matters; the timing lines that share
            // this tag are expected and must not count as parse failures.
            "Query Inventory" => TryMatch(InventoryScopeRegex, line, m =>
                new InventoryQueryEvent(
                    line.Timestamp,
                    m.Groups["owner"].Value,
                    m.Groups["scope"].Value,
                    m.Groups["key"].Value)),

            "Update Container Items Add New Item" => TryMatch(InventoryItemRegex, line, m =>
                new InventoryItemEvent(
                    line.Timestamp,
                    m.Groups["item"].Value,
                    m.Groups["scope"].Value,
                    m.Groups["key"].Value)),

            "Channel Disconnected" => Match(DisconnectRegex, line, m =>
                new DisconnectEvent(
                    line.Timestamp,
                    m.Groups["cause"].Value,
                    m.Groups["reason"].Value,
                    m.Groups["remote"].Value == "1")),

            _ => null
        };
    }

    /// <summary>
    /// Location inventory requests have a failure variant -
    /// <c>requested Location[INVALID_LOCATION_ID] doesn't have inventory</c> -
    /// which is expected, carries no location, and must not be counted as a
    /// parse failure.
    /// </summary>
    private GameEvent? ParseLocationInventory(LogLine line)
    {
        if (line.Body.Contains("doesn't have inventory", StringComparison.Ordinal))
            return null;

        return Match(LocationInventoryRegex, line, m =>
        {
            var location = m.Groups["location"].Value;

            // A sentinel, not a place: never let it reach the map or the stats.
            if (location.Equals("INVALID_LOCATION_ID", StringComparison.OrdinalIgnoreCase))
                return null;

            return new LocationInventoryEvent(line.Timestamp, m.Groups["player"].Value, location);
        });
    }

    /// <summary>
    /// Route calculation logs in two shapes: one naming both endpoints, and a
    /// shorter confirmation naming only the destination. Both are real routes.
    /// </summary>
    private GameEvent? ParseQuantumRoute(LogLine line)
    {
        var full = QuantumRouteRegex.Match(line.Body);
        if (full.Success)
        {
            return new QuantumRouteEvent(
                line.Timestamp,
                ExtractVehicle(line.Body),
                full.Groups["origin"].Value.Trim(),
                full.Groups["dest"].Value);
        }

        var destinationOnly = QuantumRouteSuccessRegex.Match(line.Body);
        if (destinationOnly.Success)
        {
            return new QuantumRouteEvent(
                line.Timestamp,
                ExtractVehicle(line.Body),
                Origin: null,
                destinationOnly.Groups["dest"].Value);
        }

        RecordUnmatched(line);
        return null;
    }

    private GameEvent? ParseUntagged(LogLine line)
    {
        // "[CSessionManager::OnClientSpawned] Spawned!" - the bracket group lands
        // in Severity because there is no <Tag> on this line.
        if (line.Severity is "CSessionManager::OnClientSpawned")
            return new ClientSpawnedEvent(line.Timestamp);

        var body = line.Body;

        if (body.StartsWith("Loading screen for", StringComparison.Ordinal))
        {
            var m = LoadingScreenRegex.Match(body);
            if (m.Success)
            {
                return new LoadingScreenEvent(
                    line.Timestamp,
                    m.Groups["screen"].Value,
                    m.Groups["rules"].Value,
                    double.Parse(m.Groups["seconds"].ValueSpan, CultureInfo.InvariantCulture));
            }

            RecordUnmatched(line);
            return null;
        }

        // Session header spans several lines: BackupNameAttachment, then
        // "Log started on", then FileVersion a few lines later. Accumulate and
        // emit once FileVersion arrives so the event is complete.
        if (body.StartsWith("BackupNameAttachment=", StringComparison.Ordinal))
        {
            var m = BackupNameRegex.Match(body);
            _pendingBuildTag = m.Success ? m.Groups["build"].Value.Trim() : null;
            return null;
        }

        if (body.StartsWith("Log started on", StringComparison.Ordinal))
        {
            _pendingSessionStart = line.Timestamp;
            return null;
        }

        if (body.StartsWith("FileVersion:", StringComparison.Ordinal))
        {
            var version = body["FileVersion:".Length..].Trim();
            var ev = new SessionStartEvent(
                _pendingSessionStart ?? line.Timestamp,
                _pendingBuildTag,
                version.Length > 0 ? version : null);

            _pendingBuildTag = null;
            _pendingSessionStart = null;
            return ev;
        }

        return null;
    }

    /// <summary>
    /// Runs a pattern and projects the result. The projection may return null to
    /// discard a line that matched but carries nothing worth recording.
    /// </summary>
    private GameEvent? Match(Regex regex, LogLine line, Func<Match, GameEvent?> project)
    {
        var m = regex.Match(line.Body);
        if (!m.Success)
        {
            RecordUnmatched(line);
            return null;
        }

        return project(m);
    }

    /// <summary>
    /// Splits <c>DRAK_Clipper_771690342710</c> into manufacturer and model by
    /// stripping the trailing entity id.
    /// </summary>
    internal static (string? Manufacturer, string Model) SplitVehicleId(string vehicleId)
    {
        var trimmed = TrailingIdRegex.Replace(vehicleId, string.Empty);

        var underscore = trimmed.IndexOf('_');
        if (underscore <= 0)
            return (null, trimmed);

        return (trimmed[..underscore], trimmed[(underscore + 1)..]);
    }

    /// <summary>
    /// Pulls the ship token out of the pipe-delimited QuantumTravel body, e.g.
    /// <c>| NOT AUTH | RSI_Aurora_Mk2_123[123]|CSCItemNavigation::...</c>.
    /// </summary>
    internal static string? ExtractVehicle(string body)
    {
        var m = QuantumVehicleRegex.Match(body);
        if (!m.Success)
            return null;

        var (_, model) = SplitVehicleId(m.Groups["vehicle"].Value);
        return model;
    }

    [GeneratedRegex(@"Handle\[(?<handle>[^\]]+)\]", RegexOptions.Compiled)]
    private static partial Regex LoginRegex { get; }

    [GeneratedRegex(
        @"geid (?<geid>\d+) - accountId (?<account>\d+) - name (?<name>\S+) - state (?<state>\S+)",
        RegexOptions.Compiled)]
    private static partial Regex CharacterRegex { get; }

    [GeneratedRegex(
        @"establisher=""(?<establisher>[^""]*)"".*?map=""(?<map>[^""]*)""\s+gamerules=""(?<gamerules>[^""]*)""\s+sessionId=""(?<session>[^""]*)""",
        RegexOptions.Compiled)]
    private static partial Regex ContextRegex { get; }

    [GeneratedRegex(
        @"CVehicleMovementBase::(?<method>\w+):.*?control token for '(?<vehicle>[^']+)'\s*\[(?<entity>\d+)\]",
        RegexOptions.Compiled)]
    private static partial Regex VehicleControlRegex { get; }

    [GeneratedRegex(
        @"Player\[(?<player>[^\]]+)\] requested inventory for Location\[(?<location>[^\]]+)\]",
        RegexOptions.Compiled)]
    private static partial Regex LocationInventoryRegex { get; }

    [GeneratedRegex(
        @"Projected Start Location is (?<origin>.+?) for route to destination (?<dest>\S+)",
        RegexOptions.Compiled)]
    private static partial Regex QuantumRouteRegex { get; }

    [GeneratedRegex(
        @"selected point (?<dest>\S+) as their destination",
        RegexOptions.Compiled)]
    private static partial Regex QuantumTargetRegex { get; }

    [GeneratedRegex(@"\|\s*(?<vehicle>[A-Za-z][A-Za-z0-9_]*)\[\d+\]\s*\|", RegexOptions.Compiled)]
    private static partial Regex QuantumVehicleRegex { get; }

    // contract appears as either [name] or [name][guid]; both forms occur.
    [GeneratedRegex(
        @"missionId\s*\[(?<mission>[^\]]+)\].*?generator name\s*\[(?<gen>[^\]]+)\],\s*contract\s*\[(?<contract>[^\]]+)\](?:\[(?<cguid>[^\]]+)\])?,?\s*contractDefinitionId\s*\[(?<cdef>[^\]]+)\]",
        RegexOptions.Compiled)]
    private static partial Regex ContractRegex { get; }

    // The MissionId tail is optional: chat/system notifications ("You have joined
    // channel ...") carry the id but no mission context.
    [GeneratedRegex(
        @"Added notification ""(?<text>.*)"" \[(?<id>\d+)\](?:\s*to queue\..*?MissionId:\s*\[(?<mission>[^\]]*)\])?",
        RegexOptions.Compiled)]
    private static partial Regex NotificationRegex { get; }

    [GeneratedRegex(
        @"Successfully calculated route to (?<dest>\S+)",
        RegexOptions.Compiled)]
    private static partial Regex QuantumRouteSuccessRegex { get; }

    [GeneratedRegex(
        @"cause=(?<cause>\d+) reason=""(?<reason>[^""]*)"".*?isRemote=(?<remote>\d)",
        RegexOptions.Compiled)]
    private static partial Regex DisconnectRegex { get; }

    [GeneratedRegex(
        @"^Loading screen for (?<screen>\S+) : (?<rules>\S+) closed after (?<seconds>[\d.]+) seconds",
        RegexOptions.Compiled)]
    private static partial Regex LoadingScreenRegex { get; }

    [GeneratedRegex(@"BackupNameAttachment=""(?<build>[^""]*)""", RegexOptions.Compiled)]
    private static partial Regex BackupNameRegex { get; }

    [GeneratedRegex(@"_\d{4,}$", RegexOptions.Compiled)]
    private static partial Regex TrailingIdRegex { get; }

    /// <summary>
    /// Harvests a vehicle id-to-name pairing from a line whose main purpose is
    /// something else entirely.
    /// </summary>
    /// <remarks>
    /// Ship names show up embedded in navigation chatter as
    /// <c>DRAK_Corsair_771478242932[771478242932]</c>. Since no event names a
    /// ship at the moment it is retrieved, these incidental sightings are the
    /// only way to identify one before the player disembarks.
    /// Gated on a cheap substring test so the backfill does not pay for a regex
    /// on every one of several million lines.
    /// </remarks>
    private GameEvent? IdentifyVehicle(LogLine line)
    {
        var body = line.Body;

        if (body.IndexOf("ItemNavigation", StringComparison.Ordinal) < 0
            && body.IndexOf("control token", StringComparison.Ordinal) < 0)
        {
            return null;
        }

        var m = VehicleNameRegex.Match(body);
        if (!m.Success)
            return null;

        var vehicleId = m.Groups["vehicle"].Value;
        var entityId = m.Groups["entity"].Value;

        // Only report a pairing once; these lines repeat constantly.
        if (!_identifiedVehicles.Add(entityId))
            return null;

        var (manufacturer, model) = SplitVehicleId(vehicleId);
        return new VehicleIdentifiedEvent(line.Timestamp, entityId, vehicleId, model, manufacturer);
    }

    /// <summary>
    /// Like <see cref="Match"/>, but a non-match is expected rather than a defect.
    /// Used for tags that cover several line shapes where only one is an event.
    /// </summary>
    private static GameEvent? TryMatch(Regex regex, LogLine line, Func<Match, GameEvent?> project)
    {
        var m = regex.Match(line.Body);
        return m.Success ? project(m) : null;
    }

    /// <summary>
    /// Fleet queries log twice: an opening "Fetching vehicle list…" line with no
    /// counts, then a completion line carrying them. Only the latter is an event;
    /// the former is expected and must not count as a parse failure.
    /// </summary>
    private GameEvent? ParseFleetQuery(LogLine line)
    {
        var m = FleetRegex.Match(line.Body);
        if (!m.Success)
            return null;

        return new FleetQueryEvent(
            line.Timestamp,
            int.Parse(m.Groups["entitlements"].ValueSpan),
            int.Parse(m.Groups["vehicles"].ValueSpan));
    }

    private static ObjectiveState ParseObjectiveState(string raw) => raw switch
    {
        "MISSION_OBJECTIVE_STATE_INPROGRESS" => ObjectiveState.InProgress,
        "MISSION_OBJECTIVE_STATE_COMPLETED" => ObjectiveState.Completed,
        "MISSION_OBJECTIVE_STATE_WITHDRAWN" => ObjectiveState.Withdrawn,
        "MISSION_OBJECTIVE_STATE_FAILED" => ObjectiveState.Failed,
        _ => ObjectiveState.Unknown
    };

    // ---- Commerce, missions, loadout, fleet ----

    [GeneratedRegex(
        @"shopId\[(?<shopId>\d+)\] shopName\[(?<shop>[^\]]+)\] kioskId\[(?<kiosk>\d+)\] " +
        @"client_price\[(?<price>[\d.]+)\].*?itemName\[(?<item>[^\]]+)\] quantity\[(?<qty>\d+)\]",
        RegexOptions.Compiled)]
    private static partial Regex ShopRequestRegex { get; }

    [GeneratedRegex(
        @"shopName\[(?<shop>[^\]]+)\] kioskId\[(?<kiosk>\d+)\] kioskState\[(?<state>[^\]]*)\] " +
        @"result\[(?<result>[^\]]*)\] type\[(?<type>[^\]]*)\]",
        RegexOptions.Compiled)]
    private static partial Regex ShopResponseRegex { get; }

    [GeneratedRegex(
        @"mission_id (?<mission>[0-9a-fA-F-]+) - objective_id (?<objective>\S+) - " +
        @"state (?<state>MISSION_OBJECTIVE_STATE_\w+)",
        RegexOptions.Compiled)]
    private static partial Regex ObjectiveRegex { get; }

    [GeneratedRegex(
        @"Player\[(?<player>[^\]]+)\] Attachment\[[^,]+,\s*(?<class>[^,]+),\s*(?<entity>\d+)\] " +
        @"Status\[(?<status>[^\]]*)\] Port\[(?<port>[^\]]+)\]",
        RegexOptions.Compiled)]
    private static partial Regex AttachmentRegex { get; }

    [GeneratedRegex(
        @"Retrieved (?<entitlements>\d+) entitlements out of (?<vehicles>\d+)",
        RegexOptions.Compiled)]
    private static partial Regex FleetRegex { get; }

    [GeneratedRegex(
        @"Inventory\[(?<owner>\d+):(?<scope>\w+):(?<key>\d+)\]",
        RegexOptions.Compiled)]
    private static partial Regex InventoryScopeRegex { get; }

    [GeneratedRegex(
        @"Entity Class\[(?<item>[^\]]+)\].*?SourceInventory\[\d+:(?<scope>\w+):(?<key>\d+)\]",
        RegexOptions.Compiled)]
    private static partial Regex InventoryItemRegex { get; }

    [GeneratedRegex(
        @"Class\((?<class>[^)]+)\).*?Port Name '(?<port>[^']*)'",
        RegexOptions.Compiled)]
    private static partial Regex CorpseItemRegex { get; }

    [GeneratedRegex(
        @"VehicleEntityId:\s*\[(?<entity>\d+)\](?:.*?LandingArea:\s*(?<area>.+?)\s*(?:\[Team|$))?",
        RegexOptions.Compiled)]
    private static partial Regex VehicleSpawnRegex { get; }

    // The backreference is what keeps this specific: the trailing bracketed id
    // must repeat the id embedded in the name.
    [GeneratedRegex(
        @"(?<vehicle>[A-Za-z][A-Za-z0-9_]*_(?<entity>\d{6,}))\[\k<entity>\]",
        RegexOptions.Compiled)]
    private static partial Regex VehicleNameRegex { get; }

    // ---- Dormant combat patterns (archived format; not emitted by SC 4.9) ----

    [GeneratedRegex(
        @"CActor::Kill: '(?<victim>[^']+)' \[(?<victimId>\d+)\] in zone '(?<zone>[^']*)' " +
        @"killed by '(?<killer>[^']+)' \[(?<killerId>\d+)\] using '(?<weapon>[^']*)' " +
        @"\[Class (?<class>[^\]]*)\] with damage type '(?<damage>[^']*)' " +
        @"from direction x: (?<x>[-\d.]+), y: (?<y>[-\d.]+), z: (?<z>[-\d.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ActorDeathRegex { get; }

    [GeneratedRegex(
        @"CVehicle::OnAdvanceDestroyLevel: Vehicle '(?<vehicle>[^']+)' \[(?<vehicleId>\d+)\] " +
        @"in zone '(?<zone>[^']*)'.*?driven by '(?<driver>[^']*)' \[\d+\] " +
        @"advanced from destroy level (?<from>\d+) to (?<to>\d+) " +
        @"caused by '(?<attacker>[^']*)' \[\d+\] with '(?<cause>[^']*)'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex VehicleDestructionRegex { get; }
}
