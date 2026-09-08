# Game2.dcb — what is actually in there

Explored 2026-08-28 against build 12519617 (4.10.191.2241), and since built on:
the reader described here is `Quantumwake.Core/GameData/DataCore.cs`, and it is
what lets the app name your cargo, list 26,028 items, read 1,606 crafting
recipes and describe every deposit table without downloading any of it.

It began as a survey in a separate tool. That tool has since moved in, so this
is kept as the record of how the format was worked out - the wrong turns
included, because they are the expensive part.

## Getting it out

It is one entry in `Data.p4k`, and the existing reader handles it unchanged:

| | |
|---|---|
| entry | `Data\Game2.dcb` |
| method | 100 — ZStd, same as everything else |
| compressed | 29,718,272 (28.3 MB) |
| uncompressed | **331,435,556 (316.1 MB)** |

The ZIP64 sentinel reads 4,096 MB, which is what made this look prohibitive at
first glance. It is not: 316 MB extracts in seconds and fits in memory.

## What it contains

Roughly 505,000 printable strings. The readable skeleton is a set of record
paths, `libs/foundry/records/<area>/...`, and the areas say what is modelled:

| area | records | why it matters |
|---|---|---|
| entities | 26,777 | every item, ship and prop |
| ui | 5,835 | |
| **missionbroker** | **2,584** | the missions, keyed by debug name |
| actor | 2,508 | |
| **missiondata** | **2,471** | |
| starmap | 2,073 | |
| **crafting** | **1,892** | what `is_craftable` is derived from |
| scitemmanufacturer | 1,161 | |
| harvestable | 899 | |
| **contracts** | **654** | |
| inventorycontainers | 586 | |
| **reputation** | **530** | standings and scopes |
| **lootgeneration** | **528** | the loot tables themselves |

`ShopLayout`, `ShopCatalog` and `shopinventory` appear as type names, so shops
are modelled — but whether the per-terminal stock lists are in here or served
from CIG's backend is unresolved, and it is the single most valuable open
question. If they are here, purchasability stops being a floor over UEX and
becomes a fact.

## The mission join, which is why this was opened

The API gives `reputation_amount` and `has_blueprints` for 1,786 missions but
nothing to tie them to the game's text. Matching on title gets **1 of 300**;
matching on `debug_name` gets **0 exact, 13 partial**, and those land on
description text rather than titles.

The blob has the missing side. `PU_Delivery_Local_DrugProduction_Stanton4_KlimIntro`
is present, as `MissionBrokerEntry.<debug_name>`. Its *title* is not — because
titles are localisation references, so the record points at a key and
`global.ini` holds the English. That is the chain a reputation tag needs:

```
MissionBrokerEntry.<debug_name>  ->  title reference  ->  global.ini key  ->  the text
                                 ->  reputation amount
```

This is how StarStrings can tag contracts with `[150 Rep]` and we cannot: they
work from extracted game data, we were working from an API that had the numbers
but not the join.

## What it would take

String-searching gets the skeleton. Reading a *value* — a reputation amount, a
shop's stock list — needs the whole format: header counts, struct definitions,
property definitions, enum tables, the typed value arrays, and the record
instances that index into them. The format is documented by community work
(scdatatools, DataForge); it is a known job, not a research project, but it is
a few hundred lines before anything useful comes out, and it moves with game
versions.

## The decision, 2026-08-28

Game files plus UEX, and nothing else.

Shop stock settles it. It is **not** in the game files: `SCShop_*` - the shop
names `Game.log` records - appear zero times, and `ShopLayout`, `ShopCatalog`
and `ShopInventory` exist only as type and UI names with no records behind
them. The shop hits are NPC shopkeeper actors. Stock is server-side, like
prices, which is why UEX exists at all.

So "is it sold" is permanently an outside fact, and everything else comes from
the installed game:

| | from the wiki API | from game files + UEX |
|---|---|---|
| requests per sync | 123, to a volunteer project | **1**, to `items_prices_all` |
| structural data | a derived copy | the installed patch |
| staleness | until the next release | none |
| wiki disappears | dead | unaffected |

The heavy dependency is the one that goes: 12,296 full item records, 37 fields
each, from a project with no bulk endpoint that refuses a default user-agent.
What remains is a single call to the one service built to answer it, which
Quantum Wake already makes.

Without any network it still works, on loot, craft, size, class and grade. Only
the `*` needs UEX, and a player's own receipts can stand in for it.

Extraction is a release-time job: `gloss extract` reads the local install, the
result is published as `facts.json`, and users who would rather not extract get
the published file.

## Reading it: where the strings are

Finding the text does not require trusting a guessed header layout, which is
just as well - assuming the text follows the header directly finds nothing,
because the definition arrays sit in between.

Scanning for contiguous runs of printable-or-null bytes finds exactly two
regions over 100 KB, and both contain known item classes and record paths:

| offset | bytes |
|---|---|
| `0x002C59A6A` | 14,950,072 |
| `0x0023613CA` | 9,406,110 |

Header words 28 and 29 hold 17,165,925 and 7,190,252 - the declared lengths of
two text sections, near enough to the scanned regions to be the cross-check a
real reader should use rather than scanning.

Validate against something known rather than against the format: this install
has looted `gmni_lmg_ballistic_01`, so a parser that cannot find that string has
not found the text section. The 109 looted classes and 147 bought ones are the
test set for everything built on top.

### Solved: the header, and why guessing failed

Guessing the layout gets nowhere and fails in a way that looks like success —
offsets land *inside* strings and produce plausible fragments. Requiring a name
to start at a string boundary took a search that scored 40/40 down to zero
matches, which is how the guesses were caught.

The format is published. unp4k's `unforge` carries it, and the header decodes
this file exactly:

| | |
|---|---|
| file version | **8** — so record definitions are 36 bytes, not 32 |
| struct / property / enum / record | 6,694 / 23,788 / 774 / 116,921 |
| text | 17,165,925 bytes at `0x23613CD` |
| blob | 7,190,252 bytes at `0x33C0232` |

Sections start at `0x78` and run struct, property, enum, mapping, record, then
the typed value arrays, then text, then blob. **Two traps, both silent:**

1. **There are two string tables.** Names come from the *blob*; file paths come
   from the *text*. Reading a name out of the text table lands mid-string.
2. **The header declares value counts in a different order from the one the
   sections are stored in.** Booleans are declared sixth and stored ninth. Get
   that wrong and every offset after it shifts.

### Verified

`DataCore.cs` reads it, and the check that matters is against this install
rather than against the format:

| | |
|---|---|
| records read | **116,921** |
| **looted classes resolved** | **109 of 109** |
| struct names | real type names — `ActivityBehaviorRequestCondition` |
| commodity records | 135, as `EntityClassDefinition` under `entities/commodities/` |

Record areas: 27,127 entities, 24,024 dialoguecontextbank, 18,878 tagdatabase,
2,584 missionbroker, 2,471 missiondata.

### Solved: the resource GUID, and the byte order that hid it

**All 203 commodity GUIDs the community dataset provides resolve in the blob.**
The logged `resourceGUID` is a record hash after all.

This was nearly recorded as a dead end, and the reason is worth keeping. Read as
a plain .NET GUID, Aluminum's record gives
`bbef43d2-080a-48c7-4043-ed2183691a90` — while the logs and the dataset say
`48c7080a-bbef-43d2-901a-698321ed4340`. Same nibbles, regrouped. That is
convincing enough to look like a different id space, and searching the whole
316 MB file for the little-endian and big-endian forms returns zero, which
appears to confirm it.

It is stored as the **big-endian GUID with each 8-byte half reversed** — neither
layout anybody tries. The tell was not the byte search but the clustering: every
parsed hash carried `4000` or `4100` in the same position, and real GUIDs do not
cluster.

| | |
|---|---|
| dataset commodities | 203 |
| resolved in the blob | **203 (100%)** |
| names matching exactly | 123 |

The other 80 differ only in presentation — `Ore_Agricium` against the dataset's
`Agricium (Ore)` — which is formatting the dataset applies. `global.ini` already
carries the display names under `items_commodities_*`.

**Quantum Wake's 110 MB dependency is breakable.**

### Where commodity names actually live

Joining GUID to class name and looking the class up in `global.ini` gets **116
of 203** — every commodity with an `items_commodities_<class>` key. The other 87
have no key under any family matching their class name, and trying `item_Name`
as well adds nothing.

They are `ResourceType` records, and the name is a property:

```
ResourceType:
   type=0x000D  displayName      <- 0x000D is varLocale, a localisation reference
   type=0x000D  description
   type=0x0110  densityType
   type=0x000A  defaultThumbnailPath
```

`ResourceType.Aluminum` is the record whose hash matched the logged GUID, so the
full chain is:

```
resourceGUID  ->  ResourceType record  ->  displayName (locale)  ->  global.ini  ->  the text
```

Struct properties read correctly - the parent chain has to be walked root-first,
because inherited fields are laid out before a struct's own and reading them in
declaration order misaligns everything after the first.

### Instance values, and the check that proves the whole chain

Instances are laid out one struct at a time in data-mapping order, each block
being count x that struct's own instance size, which the struct definition
carries. There is one mapping per struct, which is why the header declares the
same number of both.

That gives the reader its best self-check, and it passes exactly:

```
data offset    61,454,622
data total    269,980,934
sum           331,435,556   = the file length, to the byte
```

Every section size, value-array width and struct size has to be right for that
to land. One wrong number anywhere upstream moves the total by exactly that
amount, so this single assertion validates the entire chain.

Reading a field means walking the struct's properties in layout order and adding
each one's width — four bytes for a string, locale or enum, since those are
offsets rather than text; eight for any array, whatever it holds; twenty for a
reference. An inline class ends the walk rather than being skipped by a guessed
width, because a wrong width there reads a neighbouring field and returns
something that looks like an answer.

### Arrays, pointers, and the check that grades the whole width table

An array property stores a count and a first index, not the items; the items sit
in the value array for its type. A strong pointer is eight bytes — a struct index
and which instance of it — so following one reaches another record's fields.

The obstacle was inline classes. A property of type `0x0010` is a struct laid out
in place, and its width is that struct's size, carried in the property's own
`index` field: `defaultEditorColor` points at `RGB`, which is 12 bytes. Without
that the field walk has to stop at the first inline class, which is two fields
before `Components` on every entity in the game.

With it, a struct's fields can be summed and compared to the size it declares:

```
EntityClassDefinition declares 66
4 + 4 + 1 + 1 + 12 + 20 + 8 + 8 + 8  =  66
```

Run across the file, **5,862 structs add up and none does not**. That grades
every width at once — a single wrong one would fail on every struct using that
type — and it is worth keeping as a permanent assertion rather than a one-off.

The Gladius then yields 65 components, `VehicleComponentParams` and
`SCItemPurchasableParams` among them, which is where ship specifications live.

### Item facts, checked against the API they replace

`SItemDefinition` carries `Type`, `SubType`, `Size`, `Grade` and `Manufacturer`
— the whole of what the wiki API serves per item. It is an inline class on
`SAttachableComponentParams`, which is one of an entity's Components, so getting
there needs the pointer array and the inline-class width together.

Read for every item and compared with the API's own answers:

| | |
|---|---|
| items read from the blob | **12,296** |
| size agrees | 12,257 (100%) |
| type agrees | **12,296 (100%)** |

`AbsoluteZero` reads `size=2 grade=2 type=Cooler` where the API says
`size=2 grade=B type=Cooler` — grade is the same value as an ordinal and needs
mapping to a letter, nothing more.

Enums store a four-byte offset into the text table rather than an index, so an
enum field yields the option's own name without a lookup table.

That is the last of it. Everything `facts.json` currently costs 123 requests to
fetch is in the install.

### Commodity names, end to end

```
resourceGUID -> record -> displayName -> global.ini -> the text

ResourceType.Aluminum -> @items_commodities_aluminum -> "Aluminum"
```

| | |
|---|---|
| dataset commodities | 203 |
| with a `displayName` in the blob | **203 (100%)** |
| resolving to English text | 178 (88%) |
| **disagreeing with the dataset** | **0** |

The 25 that do not resolve have a `displayName` key absent from the English
table — `@items_commodities_iron` among them, well-formed and simply not filled
in. None resolves to a *wrong* name. Two are not commodities at all: the dataset
carries `Life Support` pointing at a UI tab key and `Power:` at a scan readout,
which is noise in the dump rather than in the game.

Falling back to the record's own class name, spaced at word boundaries, closes
it completely:

| | |
|---|---|
| named | **203 of 203 (100%)** |
| identical to the dataset | 185 (91%) |
| worded differently | 18 — `Ore Agricium` against `Agricium (Ore)` |
| wrong | **0** |

On the commodities this install has actually traded, 12 of 13 come straight from
the English table and the 13th — Iron — from the fallback.

This is also why joining on class name only reached 116: a commodity's name key
need not mention its class at all. "Ace Interceptor Helmet" is
`ResourceType.AceInterceptorHelmet` pointing at
`@item_Name_basl_combat_light_helmet_02_01_01`. Reading *which* struct a record is and where its
fields live is done; reading the fields themselves needs the typed value arrays
and the data-mapping table.

### Ships: reachable in part, and the rest is derived not stored

`VehicleComponentParams` carries `crewSize`, `vehicleName` and
`vehicleDescription`, so a ship's identity and crew come straight out. What is
*not* there is the rest of what a fleet view wants: cargo capacity, SCM and max
speed, shield HP.

Those are not fields anybody forgot to look for — they are **computed**. Cargo
capacity is the sum of a ship's cargo grids, shield HP the sum of its fitted
shield generators, and speeds come from its thrusters and IFCS. The community
dump has them because scunpacked walks the loadout and adds them up.

So ship specifications are a different order of work from naming: not a lookup
but a reimplementation of somebody else's derivations, each of which can be
subtly wrong in ways no single assertion catches. That is why the app still
carries the dump for the Fleet page, and why the Settings copy says
specifications are "not in the game files in a form this app can read yet"
rather than claiming they are absent.

What a first attempt found, so a second does not repeat it. On the Gladius
template:

- `SHealthComponentParams.Health` reads **1**, not a hull HP. It is normalised;
  `VehicleComponentParams` carries a separate
  `vehicleHullDamageNormalizationValue`.
- `SEntityComponentDefaultLoadoutParams.loadout` resolves to
  `SItemPortLoadoutXMLParams`, whose `loadoutPath` is **empty**.
- `SItemPortContainerComponentParams.Ports` has a **count of 0**.

So the template carries no fitted parts at all, and cargo, shields and speeds
cannot be summed from it. There are many Gladius entity records — AI, pirate,
unmanned, Valiant variants — and 466 records with "loadout" in their path, so
the fitting is described somewhere; finding which record binds a ship to its
loadout is the next thread, not another field on the ship.

One structural note for whoever pulls it: `Ports` is an array of *inline
classes* (`0x0010` with a conversion type), not of pointers. Those are a third
array shape beyond the pointer arrays and plain fields already handled.

`ClassArrayAt` now reads them. A class array stores only a count and a first
index, and its entries are consecutive instances of the property's own struct.
The Starlancer MAX has 12 of them from index 34,103, reading as
`hardpoint_controller_fuel` and `hardpoint_air_traffic_controller` rather than
as noise — which is how the shape was confirmed rather than assumed. They are
utility hardpoints, though, not the weapons and thrusters a loadout means.

### Cargo: found, and found to be incomplete

Cargo capacity is not a field on a ship. It is geometry, and the geometry *is*
in the blob:

```
EntityClassDefinition.MISC_Freelancer_CargoGrid_Rear
  -> SCItemInventoryContainerComponentParams.containerParams   (a reference)
  -> InventoryContainer.MISC_Freelancer_CargoGrid_Rear
  -> interiorDimensions = 2.5 x 11.25 x 3.75 m
```

Those metres are 1.25 m cargo units — 2 x 9 x 3 — so that grid is 54 SCU. Grid
records carry the ship's own prefix, so summing every `<ship>_CargoGrid_*` needs
no geometry files. Three checks land exactly:

| Ship | Summed | Published |
| --- | --- | --- |
| Drake Corsair | 72 SCU | 72 |
| Aegis Hammerhead | 40 SCU | 40 |
| Freelancer DUR | 30 SCU | 30 |

And two do not. The Spirit C1 reads 32 against a published 64, and the base
Freelancer 60 against 66. Both are short by a whole grid, not by a rounding
error: the C1 by its one grid again, the Freelancer by its 6 SCU mid grid again.
**A grid record is a type, and a ship may place it more than once.** The blob
describes each grid exactly and never says how many of each a hull carries;
that count is in the object container files, which are not in `Game2.dcb`.

### And why the missing half is not coming

The count of each grid a hull carries is in the ship's own definition file, and
the DataCore names it exactly: `VehicleComponentParams.vehicleDefinition` reads
`Scripts/Entities/Vehicles/Implementations/Xml/misc_starlancer.xml`. All 171 of
those files are in `Data.p4k`.

None of them can be read. They are stored the same way as everything else -
method 100, ZStd - but their payloads do not begin with a ZStd frame:

| Entry | Method | Payload begins |
| --- | --- | --- |
| `AEGS_Gladius.xml` | 100 | `77 16 D7 91` |
| `DRAK_Corsair.xml` | 100 | `E7 07 93 B9` |
| `Game2.dcb` | 100 | `28 B5 2F FD` |
| `global.ini` | 100 | `28 B5 2F FD` |

The two files this project already reads start with the frame magic and the
vehicle definitions do not, which is what encryption looks like from the
outside. So this is not a parser that needs finishing: ship speeds, shield
totals and grid placement are deliberately closed, and no amount of work on the
DataCore reaches them. Written here so the next person does not spend the day
on `vehicleDefinition` the way this one nearly did.

What the blob does give for a ship, in readable form: its name, description,
crew size, career and role, its manufacturer, and every cargo grid it defines.

So cargo is two thirds solved and honestly stuck. Reporting the sum as a
capacity would be right for the Corsair and wrong by half for the C1, with
nothing in the data to tell them apart — which is the sort of number this
project does not ship. Records ending `_Template` read 35 x 35 x 35 and are
placeholders; they must be excluded or every ship comes out at 21,952 SCU.

## What came of it

All of the following now comes out of this file instead of a 110 MB download,
and each was checked against that download rather than assumed:

| | From the install | Agreement with the download |
|---|---|---|
| Commodity names | 342 | 203 of 203 named, 185 word for word |
| Item ids for pricing | 115,124 | 10,843 of 10,843 |
| Item catalogue | 26,028 | 10,843 of 10,843 exact on type, sub-type, size, grade |
| Crafting recipes | 1,606 | 1,575 of 1,577 craft times; found 3 the download had wrong |
| Place descriptions | 1,344 | 1,251 of the 1,294 shared, word for word |
| Deposit tables | 1,321 rows | fewer than its 2,642, so the download still leads there |

What it costs is a reader that has to be re-checked every patch, against a
format CIG do not document. The download is somebody else carrying that burden,
which is worth something, and it is still there for the two things this file
cannot answer: live prices, and the ship specifications the game encrypts.
