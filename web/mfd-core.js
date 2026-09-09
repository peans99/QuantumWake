/* Shared by the placement editor, display and tests. Coordinates are monitor-local pixels. */
'use strict';
window.QwMfd = (() => {
  const clamp = (n, min, max) => Math.max(min, Math.min(max, n));
  const integer = (n, fallback) => Number.isFinite(Number(n)) ? Math.round(Number(n)) : fallback;
  function fit(panel, monitor) {
    if (!monitor) return { ...panel };
    const width = clamp(integer(panel.width, 480), Math.min(220, monitor.width), monitor.width);
    const height = clamp(integer(panel.height, 480), Math.min(220, monitor.height), monitor.height);
    return { ...panel, width, height,
      x: clamp(integer(panel.x, 0), 0, monitor.width - width),
      y: clamp(integer(panel.y, 0), 0, monitor.height - height) };
  }
  function move(panel, x, y, monitors) {
    const monitor = monitors.find(m => x + panel.width / 2 >= m.x && y + panel.height / 2 >= m.y
      && x + panel.width / 2 < m.x + m.width && y + panel.height / 2 < m.y + m.height);
    if (!monitor) return panel;
    return fit({ ...panel, monitor: monitor.id, x: x - monitor.x, y: y - monitor.y }, monitor);
  }
  function extent(monitors) {
    const x = Math.min(...monitors.map(m => m.x)), y = Math.min(...monitors.map(m => m.y));
    return { x, y, width: Math.max(...monitors.map(m => m.x + m.width)) - x,
      height: Math.max(...monitors.map(m => m.y + m.height)) - y };
  }

  /* The Cougar's 20 optical buttons, then its four two-way rockers. Which
     rocker is which number cannot be had from a datasheet, so they are
     bindable and named by the setup tester rather than guessed at here. */
  const OSBS = 20, BUTTONS = 28;
  const pageIds = ['nav', 'task', 'act', 'cargo', 'contract', 'status'];
  const pages = ['NAV', 'TASK', 'ACT', 'CARGO', 'CONTRACT', 'STATUS'];

  /* One vocabulary for the display, the setup editor and the stored profile.
     Ids rather than page numbers in the file: a profile saved today still
     means what the pilot chose if the pages are ever reordered. */
  /* Icons are stroked paths on a 24-box, drawn here rather than pulled from a
     font: standalone mode makes no outbound request, and the two glyph sets
     Windows ships with are a lottery at 14 px. `currentColor` means a pressed
     button inverts its icon along with its caption for free. */
  const commands = [
    { id: 'nav', label: 'Page · Nav', caption: 'NAV',
      icon: 'M12 3v3M12 18v3M3 12h3M18 12h3M12 7.5a4.5 4.5 0 1 0 .1 0M12 12l3.5-3.5' },
    { id: 'task', label: 'Page · Task', caption: 'TASK',
      icon: 'M4 6.5h10M4 12h10M4 17.5h6M16.5 16l2 2 3.5-4' },
    { id: 'act', label: 'Page · Act', caption: 'ACT',
      icon: 'M4 5h16v14H4zM8 12l3 3 5-6' },
    { id: 'cargo', label: 'Page · Cargo', caption: 'CARGO',
      icon: 'M3 8l9-4 9 4v8l-9 4-9-4zM3 8l9 4 9-4M12 12v8' },
    { id: 'contract', label: 'Page · Contract', caption: 'CNTRCT',
      icon: 'M6 3h8l4 4v14H6zM14 3v4h4M9 12h6M9 16h6' },
    { id: 'status', label: 'Page · Status', caption: 'STATUS',
      icon: 'M12 3a9 9 0 1 0 .1 0M12 7.5v.5M12 11v6' },
    { id: 'prev', label: 'Previous page', caption: 'PREV', icon: 'M15 4L7 12l8 8' },
    { id: 'next', label: 'Next page', caption: 'NEXT', icon: 'M9 4l8 8-8 8' },
    { id: 'home', label: 'Home (Nav)', caption: 'HOME', icon: 'M3 11l9-7 9 7M6 9.5V20h12V9.5' },
    { id: 'up', label: 'Up · select or scroll', caption: 'UP', icon: 'M4 15l8-8 8 8' },
    { id: 'down', label: 'Down · select or scroll', caption: 'DOWN', icon: 'M4 9l8 8 8-8' },
    { id: 'text-up', label: 'Text size larger', caption: 'TEXT +',
      icon: 'M2 19L8 5l6 14M4.2 14.5h7.6M18 9v8M14 13h8' },
    { id: 'text-down', label: 'Text size smaller', caption: 'TEXT −',
      icon: 'M2 19L8 5l6 14M4.2 14.5h7.6M14 13h8' },
    { id: 'bright-up', label: 'Screen brighter', caption: 'BRIGHT',
      icon: 'M12 8.5a3.5 3.5 0 1 0 .1 0M12 1.5v3M12 19.5v3M1.5 12h3M19.5 12h3M4.6 4.6l2 2M17.4 17.4l2 2M19.4 4.6l-2 2M6.6 17.4l-2 2' },
    { id: 'bright-down', label: 'Screen dimmer', caption: 'DIM',
      icon: 'M12 8.5a3.5 3.5 0 1 0 .1 0M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2' },
    { id: 'confirm', label: 'Confirm the selected task', caption: 'DONE',
      icon: 'M4 12.5l5.5 5.5L20 6' }
  ];

  /* The shipped profile, on the official clockwise numbering: top 1-5, right
     6-10, bottom 11-15 right to left, left 16-20 bottom to top. The rockers
     start unassigned - see the note above. */
  const defaults = {
    1: 'nav', 2: 'task', 3: 'act', 4: 'cargo', 5: 'contract',
    6: 'text-up', 7: 'text-down', 8: 'confirm',
    11: 'next', 12: 'down', 13: 'home', 14: 'up', 15: 'prev',
    16: 'status', 19: 'bright-down', 20: 'bright-up'
  };

  /* A stored map is the whole answer, so a button the pilot cleared stays
     cleared. Only a missing or unreadable map falls back to the defaults -
     otherwise unassigning a button would quietly reassign it on the next
     reload, which is the one way a custom profile could not be trusted. */
  function buttons(stored) {
    if (!stored || typeof stored !== 'object') return { ...defaults };
    const map = {};
    for (let n = 1; n <= BUTTONS; n++) {
      const id = stored[n] ?? stored[String(n)];
      if (typeof id === 'string' && commands.some(c => c.id === id)) map[n] = id;
    }
    return map;
  }
  const caption = id => commands.find(c => c.id === id)?.caption || null;
  const icon = id => commands.find(c => c.id === id)?.icon || null;

  /* The makers with a logo file in web/assets/manufacturers. A copy of app.js's
     MANUFACTURERS, because the panel cannot load fourteen thousand lines of
     dashboard to read sixteen names - and a copy is only safe because a test
     loads both files and fails when they disagree. Do not add a code here
     without adding the PNG. */
  const makers = {
    DRAK: 'Drake Interplanetary', ANVL: 'Anvil Aerospace', RSI: 'Roberts Space Industries',
    MISC: 'MISC', ORIG: 'Origin Jumpworks', AEGS: 'Aegis Dynamics',
    CRUS: 'Crusader Industries', CNOU: 'Consolidated Outland', TMBL: 'Tumbril',
    ESPR: 'Esperia', BANU: 'Banu', KRIG: 'Kruger Intergalactic',
    ARGO: 'ARGO Astronautics', AOPO: 'Aopoa', GATS: 'Gatac', MRAI: 'Mirai',
  };

  /* Which maker a ship name announces, by code and by name, longest match
     first - "Consolidated Outland" has to beat "Consolidated". The logs used to
     write the code ("DRAK Corsair") and the community dataset now resolves the
     real name ("Drake Corsair"), so both have to hit; matching the code alone
     is the bug that once left every maker whose code is not its name without a
     badge. `table` is the community code-to-name map when it is available, and
     may only teach new aliases for codes that already have a logo. */
  function makerOf(shipName, table) {
    if (!shipName) return null;
    const alias = new Map();
    const learn = (from, code) => { if (from) alias.set(String(from).toLowerCase(), code); };
    for (const [code, name] of Object.entries({ ...makers, ...(table || {}) })) {
      if (!(code in makers)) continue;
      learn(code, code); learn(name, code); learn(String(name).split(' ')[0], code);
    }
    const words = String(shipName).trim().split(/\s+/);
    for (let take = Math.min(3, words.length); take >= 1; take--) {
      const code = alias.get(words.slice(0, take).join(' ').toLowerCase());
      if (code) return { code, name: makers[code], model: words.slice(take).join(' ') || shipName };
    }
    return null;
  }

  function effect(id) {
    const page = pageIds.indexOf(id);
    if (page >= 0) return { page };
    return ({ prev: { cycle: -1 }, next: { cycle: 1 }, home: { page: 0 },
      up: { scroll: -1 }, down: { scroll: 1 },
      'text-up': { text: 1 }, 'text-down': { text: -1 },
      'bright-up': { brightness: 1 }, 'bright-down': { brightness: -1 },
      confirm: { confirm: true } })[id] || null;
  }
  function action(button, stored) {
    if (!Number.isInteger(button) || button < 1 || button > BUTTONS) return null;
    return effect(buttons(stored)[button]);
  }

  /* What the pilot wrote on one instruction, in their own words and units.
     Shared so the Task summary and the Act list cannot drift apart. */
  function describe(item) {
    const amount = item?.quantity != null
      ? `${item.quantity}${item.unit ? ' ' + item.unit : ''}` : '';
    return [item?.kind, amount, item?.text].filter(Boolean).join(' · ');
  }

  /* The outstanding work at the next stop, as a list a cursor can walk. A stop
     with no unfinished instruction becomes the single item, because crossing
     the stop off is then the only thing left to confirm there. */
  function tasks(briefing) {
    const stop = briefing?.stops?.[0];
    if (!stop) return [];
    const open = (stop.actions || []).filter(a => !a.done);
    if (open.length === 0)
      return [{ kind: 'stop', tripId: briefing.tripId, stopId: stop.id, place: stop.place,
        label: stop.note || 'Nothing written for this stop' }];
    return open.map(a => ({ kind: 'action', tripId: briefing.tripId, stopId: stop.id,
      actionId: a.id, place: stop.place, label: describe(a) }));
  }

  /* Everything the plan still says to put aboard. Quantities are added up only
     where the pilot wrote SCU on them; anything else is counted separately
     rather than folded into a total that would be measuring two things. */
  function plannedLoad(briefing) {
    let scu = 0, stops = 0, unmeasured = 0;
    for (const stop of briefing?.stops || []) {
      const open = (stop.actions || []).filter(a => !a.done && (a.kind === 'load' || a.kind === 'buy'));
      if (!open.length) continue;
      stops++;
      for (const a of open) {
        if (a.unit === 'SCU' && Number.isFinite(Number(a.quantity))) scu += Number(a.quantity);
        else unmeasured++;
      }
    }
    return { scu, stops, unmeasured };
  }

  /* A top-down plan of the system you are standing in, from the community
     starmap's own body coordinates - real geometry rather than an even ring.
     Coordinates come back normalised to the outermost body, so the renderer
     needs no idea how many gigametres it is drawing.

     It marks a body, never a point. The logs name the place you are at and the
     body it sits on; where you are on that body is not something Game.log ever
     says, and a dot placed on a surface would be an invention. The caption says
     so on the panel itself. */
  function mapView(atlas, state, briefing) {
    const s = state || {};
    if (!atlas) return { note: 'Loading the star map…' };
    const system = s.locationSystem || '';
    const positions = atlas.positions?.[system.toLowerCase()];
    if (!system) return { note: 'No system identified in the logs yet' };
    if (!positions || !Object.keys(positions).length)
      return { system, note: `No body positions for ${system}` };

    const bodyOf = id => id ? (atlas.nodes || []).find(n => n.rawId === id)?.body || null : null;
    const here = s.locationBody || null;

    /* The whole plan, in the order it will be flown, rather than only the next
       hop. A quantum destination goes in front of it: it is where the ship is
       actually pointed, and the plan resumes from wherever it puts you. */
    const planned = (briefing?.stops || []).map(stop => bodyOf(stop.placeId));
    const ahead = [];
    if (s.travelling) { const jump = bodyOf(s.travellingToId); if (jump) ahead.push(jump); }
    ahead.push(...planned);

    // Consecutive repeats are one place, not a leg of no length: two stops at
    // the same body is two jobs, one arrival.
    const route = [];
    for (const body of [here, ...ahead])
      if (body && positions[body] && route[route.length - 1] !== body) route.push(body);
    const elsewhere = ahead.filter(body => !body || !positions[body]).length;

    /* Straight-line, body centre to body centre. It is not a flight path and
       not a quantum route - the game plots those and never writes them down -
       so the caption says what the number is before the pilot trusts it. */
    const legs = [];
    for (let i = 1; i < route.length; i++) {
      const a = positions[route[i - 1]], b = positions[route[i]];
      const dx = a.x - b.x, dy = a.y - b.y;
      legs.push({ from: route[i - 1], to: route[i], gm: Math.sqrt(dx * dx + dy * dy) / 1e9 });
    }
    const gm = legs.reduce((total, leg) => total + leg.gm, 0);
    const next = route[1] || null, target = route[route.length - 1] || null;

    const entries = Object.entries(positions);
    const far = Math.max(...entries.map(([, p]) => Math.sqrt(p.x * p.x + p.y * p.y))) || 1;
    const bodies = entries.map(([name, p]) => {
      const radius = Math.sqrt(p.x * p.x + p.y * p.y) / far;
      return { name, x: p.x / far, y: p.y / far, radius,
        here: name === here, next: name === next,
        onRoute: route.indexOf(name) > 0 };
    });

    // Moons sit within a rounding of their planet's orbit, so one ring each
    // would draw the same circle four times over a 150 px panel.
    const rings = [...new Set(bodies.map(b => Math.round(b.radius * 50) / 50))].filter(r => r > .04);
    const note = [system.toUpperCase(),
      here ? 'bodies, not a fix or a route' : 'body not identified',
      elsewhere ? `${elsewhere} stop${elsewhere > 1 ? 's' : ''} outside this system` : null]
      .filter(Boolean).join(' · ');
    return { system, here, next, target, bodies, rings, legs, gm, elsewhere, note };
  }

  /* The distance the plan adds up to, for the Nav rows. Named for what it
     measures: a straight line between body centres, which is the only thing
     the coordinates support. */
  function routeLine(map) {
    if (!map?.legs?.length) return null;
    const total = map.gm >= 100 ? map.gm.toFixed(0) : map.gm.toFixed(1);
    return map.legs.length === 1
      ? `${total} Gm to ${map.target}`
      : `${total} Gm to ${map.target} over ${map.legs.length} legs`;
  }

  function loadLine(load) {
    if (!load.stops) return 'No load or purchase planned';
    const parts = [];
    if (load.scu) parts.push(`${load.scu} SCU`);
    if (load.unmeasured) parts.push(`${load.unmeasured} with no SCU figure`);
    return `${parts.join(' + ')} across ${load.stops} stop${load.stops > 1 ? 's' : ''} · planned, not detected`;
  }

  /* A row is [label, value, timestamp, mark]. The mark is how the display is
     told which line the cursor is on, so the selection rule lives here with
     the rest of the page logic rather than in the renderer. */
  function rows(page, state, briefing, briefingUnavailable = false, view = {}) {
    const s = state || {};
    const stop = briefing?.stops?.[0];
    const planMissing = briefingUnavailable
      ? [['PLAN UNAVAILABLE', 'Cannot read the flight plan. Check the dashboard connection.']]
      : !briefing ? [['FLIGHT PLAN', 'Loading your tracked plan…']] : null;
    switch (pageIds[page]) {
      case 'nav': return [
        ['LOCATION', s.location || 'Location not yet identified'],
        [s.travelling ? 'QUANTUM DESTINATION' : (briefingUnavailable ? 'PLAN UNAVAILABLE' : 'NEXT PLANNED STOP'),
          s.travelling ? (s.travellingTo || 'Destination not identified')
            : briefingUnavailable ? 'Open the dashboard to check your route.'
              : !briefing ? 'Loading your tracked plan…' : stop?.place || 'No outstanding stop in the tracked plan'],
        ...(routeLine(view.map) ? [['DISTANCE', routeLine(view.map)]] : []),
        ['SHIP', s.ship || 'No ship identified in the logs'],
        ['LOCATION SOURCE', s.location ? `${s.confidence || 'Unknown'} confidence · game logs` : 'Waiting for a location signal']
      ];
      case 'task': {
        if (planMissing) return planMissing;
        if (!stop) return [['NO OUTSTANDING STOP', 'Track a flight plan in the dashboard to show the next task here.']];
        const next = (stop.actions || []).find(a => !a.done);
        return [['NEXT STOP', stop.place],
          ['NEXT ACTION', next ? describe(next) : stop.note || 'Travel to this stop'],
          ['PLAN', briefing.tripTitle || 'Tracked flight plan'],
          ['PLAN ONLY', 'Not a detected cargo manifest.']];
      }
      case 'act': {
        if (planMissing) return planMissing;
        const list = tasks(briefing);
        if (!list.length) return [['NOTHING TO CONFIRM',
          'Track a flight plan in the dashboard to tick its work off from here.']];
        const selected = clamp(integer(view.selected, 0), 0, list.length - 1);
        return [
          ['STOP', list[0].place],
          ...list.map((task, i) => [
            task.kind === 'stop' ? 'CROSS OFF THE STOP' : `TASK ${i + 1} OF ${list.length}`,
            task.label, null,
            i !== selected ? null : view.armed ? 'armed' : 'cursor']),
          [view.armed ? 'CONFIRM?' : 'CONFIRM', view.armed
            ? 'Press DONE again to tick this off. Any other button cancels.'
            : 'DONE marks the selected line in your own plan. It changes nothing in the game.']
        ];
      }
      case 'cargo': {
        const cargo = s.cargo, last = cargo?.last;
        return [
          ['PLANNED LOAD', briefingUnavailable ? 'Cannot read the flight plan.'
            : !briefing ? 'Loading your tracked plan…' : loadLine(plannedLoad(briefing))],
          ['LAST COUNTER MOVE', last
            ? [`${last.sell ? 'Sold' : 'Bought'} ${last.scu} SCU`, last.commodity, last.shop,
              `${Math.round(last.amount).toLocaleString()} aUEC`].filter(Boolean).join(' · ')
            : 'No commodity counter used in this session', last?.at],
          ['THIS SESSION', cargo
            ? `${cargo.boughtScu} SCU bought · ${cargo.soldScu} SCU sold`
            : 'Nothing bought or sold at a commodity counter yet'],
          ['NOT A MANIFEST', 'The game logs no cargo hold. This is what the counters recorded '
            + 'this session and what you planned - never what is aboard.']
        ];
      }
      case 'contract': {
        const open = s.contracts || [];
        if (!open.length) return [
          ['NO OPEN CONTRACT', 'Nothing accepted in this session that the logs have not since closed.'],
          ['SESSION ONLY', 'Earlier contracts are in the dashboard logbook, not here.']];
        const c = open[0];
        // Without a text mod the game's own title is already "issuer · type ·
        // difficulty", so an issuer row would print the line above it twice.
        // Only what the title does not already carry earns the space.
        const about = [c.issuer, c.type, c.difficulty]
          .filter(part => part && !String(c.name).includes(part));
        return [
          ['CONTRACT', c.name],
          ['OBJECTIVES', c.steps > 0 ? `${c.stepsDone} of ${c.steps} done`
            : 'The journal reported no objective steps for this one'],
          ...(about.length ? [['ISSUER', about.join(' · ')]] : []),
          ['TAKEN', 'This session', c.since],
          ...(open.length > 1 ? [['ALSO OPEN', `${open.length - 1} more still open`]] : [])
        ];
      }
      case 'status': return [
        ['SESSION', s.inGame ? (s.gameRules || 'In game') : 'Menus / no active game session'],
        ['PILOT', s.handle || 'Waiting for pilot identity'],
        ...(s.screen ? [['LAST SCREENSHOT', s.screen.summary, s.screen.shotAt]]
          : [['NO SCREENSHOT READING', 'Read a screenshot in the dashboard to add a cross-check.']]),
        ['INSTRUMENTS', 'Screenshot readings are saved observations. Live shields, fuel and power are not supplied.']
      ];
      default: return [];
    }
  }
  return { fit, move, extent, action, buttons, caption, icon, commands, defaults, mapView, routeLine, makerOf, makers,
    pages, pageIds, rows, tasks, describe, plannedLoad, clamp, OSBS, BUTTONS };
})();
