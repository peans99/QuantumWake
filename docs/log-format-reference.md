# Game.log format reference

Two halves: **Part 1** is the removed combat formats (documented by the parser
repos, absent from this install). **Part 2** is what was actually observed in
this install's logs and is safe to build against.

Install: `C:\Program Files\Roberts Space Industries\StarCitizen\LIVE`
Version: 4.9.188.23497 · Handle: `nekron` · GEID: `204721322607`

---

## Envelope

Every line is prefixed with a UTC timestamp, then usually a severity tag and an
angle-bracket event tag:

```
<2026-08-20T01:28:55.402Z> [Notice] <Legacy login response> … [Team_GameServices][Login]
```

```python
TIMESTAMP = re.compile(r'<(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z)>')
ENVELOPE  = re.compile(r'^<(?P<ts>[^>]+)>\s*(?:\[(?P<sev>[^\]]+)\]\s*)?(?:<(?P<tag>[^>]+)>)?\s*(?P<body>.*)$')
```

Severity tags seen: `[Notice]` (10,313), `[Error]` (1,890), `[Trace]`, `[CVARS]`,
`[flow]`, `[VK_INFO]`, and a large family of `[SPAM nnn]` tags. Trailing
`[Team_*][Category]` markers are useful for coarse routing.

---

## Part 1 — Removed combat formats (NOT present in this install)

Kept for reference and for parsing archived pre-4.x logs. Patterns below are
StarLogs' `event_parser.py`, which is the clearest published record of them.

### Actor death

```
<Actor Death> CActor::Kill: 'VictimName' [12345] in zone 'ZoneName'
  killed by 'KillerName' [67890] using 'WeaponName' [Class WeaponClass]
  with damage type 'Bullet' from direction x: 0.0, y: 0.0, z: 0.0
```

```python
KILL_PATTERN = re.compile(
    r"<Actor Death> CActor::Kill: '([^']+)' \[(\d+)\] in zone '([^']+)' "
    r"killed by '([^']+)' \[(\d+)\] using '([^']+)' \[Class ([^\]]+)\] "
    r"with damage type '([^']+)' from direction x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)",
    re.IGNORECASE)
```

### Vehicle destruction

```
<Vehicle Destruction> CVehicle::OnAdvanceDestroyLevel: Vehicle 'ANVL_Paladin_6763231335005' [id]
  in zone 'Zone' [pos x: … y: … z: … vel x: … y: … z: …]
  driven by 'Driver' [id] advanced from destroy level 0 to 1 caused by 'Attacker' [id] with 'Combat'
```

```python
VEHICLE_DESTROY_PATTERN = re.compile(
    r"<Vehicle Destruction> CVehicle::OnAdvanceDestroyLevel: Vehicle '([^']+)' \[(\d+)\] "
    r"in zone '([^']+)' \[pos x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+) "
    r"vel x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\] "
    r"driven by '([^']+)' \[(\d+)\] advanced from destroy level (\d+) to (\d+) "
    r"caused by '([^']+)' \[(\d+)\] with '([^']+)'",
    re.IGNORECASE)
```

Destroy level `1` = soft death (disabled), `2` = full destruction.
Damage types observed historically: `Combat`, `Collision`, `SelfDestruct`,
`GameRules`, `Bullet`, `VehicleDestruction`.

### Corpse / actor stall

```python
CORPSE_PATTERN = re.compile(
    r"<\[ActorState\] Corpse>.*?Player '([^']+)'.*?:\s*(.+?)\s*\[Team_", re.IGNORECASE)

ACTOR_STALL_PATTERN = re.compile(
    r'<Actor stall>.*?Actor stall detected,\s*Player:\s*(\w+),'
    r'\s*Type:\s*(\w+),\s*Length:\s*(\d+(?:\.\d+)?)', re.IGNORECASE)
```

### Kill classification logic (StarLogs)

1. `killer == victim` → `SUICIDE`
2. `damage_type == 'bullet'` → FPS branch; `'vehicledestruction'` → vehicle-death
   branch; otherwise vehicle-combat branch.
3. Within each branch, NPC-vs-player on both sides selects between
   `FPS_DEATH` / `FPS_PVE_KILL` / `FPS_PVP_KILL` / `DEATH` / `PVE_KILL` /
   `PVP_KILL` / `KILL`.

NPC detection is substring heuristics on the entity name:
`PU_Pilots`, `PU_`, `AI_CRIM`, `AI_`, `_NPC_`, `Criminal-Pilot`, `Security-`,
`Pirate-`, `-Pilot_Light_`, `-Pilot_Medium_`, `-Pilot_Heavy_`,
`NPC_Archetypes`, `Kopion_`; plus name length > 40 chars or ≥ 3 hyphens.

Crew correlation: StarLogs links actor deaths to a vehicle destruction when they
fall within a **200 ms** window.

---

## Part 2 — Verified present in this install

All samples below are real lines from this machine, lightly truncated.
IDs replaced with `#ID#` where they were normalised during template extraction.

### Identity

```
<2026-08-20T01:28:55.402Z> [Notice] <Legacy login response> [CIG-net] User Login Success - Handle[nekron] - Time[177332566] [Team_GameServices][Login]

<2026-08-20T01:28:53.446Z> [Notice] <AccountLoginCharacterStatus_Character> Character: createdAt 1784476187540 - updatedAt 1786844282957 - geid 204721322607 - accountId 51915 - name nekron - state STATE_CURRENT [Team_GameServices][Login]
```

```python
HANDLE  = re.compile(r'<Legacy login response>.*?Handle\[([^\]]+)\]')
CHARACTER = re.compile(r'<AccountLoginCharacterStatus_Character> Character: '
                       r'createdAt (\d+) - updatedAt (\d+) - geid (\d+) - '
                       r'accountId (\d+) - name (\S+) - state (\S+)')
```

`Legacy login response` appears **once per session** — a reliable session anchor.

### Session boundaries

```
<2026-08-20T01:28:42.748Z> BackupNameAttachment=" Build(12344265) 19 Aug 26 (21 28 37)"  -- used by backup system
<2026-08-20T01:28:42.748Z> Log started on Thu Aug 20 01:28:42 2026
<2026-08-20T01:28:42.748Z> Built on Jul 29 2026 15:21:13
<2026-08-20T01:28:42.748Z> FileVersion: 4.9.188.23497
```

Session length is most robustly `last timestamp − first timestamp` per file
(this is what SCPlay does). The `Log started on` line gives wall-clock start;
`BackupNameAttachment` gives the build number and the name the file will be
archived under.

### Loading screens & game mode

```
<2026-04-21T01:41:50.494Z> Loading screen for Frontend_Main : SC_Frontend closed after 3.44 seconds
<2026-04-21T01:51:10.715Z> Loading screen for EA_TheGoodDr : EA_FreeFlight closed after 2.49 seconds
```

```python
LOADING = re.compile(r'Loading screen for (\S+) : (\S+) closed after ([\d.]+) seconds')
```

421 occurrences. The second capture is the gamerules value — the cleanest way to
segment a session into menu vs. PU vs. Arena Commander time.

### Context establisher (map / gamerules / session id)

```
<2026-08-20T01:28:58.088Z> [Notice] <Context Establisher Done> establisher="Game" runningTime=1.980013 map="megamap" gamerules="SC_Frontend" sessionId="87075c0d-aa04-4043-9d3a-faf8f4f446f5" [Team_Network][Network][Replication][Loading][Persistence]
```

```python
CONTEXT = re.compile(r'<Context Establisher Done> establisher="([^"]+)" '
                     r'runningTime=([\d.]+) map="([^"]+)" gamerules="([^"]+)" '
                     r'sessionId="([^"]+)"')
```

Gamerules distribution across backups: `SC_Frontend` 37,182 · `SC_Default` 15,421 ·
`EA_FreeFlight` 155.

### Ships flown  ← best vehicle signal

```
<2026-08-20T01:57:58.601Z> [Notice] <Vehicle Control Flow> CVehicleMovementBase::ClearDriver: Local client node [204721322607] releasing control token for 'DRAK_Clipper_771690342710' [771690342710] [Team_CGP4][Vehicle]
```

```python
VEHICLE_CONTROL = re.compile(
    r"<Vehicle Control Flow> CVehicleMovementBase::(\w+): "
    r"Local client node \[(\d+)\] .*? control token for '([^']+)' \[(\d+)\]")
```

488 occurrences. The ship name embeds the manufacturer prefix and entity id —
`DRAK_Clipper_771690342710` → manufacturer `DRAK`, model `Clipper`, id
`771690342710`. Strip the trailing `_\d+` to get the model.

`ClearDriver` = released control. Pair `SetDriver`/`ClearDriver` timestamps to
compute time-in-seat per ship (this is how SCStats derives "favourite vehicle").
Ships observed here: `DRAK_Clipper`, `RSI_Aurora_Mk2`.

### Locations visited

```
<2026-08-20T01:35:00.271Z> [Notice] <RequestLocationInventory> Player[nekron] requested inventory for Location[RR_MIC_LEO] [Team_CoreGameplayFeatures][Inventory]
```

```python
LOCATION = re.compile(r'<RequestLocationInventory> Player\[([^\]]+)\] '
                      r'requested inventory for Location\[([^\]]+)\]')
```

1,386 occurrences — fires whenever a local inventory is opened, so it's a decent
proxy for "places you actually stopped at".

### Quantum travel

```
<Calculate Route> [ItemNavigation][CL][35872] | NOT AUTH | RSI_Aurora_Mk2_#ID#[#ID#]|CSCItemNavigation::CalculateRoute|Projected Start Location is Gaslight for route to destination rs_ext_pyro-stan_jp1 [Team_CGP4][QuantumTravel]

<Player Selected Quantum Target - Local> …|CSCItemNavigation::OnPlayerSelectedQuantumTarget|Player has selected point rs_ext_pyro-stan_jp1 as their destination, routing locally

<Player Requested Fuel to Quantum Target - Server Routing> …|CSCItemNavigation::OnPlayerRequestFuelToQuantumTarget|Player has requested fuel calculation to destination rs_ext_pyro-stan_jp1
```

```python
QT_ROUTE  = re.compile(r'<Calculate Route>.*?\|([^|\[]+)\[\d+\]\|.*?'
                       r'Projected Start Location is (.+?) for route to destination (\S+)')
QT_TARGET = re.compile(r'Player has selected point (\S+) as their destination')
```

Gives origin, destination and the ship doing the travelling. Note all-slain
reports richer QT events existed only up to 4.0.1 / 4.0_PREVIEW — what remains
is the navigation-component chatter above, which is still enough to reconstruct
routes.

### Contracts and missions

```
<SHUDEvent_OnNotification> Added notification "Contract Accepted:  Bulk Covalex Shipment Needs Recovering: " [154] to queue. New queue size: 1, MissionId: [dcbc5ecb-782f-432f-aa91-68f1017d41df], ObjectiveId: [] [Team_CoreGameplayFeatures][Missions][Comms]

<SMarkerHandler_Base::CreateMissionObjectiveMarker> Creating objective marker: missionId [08cd3d5f-…], generator name [Covalex_RecoverCargo], contract [Covalex_Stanton_VeryHard_RecoverCargo][3ff320ba-…], contractDefinitionId[9d5afe0e-…]

<CObjectiveMarkerComponent::AddToPlayerDataBank> MissionObjectiveMarker_#ID#[#ID#] - Added to DataBank of Player: nekron[#ID#] - ZonePos: x: …, y: …, z: …, missionId[…], objectiveId[…]
```

```python
NOTIFICATION = re.compile(r'<SHUDEvent_OnNotification> Added notification "([^"]*)" '
                          r'\[(\d+)\] to queue\..*?MissionId: \[([^\]]*)\]')
CONTRACT     = re.compile(r'generator name \[([^\]]+)\], contract \[([^\]]+)\]'
                          r'(?:\[([^\]]+)\])?, contractDefinitionId\[([^\]]+)\]')
```

The `contract` field carries archetype, system, difficulty and type in one
string: `Covalex_Stanton_VeryHard_RecoverCargo`. That's directly parseable into
faceted stats.

### Notifications (general)

`<SHUDEvent_OnNotification>` and `<UpdateNotificationItem>` carry most
player-facing HUD text, including:

- `"Contract Accepted:  …"`
- `"Incapacitated: While incapacitated, ask others in your party…"`
- `"Entering Armistice Zone - Combat Prohibited: "` (366)
- `"Leaving Armistice Zone - Caution Advised: "` (150)
- `"Party Launch …"`, `"… Initiated by party leader KR105."` (2,554 party lines)

Each notification appears multiple times with different `Action:` values
(`Next`, `StartFade`, `Remove`) — **deduplicate on the `[nnn]` notification id**
or you will count each event three to five times.

### Disconnects

```
<Channel Disconnected> cause=30010 reason="Nub destroyed" frame=10136 isRemote=0 viewState=eCVS_InGame map="megamap" gamerules="SC_Frontend" hostType="Replicant" …

<Channel Destroyed> map="megamap" gamerules="SC_Frontend" … nickname="nekron" playerGEID=204721322607
```

```python
DISCONNECT = re.compile(r'<Channel Disconnected> cause=(\d+) reason="([^"]+)" '
                        r'frame=(\d+) isRemote=(\d) viewState=(\S+)')
```

Note `reason="Nub destroyed"` is routine teardown, not an error — don't surface
it as a crash.

---

## Part 3 — Quirks found while implementing the parser

These were not visible from grepping and only surfaced once a real parser ran
over the full backup set. Each cost real events before it was handled.

### Entries span multiple lines, and continuations are timestamped

The single nastiest quirk. Notification text can contain a newline, and Star
Citizen stamps the continuation with **the same timestamp**:

```
<2026-04-23T01:42:30.285Z> [Notice] <SHUDEvent_OnNotification> Added notification "You have joined channel 'Origin 325a : nekron'.
<2026-04-23T01:42:30.285Z> : " [4] to queue. New queue size: 1, MissionId: [00000000-...], ObjectiveId: [] [Team_CoreGameplayFeatures][Missions][Comms]
```

A leading timestamp therefore **cannot** be used to detect a new entry — the
continuation has one too. The reliable signal is an **odd number of double
quotes** in the pending entry, meaning it stopped mid-string; complete entries
always balance their quotes.

Cost of not handling this: **1,274 lost notifications** out of 8,325 (15%).

Implemented in `LogFileReader.ReadEntries`, with a 32-line join cap so a
malformed quote cannot swallow the rest of the file.

### `<Calculate Route>` has two forms

Only the first names both endpoints:

```
...|CSCItemNavigation::CalculateRoute|Projected Start Location is Gaslight for route to destination rs_ext_pyro-stan_jp1
...|CSCItemNavigation::CalculateRoute|Successfully calculated route to NavPoint_Dynamic_759722455016
```

Handling only the first form loses **652 of 1,328 routes** (49%). The second
form carries no origin, so the origin field must be nullable rather than
defaulted to a fake value.

### `<RequestLocationInventory>` has a failure variant

```
Player[nekron] requested Location[INVALID_LOCATION_ID] doesn't have inventory. [Team_CoreGameplayFeatures][Inventory]
```

Expected behaviour, not a parse failure — recognise and skip it, or it inflates
the parser-health error count. Two occurrences across the backup set.

### Verified totals

Full backfill of 144 backups + live `Game.log` (403 MB), after the above fixes:

```
hud.notification    8325      session.spawned      483
location.inventory  1399      session.loading      429
quantum.route       1328      session.character    182
session.context     1316      session.login        149
contract.marker      742      session.start        145
quantum.target       641      ! unmatched            0
net.disconnect       591
vehicle.control      497
```

Cross-checked against independent PowerShell greps: `vehicle.control` 497 =
488 backups + 9 live; `session.character` 182 = 180 + 2; `location.inventory`
1,399 = 1,386 + 15 − 2 invalid. Exact agreement.

---

## Implementation notes

- **Encoding**: files are plain ASCII/UTF-8; `[System.IO.File]::ReadLines()` was
  materially faster than `Get-Content` over 402 MB.
- **Volume**: 402.5 MB across 144 files. A full backfill parse should stream, not
  slurp, and is worth caching to SQLite or a Parquet/JSON index.
- **Noise**: `[SPAM nnn]`-tagged lines duplicate content that also appears
  untagged. Filter them or you'll double-count.
- **Line prefixes vary**: some lines have no severity tag at all (the header
  block, `Loading screen for …`). Don't assume `[Notice]` is present.
- **Timestamps are UTC** (`Z` suffix) — convert for local session display.
