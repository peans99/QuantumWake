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

## Not yet known

No Star Citizen screenshot exists on this machine, so none of the following is
stated:

- Where the game writes them. `E:\rsi\StarCitizen\LIVE\ScreenShots` does not
  exist, `%USERPROFILE%\Pictures` and `%USERPROFILE%\Videos\Captures` are both
  empty, and there is no `user.cfg`.
- The format. PNG and JPEG have very different consequences here: JPEG
  artefacts around small high-contrast text are the classic way OCR accuracy
  falls over.
- The resolution, and therefore how many pixels tall the UI text actually is.
  This is the single number that decides whether preprocessing is needed.
- How the game's UI font behaves under OCR. Interface text is crisp, evenly
  spaced and high contrast, which is the easy case — but "easy case" is a
  prediction until a real frame has been through the engine.

**The first screenshot settles all four**, and should be a full unedited frame
with its path and the display resolution, not a crop. The crop is part of what
has to be worked out.

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
1. **Read a file and print what OCR saw.** No matching, no panel — just the
   lines and their boxes, on a real screenshot. This answers every one of the
   Not yet known questions and is the point at which the feature is either
   plausible or not.
2. **Match lines to the catalogues**, with scores, and a CLI or endpoint that
   prints the candidates. Still no UI.
3. **The panel**, showing what was read beside what it matched, with the
   existing entity drawer behind a click.
4. **Prices and stock**, from UEX and the dataset, reusing the commodity and
   part cards rather than drawing new ones.

Step 1 comes first and is not a formality. If a 1440p screenshot of a mobiGlas
panel does not read cleanly, the honest answer is preprocessing or Tesseract or
neither — and it is much better to learn that before there is a panel to
disappoint somebody with.
