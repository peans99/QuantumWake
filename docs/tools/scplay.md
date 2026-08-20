# SCPlay — ckuma/scplay

**Repo:** https://github.com/ckuma/scplay

The simplest tool in the set, and the second of the two that still works on SC
4.9 logs. It does one thing: total up how long you've played.

## What it does

Scans every log file it can find across **LIVE, PTU, EPTU and TECH-PREVIEW**,
extracts timestamps from each, and sums `last − first` per session file. Output
is a lifetime playtime figure plus per-session breakdowns.

```
[INFO] Client started …
[INFO] Client closing …
→ Session: 2 hours, 15 minutes, 30 seconds
```

## Stack

Two parallel implementations of the same tool:

| Implementation | Details |
|---|---|
| Python | 3.10+, **tkinter** GUI themed with **ttkbootstrap** |
| C# | .NET Framework 4.8.1, **WinForms** |

## Method

Session boundaries come from `[INFO] Client started` / `[INFO] Client closing`
markers, with the duration computed as the span between the first and last
timestamp in each file. It reads both the active `Game.log` and archived files in
`logbackups/`.

## Why the approach is robust

The `last − first` heuristic is deliberately dumb and that's its strength: it
doesn't depend on any *semantic* event surviving a patch. As long as lines carry
timestamps — which they always have — it produces a correct answer. Every tool
that broke in 4.x broke because it depended on a specific named event. SCPlay
depends only on the envelope.

Verified against this install: all 144 backup files carry
`<YYYY-MM-DDTHH:MM:SS.mmmZ>` prefixes, so the method applies cleanly.

## Limitations

- **Playtime only** — no ships, locations, contracts, or combat.
- **Overcounts idle time.** A session left sitting in the hangar or alt-tabbed for
  three hours counts as three hours played. Segmenting by `gamerules`
  (`SC_Frontend` vs `SC_Default`) would separate menu time from actual play — this
  install's backups are 37,182 `SC_Frontend` lines vs 15,421 `SC_Default`, so the
  distinction matters a lot.
- **Undercounts crashes.** A hard crash may truncate the log, though `last − first`
  degrades gracefully here compared to looking for an explicit close marker.

## Relevance to a new build

Take the timestamp-span method as the playtime foundation, then improve on it by
splitting on gamerules transitions. It's roughly twenty lines of logic and it
gives a number that survives every future patch.
