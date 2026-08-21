# Who else is doing this

Surveyed 2026-08-21, when the rename forced a look at the neighbours. The
research in [README.md](README.md) was compiled in April and its headline —
"six of the seven tools are built on kill events, so they all show an empty
dashboard" — is no longer the shape of this space. The kill-event tools are
still there, but a second generation has grown up around everything *else*
`Game.log` records, which is exactly the ground Quantumwake stands on.

## The one that overlaps almost entirely

**[Stelliverse](https://stelliverse.fr/en/)** — Tauri 2 + React, v5.2.1,
8,312+ downloads, on the Microsoft Store, EN/FR.

Its Flight Log module is, feature for feature, close to this whole app:
locations and systems visited across **Stanton, Pyro and Nyx** with a map and
hours per system, quantum jumps and favourite routes, ships actually flown,
playtime and sessions, cargo trading and shop spending, and players crossed. It
reads the log passively, states "100% local, zero ban risk", and offers a mobile
companion over the local network — the same three positions this project took
independently.

It is also much broader: an in-game overlay with a DPS calculator, cargo price
lookup, PvP zone map, crafter module and blueprint collection, French voice
commands (Vosk + Piper), ship comparison and a keybind editor. Most of that is
not log-derived; it is community data and utilities.

Two differences worth naming, neither of them flattering to assume in our favour
without checking:

- Its cargo and price features depend on **community APIs** (UEX and friends),
  and it offers optional Discord OAuth cloud sync. Quantumwake makes *no* outbound
  request at all, including for names. That is a real difference in kind, not
  degree — but it is also a difference most users will not care about.
- It advertises **combat stats — kills, deaths, K/D, favourite weapon, last
  kill**. That sits oddly against our own evidence in
  [findings.md](findings.md): zero `<Actor Death>` lines across 403 MB and 144
  backups on 4.9. Either those counters are fed from archived logs, from
  `Incapacitated` notifications, or they are empty in practice. Worth
  investigating before assuming our finding is the complete picture — an
  independent tool claiming the opposite is exactly the kind of thing that
  should make us re-check.

## Partial overlaps

| Project | Stack | Overlaps us on | Does not do |
|---|---|---|---|
| [SCLogReader](https://github.com/miwidot/SCLogReader) | .NET 8, Avalonia, SQLite | Financial ledger with running balance, missions, ship history, quantum arrivals, locations with readable names, loadout, live tail, CSV/JSON export | No map, no overlay. Resolves names via **UEX API and scunpacked**, so it is not offline for names |
| [AssetMemory](https://github.com/PetitCastor/AssetMemory) | .NET, Blazor Server, SQLite | Inventory by location — our Stash, and **deeper**: containers nest, so a box inside a locker keeps its contents | No map, no sessions, no spending. Names from `global.ini` then a **star-citizen.wiki fallback** |
| [Starlogger](https://github.com/chrisrico/Starlogger) | Python, Flask | Cargo hauling grouped by route, trade P&L, quantum travel log, session archive and replay | Not a general companion; hauling and mining focused |
| [SC Bridge Companion](https://github.com/SC-Bridge/sc-companion) | Go, Wails, React | 57 log event patterns, SQLite, live tail | **Syncs your events to a cloud profile over OAuth** — the opposite posture to ours |
| [Picologs](https://robertsspaceindustries.com/community-hub/post/picologs-star-citizen-logs-synced-WfGFao33eXtTQ) | — | Shares log activity with friends and org in real time: who is online, what they are flying, where they are | This is **Phase 6, already shipped by someone else** |
| [StarTracker](https://github.com/BeansOnToastBruh/StarTracker) | Tray app, Win + Linux | Contracts accepted/completed/failed with rewards, sessions, ships lost, blueprint unlocks | No map, no spending, no inventory |
| [SCStats](https://github.com/Maple33-hash/SCStats) | Desktop, 12★ | Playtime, purchase-request correlation, loadout, favourite vehicle | No map, no inventory |
| [ArkanisOverlay](https://github.com/ArkanisCorporation/ArkanisOverlay) | .NET 8, WPF + WebView2 + Blazor, 44★ | Nothing — it does not read logs | Useful anyway: it is the **same overlay technique** we chose (separate top-most window, WebView2, no injection), independently arrived at, which is a good sign for the anti-cheat posture |

## What is actually still ours

Stripped of wishful thinking, three things:

1. **The atlas.** Others map where you *went*. Quantumwake draws **every place in
   the game** — 1,343 of them resolved out of the localisation table — with the
   visited ones solid and the rest hollow, so the map shows how much of the
   'verse is left rather than just a trail. Unresolved ids stay visible in a tray
   instead of being dropped.
2. **Offline all the way down, including names.** Every other tool that shows
   real item and place names fetches them from UEX, the wiki or scunpacked.
   Quantumwake reads `Data.p4k` directly with its own ZIP64 + ZStd reader, so the
   "no outbound network calls" promise survives contact with the naming problem.
   See [credits.md](credits.md) for what that owes to the community.
3. **Saying what the logs cannot support.** Confidence levels on inferred
   location, time aboard labelled as an estimate and capped, empty states that
   explain a removed event rather than showing a zero, parser health that names
   a broken tag after a patch. No competitor makes this a feature; several
   present numbers the logs cannot actually justify.

## What this changes

- **Phase 6 (server mode) is the most contested ground on the board**, not the
  open frontier the earlier docs assumed. Picologs and SC Bridge both do
  org-wide log sharing today, and SC Bridge does it with a cloud account, which
  is a thing we have promised not to build. It should probably drop down the
  order behind work that plays to the atlas and the offline stance.
- **The combat question deserves a re-check** against Stelliverse's claims
  before [findings.md](findings.md) is quoted as settled.
- **Being second is not fatal, but being vague is.** Stelliverse is broader and
  has thousands of users. The reason to keep building this one is the specific
  combination above, and that has to stay sharp rather than drifting toward a
  worse copy of an all-in-one launcher.
