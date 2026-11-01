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

## NPC ships

Added 2026-10-31 in 0.20.4. The reader is `Core/GameData/GameShipLoot.cs`; its
answer is cached in the loot digest, keyed by part.

The ships the game flies as NPCs are their own entity classes, named for who
flies them - `AEGS_Gladius_PU_AI_CRIM`, `MISC_Starfarer_PU_AI_CIV`,
`DRAK_Caterpillar_Pirate`. 642 match on this install; 637 carry their
loadout in the DataCore as port-by-port `SItemPortLoadoutManualParams`
entries, nested where a gun sits on a mount or a missile in a rack. Leaving
out Arena Commander's `_EA_` variants, **614 NPC ships carry 413 kinds of
part** worth taking, in 5,860 ship-and-part pairs:

| part | kinds |
| --- | --- |
| missile racks | 101 |
| guns | 90 |
| power plants | 49 |
| coolers, shields | 42 each |
| quantum drives | 27 |
| radars | 24 |
| missiles | 21 |
| quantum interdiction, EMP | 5 each |
| mining lasers | 3 |
| tractor beams, salvage heads | 2 each |

Seats, screens, thrusters, fuel tanks, armour and controllers are part of the
hull and are left out, as are ship-specific turrets and decoy launchers.

**Who flies it is read from the name**, and only the faction is kept: `CRIM`
criminal, `CIV` civilian, `UEE` the Navy, `NT` Nine Tails, `PIR` pirate,
`SEC` security, `HH` Headhunters, `CFP` Citizens for Prosperity, `FF`
Frontier Fighters, `Super` elite. The rest of a name - `QIG`, `FullFuel`,
`NoCargoGridMarkup`, `DefendShip` - is how the variant is set up, and listed
beside the faction it buried it. A template or a name with no faction token
reads "NPC".

**There are no odds here.** The files say what each variant carries as
standard; how often each one is spawned is decided by encounter and mission
data this does not read. The page says so.

**Nothing to check it against in these logs.** Of every item class the logs
listed in any inventory, of any scope, **none is a ship part**: the game logs
no inventory listing when a component is taken from a ship. Unlike the crates,
this is unchecked.

## What NPCs wear

Added 2026-10-31 in 0.20.5. The reader is `Core/GameData/GameNpcOutfits.cs`;
its answer is cached in the loot digest, keyed by item.

**The DataCore names NPC kit only by reference.** Of 2,312 actor classes with
a default loadout, 1,411 NPC archetypes use outfit loadouts (a base plus
alternatives picked by tag) and others random assortments
(`SLoadoutAssortment`, 321 records - a Hurston guard's lists body groups of 90
and 64, three armour sets and one weapon kit). Almost every leaf is an
`SItemPortLoadoutXMLParams` naming a file under `Data\Scripts\Loadouts`:
73,767 XML leaves against 9,260 items written into the DataCore, and 1,864 of
the 2,312 actor classes name no item in the DataCore at all.

**Those files are encrypted, and are now read.** Their payloads do not start
with the ZStd magic; 400 of 400 sampled failed the ordinary read. They are
AES-128-CBC with a zero IV over an ordinary ZStd frame padded to the block
size, under the key unp4k published (see docs/credits.md). Decrypted, the
first four bytes are `28 B5 2F FD` and the frame inflates to exactly the size
the directory records - 1,072 bytes for `Lorville_Guard_Heavy.xml`, which is
a CDS legacy heavy set in Hurston Security colours over a CDS undersuit.
Decryption is opt-in, through `P4kArchive.TryReadDecrypting`, used for these
files only; Nicolas decided on 2026-10-31 to read them. The padding is why
the frame is streamed to the recorded size rather than unwrapped whole.

| | count |
| --- | --- |
| kinds of item worn or carried | **2,571** |
| NPC classes wearing something | **2,129** - 778 kinds once gender and variant number are dropped (32 test rigs left out, see below) |
| NPC-and-item pairs, cached | 27,286 (adds ~2.3 MB to `commodities.json`) |
| read time, all 2,522 files | 1.9 s |

Kept: armour, clothing, personal weapons, attachments and magazines,
consumables, backpacks. Bodies, heads, hair and the mobiGlas are the
character, not loot. 50 item classes the loadouts name have no entry in the
item catalogue and are dropped with them.

**The share is an estimate.** Each NPC's tree is walked sharing weight evenly:
every outfit an outfit loadout offers, every assortment, every group sharing a
slot name (male and female bodies), every loadout in a group. Different
slots (body, outfit, weapon) all apply. A spawner picks by tag and seed, which the
files do not show, so "50%" means "one of two outfits it can be given". The
page heads the column "Share (est.)" and says so.

**Checked against the logs.** A body is a container in the logs, so what NPCs
wear should turn up in container listings. Of the 130 classes the logs saw in
a container, **46 are worn by some NPC**, and of the 34 that are armour, 20
are. Nothing the tables could not explain is explained by outfits instead -
the four from neither are still the goldfish and the three ship models - so
this adds sources rather than closing gaps. It cannot say which of the 46
came off a body rather than a crate: the logs do not tell them apart.

## Farm my list

Added 2026-10-31 in 0.20.6. A checklist's **Where to get these** button sends
the list's open lines to `POST /api/loot/farm`. Each line is asked for by the
item or commodity it links to, or failing that by its own text, and a ticked
line not at all.

**A line becomes an item by name.** Every class the loot tables roll, NPCs
wear, NPC ships carry or a recipe makes is indexed by its display name and its
class, built once per game-data load. Where two classes share a name, the one
with a crate behind it wins. A line that matches nothing - a commodity, a typo
- is listed as unmatched rather than dropped.

**Each line** gets its best named place (or an unmarked spot when no place is
named), the NPC likeliest to carry it and how many kinds can, the number of NPC
ships that fly with it, the cheapest seller in the UEX prices when that feed is
on, and the craft time when the game has a recipe that makes it.

**The basket** (`Core/GameData/LootBasket.cs`) ranks places for the whole list:
first by how many of the list's items a place can yield, then by their
per-slot chances added up - the expected number of list items per crate slot
opened, if each slot is one draw. Unmarked spots that yield the same items at
the same odds are folded into one row naming their bodies.

**Ties are the norm, and the page says so.** On a five-item test list on this
install - Gallant rifle, FS-9 magazine, the Hurston Balor helmet, a CF-337 and
a MedPen - **150 places tie for best**, covering four of the five: the same
`V3SlotPreset_Loot_Military` crate set stands at every ArcCorp security post
and processing centre and at unmarked spots on thirteen bodies. The CF-337 is
on no table at all; it comes off 224 NPC ships. The page shows ten and says
the rest are as good, rather than letting alphabetical order look like a
recommendation. The request takes about a quarter of a second.

Actor classes that are test rigs - `DummyPlayer`, `FeatureTest_*`,
`PU_Human-Test-*`, the "Behavior Zoo Testmap" vendors - were found on this
list (a "Dummy Player" was the MedPen's likeliest carrier) and are now left
out of the outfits.

## Where to buy it instead

Added 2026-10-31 in 0.20.19. The item panel and the checklist answer carry
two prices beside the places, when they know one. The cheapest counter in
the UEX prices, when that feed is on. And what this install was charged:
`LogLibrary.Receipts()`, the median per unit over confirmed purchases since
the last money wipe, with the shop of the latest. On this install the
P4-AR magazine reads 171 aUEC at Pyro Gateway from UEX, and 198 paid at
Casaba Outlet.

The game files were tried for a third, feed-free price and do not have one:
the encrypted shop tables are a 2018 economy (section 9 of
[untapped-signals.md](untapped-signals.md)), and the DataCore holds no
per-item price.

## Reference: one card per item and per ship

The Reference menu holds Ships, Items, Loot, Mining and Crafting. Any item or
ship opens in a modal (`/api/reference/item?id=`, `/api/reference/ship?id=`)
that joins every answer above. The item card reuses the Loot panel's own
answer, `LootWhere` in ServerHost, so the two cannot disagree. Every part and
ship in a card opens its own card, and Back walks the trail.

**Stock loadouts.** `GameShipLoot.ReadAll` walks every hull's default
loadout in the same pass as the NPC ships. It keeps the same part kinds:
guns, shields, coolers, power plants, quantum drives, radars, missiles and
their racks, mining and salvage heads, tractor beams, QIGs and EMPs. A player
hull's default loadout is the fit it is sold with. On this install the Aegis
Gladius comes with 11 kinds of part, and the CF-337 Panther Repeater comes
fitted to 68 distinct hulls, once Arena Commander copies, Teach and collector
editions are folded into their base hull by name. CacheVersion 58.

**NPC versions.** These are the NPC ship classes that extend the hull's class.
The longest hull wins, so `AEGS_Gladius_Valiant_PU_AI_UEE` is the Valiant's
and not the Gladius's. The Gladius has 12, including two Criminal ones,
`PU_AI_CRIM` and `PU_AI_CRIM_IK`; the card shows the rest of the class so
they read as two.

**Ships from the install.** The catalogue lists the community dataset's 288
hulls plus the install's vehicles it does not describe. There were 21 such
vehicles. 10 of them are props or mission hulks and are left out: probes,
orbital sentries, Arena Commander objectives, salvage wrecks, and the hijacked
Reclaimers and Caterpillar. That leaves 11, among them the four Constellation
Mk IV variants and the Skylark. Their spec sheet is blank, because only the
community dataset carries one.

**Drops column.** 5,840 of the 26,531 items the install describes can be found
somewhere: 5,304 in a crate or on a body, 2,571 on an NPC, and 413 on an NPC
ship. An item can be in more than one of these. The column shows the best
single roll, else the NPC count, else the NPC ship count.

## Not yet

**Boarding-mission loot.** The harvestables in
`PU\design\Ships\Mission\ShipBoarded` sit in mission containers no planet
places, so they have no place to name.
