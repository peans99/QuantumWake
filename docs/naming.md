# Why the project is called Quantum Wake

It was called **SC Companion** until 2026-08-21. The name did not survive being
checked against what else exists. Neither did **Verselog**, which replaced it for
a few hours and is in the rejected table below with the rest - the second attempt
was measured on the wrong thing, and it is worth keeping the record of that.

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
| SC LogBook | The metaphor is right - a pilot keeps a logbook, and that is what this app produces - but the name puts back the `SC` prefix this rename existed to escape, and lands one word from [SCLogReader](https://github.com/miwidot/SCLogReader), the tool closest to us in features. "Star Citizen Logbook" is already two community sites ([citizen-logbook.com](https://citizen-logbook.com/en/), active since 2019, and [scl.hypertesto.me](https://scl.hypertesto.me/)), and unqualified "logbook" belongs to aviation software - PilotLog, LogTen, FLYLOG.io. **The idea survived as the tagline instead:** *A pilot's logbook for Star Citizen.* |

| Verselog | **Held the name for a day before being dropped.** Genuinely unused by anything, which was the whole point — and that turned out to be the flaw. "Verse" does no Star Citizen work: it is a Bible verse, a song verse, the metaverse, and, more seriously, [Epic's Verse programming language](https://dev.epicgames.com/documentation/en-us/uefn/verse-language-reference), which puts the name in the path of whatever that ecosystem produces next. Optimising purely for *nothing else uses this* produced a name that said nothing. |
| Jumplog | [jumplog.app](https://jumplog.app/) is a skydiving logbook, and skydiving owns that whole search. |
| Portolan, Binnacle, Orrery, Pelorus | Considered in the second round and all clear in gaming — a portolan chart is compiled from voyages actually made, which fits the atlas exactly. Passed over for saying *sea* rather than *space*. |
| Spoolup | Excellent Star Citizen signal — "spooling" is what a quantum drive does before a jump — and free everywhere. Passed over for naming the departure rather than the record. |
| Chiplog, Rutter, Lodestar, Astrogator | The instrument that gave logbooks their name, the pilot's private book of routes, the guiding star, the star-navigator. All taken: an IC-scanner app, a fintech API company, ChainSafe's 1,419-star Ethereum client, and Ansys STK's aerospace module. |

## Why Quantum Wake

- **It says Star Citizen without saying it.** Quantum travel is this game's own
  idiom — Elite has frame shift, Star Wars has hyperspace. A player reads
  "quantum" and knows what game this is; nobody else does, and CIG's actual
  trademarks (ship names, mobiGlas, UEE) stay untouched, which they must.
- **The wake is what the app has.** No position is logged, ever. What survives a
  flight is the trail it left in the log file, and reading that trail back is
  the entire product.
- **Nothing else uses it.** No GitHub repository, no app, no product. The `.com`
  has sat parked since 2020 with nothing on it.

Two costs, both accepted. "Quantum" is a tech buzzword, so the word alone will
always be noisy — but the compound is not, and no quantum-computing project is
going to be called Quantumwake. And it is four syllables; expect it to be
shortened to QWake in conversation, which is fine.

## What the rename touched

Assembly and namespace `SCCompanion.*` → `Verselog.*` → `Quantumwake.*`, the six
project directories, the solution file, the launcher, the UI header, the overlay
splash, and every document.

One user-visible consequence: the cache moved from `%LOCALAPPDATA%\SCCompanion`
to `%LOCALAPPDATA%\Quantumwake`, so the first run after a rename does a full
rescan instead of a warm start — measured at 4 s for 146 logs, not the 30 s the
early docs assumed. The cache is derived entirely from the logs, so nothing is
lost, and the stale directories can be deleted. No migration code was written
for this: carrying a dead name forever to save one rescan is a bad trade.
