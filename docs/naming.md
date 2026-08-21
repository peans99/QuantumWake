# Why the project is called Verselog

It was called **SC Companion** until 2026-08-21. The name was checked against
what else exists, and it did not survive the check.

## What the search found

"SC Companion" is not merely similar to other things — it *is* other things:

| Collision | Where | How bad |
|---|---|---|
| **`sc-companion`** — "SC Bridge Companion, native Windows app for Star Citizen game log ingestion and gameplay data capture" | [github.com/SC-Bridge/sc-companion](https://github.com/SC-Bridge/sc-companion) | The worst of them. Same name, same platform, same premise: reading `Game.log` on Windows. |
| **SC Companion** — translation, UEX prices, component grades, mining tools, by *Gerby* | [Microsoft Store](https://apps.microsoft.com/detail/9nm0b6bw0133) | Same name, same game, same store this would ship to. |
| **SC Companion** — paid Android app | Anykey Interactive LLC | Same name, and a company behind it. |
| **SC Companion — "Tools for the 'verse"** | [sccompanion.com](http://www.sccompanion.com/) | Holds the `.com`. |
| **Star Citizen Companion** | [sccompanion.org](https://sccompanion.org/) | Holds the `.org`. |

Nothing here is a legal problem — a fan tool named after the game is low risk,
and "SC Companion" is close to generic. It is a *discoverability* problem: a
search for the name returned someone else's log-ingestion app first, and both
obvious domains and the Store listing were already occupied.

The same search turned up how much busier this niche has become since the April
research: [SCLogReader](https://github.com/miwidot/SCLogReader),
[StarTracker](https://github.com/BeansOnToastBruh/StarTracker),
[AssetMemory](https://github.com/PetitCastor/AssetMemory),
[Starlogger](https://github.com/chrisrico/Starlogger),
[Picologs](https://robertsspaceindustries.com/community-hub/post/picologs-star-citizen-logs-synced-WfGFao33eXtTQ),
[starloot-companion](https://github.com/kimgrcat/starloot-companion) and
[ArkanisOverlay](https://github.com/ArkanisOverlay) (the most popular of them).
Most are still built around kills. None appears to do the atlas, or to put a
confidence level on an inferred location.

## Candidates that were rejected

Every one of these was checked against GitHub, the Microsoft Store, the SC
community and the domain registries before being dropped:

| Name | Killed by |
|---|---|
| Dead Reckoning / Deadreckon | An existing RSI org, [Dead Reckoning [DEADRECK]](https://robertsspaceindustries.com/orgs/DEADRECK). Colliding *inside* the community is worse than colliding outside it. |
| Driftlog | Live dev tool at [driftlog.dev](https://www.driftlog.dev/), plus `driftlog.app`, `driftlog.net` and a GitHub repo. |
| Starfix | [Fugro Starfix](https://www.fugro.com/expertise/satellite-positioning/starfix), commercial marine-positioning software. |
| Starwake | A Steam game, an iOS game, a browser game and a protocol site. |
| Voidmark / Driftmark | `voidmark.app` and `drift-mark.app` are both live products. |
| Astrolabe | An astrology software house and a Play Store app. |
| Wayfix | Registered domain plus a Minecraft-adjacent GitHub project. |
| Ephemeris | Clear in the Star Citizen space and the best conceptual fit — a table of positions *computed* rather than observed, which is exactly what this app produces. Rejected only because a dozen astronomy libraries own the word on GitHub, the top one at 868 stars. |
| Sextant | Also clear in the SC space, also a good metaphor. Rejected for the same reason: seven software projects use it, including a 573-star Rails tool. |

## Why Verselog

- **Nothing else uses it.** No GitHub repository, no app, no site. The `.com` is
  parked by a reseller and attached to no product.
- **It says what it does.** A log of your time in the 'verse — which is the whole
  app, since everything here is derived from a log file and nothing else.
- **"'verse" is community idiom**, not a CIG trademark, and it is used freely by
  other fan tools (VerseGuide, VerseTime, Stelliverse).

The known cost, accepted deliberately: `...log` is a crowded suffix in this exact
niche — StarLogs, Starlogger, Picologs, SCLogReader all live there. The name
risks reading as one more log parser. The map, the ledger and the
confidence-labelled inference are what separate this from that crowd, so the
work has to do the distinguishing rather than the name.

## What the rename touched

Assembly and namespace `SCCompanion.*` → `Verselog.*`, the six project
directories, the solution file, the launcher, the UI header, the overlay splash,
and every document.

One user-visible consequence: the cache moved from
`%LOCALAPPDATA%\SCCompanion` to `%LOCALAPPDATA%\Verselog`, so the first run after
the rename does a full rescan (~30 s) instead of a warm start. The cache is
derived entirely from the logs, so nothing is lost, and the old directory can be
deleted. No migration code was written for this — carrying a legacy name forever
to save one rescan is a bad trade.
