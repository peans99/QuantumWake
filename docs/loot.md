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

## Not yet

**Named places.** A preset is named for the kind of place that uses it, and
the page shows it as that - "Distribution Centres · High Security", "Derelict
Outposts" - never as a location. Which location uses which preset is in the
object containers: a probe on 2026-10-31 found 9,668 `.socpak` under
`Data\ObjectContainers` (4,844 MB), each a zip whose `entdata/*.entxml`
overrides name a `subConfigRef`/`multiConfigRef`. Across 8,292 PU socpaks,
605 hold 876 such overrides pointing at 73 presets, the commonest
`cave_sand_medium_multiconfig` (158). Tying a socpak to a name the star map or
the logs use needs the `ChildObjectContainers` graph walked upward, and the
scan took 45 minutes single-threaded reading whole files - so it has to be
digested once per build, in parallel, reading only `entdata`.

**What NPCs wear.** loot-verse also lists NPC outfits; those are loadouts,
not loot tables, and are not read.
