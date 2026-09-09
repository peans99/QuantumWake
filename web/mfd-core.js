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
  const commands = [
    { id: 'nav', label: 'Page · Nav', caption: 'NAV' },
    { id: 'task', label: 'Page · Task', caption: 'TASK' },
    { id: 'act', label: 'Page · Act', caption: 'ACT' },
    { id: 'cargo', label: 'Page · Cargo', caption: 'CARGO' },
    { id: 'contract', label: 'Page · Contract', caption: 'CNTRCT' },
    { id: 'status', label: 'Page · Status', caption: 'STATUS' },
    { id: 'prev', label: 'Previous page', caption: 'PREV' },
    { id: 'next', label: 'Next page', caption: 'NEXT' },
    { id: 'home', label: 'Home (Nav)', caption: 'HOME' },
    { id: 'up', label: 'Up · select or scroll', caption: 'UP' },
    { id: 'down', label: 'Down · select or scroll', caption: 'DOWN' },
    { id: 'text-up', label: 'Text size larger', caption: 'TEXT +' },
    { id: 'text-down', label: 'Text size smaller', caption: 'TEXT −' },
    { id: 'bright-up', label: 'Screen brighter', caption: 'BRIGHT' },
    { id: 'bright-down', label: 'Screen dimmer', caption: 'DIM' },
    { id: 'confirm', label: 'Confirm the selected task', caption: 'DONE' }
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
  return { fit, move, extent, action, buttons, caption, commands, defaults,
    pages, pageIds, rows, tasks, describe, plannedLoad, clamp, OSBS, BUTTONS };
})();
