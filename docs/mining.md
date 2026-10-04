# Mining: can it be cracked?

The 0.14 line. What the install says about mining, and a calculator on the
Mining page that runs the community's rule on it. Everything here is read
from this install (Alpha 4.10, `Game2.dcb`) on 2026-09-15 with
`dotnet run --project src\Quantumwake.Cli -c Release -- --mining`; run that
again after a patch and the tables below are what to compare against.

## What prompted it

scminer.rocks has a "Can I Crack It?" calculator: rock mass, resistance,
instability and mineral in; laser, modules and gadget in; a verdict out. The
question was whether this install holds enough to do the same. It holds all
of it and a little more. None of it was being read: the Garage's community
part digest carries a mining laser's mass and health and nothing it does to a
rock, and the logs record no mining at all - no scan, no fracture, no
extraction; the one exception, a refinery order finishing, turned up on
2026-10-04 and is under *Refinery orders* below - which is why the Mining page's "mine" figure is ore sold that
was never bought, an inference, and stays so - kept to the minerals the
deposit tables name since 0.14.5, because a mission reward or a found
trinket leaves the hold the same way (a Year of the Rat Envelope sold for
60,000 aUEC was listed as a SCU of ore on this install).

## What the files hold

Structs that name themselves, all under `libs/foundry/records/mining/`:

- **`MiningGlobalParams`** - the constants of the rock model:
  `powerCapacityPerMass 10`, `decayPerMass 0.2`, `optimalWindowSize 0.1`
  (`optimalWindowMaxSize 0.5`, `optimalWindowFactor 0.75`),
  `resistanceCurveFactor 0.6`, `optimalWindowThinnessCurveFactor 0.7`,
  `controlledBreakingFillRate 0.5`/s (`dangerBreakingFillRate 0.3`),
  `absorbableVolumeThreshold 330`, `cSCUPerVolume 3`. A ground-vehicle set
  beside it with `powerCapacityPerMass 4`.
- **`MineableElement`** × 42 (31 ship, the rest hand and ROC): per mineral
  `elementResistance` (-1 to 1), `elementInstability`, the optimal window's
  midpoint, randomness and thinness, an explosion multiplier and a cluster
  factor. Quantainium: resistance 0.95, instability 1000, blast ×260. Iron:
  -0.40, 50, ×20. The `resourceType` reference names it through the
  commodity table the app already reads.
- **`MineableComposition`** × 213 presets (templates and tests dropped): what
  a deposit is made of, element by element with min/max share and
  probability. `Asteroid_PType_Copper` is copper 30-70% always, laranite
  30-60% at 0.8, gold 20-50% at 0.1, quantainium 20-50% at 0.02. The HUD name
  (`@hud_mining_asteroid_name_1` → "Asteroid (P-Type)") is shared by a dozen
  presets, so the class is the key.
- **Lasers** × 18 (`EntityClassDefinition.Mining_Laser_*`, templates and
  tests dropped): the fracture beam's `damagePerSecond.DamageEnergy` is the
  power, the second fire action is the extraction beam, and
  `SEntityComponentMiningLaserParams` carries the head's own modifiers and
  filter. Names from the item catalogue.
- **Modules** × 29 (`Mining_Modules_Passive_*`, `_Active_*`): two weapon
  modifiers - the first on the fracture beam, the second (`fireActionIndex=1`)
  on extraction - a mining modifier, a filter modifier; actives carry
  `charges` and a `modifierLifetime`.
- **Gadgets** × 6 (`Mining_Gadget_*`): one rock modifier each.

The nested structs are laid inline on some records and behind a pointer on
others under the same field name (`damagePerSecond`, `miningLaserModifiers`,
`weaponModifier`), and the composition parts are a class array rather than a
pointer array; `GameMining.Nested` reads either shape, which is what the
first dump - 0 lasers, empty modifiers, 0 compositions - taught.

## Checked against scminer.rocks

Their calculator reads the same files. Every wattage they print is the
fracture beam's `DamageEnergy` on this install - Arbor MH1 2,340, Helix I
3,900, Hofstede-S1 2,600, Impact I 2,600, Klein-S1 3,120, Lancet MH1 3,120,
Pitman 3,900 - and every module effect they list is the file's: Rieger-C3
×1.25 power, Surge ×1.5, Brandt ×1.35, Forel +15.5% resistance, Sabir -50%
resistance / +50% window / +15% instability, BoreMax +10% resistance / -70%
instability. Where a figure of theirs and a figure of ours disagree, the
files are the referee, and so far they have not.

## The rule, and whose it is

The game does not publish how mass, resistance and power meet. What the
community settled on, read out of scminer's calculator bundle on
2026-09-15: **a rock needs 0.36 W per kilogram at zero resistance**; the
HUD's resistance is scaled by the fit's resistance points
(`hud × (1 + Σ points / 100)`) and taken off the delivered power; delivered
over required at 115% or more breaks solo, 70% or more breaks with a gadget,
less needs more heads. Their green-zone width (18%) and crack time (14 s)
are theirs too and are **not** used: the game's own `optimalWindowSize`
gives the window, and the crack time is the time to reach the window, which
is the pilot's throttle hand.

`RockCracking.RequiredWattsPerKg`, `SoloRatio` and `GadgetRatio` are the
whole of the borrowed rule, named so a measured one replaces them in one
place. Beside the verdict the page quotes what the game's constants say about
the same rock - it holds `mass × 10` energy and sheds `mass × 0.2` a second -
which is a different model of the same event and, until measured against,
neither confirms nor contradicts the line. The page calls the verdict an
estimate and says whose rule it is.

**What would settle it:** a handful of scan HUD screenshots (mass,
resistance, instability, composition are all printed) paired with what
happened - broke at what throttle, or would not charge. The screen reader
already reads that HUD's family; a `Mining` frame kind reading those four
figures is the natural next step, and it would feed this calculator without
a form.

## Module slots

The head's own item ports (`SItemPortContainerComponentParams.Ports`, a
class array) name the slots: `BONE_ItemPort_Consumable_1`, `_2`, `_3`
take a `MiningModifier`; the `VEN` port beside them takes a weapon
attachment and is not one. On this install: Arbor MH1, Hofstede-S1, Lancet
MH1 and Klein-S2 one; Helix I, Impact I, Pitman, Arbor MH2, Hofstede-S2 and
Lancet MH2 two; Helix II and Impact II three; Klein-S1 none. The page shows
that many selects per head and says "no module slots" for the Klein-S1.

## The deposit on the page

Picking a deposit shows the preset's mix - each mineral's share when it is
present and its chance of being present - with UEX's best sell for the
refined mineral a SCU beside it, the way the places table values rock (the
element's "(Raw)"/"Ore" stripped to reach the UEX name). The likeliest
mineral goes into the notes. A rough worth of a SCU of the mix is given -
middle share × chance × refined price, summed - and called rough: the game
normalises the shares of the minerals that turn up and this does not.

**Per rock is not given, and says so.** The HUD's mass is kilograms; the
game's `cSCUPerVolume 3` is cargo per unit of *volume*, and nothing read
carries a density to get from one to the other. scminer's "Is It Worth
Mining?" takes the extracted cSCU as input for the same reason. The
extraction beam's power is read and unused until that gap is closed.

## Prices and the refinery, joined

Every head, module and gadget on the page carries UEX's cheapest terminal
and where, the way the Garage's bench prices a part: the game's own id for
the class (`GameCommodities.ItemUuid`) into the UEX item feed. On this
install a Helix I is 55,100 aUEC at Tammany and Sons, a Surge 1,400, a
Focus 3,800, a Sabir 13,125; a module the feed has no terminal for shows a
blank, which is "no terminal recorded" and not free. The deposit mix
carries the raw-ore price and the best refinery *station bonus* from the two
optional UEX feeds the deposit table already joins - the feeds list ore under
the element's own name, "Copper (Ore)", "Laranite (Raw)" - and a SCU of the
mix is valued both ways, at refined prices before the refinery's yield, and
raw, each called rough.

**The UEX "yield" is a bonus, not a yield.** `refineries_yields.value` is
+9 for copper at MIC-L5 and -5 for iron at Nyx Gateway: a station's
percentage points on the method's own yield. The Mining page's deposit table
had shown it as "Yield 9%" since the feed arrived, which reads as nine
percent of the ore coming back; 0.14.2 shows it signed as *Bonus* in both
tables and never multiplies it in as a yield.

**Refining itself is not in the files.** `RefiningProcess` has nine records
and each is two enums and a name - Slow/Normal/Fast × Careful/Normal/
Wasteful - with no yield, cost or time on any of them; those are the
server's. The community tables (and UEX's yields feed) were the only source
until 0.16.19, when the refinery terminal's own screen started being read -
see *Refinery orders* below: it prints the yield, the cost and the time for
the ore in front of it.

**The methods, since 0.14.10.** UEX's `refineries_methods` is nine records
with the game's own three-point ratings - `rating_yield`, `rating_cost`,
`rating_speed`, 1 to 3 - and no percentage, cost or time behind them; that
is what the in-game pips show and all that anyone outside the server has.
It is one more optional feed (~2 KB, *Refining methods* in Settings), a
table on the *Your runs* pane, and the method named on a run waiting at a
refinery is matched to it and its pips quoted. The run also carries a
**ceiling**: the SCU that went in at UEX's best refined sell, and the
station's bonus on top where the yields feed reports one for that ore at
that refinery (40 SCU of copper at 4,200 is 168,000; +9 % at MIC-L5 makes
183,120) - before the method's own yield and the fee, which nobody
publishes, and the line says so. The station is matched by one name
containing the other and not at all otherwise: a bonus at the wrong station
is worse than none. "Dinyx or Cormack, at which station, for this ore" is
therefore answered as far as the data goes - the station from the yields
feed, the method from its pips - and no further.

**Three is the cheap end.** Until 0.16.19 the methods table said a cost of
3 was dearest. The terminal says otherwise, twice on one evening: it
describes Pyrometric Chromalysis - UEX's 3 / 3 / 1 - as "HIGH YIELD // LOW
COST // SLOWEST", and with no method picked shows "LOW YIELD // MODERATE
COST // VERY FAST", which is Cormack's 1 / 2 / 3. Every axis runs 1 worst to
3 best, cost included, and the table now sorts and says so.

## The scan panel, read

No frame from this install exists - the logs record no mining and the
folder holds no scan - so the reader was written from the one public frame
found: the Star Citizen Wiki's `Mining-4.7-scan-result.png`, a 227 × 310
crop of the panel in Alpha 4.7, run through the app's own engine on
2026-09-15. The panel prints, top to bottom: SCAN RESULTS; the primary
mineral; MASS: 6295; RESISTANCE: 0%; INSTABILITY: 1.75; a difficulty bar
(EASY); COMPOSITION with **21.07 SCU** on the row; then a row per mineral -
share, name, quality - with INERT MATERIALS last at quality 0. At that size
the engine read every label and lost the mass figure, misread two shares
(5.96% as "s.gs%", 40.47% as "4147%") and one name ("ÄLUMNUX"); a screenshot
at the game's resolution is four times the height. `ScreenFrame.Mining.cs`
files the frame as `ScreenKind.Mining` on the title plus both figure labels
(the fracture HUD prints resistance and instability too, without the title),
lands each figure only where it read, and names a mineral only when one
commodity is within two letters. The fixture in `ScreenMiningTests` is the
engine's output verbatim, misreadings kept.

Two things the panel settles that nothing else did. **The SCU figure** is the
game's own conversion of this rock to cargo - 6,295 kg to 21.07 SCU here -
which the calculator could not compute; the form says "by the game's own
count" when it has one. And **instability is printed as a figure**, 1.75,
not a percentage; the community rule takes a percentage, and nothing measured
says the two are one scale, so a scan fills mass, resistance and the mineral
and leaves instability as typed, and says so.

The Log tab lists a scan with its figures and offers *Can it be cracked?*,
which opens the Mining page with the rock in the form; the page's *Use the
last scanned rock* does the same from the newest scan read.

**The first frames from this install, and what they changed (0.16.19).** On
2026-10-03 a Golem at Daymar took three scans (16:02:48, 16:02:49, 16:15:38,
3440 × 1440) and the reader filed every one as nothing. The live panel does
not say what the wiki's said: its title is RESULTS, its labels MASS:, RES:,
INST: and COMP., and each composition row is one line, share and name
together - "8.56% SILICON (RAW)". The reader now takes both layouts, finds
the labels in the title's column (the fracture HUD's RESISTANCE and
INSTABILITY sit on the same frame, in another), and finds the mass label by
position alone - the engine read it as "uss•.", "mss•.", "nss•.", "mgs:",
"HAss:" and "RAss•." and never as MASS.

The whole frame loses most of the panel's figures - orange on a glow - so
the panel is read again at x2, x2.5, x3 and x1.5 (`MiningSecondLook`) and a
figure counts when two sizes agree, the wallet's rule. On the silicon frame
that recovered the mass (4294, at three sizes), the instability (21.89, four;
the whole read made "21 .eø" of it), every share and both Heph qualities
(572 and 692; x3 read the first as 72 and was outvoted). The silicon rows'
qualities, 510 and 310, read at no size and are shown as unread. The HUD
names minerals "SILICON (RAW)" and "HEPH (RAW)" where the install's table
has "Raw Silicon" and "Raw Hephaestanite", so names are matched with the
raw/ore marker taken off both sides and a four-letter stem allowed to stand
for the one name it begins.

Two findings worth keeping. **Agreement is not proof**: on the aphorite
frame two sizes both read 23.74% as "3.74%", so the reading carries its
shares' total (79.99 there) and the Log says when it is not 100. **The
whole-frame read sits 33 px high**: RESULTS at y=512 whole and 545 in every
patch, and 542 in a plain crop of the file, the same 33 px on CARGO 423 px
lower - an offset, not a scale. The patch reaches past it and the second
look never mixes its lines with the whole read's. The wallet's "33 px above
the row" in `WalletPanel` is very likely this offset under another name.

A gem cluster prints its SCU in thousandths - "3.15m SCU" - and its mass as
0.12. **Mass to SCU is not one ratio**: the silicon rock is 4,294 to 16.74
(3.90 SCU a tonne), the wiki's aluminium 6,295 to 21.07 (3.35). Each scan
read is one more point; two do not make a density.

## Refinery orders

**The terminal.** Four frames at MIC-L5 on 2026-10-03, 23:08 to 23:10 - the
station profile, a work order set up before and after picking a method, and
the order running - are read by `ScreenFrame.Refinery.cs`. White on dark and
upright, the terminal reads almost whole: station, stage, method and its
ratings line, IN MANIFEST and TO REFINE, every lot's quality, quantity and
yield, the cost, the time, the station's load and the pilot's balance. It
missed one yield of three on the quote and the silicon's quality while
running; the station bonuses are small green type and never read, and UEX
carries them anyway.

**What the quote measured.** Pyrometric Chromalysis at MIC-L5, at 5,435 %
of capacity with a surcharge warning: 142 cSCU of silicon at quality 510 for
64 back, 33 of agricium at 588 for 15 - 45 % both - and 6 of aslarite for 2;
182 cSCU in for 121 aUEC and 6 m 35 s. The balance on the next frame was
867,069 against 867,190 - the quote's cost to the unit, a figure the reader
did not use. Each quote is one reading at one station's load on one evening;
the page lists them as that, not as the method's yield.

**The log.** One line, once in 234 logs:

```
<2026-10-04T03:16:54.738Z> [Notice] <SHUDEvent_OnNotification> Added notification
"A Refinery Work Order has been Completed at MIC-L5 Modern Icarus Station: " [42] to queue. ...
```

Placing an order writes nothing; collecting it writes nothing; this toast is
all. It is kept on the session as a `RefineryCompletion` (payload 17) and
reaches the timeline as *Refinery order complete*.

**Joined.** The running screen at 23:10:19 local read 6 m 26 s left, so due
23:16:45; the log says 23:16:54. Nine seconds - the terminal's clock is a
good timer and the log is the confirmation. `RefineryOrders` joins frames to
orders (a running frame belongs to the quote whose countdown it falls
inside) and each completion to the open order at its station whose due time
it lies nearest. A completion with no screenshot is listed on its own; a
quote with no running frame counts down "if you confirmed the quote".

**Telling the pilot.** The *Refinery orders* block on *Haul & refinery* has
four modes, kept per viewer: the game's toast, then the terminal's clock a
minute after it runs out if the game has not spoken (the default - the
minute is several times the nine seconds measured); the game's toast only;
the clock only; neither. The two kinds of ready never share words or colour:
green when the game said so, amber when only the clock has. A countdown that
ran out before the page was opened is listed as due and not announced,
because a reload must not replay history.

## The dump

`--mining` on this install, 2026-09-15:

```
18 lasers
  S0 Arbor MHV Mining Laser       power      1  extraction     0  slots 0  filter   0%  throttle min 0.20  inst -40%  [Mining_Laser_GRIN_Arbor_S0]
  S0 Lawson Mining Laser          power      1  extraction     0  slots 0  filter   0%  throttle min 0.20  res -40% inst +30% window +40%  [Mining_Laser_SHIN_Klein_S0]
  S0 Mining Laser SHIN Hofstede S0 power      1  extraction     0  slots 0  filter   0%  throttle min 0.20  res -40% inst +30% window +40% rate +20%  [Mining_Laser_SHIN_Hofstede_S0]
  S0 Mining Laser THCN Helix S0   power      1  extraction     0  slots 0  filter   0%  throttle min 0.15  window -40% rate +20%  [Mining_Laser_THCN_Helix_S0]
  S1 Arbor MH1 Mining Laser       power   1850  extraction  1000  slots 1  filter   0%  throttle min 0.00    [Mining_Laser_MPUV_Arm]
  S1 Arbor MH1 Mining Laser       power   2340  extraction  1850  slots 1  filter  30%  throttle min 0.05  res +25% inst -35% window +40%  [Mining_Laser_GRIN_Arbor_S1]
  S1 Helix I Mining Laser         power   3900  extraction  1850  slots 2  filter  30%  throttle min 0.20  res -30% window -40%  [Mining_Laser_THCN_Helix_S1]
  S1 Hofstede-S1 Mining Laser     power   2600  extraction  1295  slots 1  filter  30%  throttle min 0.05  res -30% inst +10% window +60% rate +20%  [Mining_Laser_SHIN_Hofstede_S1]
  S1 Impact I Mining Laser        power   2600  extraction  2775  slots 2  filter  30%  throttle min 0.20  res +10% inst -10% window +20% rate -40%  [Mining_Laser_THCN_Impact_S1]
  S1 Klein-S1 Mining Laser        power   3120  extraction  2220  slots 0  filter  30%  throttle min 0.15  res -45% inst +35% window +20%  [Mining_Laser_SHIN_Klein_S1]
  S1 Lancet MH1 Mining Laser      power   3120  extraction  1850  slots 1  filter  30%  throttle min 0.20  inst -10% window -60% rate +40%  [Mining_Laser_GRIN_Lancet_S1]
  S1 Pitman Mining Laser          power   3900  extraction  1295  slots 2  filter  40%  throttle min 0.20  res +25% inst +35% window +40% rate -40%  [Mining_Laser_DRAK_Golem_S1]
  S2 Arbor MH2 Mining Laser       power   2900  extraction  2590  slots 2  filter  40%  throttle min 0.05  res +25% inst -35% window +40%  [Mining_Laser_GRIN_Arbor_S2]
  S2 Helix II Mining Laser        power   4930  extraction  2590  slots 3  filter  40%  throttle min 0.30  res -30% window -40%  [Mining_Laser_THCN_Helix_S2]
  S2 Hofstede-S2 Mining Laser     power   4060  extraction  1295  slots 2  filter  40%  throttle min 0.10  res -30% inst +10% window +60% rate +20%  [Mining_Laser_SHIN_Hofstede_S2]
  S2 Impact II Mining Laser       power   4060  extraction  3145  slots 3  filter  40%  throttle min 0.30  res +10% inst -10% window +20% rate -40%  [Mining_Laser_THCN_Impact_S2]
  S2 Klein-S2 Mining Laser        power   4350  extraction  2775  slots 1  filter  40%  throttle min 0.20  res -45% inst +35% window +20%  [Mining_Laser_SHIN_Klein_S2]
  S2 Lancet MH2 Mining Laser      power   4350  extraction  2590  slots 2  filter  40%  throttle min 0.30  inst -10% window -60% rate +40%  [Mining_Laser_GRIN_Lancet_S2]

29 modules
  passive Deluge Module          power ×1.15  extraction ×0.85  filter    0%  res -15.5%  [Mining_Modules_Passive_Deluge]
  passive FLTR Module            power ×1.00  extraction ×0.85  filter   20%    [Mining_Modules_Passive_FLTR_MK1]
  passive FLTR-L Module          power ×1.00  extraction ×0.90  filter   23%    [Mining_Modules_Passive_FLTR_MK2]
  passive FLTR-XL Module         power ×1.00  extraction ×0.95  filter   24%    [Mining_Modules_Passive_FLTR_MK3]
  passive Focus II Module        power ×0.90  extraction ×1.00  filter    0%  window +37%  [Mining_Modules_Passive_Focus_MK2]
  passive Focus III Module       power ×0.95  extraction ×1.00  filter    0%  window +40%  [Mining_Modules_Passive_Focus_MK3]
  passive Focus Module           power ×0.85  extraction ×1.00  filter    0%  window +30%  [Mining_Modules_Passive_Focus_MK1]
  passive Overrun Module         power ×1.02  extraction ×0.85  filter    0%  res -24.8%  [Mining_Modules_Passive_Overrun]
  passive Rieger Module          power ×1.15  extraction ×1.00  filter    0%  window -10%  [Mining_Modules_Passive_Rieger_MK1]
  passive Rieger-C2 Module       power ×1.20  extraction ×1.00  filter    0%  window -3%  [Mining_Modules_Passive_Rieger_MK2]
  passive Rieger-C3 Module       power ×1.25  extraction ×1.00  filter    0%  window -1%  [Mining_Modules_Passive_Rieger_MK3]
  passive Torrent II Module      power ×1.00  extraction ×1.00  filter    0%  window -3% rate +35%  [Mining_Modules_Passive_Torrent_MK2]
  passive Torrent III Module     power ×1.00  extraction ×1.00  filter    0%  window -1% rate +45%  [Mining_Modules_Passive_Torrent_MK3]
  passive Torrent Module         power ×1.00  extraction ×1.00  filter    0%  window -10% rate +30%  [Mining_Modules_Passive_Torrent_MK1]
  passive Vaux Module            power ×1.00  extraction ×1.15  filter    0%  rate -20%  [Mining_Modules_Passive_Vaux_MK1]
  passive Vaux-C2 Module         power ×1.00  extraction ×1.20  filter    0%  rate -15%  [Mining_Modules_Passive_Vaux_MK2]
  passive Vaux-C3 Module         power ×1.00  extraction ×1.25  filter    0%  rate -5%  [Mining_Modules_Passive_Vaux_MK3]
  passive XTR Module             power ×1.00  extraction ×0.85  filter    5%  window +15%  [Mining_Modules_Passive_XTR_MK1]
  passive XTR-L Module           power ×1.00  extraction ×0.90  filter  5.8%  window +22%  [Mining_Modules_Passive_XTR_MK2]
  passive XTR-XL Module          power ×1.00  extraction ×0.95  filter    6%  window +25%  [Mining_Modules_Passive_XTR_MK3]
  active  Brandt Module          power ×1.35  extraction ×1.00  filter    0%  60s × 5  res +15.5% shatter -30%  [Mining_Modules_Active_Brandt]
  active  Clearcut Module        power ×1.15  extraction ×1.00  filter    0%  30s × 6  window +30%  [Mining_Modules_Active_Clearcut]
  active  Forel Module           power ×1.00  extraction ×1.50  filter    0%  60s × 6  res +15.5% overcharge -60%  [Mining_Modules_Active_Forel]
  active  Lifeline Module        power ×1.00  extraction ×1.00  filter    0%  15s × 3  res -15.5% inst -20% overcharge +60%  [Mining_Modules_Active_Lifeline]
  active  Optimum Module         power ×0.85  extraction ×1.00  filter    0%  60s × 5  inst -10% overcharge -80%  [Mining_Modules_Active_Optimum]
  active  Rime Module            power ×0.85  extraction ×1.00  filter    0%  20s × 10  res -24.8% shatter -10%  [Mining_Modules_Active_Rime]
  active  Stampede Module        power ×1.35  extraction ×0.85  filter    0%  30s × 6  inst -10% shatter -10%  [Mining_Modules_Active_Stampede]
  active  Surge Module           power ×1.50  extraction ×1.00  filter    0%  15s × 7  res -15.5% inst +10%  [Mining_Modules_Active_Surge]
  active  Torpid Module          power ×1.00  extraction ×1.00  filter    0%  60s × 5  rate +60% overcharge -60% shatter +40%  [Mining_Modules_Active_Torpid]

6 gadgets
  BoreMax      res +10% inst -70% cluster +30%  [Mining_Gadget_THCN_BoreMax]
  Okunis       window +50% rate +100% cluster -20%  [Mining_Gadget_SHIN_Okunis]
  OptiMax      res -25% window -30% cluster +60%  [Mining_Gadget_GRIN_OptiMax]
  Sabir        res -50% inst +15% window +50%  [Mining_Gadget_SHIN_Sabir]
  Stalwart     inst -35% window -30% rate +50% cluster +30%  [Mining_Gadget_THCN_Stalwart]
  Waveshift    inst -35% window +100% rate -30%  [Mining_Gadget_GRIN_WaveShift]
```
