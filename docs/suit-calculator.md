# Suit calculator — what a suit weighs, carries and lets you take, from the files

Built as 0.21.3 and 0.21.4 (2026-11-03), an Armoury tab. Read from 4.10.2
LIVE (build 12913470) and checked against the 4.10 PTU of 2026-11-03
(4.10.199.51990), which agree on every figure below. The readers are
`Quantumwake.Core/GameData/GameBody.cs` (the player) and the armour and weapon
records `GameArmoury.cs` already carried; the page is the `suit calculator`
section of `web/app.js`.

---

## Where it starts

From the loadout the log last saw (`/api/loadout`): armour by slot, guns by
holster, and everything else the Armoury knows the weight of as carried items.
A loadout item carries the uuid of the finish worn, and a finish points at its
family's figures - colours do not change mass. What the Armoury does not know
is listed with no mass rather than dropped, and the page says which fields it
counted as 0.

Every slot can be swapped - helmet, core, arms, legs, undersuit, backpack, and
each gun - stripped, or copied back to the loadout. The suit is kept in the
browser only.

## The figures, and whose they are

**Mass**, in kilograms, is each piece's own physics component, the same field
the Armoury tables show. The total is the sum over what is worn, holstered and
carried, with carried items counted by their number.

**Carrying capacity** is each piece's `CapacityMicroScu`, in millionths of an
SCU - the figure the game prints as "8K µSCU" on a heavy core.

**Speed** comes from `ActorStanceSpeedsInfo.Stand`, whose filter matches any
armour: no armour class has a stance of its own, and every armour piece's
`wearMovementMultipliers` is empty, so nothing worn changes it in these builds.
Run is 5.58 m/s and sprint 7.2 in 4.10.2. The page says this plainly rather
than inventing a penalty.

**G-force** is where the adding up happens. Every clothing item carries a
`Flight.gForceResistance`, and the pieces sum to round figures:

| piece | resistance |
|---|---|
| heavy helmet / core / arms / legs | -0.125 / -0.5 / -0.125 / -0.25, exactly -1 together |
| a medium set | -0.5 |
| a light set | -0.25 |
| an undersuit | +0.9 |
| a flight suit | up to +1 |

That is what the 4.8 patch notes describe, and it checks out against the
guides: a Tailwind flight suit reads +0.975 and its helmet -0.025, and guides
measure the pair at 95%. The page shows the figure as a percentage, the way
the community wiki writes it.

The sum is set against the player's own G-force profiles,
`ActorGForceComponent.PlayerGForce`: five of them, each with a worst, usual and
best tier (tolerance, maximum G, safe stress, time to max out, time to
recover). The files hold them in a plain array and do not say which direction
each is for. The numbers suggest it - one takes 10 G before it strains and 18
at most, which is how bodies take G pushed into the chest, and one only 4 and
6, which is how they take G pulled toward the head - but that is an inference,
and the page numbers them instead of naming them.

## The one estimate

How the game blends between the worst, usual and best tiers as the suit's
resistance moves from -1 to +1 is not in the files. The "this suit" column
draws the straight line through the three and says it is an estimate. The
three tiers themselves are the game's.

## Against the 4.10 PTU

The PTU of 2026-11-03 adds no field for weight or encumbrance to any struct,
no movement multiplier to any armour piece, and changes none of the
resistances above. New armour sets arrive (two heavy utility sets, a light
combat set, a flight suit) with the same fields. See the 2026-11-04 survey
note for the full diff.

## What is tested

`Quantumwake.WebTests/SuitCalculatorTests` drives the tab headless: the
loadout becoming the starting suit, G resistance as the sum of the pieces, the
Tailwind suit and helmet reading as the guides say, a tier between the ends
being the straight line, empty fields counting as 0 and saying so, items the
Armoury does not list being weighed from the install, Strip all, and a swap
moving the figures. `GameBody.Read` itself has no fixture test; its figures
were checked against the install by hand, as above.
