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

### Two progressions for one organisation

The Rep app can show an organisation on more than one ladder, and its
contracts need not move both. Headhunters are the case here:

- Their page has Standing (`FactionReputationScope`) and Ship Combat (`ReputationScope_ShipCombat_HeadHunters`).
- Every Headhunters contract this install logged (Surprise Party, Setting a Trap, Ambush Op and the rest; 13, 9 of them finished) pays +100 on **Standing**, and is gated by Standing rank.
- In the whole install, **3 contracts pay Ship Combat**: `HeadHunters_HijackedShip_*`, +500 each. None is gated by it.
- The Ship Combat screenshots read about 2,006 on 9 September and about 2,035 on 24 October, which is unchanged within the ±18 reading margin. All 9 finished contracts came after the second shot, so a new Ship Combat screenshot is the test: the files say it should still read about 2,035.

So reputation is counted per ladder (`GameMissionPay.On(scope)`). The rep run's
figure, and the rep a stretch of work is credited with, only count amounts that
land on the ladder the screenshot shows. The rep run's figure follows these rules:

1. A contract gated on this ladder and open at the held rank: as above (`game`).
2. Otherwise, contracts that pay onto this ladder but are not gated by it (`game-open`): the figure most of them pay, smaller on a tie, with the range. Wikelo: 61 contracts, 10 to 1,000, so 10. Headhunters' Ship Combat: 3 contracts, all 500.
3. If the install knows their contracts and none pays this ladder (`elsewhere`): no figure, the page names the ladder their work does move, and **no tag is read**, because a tag would be that other ladder's figure. This is how a Ship Combat climb had come to be priced at Standing's +100.
4. Otherwise a StarStrings tag, as before.

A gated contract that opens above the held rank is never used. Taking it once
priced Red Wind's Not Eligible at Master's 4000.

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

## The catalogue: what a contract is, besides what it pays

The Missions page and the mission window read every contract's details too,
reprinted by:

```powershell
dotnet run --project src\Quantumwake.Cli -c Release -- --mission-catalogue --data <scratch folder>
```

A contract is assembled from four layers, each overriding the one before: its
handler's `contractParams` and `defaultAvailability`, its template's
`contractDisplayInfo` (the mission type and the illegal flag), its own
`paramOverrides` and `additionalPrerequisites`, and for a sub-contract its
parent. A legacy contract names a `MissionBrokerEntry`, which carries all of it
as fields of its own (`title`, `missionGiver`, `type`, `locationMissionAvailable`,
`reputationPrerequisites.wantedLevel`).

On 4.10.2: **5,171** contracts with their sub-contracts, **878** marked not for
release or work in progress on themselves, their handler or their template, so
**4,293** that the game offers. Of those:

| Read | Share | From |
| --- | ---: | --- |
| Contractor | 99% | `Contractor` string parameter, else the handler's |
| Mission type | 100% | `missionTypeOverride`, else the template's `MissionType.LocalisedTypeName` |
| Title | 99% | `Title` string parameter, or the contractor's text it names |
| Briefing | 94% | `Description` string parameter, likewise |
| System | 81% | `ContractPrerequisite_Locality` |
| Game's time estimate | 88% | `contractResults.timeToComplete`, minutes |
| Difficulty ratings | 86% | `contractResults.difficulty`: mechanical skill, mental load, risk of loss, game knowledge |
| CrimeStat range | 57% | `ContractPrerequisite_CrimeStat` |
| Rank gates | 44% | `minStanding`/`maxStanding` and `ContractPrerequisite_Reputation` |
| Series | 53% | `ContractPrerequisite_CompletedContractTags.requiredCountValue` |
| Location | 26% | `ContractPrerequisite_Location`, a star-map object |

117 are marked illegal and 166 have a buy-in. A title whose English is a lone
run-time placeholder (`~mission(Title)`, the Covalex random hauls) is read as
none.

A title of the form `~mission(Contractor|BlackBoxRecoverTitleMedium)` is not a
run-time pick: it asks the contractor for a text by name. The contract names its
contractor as a `Contractor` property (a `MissionPropertyValue_Organization`
listing one `MissionOrganization`), and that organisation's
`stringVariants.variants` hold one text per `Tag`, keyed by the tag's `tagName`.
Bit Zeros' Medium black-box run is "Too Smart For Our Own Good". Until 0.20.46
these read as none, and 373 released contracts showed their debug name: every
recovery run for Bit Zeros, Hockrow, Dead Saints, FTL and Red Wind, Vaughn's
assassinations, Adagio's salvage claims. Resolving them leaves 37 untitled, and
for those the files hold no title at all: 24 first-time Hockrow facility-delve
contracts (template `Phase2-Mission01` and its kin, empty `displayString`), 12
tutorial parts and `MW_CH3`. No contract that already had a title changed.

The difficulty ratings are not out of five: a Red Wind bulk haul's mental load
is `Insane_complexity_NOT_soloable_7`. The page shows the number and the
game's words beside it.

A `MissionLocality` is a list of star-map objects, so it is named by the
planets it holds, by the star map's own names: `Stanton1` is Hurston,
`RegionA` is Pyro I and Monox, `RegionC` is Pyro IV and Pyro V. A locality that
holds a system's star (`PyroStar`, `NyxSolarSystem`) is the whole system.

Checked against the logs: all **751** logged contracts are found by their
`contractDefinitionId`, the debug name agrees on all 751, and the files give a
title for **737** of them. Where the files' title is a template the game fills
in ("~mission(ReputationRank) Hauler Needed for ~mission(CargoGradeToken)
Shipment"), the page shows the title the logs saw instead.

The window's runs come from the logs, under the same wipe rule as every other
page; a payout is shown against a run when the award toast landed within five
seconds of that run's completion under the same title.


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

### Our overlay writes them (0.20.39)

The item-label overlay now does the same for hauling titles
(`TextOverlayOptions.Routes`, on by default). It uses the mod's exact wording,
so `HaulingContract.RouteFromTitle` reads either the same way. Which ends a key
gets (`GameMissions.HaulTitles`) is decided in two steps:

1. **A key whose name states a shape** (`…HaulCargo_AToB…title`, `SingleToMulti`, `MultiToSingle`) gets the ends that name promises. The Covalex hauls flown here are broker missions that pick their title key at run time (`~mission(Title)`), so no contract record points at the key. All 54 logged under `Covalex_HaulCargo_SingleToMulti_title` read "from Port Tressler", as the name says. Three keys carry one generator contract of another shape each: Ling's two AToB titles each carry a multi-pickup haul, and Dead Saints' MultiToSingle title a single-pickup one. The name wins over those, as it does for the mod.
2. **Any other key a hauling contract uses** (intro hauls, the Iasi event) names an end only when every contract under it has exactly one of that end.

Measured on 4.10.2 (`--missions` prints it):

- 28 keys: 18 A > B, 6 to, 4 from.
- On the game's own text, all 28 lines are rewritten.
- StarStrings placed 20 of these keys, and **all 20 get the same ends**.
- 8 keys get places the mod never wrote: the Covalex and Ling Rehire evaluations, Covalex MultiToSingle, Ling SingleToMulti, Red Wind's intro haul and three Iasi event hauls.
- Layered over an installed StarStrings, a title that already asks for its places is left alone, so on this install it adds those 8 and nothing else.

Not yet seen: a log line from a title this overlay wrote. Install it, take a
haul, and check that `--hauling` names its end.
