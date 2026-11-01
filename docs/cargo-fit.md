# Cargo fit: does 4 × 32 + 2 × 16 fit in the Hermes?

Started 2026-09-16, the first item on the list in `IDEAS.md`. A panel on the
Garage page that takes a load of crates and says whether the hull's grids
take it, drawing where each crate goes, and which of your ships would. Run
`dotnet run --project src\Quantumwake.Cli -c Release -- --cargo` after a
patch or a dataset refresh; the tables below are what to compare against.

## Where the numbers are, and where they were not

`docs/datacore.md` had this two-thirds solved and honestly stuck: the
install's `InventoryContainer.<ship>_CargoGrid_*` records carry each grid's
inside in metres, and nothing readable says how many of each a hull places -
the Spirit C1 places its one grid record twice, and the count sits in the
vehicle definitions that are stored encrypted.

**Since 0.20.20 the install answers first.** That count was in the DataCore
after all, in each hull's default loadout - one port per grid - and the
grids are now read from there (`GameCargoGrids`): 145 of the 149 hulls
the dump credits with cargo agree with it to the SCU, so Cargo fit works
without the download. The dump below is the fallback for the four the
install does not place, and the page says which source drew the hold. See
the correction at the end of the cargo section in `datacore.md`.

**The community dump has the count.** `ships.json` (the 41 MB file the
Garage already downloads and digests) carries a `CargoGrids` block per ship
with one entry per grid the hull places, each with the grid's class, its
metres, the SCU the dump credits it with, and the smallest and largest box
it accepts per axis. On 2026-09-16, 149 of the 318 hulls have grids, and on
every one of them the sum over the block equals the dump's own `Cargo`
figure - the check the DataCore route could never pass (the C1 reads 64,
the Freelancer 66, the Hermes 288, the Ironclad 2,200). The digest keeps
the block as `ShipBase.CargoGrids` (`digest-ship-stats.json`); a digest
written before 0.14.9 has the ships and no grids, and the panel says
"refresh the reference data" rather than drawing nothing.

**The crates are the install's.** Every commodity ships in a family of
crates - `Carryable_TBO_FL_32SCU_Commodity_Organic_Oza` - and each record's
`SAttachableComponentParams.AttachDef.inventoryOccupancyDimensions` is the
box in metres (`GameCrates.Read`, cached with the rest of the game data):

| Crate | Box, metres | Cells |
| --- | --- | --- |
| 1 SCU | 1.25 × 1.25 × 1.25 | 1 × 1 × 1 |
| 2 SCU | 1.25 × 2.5 × 1.25 | 1 × 2 × 1 |
| 4 SCU | 2.5 × 2.5 × 1.25 | 2 × 2 × 1 |
| 8 SCU | 2.5 × 2.5 × 2.5 | 2 × 2 × 2 |
| 16 SCU | 2.5 × 5 × 2.5 | 2 × 4 × 2 |
| 24 SCU | 2.5 × 7.5 × 2.5 | 2 × 6 × 2 |
| 32 SCU | 2.5 × 10 × 2.5 | 2 × 8 × 2 |

So the 16, 24 and 32 are long boxes one lane wide, not squares, which is
what decides whether a grid takes them. The `SCU_Cargo_Template_*` records
read 0.15 m a side and are placeholders. `CargoFit.StandardCrates` is this
table, for an app whose install is not read yet and for the tests; the
panel says which it used.

**A crate keeps its top up.** `CargoGridOccupantProperties` on every crate
record allows only the Top face upward and `StackAll` on every face: a crate
turns on the spot and never stands on its side, and anything may stack on
anything. The first packer allowed six orientations and stood a 4 SCU crate
on end in the Freelancer's last cell; the file says no, and the test now
says so both ways.

**Every grid sits on the lattice.** All 149 hulls' grids divide by 1.25 m
exactly (`--cargo` counts the ones that do not: 0).

**The largest-box rule is real, and sometimes unfilled.** The Corsair's
grid is 5 × 11.25 × 2.5 m and takes a box no bigger than 5 × 7.5 × 2.5, so
a 24 goes in and a 32 does not, though a 32 would fit by geometry; the
Cutlass Black's main grid takes nothing bigger than a 2 SCU crate, its rear
nothing bigger than a 1. But the Ironclad's 720 SCU hold, the Hermes' 144
and the Golem OX's 64 all read a largest box of one cell, which is a
placeholder. The line is 16 SCU: a one-cell rule on a grid of 16 or more is
treated as unstated, the grid is packed by its geometry, and the row says
so. Below that the rule is believed. Of 528 grid placements, 93 read one
cell; 13 of those are on holds of 16 SCU or more.

## The packing, and whose it is

`CargoFit.Pack` in Core, tested: first-fit-decreasing on the cell lattice.
Crates largest first, into the grid with the most room first; each tried
both ways round (the way lying along the grid's long axis first), at the
lowest, then nearest, then leftmost free corner; it must stand on the floor
or on crates under every cell of its footprint. So:

- **Fits** is a packing found, drawn crate by crate, and real.
- **Does not fit** for a rule - a crate bigger than any box the grids
  accept, a size the install does not describe - is stated as the rule.
- **No packing found** for a load within the volume is this packer's
  failure and not proof; the panel says so. On these shapes - every crate
  one or two cells wide, stacking in twos - first-fit finds the packing
  whenever the crates tile the grid, which is the case a hauler asks about.

The question that started it: four 32s lie two lanes of two along the
Hermes' first hold (4 × 18 × 2 cells), a 16 lies across the 2 cells of
length that leaves, and the second 16 goes in the other hold - 160 of 288
SCU, 128 to spare. The Freelancer takes one 32 and not two: its rear is one
lane wide and three cells high, and two 32s would stack four high.

## The panel

On the Garage page, under the sheet: seven counts to type (kept in the
browser, so the same load can be tried on the next hull), *Fit it*, the
verdict, each grid drawn as layers seen from above with crates coloured by
size and the size written on them, and the fleet answer - your ships that
take it, the rest folded into one line with their hold sizes, then the
smallest hulls in the reference that take it, each a link that opens that
hull with the same load. `GET /api/cargo/{class}` is the hull's grids and
the crates; `POST /api/cargo/fit` is the answer.

## Not done

- Mass. The crates carry it (a full 32 of Oza is 30 t) and the hull's
  `MassLoadout` is on the sheet; whether the game limits a hold by mass is
  not read, so nothing is said.
- Mixed commodities in one crate, and the crate a shop actually hands over
  for a quantity - the panel takes crates, not SCU.
- The Hull series' external spindle grids are packed like any other; that
  they take only the one size they are drawn with (`MinSize` = `MaxSize`
  on the Hull C) is honoured by the rule, and nothing more is known about
  how they load.
