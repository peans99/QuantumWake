# Event journals, in points

What an event bar costs, what each contract pays into it, and how much of it
this install's logs can prove - so the app can put a number on a bar the game
only ever draws as a percentage.

Read on 2026-10-09, the day RSI Discovery Month opened, from this install's
`Data.p4k`: 4.10.2 LIVE, build 12881860 (4.10.196.36804). Re-run with
`dotnet run --project src\Quantumwake.Cli -c Release -- --events` after a patch;
an event arriving or leaving shows there first.

---

## Where the game keeps it

Two halves, joined by record id.

**The journal** is a `ScenarioProgress` record. Its `factionRewardTiers` hold
`tierProgressions`, and each of those is one bar:

| field | what it is |
|---|---|
| `progressionText` | the bar's label, a text key - `@iasi_Journal_Transport` |
| `progressionColor` | the bar's colour, or empty |
| `completionType` | `CompletionType_GlobalProgression` - every point counts - or `CompletionType_CompletionTag` naming one `Tag` record |
| `tierRewards[]` | `minPoints` and the `badgeToAward` for each tier |

`convertTiersPointsToPercent` is true on Discovery Month and Alliance Aid: the
journal shows the bar as a percentage, never the points. A `JournalEntry` whose
`type` is a `JournalEntryLeaderboard` points at the record through
`scenarioProgressRecord`, and carries the title and the blurb.

**The points** are in the contract generators. A contract that counts carries:

- an `SContractPlugin_SScenarioProgress` naming the `ScenarioProgress` record;
- a `ContractResult_ScenarioProgress` with its `PointsToAward`;
- a `ContractResult_CompletionTags` whose tag decides which bar besides the
  overall one it fills.

The tag bars count points, not tags. A tag is awarded once per contract, and
Discovery Month's tag bars run to 10,000 - no event of a month is 10,000
contracts.

## What 4.10.2 has

Seven `ScenarioProgress` records; four have tiered bars and paying contracts:

| journal | record | bars | paying contracts |
|---|---|---|---|
| RSI Discovery Event | `Iasi_ScenarioProgress` | Your total, Transport, Collection, Defense | 31 |
| Alliance Aid | `CleanAir_ScenarioProgress` | Your Total, Transport, Collections, Defense | 57 |
| Orison Relief | `ORS_ScenarioProgress` | Your Total | 13 |
| Return of XenoThreat | `RoX_ScenarioProgress` | Your Total | 5 |

`RG_ScenarioProgress`, `RG_ScenarioProgress_Unpaid` and
`FFFinale_ScenarioProgress` read no tiered bar and no contract pays into them.
They are left out rather than shown empty.

### RSI Discovery Month

New in 4.10.2. The text calls it "RSI Discovery Month"; the files call it
`Iasi`, after the RSI event platforms (63 `StarMapObject.IasiPlatform*` around
Stanton's planets and moons).

| bar | tiers |
|---|---|
| Your total | 4,500 / 9,900 / 24,000 / 30,000 |
| Transport | 2,000 / 4,000 / 6,500 / 10,000 |
| Collection | 2,000 / 4,000 / 6,500 / 10,000 |
| Defense | 2,000 / 4,000 / 6,500 / 10,000 |

What each tier awards is not on the badge. The text has it keyed by bar and
tier, `IASI_Badge_Reward_Transport_T1_Desc`, with the overall bar's `OP` badges
as its "Personal" tiers. Tier 1 of Your total is a BriskAir IC-10 cooler, a
Delphi RS-10 radar and a Sovereign IP-10 power plant. Tier 4 of Defense is the
P8-SC "Echo" SMG.

The 31 contracts, by bar:

| bar | contracts | points each |
|---|---|---|
| Defense | patrols, ship defence, bounties (9) | 125-417 |
| Transport | hauls, couriers, package recovery, refuelling, the gold box relay (15) | 104-625 |
| Collection | mining purchase orders, salvage orders, the scavenger box (7) | 183-1,142 |

The single best contract is Procure Refined Quantainium at 1,142 points, which
counts on Collection. `splitPointsForParty` is false on every one of them, so a
party member's completion is theirs alone.

## The join to the logs

`Game.log` names a contract on its objective marker:

```
<CLocalMissionPhaseMarker::CreateMarker> Creating objective marker: missionId [8117448b-…],
  generator name [TheBackpocket], contract [ORS_CA_Medium], contractDefinitionId[15dd78fb-…], …
```

The `contract [...]` is the generator contract's `debugName`. 14 of 14 sampled
markers named one exactly, and every `contractDefinitionId` matched that
contract's `id`. The parser already keeps that name on each contract record,
along with how its mission ended. So the totals need nothing new from the
logs, and `PayloadVersion` did not move.

**Checked against something the totals did not use.** This install's Orison
Relief history is 15 Medium and 12 Large Materials Orders, all on 2026-09-17:
15 × 2,208 + 12 × 3,294 = **72,648 points**, just past the 72,000 top tier
(ORS Heavy Armor). If the in-game journal agrees that Orison Relief is
complete, the arithmetic is the game's.

## What it cannot see

Every total is a floor, and the page says so:

- **No marker, no count.** A contract that never raises an objective marker
  is invisible. Whether the mining and salvage purchase orders or the
  scavenger box raise one is the first thing to check in a log that has them.
- **Rolled-out logs.** A completion in a session whose backup is gone is gone.
- **The wipe.** History before the line the wipe draws is not counted, the
  same as every other total.
- **Renamed contracts.** A logged contract carrying the event's prefix
  (`Iasi_`) that the installed table does not list is counted as
  *unrecognised*, and the page says how many.

## In the app

- **Operations → Events.** Opens on a campaign board: the next target on the
  overall bar ("4,500 pts to Your total tier 1"), with a bar and a milestone
  for each tier. Below that is the fewest-contracts path to it, filtered by a
  **Prioritise** choice of any activity, combat, mining or hauling, and a lane
  for each activity showing its best-paying contract. The preference is
  matched on the contract's title and issuer, so it is a reading of the words
  and not a field the game sets. Under the board, each bar in points, ticked
  at its tiers, with its tiers and rewards folded away. Every paying contract,
  with its points, its bar and how many times this install has finished it,
  is folded at the bottom.
- **Tier rewards, item by item.** A tier opens into what it gives. The badge
  names no item, so the reward line is matched to the catalogue by name
  (`GameData/RewardItems.cs`), and only lines the game words itself are
  matched, not ones made up from a badge id. All 34 items across Discovery
  Month's tiers match, and the 22 plain ones match exactly once "Cooler",
  "Radar" or "Power Plant" is taken off. "Zeus Mk II PHB" fits two blades and
  "Constellation Starwalker Livery" two liveries; both are listed as one of.
  Pictures: a livery's is the game's own paint render; anything else is the
  wiki's, once the community dataset is on (`/api/events/picture/{class}`),
  else a mark for its kind. On 2026-10-09 no Discovery reward had a UEX
  price, and the tier says so once.
- **Now → Event card**, also on the overlay. It shows the event you chose with
  **Track on Now**, if you chose one. Otherwise it follows play: an event with
  a contract open in the journal, or one played in the last fortnight. It gives
  the overall bar against its next tier, every other bar's points, and what
  finishing each open contract adds. The choice is kept on the server
  (`event-track.json`, `POST /api/events/track`), because the overlay runs in
  its own browser profile and would never see a choice kept in browser storage.
- **Overlay glance view.** A tracked event is one line above Current status:
  the overall points and every bar against its next tier. The overlay drops
  the feed's detail text to stay dense, so the bar figures sit outside it.
- **Focus strip.** An open event contract leads it: "+417 pts to Your total,
  Defense".
- **Contracts table.** Every row that pays into an event carries a `pts` chip.
- **Tier toasts.** A tier crossed between two reads of `/api/events` raises
  the app's toast, in the overlay too, with what the tier gives. The first
  read of a page load is taken as history, so tiers already held stay quiet.

`/api/events` serves all of it.

## The text file after a patch

Found while reading Discovery Month and fixed alongside it, because it
decides whether the event can be read in game at all.

A loose `data\localization\english\global.ini` replaces the game's text table
rather than adding to it. StarStrings writes one, and so do Quantum Wake's item
labels. Nothing updates it when the game patches. 4.10.2's table has 521 keys
that this install's loose file, written on 2026-09-19, does not: every
Discovery Month contract title, the badge rewards, the new items and the Mk V
Constellations.

- **The notice.** `/api/labels/freshness` compares the loose file's keys with
  the game's own and gives the count. The page then says what fixes it, which
  depends on whose file it is: reinstall the labels for ours, a StarStrings
  release made for the patch for theirs, and another mod's file is not ours to
  touch. "Not now" holds until the count changes, so the next patch raises it
  again.
- **The fill.** When the labels are installed over a base that is not the
  game's own (StarStrings, or the table ours displaced), any key the base lacks
  is added in the game's own English before the marks are applied. A mod's
  wording is never replaced; only keys it has no line for are added. On this
  install the preview went from 3,123 marked names to 3,233, the difference
  being new items that now get marks too. `Data/TextTables.cs` does the
  comparison. The reader is
`Quantumwake.Core/GameData/GameScenarios.cs` and the totals are
`Quantumwake.Core/State/EventProgress.cs`.
