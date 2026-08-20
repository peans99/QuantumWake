# Star Citizen Log Tooling — Research

Research notes for building a Star Citizen `Game.log` UI.

Compiled 2026-08-20 against a real install: **SC 4.9.188.23497**, handle `nekron`,
144 log backups spanning **2026-04-20 → 2026-08-17 (402.5 MB)**.

## Contents

| Doc | What's in it |
|---|---|
| [findings.md](findings.md) | **Read this first.** The kill-event problem, with evidence. |
| [log-format-reference.md](log-format-reference.md) | Line formats verified present in the local logs, plus the removed ones. |
| [architecture.md](architecture.md) | Design decisions for SC Companion and the reasons behind them. |
| [phase-1-core.md](phase-1-core.md) | Build log: the Core parser, what it verified, and the quirks it uncovered. |
| [tools/starlogs.md](tools/starlogs.md) | Ozy311/StarLogs — Python/Flask/SSE dashboard |
| [tools/autotrackr2.md](tools/autotrackr2.md) | BubbaGumpShrump/AutoTrackR2 — C#/WPF kill tracker |
| [tools/scstats.md](tools/scstats.md) | Maple33-hash/SCStats — read-only session analyser |
| [tools/scplay.md](tools/scplay.md) | ckuma/scplay — playtime totaliser |
| [tools/sc-kill-monitor.md](tools/sc-kill-monitor.md) | greluc/SC-Kill-Monitor — Java/JavaFX "who killed me" |
| [tools/all-slain.md](tools/all-slain.md) | DimmaDont/all-slain — Python event viewer, best patch-history notes |
| [tools/citizenmon.md](tools/citizenmon.md) | danieldeschain/citizenmon — Go killfeed |

## The one-line summary

Six of the seven tools are built on `<Actor Death>` / `<Vehicle Destruction>`
log events. **Those events are not present in this install's logs** — not in the
current `Game.log`, and not in any of the 144 backups going back to April.
Any of them, run here today, shows an empty dashboard.

The two that still work (SCStats, SCPlay) are the two that never touched combat
data — they read session timestamps, ship usage, and loadout changes, all of
which are still logged in abundance.

## Comparison matrix

| Tool | Stack | Live tail | Reads backups | Depends on kill events | Works on this install |
|---|---|:---:|:---:|:---:|:---:|
| StarLogs | Python 3.8+ / Flask / SSE | ✅ | ✅ | ✅ | ❌ |
| AutoTrackR2 | C# / .NET 4.7.2 / XAML | ✅ | ❌ | ✅ | ❌ |
| SC-Kill-Monitor | Java / JavaFX | ✅ | ❌ | ✅ | ❌ |
| all-slain | Python 3.10+ | ✅ | ✅ | mostly | ⚠️ partial |
| citizenmon | Go | ✅ | ❌ | ✅ | ❌ |
| **SCStats** | desktop binary | ❌ | ✅ | ❌ | ✅ |
| **SCPlay** | Python/tkinter + C#/WinForms | ❌ | ✅ | ❌ | ✅ |

## Where that leaves a new build

The logs are rich — they're just not rich in *combat*. Four months of data
covers ships flown, locations visited, quantum routes, contracts accepted,
party activity, and session timing. See
[log-format-reference.md](log-format-reference.md) for the verified line
formats to build against.
