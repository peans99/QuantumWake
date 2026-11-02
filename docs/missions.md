# What a contract pays, from the game files

Measured on 4.10.2 against this install: 282 log files, 751 logged contracts.
Every figure below is printed again by:

```powershell
dotnet run --project src\Quantumwake.Cli -c Release -- --missions --data <scratch folder>
```

Point `--data` at a scratch folder: it writes a game-data cache there, and the
running app's cache is not this build's to replace.

## The join

`Game.log`'s objective marker writes a `contractDefinitionId`:

```
Creating objective marker: missionId [08cd...], generator name [RedWind_Hauling],
contract [RedWind_Pyro_SmallGrade_..._Multi3ToSingle][...], contractDefinitionId[9d21...]
```

It is **not a record**. Looked up as a record id it finds 0 of 734. It is the inline `id`
(a guid, data type `0x000E`) of one contract inside a `ContractGenerator` record.
A generator's `generators[]` handlers hold `contracts`, `introContracts` and
`legacyContracts`, and a contract can hold `subContracts` with ids of their own.
A sub-contract is logged under its parent's name plus its index (`Iasi_DefendShip_Easy_2`).

The guid is stored the way a record's own id is, as two little-endian halves, and
needs the same unscrambling (`DataCore.GuidAt`). Read straight, it matches nothing.

- 5,171 contracts and sub-contracts in the install.
- **751 of 751 logged contracts found by id**; the debug name agrees on every one.
- `ContractRecord.DefinitionId` carries it (PayloadVersion 20).

## Reputation

Each contract lists `contractResults`. Reputation is
`ContractResult_LegacyReputation` → `contractResultReputationAmounts`:

| field | example |
|---|---|
| `factionReputation` | `FactionReputation_Lawful_RedWindLinehaul` |
| `reputationScope` | `ReputationScope_Hauling` — the ladder |
| `reward` | `SReputationRewardAmount.ReputationRewardAmount_Positive_XS` → `reputationAmount` 500 |

Read the number, never the name: `RepuationRewardAmount_Positive_1600` holds 1500.
There are 58 amount records.

Every result also has `missionResults`, a bool array with one flag per
`EMissionResult`: `Completed, InProgress, Failed, Abandoned, Withdrawn`, the
enum's own order in the DCB. Across every generator contract:

- 2,761 positive amounts apply on Completed.
- 54 negatives apply on Failed. For example, a Bounty Hunters Guild kill pays 500 and takes 250 back on a failure.
- 48 negatives apply on Completed. All 48 are the 2025 pick-a-side contracts, −190,500 to whichever side you did not choose.

What a contract "pays" (`GameMissionPay.OnCompletion`) is the **largest**
positive amount on Completed, not the sum. A Red Wind haul pays 500 on Hauling
and 50 on Affinity, and in the Rep app those are two separate bars.

### Checked against StarStrings

The mod's title tags were written by people reading the same files, but the
game data never saw them, so they are an independent check:

- **38 of 38** single-figure tags (`[150 Rep]`) equal the game's figure.
- **145 of 145** ladder tags (`[50/200/250/500/1000/2000/4000 Rep]`) contain the game's figure.

The ladder exists because one title key covers every rank. Red Wind's
`RedWind_HaulCargo_MultiToSingle_title_01` is shared by contracts with
`minStanding` from Rank2 to Rank6. Each contract names its own range, so the
ladder can be rebuilt from the contracts:

| opens at | Trainee | Rookie | Junior | Member | Experienced | Senior | Master |
|---|---|---|---|---|---|---|---|
| Hauling rep | 50 | 100, 200 | 250 | 500 | 1000 | 2000 | 4000 |

That is the mod's eight figures. The rep run takes the most a contract opening
at the highest rank at or below yours pays (`ReputationBoards.Pay`, source `game`).
Intro contracts carry no rank range and are left out, because they are open once
to everybody.

### What pays none

Of the 751 logged contracts, 426 pay rep on completion. The rest are mostly
180 Iasi event contracts, which pay `ScenarioProgress` points only (see
[events.md](events.md)), and 101 Gilly's Pilot School contracts, which pay items.
When the install knows a contract and it pays no rep, that is the answer, and a
title tag does not overrule it.

## Blueprints

A `BlueprintRewards` result has a `chance` and a `blueprintPool`. Two pools on
one contract combine as two chances.

The ground truth is the `Received Blueprint:` toast:

- 18 toasts, every one within a second of a contract completing.
- **The install marked all 18 of those contracts** as awarding a blueprint.
- The mod's `[BP]` tag was on 10 of them. It missed every FTL courier.
- 51 completed contracts carry `[BP]*` where the install has no blueprint result. All are Covalex hauls, and none was followed by a blueprint. The tag sits on the shared `Covalex_HaulCargo_*_title` templates, so it marks every contract using one.

## Money

A generator contract's payout is `ContractResult_CalculatedReward`, and that
result has **no fields at all**. Nothing beside it carries a figure either: no
int parameter, no mission property, and `ContractDifficultyProfile` holds
weights only. The server computes the sum, and the files cannot say what a
generator contract should pay.

Legacy contracts are different. A `legacyContracts` entry with no results of
its own names an older `MissionBrokerEntry` record. That record has a fixed
`missionReward.reward` and one `missionResultReputationRewards` list per
ending. In these logs that is the 111 Covalex hauls. The logs print 14 payouts
(`Awarded N aUEC`, paired with the completion toast ahead of it), all Covalex:

- 12 equal the broker reward to the credit (50,250, 61,500, 76,750 and so on).
- 2 paid less: 39,750 against 52,500, and 9,000 against 61,500. They are probably partial deliveries; not confirmed.

`GameMissionPay.Reward` holds the figure. It is not shown on a page yet.

## Hauling routes are not in the files

One definition, `RedWind_..._TradepostToStation_Aluminum_..._Multi3ToSingle`,
was logged as "to Checkmate", "to Gaslight" and "to Ruin Station". The server
picks the places for each run. StarStrings gets them into the log by rewriting
the title template in `global.ini`:

```
Covalex_HaulCargo_AToB_title=~mission(ReputationRank) | DIRECT ~mission(CargoGradeToken) Haul | ~mission(Location|name) > ~mission(Destination|name) [BP]*
```

The game then fills in the placeholders. That is the one thing that still needs
a text table rather than the DataCore.
