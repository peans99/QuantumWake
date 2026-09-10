'use strict';
const mfdParams = new URLSearchParams(location.search);
const panelId = mfdParams.get('panel') === 'right' ? 'right' : 'left';
const mfdKey = 'qw-mfd-' + panelId;
let preferences = {};
try { preferences = JSON.parse(localStorage.getItem(mfdKey) || '{}') || {}; } catch { }
// The cockpit this was built for: where you are on the left, what to do next
// on the right. Only until the frame is used - after that it remembers.
let screen = QwMfd.restoreScreen(preferences, panelId === 'right' ? 'task' : 'nav');
let page = Math.max(0, QwMfd.pageIds.indexOf(screen));
let details = false, hasDetails = false;
// Which page of a paged screen is showing. Reset on navigate, like the Act
// cursor: coming back to the ledger should start at the newest entry, not
// wherever you had read to before going somewhere else.
let offset = 0;
/* Set in MFD setup and pushed from the host. A bound button can still nudge
   them - they stay in the vocabulary - but the nudge lasts until the panel
   reloads, because setup is where a setting is kept. */
let brightness = 1, textScale = 1;
/* Dim after a spell with nothing pressed, if the pilot asked for it. Off
   unless set: these are LED panels, so this is for a dark cockpit rather
   than for protecting anything. It dims and never blanks - a frame you can
   still glance at beats one that has to be woken before it will answer. */
let sleepAfterMinutes = 0, lastPress = Date.now(), dozing = false;
let bindings = QwMfd.buttons(null);
/* Body coordinates and the place gazetteer. Fetched once and kept: it is
   reference data that only changes when the game does, and re-pulling 294
   places every five seconds to draw a plan that has not moved would be waste
   on a panel that is meant to be left running. */
let atlas = null;
/* Community code-to-name, so a ship that arrives as "Drake Corsair" finds the
   same badge as one that arrives as "DRAK Corsair". Absent is fine: the
   sixteen built-in names already cover the fleet anyone flies. */
let makerNames = null;
/* Earnings, lists and the regen hint: three pages worth of answers that change
   slowly, so they ride a 30-second timer rather than the five-second briefing
   poll. A panel left running all evening should not be asking the ledger for a
   rate twelve times a minute. */
let extra = {};
let state = null;
let briefing = null, briefingUnavailable = false, briefingBusy = false;
let selected = 0, armed = false;
/* What the arming press was actually pointing at, and whether a write is in
   flight. An index is not an identity: the plan is re-read every five seconds,
   so the line under the cursor when DONE was armed may not be the line under it
   when DONE is pressed again. */
let armedTask = null, confirming = false, saved = '';
/* Below this the edge captions have room for about six characters and the map
   is taking space the readings need. Measured, not guessed. */
let compact = false;
let lastRows = '';
let stream;
const byId = id => document.getElementById(id);
/* Long explanations belong in the dashboard. The frame keeps the operational
   result; status rows retain their meaning through a labelled icon. */
const hudText = value => {
  const terseLoad = /^(.+) across (\d+) stop(s?) · planned, not detected$/.exec(value || '');
  if (terseLoad) return `${terseLoad[1]} · ${terseLoad[2]} stop${terseLoad[3]}`;
  return ({
  'Cannot read the flight plan. Check the dashboard connection.': 'Plan offline',
  'Open the dashboard to check your route.': 'Route unavailable',
  'Loading your tracked plan…': 'Loading plan…',
  'No outstanding stop in the tracked plan': 'No planned stop',
  'Location not yet identified': 'No location',
  'No ship identified in the logs': 'No ship',
  'Waiting for a location signal': 'Waiting for signal',
  'Track a flight plan in the dashboard to show the next task here.': 'No tracked plan',
  'Track a flight plan in the dashboard to tick its work off from here.': 'No tracked plan',
  'No commodity counter used in this session': 'No counter use',
  'Nothing bought or sold at a commodity counter yet': 'No counter activity',
  'Counter receipts and your plan. Never the hold.': 'Plan + counters · not a hold',
  'Nothing accepted in this session that the logs have not since closed.': 'No active contract',
  'Earlier ones are in the logbook.': 'Earlier: logbook',
  'The log has said nothing this session. Entries appear here as the game writes them.': 'Waiting for events',
  'The party channel has not named anyone this session.': 'No crew named',
  'Absence means nothing.': 'Not a roster',
  'Too little recorded flying time to state a rate': 'Need more flight time',
  'Set one in the dashboard and the flying time to reach it shows here.': 'Set a goal in dashboard',
  'Nothing for this hull.': 'No table entry',
  'Nothing the installed data can identify here': 'No listed services',
  'Reading what the logs priced…': 'Reading ledger…',
  'No confirmed transaction in the last few days.': 'No recent transactions'
  }[value] || value);
};
const iconOnlyLabels = new Set(['LOCATION SOURCE', 'NOT A MANIFEST', 'SESSION ONLY', 'HOW SURE', 'A FLOOR', 'NOT ALL NEARBY', 'FROM']);
/* Which frame this is and which Cougar drives it, in one line at the top. The
   footer used to carry the device and a BTN-nn readout of the last press; the
   press is under the pilot's own thumb, so the readout was a label for
   something they had just done, costing a strip of a 480 px panel to say it. */
let cougar = null, usb = false, connection = 'CONNECTING';
function drawIdentity() {
  byId('identity').textContent = cougar
    ? `${panelId.toUpperCase()} · MFD ${cougar}` : panelId.toUpperCase() + ' MFD';
  byId('connection').textContent = connection + (cougar && !usb ? ' · NO USB' : '');
}
drawIdentity();
// The frame's own numbering. The four rockers carry no printed button, so they
// are input only: setup names them, and this face has nothing to draw for them.
const edges = { top: [1, 2, 3, 4, 5], right: [6, 7, 8, 9, 10], bottom: [15, 14, 13, 12, 11], left: [20, 19, 18, 17, 16] };
const osbs = Object.values(edges).flat();
const face = {};
for (const [edge, numbers] of Object.entries(edges)) {
  for (const number of numbers) {
    const button = document.createElement('button');
    button.id = 'osb-' + number;
    const glyph = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    glyph.setAttribute('viewBox', '0 0 24 24');
    const shape = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    glyph.append(shape);
    const text = document.createElement('b');
    button.append(glyph, text);
    button.onclick = () => press(number);
    byId(edge).append(button);
    face[number] = { button, glyph, path: shape, text };
  }
}
/* An icon over the caption, and nothing whatever over an unassigned button.
   The number used to sit there, which was a label for a thing the pilot is
   looking straight at - the button is under their thumb, printed on the frame.
   A blank position reads as blank, the way a real MFD's does; the number is
   still on the hover title, and the setup tester is where numbers matter. */
function drawLabels() {
  for (const number of osbs) {
    // The pieces were made here, so they are held rather than looked up again
    // on every draw - twenty buttons times three queries, several times a press.
    const { button, glyph, path: shape, text } = face[number];
    const command = QwMfd.resolveCommand(bindings[number], screen);
    const inactive = (command === 'confirm' && screen !== 'act') || (command === 'details' && !hasDetails);
    // The cycling button carries the level it is on, or pressing it would be
    // the only control on the frame with no idea where it had got to.
    const caption = inactive ? null
      : command === 'details' && details ? 'LESS'
      : command === 'bright' ? 'BRT ' + QwMfd.brightnessLevel(brightness)
      : QwMfd.caption(command, compact);
    const path = caption ? QwMfd.icon(command) : null;
    text.textContent = caption || '';
    shape.setAttribute('d', path || '');
    glyph.style.visibility = path ? '' : 'hidden';
    button.disabled = !caption;
    button.title = 'Cougar button ' + number + (caption ? ': ' + caption : ': unassigned');
    button.setAttribute('aria-label', caption ? `${QwMfd.title(command) === 'Cockpit' ? caption : QwMfd.title(command)} · Cougar button ${number}` : 'Unused button ' + number);
    button.classList.toggle('selected', command === screen);
    button.setAttribute('aria-pressed', String(command === screen));
  }
}
function savePreferences() {
  try { localStorage.setItem(mfdKey, JSON.stringify({ screen })); } catch { }
}
const isMenu = () => !QwMfd.pageIds.includes(screen);
function navigate(target) {
  if (!QwMfd.validScreen(target)) return;
  standDown(); notice = ''; saved = ''; details = false; selected = 0; offset = 0;
  screen = target; page = Math.max(0, QwMfd.pageIds.indexOf(screen));
  byId('content').scrollTop = 0;
  savePreferences(); render();
}
function back() {
  if (details) { details = false; lastRows = ''; byId('content').scrollTop = 0; render(); }
  else navigate(QwMfd.parent(screen));
}
function toggleDetails() {
  if (!hasDetails || isMenu()) return;
  standDown(); notice = ''; details = !details; byId('content').scrollTop = 0; render();
}
byId('more').onclick = toggleDetails;
window.addEventListener('keydown', event => {
  if (event.key === 'Escape' || event.key === 'Backspace') { event.preventDefault(); back(); }
});
let lastMenu = '';
function drawBreadcrumb() {
  const breadcrumb = byId('breadcrumb');
  breadcrumb.replaceChildren();
  const path = QwMfd.trail(screen);
  path.forEach((id, index) => {
    if (index) {
      const divider = document.createElement('span'); divider.textContent = '/';
      divider.setAttribute('aria-hidden', 'true'); breadcrumb.append(divider);
    }
    const crumb = document.createElement('button'); crumb.type = 'button'; crumb.textContent = QwMfd.title(id);
    crumb.disabled = id === screen; crumb.onclick = () => navigate(id);
    breadcrumb.append(crumb);
  });
}
function drawMenu(plan) {
  const menu = byId('menu'); menu.hidden = !isMenu();
  if (menu.hidden) return;
  const items = QwMfd.menuItems(screen).map(id => {
    const node = QwMfd.menuNode(id);
    const preview = QwMfd.previewPage(id);
    const row = !preview ? null : QwMfd.rows(QwMfd.pageIds.indexOf(preview), state, briefing, briefingUnavailable,
      { selected: 0, extra, map: plan, now: Date.now() })[0];
    return { id, title: QwMfd.title(id), hint: node?.hint || row?.[0] || '', summary: row?.[1] || 'Not recorded', branch: !!node };
  });
  const key = JSON.stringify([screen, items, bindings]);
  if (key === lastMenu) return;
  lastMenu = key; menu.replaceChildren();
  for (const item of items) {
    const tile = document.createElement('button'); tile.type = 'button'; tile.className = 'menu-tile';
    tile.dataset.screen = item.id; tile.onclick = () => navigate(item.id);
    const icon = document.createElementNS(svgns, 'svg'); icon.setAttribute('viewBox', '0 0 24 24'); icon.setAttribute('aria-hidden', 'true');
    const path = document.createElementNS(svgns, 'path'); path.setAttribute('d', QwMfd.icon(item.id)); icon.append(path);
    const name = document.createElement('strong'); name.textContent = item.title;
    const hint = document.createElement('span'); hint.className = 'tile-hint'; hint.textContent = item.hint;
    const summary = document.createElement('span'); summary.className = 'tile-summary'; summary.textContent = item.summary;
    const number = osbs.find(n => QwMfd.resolveCommand(bindings[n], screen) === item.id);
    const keycap = document.createElement('small'); keycap.textContent = number ? String(number).padStart(2, '0') : item.branch ? '›' : '•';
    tile.title = item.title + ' · ' + item.summary; tile.setAttribute('aria-label', tile.title);
    tile.append(icon, keycap, name, hint, summary); menu.append(tile);
  }
}
function render() {
  // Recomputed here so the frame can never be left showing a dimming that
  // has since expired; the timer below only decides when to ask again.
  dozing = QwMfd.dozed(sleepAfterMinutes, Date.now() - lastPress);
  byId('mfd').style.filter = `brightness(${dozing ? brightness * QwMfd.DOZE : brightness})`;
  byId('mfd').style.setProperty('--text-scale', textScale);
  const where = QwMfd.pageLabel(screen, { extra, offset });
  byId('title').textContent = QwMfd.title(screen)
    + (details ? ' · details' : '') + (where ? ' · ' + where : '');
  const showing = screen;
  byId('mfd').dataset.screen = screen;
  byId('mfd').classList.toggle('menu-open', isMenu());
  byId('mfd').classList.toggle('menu-dense', textScale > 1.2 || window.innerWidth > window.innerHeight * 1.2);
  drawBreadcrumb();
  // One view model, drawn as a plan and quoted as a row: the picture and the
  // number must not be able to disagree about where you are going. Built once
  // and handed on - the menu was building a fresh one per tile, so Home cost
  // six of these a second on a panel meant to be left running all evening.
  const plan = QwMfd.mapView(atlas, state, briefing);
  drawMenu(plan);
  drawMap(showing === 'map', plan, true);
  drawMaker();
  const view = QwMfd.readingView(screen, isMenu() ? [] : QwMfd.rows(page, state, briefing, briefingUnavailable,
    { selected, armed, map: plan, extra, offset, now: Date.now() }), details);
  hasDetails = view.more;
  const rows = view.rows;
  const key = JSON.stringify([screen, details, rows]);
  const readings = byId('readings');
  const content = byId('content');
  content.hidden = isMenu();
  readings.hidden = isMenu();
  byId('more').hidden = !hasDetails;
  byId('more').textContent = details ? '← Overview' : 'More details →';
  drawLabels();
  if (key !== lastRows) {
    lastRows = key;
    const scroll = content.scrollTop;
    readings.replaceChildren();
    for (const [index, [label, value, at, mark]] of rows.entries()) {
      const row = document.createElement('article'); row.className = 'reading';
      if (iconOnlyLabels.has(label)) row.classList.add('status');
      if (index === 0 && !details && !['act', 'feed', 'crew', 'list', 'ledger', 'mine', 'map'].includes(screen)) row.classList.add('hero');
      if (mark) row.classList.add(mark);
      const glyph = document.createElementNS(svgns, 'svg');
      glyph.setAttribute('viewBox', '0 0 24 24');
      const shape = document.createElementNS(svgns, 'path');
      shape.setAttribute('d', QwMfd.rowIcon(label));
      glyph.append(shape);
      glyph.setAttribute('role', 'img'); glyph.setAttribute('aria-label', label);
      const heading = document.createElement('h2'); heading.textContent = label;
      const body = document.createElement('div'); body.className = 'value';
      const text = document.createElement('p'); text.textContent = hudText(value || 'Not recorded');
      body.append(text);
      if (at) { const time = document.createElement('time'); time.textContent = new Date(at).toLocaleString(); body.append(time); }
      row.append(glyph, heading, body);
      readings.append(row);
    }
    content.scrollTop = scroll;
    // Instant, not smooth: a running scroll animation is one of the things that
    // keeps a headless render from ever settling, and this fires on every press.
    readings.querySelector('.cursor, .armed')?.scrollIntoView({ block: 'nearest' });
  }
  /* Whether the panel can scroll is a question only the laid-out page can
     answer, so it is measured here and handed to the rule rather than guessed
     at from a row count. Outside the redraw, because an unchanged page still
     needs its face marked after a page change. */
  drawAction(QwMfd.actionLine(showing, { briefing, selected, armed, saved }));
  updateScrollHint();
  const idle = idleNow(content.scrollHeight > content.clientHeight + 1);
  for (const number of osbs)
    face[number].button.classList.toggle('dormant', idle.includes(QwMfd.resolveCommand(bindings[number], screen)));
}
function updateScrollHint() {
  const r = byId('content'), hint = byId('scroll-hint');
  hint.hidden = isMenu() || r.scrollHeight <= r.clientHeight + 1;
  hint.textContent = r.scrollTop + r.clientHeight < r.scrollHeight - 2 ? '↓ More below' : '↑ More above';
}
byId('content').addEventListener('scroll', updateScrollHint);
/* Pinned under the readings rather than appended to them. The row that asked
   "confirm this?" used to sit at the end of the task list and scroll away
   behind the very list it was asking about. */
function drawAction(line) {
  const strip = byId('action');
  // A message about the press that just happened outranks the standing prompt,
  // and off Act it is the only thing the strip has to say. Cleared by the next
  // press, so it never outlives the thing it is reporting.
  const showing = notice ? { state: 'note', text: notice } : line;
  strip.hidden = !showing;
  if (!showing) return;
  strip.className = showing.state;
  byId('action-text').textContent = showing.text;
  byId('action-note').textContent = showing.note || '';
}
// Re-measure after placement changes; the host resizes windows without a reload.
function measure() {
  const small = window.innerWidth < 320 || window.innerHeight < 320;
  compact = small;
  byId('mfd').classList.toggle('compact', compact);
  drawLabels(); lastMap = ''; lastRows = ''; render();
}
window.addEventListener('resize', measure);
/* The maker's mark for the ship the logs last saw retrieved, in the corner of
   every page. It stays where it is rather than moving with the page, because a
   badge that comes and goes is one more thing changing in the corner of a
   pilot's eye. A maker with no logo file, or no ship at all, shows nothing -
   the ship is named in words on Nav either way. */
let lastMaker = '';
function drawMaker() {
  const badge = byId('maker');
  const maker = QwMfd.makerOf(state?.ship, makerNames);
  const key = maker?.code || '';
  if (key === lastMaker) return;
  lastMaker = key;
  badge.hidden = !key;
  if (!key) return;
  badge.src = 'assets/manufacturers/' + key + '.png';
  badge.alt = maker.name;
  badge.title = state.ship;
  // A file that is not there must not leave a broken-image glyph on a HUD.
  badge.onerror = () => { badge.hidden = true; };
}
// The map and the navigation distance share the same body-centre geometry.
const svgns = 'http://www.w3.org/2000/svg';
let lastMap = '';
function drawMap(visible, view, big) {
  const figure = byId('map');
  figure.hidden = !visible;
  figure.classList.toggle('big', !!big);
  if (!visible) return;
  const key = JSON.stringify(view);
  if (key === lastMap) return;
  lastMap = key;
  byId('map-note').textContent = view.note || '';
  const plot = byId('map-plot');
  plot.replaceChildren();
  if (!view.bodies) return;
  const add = (name, attrs, className) => {
    const node = document.createElementNS(svgns, name);
    for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value);
    if (className) node.setAttribute('class', className);
    plot.append(node);
    return node;
  };
  for (const radius of view.rings) add('circle', { cx: 0, cy: 0, r: radius }, 'orbit');
  // The whole plan, leg by leg, not just the next hop.
  const at = name => view.bodies.find(b => b.name === name);
  for (const leg of view.legs) {
    const a = at(leg.from), b = at(leg.to);
    if (a && b) add('line', { x1: a.x, y1: a.y, x2: b.x, y2: b.y }, 'leg');
  }
  add('circle', { cx: 0, cy: 0, r: .05 }, 'star');
  for (const body of view.bodies) {
    add('circle', { cx: body.x, cy: body.y, r: body.here || body.onRoute ? .06 : .035 },
      body.here ? 'body here' : body.next ? 'body target' : body.onRoute ? 'body stop' : 'body');
    if (body.here) add('circle', { cx: body.x, cy: body.y, r: .13 }, 'halo');
  }
  // Only the two that matter carry a name; a 150 px plan cannot hold sixteen.
  // The rest of the route is named in the Distance row instead.
  for (const body of [at(view.here), at(view.next)]) {
    if (!body) continue;
    const label = add('text', { x: body.x, y: body.y - .19 }, body.here ? 'name here' : 'name target');
    label.textContent = body.name;
  }
}
/* What just happened, in the strip rather than a footer of its own. It clears
   on the next render that has something else to say, so a message about a press
   never outlives the press. */
let notice = '';
function note(text) { notice = text; drawAction(QwMfd.actionLine(screen, { briefing, selected, armed, saved })); }
/* One rule for what is idle, asked by the face when it dims a button and by the
   input before it acts on one. */
function idleNow(scrollable) {
  return [...QwMfd.dormant(screen, { tasks: QwMfd.tasks(briefing).length, scrollable: !isMenu() && scrollable,
      paged: QwMfd.pageStep(screen, { extra, offset }, 1) !== null && QwMfd.pageLabel(screen, { extra, offset }) !== null }),
    ...(!hasDetails ? ['details'] : []), ...(screen === 'home' ? ['back'] : []),
    ...(briefingUnavailable || confirming ? ['confirm'] : [])];
}
function isIdle(commandId) {
  const readings = byId('content');
  return idleNow(readings.scrollHeight > readings.clientHeight + 1).includes(commandId);
}
function standDown() { armed = false; armedTask = null; lastRows = ''; }
function press(number) {
  // Any press is the pilot being there: it wakes the frame before anything
  // else, so the first press after a doze is not spent on turning the light
  // back on.
  lastPress = Date.now();
  if (dozing) { dozing = false; render(); }
  // Each press starts clean, so a message only ever describes this one.
  notice = '';
  const button = face[number]?.button;
  if (button) { button.classList.add('pressed'); setTimeout(() => button.classList.remove('pressed'), 180); }
  if (!Number.isInteger(number) || number < 1 || number > QwMfd.BUTTONS) return;
  const command = QwMfd.resolveCommand(bindings[number], screen);
  const action = QwMfd.effect(command);
  if (!action) return;
  /* A dimmed button does nothing. Dimming and refusing were two rules and only
     the face's ran, so DONE looked dead on Nav and still marked a task off -
     with the confirmation drawn on a page nobody was looking at. */
  if (isIdle(command)) return;
  // Anything but DONE stands a live confirmation down. A pilot reaching for
  // another page must not leave one armed behind them.
  if (!action.confirm && armed) standDown();
  if (action.confirm) { confirmSelected(); return; }
  if (action.menu) { action.menu === 'back' ? back() : navigate(action.menu); return; }
  if (action.details) { toggleDetails(); return; }
  if (action.page !== undefined || action.cycle) {
    const siblings = QwMfd.menuItems(screen);
    const target = action.page !== undefined ? QwMfd.pageIds[action.page]
      : siblings[(siblings.indexOf(screen) + action.cycle + siblings.length) % siblings.length];
    navigate(target); return;
  }
  if (action.text) textScale = QwMfd.clamp(textScale + action.text * .1, .8, 1.5);
  if (action.brightness) brightness = QwMfd.stepBrightness(brightness, action.brightness);
  if (action.cycleBright) brightness = QwMfd.cycleBrightness(brightness);
  if (action.scroll) step(action.scroll);
  savePreferences(); render();
}
/* Up and down move the cursor on Act, which has something to point at, and
   scroll the panel everywhere else. One pair of buttons either way: a cockpit
   frame has no spare ones, and a rocker nobody can name yet is not a plan. */
function step(direction) {
  // A paged screen steps a page; everything else scrolls. Asked of one rule
  // rather than tested against a list of screen names here.
  const paged = QwMfd.pageStep(screen, { extra, offset }, direction);
  if (paged !== null) {
    if (paged === offset) { note(direction > 0 ? 'End of the ledger' : 'Newest entries'); return; }
    offset = paged; lastRows = '';
    return;
  }
  if (screen !== 'act') {
    byId('content').scrollBy({ top: direction * byId('content').clientHeight * .7, behavior: 'smooth' });
    return;
  }
  const count = QwMfd.tasks(briefing).length;
  if (!count) return;
  selected = (selected + direction + count) % count;
  lastRows = '';
}
/* Press once to arm, again to commit. A single press that ticks work off is a
   glove-width away from a page button, and the plan is the pilot's own record. */
async function confirmSelected() {
  // One write at a time. Two quick presses used to be two POSTs, and a toggle
  // sent twice puts the line back exactly where it started.
  if (confirming || screen !== 'act' || briefingUnavailable) return;
  const list = QwMfd.tasks(briefing);
  const target = list[QwMfd.clamp(selected, 0, list.length - 1)];
  if (!target?.tripId) { note('Nothing to confirm here'); render(); return; }

  if (!armed) { armed = true; armedTask = target; saved = ''; lastRows = ''; render(); return; }

  /* The plan is re-read every five seconds. If it moved under the pilot between
     arming and confirming, the line their thumb was pointing at is not the line
     the cursor is on any more - so the press stands down rather than ticking
     off whatever happens to be there now. */
  if (!QwMfd.sameTask(armedTask, target)) {
    standDown();
    note('The plan changed. Nothing was marked.');
    render();
    return;
  }

  const marking = target.label;
  standDown();
  confirming = true;
  render();
  const path = target.kind === 'stop'
    ? `stops/${encodeURIComponent(target.stopId)}/toggle`
    : `stops/${encodeURIComponent(target.stopId)}/actions/${encodeURIComponent(target.actionId)}/toggle`;
  try {
    const response = await fetch(`/api/trips/${encodeURIComponent(target.tripId)}/${path}`,
      { method: 'POST', signal: AbortSignal.timeout(8000) });
    if (!response.ok) throw new Error('Not saved');
    saved = 'Marked done: ' + marking;
    selected = 0;
    await refreshBriefing();
  } catch {
    note('Not saved. Check the dashboard.');
    saved = 'Not saved: ' + marking;
  } finally { confirming = false; lastRows = ''; render(); }
}
window.chrome?.webview?.addEventListener('message', ({ data }) => {
  if (data.type === 'button') press(data.button);
  if (data.type === 'display') {
    bindings = QwMfd.buttons(data.buttons);
    if (Number.isFinite(data.brightness)) brightness = QwMfd.brightnessAt(QwMfd.brightnessLevel(data.brightness));
    if (Number.isFinite(data.textScale)) textScale = QwMfd.clamp(data.textScale, .8, 1.5);
    if (Number.isFinite(data.sleepAfterMinutes)) {
      sleepAfterMinutes = QwMfd.clamp(data.sleepAfterMinutes, 0, 120);
      lastPress = Date.now(); dozing = false;
    }
    drawLabels(); lastRows = ''; render();
  }
  if (data.type === 'device') { cougar = data.cougar; usb = !!data.connected; drawIdentity(); }
  if (data.type === 'alignment') {
    byId('alignment').hidden = !data.enabled;
    byId('alignment-name').textContent = panelId.toUpperCase() + ' MFD';
  }
});
async function refreshBriefing() {
  if (briefingBusy) return;
  briefingBusy = true;
  try {
    const response = await fetch('/api/briefing', { signal: AbortSignal.timeout(8000) });
    if (!response.ok) throw new Error('Plan unavailable');
    briefing = await response.json(); briefingUnavailable = false;
  } catch { briefingUnavailable = true; }
  finally { briefingBusy = false; render(); }
}
/* Failing quietly is the right answer: the Nav rows carry the location in
   words either way, and a panel that loses its plan should not lose its
   readings with it. mapView says what it is missing. */
async function loadAtlas() {
  try {
    const response = await fetch('/api/map', { signal: AbortSignal.timeout(15000) });
    if (!response.ok) throw new Error('No atlas');
    atlas = await response.json();
  } catch { atlas = { nodes: [], positions: {} }; }
  finally { lastMap = ''; render(); }
}
/* Each one is allowed to fail on its own: a page whose figure is missing says
   so, and must not take the other two down with it. */
async function refreshExtras() {
  const grab = async (url, key) => {
    try {
      const response = await fetch(url, { signal: AbortSignal.timeout(15000) });
      if (response.ok) extra = { ...extra, [key]: await response.json() };
    } catch { /* the page it feeds says what it is missing */ }
  };
  await Promise.all([grab('/api/earnings?days=30', 'earnings'), grab('/api/jobs', 'jobs'),
    grab('/api/respawn', 'respawn'), grab('/api/ledger?days=30', 'ledger'),
    grab('/api/screen/readings?take=8', 'screenLog')]);
  lastRows = ''; render();
}
async function loadMakers() {
  try {
    const response = await fetch('/api/manufacturers', { signal: AbortSignal.timeout(15000) });
    if (response.ok) makerNames = await response.json();
  } catch { /* the built-in sixteen stand on their own */ }
  finally { lastMaker = ''; render(); }
}
async function bootMfd() {
  measure();
  drawLabels();
  render();
  refreshBriefing();
  loadAtlas();
  loadMakers();
  refreshExtras();
  if (mfdParams.has('snapshot')) {
    try {
      const response = await fetch('/api/now');
      if (!response.ok) throw new Error('No snapshot');
      state = await response.json();
      connection = 'SNAPSHOT'; drawIdentity(); render();
    } catch { connection = 'SERVER UNAVAILABLE'; drawIdentity(); }
    return;
  }
  stream = new EventSource('/api/stream');
  const briefingTimer = setInterval(refreshBriefing, 5000);
  const extraTimer = setInterval(refreshExtras, 30000);
  /* Its own timer rather than a check inside render: the panel re-renders
     whenever anything changes, and a frame nobody is touching is exactly the
     case where nothing is changing. */
  const dozeTimer = setInterval(() => {
    if (dozing !== QwMfd.dozed(sleepAfterMinutes, Date.now() - lastPress)) render();
  }, 15000);
  stream.onmessage = event => {
    try {
      state = JSON.parse(event.data);
      connection = state.connected ? (state.inGame ? 'LIVE LOG' : 'GAME MENUS') : 'WAITING FOR GAME';
      drawIdentity();
      render();
    } catch { connection = 'INVALID UPDATE'; drawIdentity(); }
  };
  stream.onerror = () => { connection = state ? 'DISCONNECTED · LAST DATA' : 'SERVER UNAVAILABLE'; drawIdentity(); };
  window.addEventListener('beforeunload', () => {
    stream.close(); clearInterval(briefingTimer); clearInterval(extraTimer); clearInterval(dozeTimer);
  });
}
bootMfd();
