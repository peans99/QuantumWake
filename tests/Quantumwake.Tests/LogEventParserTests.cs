using Quantumwake.Core.Events;
using Quantumwake.Core.Logging;
using Quantumwake.Core.Parsing;

namespace Quantumwake.Tests;

/// <summary>
/// Event extraction. Fixtures are real lines from a 4.9.188.23497 install.
/// </summary>
public class LogEventParserTests
{
    private static T ParseOne<T>(string raw, bool includeSpam = false) where T : GameEvent
    {
        Assert.True(LogEnvelope.TryParse(raw, out var line));
        var ev = new LogEventParser().Parse(line, includeSpam);
        return Assert.IsType<T>(ev);
    }

    [Fact]
    public void Extracts_handle_from_login()
    {
        var ev = ParseOne<LoginEvent>(
            "<2026-08-20T01:28:55.402Z> [Notice] <Legacy login response> [CIG-net] " +
            "User Login Success - Handle[nekron] - Time[177332566] [Team_GameServices][Login]");

        Assert.Equal("nekron", ev.Handle);
    }

    [Fact]
    public void Extracts_character_geid()
    {
        var ev = ParseOne<CharacterEvent>(
            "<2026-08-20T01:28:53.446Z> [Notice] <AccountLoginCharacterStatus_Character> Character: " +
            "createdAt 1784476187540 - updatedAt 1786844282957 - geid 204721322607 - accountId 51915 - " +
            "name nekron - state STATE_CURRENT [Team_GameServices][Login]");

        Assert.Equal("nekron", ev.Name);
        Assert.Equal("204721322607", ev.Geid);
        Assert.Equal("51915", ev.AccountId);
        Assert.Equal("STATE_CURRENT", ev.State);
    }

    [Fact]
    public void Extracts_gamerules_from_loading_screen()
    {
        var ev = ParseOne<LoadingScreenEvent>(
            "<2026-04-21T01:51:10.715Z> Loading screen for EA_TheGoodDr : EA_FreeFlight closed after 2.49 seconds");

        Assert.Equal("EA_TheGoodDr", ev.Screen);
        Assert.Equal("EA_FreeFlight", ev.GameRules);
        Assert.Equal(2.49, ev.DurationSeconds, 3);
    }

    [Fact]
    public void Extracts_context_establisher_fields()
    {
        var ev = ParseOne<ContextEvent>(
            "<2026-08-20T01:28:58.088Z> [Notice] <Context Establisher Done> establisher=\"Game\" " +
            "runningTime=1.980013 map=\"megamap\" gamerules=\"SC_Frontend\" " +
            "sessionId=\"87075c0d-aa04-4043-9d3a-faf8f4f446f5\" [Team_Network][Network]");

        Assert.Equal("Game", ev.Establisher);
        Assert.Equal("megamap", ev.Map);
        Assert.Equal("SC_Frontend", ev.GameRules);
        Assert.Equal("87075c0d-aa04-4043-9d3a-faf8f4f446f5", ev.SessionId);
    }

    [Fact]
    public void Extracts_vehicle_and_splits_manufacturer_from_model()
    {
        var ev = ParseOne<VehicleControlEvent>(
            "<2026-08-20T01:57:58.601Z> [Notice] <Vehicle Control Flow> " +
            "CVehicleMovementBase::ClearDriver: Local client node [204721322607] releasing " +
            "control token for 'DRAK_Clipper_771690342710' [771690342710] [Team_CGP4][Vehicle]");

        Assert.Equal("DRAK_Clipper_771690342710", ev.VehicleId);
        Assert.Equal("DRAK", ev.Manufacturer);
        Assert.Equal("Clipper", ev.Model);
        Assert.Equal("771690342710", ev.EntityId);
        Assert.Equal(SeatChange.Left, ev.Change);
    }

    [Theory]
    [InlineData("DRAK_Clipper_771690342710", "DRAK", "Clipper")]
    [InlineData("RSI_Aurora_Mk2_123456789012", "RSI", "Aurora_Mk2")]
    [InlineData("ANVL_Paladin_6763231335005", "ANVL", "Paladin")]
    public void Splits_vehicle_ids(string id, string manufacturer, string model)
    {
        var (actualManufacturer, actualModel) = LogEventParser.SplitVehicleId(id);

        Assert.Equal(manufacturer, actualManufacturer);
        Assert.Equal(model, actualModel);
    }

    [Fact]
    public void Extracts_location_visit()
    {
        var ev = ParseOne<LocationInventoryEvent>(
            "<2026-08-20T01:35:00.271Z> [Notice] <RequestLocationInventory> Player[nekron] " +
            "requested inventory for Location[RR_MIC_LEO] [Team_CoreGameplayFeatures][Inventory]");

        Assert.Equal("nekron", ev.Player);
        Assert.Equal("RR_MIC_LEO", ev.LocationId);
    }

    [Fact]
    public void Extracts_quantum_route_with_origin_destination_and_ship()
    {
        var ev = ParseOne<QuantumRouteEvent>(
            "<2026-08-20T02:10:00.000Z> [Notice] <Calculate Route> [ItemNavigation][CL][35872] | " +
            "NOT AUTH | RSI_Aurora_Mk2_123456789012[123456789012]|CSCItemNavigation::CalculateRoute|" +
            "Projected Start Location is Gaslight for route to destination rs_ext_pyro-stan_jp1 " +
            "[Team_CGP4][QuantumTravel]");

        Assert.Equal("Gaslight", ev.Origin);
        Assert.Equal("rs_ext_pyro-stan_jp1", ev.Destination);
        Assert.Equal("Aurora_Mk2", ev.Vehicle);
    }

    /// <summary>
    /// Route calculation also logs a shorter confirmation naming only the
    /// destination. 652 of these appeared in the backfill before it was handled.
    /// </summary>
    [Fact]
    public void Extracts_quantum_route_from_destination_only_form()
    {
        var ev = ParseOne<QuantumRouteEvent>(
            "<2026-08-20T02:10:00.000Z> [Notice] <Calculate Route> [ItemNavigation][CL][27344] | " +
            "NOT AUTH | ORIG_325a_9942716387315[9942716387315]|CSCItemNavigation::CalculateRoute|" +
            "Successfully calculated route to NavPoint_Dynamic_759722455016 [Team_CGP4][QuantumTravel]");

        Assert.Null(ev.Origin);
        Assert.Equal("NavPoint_Dynamic_759722455016", ev.Destination);
        Assert.Equal("325a", ev.Vehicle);
    }

    /// <summary>
    /// Chat and system notifications carry an id but no MissionId tail, so that
    /// part of the pattern must be optional.
    /// </summary>
    [Fact]
    public void Extracts_notification_without_mission_context()
    {
        var ev = ParseOne<NotificationEvent>(
            "<2026-08-20T02:14:00.000Z> [Notice] <SHUDEvent_OnNotification> Added notification " +
            "\"You have joined channel 'Origin 325a : nekron'.\" [7] to queue.");

        Assert.Equal("7", ev.NotificationId);
        Assert.Null(ev.MissionId);
        Assert.False(ev.IsContractAccepted);
    }

    /// <summary>
    /// The "no inventory here" variant is an expected outcome, not a parse
    /// failure, and must not pollute the parser-health numbers.
    /// </summary>
    [Fact]
    public void Location_request_without_inventory_is_ignored_not_counted_as_failure()
    {
        Assert.True(LogEnvelope.TryParse(
            "<2026-08-20T01:35:00.271Z> [Notice] <RequestLocationInventory> Player[nekron] " +
            "requested Location[INVALID_LOCATION_ID] doesn't have inventory. " +
            "[Team_CoreGameplayFeatures][Inventory]", out var line));

        var parser = new LogEventParser();

        Assert.Null(parser.Parse(line));
        Assert.Equal(0, parser.UnmatchedKnownTags);
    }

    [Fact]
    public void Records_unmatched_tags_with_a_sample_for_diagnosis()
    {
        Assert.True(LogEnvelope.TryParse(
            "<2026-08-20T01:35:00.271Z> [Notice] <RequestLocationInventory> totally unexpected shape",
            out var line));

        var parser = new LogEventParser();
        parser.Parse(line);

        Assert.Equal(1, parser.UnmatchedKnownTags);
        var (count, sample) = parser.UnmatchedByTag["RequestLocationInventory"];
        Assert.Equal(1, count);
        Assert.Contains("unexpected shape", sample);
    }

    [Fact]
    public void Extracts_contract_identity()
    {
        var ev = ParseOne<ContractEvent>(
            "<2026-08-20T02:11:00.000Z> [Notice] <SMarkerHandler_Base::CreateMissionObjectiveMarker> " +
            "Creating objective marker: missionId [08cd3d5f-5b63-48e5-8e61-50f7722d98a7], " +
            "generator name [Covalex_RecoverCargo], " +
            "contract [Covalex_Stanton_VeryHard_RecoverCargo][3ff320ba-9d4e-4348-af40-4be1dce8ef27], " +
            "contractDefinitionId[9d5afe0e-da4c-4123-bb3a-cb26b02230c7]");

        Assert.Equal("08cd3d5f-5b63-48e5-8e61-50f7722d98a7", ev.MissionId);
        Assert.Equal("Covalex_RecoverCargo", ev.GeneratorName);
        Assert.Equal("Covalex_Stanton_VeryHard_RecoverCargo", ev.Contract);
        Assert.Equal("9d5afe0e-da4c-4123-bb3a-cb26b02230c7", ev.ContractDefinitionId);
    }

    /// <summary>The contract field also occurs without a trailing GUID block.</summary>
    [Fact]
    public void Extracts_contract_without_guid_suffix()
    {
        var ev = ParseOne<ContractEvent>(
            "<2026-08-20T02:12:00.000Z> [Notice] <CLocalMissionPhaseMarker::CreateMarker> " +
            "Creating objective marker: missionId [dcbc5ecb-782f-432f-aa91-68f1017d41df], " +
            "generator name [Covalex_RecoverCargo], " +
            "contract [Covalex_Stanton_VeryHard_RecoverCargo_2], " +
            "contractDefinitionId[9d5afe0e-da4c-4123-bb3a-cb26b02230c7]");

        Assert.Equal("Covalex_Stanton_VeryHard_RecoverCargo_2", ev.Contract);
    }

    [Fact]
    public void Extracts_notification_with_id_for_deduplication()
    {
        var ev = ParseOne<NotificationEvent>(
            "<2026-08-20T02:13:00.000Z> [Notice] <SHUDEvent_OnNotification> Added notification " +
            "\"Contract Accepted:  Bulk Covalex Shipment Needs Recovering: \" [154] to queue. " +
            "New queue size: 1, MissionId: [dcbc5ecb-782f-432f-aa91-68f1017d41df], ObjectiveId: [] " +
            "[Team_CoreGameplayFeatures][Missions][Comms]");

        Assert.Equal("154", ev.NotificationId);
        Assert.Equal("dcbc5ecb-782f-432f-aa91-68f1017d41df", ev.MissionId);
        Assert.True(ev.IsContractAccepted);
        Assert.False(ev.IsIncapacitation);
    }

    [Fact]
    public void Recognises_incapacitation_notification()
    {
        var ev = ParseOne<NotificationEvent>(
            "<2026-08-17T21:30:00.000Z> [Notice] <SHUDEvent_OnNotification> Added notification " +
            "\"Incapacitated: While incapacitated, ask others in your party, in chat, or through " +
            "rescue service beacons to revive you before the 'Time to Death' timer expires.\" [44] " +
            "to queue. New queue size: 1, MissionId: [00000000-0000-0000-0000-000000000000], ObjectiveId: []");

        Assert.True(ev.IsIncapacitation);
        Assert.Equal("44", ev.NotificationId);
    }

    /// <summary>
    /// Trap 1: the same notification fires repeatedly with differing Action values.
    /// Deduplicating on the bracketed id is what keeps counts honest.
    /// </summary>
    [Fact]
    public void Notification_id_deduplicates_repeat_fires()
    {
        string[] repeats =
        [
            "<2026-08-17T21:30:00.000Z> [Notice] <SHUDEvent_OnNotification> Added notification \"Incapacitated: x\" [44] to queue. New queue size: 1, MissionId: [m], ObjectiveId: []",
            "<2026-08-17T21:30:00.100Z> [Notice] <SHUDEvent_OnNotification> Added notification \"Incapacitated: x\" [44] to queue. New queue size: 2, MissionId: [m], ObjectiveId: []",
            "<2026-08-17T21:30:00.200Z> [Notice] <SHUDEvent_OnNotification> Added notification \"Incapacitated: x\" [44] to queue. New queue size: 3, MissionId: [m], ObjectiveId: []"
        ];

        var parser = new LogEventParser();
        var ids = new HashSet<string>();

        foreach (var raw in repeats)
        {
            Assert.True(LogEnvelope.TryParse(raw, out var line));
            if (parser.Parse(line) is NotificationEvent n)
                ids.Add(n.NotificationId);
        }

        Assert.Single(ids);
    }

    /// <summary>Trap 2: spam duplicates are dropped unless explicitly requested.</summary>
    [Fact]
    public void Spam_lines_are_skipped_by_default()
    {
        const string raw =
            "<2026-04-27T01:53:07.044Z> [SPAM 299][Notice] <RequestLocationInventory> " +
            "Player[nekron] requested inventory for Location[RR_MIC_LEO]";

        Assert.True(LogEnvelope.TryParse(raw, out var line));

        Assert.Null(new LogEventParser().Parse(line));
        Assert.NotNull(new LogEventParser().Parse(line, includeSpam: true));
    }

    [Fact]
    public void Recognises_routine_disconnect_teardown()
    {
        var ev = ParseOne<DisconnectEvent>(
            "<2026-08-20T01:32:18.399Z> [Notice] <Channel Disconnected> cause=30010 " +
            "reason=\"Nub destroyed\" frame=10136 isRemote=0 viewState=eCVS_InGame map=\"megamap\"");

        Assert.Equal("30010", ev.Cause);
        Assert.True(ev.IsRoutineTeardown);
        Assert.False(ev.IsRemote);
    }

    [Fact]
    public void Extracts_client_spawned()
    {
        ParseOne<ClientSpawnedEvent>("<2026-08-20T01:28:58.254Z> [CSessionManager::OnClientSpawned] Spawned!");
    }

    /// <summary>
    /// The retrieval line the game writes today. Build 12519617 stopped emitting
    /// the "Spawned" confirmation, so this spelling is the only one left - and
    /// reading only the other one lost every retrieval on current builds without
    /// registering as a parse failure.
    /// </summary>
    [Fact]
    public void Extracts_retrieval_from_the_spawning_line()
    {
        var ev = ParseOne<VehicleSpawnEvent>(
            "<2026-08-27T14:00:41.291Z> [Notice] " +
            "<CEntityComponentShipListProvider::SetVehicleSpawningInformations> " +
            "SetVehicleSpawningInformations - VehicleEntityId: [787284778374], LandingArea: nekron's");

        Assert.Equal("787284778374", ev.EntityId);
        Assert.Equal("nekron's", ev.LandingArea);
    }

    /// <summary>
    /// The ASOP terminal emits an [Error] twin beside the real request when it
    /// cannot resolve the landing area name. It names an entity already being
    /// retrieved, so it is neither a retrieval nor a parse failure.
    /// </summary>
    [Fact]
    public void Ignores_the_invalid_landing_area_twin()
    {
        Assert.True(LogEnvelope.TryParse(
            "<2026-07-26T19:37:55.992Z> [Error] " +
            "<CEntityComponentShipListProvider::SetVehicleSpawningInformations> " +
            "SetVehicleSpawningInformations - Invalid landingAreaLocStr - " +
            "Entity id: 738680164755 [Team_GameServices][ASOP]", out var line));

        var parser = new LogEventParser();

        Assert.Null(parser.Parse(line));
        Assert.Equal(0, parser.UnmatchedKnownTags);
    }

    /// <summary>
    /// The older confirmation line, still present in archived logs. Its extra
    /// LandingATCId field sits between the id and the landing area.
    /// </summary>
    [Fact]
    public void Extracts_retrieval_from_the_spawned_line()
    {
        var ev = ParseOne<VehicleSpawnEvent>(
            "<2026-08-24T01:34:39.741Z> [Notice] " +
            "<CEntityComponentShipListProvider::SetVehicleSpawnedInformations> " +
            "SetVehicleSpawnedInformations - VehicleEntityId: [774736075446], " +
            "LandingATCId: [746997539721], LandingArea: nekron's");

        Assert.Equal("774736075446", ev.EntityId);
        Assert.Equal("nekron's", ev.LandingArea);
    }

    /// <summary>
    /// The session header spans several lines and only completes at FileVersion,
    /// so the parser must hold state across them.
    /// </summary>
    [Fact]
    public void Assembles_session_header_across_multiple_lines()
    {
        string[] header =
        [
            "<2026-08-20T01:28:42.748Z> BackupNameAttachment=\" Build(12344265) 19 Aug 26 (21 28 37)\"  -- used by backup system",
            "<2026-08-20T01:28:42.748Z> Log started on Thu Aug 20 01:28:42 2026",
            "<2026-08-20T01:28:42.748Z> Built on Jul 29 2026 15:21:13",
            "<2026-08-20T01:28:42.748Z> Running 64 bit version",
            "<2026-08-20T01:28:42.748Z> FileVersion: 4.9.188.23497"
        ];

        var parser = new LogEventParser();
        SessionStartEvent? start = null;

        foreach (var raw in header)
        {
            Assert.True(LogEnvelope.TryParse(raw, out var line));
            if (parser.Parse(line) is SessionStartEvent s)
                start = s;
        }

        Assert.NotNull(start);
        Assert.Equal("4.9.188.23497", start.FileVersion);
        Assert.Equal("Build(12344265) 19 Aug 26 (21 28 37)", start.BuildTag);
    }

    [Fact]
    public void Tracks_match_counts_for_parser_health()
    {
        var parser = new LogEventParser();

        Assert.True(LogEnvelope.TryParse(
            "<2026-08-20T01:35:00.271Z> [Notice] <RequestLocationInventory> Player[nekron] " +
            "requested inventory for Location[RR_MIC_LEO]", out var line));

        parser.Parse(line);
        parser.Parse(line);

        Assert.Equal(2, parser.MatchCounts["location.inventory"]);
    }

    /// <summary>Unknown tags are ignored, never fatal - this is how the app survives patches.</summary>
    [Fact]
    public void Unknown_tags_are_ignored()
    {
        Assert.True(LogEnvelope.TryParse(
            "<2026-08-20T01:28:42.748Z> [Notice] <SomeFutureEventCigInvents> whatever", out var line));

        var parser = new LogEventParser();
        Assert.Null(parser.Parse(line));
        Assert.Equal(0, parser.UnmatchedKnownTags);
    }

    [Fact]
    public void Extracts_commodity_sale_with_resource_id()
    {
        var trade = ParseOne<CommodityTradeEvent>(
            "<2026-08-15T03:12:41.100Z> [Notice] <CEntityComponentCommodityUIProvider::SendCommoditySellRequest> " +
            "Sending SShopCommoditySellRequest - playerId[204721322607] shopId[730090005328] " +
            "shopName[SCShop_Admin_lt_base_g] kioskId[730090005327] amount[146240.000000] " +
            "resourceGUID[B999EF65-35BE-45BF-908A-5EAC6E06BA12] autoLoading[0] quantity[320] " +
            "transactionMode[Location] Cargo Box Data:  [boxSize[16] | unitAmount[20]]");

        Assert.True(trade.IsSell);
        Assert.Equal(146240m, trade.Amount);
        Assert.Equal(320, trade.Quantity);
        Assert.Equal("Location", trade.TransactionMode);

        // Normalised to lower case: the community dataset is keyed that way.
        Assert.Equal("b999ef65-35be-45bf-908a-5eac6e06ba12", trade.ResourceId);
    }

    /// <summary>
    /// Some builds have written trade lines without the resource id; the field
    /// is optional, not a new way for the whole line to stop parsing.
    /// </summary>
    [Fact]
    public void Commodity_sale_survives_a_missing_resource_id()
    {
        var trade = ParseOne<CommodityTradeEvent>(
            "<2026-08-15T03:12:41.100Z> [Notice] <CEntityComponentCommodityUIProvider::SendCommoditySellRequest> " +
            "Sending SShopCommoditySellRequest - playerId[204721322607] shopId[730090005328] " +
            "shopName[SCShop_Admin_lt_base_g] kioskId[730090005327] amount[1058400.000000] " +
            "quantity[288] transactionMode[ResourceContainer]");

        Assert.Equal(288, trade.Quantity);
        Assert.Null(trade.ResourceId);
    }

    /// <summary>
    /// The buy shape, which went unread until 0.7. Three things differ from a
    /// sale: the total is <c>price</c>, the quantity is centi-SCU, and there is
    /// no <c>transactionMode</c>.
    /// </summary>
    [Fact]
    public void Extracts_commodity_purchase()
    {
        var trade = ParseOne<CommodityTradeEvent>(
            "<2026-08-03T02:29:40.941Z> [Notice] <CEntityComponentCommodityUIProvider::SendCommodityBuyRequest> " +
            "Sending SShopCommodityBuyRequest - playerId[204721322607] shopId[730090138592] " +
            "shopName[SCShop_Admin_lt_base_g] kioskId[730090138591] price[63980.000000] " +
            "shopPricePerCentiSCU[1.999375] resourceGUID[b999ef65-35be-45bf-908a-5eac6e06ba12] " +
            "autoLoading[0] quantity[32000.000000 cSCU] Cargo Box Data: boxSize[16.000000] | unitAmount[20]");

        Assert.False(trade.IsSell);
        Assert.Equal("commodity.buy", trade.Kind);
        Assert.Equal(63980m, trade.Amount);

        // 32,000 cSCU is 320 SCU - and 320 is what boxSize 16 x unitAmount 20
        // says was loaded. Reading the number as written would report a hundred
        // times the cargo any ship in the game can carry.
        Assert.Equal(320, trade.Quantity);

        Assert.Null(trade.TransactionMode);
        Assert.Equal("b999ef65-35be-45bf-908a-5eac6e06ba12", trade.ResourceId);
    }

    /// <summary>
    /// "price" must not be taken from "shopPricePerCentiSCU", and "amount" must
    /// not be taken from the "unitAmount" trailing both shapes. Both lines carry
    /// their decoy after the real field, so a mis-anchored match takes the
    /// wrong number rather than failing outright.
    /// </summary>
    [Theory]
    [InlineData(
        "SendCommodityBuyRequest> Sending SShopCommodityBuyRequest - shopName[SCShop_x] " +
        "price[63980.000000] shopPricePerCentiSCU[1.999375] quantity[1600.000000 cSCU] " +
        "Cargo Box Data: boxSize[16.000000] | unitAmount[1]",
        63980, 16)]
    [InlineData(
        "SendCommoditySellRequest> Sending SShopCommoditySellRequest - shopName[SCShop_x] " +
        "amount[146240.000000] quantity[320] transactionMode[Location] " +
        "Cargo Box Data:  [boxSize[16] | unitAmount[20]]",
        146240, 320)]
    public void Trade_totals_are_not_taken_from_the_lookalike_fields(
        string body, int amount, int scu)
    {
        var trade = ParseOne<CommodityTradeEvent>(
            $"<2026-08-03T02:29:40.941Z> [Notice] <CEntityComponentCommodityUIProvider::{body}");

        Assert.Equal(amount, trade.Amount);
        Assert.Equal(scu, trade.Quantity);
    }
}
