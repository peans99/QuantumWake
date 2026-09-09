'use strict';
const mfdParams = new URLSearchParams(location.search);
const panelId = mfdParams.get('panel') === 'right' ? 'right' : 'left';
const mfdKey = 'qw-mfd-' + panelId;
let preferences = {};
try { preferences = JSON.parse(localStorage.getItem(mfdKey) || '{}') || {}; } catch { }
let page = Number.isInteger(preferences.page) ? QwMfd.clamp(preferences.page, 0, QwMfd.pages.length - 1) : (panelId === 'right' ? 1 : 0);
let brightness = Number.isFinite(preferences.brightness) ? QwMfd.clamp(preferences.brightness, .3, 1) : 1;
let textScale = Number.isFinite(preferences.textScale) ? QwMfd.clamp(preferences.textScale, .8, 1.5) : 1;
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
let state = null;
let briefing = null, briefingUnavailable = false, briefingBusy = false;
let selected = 0, armed = false;
let lastRows = '';
let stream;
const byId = id => document.getElementById(id);
byId('identity').textContent = panelId.toUpperCase() + ' MFD';
// The frame's own numbering. The four rockers carry no printed button, so they
// are input only: setup names them, and this face has nothing to draw for them.
const edges = { top: [1, 2, 3, 4, 5], right: [6, 7, 8, 9, 10], bottom: [15, 14, 13, 12, 11], left: [20, 19, 18, 17, 16] };
const osbs = Object.values(edges).flat();
for (const [edge, numbers] of Object.entries(edges)) {
  for (const number of numbers) {
    const button = document.createElement('button');
    button.id = 'osb-' + number;
    const glyph = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    glyph.setAttribute('viewBox', '0 0 24 24');
    glyph.append(document.createElementNS('http://www.w3.org/2000/svg', 'path'));
    button.append(glyph, document.createElement('b'));
    button.onclick = () => press(number);
    byId(edge).append(button);
  }
}
/* An icon over the caption, and nothing whatever over an unassigned button.
   The number used to sit there, which was a label for a thing the pilot is
   looking straight at - the button is under their thumb, printed on the frame.
   A blank position reads as blank, the way a real MFD's does; the number is
   still on the hover title, and the setup tester is where numbers matter. */
function drawLabels() {
  for (const number of osbs) {
    const button = byId('osb-' + number);
    const caption = QwMfd.caption(bindings[number]);
    const path = QwMfd.icon(bindings[number]);
    button.querySelector('b').textContent = caption || '';
    button.querySelector('path').setAttribute('d', path || '');
    button.querySelector('svg').style.visibility = path ? '' : 'hidden';
    button.disabled = !caption;
    button.title = 'Cougar button ' + number + (caption ? ': ' + caption : ': unassigned');
  }
}
function savePreferences() {
  try { localStorage.setItem(mfdKey, JSON.stringify({ page, brightness, textScale })); } catch { }
}
function render() {
  byId('mfd').style.filter = `brightness(${brightness})`;
  byId('mfd').style.setProperty('--text-scale', textScale);
  byId('title').textContent = QwMfd.pages[page];
  const showing = QwMfd.pageIds[page];
  for (const number of osbs) byId('osb-' + number).classList.toggle('selected', bindings[number] === showing);
  drawMap(showing === 'nav');
  drawMaker();
  const rows = QwMfd.rows(page, state, briefing, briefingUnavailable, { selected, armed });
  const key = JSON.stringify(rows);
  if (key === lastRows) return;
  lastRows = key;
  const readings = byId('readings');
  const scroll = readings.scrollTop;
  readings.replaceChildren();
  for (const [label, value, at, mark] of rows) {
    const row = document.createElement('article'); row.className = 'reading';
    if (mark) row.classList.add(mark);
    const heading = document.createElement('h2'); heading.textContent = label;
    const text = document.createElement('p'); text.textContent = value || 'Not recorded';
    row.append(heading, text);
    if (at) { const time = document.createElement('time'); time.textContent = new Date(at).toLocaleString(); row.append(time); }
    readings.append(row);
  }
  readings.scrollTop = scroll;
  // Instant, not smooth: a running scroll animation is one of the things that
  // keeps a headless render from ever settling, and this fires on every press.
  readings.querySelector('.cursor, .armed')?.scrollIntoView({ block: 'nearest' });
}
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
/* The system plan on the Nav page, kept deliberately small: it is there to say
   which way round the system you are, beside the words that say where. The
   readings are the answer; this is the shape of it. */
const svgns = 'http://www.w3.org/2000/svg';
let lastMap = '';
function drawMap(visible) {
  byId('map').hidden = !visible;
  if (!visible) return;
  const view = QwMfd.mapView(atlas, state, briefing);
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
  const from = view.bodies.find(b => b.here), to = view.bodies.find(b => b.target);
  // The leg the ship is actually flying, when both ends are known.
  if (from && to) add('line', { x1: from.x, y1: from.y, x2: to.x, y2: to.y }, 'leg');
  add('circle', { cx: 0, cy: 0, r: .05 }, 'star');
  for (const body of view.bodies) {
    add('circle', { cx: body.x, cy: body.y, r: body.here || body.target ? .06 : .035 },
      body.here ? 'body here' : body.target ? 'body target' : 'body');
    if (body.here) add('circle', { cx: body.x, cy: body.y, r: .13 }, 'halo');
  }
  // Only the two that matter carry a name; a 150 px plan cannot hold sixteen.
  for (const body of [from, to]) {
    if (!body) continue;
    const label = add('text', { x: body.x, y: body.y - .19 }, body.here ? 'name here' : 'name target');
    label.textContent = body.name;
  }
}
function note(text) { byId('last-input').textContent = text; }
function press(number) {
  note('BTN ' + String(number).padStart(2, '0'));
  const button = byId('osb-' + number);
  if (button) { button.classList.add('pressed'); setTimeout(() => button.classList.remove('pressed'), 180); }
  const action = QwMfd.action(number, bindings);
  if (!action) return;
  // Anything but DONE stands a live confirmation down. A pilot reaching for
  // another page must not leave one armed behind them.
  if (!action.confirm && armed) { armed = false; lastRows = ''; }
  if (action.confirm) { confirmSelected(); return; }
  if (action.page !== undefined || action.cycle) {
    page = action.page ?? (page + action.cycle + QwMfd.pages.length) % QwMfd.pages.length;
    selected = 0; lastRows = ''; byId('readings').scrollTop = 0;
  }
  if (action.text) textScale = QwMfd.clamp(textScale + action.text * .1, .8, 1.5);
  if (action.brightness) brightness = QwMfd.clamp(brightness + action.brightness * .1, .3, 1);
  if (action.scroll) step(action.scroll);
  savePreferences(); render();
}
/* Up and down move the cursor on Act, which has something to point at, and
   scroll the panel everywhere else. One pair of buttons either way: a cockpit
   frame has no spare ones, and a rocker nobody can name yet is not a plan. */
function step(direction) {
  if (QwMfd.pageIds[page] !== 'act') {
    byId('readings').scrollBy({ top: direction * byId('readings').clientHeight * .7, behavior: 'smooth' });
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
  const list = QwMfd.tasks(briefing);
  const target = list[QwMfd.clamp(selected, 0, list.length - 1)];
  if (!target?.tripId) { note('NOTHING TO CONFIRM'); return; }
  if (!armed) { armed = true; lastRows = ''; render(); return; }
  armed = false; lastRows = '';
  render();
  const path = target.kind === 'stop'
    ? `stops/${encodeURIComponent(target.stopId)}/toggle`
    : `stops/${encodeURIComponent(target.stopId)}/actions/${encodeURIComponent(target.actionId)}/toggle`;
  try {
    const response = await fetch(`/api/trips/${encodeURIComponent(target.tripId)}/${path}`,
      { method: 'POST', signal: AbortSignal.timeout(8000) });
    if (!response.ok) throw new Error('Not saved');
    note('MARKED DONE');
    selected = 0;
    await refreshBriefing();
  } catch { note('NOT SAVED · CHECK THE DASHBOARD'); }
}
window.chrome?.webview?.addEventListener('message', ({ data }) => {
  if (data.type === 'button') press(data.button);
  if (data.type === 'buttons') { bindings = QwMfd.buttons(data.buttons); drawLabels(); lastRows = ''; render(); }
  if (data.type === 'device') byId('device').textContent = data.connected
    ? 'F16 MFD ' + data.cougar + ' · USB' : 'F16 MFD ' + data.cougar + ' · not available';
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
async function loadMakers() {
  try {
    const response = await fetch('/api/manufacturers', { signal: AbortSignal.timeout(15000) });
    if (response.ok) makerNames = await response.json();
  } catch { /* the built-in sixteen stand on their own */ }
  finally { lastMaker = ''; render(); }
}
async function bootMfd() {
  drawLabels();
  render();
  refreshBriefing();
  loadAtlas();
  loadMakers();
  if (mfdParams.has('snapshot')) {
    try {
      const response = await fetch('/api/now');
      if (!response.ok) throw new Error('No snapshot');
      state = await response.json();
      byId('connection').textContent = 'SNAPSHOT'; render();
    } catch { byId('connection').textContent = 'SERVER UNAVAILABLE'; }
    return;
  }
  stream = new EventSource('/api/stream');
  const briefingTimer = setInterval(refreshBriefing, 5000);
  stream.onmessage = event => {
    try {
      state = JSON.parse(event.data);
      byId('connection').textContent = state.connected ? (state.inGame ? 'LIVE LOG' : 'GAME MENUS') : 'WAITING FOR GAME';
      render();
    } catch { byId('connection').textContent = 'INVALID UPDATE'; }
  };
  stream.onerror = () => { byId('connection').textContent = state ? 'DISCONNECTED · LAST DATA' : 'SERVER UNAVAILABLE'; };
  window.addEventListener('beforeunload', () => { stream.close(); clearInterval(briefingTimer); });
}
bootMfd();
