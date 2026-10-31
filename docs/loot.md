# Loot: what the game's own tables say an item comes from

Started 2026-10-31 to give Quantum Wake the core of what
[loot-verse.com](https://loot-verse.com/) offers - for any item, where it can
drop and how often - read from this install rather than a website. Everything
below is from Alpha 4.10's `Game2.dcb` on this install, through
`dotnet run --project src\Quantumwake.Cli -c Release -- --loot`; run it again
after a patch and compare. `--loot=<item class>` lists every container and body
that can roll one item.

The reader is `Core/GameData/GameLoot.cs`, the arithmetic `LootRoll.cs`, the
cached digest `GameLootData.cs`. The Loot page reads it through
`/api/loot/where` and `/api/loot/search`, and `/api/loot` puts the best roll
beside each pickup.

## What is in the files

| | count |
| --- | --- |
| loot tables | **220** - 59 `LootTableV3Record`, 161 older `LootTable` |
| distinct items any table can roll | **5,665** (of 26,028 the catalogue holds) |
| things that roll a table | **2,230** - 2,091 container slots in 137 slot presets, 139 NPC and corpse classes |
| distinct table-and-constraints rolls | **333** |
| item-and-roll pairs, cached | 179,229 (adds ~5 MB to `commodities.json`, which reloads in 0.3 s) |

### The chain

A **V3 table** is a weighted list of entries; each points at an archetype by
record (126) or written inline (71). An **archetype** is another weighted list
whose entries *select* items, by tag (248 selectors) or by naming classes (32).
A tag selector is a DNF term: every positive tag present, no negative one.

**Tags count for their ancestors.** An entity's `tags` array on
`EntityClassDefinition` lists its own tags, and a selector asking for
`Pistol` must also find a magazine tagged with a child of `Pistol`. The first
reading matched flat and every ammo selector on the install came up empty -
`V3LootTable_Actor_Soldier` resolved to 2 items; walking the `Tag.children`
tree up, 12.

An **older table** is the same idea in other fields: weighted references to
`LootArchetype` records, whose primary entries each select by one tag plus a
DNF term, whose `excludedTags` strike items for that archetype alone, and whose
secondary groups ("Rarity") are bands the archetype draws itself. 767 of the
2,088 loot slots on this install still point at one, so both are read.

The **container** adds `LootConstraints`: a pool filter (`V3PoolFilter_Generic`
strikes faction and special-event gear) and secondary choices that draw a band
before an item - `V3SecondaryChoices_Rarity_Common` is Common 100, Uncommon 10,
Rare 1, Epic 0.1; the Rare layer reverses it.

### Where tables are attached

- **Slot presets** - `SubHarvestableConfigRecord` and
  `SubHarvestableMultiConfigRecord`. A multi-config is a list of tagged groups
  ("Weapons Rack LMG Common"); each group has an `initialSlotsProbability` and
  weighted `SubHarvestableSlot`s, each with a container and its own
  `lootConfig`. The same crate rolls food in one preset and rifles in another.
  Slots with no table are mineables and plants, which are the item themselves,
  and are left out.
- **Bodies** - any `EntityClassDefinition` carrying
  `LootGenerationComponentParams`: corpses (`Corpse_ShatteredBlade_soldier` →
  `GeneralLoot`), prisoners (14 classes → `V3LootTable_Actor_Prisoner`), and
  the named SOO bosses. `V3LootTable_Actor_*` is referenced from 38 records.

### Respawn

The older crates' `HarvestablePreset.respawnInSlotTime` reads 1,200 s (20 min)
on the outpost crates, and both multipliers (config and slot) are applied. The
V3 boxes name a `HarvestableSetup` instead, and **every one reads
`respawnInSlotTime` 0** - the "30M" in `V3HarvestableSetup_Lootbox_30M` is
`despawnTimeSeconds` 1,800, the despawn after a box is touched. The page shows
a dash and says the files give none rather than borrowing a number.

## Where: named places

Added 2026-10-31 in 0.20.3. The reader is `Core/GameData/GameLootPlaces.cs`;
its answer is cached inside the loot digest, keyed by preset.

**Placement is per instance, in the map files.** The 9,668 `.socpak` zips
under `Data\ObjectContainers` (4,844 MB) each hold an XML list of the
containers they place and an `entdata/*.entxml` per entity that carries
instance data. An entity that holds loot sets its preset there:
`HarvestableComponent` → `looseSubConfigBase` → `subConfigRef` or
`multiConfigRef`. Of 307 entity classes with a `HarvestableParams`, only three
carry a default preset - the Fleet Week 2022 shipping containers - so instance
data is the whole picture. 839 entities carry harvestable metadata, every one
with instance data.

**Names come from the planet's own list.** Hurston's container lists 5,188
children. The ones the star map shows sit under a `RastarLocationEntity` with
a `starMapRecord` attribute - the id of a `StarMapObject`, whose name the
English table gives - so `final_s1_dc_dpre_imf` is Dupree Industrial
Manufacturing Facility and `ugf_lgcy_structure_002` under `HDRSO-Olliff` is
that bunker. A module is walked up through whatever places it until it meets
a marker. One that reaches a body's container (`stanton1.socpak` is
`StarMapObject.Stanton1`) without meeting one is an **unmarked spot** on that
body: caves, on this install, nearly all of them. No body's own container
holds a preset, so nothing is lost by starting the walk one level down.

A multi-slot preset that lends a slot group to another preset by reference
lends its places too: the borrowed rows on the page are the same crates.

**Fast because of how the zips are stored.** Every socpak is held
uncompressed in `Data.p4k`. The first probe read each one whole, walking the
archive's 1.36-million-entry directory for every file, and took 45 minutes.
Located in one walk and opened as a window onto the archive, so the zip
reader seeks to the XML and the instance data and leaves the geometry on
disk, the whole scan takes **1.8 s** here. It runs once per game build with
the rest of the game data.

| | count |
| --- | --- |
| slot presets that roll a table and are placed | **51 of 137** |
| named places | **529**, plus unmarked spots on **20** bodies |
| items any table rolls that have a named place | **5,045 of 5,665** |
| item classes the logs saw in a container that have one | **118 of 130** |

**86 presets are set down by no map file.** The station presets
(`LootGeneration_SlotPreset_Station_*`), the older distribution-centre
multi-configs, the occupied-cave presets and the generic V3 ones
(`V3SlotPreset_Loot_Generic`, `_Criminal`, `_HighTech`, `_LowTech`) among
them. For 81 of the 86 the preset's id appears nowhere in `Game2.dcb` except
its own record; the other five are referenced once. They may be spawned by
missions - nothing read here can see that - or be leftovers. The page marks
their rows "not placed" rather than dropping them.

The map files also place plants, minerals and rock-cracker nodes through the
same mechanism - `Cave_Sand_Medium_MultiConfig` (158 instances) is entirely
FPS mineables, fossils and plants. Those roll no loot table and are not shown.

**Spots are a floor.** "Crate sets" counts the instances in the modules under
a place, each module once however many times the place repeats it.

## What is assumed

The files hold weights, not the draw. `LootRoll` reads them as weighted picks
at every level and a uniform pick at the bottom, and three choices follow -
each the reading under which every table on this install sums to one:

1. **Anything with nothing to draw from is skipped** and its weight goes to the
   rest. The armour tables give 26 of 32 parts of weight to "Mission item"
   slots selecting `ArmourMissionItem_*` tags no item carries - a mission
   plants the item. Counted, a small armour box would roll nothing 81% of the
   time. They are listed as unfilled so a patch that breaks a tag shows up.
2. **A rarity band with no member in the pool is skipped.** One consequence is
   visible: every pistol is tagged Common, so the Pistols choice gives the S-38
   1.755% a roll in a Rare, Uncommon and Common weapons box alike - the rarity
   layer only moves weight between bands that have members.
3. **Per roll, not per crate.** How many draws a container makes depends on
   its size and `fullnessFactorRange` (0.2-0.8 on outpost crates, 0.1-0.167
   and a limit of 5 on V3 weapon boxes). That is not modelled, and the page
   says the chance of finding it is higher than the number.

## Checked against something it did not read

The logs list items out of `Container`-scope inventories - crates, bodies and
the pilot's own packs mixed together - 130 classes on this install. **126 of
them are reachable from some table.** The four that are not are
`GoldFish` and three `Flair_Model_*` ship models: hangar decorations, not
loot. Before the tag tree was walked the same check read 104 of 130, which is
what found the flat-matching mistake.

All 49 items on the Loot page's first-seen list carry a best roll.

**The places, against where the pilot went.** The logs never say where a
crate stood, but they do say where a course was last set. Of the container
pickups that followed a quantum course set within two hours, 117 were of
items with places. **13 followed a course to a planet or moon - 11 to Pyro I,
one each to Daymar and microTech - and all 13 are items the map files place
on that body.** The other 104 followed a course to a rest stop, a city, a
jump point or a party member's marker, where what gets browsed is the pilot's
own storage - and the station presets are among those no map file places.
Thin, and a check on bodies rather than buildings, but nothing contradicts.

## Not yet

**Ship loot.** Parts tractored off NPC ships come from loadouts, and the
boarding-mission harvestables (`PU\design\Ships\Mission\ShipBoarded`) sit in
mission containers no planet places. Neither is read.

**What NPCs wear.** loot-verse also lists NPC outfits; those are loadouts,
not loot tables, and are not read.
