# Credits and third-party resources

Quantumwake is built by **nekron**. Everything below came from someone else,
and this page says exactly what was taken from where.

The rule this page follows: if a line of logic, a regular expression, an image
or a package came from outside this repository, it is named here — even when it
was re-typed rather than copied, because the *knowledge* was still someone
else's work.

---

## Community projects

The Star Citizen log-tooling community worked out the log format years before
this app existed. None of their source files are vendored here — this is C#,
they are Python, Java and Go — but the patterns and the classification logic
below are theirs, re-implemented.

| Project | Author | What this app took |
|---|---|---|
| [StarLogs](https://github.com/Ozy311/StarLogs) | Ozy311 | **The most is owed here.** The archived `<Actor Death>` and `<Vehicle Destruction>` line formats, the kill-classification tree (suicide → damage-type branch → NPC/player matrix), the actor-to-vehicle crew correlation, and the NPC-name marker list — all derived from `event_parser.py` and re-implemented in `Events/CombatEvents.cs` and `Parsing/LogEventParser.cs`. |
| [all-slain](https://github.com/DimmaDont/all-slain) | DimmaDont | The per-patch record of which events CIG removed and when — quantum detail in 4.0.1, death scope in 4.0.2, inter-system jumps in 4.1.0. That timeline is what turned "our logs look empty" into a documented erosion pattern, and it is why every feature here degrades to a labelled empty state. |
| [SCStats](https://github.com/Maple33-hash/SCStats) | Maple33 | The read-only, offline posture adopted wholesale (see [architecture.md](architecture.md)), the idea of correlating shop purchase requests with their responses, and time-in-seat as a ship metric — which is how we found out 4.9 no longer supports it. |
| [SCPlay](https://github.com/ckuma/scplay) | ckuma | Playtime measured as the span between first and last timestamp in a log, and the separation of in-game time from menu time. |
| [AutoTrackR2](https://github.com/BubbaGumpShrump/AutoTrackR2) | BubbaGumpShrump | Studied for its live-tail loop and overlay approach. |
| [SC-Kill-Monitor](https://github.com/greluc/SC-Kill-Monitor) | greluc | Studied as the cautionary case: one regex, no fallback, total failure when the format moved. |
| [citizenmon](https://github.com/danieldeschain/citizenmon) | danieldeschain | Studied for its tailing strategy. |
| [scunpacked-data](https://github.com/StarCitizenWiki/scunpacked-data) | StarCitizenWiki | **The optional commodity names.** A cargo sale logs the commodity as an id nothing in the local install can resolve; this repository publishes the id-to-name table, regenerated after each patch, and it resolved every id this project had ever logged. Fetched only when the user opts in — never vendored, never fetched silently. |
| [ScDataDumper](https://github.com/octfx/ScDataDumper) | octfx | The loader that generates scunpacked-data from the game files. Not run or shipped here, but the names the opt-in feature shows exist because of it. |
| [StarStrings](https://github.com/MrKraken/StarStrings) | MrKraken | **The optional text mod, entirely their work.** Every string in it was written, tested and maintained by MrKraken; this app only offers to fetch their release and copy it into the game folder, on a click, and to take it back out again. Nothing of theirs is vendored, modified or redistributed here - the download comes from their own GitHub releases, and the mod is theirs to credit and theirs to change. |
| [UEX](https://uexcorp.space) | UEX Corp and its datarunners | **The optional live prices**, crowd-sourced by players and fetched only at the user's request - and the destination of the optional price reports, where a user with UEX credentials can contribute the sale prices their own logs recorded. |

The comparison of the seven log tools, and what each one does on a current
install, is in [docs/README.md](README.md).

## Game data and file formats

**`Data.p4k`** — display names for places, items and shops are read from the
game's own localisation table inside `Data.p4k`. The archive is a ZIP64
container whose entries are ZStd-compressed under method `100`; that fact is
community knowledge, first published by the reverse-engineering work behind
[scdatatools](https://github.com/ventorvar/scdatatools) and the `unp4ck`
generation of tools before it. No third-party extractor is used or shipped —
`GameData/P4kArchive.cs` walks the central directory itself and decompresses a
single entry in memory — but the format was not worked out here.

**Star Citizen Wiki API** ([api.star-citizen.wiki](https://api.star-citizen.wiki))
and [starcitizen-api.com](https://starcitizen-api.com) were consulted during
research as a cross-check on body and station names. Neither is called at
runtime and no data from either is committed — the shipped names come from
`Data.p4k`, so the app stays offline. They are listed because they informed the
location model.

## Artwork

The manufacturer marks in `web/assets/manufacturers/` and the *Made by the
Community* badge in the page footer are from the **official Star Citizen
Fankit** (<https://robertsspaceindustries.com/fankit>), used under the Fankit
Agreement and the Made by the Community licence. They are Cloud Imperium's
property, not ours, and are included unmodified.

Star Citizen®, Roberts Space Industries® and Cloud Imperium® are registered
trademarks of Cloud Imperium Rights LLC. This is an unofficial fan tool, not
affiliated with or endorsed by Cloud Imperium Games.

## Packages

| Package | Author | Used for | Licence |
|---|---|---|---|
| [ZstdSharp.Port](https://github.com/oleg-st/ZstdSharp) | Oleg Stepanischev | Managed ZStd decoder — the only way to read `Data.p4k` entries without a native binary | MIT |
| [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/) | Microsoft | The session cache | MIT |
| [Microsoft.Web.WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) | Microsoft | Hosts the web UI inside the overlay window | Proprietary, redistributable |
| [xUnit.net](https://xunit.net) | xUnit contributors | Test framework | Apache-2.0 |
| [coverlet](https://github.com/coverlet-coverage/coverlet) | Toni Solarin-Sodara and contributors | Coverage collection | MIT |

There are no client-side dependencies at all — the dashboard is hand-written
HTML, CSS and JavaScript with no framework, no bundler and no CDN, which is what
keeps the "no outbound network calls" promise literally true.

## What is original here

For completeness, the parts that are not derived from anything above: the
location-id grammar and resolver, the topology map layout, the session and
location state machines, the inferred-location confidence model, the estimated
time-aboard metric, the purchase/response pairing implementation, the SQLite
cache and fingerprinting scheme, the log simulator, the overlay window, and all
of the UI.
