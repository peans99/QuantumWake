# What commodities can be sold where

Raised 2026-08-21 by nekron. Route 1 — write a DataForge reader — was tried the
same day. This page is now the result rather than the plan.

**The short answer: half of it is possible offline, and the interesting half is
not.** The commodity catalogue is in the DataCore and can be read today. Which
kiosk trades which commodity is not in there at all, and neither is the join that
would name a sale in our own logs.

The related finding — that a cargo sale cannot be named from the log alone — is
in [commodity-names.md](commodity-names.md).

## What was done

`Data\Game2.dcb` was pulled out of `Data.p4k` with the existing `P4kArchive`,
no external tool involved, and examined in four passes. It is **330,491,142
bytes**, unencrypted, and its header reads cleanly: version 8, then the
definition counts — **6,685 structs, 23,722 properties, 772 enums, 116,512
records**.

So the community's account of the three protection layers is accurate in
general, and beside the point here: on this install the DataCore is open.

## 1. The commodity catalogue is there

**135 commodity records**, named and categorised:

| Category | | Category | | Category | |
|---|---:|---|---:|---|---:|
| minerals | 21 | manmade | 12 | agriculturalsupplies | 4 |
| metals | 17 | gas | 7 | halogens | 4 |
| vice | 17 | processedgoods | 5 | food | 3 |
| natural | 16 | alloys | 4 | counterfeit | 3 |
| consumergoods | 12 | medicalsupplies | 3 | scrap | 2 |
| mixedmining | 2 | non_metals | 2 | waste | 1 |

Real names, not ids: `aphorite`, `bexalite`, `dolivine`, `agricium`,
`quantumfuel`, `rmc`, `sunsetberry`, `altruciatoxin`. This is a usable reference
table and it costs nothing but a parser.

Shops are represented too, but only as **brands**: 58 kiosk manufacturer records
and 58 brand styles — CenterMass, Casaba, Astro Armada, Cordry's. The app
already resolves those from the localisation table.

## 2. The kiosks are not

The log names shops as `SCShop_OmegaPro_NewBabbage` and
`SCShop_Admin_lt_base_g`. The DataCore contains **zero strings beginning
`SCShop`**. The only location-flavoured shop records are seven UI map section
definitions (`shop_admin`, `shop_centermass`, `shop_wallys` and four more),
which are map furniture, not stock lists.

There is no shop→commodity table in this file. Nothing to parse harder for.

## 3. The join is not there either

Every `resourceGUID` this install has ever logged was extracted — **13 distinct
ids across 146 log files** — and each was searched through the whole DataCore in
three forms: ASCII text, little-endian bytes, big-endian bytes.

**None of the 13 appears, in any form.** That settles the question the earlier
note left open: the ids in the sale log belong to a different numbering from
anything the DataCore holds. (The earlier note said four ids; the true figure
across every backup is thirteen.)

## 4. There is no second copy

The DataCore is compiled from source records, so the archive was checked for
those too — `Data\Libs\Foundry\Records\...` in both slash styles and both cases,
five root spellings. **All misses.** The compiled database is the only copy in
the archive.

## Where that leaves it

| Want | Possible offline? |
|---|---|
| A catalogue of every commodity, by category | **Yes.** 135 records, names and all, from the user's own install |
| Which brand a kiosk belongs to | **Yes**, and already done |
| What a given kiosk buys or sells | **No** — not in the DataCore. Community data has it; see below |
| Naming a commodity in our own sale log | **Not offline** — solved via the opt-in community dataset; see below |

The last two most likely live in `Data\ShopInventories\*.json`, which ships
encrypted. Reading what CIG leaves open is one thing; circumventing a protection
measure they deliberately applied is another, and it would break on any key
change besides. Still not planned.

## The break: the community already resolved the join

Checked the same day, at nekron's prompting: **StarCitizenWiki/scunpacked-data**
carries `resources/commodities.json` — 243 KB, regenerated after each game
patch — and it resolves **every resourceGUID this install has ever logged**.
All of them, tested, not sampled: the sales were DynaFlex, Waste, Tin, Stims,
Medical Supplies, Iron, Copper, Aluminum, Nitrogen, Hydrogen, Hephaestanite and
a Year of the Rat Envelope.

Seven further byte-order permutations were tried against the DataCore first
(CryEngine's CigGuid has its own layout) — all misses, so the id genuinely is
not recoverable from the local install. The community file is the only source.

**Shipped as an opt-in.** The repository carries no licence and the data is
CIG-derived, so it is not vendored into this repository or the binary — that
decision is not ours to make. Instead the Cargo page offers a button that
fetches the one file into local app data, with the source named and the promise
stated: it is the only network request the application can make, and it never
happens without the click. `CommunityData` in the Data project holds the
mechanics; the README's network claims carry the exception.

## Still open: what sells where, on the map

The same repository has `resources/commodity_trade_locations.json` (27.9 MB):
per commodity, the facilities that buy and sell it, with class names like
`DC_Stan_Hurston_S1_Farnesway_CargoShop` that our resolver's grammar can meet.
That is the map-enrichment half of nekron's ask — "sell Waste here" markers —
and it is a second, larger piece of work: download opt-in alongside the first
file, parse, and join facility class names onto atlas nodes. Parked until the
naming slice has settled.

## If the offline catalogue is still wanted

Parsing 135 records out of the DataCore needs the DataForge structure walked
properly. The header is understood (above), which is the part that usually
stops people. But with the community file resolving ids for anyone who opts in,
the offline catalogue would only serve those who decline — worth doing someday
for completeness, not first.
