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

- **Operations → Events.** Each bar in points, ticked at its tiers, with the
  reward at each tier and the contracts that reach the next one in the fewest
  completions. Below that, every paying contract with its points, its bar and
  how many times this install has finished it.
- **Now → Event card**, also on the overlay. It shows the event being played:
  one with a contract open in the journal, or one played in the last fortnight.
  It gives the overall bar against its next tier, every other bar's points,
  and what finishing each open contract adds.
- **Focus strip.** An open event contract leads it: "+417 pts to Your total,
  Defense".
- **Contracts table.** Every row that pays into an event carries a `pts` chip.

`/api/events` serves all of it. The reader is
`Quantumwake.Core/GameData/GameScenarios.cs` and the totals are
`Quantumwake.Core/State/EventProgress.cs`.
