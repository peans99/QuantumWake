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

## One detail view for each kind of thing

- **Items and ships** open the Reference window (`openRefDetail`), from any
  page, including search. Inside it, every part and hull opens in turn, and
  Back walks the trail.
- **Places and commodities** open the drawer, which sits beside the map.

`openEntity('part' | 'ship', …)` sends items and ships to the window, so the
two can never describe one gun two ways again.
