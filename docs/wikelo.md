# Wikelo's emporium, from the game files

What the Banu trader Wikelo will build for you, what he wants for it, and where
the game keeps that - so the app can say it from the installed patch rather
than from a guide that may be a patch behind.

Read on 2026-09-11 from this install's `Data.p4k`, build 4.10 LIVE, with the
`DataCore` reader that already names cargo and reads crafting recipes. Nothing
here comes from a website except where it says so.

---

## What the guides say, and where they disagree

Three community sources were read first
([expcarry](https://expcarry.com/star-citizen-wikelo-ships-guide), verified
September 2026; [scverse.guide](https://scverse.guide/wikelo/), 4.9.0 data;
[sc-manifest](https://sc-manifest.com/wikelo), 4.8.0). They agree on the shape -
three emporiums (Dasi, Selo, Kinga; the wiki page itself refuses automated
reads), a once-only intro contract, Favors as the currency, ranks at 0 / 340 /
999 - and disagree on numbers: scverse has the Polaris at **200 Favors**,
expcarry at **50**. That disagreement is the reason to read the game.

## Where the game keeps it

Not in `missionbroker` - none of its 2,584 entries mention Wikelo. The emporium
is one `ContractGenerator` and the templates it points at:

| record | what it holds |
|---|---|
| `ContractGenerator.TheCollector` | three groups - `TheCollector_Small_Items` (38 contracts), `TheCollector_Standard` (7), `TheCollector_Vehicles` (42) |
| each `Contract` in a group | `debugName`, `template ->`, the **title and description keys**, the **reward** (`contractResults -> ContractResult_Item.entityClass`, `amount`), the **reputation reward** (`SReputationRewardAmount.Wikelo_30` etc.), and any **rank gate** (`ContractPrerequisite_Reputation.minStanding`) |
| `ContractTemplate.TheCollector_Vehicles_Fortune` and 68 siblings | the **requirements**: `objectiveTokens[].objectiveHandler.haulingOrders[]`, each an `entityClass` and `minAmount` |
| the contract's `propertyOverrides["HaulingOverride"]` | the same shape, for the 27 contracts that share a template (`ItemResourceGathering_TheCollector`, `TheCollector_Vehicles_Large`, ...) |
| `SReputationStandingParams.ReputationStanding_Wikelo_000..003` | the ranks: `Nothing` 0, `Armour` 340, `Wolf` 999, `Idris` 999 |
| `global.ini` | the English for every title key - `@TheCollector_Ships_Fortune_Indus_TItle` is "Fortune ship for you" |

The chain for one trade, the MISC Fortune:

```
ContractGenerator.TheCollector
  generators[2] "TheCollector_Vehicles"
    contracts[0]  debugName "TheCollector_Vehicle_Small_Fortune"
      template -> ContractTemplate.TheCollector_Vehicles_Fortune
                    haulingOrders: 3x Carryable_1H_CY_banu_favour_Wikelo
                                   1x Harvestable_Mineral_1H_CarinitePure
      contractResults: 1x EntityClassDefinition.MISC_Fortune_Collector_Industrial
                       +SReputationRewardAmount.Wikelo_30
      Title "@TheCollector_Ships_Fortune_Indus_TItle" -> "Fortune ship for you"
```

Which is the guide's "3x Favor, 1x Carinite (Pure)", to the number.

## What it says

Requirements are the game's class names. The guides' words for the common ones,
for reading: `banu_favour_Wikelo` is a Wikelo Favor, `banu_favour_Wikelo_special`
a Polaris Bit, `CarinitePure` Carinite (Pure), `Vlk_Pearl_Irradiated_Super_01` an
Irradiated Valakkar Pearl (AAA) and `_High_02` (AA), `Pyro_Serverblade_5` the
DCHS-05 comp-board, `basl_combat_light_helmet_02_01_01` the Ace Interceptor
Helmet, `medal_1_pristine_b/c/d` the UEE 6th Platoon Medal / Tevarin War Service
Marker / Government Cartography Agency Medal, `HardDrive_..._ASD_Red` the ASD
Secure Drive, `Scrip_Merc_1` MG Scrip, `ASDReward_pwl1` RCMBNT-PWL-1. The app
resolves these properly through its item catalogue; the doc does not.

### Getting in, and getting Favors (`TheCollector_Standard`)

| title | needs | gives |
|---|---|---|
| Wikelo Arrive to System | 1 Vestal Water, 3 Tundra Kopion Horn | +10 rep, and every other contract |
| Very Hungry | 1 smoothie, 1 fat-free ice cream | food reward pool |
| Trade Merc Scrip for Favors? | 50 MG Scrip | 1 Favor |
| Trade Council Scrip for Favors? | 50 Council Scrip | 1 Favor |
| Turn Things to Favor | 50 Carinite | 1 Favor |
| Trade Worm Parts for Favors? | 12 Irradiated Valakkar Pearl (AA) | 1 Favor |
| Want Polaris? Need something special. | *a commodity in SCU - see the gap below* | 1 Polaris Bit |

### Ships and vehicles (`TheCollector_Vehicles`, 42)

| title | needs | gives | rep |
|---|---|---|---|
| Noxy Mod | 4 Favor | Nox | 20 |
| Pulse Plus | 4 Favor | Pulse | 20 |
| Make a Ursa Mod | 4 Favor, 20 Saldynium Ore, 20 Jaclium Ore | Ursa Medivac | 20 |
| Make ATLS shoot | 2 Favor, 1 ATLS, 2 NN-13, 5 Apex Fang (irr.) | ATLS IKTI | 20 |
| Make jumpy ATLS shoot | 1 Favor, 1 ATLS, 2 NN-13, 5 Apex Fang (irr.) | ATLS GEO IKTI | 20 |
| ATLS GEO paint, three | 1 Favor, 1 ATLS, + 1 Carinite (Pure) / + 1 Pearl (AAA) / nothing more | ATLS GEO Grad 1-3 | 20 |
| Golem Rocks | 2 Favor, 15 ASD Secure Drive | Golem | 30 |
| Fortune ship for you | 3 Favor, 1 Carinite (Pure) | Fortune | 30 |
| Upgrade Intrepid | 3 Favor, 1 Cartography Medal | Intrepid | 30 |
| Peregrine Wikelo Mod | 4 Favor, 1 DCHS-05 | Sabre Peregrine | 30 |
| Spirit Cargo mod | 8 Favor, 1 Tevarin Marker | C1 Spirit | 30 |
| Ready for RAFT? | 8 Favor, 10 Adult + 20 Juvenile Fang (irr.), 5 Kopion Horn (irr.), 1 Pearl (AAA) | RAFT | 30 |
| Zeus Special | 10 Favor, 1 6th Platoon Medal | Zeus ES | 30 |
| RSI Meteor Mod, Prospects Look Good, Where Wolf? Here Wolf | *shared template; read from the override* | Meteor, Prospector, L-21 Wolf | 30 |
| Most Special Wolf | 5 Favor - **rank Armour (340) or better** | L-21 Wolf, unique | - |
| Firebird Mod | 15 Favor, 4 DCHS-05, 3 Ace Helmet, 1 Cartography Medal | Sabre Firebird | 100 |
| Build a Mod Scorpius | 15 Favor, 4 DCHS-05, 1 Tevarin Marker | Scorpius | 100 |
| What is Terrapin? | 15 Favor, 10 ASD Secure Drive, 1 Tevarin Marker | Terrapin Medic | 100 |
| Wikelo Navy F7 | 16 Favor, 3 DCHS-05, 5 Ace Helmet, 1 Cartography Medal | F7 Mk II | 100 |
| Zeus Cargo Special | 20 Favor, 15 Carinite, 10 Ace Helmet, 2 Carinite (Pure) | Zeus CL | 100 |
| Guardian Fight Mod | 20 Favor, 15 Pearl (AA), 10 Ace Helmet, 1 Tevarin Marker | Guardian | 100 |
| Guardian take down ship | 25 Favor, 15 Pearl (AA), 15 DCHS-05, 2 6th Platoon Medal | Guardian QI | 100 |
| Starfighter Ion / Inferno, Guardian WiK-X | *override* | Ares Ion / Inferno, Guardian MX | 100 |
| Red Fight Apollo | 30 Favor *(+ a commodity in SCU, see below)* | Apollo Triage | - |
| More than a Max | 30 Favor, 10 Ace Helmet, 3 Carinite (Pure), 3 Pearl (AAA) | Starlancer MAX | 250 |
| Want Taurus ship | 30 Favor, 3 Carinite (Pure), 3 Pearl (AAA), 3 Cartography Medal | Constellation Taurus | 250 |
| F8 War Mod | 40 Favor, 4 Carinite (Pure), 4 Pearl (AAA), 4 Tevarin Marker | F8C Lightning | 250 |
| Sneaky Stabber | 40 Favor, 15 DCHS-05, 3 Carinite (Pure), 3 Pearl (AAA) | F8C Lightning, stealth | 250 |
| Prowler More Utility | 40 Favor, 10 Yormandi Tongue, 20 Yormandi Eye, 3 Pearl (AAA), 3 Carinite (Pure) | Prowler Utility | 250 |
| New Move Big Starlancer Ship | 50 Favor, 15 Ace Helmet, 30 ASD Drive, 3 each Pearl (AAA), Tevarin Marker, DCHS-05, Carinite (Pure) | Starlancer TAC | 250 |
| Asgard Fight Mod | *override* | Asgard | 250 |
| Starlifter A2 War Mod | 50 Favor, 20 Polaris Bit, 20 MG Scrip, 6 each ASD Drive, Pearl (AAA), Tevarin Marker, DCHS-05, Carinite (Pure) | A2 Hercules | 500 |
| Now make Polaris. Short Time Deal. | **50 Favor**, 15 Polaris Bit, 10 DCHS-05, 20 Carinite, 20 Apex Fang, 20 MG Scrip, 15 each Ace Helmet, Pearl (AAA), 6th Platoon Medal, Carinite (Pure), ASD Drive, 1 each of the nine RCMBNT parts | Polaris | 1000 |
| Special Idris For Killing | 50 Favor, 50 Polaris Bit, 50 each DCHS-05, Carinite, Apex Fang, MG Scrip, Ace Helmet, 30 each Pearl (AAA), 6th Platoon Medal, Carinite (Pure), ASD Drive, 5 each of the nine RCMBNT parts - **rank Wolf (999)** | Idris-P | - |
| Extra Special Wolf | 1 Metamaterial Test (`CollectorMaterial_001`) | L-22 Alpha Wolf | - |
| Clipper Fight Now | 1 Metamaterial Test (`CollectorMaterial_002`) | Clipper | - |

So the Polaris is **50 Favors** in the installed game; scverse's 200 is stale.
Every number expcarry lists that this extraction can check, it matches.

### Small items (`TheCollector_Small_Items`, 38)

Armour and weapon recolours and mods - the "Fun Kopion Skull Gun" and its
kind, the GG suits, the AD rifles. Seventeen carry `DO_NOT_USE_NOW_LOOT` or
`DO_NOT_USE_STORE_EXCLUSIVE` in their debug name and have no reward attached:
retired or moved to loot and the store, and still in the file. A page built on
this has to hide them, not list them.

## What the reader does

`Quantumwake.Core/GameData/GameWikelo.cs` reads all of the above into the
game-data cache beside the recipes, and the Wikelo page under Jobs shows it.
Since the survey it also reads the **SCU requirements**: a resource order is a
`HaulingOrderContent_Resource` with a `ResourceType` and a `minSCU` that is a
float in the file, which is why the first pass missed it. The Polaris Bit is
24 SCU Quantainium; the Apollo wants 48 SCU Savrilium besides its 30 Favors;
the ATLS Orange Line wants 36 SCU Quantainium and 8 each of Copper, Tungsten
and Corundum. A contract's own `HaulingOverride` replaces its template's orders
rather than adding to them - read both and the Polaris Bit asks for its 24 SCU
twice.

Names come from the item catalogue, then `vehicle_Name<class>` for ships (the
localisation table keys some of those with a `,P` suffix), then the class name
spaced. Three ATLS paint variants have no name anywhere in the game's text and
show their class.

- **Which emporium.** Any of the three; the drop-off resolves by tag to the
  collector's asteroid bases. Which three, the logs say better than the file -
  see below.

## What the logs already say

The game's client log echoes the offer list every time the emporium generates
it, one line per contract:

```
<GenerateLocationProperty> Generated Locations - variablename: DropoffLocation_BP[Destination],
  locations: (Wikelo Emporium Selo Station [1615454559] [TheCollectorsAsteriod_Stanton2])
             (Wikelo Emporium Dasi Station [1231535936] [TheCollectorsAsteriod_Stanton1])
             (Wikelo Emporium Kinga Station [3168785171] [TheCollectorsAsteriod_Stanton4])
  contract: TheCollector_Vehicle_Small_Fortune
```

Measured over this install's 187 backups:

- **The three emporiums and their systems**: Dasi at `Stanton1` (Hurston),
  Selo at `Stanton2` (Crusader), Kinga at `Stanton4` (microTech). 52,192 lines
  each; the location ids are the game's own.
- **The list this pilot was offered**: 60 distinct contracts, each in the same
  58 files - so the catalogue is generated whole, not rotated, and "Short Time
  Deal" in the Polaris title is not a rotation the logs have ever shown. The
  intro appears in 73 files, the fifteen extra being sessions before it was done.
- **What was never offered**: `Kruger_Wolf_Unique`, `Super_Idris` - the two
  rank-gated trades - and `DrakeClipper`. The gate works, and the log is where
  the app can see it working for *this* pilot without knowing their standing.
- **Not seen**: an accept or a completion. No Wikelo contract has been taken on
  this install, so what those lines look like is still waiting on a log that
  has one. The ordinary contract parser will very likely catch them by debug
  name, as it does every other mission.

So "is it on offer" is not server-side after all: it is in the log, per
session, which is a better answer than the file's - it is the offer list
*as this account saw it*.

## The page

Jobs → Wikelo. Every live trade as a card - the retired seventeen are counted
and hidden - with what it wants ticked against stash sightings by the same rule
the Jobs page uses: presence, never a count, and the place it was seen. A rank
gate is stated as a fact about the trade, since the pilot's standing is not in
the logs. **Track as a goal** turns the requirements into a shopping list
(`source: wikelo:<debugName>`, one per trade), which then behaves like any
other list: held marks, prices and shops from UEX, pin it to Now and the MFD.
