# NPC encounters and what their holds carry, from the game files

Measured on 4.10.2 against this install. Every figure below is printed again by:

```powershell
dotnet run --project src\Quantumwake.Cli -c Release -- --encounters --data <scratch folder>
```

Point `--data` at a scratch folder: it writes a game-data cache there, and the
running app's cache is not this build's to replace.

The logs never mention an NPC ship's cargo (freight in the log is loading-platform
noise; see [untapped-signals.md](untapped-signals.md) section 6), so nothing here is
checked against play. It is what the files say a ship is spawned with.

## The records

An `AIWaveCollection` is a group of NPC ships spawned in waves. It holds `waves[]`,
each an `AIWave` with an `id` and `members[]`. Each member is an `AIWaveMember`:

| Field | What it holds |
| --- | --- |
| `entityClassDefinition` | The ship, `DRAK_Cutlass_Black_PU_AI_NT`. Empty on 112 of 731 members, which name only who flies them. |
| `name` | A text key for that faction, `@Factions_CrusaderSecurity_DisplayName`. |
| `amount`, `minAmount`, `maxAmount` | How many. `-1` is "not set"; most members give a plain `amount`. |
| `skillsetOverride` | A `SkillDefinitions` record, `HumanPilot60`. |
| `cargoManifests` | The `CargoManifest`s its hold may be spawned with. |

A `CargoManifest` holds `cargoFillCapacity`, a pointer to `CargoFillCapacityValue_Even`,
`_Random` or `_Custom` with `minCapacityRange` and `maxCapacityRange` (a share of the
hold) and `resources[]`, each a `ResourceType` and a `probability`.

On 4.10.2: **184** collections, **731** members, **370** members in **88** collections
carry a manifest, and **82** manifests exist.

## What the holds carry

Only **17** of the 82 manifests are carried by any member. All of them fill 1 to 25%
of the hold:

- Civilians: processed food, medical supplies, agricultural supplies.
- Criminals and Nine Tails: distilled spirits, some processed food.
- Security: two manifests that name no commodity at all.

The page turns a share into SCU with the hold the install gives the hull: the class's
own cargo grids, else those of the longest player hull the class is built on
(`DRAK_Cutlass_Black_PU_AI_NT` on `DRAK_Cutlass_Black`, never on `DRAK_Cutlass`). The
community dataset's capacity is used only when the install has a grid for neither,
and the page says so. The fill times the hold is rounded down, since a hold is loaded
in whole SCU.

| Ship | Hold | Fill | Up to |
| --- | ---: | ---: | --- |
| Caterpillar, Nine Tails | 576 SCU | 25% | 144 SCU distilled spirits |
| Reclaimer, Nine Tails | 420 SCU | 25% | 105 SCU distilled spirits |
| Starfarer, civilian | 291 SCU | 25% | 72 SCU processed food |
| Constellation Andromeda, Nine Tails | 96 SCU | 25% | 24 SCU distilled spirits |
| Cutlass Black, Nine Tails | 46 SCU | 25% | 11 SCU distilled spirits |

Of the 57 ship kinds that carry a manifest, **34** have no cargo grid in the install:
the Gladius, Buccaneer, Vanguard, Arrow, Hawk, Sabre, M50, Mantis and the like. A
manifest on a hull with no hold has nothing to fill, and the page says that rather
than giving it a figure. Of the 370 carrying members, 130 take their hold from the
install, 12 from the community dataset and 228 have none.

## The 65 nobody carries

**65** manifests are carried by no member. A raw scan of the DataCore for each one's
id, stored the way a record id is, finds it once: its own record. The one other field
whose type can name a manifest, `CargoControllerParams.cargoManifestOverride`, is empty
on all **1,144** entities that have one. A search of the 4,176 Subsumption scripts,
`Data\Scripts` (4,277 files, decrypted) and the rest of `Data\Libs` (8,565) finds the
names only in `TagDatabase`.

| Prefix | Count | Fill | What |
| --- | ---: | --- | --- |
| `LegalCargo` | 19 | 0 to 100% | Metals, gases, Molina filters and mould samples |
| `MixedCargo` | 14 | 0 to 100% | Nitrogen, chlorine, spirits, food, DCSR2 |
| `IllegalCargo` | 13 | 0 to 100% | WiDoW, Altruciatoxin, stims, Revenant tree pollen |
| `BlockadeRunner` | 7 | 14 to 95% | Zeta-Prolanide, one per hull |
| `Civilian` | 5 | 1 to 25% | As the carried civilian lists |
| `ShipHeist` | 4 | 0 to 100% | No commodity named |
| `Criminal`, `Pyro`, `CargoTest` | 3 | | One each |

They are probably handed out by the server. Their names pair a hull and a fill, which
is a reading of the name, not a link, so the page lists them apart and attaches them
to nothing.

## Which mission spawns an encounter

Nothing in the DataCore points at a collection. The same raw id scan finds each of the
184 once, no field's type is `AIWaveCollection`, and no contract, template or broker
string names one. What does name them is the mission scripts: the Subsumption CryXml
files under `Data\Libs\Subsumption`, which carry ids as text. The reader collects
every id-shaped run in each file once and looks it up, which takes under a second for
the 4,176 files.

**48** collections are named by a script:

| Script folder | Count | What |
| --- | ---: | --- |
| `Missions/EnvironmentalMissionScenarios/LawEMs` | 32 | Stanton law missions: wanted policing, security patrols per area, the bounty hunters sent after a wanted player, Pirate_Buccaneer and Pirate_Cutlass_Black |
| `Missions/AC/AC_Swarm` | 11 | Arena Commander's swarm modes |
| `Missions/EnvironmentalMissionScenarios/SB_CombatAssist_NPCRequest` | 4 | Service beacon combat assist |
| `Missions/EnvironmentalMissionScenarios/FreighterDerelict` | 1 | Pirate_Interdiction_Single_Medium |

A search by name instead of id finds the same 48.

**136** are named by no script. 12 collections are filed under `ea/`, Arena Commander;
11 of those are scripted, so the universe has **172**, of which **37** are scripted and
**135** are not. The unscripted include every bounty (15), assassination (14),
recovery (12) and steal (12) wave, the pirate raids, the reinforcement waves, the lone
faction ships, Fleet Week, the Nine Tails Lockdown and the convoys. A search of
`Data\Libs` and `Data\Scripts` for the prefixes a script would build their names from
(`Bounty_Wave`, `Steal_Wave`, `Pirate_Battle` and so on) finds none, so they are
picked on the server.

For those the files give two things, and the page shows both under their own labels:

- **The folder the game files the record in**, below `aiwavecollection/`:
  `pu/mission/ai_bounty`, `pu/reinforcement/pirate/ninetails`, `pu/faction/single/...`.
  It says what kind of work an encounter belongs to, not that a contract spawns it.
- **What its name says**: `Bounty_Wave_Hard_Stanton2` is "hard · Crusader".
  `Stanton1` to `Stanton4` are Hurston, Crusader, ArcCorp and microTech, the game's own
  designators (see [GameClimates](../src/Quantumwake.Core/GameData/GameClimates.cs)).

Your logs name a bounty contract by generator and difficulty, which matches these
names only loosely, so the page does not join them.

## Where it shows

- **Reference → Encounters**: every collection, filtered to the universe by default,
  with its folder, the mission script that names it or what its name says, waves,
  ships, who flies them, and the cargo the whole group carries at its fullest, priced
  at the best UEX sell when UEX is on. Open one for its waves ship by ship.
- **An NPC ship's Reference window**: the encounters that spawn it, the ones a script
  names marked, and its hold under each list it is spawned with.

The reader is `GameEncounters` (cached on `GameLootData.Encounters`, cache version
63); the endpoint is `/api/reference/encounters`.
