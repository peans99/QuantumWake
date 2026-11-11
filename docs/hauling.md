# Hauling: where the cargo goes

A hauler with five contracts open sees five cards in the mobiGlas, each naming
its own destination and nothing else. Where the cargo is collected is in each
card's detail panel, one card at a time, and the logs never print it. This is
what the app can and cannot know about a hauling run, with the count behind
each claim, and how the plan on the Operations command deck is put together from it.

Everything below was measured on this install's 209 backups on 21 September
2026 (`dotnet run --project src\Quantumwake.Cli -- --hauling`), except where a
frame is named.

---

## What the logs say

Two lines carry anything about a hauling contract's route, and they land
within 20 ms of each other with the same mission id.

**The acceptance toast** carries the contract's *displayed* title:

```
<SHUDEvent_OnNotification> Added notification "Contract Accepted:  Junior | Stellar Small Haul | to Stanton Gateway <EM4>[50/200/250/500/1000/2000/4000 Rep]</EM4>: " [41] to queue. New queue size: 1, MissionId: [19fac03f-…], ObjectiveId: []
```

With the StarStrings text mod installed the title names a place. Across 1,160
hauling acceptances here:

| Title shape | Acceptances | What it gives |
|---|---:|---|
| `… \| to X` | 235 | the delivery |
| `… \| from X` | 273 | the pickup |
| `… \| A > B` | 138 | both ends |
| vanilla `Junior Rank - Direct Small Cargo Haul` | 285 | nothing |
| `… \| to Destinationname` | 11 | nothing — the mod's template, unfilled |

Folded to contracts (the toast repeats), that is 139 hauling contracts, every
one with its title, 54 naming a delivery, 41 a pickup, 14 both and 58 neither.
Seventeen distinct places are named; Port Tressler 209 times, Ruin Station 99.

**The objective marker** carries the archetype id:

```
<SMarkerHandler_Base::CreateMissionObjectiveMarker> … contract [RedWind_Pyro_SmallGrade_Solar_CFP_TradepostToStation_Aluminum_CargoHauling_Multi3ToSingle]
```

It spells the *shape* — `AToB`, `SingleToMulti2`, `Multi3ToSingle` — and
usually the cargo (123 of 139 here; the interstellar bulk ids abbreviate it,
`ShipAmm_Hydro_Med`, and are left unread). It never names a place. The 49
direct, 40 single-to-multi and 43 multi-to-single contracts here are how the
plan knows a `to X` title has three pickups still to find.

**The objective ids** name their end. `ObjectiveUpserted` ids read
`pickup_<uuid>_1` and `dropoff_<uuid>_0` — 250 and 207 of them — so "two of
three pickups done" is readable from the logs even though which place a step
was is not. The objective *text* is not: `CMissionLogEntry::UpdateActiveObjective`
carries a `uiDisplay[Text=…]` for 4,867 lines here and every one is either a
template (`Go to ~mission(Location)`) or a combat line; no hauling step ever
appears with text. Checked so nobody looks again.

### The join that was missing

Until 0.15.24 the toast's title went into the timeline and nowhere else. Every
contract record was named from the archetype — "Red Wind · Recover Cargo ·
Easy" — and three things read a title off that name that never carried one:
the rep and blueprint chips on the Contracts page, the screenshot check that
compares the Contracts app's cards to the logs' open contracts, and anything
wanting a destination. The two lines share the mission id and arrive in either
order, so the builder now keeps the title by mission id and both sides look
the other up. Contracts are keyed by mission id as well: keyed by archetype,
two Aluminium runs to the same station in one session were one record.

---

## What a screenshot of the card says

The Contracts app's detail panel prints `PRIMARY OBJECTIVES` for the selected
card only. Three shapes are measured, and the indent is what pairs a pickup
with its drop-off:

| Shape | Parent line | Indented under it | Frame |
|---|---|---|---|
| direct | `Collect Agricultural Supplies from Port Tressler.` | `Deliver 0/13 SCU to NB Int. Spaceport.` — no cargo named | Star Docs hauling guide, read by eye |
| multi-pickup | `Deliver 0/18 SCU of Aluminum to Stanton Gateway.` | `Collect Aluminum from Fallow Field.` ×3 — no share per source | this install, 8 Sep 2026 |
| multi-drop | `Collect Waste from CRU-L5 Beautiful Glen Station.` ×4 | `Deliver 0/2 SCU of Waste to Seraphim Station above Crusader.` | timesaver.gg 4.10 guide, read by eye |

A long line wraps — `… to Seraphim Station above` / `Crusader.` — and comes
back from the engine as two lines, the second without a diamond; it is joined
back when it sits within two line heights and no further left than the text.
`above Crusader` and `on Pyro IV` come off the place as the body.

The contract's own text adds the one thing the objectives never say: which
body a pickup is on. `PICK UP LOCATIONS (ANY ORDER)` lists `Freight elevator
at Fallow Field on Pyro IV`, and the 8 Sep frame read all three, `Pyro Ill`
for III included — kept as read, since a body is a hint for grouping stops and
not a claim the map has to honour.

What one frame does **not** give: the other cards' objectives. Five contracts
means five screenshots, one per card selected, and the plan says how many are
still to take.

---

## The plan

`GET /api/haul/plan`, drawn on the Operations command deck under the session's
contracts, and `POST /api/haul/plan/trip` to write it into a tracked flight
plan with a load or unload action at every stop.

Each open hauling contract gets its legs from the best source it has, and
says which:

- **screenshot** — a Contracts-app frame showing this card selected, taken
  after the contract was accepted. Titles repeat — three of the five cards on
  the 8 Sep frame read *to Stanton Gateway* — so a card has to agree with the
  archetype's readable cargo and source count before it can supply a route.
  A card whose objectives did not read falls back to the title instead.
- **title** — one leg with whichever end the title named and the other
  missing, with the archetype's count of the missing end in the note: *3
  pickups, places unknown until this card is photographed*.
- **none** — a vanilla title; only a screenshot can place it.

The stops are every pickup before any delivery, same-body stops kept
together, a delivery whose contract has no pickup on the plan placed after
every pickup the plan can see (the cargo has to come from somewhere), and a
place that is one contract's source and another's destination visited for the
load first and again to deliver once its cargo is aboard. Nothing here knows a
distance — the atlas has no coordinates for a station — so it is a starting
order for the pilot to rewrite, not an answer.

The SCU total is a floor and is called one: it is the known cargo still to
deliver, after any quantity the latest card says has already arrived. Against
it the plan prints the ship's hold from the community dump when the live feed
knows the ship, and says in words when the floor is already more than the hold.
Shared destinations keep separate unload actions for each commodity. Every stop
also shows the cargo known aboard afterwards; where a multi-pickup card gives
only its delivery total, its source shares stay explicitly unknown rather than
being made up. A flight-plan action retains the mission id and the card legs it
came from, but the pilot still confirms it manually: the logs count completed
objectives without naming the photographed pickup or delivery that changed.

Places resolve to the map the way price terminals do — by the place whose name
the text contains — and a place the logs have never seen visited keeps its
name and is listed as *not on the map yet* rather than dropped.

---

## Sharing the run with a group (1.2.32)

The run above is one pilot's. Under it, **Share the run** takes the same open
contracts and shares them between the ships flying together, so the whole of
it is done soonest: the longest single ship's run is what is made short,
because a group waits on its slowest member.

- **Who and where.** The pilot's own ship and location come from the log, as
  for the run. Wingmates are a saved list (`crew.json`, name and usual hull,
  at most twelve); where each one is now is asked on the page every run and
  never kept. A hull sets the hold from the community ship data; a wingmate
  with no hull set is not limited, and the page says so.
- **Distance** is a straight line between body centres from the community
  star map - the same measure the MFD map draws - in Gm. Two stops on one
  body count as nothing, a stop with no position on file is counted as 25 Gm
  when sharing and left out of the figure shown (which is then marked as a
  floor), and another system is never measured. Checked against this
  install's star map on 2026-11-11: Hurston to Crusader 31.9 Gm, Hurston to
  microTech 38.4 Gm, Crusader to Daymar 0.06 Gm. The first check found that
  the star map spells bodies its own way ("microTech") in an ordinary
  case-sensitive dictionary, and a lower-cased lookup matched none of the 202
  atlas places that have a body; the lookup ignores case now.
- **Rules that always hold.** A contract's pickups come before its drops on
  the ship that carries it; no ship is planned over its hold, and a leg bigger
  than the hold becomes several hold-sized trips. Each contract stays on one
  ship unless the pilot ticks *split* for the run (contracts shared with the
  party), and then each leg can go its own way. A contract with a pickup
  already made stays with the pilot, whose loading is the only loading the
  log can see.
- **The method** is cheapest insertion, longest contract first, then moving
  contracts off the longest run while that shortens it. Not an optimum, and
  not claimed as one. On the real star map, four contracts around Stanton
  took one Hercules 171.8 Gm; with a Cutlass Black starting at New Babbage the
  longest run fell to 89.4 Gm, the Cutlass's 46 SCU never exceeded.
- **What comes out.** One card per ship in flying order, with each hop's
  distance and the cargo aboard after each stop. The pilot's own share can be
  written into a tracked flight plan; a wingmate's copies as numbered lines
  for party chat. A contract with an end not read yet is listed as left out,
  with the screenshot that would place it.

With only the pilot ticked it is a distance-ordered version of the run
above. *Put in order* on the Itinerary page uses the same distance since
1.2.32: after the same-body and same-system rules, the nearest ready stop
goes next.

## Not done, and why

- **Which pickup is done.** The objective ids carry an index (`_0`, `_1`) that
  might follow the card's order. Nothing here checks it, so the plan shows
  *1 of 3 pickups done* on the contract and ticks no stop on its own.
- **Distances inside a body's space.** Since 1.2.32 hops between bodies are
  measured, body centre to body centre; a station's own orbit, a Lagrange
  point and the quantum route actually flown are not. A `/showlocation` pin
  has coordinates and could one day place a stop more finely.
- **The Offers tab.** The reader anchors on `ACCEPTED (n/m)`, so a frame of a
  contract not yet taken reads as nothing. Deliberate: a plan is for contracts
  the pilot holds.
