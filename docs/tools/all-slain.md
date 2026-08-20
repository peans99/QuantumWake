# all-slain — DimmaDont/all-slain

**Repo:** https://github.com/DimmaDont/all-slain · "Star Citizen Game Log Event Viewer"

**The most valuable of the seven tools for our purposes** — not because of what it
does, but because it is the only project that documents *which events exist in
which patch*. That patch-by-patch record is the evidence trail behind
[../findings.md](../findings.md).

## What it does

A log event viewer rather than a killboard. Parses `Game.log` and renders a
readable, colourised event stream — deaths, vehicle destruction, respawns, load
progress, quit events. Broader event coverage than the kill-only tools.

## Stack

**Python 3.10+**, with `main.py` and `pyproject.toml` at the root. Implementation
detail lives in a `DEVELOPERS.md` referenced by the README.

## Event support, by patch — the important part

The README tracks availability against game version, which no other tool does:

| Event | Availability |
|---|---|
| Player/NPC deaths | Current, **but since patch 4.0.2 only events involving the client player are logged** |
| Ship/vehicle destruction | Current (as of the documented LIVE 4.3.2) |
| Player respawns / corpse activity | Current |
| Game load progress | Current |
| Quit to menu / desktop | Current |
| Incapacitation events | Current |
| Medical bed limb healing | Current |
| **Inter-system jumps** | **Lost after 4.1.0** |
| **Quantum travel with destination** | **4.0_PREVIEW only** |
| **Quantum travel events** | **Lost after 4.0.1** |

## Why this matters

Read that table as a timeline and the direction is unmistakable:

- **4.0.1** — quantum travel events removed
- **4.0.2** — death logging narrowed to client-player-only
- **4.1.0** — inter-system jump events removed
- **4.9 (this install)** — death and vehicle-destruction events absent entirely

all-slain's documentation stops at LIVE 4.3.2, where client-player deaths still
worked. Our scan of 402.5 MB of 4.x logs — spanning builds `11715810` through
`12344265` — found zero. The erosion this project was tracking simply continued
past the point where the README was last updated.

This is the strongest available evidence that the missing kill events are a
deliberate, progressive CIG policy rather than a local configuration problem or
a quiet session.

## Status against SC 4.9

**Partially functional.** Its non-combat events — load progress, quit to
menu/desktop, respawns, incapacitation — still fire, and incapacitation in
particular is confirmed present in our logs (95 occurrences across 11 sessions).
Its combat events do not.

That mixed result is itself a useful design lesson: a tool that parses *many*
event families degrades gracefully as CIG removes individual ones, where a
single-purpose killboard simply dies. Our plan's "parser health" panel comes
directly from this observation.

## Limitations of the source material

The README does not publish regex patterns or full log-format specifications, and
attempts to fetch `DEVELOPERS.md` and the parser source over HTTP returned
404/rate-limit during this research. The patch-availability table above is taken
from the README and is reliable; anything about its internal implementation is
not documented here and would need a fresh look at the source.
