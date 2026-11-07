# Privacy policy

Quantum Wake is a fan-made companion for Star Citizen. It runs on your own
computer, has no account and no server of its own, and sends nothing about you
to its author. This page says what it reads, what it keeps, and every
occasion on which it talks to the internet, with the switch that controls each
one.

In force from version 1.0 (November 2026); reading the screen live was added
in 1.1 (November 2026). Changes to this page are dated in
the project's history on GitHub. Questions go to the project's issue tracker,
<https://github.com/peans99/QuantumWake/issues>.

Quantum Wake is not made by, endorsed by or affiliated with Cloud Imperium
Games or Roberts Space Industries.

---

## In one paragraph

Everything the app learns, it learns from files already on your machine - and,
only if you say yes when it asks, from the game's screen while you play - and
it keeps what it learns in your own user profile. It collects no telemetry, no
analytics, no crash reports and no identifier of any kind. It reaches the
internet only for the optional features listed below. Each one is off until you
turn it on or press its button, except the two that only fetch public files and
send nothing about you.

## What it reads on your computer

- **The game's logs**: `Game.log` and the `logbackups` folder in each Star
  Citizen channel (LIVE, PTU and the others). This is where your sessions,
  contracts, trips, purchases and party come from. Your in-game handle appears
  in these logs, and the app shows it back to you.
- **The game's own files**: `Data.p4k`, read but never changed, for item names,
  ships, missions and the 3D models.
- **The RSI launcher's log**, to find where each game channel is installed.
- **Screenshots and the clipboard**, only if you turn on screen reading in its
  settings. They are read on your machine by Windows' own text recognition, or
  by the open models described under "Text recognition models" below. No image
  leaves your computer.
- **The game's screen, live**, only if you said yes when the app asked (it
  asks once, at start) or turned it on later on the Log page. While Star
  Citizen is the window in front, the app copies the pixels of the game's
  window about every two seconds - every second while a ship is targeted -
  the same pixels a screenshot would hold, and reads them on your machine
  like a screenshot. Each frame is overwritten by the next and deleted when
  reading stops. What is kept is the reading - never the picture - of the
  screens a screenshot would have been taken of: a mining scan panel, one
  per rock, and since 1.1.21 the Fleet Manager, the map, a loadout, a
  commodity kiosk, a refinery and the Contracts and Rep apps, one reading per
  screen while it stays up, in the same reading log screenshots go to, and
  since 1.1.22 your balance from the bar of any other mobiGlas screen. Item
  tooltips and screens the app cannot read are not kept. The ship you have targeted - its name, signatures, speed and whether
  it is marked hostile - is held in memory for the combat page and never
  written down. The MFD's Target page can show the game's hologram of that
  ship, cut from the frame being read when the page asks and sent only to
  the page on your own machine. A frame can show anything the game does, other players' names
  included, and none of it leaves your computer. The app never opens the
  game's process, reads its memory or sends it input; voice commands, below, are
  a switch of their own and the one part of the app that presses keys.
- **Spoken callouts**, only if you turn on the voice on the Log page: the
  app says things like "Target identified" with the speech voice Windows
  already has, on your PC. The sentence is not sent anywhere.
- **Your microphone**, only if you turn on Voice commands in Settings →
  Voice & callouts. It is heard by the speech recogniser Windows carries, on
  your PC, told only the copilot's few dozen phrases. The app never gets the
  audio: it sees the phrase matched and how sure the recogniser was, keeps the
  last 40 in memory for the page, and writes none of them down. A command
  presses the game's key for it, and only while Star Citizen is the window in
  front. See [voice-commands.md](voice-commands.md).
- **Joysticks and the game's key bindings**, on the Controls page, to show and
  back up your bindings.

## What it writes, and where

- **Its own data folder**, `%LOCALAPPDATA%\Quantumwake` on Windows. This holds
  what it has summarised from your logs, what you typed into it (goals, notes,
  plans, prices you entered), caches of the game files, and its settings.
  Uninstalling the app does not delete this folder, so your history survives a
  reinstall; delete the folder to remove it.
  The Microsoft Store edition uses this same folder, and uninstalling it
  leaves the folder in place too.
- **One frame of the game's screen**, while live reading is on:
  `live\frame.bmp` in the data folder, replaced every couple of seconds and
  deleted when reading is switched off or the app closes.
  Beside it, `live\frames-<date>.csv` notes when each frame was taken, what
  kind of screen it was and how long reading it took - no text from the
  screen - one file a day, a week of them kept.
- **The game folder, only when you ask**: installing item labels writes a text
  table into `Data\Localization\english` and, when `user.cfg` names no
  language, adds `g_language = english` to it. Removing the labels takes back
  only what was added, and the files they replaced are kept in a backup in the
  app's data folder.
- **Files you save on purpose**: a bug report, an export to share with a
  friend, a picture from the 3D view. Each is handed to your browser to save
  and goes nowhere else unless you send it yourself.

## When it talks to the internet

Every request identifies itself only as `QuantumWake`. None carries an
identifier for you, your machine or your install, except the UEX submission
described below, which carries your own UEX credentials because UEX requires
them.

| feature | who it contacts | what is sent | when |
|---|---|---|---|
| **New versions** | GitHub (`api.github.com`) | the request for the newest release, nothing else | when you press Check now, or at start if you tick "Check when the app starts" (off by default) |
| **Installing an update** | GitHub | the download request for the release file, which is then checked against GitHub's published SHA-256 | only when you accept the offered update |
| **UEX live prices** | UEX Corp (`api.uexcorp.space`) | requests for public price lists | when you turn UEX on or press Refresh; while the app is open only if you also turn on its automatic refresh |
| **Reporting prices to UEX** | UEX Corp | for each kiosk sale you choose: the terminal, the commodity, the sale price, and your UEX application token and secret key | only when you press the button, after the app has shown you exactly what will be sent; needs credentials you created on UEX and pasted in |
| **Community ship and trade reference** | GitHub (`raw.githubusercontent.com`) and the Star Citizen Wiki (`api.star-citizen.wiki`, `starcitizen.tools`) | requests for public data files, and for an item's picture the item's name or id | only after you turn the community reference on |
| **Pictures of sticks** | GitHub (the joystick-diagrams project) | requests for public picture files | only after you turn the template feed on |
| **StarStrings** | GitHub | the download request for that project's latest release | only when you press its install button |
| **Text recognition models** | Hugging Face (`huggingface.co`) | the download request for two pinned, hash-checked model files | the first time screen reading needs them: on Linux, or on a Windows install without an English language pack |

Each of these services has its own privacy policy, which applies to what
they receive: GitHub's, UEX Corp's, the Star Citizen Wiki's and Hugging Face's.
They will see your IP address, as any website does.

## Your UEX credentials

If you use price reporting, the token and secret key you paste in are stored in
`%LOCALAPPDATA%\Quantumwake\uex\credentials.json`, as plain text. Anything that
runs as you on your computer can read that file. They are sent only to UEX,
only with a report you pressed the button for. Saving the two fields empty in
Settings → UEX live prices deletes the file, as does deleting it yourself.

## On your local network

The dashboard listens only on your own computer (`127.0.0.1`). If you turn on
access from your local network, to use a tablet as a second screen, other
devices on that network can open it. It is off by default.

## Children

The app has no accounts and collects nothing from anyone, so it collects
nothing from children. Star Citizen's own age rules apply to the game it
reads.

## What it will not do

It does not send your logs, your handle, your history, your screenshots or
anything read from your screen anywhere. It does not show advertising. It does not sell or share data, because
it has none to share: what it knows stays in the folder above, on your
machine.
