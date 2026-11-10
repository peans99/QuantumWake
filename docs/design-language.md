# The top bar

The dashboard's pages are styled by `web/app.css`. The one exception is the
header, which `web/console.css` restyles after it: a Quantum Wake nameplate
above a single row of navigation menus.

1.2.18 restyled every page as a charcoal "spacecraft console". Nicolas
preferred the previous pages and asked to keep only the top menu with its
symbols, so 1.2.19 put the pages back and kept the header. Changes to the
header go in `console.css`, and changes to the pages go in `app.css`.

## What the header keeps

- A nameplate row (mark, name, live status, search) above the navigation row.
- The navigation symbols from `PAGE_GLYPHS` beside each menu name.
- An orange mark under the page you are on. The header has its own palette,
  scoped to `.topbar`, so that orange and its warm-white type stay in the
  header and the pages keep their cyan.
- In a narrow window the navigation wraps onto more lines instead of
  scrolling sideways, and `initNavigationMenuBounds` in `app.js` moves each
  dropdown so it stays inside the window. A scroller would cut the dropdowns
  off and hide keyboard-focused menu items.
- The header is two rows, so the flight line, the context rail and the Stream
  Deck summary stick under `--console-header` rather than app.css's fixed
  offsets. `syncNavigationHeader` measures the real height when the navigation
  wraps.

Everything is scoped to `body:not(.overlay)`: the in-game overlay and the
Cockpit HUD are not touched.

## Checking changes

Render the actual application with isolated data, following
`run-quantumwake`. Check a wide window and a narrow one (the navigation
wraps), open a menu with the keyboard, and check that the flight line sits
directly under the header rather than behind it.
