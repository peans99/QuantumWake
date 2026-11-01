# Interface rules

The dashboard's house rules for how a page is laid out, so a new page reads like
the others. They came out of a consistency pass in 0.20.24, which found three header
patterns, nine kicker styles and four link styles across 43 pages.
`PageHeadTests` holds the first two rules.

## A page header

Every page in a menu group opens with the same `.section-bar`:

```html
<div class="section-bar">
  <div class="page-title">
    <span class="page-kicker">Hangar</span>   <!-- the menu group -->
    <h2>Fleet</h2>                          <!-- the menu's own word -->
  </div>
  <div class="controls">…</div>
</div>
```

- **The kicker is the menu group,** and nothing else. The old kickers each said
  something different (CONTROL ROOM, Operational roster, Flight ledger), so the
  label told you nothing about where you were.
- **The title is the page's menu name.** The Loadout page used to be titled
  Armoury, the same as the Armoury page, and Sessions was titled Session
  debriefs. Mining is the one exception: its title is the workspace in use.
- **The header comes first.** Tiles, history and filters go below it. Fleet used
  to open on its tiles.

## Explanations fold

How a page works goes in `<details class="page-note">` with **About this page**
(or **About this table**) as the summary, directly under the header. The first
screen is then the page itself rather than six lines of prose.

The exception is a caveat the numbers depend on. Crew and Events lead with
"every number is a floor", and Stash with "pledged items will not be here".
Those stay visible, because the house rule is that a floor is called a floor
where the number is read.

## Cyan means clickable

- A link is a `button` with `link`, `place-link`, `ref-link` or
  `commodity-open`. They share one style: the surrounding font, cyan, and a
  dotted underline that goes solid on hover.
- A table's first column is plain text. It used to be cyan mono in every table,
  which made dates and names look clickable.
- Row actions (`.ghost` buttons inside `tbody td`) are quiet words that border
  on hover. They stay on screen: a control that appears only on hover is one
  nobody finds.

## Symbol buttons

A button whose label is a lone symbol (×, ↑, ↓, +, ★) carries the class
`icon` (0.20.27). It is drawn as the bare symbol, 24 px square, and outlined
only on hover. A delete warms to red. The framed button style, with its cut
corner, inset edge and corner tick, drew an empty box at that size with the
symbol lost in it: the Now page's server star once spilled its word out under
such a box. Give an icon button a `title`, which is its only words.

## One detail view for each kind of thing

- **Items and ships** open the Reference window (`openRefDetail`), from any
  page, including search. Inside it, every part and hull opens in turn, and
  Back walks the trail.
- **Places and commodities** open the drawer, which sits beside the map.

`openEntity('part' | 'ship', …)` sends items and ships to the window, so the
two can never describe one gun two ways again.

**Every item name is `refLink('item', class, label)`** (0.20.32), and it is
never plain text. Before this a gun was a link in the Items table and in a
checklist, words in the Loadout, the Stash, the Ledger and the Logbook, a link
to the Loot page's own panel in a farm list, and a row toggle in the Armoury.
Where a row carries only a display name (the Ledger, Spending, the Loadout),
pass the name: the server resolves a name as readily as a class. Where the
row has both, pass the class and show the name. The server sends the class
beside the words where the words alone could not say which item they meant:
`LedgerEntry.Item`, `LogbookLine.Subject`, `TimelineEntry.Subject`.
`ItemLinksTests` holds one test per page.

Two kinds of name are not linked, on purpose. A Wikelo requirement or reward
can be an item, a commodity or a ship, and the row does not say which, so a
link would open the wrong window one time in three. A name read off a
screenshot (Fleet fittings) has no class and may not match any item.

A name inside a table row that itself toggles (the Armoury) stops its click
from reaching the row: `armouryItemLink`. A name inside a `<button>` (the
Garage rig's fitted part) is not linked, since a button cannot hold one; the
Bench's candidate list beside it links every part.

## Identity

Trialled on Sessions, Fleet and Market in 0.20.25, on every page since
0.20.30. It applies to each `.view` whose first child is its `.section-bar`,
and not in the overlay, where a header is a small label over a glance.
`IdentityTests` holds it.

- **Whose number it is.** Gold (`--own`) is the pilot's own record: logs and
  screenshots. It is not `--amber`, which warns that a price is stale. Cyan
  (`--src-files`) is the game's files, green (`--src-community`) the community
  dataset, and violet (`--src-uex`) UEX. The colours are on the source badges
  everywhere, in the shared report, and as an underline on a column heading
  carrying `src-own`, `src-files`, `src-community` or `src-uex`. Mark the
  headings on a table that mixes sources (Market, Loot); a page that is all one
  source says so with its badge instead. A value is gold only when there is
  one: an empty cell stays plain.
- **Own pages say so.** `data-voice="own"` on the section turns the kicker
  gold. It is on the pages that are the pilot's own history: Logbook,
  Sessions, Servers, Places, Points, Crew, Casualties, Spending, Ledger,
  Commodities, Fleet, Hangar, Loadout, Stash and Reputation. Not on reference
  pages, nor on planning pages (the Jobs command deck, Itinerary, Checklists),
  nor on pages that set the game's figures beside yours (Events, Market,
  Loot), where the column headings carry the distinction.
- **The wake.** `drawWakes()` draws the last 60 days of `allSessions` under
  every page header. It is smoothed over three days, as wide as the time spent
  in game, and fades to the left. The mark at today's end fills in once the
  pilot has flown today, and its hover text has the numbers. The header has
  none of its own: there it surfaced over every control in the header. The
  wake goes under the header's contents by `z-index: -1` inside an isolated
  header rather than by lifting the contents, which would override how a
  header's own controls are positioned, and without `overflow: hidden`, which
  would clip a menu opening out of a header.
- **Title face.** Big Shoulders Display (SIL OFL 1.1), bundled in
  `web/assets/fonts/` and credited in `docs/credits.md`. It is used for page
  titles only; mono stays for numbers.
