# Quantum Wake: a ship's console

Quantum Wake is a pilot's record of a spacefaring life. Its dashboard should
feel like equipment aboard an exploration vessel: useful over a long session,
recognisable without a logo in every panel, and explicit about what it knows.

The dashboard treatment lives in `web/console.css`, after the functional
component styles in `web/app.css`. It is scoped away from the in-game overlay;
the overlay's footprint, transparency and type scale are a separate concern.

## Identity

- Charcoal instrument surfaces and warm-white readouts. Space comes from the
  ships, places and course information, rather than cyan glow on every frame.
- Orange marks actions, navigation selection and the current focus. Warning,
  danger and success keep their own meanings; provenance retains its source
  names and colours.
- A nameplate and continuous navigation index form the console's top edge.
  Menus remain attached to their controls and fit inside the viewport.
- The wake mark extends into a restrained course-chart motif on the Now page.
  It is decorative, with no invented coordinates, route claims or telemetry.
  Working page headers leave room for filters rather than putting art behind
  them.
- Flat panels, fine structural rules and clear type establish hierarchy.
  Avoid glowing outlines, ambient scan animations, rounded panels inside more
  rounded panels, and a decorative icon beside every label.

## Information

Page names match navigation. The spacecraft language belongs in the visual
system, not in unfamiliar names for ordinary actions. The pilot still sees
Now, Fleet, Map and Settings.

Use the game's actual ship art and recorded course information as the main
visual material. Do not add decorative statistics, fake status lights or an
illustration that could be mistaken for a sensor reading.

Missing data and inferred figures retain their explanations. Closed-game
records do not present the last reported activity as a current focus. Session
health has room for its caveat rather than forcing it into a narrow column.

Keep actions visible before hover. Preserve keyboard focus and the existing
choice of status fields. On narrow windows, wrap the navigation index and
bound its menus instead of clipping them inside a horizontal scroller.

## Checking changes

Render the actual application with isolated data, following
`run-quantumwake`. Check Now, a ship-heavy view, a reference or map view,
Settings and a dense tool such as the Stream Deck builder. Inspect narrow
layouts and keyboard-opened menus. Compare the overlay with the console
stylesheet enabled and disabled before claiming the dashboard work leaves it
alone.
