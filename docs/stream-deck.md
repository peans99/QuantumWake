# Stream Deck

Settings → Stream Deck (1.2.8, its own tab since 1.2.15) builds a Star Citizen
profile for an Elgato Stream Deck from the game's own keybindings, and ships the
small plugin that presses them. Nothing is sent anywhere: the profile is built
on this PC and handed to the Stream Deck app on this PC.

## Where the keys come from

Every button is one game action, and the key it presses is resolved the same way
the Controls page and voice commands resolve it:

1. **Your rebinding**, from the active `actionmaps.xml` profile, if you changed it.
2. **Nothing**, if you cleared it. A cleared binding stays cleared rather than
   quietly falling back to the default, because the default may now be on
   something else.
3. **The game's default**, from the control catalogue read out of the install.

Only keyboard keys, key chords and mouse buttons can be sent. An action bound
only to a stick or a gamepad is listed with the reason it is unavailable and
cannot be selected. On this install (measured 2026-11-08) that is 511 of the
1,103 actions. Giving one a keyboard key in the game's options and rebuilding
makes it available; offering free chords for them, as voice commands already
do for theirs, is not built yet.

**Rebuild after rebinding in game.** A profile is a snapshot: it carries the
key, not a link to the binding. The server resolves every selected action again
when the profile is built, so a profile built today never sends yesterday's key,
but one built yesterday does.

## What a profile contains

- **Two models:** 15 keys (5 × 3) and XL (32 keys, 8 × 4).
- **Folders by category.** The home page holds one folder per category; Back
  sits top left on every page, and a category with more commands than fit gets
  More at the bottom right.
- **132 preset commands in 11 categories:** Flight 12, Combat 13, Power 12,
  Shields 7, Mining 15, Salvage 15, Scan 11, Tractor Beam 10, Ground Vehicle 14,
  Ship & Utilities 14, Extended 9. The first 97 were transcribed from the
  original D:\streamdeck generator into `integrations/streamdeck/presets.json`,
  each tied to one action-map id; all 132 name an action this install's game
  defines. Extended is off by default, so a first profile offers 123 commands in
  10 folders, less any your bindings cannot reach (below). Any of the game's
  other actions can be added from "All game commands".
- **The game as its application.** The profile names `Bin64\StarCitizen.exe`
  from the selected install, so Stream Deck switches to it when the game is in
  front.

The two mining-power presets turn the mouse wheel the game uses for laser power
into separate up and down pulses, and only while that axis is still bound to the
wheel.

## Operator and master modes

Each mode key sits in the folder where it is used: NAV, SCM and Quantum in
Flight, Guns and Missiles in Combat, Scan in Mining, Salvage and its own Scan
folder. Mining and Salvage also carry **Next Mode**, the industrial cycle.

4.10.3 left most of these without a key. On this install's bindings only four
mode actions have one: the industrial cycle (M), Scan (V), the master-mode
cycle (B, held) and Next Operator Mode (middle mouse). Mining Mode, Salvage
Mode, NAV, SCM, Quantum, Guns and Missiles are all unbound, so the page shows
those seven greyed with the reason until they are bound in game; that is why
Next Mode is beside Mining Mode and Salvage Mode rather than instead of them.

The Tractor Beam folder holds the hand tractor beam (pull in, push out,
rotate, detach, throw) and the ship tractor's distance. Most of them are on the
mouse wheel, so Pull In and Ship Beam In send the same wheel turn: which one
it moves depends on what you are holding, as it would with the wheel itself.

## Shields

The Shields folder (1.3.11) holds the six shield faces, Front, Back, Left,
Right, Top and Bottom, and Reset: the game's "Shield raise level" actions under
Vehicles - Shields and Countermeasures. They sit beside Power rather than inside
it, because Power's keys are the engineering pips (SHD +/−/MAX), which move
power into the shield, not round it.

Neither 4.10.3 nor the 5.0 PTU gives any of the seven a default key, and this
install's own bindings leave them unbound too, so the page shows all seven
greyed with the reason until they are bound in game (Options → Keybindings →
Vehicles - Shields and Countermeasures). The key appears on the deck the next
time the profile is built. What a face press does to a given shield generator
has not been seen in a log or on screen here; the folder sends the key and
nothing more.

## Holds and taps

A button behaves like the key it stands for: it goes down when the button goes
down and up when the button comes up. **Touches are never stretched to the
hold threshold.** Self destruct's half-second hold is its safety catch, and 11
actions (Decoy among them) do one thing on a tap and another on a hold, so a
button that held every press for the threshold would turn every tap into the
hold action.

What the page does instead is say so. Each command reads the game's own
activation rule — the same rules voice commands use — and shows "Hold 0.5 s
before it fires", "Tap, or hold 0.25 s for its hold action" or "Hold the
button". On this install 92 actions need a hold and 12 fire on a double or
triple tap.

## The plugin

Profiles use the **Star Citizen Commands** plugin
(`com.nlebel.starcitizen`), bundled with the app and offered as a download on
the page. Open in Stream Deck installs it the first time; click it again after
installing to import the profile.

- **Windows x64, Stream Deck 7.1 or later.** Its native keyboard dependency is
  x64 only.
- **Only into the game.** By default a press is dropped unless Star Citizen is
  the window in front, so a button pressed over a browser does not type into it.
- **Physical key positions.** Keys are sent as scan codes, so an AZERTY or other
  layout presses the key in the place the game expects.
- **The same privilege as the game.** Windows will not let a normal process send
  input to an elevated one; run Stream Deck and Star Citizen at the same level.

The plugin's version is held in two places, its `manifest.json` and
`StreamDeckProfiles.PluginVersion`. Stream Deck installs the bundled copy only
when it is newer than the one in place, and a test fails if the packed installer
differs from its source.

## Key art

Each command key is drawn by `bin/key-art.js`, the same file the dashboard's
preview loads (served at `/api/streamdeck/art.js`), so the preview is the key
rather than an approximation of it. The icon and colour come from
`data/appearance.json`: a category gives the default, and words in the action
id or label pick a function icon (gear, shield, missile, quantum, and so on).
The match uses only the last part of a preset id, because the category inside
it would otherwise give every Flight key a lamp ("light" in "flight").

Folder, Back and More keys are PNGs with no words on them, rendered from the
same art by `tools/render-streamdeck-art.mjs`. Stream Deck draws the folder's
own title under the emblem; the first folder picture had "Ship & Utilities"
drawn into it, and every folder showed two names on top of each other (fixed in
1.2.10).

**Labels never break inside a word.** The device's SVG renderer has no canvas
to measure text with, so widths come from a table of Segoe UI advances measured
in Chrome. A label shrinks before it wraps; a word too wide for the key breaks
only after `_`, `/` or `-` or where a lower-case letter meets a capital, then
drops one size, and only then is hyphenated. Over the 1,093 distinct labels in
this install's catalogue that took mid-word breaks from 313 to 0 (1.2.14); 3
all-lowercase debug ids such as `combathealtarget` are hyphenated, and 126
labels too long for three lines still end in "…", with the full label shown
when the key is selected in the preview.

## Not yet measured

- **The hardware.** Profiles have been built, unpacked and checked against the
  format, and the art rendered; a press on a physical deck going into the game
  has not been recorded here.
- **The import prompt** on each model, and Open in Stream Deck from the
  Microsoft Store build, which hands the same temporary file to the same file
  association but has not been tried.
