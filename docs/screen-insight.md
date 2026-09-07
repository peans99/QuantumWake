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
