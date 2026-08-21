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
| What a given kiosk buys or sells | **No.** Not in the DataCore |
| Naming a commodity in our own sale log | **No.** The id does not resolve against anything here |

The last two most likely live in `Data\ShopInventories\*.json`, which ships
encrypted. Reading what CIG leaves open is one thing; circumventing a protection
measure they deliberately applied is another, and it would break on any key
change besides. Still not planned.

That leaves exactly two honest options for "where can I sell this":

1. **An opt-in network lookup** (UEX or similar), off by default, clearly
   labelled, and never contacted unless the user turns it on. It would also give
   live prices, which is the part players actually want.
2. **Nothing**, and the Cargo view keeps reporting what is provably known.

## If the catalogue is wanted

Parsing 135 records out of a 315 MB file needs the DataForge structure walked
properly — the string tables are readable without it, but the category and the
display name of a record are not reliably recoverable from loose strings. That
is a real piece of work: header, struct and property definitions, then records.
The header is understood (above), which is the part that usually stops people.

Worth doing only if the catalogue is worth having on its own, because it will not
lead to the shop mapping. It does not exist in this file.
