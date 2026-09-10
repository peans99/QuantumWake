/* Shared by the placement editor, display and tests. Coordinates are monitor-local pixels. */
'use strict';
window.QwMfd = (() => {
  const clamp = (n, min, max) => Math.max(min, Math.min(max, n));

  /* Dimming and its timing are one decision, not two: how readable a frame
     nobody has touched should stay. A third of the pilot's own brightness
     still reads at a glance, which is the point — a panel bolted into a
     cockpit is looked at far more often than it is pressed, so blanking it
     would trade every glance for a press. */
  const DOZE = .3;

  /* Four steps rather than a slider. A button that nudges five percent cannot
     be pressed to a known place — you press it until it looks right, and next
     flight you do it again — whereas "the third one" is a setting a pilot can
     actually hold in their head. The setup page offers these same four, so a
     level picked by hand and a level pressed on the frame mean one thing. */
  const BRIGHTNESS = [.4, .6, .8, 1];
  const brightnessLevel = value => {
    const n = Number(value);
    if (!Number.isFinite(n)) return BRIGHTNESS.length;
    let best = 0;
    for (let i = 1; i < BRIGHTNESS.length; i++) {
      if (Math.abs(BRIGHTNESS[i] - n) < Math.abs(BRIGHTNESS[best] - n)) best = i;
    }
    return best + 1;
  };
  const brightnessAt = level => BRIGHTNESS[clamp(Math.round(level) || 1, 1, BRIGHTNESS.length) - 1];
  /* The one button wraps, because a button that stops has a dead press in it.
     The rocker stops at the ends, because a rocker held down rolling off the
     bottom into full brightness is the opposite of what the hand asked for. */
  const cycleBrightness = value => BRIGHTNESS[brightnessLevel(value) % BRIGHTNESS.length];
  const stepBrightness = (value, direction) => brightnessAt(brightnessLevel(value) + direction);
  const dozed = (sleepAfterMinutes, idleMs) =>
    sleepAfterMinutes > 0 && idleMs >= sleepAfterMinutes * 60000;
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
  // Keep these indices stable for displays saved before menu navigation.
  const pageIds = ['nav', 'task', 'act', 'cargo', 'contract', 'status', 'feed', 'crew', 'money', 'list',
    'ship', 'here', 'ledger', 'mine', 'map'];
  const pages = ['NAV', 'TASK', 'ACT', 'CARGO', 'CONTRACT', 'STATUS', 'FEED', 'CREW', 'MONEY', 'LIST',
    'SHIP', 'HERE', 'LEDGER', 'MINE', 'MAP'];

  /* One vocabulary for the display, the setup editor and the stored profile.
     Ids rather than page numbers in the file: a profile saved today still
     means what the pilot chose if the pages are ever reordered. */
  /* Icons are stroked paths on a 24-box, drawn here rather than pulled from a
     font: standalone mode makes no outbound request, and the two glyph sets
     Windows ships with are a lottery at 14 px. `currentColor` means a pressed
     button inverts its icon along with its caption for free. */
  const commands = [
    { id: 'nav', label: 'Page · Nav', caption: 'NAV', short: 'NAV',
      icon: 'M12 3v3M12 18v3M3 12h3M18 12h3M12 7.5a4.5 4.5 0 1 0 .1 0M12 12l3.5-3.5' },
    { id: 'task', label: 'Page · Task', caption: 'TASK', short: 'TASK',
      icon: 'M4 6.5h10M4 12h10M4 17.5h6M16.5 16l2 2 3.5-4' },
    { id: 'act', label: 'Page · Act', caption: 'ACT', short: 'ACT',
      icon: 'M4 5h16v14H4zM8 12l3 3 5-6' },
    { id: 'cargo', label: 'Page · Cargo', caption: 'CARGO', short: 'CRGO',
      icon: 'M3 8l9-4 9 4v8l-9 4-9-4zM3 8l9 4 9-4M12 12v8' },
    { id: 'contract', label: 'Page · Contract', caption: 'CNTRCT', short: 'CNTR',
      icon: 'M6 3h8l4 4v14H6zM14 3v4h4M9 12h6M9 16h6' },
    { id: 'status', label: 'Page · Status', caption: 'STATUS', short: 'STAT',
      icon: 'M12 3a9 9 0 1 0 .1 0M12 7.5v.5M12 11v6' },
    { id: 'feed', label: 'Page · Feed', caption: 'FEED', short: 'FEED',
      icon: 'M4 18a14 14 0 0 1 14 0M4 18h.01M5 13.5a9.5 9.5 0 0 1 13 0M5 9a15 15 0 0 1 15 0' },
    { id: 'crew', label: 'Page · Crew', caption: 'CREW', short: 'CREW',
      icon: 'M9 11a3.2 3.2 0 1 0 .1 0M3.5 20c0-3 2.5-5 5.5-5s5.5 2 5.5 5M16 5.4a3.2 3.2 0 0 1 0 6.2M17.5 15.4c2 .8 3 2.4 3 4.6' },
    { id: 'money', label: 'Page · Money', caption: 'MONEY', short: 'MNY',
      icon: 'M12 2.5v19M8 7h6.5a2.75 2.75 0 0 1 0 5.5h-5a2.75 2.75 0 0 0 0 5.5H16' },
    { id: 'list', label: 'Page · List', caption: 'LIST', short: 'LIST',
      icon: 'M9 6.5h11M9 12h11M9 17.5h11M4 6l1.2 1.2L7.5 4.5M4 17.5l1.2 1.2 2.3-2.7' },
    { id: 'ship', label: 'Page · Ship', caption: 'SHIP', short: 'SHIP',
      icon: 'M12 2.2c3 3 4.6 7 4.6 11l-4.6 4.2-4.6-4.2c0-4 1.6-8 4.6-11zM12 9.4a1.7 1.7 0 1 0 .1 0M7.6 15.2 5 19.6l4.2-1.2M16.4 15.2l2.6 4.4-4.2-1.2' },
    { id: 'here', label: 'Page · Here', caption: 'HERE', short: 'HERE',
      icon: 'M12 21.2s6.8-6.4 6.8-11.2a6.8 6.8 0 1 0-13.6 0c0 4.8 6.8 11.2 6.8 11.2zM12 7.6a2.4 2.4 0 1 0 .1 0' },
    { id: 'ledger', label: 'Page · Ledger', caption: 'LEDGER', short: 'LEDG',
      icon: 'M4 20.5V10M9.5 20.5V3.5M15 20.5v-7M20.5 20.5V6.5' },
    { id: 'mine', label: 'Page · Mine', caption: 'MINE', short: 'MINE',
      icon: 'M4 20.5 12 12.5M6.2 8.2c3-3 8.2-4.1 12.2-3-1 4-2.1 9.2-5.1 12.2M14.2 6.2l4 4' },
    { id: 'map', label: 'Page · Map', caption: 'MAP', short: 'MAP',
      icon: 'M2.5 5.8 9 3.4v14.8L2.5 20.6zM9 3.4l6 2.4v14.8l-6-2.4M15 5.8l6.5-2.4v14.8L15 20.6' },
    { id: 'prev', label: 'Previous page', caption: 'PREV', short: 'PRV', icon: 'M15 4L7 12l8 8' },
    { id: 'next', label: 'Next page', caption: 'NEXT', short: 'NXT', icon: 'M9 4l8 8-8 8' },
    { id: 'home', label: 'Home menu', caption: 'HOME', short: 'HOME', icon: 'M3 11l9-7 9 7M6 9.5V20h12V9.5' },
    { id: 'back', label: 'Back · up one menu level', caption: 'BACK', short: 'BACK', icon: 'M10 5l-7 7 7 7M3 12h18' },
    { id: 'details', label: 'Show details / overview', caption: 'MORE', short: 'MORE', icon: 'M5 6h14M5 12h14M5 18h14' },
    { id: 'up', label: 'Up · select or scroll', caption: 'UP', short: 'UP', icon: 'M4 15l8-8 8 8' },
    { id: 'down', label: 'Down · select or scroll', caption: 'DOWN', short: 'DN', icon: 'M4 9l8 8 8-8' },
    { id: 'text-up', label: 'Text size larger', caption: 'TEXT +', short: 'A+',
      icon: 'M2 19L8 5l6 14M4.2 14.5h7.6M18 9v8M14 13h8' },
    { id: 'text-down', label: 'Text size smaller', caption: 'TEXT −', short: 'A-',
      icon: 'M2 19L8 5l6 14M4.2 14.5h7.6M14 13h8' },
    { id: 'bright', label: 'Screen brightness · 1 to 4', caption: 'BRT', short: 'BRT',
      icon: 'M12 8.5a3.5 3.5 0 1 0 .1 0M12 1.5v3M12 19.5v3M1.5 12h3M19.5 12h3M4.6 4.6l2 2M17.4 17.4l2 2M19.4 4.6l-2 2M6.6 17.4l-2 2' },
    { id: 'bright-up', label: 'Screen brighter · one level', caption: 'BRIGHT', short: 'BRT+',
      icon: 'M12 8.5a3.5 3.5 0 1 0 .1 0M12 1.5v3M12 19.5v3M1.5 12h3M19.5 12h3M4.6 4.6l2 2M17.4 17.4l2 2M19.4 4.6l-2 2M6.6 17.4l-2 2' },
    { id: 'bright-down', label: 'Screen dimmer · one level', caption: 'DIM', short: 'DIM',
      icon: 'M12 8.5a3.5 3.5 0 1 0 .1 0M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2' },
    { id: 'confirm', label: 'Confirm the selected task', caption: 'DONE', short: 'DONE',
      icon: 'M4 12.5l5.5 5.5L20 6' }
  ];

  /* The shipped profile, on the official clockwise numbering: top 1-5, right
     6-10, bottom 11-15 right to left, left 16-20 bottom to top. The rockers
     start unassigned - see the note above. */
  /* Menus are data, not a screen-specific set of if statements. Adding a new
     branch only adds a node here; the face, breadcrumb and Back all discover
     its parent and children from the same tree.

     Two levels, not three. The middle tier existed to split two screens apiece -
     Route held Navigation and System map, People held Crew on its own - so it
     cost a press on the way to everything and sorted nothing. Each category
     fits the five top buttons by itself: four screens at most, and the row has
     five. The tree still nests to any depth if a branch ever earns it. */
  const groups = [
    { id: 'flight', title: 'Flight', short: 'FLT', icon: 'nav', hint: 'Where you are, and what you fly',
      children: ['nav', 'map', 'ship', 'here'] },
    { id: 'operations', title: 'Operations', short: 'OPS', icon: 'task', hint: 'Plan, checklist, jobs',
      children: ['task', 'act', 'contract', 'list'] },
    { id: 'resources', title: 'Resources', short: 'RSRC', icon: 'cargo', hint: 'Cargo, money, mining',
      children: ['cargo', 'ledger', 'mine', 'money'] },
    { id: 'pilot', title: 'Pilot', short: 'PLT', icon: 'status', hint: 'Session, activity, crew',
      children: ['status', 'feed', 'crew'] }
  ];
  const titles = { home: 'Cockpit', nav: 'Navigation', task: 'Flight plan', act: 'Checklist',
    cargo: 'Cargo & trade', contract: 'Contract', status: 'Session', feed: 'Activity', crew: 'Crew',
    money: 'Earnings', list: 'Shopping', ship: 'Ship', here: 'Local intel', ledger: 'Ledger', mine: 'Mining', map: 'System map' };
  const menuNodes = {}, leafParents = {};
  function indexMenu(nodes, parentId = 'home') {
    for (const node of nodes) {
      const children = node.children.map(child => typeof child === 'string' ? child : child.id);
      menuNodes[node.id] = { ...node, children, parent: parentId };
      for (const child of node.children) {
        if (typeof child === 'string') leafParents[child] = node.id;
        else indexMenu([child], node.id);
      }
    }
  }
  indexMenu(groups);
  for (const node of Object.values(menuNodes)) commands.push({ id: node.id, label: 'Menu · ' + node.title,
    caption: node.short, short: node.short, icon: commands.find(c => c.id === node.icon).icon });
  /* Named for what the pilot sees rather than for what the code does: these are
     the five top buttons, and what each one means is whatever the screen is
     offering in that position. "Context menu · option 3" described the
     mechanism to somebody who wanted the effect. */
  for (let slot = 1; slot <= 5; slot++) commands.push({ id: 'menu-' + slot,
    label: 'Menu position ' + slot + ' · follows the screen',
    caption: 'OPT ' + slot, short: 'M' + slot,
    icon: 'M4 6h16M4 12h16M4 18h16' });

  const defaults = { 1: 'menu-1', 2: 'menu-2', 3: 'menu-3', 4: 'menu-4', 5: 'menu-5',
    8: 'confirm', 10: 'details', 12: 'down', 13: 'home', 14: 'up', 15: 'back' };
  const menuNode = id => menuNodes[id] || null;
  const parent = id => menuNodes[id]?.parent || leafParents[id] || 'home';
  const title = id => menuNodes[id]?.title || titles[id] || titles.home;
  const validScreen = id => id === 'home' || !!menuNodes[id] || pageIds.includes(id);
  function menuItems(screen) {
    /* Home carries Navigation beside the four categories: it is the page a
       pilot wants most and the fifth slot was empty, so putting it behind a
       category would have been spending a press to save nothing. */
    if (screen === 'home') return [...groups.map(g => g.id), 'nav'];
    return menuNodes[screen]?.children || menuNodes[parent(screen)]?.children || [];
  }
  function previewPage(id) {
    if (pageIds.includes(id)) return id;
    const next = menuItems(id)[0];
    return next ? previewPage(next) : null;
  }
  function trail(id) {
    const path = [id];
    while (path[0] !== 'home') path.unshift(parent(path[0]));
    return path;
  }
  // The label and the press resolve through the same menu, including unused slots.
  function resolveCommand(id, screen) {
    if (!/^menu-[1-5]$/.test(id || '')) return id;
    return menuItems(screen)[Number(id.slice(-1)) - 1] || null;
  }
  /* A frame that has never been used opens on something worth reading, not on
     the menu. Two frames both starting at Home showed the same list twice and
     wasted the second one - which is the whole reason there are two. */
  function restoreScreen(preferences, fallback = 'home') {
    if (validScreen(preferences?.screen)) return preferences.screen;
    if (Number.isInteger(preferences?.page) && pageIds[preferences.page]) return pageIds[preferences.page];
    return validScreen(fallback) ? fallback : 'home';
  }
  const longPages = ['act', 'feed', 'crew', 'list', 'ledger', 'mine', 'map'];
  function readingView(id, rows, details) {
    // The cargo qualifier must travel with both views, never disappear behind More.
    const qualifier = rows.filter(r => r[0] === 'NOT A MANIFEST');
    const body = rows.filter(r => r[0] !== 'NOT A MANIFEST');
    const limit = qualifier.length ? 2 : 3;
    const split = !longPages.includes(id) && body.length > limit;
    return { rows: [...(split ? (details ? body.slice(limit) : body.slice(0, limit)) : body), ...qualifier], more: split };
  }

  /* Which commands do nothing on the page in front of the pilot. Only three are
     ever in doubt: Up and Down have nothing to move on a page that already
     fits, and Done has nothing to confirm anywhere but Act. A page button never
     dims - a way out that disappears is worse than one that is redundant. */
  function dormant(pageId, context) {
    const idle = [];
    const tasks = context?.tasks || 0;
    if (!(context?.scrollable || (pageId === 'act' && tasks > 1))) idle.push('up', 'down');
    if (!(pageId === 'act' && tasks > 0)) idle.push('confirm');
    return idle;
  }

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
  /* A 220 px opening leaves an edge button about six characters. The long
     caption is what to read on a full-size panel; the short one is what
     survives a small one, because clipping CNTRCT to "CNTRC" is worse than
     either of them. */
  /* One glyph per kind of reading, so a page scans as a HUD rather than as a
     list of sentences. Keyed off the label because the labels are a closed
     vocabulary written in this file - and anything unmapped gets the neutral
     mark rather than a blank, so a column of icons stays a column. */
  const glyphs = [
    [/LOCATION|PLACE|SEEN HERE|WAKE UP/, 'M12 21.2s6.8-6.4 6.8-11.2a6.8 6.8 0 1 0-13.6 0c0 4.8 6.8 11.2 6.8 11.2zM12 7.6a2.4 2.4 0 1 0 .1 0'],
    [/NEXT STOP|DESTINATION|PLAN UNAVAILABLE|FLIGHT PLAN|ROUTE/, 'M3 12h13M12 6l6 6-6 6'],
    [/SHIP|WHAT IT IS FOR/, 'M12 2.6c2.7 2.7 4.2 6.4 4.2 10l-4.2 3.8-4.2-3.8c0-3.6 1.5-7.3 4.2-10zM12 9.6a1.6 1.6 0 1 0 .1 0'],
    [/RATE|EARNED|GOAL|AUEC|BOUGHT|SOLD|PRICED/, 'M12 3v18M8.5 7.4h6a2.5 2.5 0 0 1 0 5h-4.6a2.5 2.5 0 0 0 0 5h6.1'],
    [/SERVICES/, 'M14.6 6.2a4.2 4.2 0 0 0-5.7 5.2L3.6 16.7l2.9 2.9 5.3-5.3a4.2 4.2 0 0 0 5.2-5.7l-2.6 2.6-2.1-2.1z'],
    [/TASK|STOP|CROSS OFF|CONFIRM|ON YOUR LIST/, 'M4.5 5.5h15v13h-15zM8.5 12l2.5 2.5 4.5-5'],
    [/CONTRACT|OBJECTIVES|ISSUER|TAKEN|ALSO OPEN/, 'M6.5 3h7l4 4v14h-11zM13.5 3v4h4M9.5 12h5M9.5 16h5'],
    [/SESSION|ELAPSED|TAKEN/, 'M12 3a9 9 0 1 0 .1 0M12 7.2V12l3.4 2'],
    [/PILOT|CREW|FLOOR|DISBANDED|NOBODY/, 'M12 11.4a3.4 3.4 0 1 0 .1 0M5 20.4c0-3.2 3.1-5.4 7-5.4s7 2.2 7 5.4'],
    [/CARGO|LOAD|COUNTER|MANIFEST|SCU/, 'M3.5 8 12 4.2 20.5 8v8L12 19.8 3.5 16zM3.5 8l8.5 3.8L20.5 8M12 11.8v8'],
    [/CONTRACT DONE|BOUGHT|SOLD|FEED|NOTHING YET/, 'M4 18a13 13 0 0 1 13 0M5 13.5a9 9 0 0 1 12 0M5.5 9.2a14 14 0 0 1 13.5 0'],
    [/SCREEN|NO SCREENSHOT/, 'M3.5 5h17v11h-17zM9 20h6M12 16v4'],
    [/DEATH|INCAPACIT|THIS SESSION/, 'M12 3.4 21 19.4H3zM12 9.6v4.2M12 16.4v.4'],
    [/MINE|ROCK|RANKED|NEARBY/, 'M4 20.5 12 12.5M6.2 8.2c3-3 8.2-4.1 12.2-3-1 4-2.1 9.2-5.1 12.2M14.2 6.2l4 4'],
  ];
  const rowIcon = label => (glyphs.find(([test]) => test.test(String(label || '')))
    || [, 'M6.5 12h11'])[1];

  const caption = (id, compact) => {
    const command = commands.find(c => c.id === id);
    return command ? (compact && command.short) || command.caption : null;
  };

  /* What a confirmation was armed against, rather than where the cursor was.
     The plan is re-read every five seconds, so an index that meant "load 32 SCU"
     when the pilot armed it can mean "refuel" by the time they press again. */
  const taskId = task => task
    ? [task.tripId, task.stopId, task.actionId || task.kind].join('/') : null;
  const sameTask = (a, b) => taskId(a) !== null && taskId(a) === taskId(b);
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
    if (menuNodes[id] || id === 'home' || id === 'back') return { menu: id };
    if (/^menu-[1-5]$/.test(id || '')) return { slot: Number(id.slice(-1)) };
    return ({ prev: { cycle: -1 }, next: { cycle: 1 }, details: { details: true },
      up: { scroll: -1 }, down: { scroll: 1 },
      'text-up': { text: 1 }, 'text-down': { text: -1 },
      'bright-up': { brightness: 1 }, 'bright-down': { brightness: -1 },
      bright: { cycleBright: true },
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
      // "Not a fix" reads as a caveat about the position, which is not what is
      // being disclaimed: the position is as good as the logs get. It is the
      // line and its distance that are direct rather than flown.
      here ? 'direct line, not the route flown' : 'body not identified',
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
      /* Where you are going, then where you are. A nav page exists for the
         first of those and it had been sitting third - below the plan, under
         the map, off the bottom of a 480 px panel that left 53 pixels of
         readings. The distance rides on that same line rather than taking one
         of its own: it is a property of the destination, not a fact beside it. */
      case 'nav': {
        const legs = view.map?.legs?.length || 0, far = view.map?.gm;
        const reach = legs ? ` · ${far >= 100 ? far.toFixed(0) : far.toFixed(1)} Gm`
          + (legs > 1 ? ` over ${legs} legs` : '') : '';
        return [
          [s.travelling ? 'QUANTUM DESTINATION' : (briefingUnavailable ? 'PLAN UNAVAILABLE' : 'NEXT STOP'),
            s.travelling ? (s.travellingTo || 'Destination not identified') + reach
              : briefingUnavailable ? 'Open the dashboard to check your route.'
                : !briefing ? 'Loading your tracked plan…'
                  : stop?.place ? stop.place + reach : 'No outstanding stop in the tracked plan'],
          ['LOCATION', s.location || 'Location not yet identified'],
          ['SHIP', s.ship || 'No ship identified in the logs'],
          ['LOCATION SOURCE', s.location ? `${s.confidence || 'Unknown'} confidence · game logs` : 'Waiting for a location signal']
        ];
      }
      case 'task': {
        if (planMissing) return planMissing;
        if (!stop) return [['NO OUTSTANDING STOP', 'Track a flight plan in the dashboard to show the next task here.']];
        const next = (stop.actions || []).find(a => !a.done);
        return [['NEXT STOP', stop.place],
          ['NEXT ACTION', next ? describe(next) : stop.note || 'Travel to this stop'],
          ['PLAN', briefing.tripTitle || 'Tracked flight plan'],
        ];
      }
      case 'act': {
        if (planMissing) return planMissing;
        const list = tasks(briefing);
        if (!list.length) return [['NOTHING TO CONFIRM',
          'Track a flight plan in the dashboard to tick its work off from here.']];
        const selected = clamp(integer(view.selected, 0), 0, list.length - 1);
        // The confirmation is no longer the last row: it used to scroll out of
        // sight behind the very list it was asking about. It has a fixed strip
        // of its own now - see actionLine.
        return [
          ['STOP', list[0].place],
          ...list.map((task, i) => [
            task.kind === 'stop' ? 'CROSS OFF THE STOP' : `TASK ${i + 1} OF ${list.length}`,
            task.label, null,
            i !== selected ? null : view.armed ? 'armed' : 'cursor'])
        ];
      }
      case 'cargo': {
        const cargo = s.cargo, last = cargo?.last;
        return [
          ['PLANNED LOAD', briefingUnavailable ? 'Cannot read the flight plan.'
            : !briefing ? 'Loading your tracked plan…' : loadLine(plannedLoad(briefing))],
          ['LAST COUNTER', last
            ? [`${last.sell ? 'Sold' : 'Bought'} ${last.scu} SCU`, last.commodity, last.shop,
              `${Math.round(last.amount).toLocaleString()} aUEC`].filter(Boolean).join(' · ')
            : 'No commodity counter used in this session', last?.at],
          ['THIS SESSION', cargo
            ? `${cargo.boughtScu} SCU bought · ${cargo.soldScu} SCU sold`
            : 'Nothing bought or sold at a commodity counter yet'],
          // The dashboard's "Trade from here" card, folded in: it is a lead
          // about the counter you are standing at, which is this page's subject.
          ...(briefing?.trade?.length ? [['TRADE FROM HERE', briefing.trade.slice(0, 2)
            .map(t => `${t.commodity} · +${Math.round(t.marginPerScu).toLocaleString()}/SCU at ${t.sellTerminal}`)
            .join('  |  ')]] : []),
          ['NOT A MANIFEST', 'Counter receipts and your plan. Never the hold.']
        ];
      }
      case 'contract': {
        const open = s.contracts || [];
        if (!open.length) return [
          ['NO OPEN CONTRACT', 'Nothing accepted in this session that the logs have not since closed.'],
          ['SESSION ONLY', 'Earlier ones are in the logbook.']];
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
          ...(open.length > 1 ? [['ALSO OPEN', `${open.length - 1} more`]] : [])
        ];
      }
      /* Session, Handle, "This session" and "Wake up at" all fold in here: two
         counters and a regen hint do not each earn a page, and this one already
         answers "what state am I in, and can I trust it". */
      case 'status': {
        const wake = wakeUpAt(view.extra?.respawn);
        return [
          ['SESSION', s.inGame ? (s.gameRules || 'In game') : 'Menus / no active game session'],
          ['ELAPSED', elapsed(s.sessionStarted, view.now) || 'No session start recorded'],
          ['PILOT', s.handle || 'Waiting for pilot identity'],
          ['THIS SESSION', `${s.deaths || 0} death${s.deaths === 1 ? '' : 's'} · `
            + `${s.incapacitations || 0} incapacitation${s.incapacitations === 1 ? '' : 's'}`],
          ...(wake ? [['WAKE UP AT', wake.place, wake.at], ['HOW SURE', wake.why]] : []),
          ...(s.screen ? [['SCREEN READING', s.screen.summary, s.screen.shotAt]]
            : [['NO SCREENSHOT READING', 'Read a screenshot in the dashboard to add a cross-check.']]),
        ];
      }
      /* The live timeline, newest first. The one page that answers "what just
         happened", which is the question a pilot has after looking away. */
      case 'feed': {
        const feed = s.recentEvents || [];
        if (!feed.length) return [['NOTHING YET',
          'The log has said nothing this session. Entries appear here as the game writes them.']];
        return feed.slice(0, 8).map(entry => [
          String(entry.kind || 'event').replace(/-/g, ' ').toUpperCase(),
          [entry.text, entry.detail].filter(Boolean).join(' · '), entry.at]);
      }
      /* A floor and never a roster, exactly as the dashboard's Crew page has
         it: a party member who was already grouped up when you logged in and
         never dropped produces no toast at all, so absence means nothing. */
      case 'crew': {
        const party = s.party || [];
        // The one qualifier that survives on the frame: absence here means nothing,
        // and a pilot who reads this list as a roster is reading it wrong.
        const floor = ['A FLOOR', 'Absence means nothing.'];
        if (!party.length) return [['NOBODY NAMED', 'The party channel has not named anyone this session.'], floor];
        return [
          ...party.slice(0, 6).map(p => [String(p.handle).toUpperCase(), p.moment, p.at]),
          ...(s.partyDisbanded ? [['DISBANDED', 'The channel said the group broke up.']] : []),
          floor
        ];
      }
      case 'money': {
        const money = view.extra?.earnings;
        if (!money) return [['EARNINGS', 'Reading what the ledger recorded…']];
        const rate = money.basis === 'recent' ? money.window : money.lifetime;
        return [
          ['TRADING RATE', rate?.perHour > 0 ? `${aUEC(rate.perHour)} per hour`
            : 'Too little recorded flying time to state a rate'],
          ['FROM', money.basis === 'recent' ? `the last ${money.window.days} days` : 'every session on record'],
          ['EARNED', rate ? aUEC(rate.earned) : 'Nothing recorded'],
          ...(money.goal
            ? [['GOAL', `${money.goal.name || 'Saving up'} · ${aUEC(money.goal.target)}`],
               ['AT THIS RATE', money.hoursToGoal != null
                 ? hours(money.hoursToGoal) + ' of flying' : 'No rate to divide the goal by']]
            : [['NO GOAL SET', 'Set one in the dashboard and the flying time to reach it shows here.']]),
        ];
      }
      case 'list': {
        const jobs = view.extra?.jobs;
        if (!jobs) return [['SHOPPING LISTS', 'Reading your lists…']];
        const open = jobs.filter(job => !job.done);
        if (!open.length) return [['NO LIST IN HAND',
          'Make a shopping list in the dashboard and its progress shows here.']];
        return [
          ...open.slice(0, 4).map(job => [
            (job.pinned ? '★ ' : '') + String(job.title).toUpperCase(),
            [`${job.haveCount} of ${job.totalCount} seen`, job.destination].filter(Boolean).join(' · ')]),
        ];
      }
      /* What am I flying, what is it for, and what does losing it cost? All
         three come off the briefing the panel already had: focus and claim were
         being fetched every five seconds and thrown away. */
      case 'ship': {
        const focus = briefing?.focus, claim = briefing?.claim;
        return [
          ['SHIP', s.ship || 'No ship identified in the logs'],
          ...(focus ? [['WHAT IT IS FOR',
            [focus.label, focus.career, focus.role].filter(Boolean).join(' · ')]] : []),
          ...(claim
            ? [['CLAIM, PER THE TABLES', [
                claim.expeditedCost ? `${aUEC(claim.expeditedCost)} expedited` : null,
                claim.expeditedMinutes ? `${Math.round(claim.expeditedMinutes)} min` : null
              ].filter(Boolean).join(' · ') || 'The tables carry no figure for this hull'],
              ['OR WAIT', claim.standardMinutes
                ? `${Math.round(claim.standardMinutes)} min and no fee` : 'Not stated']]
            : [['CLAIM, PER THE TABLES', 'Nothing for this hull.']]),
        ];
      }
      /* The page for the moment after landing: what this place can do for you,
         what on your list it stocks, and what you left here last time. */
      case 'here': {
        if (planMissing) return planMissing;
        const services = briefing.services || [], shopping = briefing.shopping || [], stash = briefing.stash || [];
        return [
          ['PLACE', briefing.location || s.location || 'Not identified'],
          /* Only what is here. Naming all five with their statuses was five
             facts to say "none of them", and wrapped to four lines doing it. */
          ['SERVICES', !services.length ? 'Nothing the installed data can identify'
            : services.some(available)
              ? services.filter(available).map(v => v.name).join(' · ')
              : `None of ${services.length} listed here`],
          ...(shopping.length ? [['ON YOUR LIST, IN STOCK', shopping.slice(0, 3)
            .map(i => `${i.name} · ${i.needed} ${i.unit} · ${aUEC(i.price)}`).join('  |  ')]] : []),
          ...(stash.length ? [['SEEN HERE BEFORE',
            stash.slice(0, 4).map(i => i.name).join(' · '), stash[0].lastSeen]] : []),
        ];
      }
      case 'ledger': {
        const ledger = view.extra?.ledger;
        if (!ledger) return [['LEDGER', 'Reading what the logs priced…']];
        if (!ledger.length) return [['NOTHING PRICED',
          'No confirmed transaction in the last few days.']];
        return [
          ...ledger.slice(0, 4).map(entry => [
            String(entry.kind || 'entry').toUpperCase(),
            [entry.what, entry.amount != null
              ? `${entry.amount > 0 ? '+' : ''}${aUEC(entry.amount)}` : null,
              entry.where].filter(Boolean).join(' · '),
            entry.at]),
        ];
      }
      case 'mine': {
        if (planMissing) return planMissing;
        const mining = briefing.mining || [];
        if (!mining.length) return [['NOTHING RANKED', 'The deposit tables rank nothing here, or '
          + 'the community dataset is switched off.']];
        return [
          ...mining.slice(0, 4).map(place => [
            place.here ? String(place.place).toUpperCase()
              : `${String(place.place).toUpperCase()} · ${place.system || 'another system'}`,
            [`${aUEC(place.perRock)} a rock`, place.best].filter(Boolean).join(' · ')]),
          ...(mining.some(place => !place.here) ? [['NOT ALL NEARBY', 'Some are the best anywhere, not the best near you.']] : []),
        ];
      }
      case 'map': {
        const plan = view.map;
        if (!plan?.bodies) return [['MAP', plan?.note || 'No system to draw yet']];
        return [
          ['LOCATION', s.location || plan.here || 'Not identified'],
          ...(plan.next ? [['NEXT STOP', plan.next + (plan.gm
            ? ` · ${plan.gm >= 100 ? plan.gm.toFixed(0) : plan.gm.toFixed(1)} Gm` : '')]] : [])
        ];
      }
      default: return [];
    }
  }

  /* The fixed strip under the readings on Act: what a press will do, what it
     is asking, or what it just did. Pinned rather than appended to the list,
     because the row that asked "confirm this?" used to scroll away behind the
     list it was asking about. Null anywhere it would say nothing. */
  function actionLine(pageId, view) {
    if (pageId !== 'act') return null;
    if (view?.saved) return { state: 'saved', text: view.saved };
    const list = tasks(view?.briefing);
    if (!list.length) return null;
    const task = list[clamp(integer(view.selected, 0), 0, list.length - 1)];
    return view.armed
      ? { state: 'armed', text: `Confirm: ${task.label}?`,
          note: 'DONE again to mark it. Any other button cancels.' }
      : { state: 'ready', text: `DONE marks: ${task.label}`,
          note: 'Writes to your plan. Tells the game nothing.' };
  }

  /* "not listed" and "not reported" both open with "not", which is the only
     thing separating a service that is here from one nobody recorded.
     startsWith rather than a regex: an escape that does not survive being
     written by a script is an escape that silently matches nothing, which is
     exactly what happened to the word boundary that used to be here. */
  const available = service =>
    !String(service?.status || '').trim().toLowerCase().startsWith('not');

  const aUEC = n => `${Math.round(Number(n) || 0).toLocaleString()} aUEC`;

  /* Whole minutes. The clock is on a page that re-renders whenever anything
     changes, and a seconds field would rewrite it every second for no reader. */
  function elapsed(from, now) {
    if (!from) return null;
    const minutes = Math.floor(((now || Date.now()) - new Date(from).getTime()) / 60000);
    if (!Number.isFinite(minutes) || minutes < 0) return null;
    return minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
  }

  function hours(value) {
    const total = Number(value);
    if (!Number.isFinite(total) || total < 0) return 'Not a figure this can divide';
    return total < 1 ? `${Math.round(total * 60)} min` : `${total.toFixed(total < 10 ? 1 : 0)} h`;
  }

  /* Where the last death put you, or the bed you last used, whichever the logs
     saw more recently - which is as close as they get to a regen point. The
     game states one nowhere, so the page carries how thin the evidence is
     rather than presenting a place as a fact. */
  function wakeUpAt(respawn) {
    if (!respawn?.known) return null;
    const bed = respawn.bed;
    const bedIsNewer = bed && (!respawn.at || new Date(bed.at) > new Date(respawn.at));
    const place = bedIsNewer ? bed.place : (respawn.place || bed?.place);
    if (!place) return null;
    return { place, at: bedIsNewer ? bed.at : respawn.at,
      why: bedIsNewer
        ? `A medical bed used ${bed.times} times · the game never states a regen point`
        : `${respawn.agreeing} of ${respawn.of} deaths woke there · the game never states a regen point` };
  }
  return { fit, move, extent, action, effect, buttons, caption, icon, commands, defaults, mapView, routeLine, makerOf, makers, dormant, actionLine, sameTask, taskId, rowIcon,
    groups, menuNodes, menuNode, parent, title, validScreen, menuItems, previewPage, trail, resolveCommand, restoreScreen, readingView,
    pages, pageIds, rows, tasks, describe, plannedLoad, elapsed, wakeUpAt, clamp, dozed, DOZE, BRIGHTNESS, brightnessLevel, brightnessAt,
    cycleBrightness, stepBrightness, OSBS, BUTTONS };
})();
