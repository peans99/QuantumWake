namespace Quantumwake.Core.Events;

/// <summary>
/// A kiosk purchase request, before the server has confirmed it.
/// </summary>
/// <remarks>
/// Logged in full, including the price the client believes it is paying:
/// <code>
/// &lt;CEntityComponentShopUIProvider::SendShopBuyRequest&gt; Sending SShopBuyRequest -
///   playerId[204721322607] shopId[752023944375] shopName[SCShop_OmegaPro_NewBabbage]
///   kioskId[752023944372] client_price[475200.000000]
///   itemClassGUID[...] itemName[POWR_JUST_S02_Genoa_SCItem] quantity[1]
/// </code>
/// A request is only counted as spend once a matching
/// <see cref="ShopFlowResponseEvent"/> reports success - the player may cancel,
/// or lack the funds.
/// </remarks>
/// <param name="Price">
/// <b>The order total, not a unit price.</b> A line reading
/// <c>client_price[52520] quantity[101]</c> is 101 magazines for 52,520 aUEC
/// altogether - about 520 each - not 52,520 apiece. Multiplying by quantity
/// inflated that single purchase to 5.3 million.
/// </param>
public sealed record ShopRequestEvent(
    DateTimeOffset Timestamp,
    string ShopName,
    string ShopId,
    string KioskId,
    string ItemName,
    decimal Price,
    int Quantity) : GameEvent(Timestamp)
{
    public override string Kind => "shop.request";

    public decimal Total => Price;

    /// <summary>Worked out, since the log gives the total rather than the unit.</summary>
    public decimal UnitPrice => Quantity > 0 ? Price / Quantity : Price;
}

/// <summary>The server's answer to a kiosk request.</summary>
public sealed record ShopFlowResponseEvent(
    DateTimeOffset Timestamp,
    string ShopName,
    string KioskId,
    string KioskState,
    string Result,
    string TransactionType) : GameEvent(Timestamp)
{
    public override string Kind => "shop.response";

    public bool Succeeded => Result.Equals("Success", StringComparison.OrdinalIgnoreCase);

    public bool IsBuying => TransactionType.Equals("Buying", StringComparison.OrdinalIgnoreCase);

    public bool IsSelling => TransactionType.Equals("Selling", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A commodity traded at a cargo or admin kiosk.
/// </summary>
/// <remarks>
/// <para>
/// Selling is the only income the logs record, and it dwarfs everything else -
/// single sales over a million aUEC:
/// </para>
/// <code>
/// &lt;CEntityComponentCommodityUIProvider::SendCommoditySellRequest&gt;
///   ... shopName[SCShop_Admin_lt_base_g] ... amount[1058400.000000]
///   ... quantity[288] transactionMode[ResourceContainer]
///   Cargo Box Data: [boxSize[16] | unitAmount[18]]
/// </code>
/// <para>
/// Buying is the same transaction written differently, and until 0.7 it was not
/// read at all - so cargo could be watched leaving but never arriving, and no
/// run could be priced. The total is <c>price</c> rather than <c>amount</c>,
/// the quantity is centi-SCU rather than SCU, and there is no
/// <c>transactionMode</c>:
/// </para>
/// <code>
/// &lt;CEntityComponentCommodityUIProvider::SendCommodityBuyRequest&gt;
///   ... shopName[SCShop_Admin_lt_base_g] ... price[63980.000000]
///   ... quantity[32000.000000 cSCU]
///   Cargo Box Data: boxSize[16.000000] | unitAmount[20]
/// </code>
/// <para>
/// <see cref="Quantity"/> is SCU on both sides; the parser converts.
/// </para>
/// <para>
/// Unlike item purchases there is no matching server response, so a trade is
/// recorded as requested rather than confirmed. Totals built on it should be
/// labelled accordingly.
/// </para>
/// </remarks>
/// <param name="ResourceId">
/// The commodity, as the game's own resource id (<c>resourceGUID[...]</c>).
/// The id resolves against nothing in the local install - not the DataCore in
/// any byte order, not the localisation table - so it is carried verbatim and
/// becomes a name only if the user opts into the community dataset.
/// </param>
public sealed record CommodityTradeEvent(
    DateTimeOffset Timestamp,
    string ShopName,
    decimal Amount,
    int Quantity,
    bool IsSell,
    string? TransactionMode,
    string? ResourceId = null) : GameEvent(Timestamp)
{
    public override string Kind => IsSell ? "commodity.sell" : "commodity.buy";
}

/// <summary>Lifecycle state of a mission objective.</summary>
public enum ObjectiveState
{
    Unknown,
    InProgress,
    Completed,
    Withdrawn,
    Failed
}

/// <summary>
/// A mission objective changing state.
/// </summary>
/// <remarks>
/// <para>
/// The event that finally makes contract outcomes visible:
/// </para>
/// <code>
/// &lt;ObjectiveUpserted&gt; Received ObjectiveUpserted push message for:
///   mission_id 2e26403d-82a5-44b7-9830-7e99bc0bf2bf
///   objective_id pickup_a812d48d-...-_0
///   state MISSION_OBJECTIVE_STATE_COMPLETED - created 0 - flags=ShowInLog|
/// </code>
/// <para>
/// <paramref name="MissionId"/> joins onto the <c>missionId</c> already captured
/// from objective markers, so an accepted contract can be followed through to
/// completion or abandonment.
/// </para>
/// </remarks>
/// <param name="ShownInLog">
/// True when the game flags this objective ShowInLog - the steps a player
/// actually sees in their journal. The rest are internal bookkeeping and would
/// triple every step count.
/// </param>
public sealed record MissionObjectiveEvent(
    DateTimeOffset Timestamp,
    string MissionId,
    string ObjectiveId,
    ObjectiveState State,
    bool ShownInLog = false) : GameEvent(Timestamp)
{
    public override string Kind => "mission.objective";
}

/// <summary>
/// An item attached to a character slot - armour, weapon, magazine, optic.
/// </summary>
/// <remarks>
/// <code>
/// &lt;AttachmentReceived&gt; Player[nekron]
///   Attachment[rsi_odyssey_undersuit_01_01_01_200000000219, rsi_odyssey_undersuit_01_01_01, 200000000219]
///   Status[persistent] Port[Armor_Undersuit] Elapsed[22.216066]
/// </code>
/// These fire on every spawn and inventory refresh rather than only on real
/// changes, so consumers must deduplicate by port and item class - the same
/// trap as HUD notifications.
/// </remarks>
public sealed record AttachmentEvent(
    DateTimeOffset Timestamp,
    string Player,
    string ItemClass,
    string EntityId,
    string Port,
    string Status) : GameEvent(Timestamp)
{
    public override string Kind => "loadout.attachment";
}

/// <summary>
/// An inventory scope being queried, e.g. <c>204721322607:Location:3531251586</c>.
/// </summary>
/// <remarks>
/// This is what makes stash tracking possible. The query fires immediately after
/// <see cref="LocationInventoryEvent"/> names the place, so the opaque numeric
/// key can be bound to a real location id.
/// </remarks>
public sealed record InventoryQueryEvent(
    DateTimeOffset Timestamp,
    string OwnerGeid,
    string ScopeType,
    string ScopeKey) : GameEvent(Timestamp)
{
    public override string Kind => "inventory.query";

    public bool IsLocation => ScopeType.Equals("Location", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// An item listed in an inventory scope.
/// </summary>
/// <remarks>
/// <code>
/// &lt;Update Container Items Add New Item&gt; End Page Entity Class[gmni_sniper_ballistic_01]
///   Rank[amracixuxglwx] SourceInventory[204721322607:Location:3531251586]
/// </code>
/// These are listings produced while browsing an inventory, not transfers, so
/// they show what was <i>seen</i> at a place rather than a live stock level.
/// Removals are not logged.
/// </remarks>
public sealed record InventoryItemEvent(
    DateTimeOffset Timestamp,
    string ItemClass,
    string ScopeType,
    string ScopeKey) : GameEvent(Timestamp)
{
    public override string Kind => "inventory.item";

    public bool IsLocation => ScopeType.Equals("Location", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Owned-vehicle count, from the entitlement query at login and hangars.</summary>
/// <remarks>
/// <c>Retrieved 12 entitlements out of 14 vehicules</c> - the second number is
/// the fleet size, and it grows over time.
/// </remarks>
public sealed record FleetQueryEvent(
    DateTimeOffset Timestamp,
    int Entitlements,
    int Vehicles) : GameEvent(Timestamp)
{
    public override string Kind => "fleet.query";
}
