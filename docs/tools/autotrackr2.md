# AutoTrackR2 — BubbaGumpShrump/AutoTrackR2

**Repo:** https://github.com/BubbaGumpShrump/AutoTrackR2

A streamer-oriented PvP kill tracker. The most automation-heavy tool of the set:
it doesn't just display kills, it reacts to them.

## What it does

Watches `Game.log` for player elimination events. On a kill it:

- appends a row to a local CSV in `%LOCALAPPDATA%\AutoTrackR2\`
- scrapes the victim's **RSI website profile** (handle, org) and displays it
- optionally POSTs the kill to a **user-configured API endpoint**
- optionally fires **AutoHotkey** scripts — documented uses are visor wiping and
  triggering video capture (ShadowPlay / OBS clip hotkeys)

## Stack

- **C# / .NET Framework 4.7.2+**, WPF with **XAML** UI
- Windows 10 or later
- Config in a local `config.ini`

## Configuration surface

- path to the live `game.log`
- API endpoint + key for kill submission
- AutoHotkey script toggles (visor wipe, recording)
- offline/local-only mode

## Caveats

- **Kills only.** No session stats, no playtime, no non-combat events.
- **No backup processing.** Points at the live `game.log`; doesn't backfill from
  `logbackups/`.
- **Regexes are undocumented.** The README describes behaviour but not the
  patterns — they live in the source. Repeated attempts to fetch the source files
  over HTTP returned 404 during this research, so the patterns below are inferred
  from the shared format, not verified against this repo's code.
- **External data flow.** RSI profile scraping and API submission both send
  player identity data off-machine. Worth knowing before enabling.

## Status against SC 4.9

**Non-functional here.** It depends entirely on kill events, and this install has
none across 402.5 MB / 144 log files (see [../findings.md](../findings.md)).
There is nothing for it to trigger on.

The *interesting* part of this tool for a new build isn't the parsing — it's the
side-effect model: log event → local record → external enrichment → OS-level
automation trigger. That pattern is reusable for any event type. A contract
completion could just as easily fire a clip trigger or a webhook.
