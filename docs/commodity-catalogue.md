# Parked: what commodities can be sold where

Raised 2026-08-21 by nekron, parked to come back to. This is the state of the
question and the routes out of it, written down while the detail is fresh.

The related finding — that a cargo sale in *our own logs* cannot be named — is in
[commodity-names.md](commodity-names.md) and is not repeated here. This page is
about the other half: a **reference table of which kiosk trades which commodity**,
which needs no logs at all, only the game's own static data.

## The three layers between us and it

| Layer | What it is | Where we stand |
|---|---|---|
| **Archive encryption** | `Data.p4k` is a ZIP64 container; CryEngine/Lumberyard encrypts some entries | Partly a non-issue. The localisation table is *not* encrypted and `P4kArchive` already reads it, which is where 9,527 item and 1,343 place names come from. Encrypted entries are reported, never guessed at. |
| **CryXMLB** | XML compiled to a binary format; opens as junk in a text editor | Not hit yet. The files read so far are plain. Community tools (`unforge`, `CryXMLConverter`) convert it, and the format is documented. |
| **DataForge** | `Data\Game2.dcb`, the record database — ships, items, shops, prices | **Readable.** 330 MB, unencrypted, extractable today with `P4kArchive`. It holds the commodity catalogue: `libs/foundry/records/entities/commodities/minerals/dolivine.xml`, `.../natural/sunsetberry.xml`, `.../scrap/scrap.xml`. |

The community's account of these layers is accurate, but on this install the
DataCore is *not* the wall. It is open. What we have not done is **parse** it —
every search so far has been a raw byte scan, which finds strings and misses
structure. A DataForge reader resolves records, enums, string tables and
pointers, and only then can a shop record be asked what it stocks.

## The routes, and what each costs

**1. Write a DataForge reader.** Parse `Game2.dcb` from the user's own install,
the way `P4kArchive` already parses the container. Offline, no redistribution, no
new dependency, and it either answers the question or proves the answer is not
in there. The format is community-documented and `ScDataDumper` is a working
reference implementation to check behaviour against.

*Recommended.* It is the only route that keeps every promise the README makes.

**2. Ship pre-extracted JSON** from `StarCitizenWiki/scunpacked-data`.
Fastest by far, and it directly contradicts our own `NOTICE`: *no game data is
contained in this repository, and none may be added to it*. It also redistributes
data derived from CIG's build. Rejected unless that policy changes deliberately.

**3. Fetch UEX or scunpacked at runtime.** Would give live prices too, which is
genuinely useful for trading and is what most neighbours do. It breaks "no
outbound network calls", which is the thing that distinguishes this app in
[landscape.md](landscape.md). Only ever as an opt-in that is off by default and
says plainly what it contacts.

**4. Decrypt `Data\ShopInventories\*.json`.** These are the shop stock tables and
are the most likely home of the `resourceGUID` mapping. They are deliberately
encrypted. Reading what CIG left open is one thing; circumventing a protection
measure they chose to apply is another, and it would break on any key change
besides. **Not planned.**

## What to try first, if this is picked up

1. Extract `Data\Game2.dcb` with the existing `P4kArchive` — already proven.
2. Parse the DataForge header: structure definitions, property tables, enums,
   string tables, then records.
3. Look for shop or kiosk records that reference commodity records, and for any
   id form matching the four `resourceGUID` values in the logs.
4. If the mapping is there, the Cargo view gains real names and a
   "where to sell this" reference, entirely offline.
5. If it is not, say so here, and the decision becomes route 3 or nothing.

Whatever comes of it, the rule from [architecture.md](architecture.md) holds: if
a name cannot be established, show what is provably known and stay quiet about
the rest rather than guessing from unit price.
