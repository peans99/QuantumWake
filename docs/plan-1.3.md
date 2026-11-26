# 1.3 - Star Citizen 5.0

Plan, unbuilt. Written 2026-11-25 from a diff of the 5.0 PTU against LIVE, to be
worked through from 2026-11-26 on `dev130`. Part A is what has to be right before
5.0 reaches LIVE. Part B is what 5.0 adds that the app does not read at all.
Part C is the local telemetry API, which is an investigation before it is a
feature.

## What it was measured against

| | LIVE | PTU |
|---|---|---|
| Build | 4.10.201.48085 (P4 13220821) | 5.0.202.46035 (P4 13284307, built 2026-11-25) |
| `Game2.dcb` | 340,988,493 bytes, v8 | 354,214,969 bytes, v8 |
| Structs / records | 6,700 / 118,499 | 6,861 / 122,280 |
| Vehicles in the app's catalogue | 1,097 | 1,105 |

The DataCore reader opens 5.0 unchanged and the full catalogue builds in 35 s.
Method: a scratch dumper over `P4kArchive` + `DataCore` (structs, records,
`global.ini`, p4k listing) and `GameCommodities.Load` per install, then a
section-by-section diff of the two `commodities.json`. Same method as the 4.10.3
surveys.

**Not measured:** the log format. The PTU's only `Game.log` is from 3 November
(4.10.3), so no 5.0 line has been parsed yet. See A3.

## Part A - fixes

Each is one patch on `dev130` (1.3.1, 1.3.2 ...) with its test, and a
`CacheVersion` bump in `GameCommodities` where the catalogue's content changes.

### A1. Wikelo special names

The six new Wikelo specials would show as the base hull: "Drake Pitbull" where
the game says "Drake Pitbull Wikelo Sneak Special". Their `vehicleName` points at
the base hull's key, and only the attach definition's `Localization.Name` carries
the special one:

| Class | `vehicleName` | attach name |
|---|---|---|
| `MISC_Starlite_Collector_Industrial` | `vehicle_NameMISC_Starlite` | `..._Starlite_Collector_Indust` |
| `DRAK_Pitbull_Collector_Stealth` | `vehicle_NameDRAK_Pitbull` | `..._Pitbull_Collector_Stealth` |
| `AEGS_Tiburon_Collector_Military` | `vehicle_NameAEGS_Tiburon` | `..._Tiburon_Collector_Military` |
| `DRAK_Ironclad_Collector_Industrial` | `vehicle_NameDRAK_Ironclad` | `..._Ironclad_Collector_Indust` |
| `DRAK_Ironclad_Assault_Collector_Military` | `vehicle_NameDRAK_Ironclad_Assault` | `..._Assault_Collector_Military` |
| `ORIG_m80_Collector_Stealth` | `vehicle_NameORIG_M80` | `..._M80_Collector_Stealth` |

Simply preferring the attach name is wrong. On the PYAM Exec ships (Cutlass Black,
Corsair, Syulen, F8C, F7A Mk II) it runs the other way: `vehicleName` is the
specific key and the attach name the generic one. **Rule:** when one key extends
the other (`X_...` over `X`) and resolves, take the longer one; otherwise keep
`vehicleName`. In `GameVehicles.cs:81`.

Done when: the six read their "Wikelo ... Special" names, the 28 LIVE
disagreements (PYAM Exec, `hud_wreckage` derelicts, hijacked Reclaimers) keep
the names they have today, and a test covers both directions.

**Done in 1.3.1.** Over LIVE's 1,097 vehicles and the PTU's 1,105, exactly
eight names change: the six specials, and the RSI Polaris Wikelo Special on
both installs, which LIVE was already showing as a plain Polaris.

### A2. Loot odds under loot themes

5.0 adds a theme layer to loot generation: 51 `LootThemeRecord`s (Rarity
Common/Uncommon/Rare/Unique, Faction, Event, Flavour wealth/tech/lawfulness,
ContentType per weapon kind). The pool filters and secondary choices that
`GameLoot.Roll` reads from `LootConstraints.poolFilter` / `.secondaryChoices`
(`GameLoot.cs:291`) now mostly live inside `LootTheme_Manual.poolFilters` /
`.secondaryChoices`, which are reached through the new `LootConstraints.themes`,
and per place through `LocationLootGenerationContextData.themes`.

Measured effect: `Loot.Rolls` 333 -> 178. 200 rarity-split rolls (for example
`V3LootTable_Weapons_Large | V3PoolFilter_Generic | V3MultiChoice_Generic_Rare`)
collapse into 45 unfiltered ones. The Loot page would still list items but get
the rarity odds wrong.

Work:
1. Read `themes` on the constraints and on location context data. Resolve
   `LootTheme_RecordRef` and `LootTheme_Manual`, and their filters and choice
   layers (`LootV3SecondaryChoicesLayer.choices`, `subThemeModifier`,
   `LootSubThemeModifier_Ignore.themesToIgnore`).
2. Find out how several themes on one container combine: intersect the
   filters, or one layer each. `LootGenerationGlobalParams.themeCategoryOrdering`
   suggests an order. Write down what is proven and what is assumed.
3. Keep the pre-5.0 path: LIVE must read the same 333 rolls as today.

Done when: PTU rolls carry their rarity split again, LIVE output is unchanged,
`docs/loot.md` explains themes, and a fixture covers a themed container.

**Done in 1.3.2**, with one correction to "LIVE output is unchanged". LIVE
already writes `Additive` filter steps: every event filter strikes all event
gear, then adds its own event back. Read as an exclusion, that add-back had
left 79 of LIVE's 333 rolls empty. In order, those 79 fill and the other 254
are byte-identical. On the PTU: 976 rolls, none empty, and 1,212 of 1,215
shared containers keep LIVE's rarity. Themes from places
(`LocationLootGenerationContextData`, 11 presets) are not applied: that waits
on B4. `docs/loot.md` has the detail, and the assumptions it adds.

### A3. First 5.0 log

After the first PTU session on 5.0:
`dotnet run --project src\Quantumwake.Cli -c Release -- --path D:\rsiptu\StarCitizen\PTU`.
`! unmatched known tags` must read 0. Any parser change moves `PayloadVersion`.
Look in particular for guild rep lines, career-board contract titles, and
`HaulingOrder_CollectOnly` legs (B8).

### A4. Checks, probably no code

- **Faction ladder:** the ceiling goes 95,251 -> 238,000, while Elite stays at
  95,250 and rank 0 is renamed Neutral -> Applicant. Render the Reputation page
  against the PTU catalogue and check the Elite bar is not stretched to 238,000.
- **FTL Courier re-keyed:** `FactionReputation_Lawful_FTLCourier` -> `..._FTL`.
  Nothing hard-codes either id. Check that stored rep history and the lone-tab
  reader match FTL by name rather than id.
- **Door and port-lock actions** moved from `spaceship_general` /
  `vehicle_general` to `seat_general`. Voice commands still work, because
  `ControlCatalogue.Find` falls back by action name. Update the map named in
  `VoiceCommands.cs:125-126` so it does not read as stale. The nine operator-mode
  toggles go `press` -> `tap`: confirm a voice press still lands.
**Checked 2026-11-26 (1.3.3):**

- **Controls.** A diff of the two catalogues (`--keys=md`, 1,104 actions on LIVE, 1,096 on
  the PTU) finds 9 actions that moved, all of them door, door-lock and port-lock actions,
  from `spaceship_general` and `vehicle_general` to `seat_general`. 11 actions changed
  from `press` to `tap`: the 9 operator-mode toggles, plus the door toggle and the
  port-lock toggle. Nothing else moved or changed mode. One action is new:
  `hmd_theater_screen_move`. Self destruct stays in `spaceship_general` on both. Voice
  holds a key for 0.06 s, which is inside `tap`'s 0.25 s release window, and matches the
  pilot's binding under either group. All 125 Stream Deck presets resolve on both
  installs. Eight mode-key presets mirrored the finger, though, so a press longer than a
  quarter second missed the window on 5.0. They now send a fixed tap.
- **FTL.** The record is re-keyed, but the name ("FTL Courier") and the logo are
  unchanged. Stored readings and the lone-tab reader go by the printed name, so nothing
  breaks.
- **Faction ladder: still open.** The ceiling goes from 95,251 to 238,000 with Elite at
  95,250. Only `ReputationLadders.Place` reads the ceiling: at the top rank, the estimate
  fills toward it. Whether the game's Elite bar does the same needs a PTU Rep app
  screenshot at Elite.
- **The guild ladder has the faction ladder's card names.** Both read Applicant, Jr.
  Contractor ... Elite Contractor under the heading "Standing", with no perks. Their
  thresholds differ (513 against 800 for Jr. Contractor), so `ReputationLadders.Match`
  cannot tell a guild page from a faction page by its cards. Each guild's
  `FactionReputation` record names `ReputationScope_Guild` in its `alliedParams.scope`,
  so the organisation can settle it. That is B1's first step.
- **Backpacks** now carry temperature (-5..50) and radiation (50) ratings, and
  lose their shop type, so the page falls back to the slot. The Climate tab skips
  backpacks on purpose. Leave that until there is evidence of how the game
  combines a backpack's range with a suit's.
- **Garage** sheets come from scunpacked and will lag 5.0, as they did 4.10.3.
  Settings already says when it is behind.

### Comes in on its own

These need nothing but a release note:

- No new player hull. The Aegis Javelin gets real dimensions (210 x 475 x 85 m)
  and joins the ship list. RSI Ursa Rover Survey is new. The Golem's box axes
  are corrected. 17 temporary BIS/Showdown hulls are gone.
- 5 new Wikelo trades (Starlite, Pitbull, Tiburon, Ironclad, Ironclad Assault).
- Armour: 2,418 pieces 1 kg lighter; heavy-armour impact resistance 0.65 -> 0.4.
- Ship weapons: repeater alpha and DPS about 2.5x (KLWE S5 1,227 -> 3,308 DPS).
  Shields retuned (S4 template 194,400 -> 330,480 HP, regen 14,256 -> 6,415).
- 58 new blueprints, 6 new commodities (4 recalled cryptokeys, Pouchweed,
  Gatala Fungi), 17 new `_v5` surface rock signatures, Nyx-Stanton jump point,
  Nyx I accessible, the People's Service Stations get amenities.
- Unchanged: spawns, crates, salvage, mining compositions, events (4 campaigns,
  106 contracts).

## Part B - new systems the app does not read

Ranked by how much a pilot gets from it. Each starts with reading the records
and putting counts in this doc before any UI is built.

### B1. Guilds

The 5.0 headline. A new `ReputationScope_Guild` ladder: Applicant 0, Jr.
Contractor 513, Contractor 2,051, Sr. 4,615, Veteran 8,205, Head 12,820, Elite
18,461. Eight new organisations: United Resource Workers, Mercenary Guild,
Interstellar Transport Guild, Academy of Science, Imperial Sports Federation,
Cornerstone Developments, Nyx I, and FTL (re-keyed).

The catalogue already reads the rep: every career-board contract pays guild
standing plus the contractor's own faction (`CB_URW_S_Shubin_...` pays URW 100 +
Shubin 100). What is missing is a view. Guild standing on the Reputation page,
which contracts raise which guild, and what each rank unlocks (`Gates`).
Academy of Science and Sports have no logo in the files.

**Done in 1.3.4.** Counts from the 5.0 PTU:

- **Career boards:** 20 entities with `MissionBoardType_Career`, which is each guild's
  console in 4 placements, for 4 guilds:
  - URW (`Guild_Industrial`): Shubin, Adagio, Cornerstone.
  - Mercenary: Eckhart, Hockrow, Northrock, Bounty Hunters Guild.
  - Transport: FTL, Ling, Covalex, Red Wind.
  - Science: Rayari, plus Adagio, Covalex and Red Wind on its HighTech boards.
- **Guilds with no board:** Sports has a record and no board. `Guild_Criminal` has a
  record and no name.
- **Pay into `ReputationScope_Guild`:** URW 113 contracts (100-250), Mercenary 109
  (100-300), Transport 76 (100-250), Science 6 (100).
- **Gates:** no contract is gated on guild rank. Career-board contracts are gated by the
  contractor's own faction rank, by Hauling, or by Bounty Hunters. The guild ranks name
  no perks. So "what each rank unlocks" has no answer in the files yet.
- **Telling guild pages from faction pages:** the guild ladder and the faction ladder
  share every card name, the heading and the lack of perks.
  - `alliedParams.scope` cannot settle it. Only the Mercenary Guild's names the guild
    scope.
  - What settles it is the scope an organisation's contracts pay into
    (`GameRepOrganisation.Ladder`), used by `ReputationLadders.Match` only to break a
    tie.
- **Seen rendered:** against the PTU catalogue, with a second server on scratch data.
- **Emblems (1.3.6).** Three guilds have a logo in `ManufacturerLogos`. The Academy of
  Science's record names `org_aos_icon_rgb_white.tif`, which the PTU archive does not
  hold. Every guild record also names a `guildBanner`, and all four exist under
  `Starchitect\Boards`. The Career board header shows "Authorized by" and the guild's
  emblem, so each guild card shows that header, and the Academy gets its mark. Sports has
  neither, and no board.

### B2. Mission boards

`MissionBoardType_Career` / `_Notice`, `EntityComponentMissionBoardUIProvider`,
and physical `PhysicalizedJobBoard_*` entities (three for the Wikelo vehicles
alone). 453 new contracts: Career Board URW 108, Mercenary 86, Transport 62,
Science 7; Notice Board 67 (`NB_L_Local_*`, `NB_R_Local_*`, from the new
`LocalContractGeneratorParams`); ICC probe/survey 55; Luminalia 12.

Notice-board and ICC contracts pay no rep. For the app that means Reference →
Missions says which board a contract comes from and where that board is. The
Contracts screen reader may meet a new board UI.

**Which board: done in 1.3.5.**

- **Where the board is recorded:** `ContractAvailability.missionBoardAvailability`
  (None / Career / Notice). It sits on the generator handler's `defaultAvailability`:
  339 availability blocks in all, 41 Career and 2 Notice.
- **Contracts per board on the PTU:** of 5,634 contracts, 249 released ones are on Career
  boards (248 `CB_`, plus one Rayari intro) and 63 on Notice boards (`NB_`). The 55 ICC
  probe contracts are Career, but marked unreleased.
- **Which guild:** read from who the contract pays guild standing to, else from the
  guild whose boards carry the contractor. URW 97, Mercenary 85, Transport 61,
  Science 6.
- **Still open:** where each board physically stands. That needs the board entities'
  placements from the map files, as `GameLootPlaces` reads crates.

### B3. Crafting modifiers and location quality

**Done in 1.3.7, and smaller than it looked.** Counts from the PTU:

- **Crafters:** 5 `CrafterComponentParams` entities, every one with
  `baseCraftingTime` 1. Only the two new ship fabricators carry a modifier:
  `CraftingModifierType_Time` 0.7 when the category is `FPSArmours`.
- **Ships:**
  - The Carrack's fabricator is a loadout entry on the Carrack and the Carrack
    Expedition.
  - The Drake kit's fabricator is placed in the object containers
    `Ships\DRAK\Clipper` and `Ships\DRAK\ironclad_assault`, and nothing in the
    DataCore names it.
  - The standard and Alliance Aid fabricators are placed nowhere in the map files, so
    they are player-placed.
- **Blueprints:** 942 of 1,691 are `FPSArmours` and show the faster time.
- **Quality by place:** nothing new to read. `CraftingQualityResourceOverrideRecord` has
  no records on the PTU. The per-place quality distributions
  (`CraftingQualityLocationOverrideRecord`, 12 on both builds) are already read by
  `GameQuality`.

The original note, for reference:
`CraftingModifierParams` with a time modifier, gated by blueprint category,
whitelist or process type. `CraftingQualityResourceOverride` (records) and
`LocationCraftingQualityContextData`: crafting quality now depends on where you
craft. For the app: the Blueprints page shows the real craft time and which
places give better quality.

### B4. Loot themes by place

Builds on A2. Once themes are read, each location's themes (faction, wealth,
tech, lawfulness, event) say what kind of loot a place rolls, which is the
"named places" loot question `docs/loot.md` still has open.

**Done in 1.3.8.** The 11 place presets are the factions' lands, and all of
them are used in `nyx1.socpak` and nowhere else.

- **How the loot gets there.** The loot was not where the crate reader looked.
  The 1,230 Starchitect places carry `V3SlotPreset_Loot_Generic_HCD` on their
  own entity, as `SubConfigGUID`, and each sits in its own land. That preset
  now rolls once per land with the land's themes added: 396 rolls, 1,230
  places in 9 lands.
- **What changes.** Shattered Blade land adds 66 items to a medium armour
  crate and Generic Lawful adds none. No land loses an item.
- **Checks.** LIVE is byte-identical. Named places on the PTU go from 529 to
  1,169. The detail and two assumptions are in `docs/loot.md` under "Lands".
- **Open.** The place names come from the map file, and the game may show
  others: the star map lists none of these places. A 5.0 log or a screenshot
  of a Nyx I settlement's name would settle that.

### B5. Item recovery

`ItemRecoveryCooldownParams`, `ItemRecoveryDeliveryTimeParams`,
`ItemRecoveryRepairParams`. Values not read yet. Probably recovery and
insurance timings worth showing next to a ship or an item.

### B6. Hauling: collect-only orders

`HaulingOrder_CollectOnly` is a new order type; the 4.10.3 PTU had added
`dropOffInstanceBrokerTags`. Six new Covalex Nyx mass-resource haul titles. The
Hauling reader and the group share assume pickup + drop, so check what a
collect-only leg looks like in a log (A3) before touching anything.

### B7. Lower priority

- NPC archetypes and `AutoSpawn*` (`NPCArchetype`, `NPCClassPool`,
  `FactionIdentity`) for Reference → Encounters.
- Weather: `WeatherEffects_Atmosphere_PlanetRain` / `_PlanetSnow` for the
  Climate tab, if they change body temperature.
- Starmap presets (`StarMapPreset*`), 1,299 new `Starchitect_Location` names.

## Part C - the local telemetry API

5.0 opens a local game API that other tools are already building on. **CIG has
not documented it**, and everything here beyond the first two bullets comes from
community projects.

What was checked on disk, 2026-11-25:

- `StarCitizen.exe` in both installs contains the `sc.game.telemetry` gRPC
  package, `GameTelemetryService` and `TelemetryStream`.
- Only the 5.0 PTU exe has `ListTriggerableActions`, `TriggerGameAction` (8
  references, 0 on LIVE) and the `game.telemetry.enabled` switch (1, 0 on LIVE).
  The PTU has no `user.cfg` yet.
- `Data\Libs\Telemetry\*.xml` are CIG's own analytics configs, unchanged. Not
  the API.

What community projects report: the service listens on `127.0.0.1:2942` and is
switched on by `server.game.telemetry.enabled=1` in `user.cfg`. The `.proto`
schema is embedded in the exe and can be dumped from it
([Purzifal/sc_make_proto](https://github.com/Purzifal/sc_make_proto)). Stream Deck,
MFD and tablet tools already use it
([Sokren/sc-telemetry-deck](https://github.com/Sokren/sc-telemetry-deck),
[zolij/verse-data-gateway](https://github.com/zolij/verse-data-gateway),
[city028/sc-telemetry](https://github.com/city028/sc-telemetry),
[scpages/telemetry](https://github.com/scpages/telemetry)). Each says it is
unofficial and may change with any patch.

Why it matters to this app:

- **Reading.** A live stream of ship and player state that today comes from
  log inference and screen reading. Every reading has to be checked against
  what the app already infers, the same way commodity prices were checked
  against `shopPricePerCentiSCU`.
- **Actions.** `TriggerGameAction` could fire an action directly. Voice commands
  and Stream Deck press keys today, and 511 of 1,103 actions have no key a deck
  can send. If actions cover those, the "no key, voice can only press keys"
  answers go away.

Steps:

1. Nicolas enables it in the PTU `user.cfg` and plays a short session.
2. Read the schema out of our own exe copy with our own reader (credited, not
   vendored), into `docs/telemetry.md`: services, messages and fields.
3. Scratch probe: connect, list the triggerable actions, record the stream for a
   session. Note message rate, fields, and what is missing when the pilot is on
   foot or in a seat.
4. Compare with the app: location, ship, quantum, shields, mining, cargo. Each
   matching or contradicting number is written down.
5. Design only then. It must be its own opt-in switch, off by default
   (every feed is its own switch). Reading comes first. Actions come after a decision on
   whether firing them is in bounds for the app (the same line `precise-poi.md`
   draws on macros). Add `Grpc.Net.Client` + `Google.Protobuf` and credit them in
   `docs/credits.md`. LIVE 4.10 has the stream but no switch or actions, so the
   app must say when the API is not there rather than show zeros.

## Order for 2026-11-26

1. A1 Wikelo names (small, 1.3.1).
2. A2 loot themes (1.3.2, the largest fix).
3. A4 checks, with fixes folded into whichever patch they touch.
4. C1-C3 as soon as the PTU `user.cfg` has the switch: needs Nicolas in the game.
5. B1 guilds, then B2 boards, B3 crafting. One patch each, each starting with
   counts in this doc.
6. A3 whenever a 5.0 log exists.

Each patch: worktree off `dev130`, both test suites green, CLI over the corpus
(0 unmatched), local exe in Downloads, push. No release until Nicolas says.

