# Untapped log signals

A second sweep of the logs after the app was working, looking for events worth
adding. Counts are from the 30 most recent backups plus the live `Game.log`.

Ranked by value to the app.

---

## 1. Purchases — high value, fully formed

The best find. Every kiosk transaction is logged with shop, item, price and
outcome.

```
<Notice> <CEntityComponentShopUIProvider::SendShopBuyRequest> Sending SShopBuyRequest -
  playerId[204721322607] shopId[752023944375] shopName[SCShop_OmegaPro_NewBabbage]
  kioskId[752023944372] client_price[475200.000000]
  itemClassGUID[2a02027b-5c19-456b-901b-663189505be0]
  itemName[POWR_JUST_S02_Genoa_SCItem] quantity[1] [Team_CoreGameplayFeatures][Shops][UI]

<Notice> <CEntityComponentShopUIProvider::RmShopFlowResponse> Received ShopFlowResponse -
  ... shopName[...] kioskState[BuyRequestProcessing] result[Success] type[Buying]
```

Pairing request with response gives **confirmed spend**: a running aUEC total,
spend by shop, spend by category, and most-bought items. Observed prices range
from 265 aUEC for a healing consumable to 475,200 for a Genoa power plant.

`type[Buying]` implies a selling counterpart, which would give net trade profit.
Worth confirming with a session that sells something.

This is the feature SCStats advertises as "correlates purchase requests with
in-game responses" — we now have the exact format.

**Suggested view:** a Spending tab — total outlay, top shops, top items, and a
timeline of large purchases.

## 2. Mission objective completion — closes a known gap

`docs/phases-2-5.md` lists contract *completion* as unresolved. It is logged,
just under a different tag than contract acceptance:

```
<Notice> <ObjectiveUpserted> Received ObjectiveUpserted push message for:
  mission_id 2e26403d-82a5-44b7-9830-7e99bc0bf2bf
  objective_id pickup_a812d48d-4835-4a4d-81d5-dedf59ce0618_0
  state MISSION_OBJECTIVE_STATE_COMPLETED - created 0 - flags=ShowInLog|
```

Observed states:

| State | Count |
|---|---:|
| `MISSION_OBJECTIVE_STATE_INPROGRESS` | 263 |
| `MISSION_OBJECTIVE_STATE_COMPLETED` | 69 |
| `MISSION_OBJECTIVE_STATE_WITHDRAWN` | 8 |

`mission_id` joins straight onto the `missionId` we already capture from
`<SMarkerHandler_Base::CreateMissionObjectiveMarker>`, so contracts can finally
be shown as accepted → in progress → completed or abandoned, with a completion
rate and a time-to-complete.

`<CMissionLogEntry::UpdateActiveObjective>` carries the same objective ids with
UI display text, useful for naming individual objectives.

**Suggested change:** upgrade the Contracts view from a list of names to a
funnel with outcomes.

## 3. Loadout — good, and cheap to add

```
<Notice> <AttachmentReceived> Player[nekron]
  Attachment[rsi_odyssey_undersuit_01_01_01_200000000219, rsi_odyssey_undersuit_01_01_01, 200000000219]
  Status[persistent] Port[Armor_Undersuit] Elapsed[22.216066]
```

6,493 occurrences. Ports name the slot, so a full kit can be reconstructed:

| Port | Count |
|---|---:|
| `magazine_attach` | 925 |
| `optics_attach` | 445 |
| `weapon_attach_hand_right` | 440 |
| `helmet_visor` | 246 |
| `wep_stocked_3` | 229 |
| `wep_sidearm` | 192 |
| `Armor_Helmet` | 170 |
| `Armor_Undersuit` | 162 |

**Caveat:** these fire on every spawn and inventory refresh, not only on real
changes, so it needs deduplication by (port, item) per session — the same trap
as HUD notifications.

**Suggested view:** "kit you spawned in with" per session, and most-used weapons.

## 4. Fleet size — one line, genuinely interesting

```
<Notice> <VehicleListQuery> Fetching vehicle list for player 204721322607 completed.
  Retrieved 12 entitlements out of 14 vehicules. [Team_GameServices][ASOP][Entitlement][Insurance]
```

The second number is **ships owned**, and it grows over time in this data: 11,
then 13, up to 17. A fleet-size-over-time chart falls straight out of the
existing session timeline, and pairs nicely with the flights-per-ship stats we
already show.

`<OnRequestFetchVehicles>` also logs *hangar inventory queries by location*,
which would show where ships are stored.

## 5. Hangar and ship elevator activity — moderate

`ShipElevator` appears 9,263 times, plus:

```
<Notice> <LandingArea_UnregisterFromExternalSystems_StowingVehicle>
  [STOWING ON UNREGISTER] LandingArea_ShipElevator_HangarMediumFront_Rund [759193770304] -
  Attempting to stow current vehicle [753454720606] due to landing area unregistering.
```

Gives hangar size used (Small/Medium/Large), and ship retrieval/stow events. A
decent proxy for "sessions where you actually took a ship out", and the hangar
names identify the station.

## 6. Cargo and freight — investigated, and a dead end

`<Update Container Items Add New Item>` (294) names item classes entering
containers, and `FreightElevatorKiosk` appears in the shop UI component. Between
them there looked to be enough to track cargo hauling, which would have paired
well with the `HaulCargo_*` contracts we already parse.

**Checked in 0.7.0, and there is not.** The word *freight* is everywhere - 6,488
hits across six recent 4.9 sessions - but almost all of it is
`CSCLoadingPlatformManager` state changes and errors about missing light groups.
The only lines from the kiosk itself read:

```
<CEntityComponentFreightElevatorUIProvider::FillUnstowRequest>
  FreightElevatorKiosk_FreightElevator_Util_HangarSmall[776094926102] -
  Processed bindings into transfer request - Entities: 1, Location: 4075093849
```

A count of entities and an opaque location id. No item class, no quantity, no
commodity. Hangar size is legible from the kiosk name (`HangarSmall`,
`HangarLarge`), which is worth something to §5, but cargo hauling cannot be
reconstructed from this. Do not re-investigate without a new line shape.

## 6b. The party channel — taken in 0.7.0, widened in 0.8

Missed by the original sweep, and the best of what was left: the HUD’s party
notifications are the **only** lines in a 4.9 log that name another player.

```
<SHUDEvent_OnNotification> Added notification "Party
D-Rud disconnected.: " [94] to queue. ...
```

**Six titles, not five.** 0.7.0 read `Party`, `New Party Leader` and
`Party Disbanded`, and dismissed the matchmaking pair `Party Launch` /
`Party Launch Accepted` as chatter. It missed two more, because neither begins
with the word *Party*: **`New Member Joined`** (`X has joined the party.`) and
**`Member Left`** (`X has left the party.`). Fifty-five membership events on
this install were being dropped, and dropped invisibly — they never entered the
party-notification tally at all, so the accounting looked clean at 273 of 291
read.

They are a different fact from the connect and disconnect toasts, and a more
valuable one. Connecting is a client coming online while already partied; a
member who logs out and back in produces that pair all evening. Joining and
leaving are the ones that mean somebody was not there a moment before, or is
not coming back. Reading only the first makes a friend with a poor connection
look like one who walked off.

Current figures on this install: 365 notifications, 324 read, **32 people
named** — seven of whom (`Craven`, `IanH1194`, `Krios`, `Ronus`,
`SuperAtomic`, `Sybreed`, `drudz`) appear *only* in join or leave lines and
were unnameable before. 187 connects, 64 drops, 33 departures, 22 joins, 13
leader handovers, 5 disbands. The remaining 41 are queue chatter and one line
the game garbled.

**`Member Left` is shared with ship comms**, and this is the trap. Twenty-seven
of its lines are a ship channel emptying rather than a party changing:

```
Member Left  X has left the channel 'RSI Ursa Medivac : DeathStrokeo1'.
```

So the body has to be read rather than the title trusted, or a passenger
stepping out of a hired ship is recorded as leaving a party they were never in.

### Taken in 0.8: who was aboard whose ship

Those channel lines are read now. They name the ship **and its owner** —
`'RSI Ursa Medivac : DeathStrokeo1'` — which is the only thing in a 4.9 log
that puts a person inside a particular vehicle.

**Count the queue entries, not the log lines.** Each notification is written
four or five times as it is queued, faded and removed, and grepping naively
inflates this signal by roughly seven times — which is exactly what happened
when it was first proposed. The honest figures for this install:

| | Count |
|---|---|
| Boardings of your own ships | 388 |
| Boardings of somebody else’s | 21 |
| Other people boarding | 22 |
| Departures | 24 |
| Distinct ship-and-owner berths | 28 |
| Other pilots named | 5 |

Small, and worth having anyway: five people you actually crewed with, which
no other line records. The Crew page shows it as boardings rather than hours,
because there is no leave line for you, a channel opens on boarding rather
than on flying, and a parked ship reads the same as a crossing.

The limit is worth restating wherever this gets used: there is **no roster
event**, and **no line records you yourself joining a party** — your own handle
never appears as a joiner or a leaver. Presence is inferred from toasts about
other people alone, so somebody already online when you group up, who stays
until you log off, produces nothing whatever. Every figure built on this is a
floor, never a total.

## 7. Lower value

- **`<Connection Flow>`** (1,388) — comms channels opened with NPC modules
  (`AImodule_Pyro_751896004783`). Shows NPC interaction but little else.
- **Salvage** (592) — mostly `SetSalvageRepairAmmoCount_NoTarget` warnings and
  damage-map file paths. Noisy; no clean "salvaged X" event found.
- **Beacons** (76) — too sparse here to build on.
- **Mining** (15) — essentially absent from this player's logs.

---

## Recommended order

1. **Purchases** — self-contained, high information density, no new
   infrastructure needed.
2. **Objective completion** — closes a gap we documented, and reuses the
   `missionId` join we already have.
3. **Fleet size** — one regex, one chart.
4. **Loadout** — valuable but needs careful deduplication.

Everything above is read-only and needs no new dependency; each is a parser
addition plus a view.

## A caution

All of these are subject to the same erosion as combat logging. Quantum travel
detail went in 4.0.1, death scope narrowed in 4.0.2, inter-system jumps went in
4.1.0, and combat went entirely by 4.9. Any feature built on these should degrade
to a clearly-labelled empty state rather than a bare zero, and the parser-health
panel should cover them from day one.
