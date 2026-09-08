# Points worth marking

A plan for knowing exactly where something is, rather than roughly which
station it was near.

Written before any code. The coordinate below is a real `/showlocation` reading
from this install's owner; everything measured from it was computed against the
app's own position data and carries the number that came out.

---

## What the app knows about "where" today

A name. Not a position.

`ResolvedLocation` carries `RawId`, `DisplayName`, `System`, `Body`, `Kind` and
`IsResolved`. There is no coordinate on it, and nothing in `Game.log` carries
one — arrivals and quantum jumps give names and ids. A receipt's place is
back-tracked from the last arrival before it rather than logged with the sale,
which the Ledger and the run review both say out loud.

The only coordinates the app holds are **31 planet and moon centres across
three systems**, 1,734 bytes of `digest-positions.json`, X and Y only:

```json
"pyro": { "Bloom": { "X": ..., "Y": ... }, "Pyro V": { ... } }
```

Enough to lay out a system map. Nothing finer. **No stations, no Lagrange
points, no jump points, no belts, no wrecks, no caves, no bunkers** — every
place worth marking precisely is absent by construction.

---

## The evidence: one real reading

```
x: -9641671346.904709
y: -11490734321.189394
z:    -91805.115677
```

Three things fall out of it.

**It is 15.0000 Gm from the system centre.** 14,999,960,053 m — fifteen
gigametres to within 40 km, or three parts in a million. Round numbers like
that are not where a ship drifts to; they are where something is.

**Z is nothing.** −91.8 km against horizontal distances of eleven billion. The
system is a plane for every practical purpose, which is why the existing
position data stores X and Y only — an assumption that was already baked in and
now has a measurement behind it.

**The app cannot name it, and is not close to naming it.** The nearest body it
knows is Bloom, in Pyro, **3.996 Gm away**. Four million kilometres. Nothing is
within one gigametre of the reading, and 15 Gm sits in a gap in the catalogue:

| Stanton orbit | Gm from centre |
|---|---|
| Hurston and its moons | 12.79 – 12.91 |
| **the reading** | **15.00** |
| Crusader and its moons | 19.11 – 19.20 |
| ArcCorp and its moons | 28.67 – 28.97 |
| microTech and its moons | 43.44 – 43.52 |

Between Hurston's orbit and Crusader's, which in Stanton is where the Aaron
Halo is — but that is a guess and is written here as one. What is not a guess is
that the app has nothing to say about the spot.

---

## The channel, and why it is not OCR

The game will simply tell you. `/showlocation` in chat copies the player's
global coordinates to the clipboard as text — exact, free, and nothing to
recognise. [Star Citizen Navigation](https://github.com/Valalol/Star-Citizen-Navigation)
is built entirely on this and has been for years.

That is the whole acquisition story. No pixels, no engine, no preprocessing, no
confidence score on the number itself.

---

## The line this feature does not cross

**The app never sends the command.** Typing `/showlocation` into the game's
chat is synthesising input, and the ToS names that directly:

> "Use or distribute 'auto' software programs, 'macro' software programs or
> other 'cheat utility' software program or applications."

Of everything in this area that is the one thing explicitly prohibited rather
than merely unaddressed, and it is also what anti-cheat exists to notice. So:

- The **pilot** runs `/showlocation`. It is their keypress, their command.
- The **app** notices coordinates arrive on the clipboard and offers to mark
  them.

Which means the hotkey is not needed for the ordinary case at all. It becomes
"mark where I am" for when the coordinates are already copied — a fifth
registration beside the four the overlay already has (`Ctrl+Alt+O`, the arrows,
`F`).

---

## Reading the clipboard is not free either

Watching the clipboard means seeing everything the pilot copies. Passwords,
messages, everything. That is a real cost and it is paid whether or not anyone
notices.

The only version worth building:

- **Off until switched on**, in Settings, with the sentence above next to the
  switch rather than buried in a doc.
- **Matches the coordinate shape and discards everything else** in the same
  breath — not stored, not logged, not held in a field, not sent anywhere.
- **Says what it is doing while it does it.** A status the pilot can see beats
  a promise they have to take on trust.

If that feels heavy for a convenience, it is proportionate: a tool quietly
reading somebody's clipboard is a thing people are right to be angry about, and
being the app that does it carefully is worth more than the two seconds saved.

---

## A coordinate is not a POI

The reading says where. It does not say which system, because `/showlocation`
is relative to whichever system the pilot is in — the same numbers mean
different places in Stanton and Pyro.

**The logs already know the system.** That join is the entire reason this
belongs in Quantum Wake rather than in a standalone tool, and it is also the
feature's honest limit: a POI is only as certain as the app's idea of where the
pilot was, which is inferred and already carries a confidence.

So a saved point is three things from three sources, and the card should say so:

| Part | Where it comes from | How certain |
|---|---|---|
| Coordinates | the game, via the clipboard | exact |
| System | the app's own location inference | carries a confidence |
| Name | the pilot | theirs |

---

## Shape

```
Poi
  Id, Name, Note
  System            from the app's location at the time
  X, Y, Z           as read, unrounded
  Confidence        the location confidence when it was marked
  CreatedAt, ModifiedAt
```

Authored, so: its own store beside the jobs and the kits, never touched by a
rescan, and **in the backup from the first commit** — a store added later and
wired into the backup afterwards is a backup quietly no longer complete, which
is a mistake this project has already made once and written down.

`Z` is stored even though the map will not use it. Throwing away a measured
number because today's view is two-dimensional is the kind of decision that is
annoying to reverse.

---

## Where it surfaces

**The map**, as a marker at a real position rather than a node in a name-based
layout. This is the one that makes the feature feel like something.

**Distance to the nearest POI**, which is the first question a coordinate can
answer that a name never could — and it works while flying, on the Now page and
in the overlay.

**A flight plan stop.** A run already has stops with `PlaceId` and `Place`; a
POI is a stop the game has no name for. That wants care rather than
enthusiasm — `TripStore.Arrived` ticks stops by matching place names, and a
coordinate does not have one.

---

## Open questions

1. **Does `/showlocation` say anything but the coordinates?** The reading in
   hand is three numbers. If the game also names the system, the join above gets
   simpler and more certain. One more reading answers it.
2. **How near is "at" a POI?** A metre is absurd, a gigametre is useless, and
   the right answer probably differs between a cave mouth and a belt. Worth
   deciding from real readings rather than from taste.
3. **Do POIs travel?** They are authored, so they belong in the backup. Whether
   they belong in the *share* export — pooled with a wing, the way the org
   network plan imagined — is a separate and larger question.
4. **What happens when the system is unknown?** The app's location confidence
   is sometimes none. Refusing to save is hostile; saving without a system is a
   point that means nothing later. Probably: save it, mark it, and let the pilot
   name the system.

---

## Build order

1. **Read the clipboard and print what was found.** No store, no UI, no watch —
   a CLI command that parses one reading and says what it made of it. Settles
   question 1 and proves the pattern against a real string rather than an
   invented one.
2. **`PoiStore`**, authored, in the backup, with tests — the pattern is well
   worn by now.
3. **The watch and the prompt**: off by default, pattern-matched, discarding
   everything else, with the overlay toast that already exists.
4. **The map marker**, and distance to nearest.
5. **Everything else**, once there are enough real points to know what is
   actually wanted.

Step 1 is small and answers the only question that could change the design.
