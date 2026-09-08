# Screen insight

A plan for reading a Star Citizen screenshot and saying what is in it: what the
item is, what the install knows about it, and what UEX says it sells for.

Written before any code. Everything under **Verified** was checked on this
machine and carries what was found; everything under **Not yet known** is
waiting on a real screenshot and is marked as waiting rather than guessed at.

---

## The one rule this feature is built around

**The app reads a file. It never reads the screen.**

Enumerating windows, grabbing a framebuffer, or hooking a hotkey that captures
the display is the class of thing anti-cheat exists to notice, and it is not
worth a feature. A PNG the pilot saved is inert — reading it is the same act as
reading `Game.log`, which this app has always done.

So: no capture APIs, no window handles, no screen hotkey. The pilot takes the
shot however they already do; the app watches a folder or accepts a drop.

That is a constraint on the implementation, not a limitation to apologise for.
It also means the feature works on a screenshot taken last week, on another
machine, or sent by somebody else.

---

## What the licence actually says

Read from the [EULA](https://robertsspaceindustries.com/eula) and the
[Terms of Service](https://robertsspaceindustries.com/tos) rather than from
forum wisdom. Quoted so the wording can be checked rather than trusted.

Nobody here is a lawyer and this is not advice. It is the text, and what the
text plainly does and does not cover.

### Named and prohibited

| Clause | Where | Does this feature do it? |
|---|---|---|
| "any software that reads areas of RAM used by the Game to store information about a character or the game environment" | EULA §I | **No.** Nothing touches the game process. |
| "intercept, emulate or redirect the communication protocols used by RSII" | EULA §I | **No.** No network traffic of the game's is touched. |
| "Modify or cause to be modified any files that are a part of the Game Client in any way not expressly authorized by RSII" | EULA §I | **No** — for this feature. See below for what does. |
| "Use cheats, automation software (bots), hacks, mods or any other unauthorized third party software designed to modify the Game experience" | EULA §I | **No.** Nothing is automated and the game experience is unchanged. |
| "'auto' software programs, 'macro' software programs or other 'cheat utility' software" | ToS | **No.** No input is generated. |
| "'bots', 'spiders', 'scrapers' … data scraping or harvesting" | ToS | **No.** That clause governs the RSI Services - their site and servers - which this app never contacts. |

### Not mentioned at all

**Neither document says anything about screen capture or screenshots.** Not
permitted, not prohibited — absent. So the honest position is that reading a
screenshot is unaddressed rather than blessed, and the argument for it is that
it does none of the things that *are* named.

What the ToS does address is fan content, and generously: "You may make review,
gameplay, tutorial, or fan commentary videos using RSI Content". A player is
plainly expected to capture what is on their screen. What they may then do with
it locally is not restricted anywhere in either document.

### Where the real exposure is, and it is not here

Two things this app already does sit closer to the line than anything in this
plan.

**The label overlay writes to the game folder.** Installing item labels — and
StarStrings — replaces `Data\Localization\english\global.ini`, which is a file
that is part of the Game Client. The EULA's wording is "Modify or cause to be
modified any files that are a part of the Game Client in any way not expressly
authorized by RSII". A text mod is a long-standing and openly tolerated part of
this community, and the app is careful to back the file up and put it back — but
tolerated is not the same as authorised, and this document should say so rather
than let a screenshot reader carry a worry that belongs elsewhere.

**The catalogues are read out of `Data.p4k`.** Item names, ship data and the
star map's own paragraphs come from the install's proprietary archive, which
requires understanding its format. Whether that is "reverse engineer" or merely
"read a file the user owns" is a question this doc cannot settle, and it is
worth knowing that it is the older question.

Neither is a reason to stop. Both are reasons not to pretend the new feature is
where the risk lives.

### The limit this feature sets itself

Reading a saved file does none of the named things, and it is also the variant
that stays furthest from the game process: no window handles, no capture APIs,
no hooks, no hotkey that grabs the display, and nothing running while the game
runs. The image is a file on disk like `Game.log` is a file on disk.

If that ever stops being enough, the answer is to stop — not to find a cleverer
way in.

---

## What other people already built

Checked before designing anything, because several of these have been through
exactly the problem this doc is about.

**Star Citizen Navigation** ([Valalol](https://github.com/Valalol/Star-Citizen-Navigation))
does not use OCR at all, and that is the most useful thing in this section. It
watches the clipboard: the in-game `/showlocation` command copies the player's
global coordinates there, and the tool reads them as text. Exact, free, no
pixels involved.

The lesson generalises past navigation. **Before reading a screen, find out
whether the game will simply say it.** For coordinates it will. For an item's
name and price there is no equivalent command as far as this doc knows, which
is what leaves OCR on the table — but that is a thing to confirm rather than
assume, and it is cheap to confirm.

**Datarunner for UEX** ([shebuka](https://shebuka.github.io/SC-Datarunner-UEX/))
is the closest prior art to the half of this feature that matters most. It
reads trading terminal screens with Tesseract and pulls out the terminal name,
the commodity, SCU, stock level, price and whether the terminal buys or sells,
then submits it to UEX. That is the same class of text this feature wants, off
the same screens, and it works well enough that UEX takes the data.

**ContractTracker V2** ([Nexus](https://www.nexusmods.com/starcitizen/mods/34))
uses Windows 10/11's built-in recognition with Tesseract as a fallback. That is
the arrangement this doc arrived at independently, which is mild evidence it is
the right one rather than merely the convenient one.

**SC Signature Scanner**
([seneca0815-rgb](https://github.com/seneca0815-rgb/SC_Signature_Scanner))
does not feed the raw frame to OCR. It finds the scanner's pills with OpenCV,
applies adaptive thresholding and morphological operations, and only then runs
Tesseract in single-line mode for digits. Preprocessing is not a nicety on this
kind of source.

**SC Toolbox** ([ScPlaceholder](https://github.com/ScPlaceholder/SC-Toolbox-Beta-V2))
went further still: its mining signal reader is a purpose-trained CNN rather
than an OCR engine. Somebody reached for that because general-purpose OCR was
not good enough on that particular HUD element — which is the honest ceiling of
this approach, and worth knowing before promising anything about the scanner
HUD specifically.

### What that changes here

1. **Ask the game first.** A command that puts text on the clipboard beats any
   amount of image processing. Worth ten minutes before step 1.
2. **The in-box engine with Tesseract behind it is a known-good arrangement**,
   not a guess.
3. **Terminal and panel text is proven readable.** Datarunner does it in
   production. The scanner HUD is the hard case, and is not what this feature
   is aimed at.
4. **Expect to preprocess.** Every one of these tools does something to the
   image before the engine sees it.

One difference worth stating: most of these capture the screen live. This one
reads a file the pilot saved, which is the more conservative variant of an
established practice rather than a new idea.

---

## Verified on this machine

**Windows has an OCR engine, offline and in the box.**

```
C:\Windows\System32\Windows.Media.Ocr.dll      876,544 bytes
C:\Windows\OCR\en-us\MsOcrRes.orp              (English model, installed)
```

`Windows.Media.Ocr` gives text plus a bounding box per word, needs no network,
and ships with the operating system — nothing to download, nothing to bundle,
and no outbound request, which matters because standalone mode makes none.

Reaching it needs a Windows target framework. `Quantumwake.Overlay` is already
`net10.0-windows`, so the precedent exists.

PowerShell 7 could not resolve the type. That is PowerShell dropping its WinRT
bridge, not the engine being absent — the DLL and the language model are both
on disk.

**The alternative, for the record.** Tesseract is more tunable on awkward fonts
and costs a native binary plus roughly 15 MB of training data to ship. Worth
reaching for only if the game's font defeats the in-box engine.

---

## Measured

Step 1 has been done. Nine screenshots from this install went through the
in-box engine; every number below came out of that run rather than out of an
expectation.

### Where the game puts them, and in what shape

```
E:\rsi\StarCitizen\LIVE\screenshots\ScreenShot-2026-09-07_21-09-02-4D4.jpg
```

Lowercase `screenshots`, beside the install. Named
`ScreenShot-<date>_<time>-<hex>.jpg`.

**JPEG, 3440x1440, 726-931 KB.** That is 0.15 to 0.19 bytes per pixel, which is
firm compression — and it turned out not to matter, which is the more useful
half of the finding.

### The engine

`Windows.Media.Ocr`, `en-US`, reached from a `net10.0-windows10.0.19041.0`
console project with **no package reference at all**. `MaxImageDimension` is
10000, so a 3440-wide frame goes in whole.

| Input | Time | Lines |
|---|---|---|
| Full frame, 3440x1440 | **170 ms** | 59 |
| One 400x250 panel crop | **45 ms** | 7 |

Fast enough that cropping is an accuracy decision, not a performance one.

### What it read

Text heights ran 11 to 42 pixels; the item names that matter sat at 12-16.

The names come back **exact**, which is the whole ballgame:

```
Missile Rack 4
MSD-423 Missile Rack
DRAKE CORSAIR
Only showing ships and equipment located in Nyx.
```

`MSD-423 Missile Rack` is precisely the string that joins to the catalogue, and
it is character-perfect.

The errors are real but confined, and they are all punctuation and glyph shape:

| Read as | Should be |
|---|---|
| `[IR31 chaos' Missile` | `[IR3] 'Chaos' Missile` |
| `tCS31 Arrester Missile` | `[CS3] Arrester Missile` |
| `MSO-423 Missile Rack` | `MSD-423 Missile Rack` |
| `Missile Slot I` | `Missile Slot 1` |
| `129.Missile (Missile)` | `129,Missile (Missile)` |

Closing brackets become `1`, opening brackets become `t`, `D` becomes `O`, `1`
becomes `I`. Every one of them is a shape the engine confuses under 13 pixels,
and none of them touches a whole word. The same line read correctly elsewhere in
the same frame — `[IR3] 'Chaos' Missile` came out perfectly one row down — which
says this is marginal rather than systematic.

**The matching layer has to be tolerant of exactly this class of error and no
more.** Not general fuzziness: a scorer loose enough to fix `MSO` to `MSD` is
loose enough to confuse `P4-AR` with `P8-AR`, and that is the failure mode this
app cannot afford.

### The tooltip, which is the case the feature is for

An eighth screenshot arrived afterwards and is the most useful of the set: a
**looting view** with an item hovered, so the game's own tooltip is open. That
is the moment the whole feature targets — standing over a body, deciding
whether the rifle is worth the slot.

The tooltip crop is 470x490. It read in **47 ms**, 19 lines:

```
Arlington Rifle
Volume: 13000 pscu
Manufacturer. Hedeby Gunworks
Item Type: Rifle
Class: Ballistic
Magazine Size: 20
Rate Of Fire: 320 rpm
Effective Range: SO m
Attachments: Optics (S2). Barrel (S2),
Underbarrel (S3)
```

Every field the panel would want to key on is there, and the name —
`Arlington Rifle` — is again character-perfect. The errors are the same
confined class as before plus two new members:

| Read as | Should be | What happened |
|---|---|---|
| `pscu` | `μSCU` | mu has no ASCII neighbour, so it guessed `p` |
| `Manufacturer.` | `Manufacturer:` | colon flattened to a full stop |
| `SO m` | `50 m` | zero/O, five/S, in one two-character token |
| `(S2).` | `(S2),` | comma flattened to a full stop |

`SO m` is the one that matters, because it is a **number** being misread rather
than punctuation, and a number is what a price or a rating would be. It is also
the shortest token in the block — two characters with no word around them to
constrain the guess. That is the argument for reading tooltips **by label**
(`Effective Range:` → take the rest of the line) rather than scraping loose
numbers off the frame: the label survives, and it says what the number means.

The lore paragraph below the stats came back partially eaten (`accura
marksmanship`, `equall it f r the nt as`) because it is set over the item's
render and the contrast collapses. That is fine — nothing needs the lore.

### The map footer gives the place away for free

A ninth screenshot is the **mobiGlas Maps** view, inside a hangar. Its footer
strip reads, at full resolution and in one line:

```
PYRO > DUDLEY & DAUGHTERS   0.00° -155.25° 68.33GM
```

OCR returns that as:

```
9 PYRO DUDLEY B DAUGHTERS > 0.000 -155.250 68.33GM
```

System name, location name, and all three figures — correct. `&` became `B`,
and the degree signs merged into the numbers as trailing zeroes. `68.33GM` came
through exactly.

This is a direct answer to a question left open in
[precise-poi.md](precise-poi.md): `/showlocation` gives coordinates with no
name, and the app has to guess the place from the nearest catalogue body. **The
map footer gives the name and the position together**, which is the pairing that
turns one reading into a labelled point of interest.

Two cautions before anything is built on it. The trailing-zero problem means
`-155.250` has to be parsed as `-155.25°` — which is safe only because the
degree symbol is the *only* thing that can sit there. And upscaling made this
line **worse**, not better: at 3x the engine dropped `DUDLEY` entirely. Native
resolution is the right input, here and everywhere else measured so far.

### The one thing it will not read

**The wallet balance.** `1,971,263` sits at the bottom of the mobiGlas in an
italic display face, and the engine returns nothing for it — only the handle
`NEKRON` beside it.

That is not a size problem. Cropped tight, upscaled three times with Lanczos,
and contrast-stretched to greyscale, it still comes back with `NEKRON` alone:

```
wallet.png       ->  NEKRON
wallet_3x.png    ->  NEKRON
wallet_3x_bw.png ->  NEKRON
```

The regular UI face reads perfectly at 12 pixels; the italic face does not read
at 42. **It is the typeface, not the resolution.**

Confirmed a second time on the Maps screenshot, which is a different screen on a
different day with a different balance — same result: `NEKRON` reads at 1x and
at 3x, the number reads at neither.

This is the honest ceiling of the in-box engine, and it is worth knowing which
side of the line each ambition falls on:

- **Item names, part numbers, ship names, the system name** — the regular face.
  Read them today.
- **The wallet balance** — the display face. Needs Tesseract with a trained
  model, or something purpose-built. SC Toolbox reached for a CNN for their HUD
  for what is very likely this same reason.

Which is a shame, because the balance is a genuinely new signal: `Game.log`
records what was spent and earned and **never the total**, so the Ledger has a
running sum it has never once been able to check against the truth.

### Step 2: what the catalogue makes of it

Built and measured on the same eight screenshots. The matcher is
`src/Quantumwake.Data/ScreenInsight.cs`; the harness is
`dotnet run --project src\Quantumwake.Cli -- --screen <lines.txt>`, which takes
the engine's output as plain text so a bad match can be reproduced by editing a
file. Reading a frame needs Windows; naming what was read does not, and keeping
them apart is what lets the matching be tested at all.

**The looting-view tooltip resolves to one item, certain**, against the 26,028
items this install holds:

```
Arlington Rifle  [Exact]
    class    hdgw_rifle_ballistic_01
    agrees   manufacturer, volume, item type, class
    against  (nothing)
```

Four independent agreements, and the volume is the good one: the tooltip prints
13000 μSCU and the catalogue holds 13000 micro-SCU, in the same unit, from a
completely different source. That is the same kind of check that caught the
centi-SCU error in commodity buying.

**`MSO-423` finds `MSD-423`**, which was the whole point of folding anything.
The measured confusions are undone and the comparison stays an equality - no
edit distance, because a scorer loose enough to repair `MSO` is loose enough to
read `P4-AR` as `P8-AR`, and there is a test holding that line.

#### Three things the catalogue does not say the way the screen says it

**The tooltip's words for what a thing is are not the catalogue's.** The
Arlington's tooltip says `Item Type: Rifle` and `Class: Ballistic`. The
catalogue's own `Type` and `SubType` for the same weapon are `WeaponPersonal`
and **`Medium`** - a size class, not an ammunition class. Comparing them
directly reported a contradiction on a candidate that was correct.

Those words do appear, in the *class name*: `hdgw_rifle_ballistic_01` carries
both "rifle" and "ballistic". So they are looked for there, and they can only
ever vouch for a candidate, never convict one - a class name is an id and owes
no particular words to anybody. Manufacturer and volume are the two fields that
can convict, because those the catalogue holds in the same words and the same
unit.

**The loadout screen does not print catalogue names.** It shows
`[IR3] 'Chaos' Missile`; the catalogue calls the same missile
`'Chaos' III Missile`. That is not a misreading - the screen decorates the name
with a tag the catalogue spells out in the class name instead
(`MISL_S03_IR_VNCL_Chaos` is infrared, size 3). No amount of glyph folding
bridges it, and it is why the missiles on those frames match nothing while the
racks beside them match exactly.

**"Available" is an item.** The catalogue holds entries named `Available` -
empty rack slots - so the loadout screen's own `Available:` heading matched
three of them exactly. A line ending in a colon introduces something rather
than naming it, and no longer counts.

#### A frame is usually not a tooltip

Six of the eight screenshots are loadout screens: thirty names, no labels to
anchor on, no stats to corroborate with, and no single thing the frame is
about. So there are two questions, not one. A tooltip is read and matched with
its fields; a frame is swept line by line, and a sweep refuses partial matches
outright, because with no manufacturer and no volume to check a guess against
half a name is not evidence.

| Frame | Result |
|---|---|
| Looting view, item hovered | one item, **certain** |
| Loadout × 5 | 14 lines named exactly, 21 ambiguous |
| Loadout, component tooltip | 2 named |
| mobiGlas map | nothing named, correctly |

The ambiguous ones are almost all honest ties: `DRAKE CORSAIR` matches twelve
catalogue entries all named "Drake Corsair" - `DRAK_Corsair`,
`DRAK_Corsair_Exec_Military`, and ten more. Reporting twelve is the right
answer. Picking one would be a coin toss with a confident face on.

#### Reading by position, not by order

**OCR line order is not layout order**, and the first version of this step
assumed it was. Taking the name to be the line directly above the first
labelled one worked on the looting tooltip and failed on the component
tooltip, where the engine returned `MAPS` - the toolbar item at the bottom of
the screen - immediately before `Manufacturer: Drake Interplanetary`. The app
confidently reported the toolbar as the item.

The engine gives a bounding box per line, so the question is geometric. The
thresholds come from the frames rather than from taste:

| | Name | First stat | Apart |
|---|---|---|---|
| Looting tooltip | x 1243, y 379 | x 1243, y 399 | **0 across, 20 up** |
| The misfire | `MAPS` x 1800, y 1329 | x 2097, y 995 | 297 across, *below* |

So the name is the nearest unlabelled line **above** the topmost label, within
**two line heights across** and **three line heights up**. Distances are
measured in line heights rather than pixels, so the rules hold at whatever
resolution the game is played at - the real gap is one line and the real
misfire was eighteen line heights away and on the wrong side.

The same geometry fixed something that had not been noticed: **stats are now
taken only from the anchor's own column**. A frame can hold two tooltips, the
item hovered and the one already equipped shown beside it, and the old rule
would have mixed their stats into one reading that described neither.

Re-run across all eight frames, the misfire is gone: the looting tooltip still
resolves certain, and the component frame now reports no name - which is the
truth, because the component's name is not in the reading at all.

#### Two halves of an answer that have not been introduced

That component frame is worth dwelling on. The tooltip gives:

```
Manufacturer: Drake Interplanetary
Type: Flight Blade
```

and no name. Meanwhile the sweep of the same frame finds, elsewhere on it:

```
"Drake Corsair Standard Flight Blade"  [exact]
    Drake Corsair Standard Flight Blade  (Controller_Flight_DRAK_Corsair)
```

The two agree completely and nothing joins them. A manufacturer and a type
with no name still narrow 26,028 items to a handful, and here they narrow it
to exactly the thing the sweep already found. **Matching on fields alone, and
crossing that against what the rest of the frame names, is the obvious next
move** - and it is only obvious because a real screenshot was measured rather
than imagined.

### What this settles

The feature is viable, with the in-box engine, on unmodified JPEGs, at full
resolution, in under a fifth of a second — for the text it was actually aimed
at. No Tesseract, no preprocessing, nothing to ship.

The scanner HUD and the display-face numbers are a different project, and this
document should not pretend otherwise.

## The fleet, where the logs say nothing at all

The idea this arrived at: a screenshot is not a second-rate source of the
things the logs already carry. For one thing it is the **only** source, and
that thing is what you have fitted to your ships.

### The measurement that settles it

Across the 179 log backups in this install:

| Searched for | Times found |
|---|---|
| `hardpoint` | **0** |
| Any `Port[...]` naming a ship component | **0** |

Every `Port[...]` the game writes is a person: `Armor_Helmet`,
`magazine_attach`, `weapon_attach_hand_right`, `inventory_pocket_2`. The logs
record what the pilot is wearing in exhaustive detail and record **nothing
whatsoever** about what is bolted to their ship.

What the Fleet page shows today is `ShipSlot.Fitted`, and its own summary says
what that is: *"The part in it as the ship comes"* - the factory loadout out of
the game files. It is the same for every player who owns that ship. If you have
swapped every component on your Corsair, the app has never known.

**So this is not a better source of truth. It is the first one.**

### What a loadout frame gives, measured

From `ScreenShot-2026-09-07_21-09-02-4D4.jpg`, the engine's own boxes:

```
[ 727  334 h16]  Missile Rack 4          <- the slot
[ 728  356 h14]  MSD-423 Missile Rack    <- what is in it
[ 746  421 h15]  Missile Slot 2          <- a slot with nothing under it
[ 750  507 h15]  Missile Slot 1
[ 739  591 h15]  Missile Rack 1
[ 740  613 h12]  MSO-423 Missile Rack
```

Three things fall out of that.

**The pairing rule is the one already built, upside down.** A fitted part is
the line directly *below* its slot label, in the same column: 22 pixels down
and 1 pixel across, against a 16-pixel line. That is the same geometry as a
tooltip's name sitting directly above its stats, and the same constants apply -
though the column tolerance has to be looser here, because the tree indents and
the left edges wander across 46 pixels between depths.

**An empty slot is visible as an absence.** `Missile Slot 2` at y=421 has
nothing beneath it until the next slot label at y=507 - an 86-pixel gap where a
filled slot has 22. A frame therefore says *this port is empty*, which is a
claim, not a silence.

**The frame says how complete it is.** The game prints its own caveat at the
top, and the engine reads it:

```
Only showing ships and equipment located in Nyx.
```

So a loadout frame is a statement about the ships at one place, not about a
fleet. Anything built on this has to carry that sentence through to the page
rather than quietly presenting a partial fleet as a whole one.

### What a fitting would have to be

Not a value on a ship, but a record with a provenance and a date:

| Part | Where it comes from | How certain |
|---|---|---|
| The ship | the frame, matched to the fleet | a name, exactly read |
| The port | the frame's slot label | as the game labels it, not the game's id |
| The part | the frame, matched to the catalogue | exact or corroborated only |
| When | the screenshot's own timestamp | exact |
| Where the fleet was | the frame's own caveat line | the game's words |

Two honesty constraints follow, and they are the whole difference between this
being useful and being a liability.

**A screenshot is a moment, never a state.** It says what was fitted when the
shot was taken. A page that shows it as *the* loadout is lying by a week. The
wording has to be "as you last photographed it, on 7 September", the same way
kit sightings are already worded.

**The slot labels are not the game's port ids.** The screen says `Missile Rack
4`; the data says `hardpoint_missilerack_4` or something else entirely. Joining
them is a separate problem and probably a partial one, so a fitting should
stand on its own terms - the pilot's words - and join to `ShipSlot` only where
the join is certain. Half a join is worse than none: it would put a real part
in the wrong port.

### Choosing the source

The idea of *selecting* a source of truth is right, but it is narrower than it
sounds, because there is only one source for most of this. Where both exist the
rule can be plain:

- **A port the factory fills and a screenshot confirms** - agreement, say so.
- **A port the factory fills and a screenshot contradicts** - the screenshot
  wins, and the page says the ship is not stock.
- **A port only the factory knows** - shown as the factory part, labelled as
  such, because that is a guess about this pilot's ship.
- **A port only a screenshot knows** - shown, dated.

That is a preference over provenance, not a switch to be flipped, and it can
be stated in one sentence on the page instead of being a setting.

### What the next screenshots need to show

Not more of the same. The frames measured so far are all missile racks on one
ship, which is the easy case - a repeated part in a repeated slot. Worth
having:

1. **A ship with mixed components** - shields, coolers, a power plant, a quantum
   drive - so the slot labels of different kinds can be read rather than one
   kind generalised from.
2. **A ship that is visibly not stock**, so the disagreement case is real
   rather than imagined.
3. **The same ship twice, some time apart**, which is the only way to see what
   a stale fitting looks like.
4. **A frame with the fleet list visible**, to find out whether ship names read
   as reliably as component names do.

---

## How the pieces would fit

The striking thing is how little of this is new.

1. **Load the file.** Trivial.
2. **OCR to text and word boxes.** The only genuinely new code.
3. **Match the text against catalogues the app already holds** —
   `LogLibrary.Items()` for install and community items with display names,
   `Community.Ships`, the commodity tables, and `UexData` for prices.
4. **Draw a panel.** The entity drawer already describes an item, a ship, a
   place and a commodity; this would open the same one.

Step 3 is solved. Step 4 exists. The feature is mostly plumbing between things
that are already here, which is why it is a small feature rather than a project.

### Shape

```
ScreenReading
  Source        the file, and when it was taken
  Lines         every line OCR read, with its box and confidence
  Candidates    what each line might be, best first

ScreenCandidate
  Kind          "part" | "ship" | "commodity" | "place"
  Id            the class the drawer resolves
  Name          what the catalogue calls it
  Read          the text OCR actually saw, kept verbatim
  Score         how close the two are
```

`Read` is not a debugging aid. It is the thing that makes a wrong answer
visible, and it belongs on screen.

### Where the code lives

OCR needs a Windows TFM; the server does not have one. Three options, in the
order I would consider them:

1. **A small Windows-targeted project behind an interface** — `IScreenReader`
   in Data, implemented in a new `Quantumwake.Ocr` targeting
   `net10.0-windows10.0.19041.0`, referenced by the server. Keeps the rest of
   the app free of a Windows target and makes the engine swappable if Tesseract
   ever wins.
2. **Bump the server's TFM.** Fewer moving parts, and the app is Windows-only in
   practice — WPF overlay, WebView2. But it drags every consumer of the server
   project along with it.
3. **Do the OCR in the overlay process** and post the text to the server. Worst
   of the three: the feature would only work while the overlay is running, and
   the dashboard is where somebody would actually sit and read this.

Option 1 unless the interface turns out to be one method with no second
implementation in sight, at which point option 2 is honest.

---

## What the panel must not claim

**OCR is a guess, and the panel shows its working.** If it reads `P4-AR Rlfle`
and matches `P4-AR Rifle`, both appear with the score between them. Silently
correcting is how somebody gets a confident answer about the wrong gun.

**A low score is a question, not an answer.** Below whatever threshold we
settle on, the panel offers candidates rather than naming one — the same
three-valued honesty the kit preparation uses, for the same reason.

**Rarity is not a thing the game states.** There is size, there is grade, and
there is whether anything in the dataset sells it — the text overlay already
marks gear nothing stocks. That last one is the honest analogue and a genuinely
useful answer. Inventing a rarity tier would be the app making something up.

**Prices are UEX's and carry their age.** The existing rule stands: a stale
price shown without its age is the one number on a card that can lose real
money.

**Nothing is written to the game folder, and nothing leaves the machine.** The
image is read and forgotten unless the pilot asks to keep the reading.

---

## Open questions

1. **Whole frame or a region?** Reading the whole screenshot is simple and
   returns a lot of noise — chat, HUD, ship name, everything. Reading a region
   is more accurate and asks the pilot to draw a box. A middle path: read the
   whole frame, then rank candidates by how close they sit to the centre, since
   an inspected item is what the player has centred. Cheap to try once there is
   a real frame.

2. **How does a screenshot reach the app?** A watched folder is the least
   friction — new file appears, panel updates. A drop target is more explicit
   and needs no configuration. A watched folder also means the app is following
   a directory the pilot may keep other things in, which wants saying out loud.

3. **What happens with several items on screen?** An inventory screen is dozens
   of names. Listing all of them is probably right, and probably wants the same
   grouping-by-source the global search uses.

4. **Is the panel in the overlay, the dashboard, or both?** The overlay is where
   somebody is while playing; the dashboard is where they would sit and read. If
   the reading is a server endpoint, both come nearly free.

---

## Build order

Each step is worth having on its own, which is the test of whether the order is
right.

0. **Ask whether the game will just tell us.** Star Citizen Navigation gets
   coordinates from `/showlocation` and the clipboard, with no image at all. If
   any in-game command names the item under the cursor, this feature is a
   clipboard reader and the rest of this document is moot. Ten minutes, and it
   is the only step that could make the other four unnecessary.
1. ~~**Read a file and print what OCR saw.**~~ **Done** — see **Measured**
   above. 170 ms for a full frame, names character-perfect, and one hard limit
   found in the italic display face.
2. ~~**Match lines to the catalogues**, with scores, and a CLI or endpoint
   that prints the candidates.~~ **Done** - see **Step 2** above. One tooltip
   resolves certain, the loadout frames name what they can and tie where they
   should, three places where the screen and the catalogue disagree about what
   a thing is called are written down, and the reading is done by position
   after list order turned out to name the wrong thing.
2b. **Match on fields when there is no name.** A manufacturer and a type narrow
   26,028 items to a handful, and one measured frame has exactly that and no
   readable name. Small, and it uses only what is already here.
3. **The panel**, showing what was read beside what it matched, with the
   existing entity drawer behind a click. The reading is positional now, so
   whatever does the OCR has to hand the boxes through rather than a list of
   strings.
4. **Prices and stock**, from UEX and the dataset, reusing the commodity and
   part cards rather than drawing new ones.
5. **Fitted loadouts for the fleet** - see above. Last in the order and first in
   value: it is the only one of these that tells the pilot something no other
   part of the app could ever have told them.

Step 1 was not a formality and did not go entirely the expected way: the panel
text read better than hoped and the wallet balance did not read at all. Both of
those change what step 2 should try to match, and neither would have been
guessed correctly from a design conversation.
