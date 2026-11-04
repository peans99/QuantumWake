# Game channels — reading LIVE, PTU and the others, one at a time

Built as 0.21.1 and 0.21.2 (2026-11-03). The code is
`Quantumwake.Core/Logging/GameInstall.cs` (finding installs),
`InstallPathStore.cs` (remembering one), `AppPaths.ForChannel` (where each
one's caches go) and the server's `GameChannels.cs` (the switch). Measured on
2026-11-04 on this machine, which has LIVE under `C:\Program Files\Roberts
Space Industries\StarCitizen\LIVE` and a PTU under `D:\rsiptu\StarCitizen\PTU`.

---

## One channel at a time, on purpose

Everything downstream holds the install it was started with, and each
install's sessions live in their own database (`sessions-<fingerprint>.db`,
keyed on the folder), so a PTU evening never lands in the LIVE totals. The app
therefore reads one channel and switching is a restart pointed at the other
folder - which is what the tray's folder picker already did, now with the
folder found for you.

The caches that come out of a channel's game files - names, commodities,
models, pictures, and the record of what was written into its folder - are
kept apart too. LIVE keeps the data folder itself, so nothing anyone already
has moves; every other channel gets `channels\<name>` beside it. The caches
are stamped with the archive's write time, so sharing one file between LIVE
and PTU would not show wrong answers - it would throw the other's away on
every switch, and rebuilding the commodity table alone reads 316 MB.

## How a channel is found

`/api/install/channels` returns the union of four searches, kept for two
minutes because the page asks every minute and a sleeping external drive can
make one of them slow (`?refresh=true` looks again):

1. **The drive walk.** Every fixed drive, under the layouts the launcher
   produces and the library names people actually choose
   (`StarCitizen`, `Games\StarCitizen`, `Games\Roberts Space Industries`,
   `RSI\StarCitizen`, `SC\Roberts Space Industries\StarCitizen`,
   `Program Files\StarCitizen`), plus every top-level folder of the drive
   holding a `StarCitizen` or `Roberts Space Industries\StarCitizen` folder -
   which is what finds `D:\rsiptu`, a name no list would guess.
2. **The launcher's own log**, `%APPDATA%\rsilauncher\logs\log.log`, which
   names every channel folder it has installed to. On this machine it names
   `D:\rsiptu\StarCitizen\PTU` four times. Under Wine the same log is read
   out of each prefix and its Windows paths mapped.
3. **The folder being read now**, and the one remembered in
   `install-path.txt`.
4. **Their siblings**: the launcher puts every channel under one folder, so
   `LIVE`'s neighbours are tried even when they live somewhere no scan looks.

A folder counts when it has a `Game.log` or a `logbackups` folder. The list is
LIVE first, then the test channels by name, the order the launcher shows.

What this answered on 2026-11-04, from a 0.21.9 server on a copy of this
install's data:

| channel | root | logs | last played |
|---|---|---|---|
| LIVE | `C:\Program Files\Roberts Space Industries\StarCitizen\LIVE` | 286 | 2026-11-04 04:53 UTC |
| PTU | `D:\rsiptu\StarCitizen\PTU` | 0 | 2026-11-03 23:59 UTC |

## Which one the game is on

"Running" is read off `Game.log`'s write time, not the process list: the game
is started fresh for every session and writes its log within seconds, so a
log written in the last ten minutes is a game being played. Asking the process
for its path works on Windows only, and not at all for a game under Wine seen
from the Linux side. When another channel's log is newer than the current
one's and within those ten minutes, the page's notice names it and offers the
switch.

## The switch

`POST /api/install/channel` with `{"root": ...}`. The root has to be one the
list gave - a path the page sends back, not one it made up - or the answer is
400 "That channel is no longer there." The same folder again answers
`already: true` and does nothing. Otherwise the folder is written to
`install-path.txt` and the shell is asked to restart; the answer carries
`restarting: false` when nothing is listening that can, and the page then says
to restart by hand rather than promising one. A folder named on the command
line (`--path`) or in the settings file always wins over the remembered one,
so with one of those the switch answers 409 instead of restarting for nothing.

Measured on the copy: LIVE → PTU answered `{"channel":"PTU","restarting":false}`
(a bare server, no tray), wrote the path, and the restarted server read
`D:\rsiptu\StarCitizen\PTU` with 0 backups and created `channels\PTU` for its
caches. PTU → LIVE wrote the LIVE path back. Nothing in the LIVE data moved.

## What the PTU is worth reading

As of the 4.10.199 PTU of 2026-11-03, the game files it carries read the same
as LIVE for everything the app reads from them - the same seven event
journals, the same reputation ladders, the same armour figures - with thirteen
new InterSec contracts for a Vanduul incursion in Nyx gated behind a server
switch. The point of the channel is the logs: a PTU session's contracts,
trips and receipts land in its own history, and come to the LIVE pages when
they ship.

## What is tested

`Quantumwake.Tests/GameChannelTests` covers LIVE keeping the data folder while
the others get their own, a library folder of any name being found, the key
that makes two spellings of one folder the same channel, and the ten-minute
rule in its three cases (a game running elsewhere, an old log elsewhere, the
channel being read being the newer). `Quantumwake.WebTests/ChannelSwitchTests`
drives the page's label, the notice and the switch. The pinned case and the
restart itself are not under test; the switch above was checked by hand.
