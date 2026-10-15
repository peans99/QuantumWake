# Delivery runs

Plan, unbuilt. A hauling run treated the way a delivery driver's handheld
treats a shift: a manifest, one step at a time, a scan when the load goes on
and another when it comes off, and an honest outcome for every stop — including
the ones that went wrong.

[hauling.md](hauling.md) already gets a run *planned*: every open haul on one
route, legs from titles and Contracts-app screenshots. This is about *flying*
it — what is aboard, what comes off at this stop, how much is left — and it
starts from a field session, because the screens it would most like to read have
not been seen yet.

Measured 2026-10-15 against this install: SC LIVE, `Game.log` plus 252 files in
`logbackups` (26 Apr → 7 Oct 2026). A haul here is a `Contract Accepted` toast
whose title contains *Haul*, counted once per mission id: **205**.

## The problem

Three questions a hauler with four contracts aboard cannot answer from the game
without opening the mobiGlas card by card:

1. **At the pickup** — what do I load, and where does each of it go?
2. **At a stop** — what comes off *here*, and how much of it?
3. **On the way** — what is left, and for whom?

The hard one is the second. Two contracts can carry the same commodity to
different stations, the crates are not labelled with a destination, and the
game hands out a mix of sizes, so "18 SCU of Aluminium to Stanton Gateway" has
to become "the 16 and the 2" while standing at the grid.

## The workflow

| Driver | Quantum Wake | Moves on by |
|---|---|---|
| Shift starts, route loaded | Accepting a haul opens a **run**; each further accept joins it | `Contract Accepted` toast |
| Manifest | Stops by station, commodity and SCU per drop | delivery toasts, titles, Contracts-app screenshot |
| Load the van, scan each parcel | Screenshot the **freight elevator kiosk**; SCU becomes crates; run marked *loaded* | screenshot (reader unbuilt) |
| "Next stop: …" | Overlay / MFD card: next stop, what comes off there | the pilot, or the next open drop |
| Arrive | Card switches to *unload* | inferred location, with its confidence |
| Scan, proof of delivery | Tick the drop | log when it says so, screenshot, or the pilot's tick |
| Exception | Partial, crate lost, failed, abandoned, died with cargo | `EndMission` completion type, the pilot |
| End of shift | Run summary: stops, SCU, time, outcomes | last contract closed |

Two rules carried over from the driver's handheld:

- **One step at a time in the cockpit.** The overlay card shows the current
  stop and nothing else; the whole manifest lives on the dashboard for planning
  and history.
- **The app moves forward, the pilot confirms.** Every tick says where it came
  from — *from the log*, *from a screenshot*, *ticked by you* — and the pilot's
  tick wins.

## What the logs give

### The delivery toast — new, and half the time

Unread by the parser today. Each drop-off of a haul gets its own HUD toast,
keyed by mission id and the drop's objective id:

```
<SHUDEvent_OnNotification> Added notification "New Objective: Deliver 0/177 SCU of Potassium to Ruin Station above Pyro VI: " [..] to queue. ... MissionId: [<mission>], ObjectiveId: [dropoff_<uuid>_0]
```

Place, commodity and SCU for **each** drop, and the drop's index — which is the
per-place link [hauling.md](hauling.md) said the logs lacked. Indices seen run
`_0` to `_5` (129, 51, 19, 15, 10, 4 distinct drops). Some lines say
`Cargo Boxes` instead of `SCU of <commodity>`.

**Coverage: 106 of the 205 hauls.** Not explained by outcome — 49 hauls that
completed never got one, against 52 that did — and not by title shape: every
shape appears on both sides. The rate drops in sessions with many accepts
(23 Aug: 9 of 37; 24 Aug: 2 of 16). Untested guess: the toast only fires for the
contract being tracked. The first field check below settles it.

The numerator is almost always `0`. Four drops showed a partial value —
`26/30 SCU of Medical Supplies`, `16/32 SCU of Hydrogen`, `4/11` and `4/19` —
and each reads like the objective being re-announced mid-run rather than a
progress report. Treat the toast as a manifest line, not a progress feed, until
the field session says otherwise.

No haul gets a pickup toast. `New Objective: Collect … From <place>` does exist
— 15 of them, on 12 missions — but every one is a **courier** contract
(*ENTRY LVL. COURIER NEEDED IN STANTON*, *Med. Distance Covalex Delivery
Stanton*), with the commodity and the place in it. So hauling pickups still
come from the title (`from X`, `A > B`) or a Contracts-app screenshot, while a
courier run would get both ends from the log — worth keeping in scope, since box
delivery is exactly the driver's job.

### Per-drop completion — a third of the time

```
<ObjectiveUpserted> ... mission_id <mission> - objective_id dropoff_<uuid>_0 - state MISSION_OBJECTIVE_STATE_COMPLETED
```

**36 of the 101 completed hauls** have one. So a stop *can* tick itself, but
mostly will not; the manual tick is the backbone, not the fallback.

### Contract outcome — every time

```
<EndMission> Ending mission for player. MissionId[<mission>] ... CompletionType[Complete] Reason[Mission Ended]
```

Complete, Fail and Abandon are all there (across every mission type: 313, 16,
76; 26 of the hauls were abandoned). A single-drop contract that completes has
delivered its drop, which is the cheap case of the auto-tick. A contract that
ends any other way closes the run's stops for it as an exception rather than
leaving them open.

### What the logs do not give

- **Crates.** The freight elevator logs an entity count and an opaque location
  id and nothing else — checked in 0.7.0, see
  [untapped-signals.md §6](untapped-signals.md). No crate size, no commodity.
- **Partial deliveries.** Nothing between *in progress* and *completed*.
- **Payout.** `Awarded N aUEC` appears 14 times in all; hauls do not reliably
  say what they paid. A run summary's earnings would be the contract's offered
  reward from a screenshot, or nothing, and labelled as such.
- **Distances.** Still none; stop order stays a suggestion.

## Field session

What to bring back from one hauling session, in this order. Leave screenshots
in the game's screenshots folder; note the time of each so it can be matched to
the log.

**Screenshots**

1. **Contracts app** right after accepting each haul, the card selected — the
   reader exists, this anchors everything else.
2. **Freight elevator kiosk at the pickup, before loading** — the contract
   cargo as the kiosk lists it. Scroll and take another if the list is long.
   *The question:* does it show crate sizes and counts, and does it say which
   contract a crate belongs to?
3. **The same kiosk after loading**, and the ship's cargo view if there is one.
4. **Freight elevator kiosk at a drop, before and after unloading.**
   *The question:* does it show what is aboard, and is a partial unload
   visible?
5. **Contracts app mid-run**, with a contract partly delivered — does the card
   say `Deliver 26/30`?
6. **The HUD objective tracker** in flight, if it shows the SCU left.

**Log checks** (afterwards, against that session's `Game.log`)

- Accept two hauls, track only one: does the untracked one get a delivery
  toast? Then switch tracking — does it fire then?
- Deliver one drop of a multi-drop contract: is there an `ObjectiveUpserted …
  COMPLETED` for that drop, and does its index match the toast's?
- Deliver part of a drop: any line at all?
- Abandon one: confirm `CompletionType[Abandon]` and nothing else is needed.

## Build order

Each step stands on its own if the next never happens.

1. **The run, from the logs.** A run opens on the first accept and gathers
   contracts until the last one ends. Manifest from delivery toasts, titles and
   any Contracts-app screenshot already read; stops by station; manual tick per
   drop; outcomes from `EndMission`. Parser change — `PayloadVersion` moves.
2. **The cockpit card.** Overlay and MFD: next stop, what comes off there, what
   stays aboard, stops left. Switches to *unload* when the inferred location is
   the stop.
3. **Ticks from the log.** Drop completed → tick; single-drop contract
   complete → tick; any other ending → exception.
4. **The elevator scan.** A reader for the kiosk, if the field session shows it
   can be read: SCU becomes crates, and a shared commodity is split into "these
   come off here" by crate size.
5. **The drop scan.** Partial deliveries from a kiosk or Contracts-app
   screenshot.
6. **Run history.** Summary per run beside the other trips.

## What it reuses

- `HaulPlanner` / `HaulLegs` and `GET /api/haul/plan` — the stops and their
  order. The run grows this rather than starting a second plan.
- `TripStore` — tracked flight plans with load and unload actions per stop; the
  run is a trip with outcomes.
- `ScreenFrame` readers and the screenshot watcher — the Contracts app is read
  already; the kiosk would be a new reader beside the refinery ones.
- Location inference — arrival, with the confidence it already carries.
- `CargoFit` and `GameCrates` — crate sizes (1 to 32 SCU) and the hold, for the
  split and for "keep these near the door".

## Open questions

- Why half the hauls get no delivery toast (field check 1).
- Whether the kiosk names crates, sizes and contracts (screenshots 2–4).
- Whether a drop's objective index follows the card's order — still unchecked
  since [hauling.md](hauling.md) raised it.
- Whether courier contracts join a run alongside hauls, or get a run of their
  own.
- Where the run lives on the dashboard: the Operations command deck, where the
  haul plan is, or a page of its own.
