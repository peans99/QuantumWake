/* Quantumwake dashboard.
 *
 * No framework and no external requests: the page is served by the local
 * process and also loaded by the overlay's WebView2, so it stays dependency
 * free. Live updates arrive over Server-Sent Events, which every browser
 * supports natively. */

const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => Array.from(document.querySelectorAll(sel));

const params = new URLSearchParams(location.search);

/** True when hosted in the overlay shell, which wants a denser layout. */
const isOverlay = params.has('overlay');

/**
 * True for ?snapshot=1, which loads the data once and then leaves the page
 * still. The live event stream never closes, so a headless browser waits on it
 * forever and never reaches the load event - which is why documentation
 * screenshots could not be captured until this existed.
 */
const isSnapshot = params.has('snapshot');

const KIND_COLOURS = {
  City: '#7fe4ff',
  RestStop: '#4fd48a',
  Outpost: '#ffab3d',
  Research: '#b58cf0',
  DistributionCentre: '#ff5a4d',
  JumpPoint: '#eaf6ff',
  Mine: '#c78b4a',
  Asteroid: '#8fa0b6',
  Station: '#35c8f0',
  Planet: '#7796b0',
  Moon: '#46617a',
  NavPoint: '#46617a',
  MissionBeacon: '#ffab3d',
  Unknown: '#46617a',
};

/* Body order defines the ring layout, innermost first. */
const SYSTEMS = {
  Stanton: ['Hurston', 'Arial', 'Aberdeen', 'Magda', 'Ita', 'Crusader', 'Cellin', 'Daymar',
            'Yela', 'ArcCorp', 'Lyria', 'Wala', 'microTech', 'Calliope', 'Clio', 'Euterpe'],
  Pyro: ['Pyro I', 'Monox', 'Bloom', 'Pyro IV', 'Pyro V', 'Terminus'],
  Nyx: ['Delamar', 'Glaciem Ring', 'Keeger Belt'],
};

/* ---------- helpers ---------- */

const pad = (n) => String(n).padStart(2, '0');

function duration(seconds) {
  if (!seconds || seconds < 0) return '—';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h === 0) return `${m}m`;
  return `${h}h ${pad(m)}m`;
}

function clock(fromIso) {
  if (!fromIso) return '—';
  const secs = Math.max(0, (Date.now() - new Date(fromIso).getTime()) / 1000);
  const h = Math.floor(secs / 3600);
  const m = Math.floor((secs % 3600) / 60);
  const s = Math.floor(secs % 60);
  return `${pad(h)}:${pad(m)}:${pad(s)}`;
}

const timeOf = (iso) => new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
const shortTimeOf = (iso) => new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
const dateOf = (iso) => new Date(iso).toLocaleDateString([], { year: 'numeric', month: 'short', day: '2-digit' });

/**
 * A calendar day as it was written, not as the reader's clock re-reads it.
 *
 * A wipe is a day rather than a moment: it is stored at midnight UTC and typed
 * into a date field that means UTC. Formatting that in local time shows the day
 * before to everyone west of Greenwich - the Settings field said the 15th while
 * the notice beside it said the 14th, which is exactly how a date stops being
 * believed.
 */
const UTC_MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
                    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

// Assembled from the UTC parts rather than asked of Intl: a timeZone option is
// not honoured everywhere this page runs, and a date that quietly falls back to
// local time is the bug this exists to prevent.
const dayUtc = (iso) => {
  const at = new Date(iso);

  return `${UTC_MONTHS[at.getUTCMonth()]} ${String(at.getUTCDate()).padStart(2, '0')}, `
    + `${at.getUTCFullYear()}`;
};

/* Day and month only, for dense rows where a year wraps the line. */

const dayOf = (iso) => new Date(iso).toLocaleDateString([], { month: 'short', day: '2-digit' });

async function getJson(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} -> ${response.status}`);
  return response.json();
}

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

/* ---------- period selector ---------- */

/**
 * The one definition of the time windows every view offers.
 *
 * Six views needed the same control, so it is built from here rather than
 * repeated in markup - adding a window means editing this list once.
 */
const PERIODS = [
  { days: 0, label: 'All time' },
  { days: 1, label: 'Last 24 hours' },
  { days: 3, label: 'Last 3 days' },
  { days: 7, label: 'Last 7 days' },
  { days: 30, label: 'Last 30 days' },
  { days: 90, label: 'Last 90 days' },
  { days: 180, label: 'Last 6 months' },
  { days: 365, label: 'Last year' },
];

/** Fills every `select.period`. A view can reword its all-time option via data-all. */
function buildPeriodSelects() {
  for (const select of $$('select.period')) {
    const allLabel = select.dataset.all || PERIODS[0].label;

    for (const period of PERIODS) {
      const option = document.createElement('option');
      option.value = String(period.days);
      option.textContent = period.days === 0 ? allLabel : period.label;
      select.append(option);
    }
  }
}

buildPeriodSelects();

/* ---------- tabs ---------- */

function showView(name) {
  // Assets merged into Fleet; old #assets links and habits still land somewhere.
  if (name === 'assets') name = 'fleet';

  const buttons = $$('#tabs button');

  // The commodity page is a drill-down rather than a tab: it has no button of
  // its own, and keeps Market lit, because Market is where it is opened from.
  const target = buttons.find(
    (b) => b.dataset.view === (name === 'commodity' ? 'market' : name));

  if (!target) return;

  // Leaving the drill-down forgets its subject. The name alone is not enough
  // for the hash handler to know the page is up: keep it after a tab click and
  // a later link back to the same commodity matches, returns, and moves the
  // fragment with Market still on screen.
  if (name !== 'commodity') openCommodityName = null;

  // Settings reflects live state (the tray can change it), so re-read on entry.
  if (name === 'settings') renderSettings().catch(() => {});

  // Jobs change from the Crafting page and from play, so re-read on entry too.
  if (name === 'jobs' || name === 'blueprints') loadJobs().catch(() => {});
  if (name === 'checklists') loadChecklists().catch(() => {});
  if (name === 'imports') loadImports().catch(() => {});
  if (name === 'commodities') renderSharedReceipts().catch(() => {});
  if (name === 'blueprints') renderSharedBlueprints().catch(() => {});

  // These read live state or want the freshest prices, so they re-run on entry.
  if (name === 'routes') loadRoutes().catch(() => {});
  if (name === 'casualties') loadCasualties().catch(() => {});
  if (name === 'crew') loadCrew().catch(() => {});

  // The overlay page shows live state from both halves of the app.
  if (name === 'overlay') {
    renderSettings().catch(() => {});
    renderOverlayLayout().catch(() => {});
  }

  buttons.forEach((b) => b.classList.toggle('active', b === target));
  $$('.view').forEach((v) => v.classList.toggle('active', v.id === `view-${name}`));

  // A group lights up when the active view lives inside it, so the strip
  // still shows where you are even with the menu closed.
  $$('#tabs .tab-group').forEach((g) => {
    g.querySelector('.group-btn')?.classList
      .toggle('active', Boolean(g.querySelector('button.active[data-view]')));
  });

  // Switching view shows the top of it.
  //
  // The map's SVG is id="starmap" rather than the obvious id="map" for the same
  // reason: with the view in the fragment, #map would have matched that element
  // and the browser would anchor-scroll to it after load, leaving the header
  // stranded mid-screen. Fragments name views here, so no element may share a
  // view's name.
  window.scrollTo(0, 0);

  // Keep the active tab in view when the strip scrolls, as it does in overlay mode.
  target.scrollIntoView({ block: 'nearest', inline: 'center' });

  // replaceState, not assignment: cycling views with the arrow keys should not
  // fill the back button with thirty entries.
  if (location.hash !== `#${name}`) history.replaceState(null, '', `#${name}`);
}

/* The view lives in the URL fragment, so #map is a link that can be sent to
   someone and a page that survives a refresh. */
function viewFromHash() {
  const name = decodeURIComponent(location.hash.replace(/^#/, ''));
  return $$('#tabs button').some((b) => b.dataset.view === name) ? name : null;
}

/**
 * The commodity page carries its subject in the fragment - #commodity/Aluminum
 * - so a drill-down is a link like every other view, and survives a refresh.
 */
function commodityFromHash() {
  const raw = location.hash.replace(/^#/, '');
  return raw.startsWith('commodity/') ? decodeURIComponent(raw.slice('commodity/'.length)) : null;
}

window.addEventListener('hashchange', () => {
  const commodity = commodityFromHash();

  if (commodity) {
    if (commodity !== openCommodityName) openCommodity(commodity).catch(() => {});
    return;
  }

  const name = viewFromHash();
  if (name) showView(name);
});

$('#tabs').addEventListener('click', (event) => {
  const button = event.target.closest('button');
  if (!button || !button.dataset.view) return;

  showView(button.dataset.view);

  // Dropping focus lets a group menu close once a view is picked; otherwise
  // :focus-within pins it open over the page.
  button.blur();
});

/* Driven by the overlay shell's global hotkeys, so views can be changed without
   unlocking click-through. Also bound to the arrow keys for browser use. */
window.scCycleView = (delta) => {
  // In the overlay, only the tabs actually on show: it hides most of the
  // strip, and cycling into an invisible view would strand the widget
  // somewhere its own tab bar cannot reach. The dashboard cycles everything -
  // its group menus hide buttons without retiring the views behind them.
  const buttons = $$('#tabs button').filter((b) => b.dataset.view
    && (isOverlay ? b.offsetParent !== null : true));
  if (!buttons.length) return;

  const current = buttons.findIndex((b) => b.classList.contains('active'));
  const next = (current + delta + buttons.length) % buttons.length;
  showView(buttons[next].dataset.view);
};

window.scShowView = showView;

/* Driven by the overlay shell's fullscreen toggle: at full size the widget can
   afford the whole tab strip, so the six-tab whitelist lifts while expanded. */
window.scOverlayExpanded = (on) => {
  document.body.classList.toggle('expanded', Boolean(on));

  // Fullscreen shows everything; going back re-applies the chosen few.
  if (isOverlay) applyOverlayLayout().catch(() => {});
};

/* ---------- Now card collapse ---------- */

const NOW_COLLAPSED_KEY = 'qw-now-collapsed-cards';
const NOW_HIDDEN_KEY = 'qw-now-hidden-cards';
let collapsedNowCards = new Set();
let hiddenNowCards = new Set();

try {
  const saved = JSON.parse(localStorage.getItem(NOW_COLLAPSED_KEY) || '[]');
  if (Array.isArray(saved)) collapsedNowCards = new Set(saved);
} catch { /* a bad preference must not hide the dashboard */ }

try {
  const saved = JSON.parse(localStorage.getItem(NOW_HIDDEN_KEY) || '[]');
  if (Array.isArray(saved)) hiddenNowCards = new Set(saved);
} catch { /* a bad preference must not remove a dashboard card */ }

function saveCollapsedNowCards() {
  try { localStorage.setItem(NOW_COLLAPSED_KEY, JSON.stringify([...collapsedNowCards])); } catch { /* optional */ }
}

function saveHiddenNowCards() {
  try { localStorage.setItem(NOW_HIDDEN_KEY, JSON.stringify([...hiddenNowCards])); } catch { /* optional */ }
}

function renderHiddenNowCards() {
  const tray = $('#now-card-visibility');
  const list = $('#now-hidden-card-list');
  if (!tray || !list) return;

  list.textContent = '';
  for (const card of $$('#view-now .card[data-card]')) {
    const name = card.dataset.card;
    if (!hiddenNowCards.has(name)) continue;

    const label = card.querySelector('.card-label')?.textContent.trim() || name;
    const show = el('button', 'ghost tiny', `Show ${label}`);
    show.type = 'button';
    show.addEventListener('click', () => {
      hiddenNowCards.delete(name);
      card.classList.remove('user-hidden');
      saveHiddenNowCards();
      renderHiddenNowCards();
    });
    list.append(show);
  }
  tray.hidden = !list.children.length;
}

function initNowCardCollapsers() {
  for (const card of $$('#view-now .card[data-card]')) {
    const name = card.dataset.card;
    if (!name || card.querySelector('.now-collapse')) continue;

    const actions = el('div', 'now-card-actions');
    const button = el('button', 'now-collapse now-card-action');
    button.type = 'button';
    button.title = 'Collapse this card';
    button.setAttribute('aria-label', 'Collapse this card');
    button.addEventListener('click', () => {
      const collapsed = !card.classList.contains('collapsed');
      card.classList.toggle('collapsed', collapsed);
      button.textContent = collapsed ? '⌄' : '⌃';
      button.title = collapsed ? 'Expand this card' : 'Collapse this card';
      button.setAttribute('aria-label', button.title);
      button.setAttribute('aria-expanded', String(!collapsed));
      if (collapsed) collapsedNowCards.add(name);
      else collapsedNowCards.delete(name);
      saveCollapsedNowCards();
    });

    const collapsed = collapsedNowCards.has(name);
    card.classList.toggle('collapsed', collapsed);
    button.textContent = collapsed ? '⌄' : '⌃';
    button.title = collapsed ? 'Expand this card' : 'Collapse this card';
    button.setAttribute('aria-label', button.title);
    button.setAttribute('aria-expanded', String(!collapsed));

    const hide = el('button', 'now-hide now-card-action', '×');
    hide.type = 'button';
    hide.title = 'Hide this card from the Now page';
    hide.setAttribute('aria-label', hide.title);
    hide.addEventListener('click', () => {
      hiddenNowCards.add(name);
      card.classList.add('user-hidden');
      saveHiddenNowCards();
      renderHiddenNowCards();
    });
    actions.append(hide, button);
    card.append(actions);

    card.classList.toggle('user-hidden', !isOverlay && hiddenNowCards.has(name));
  }

  renderHiddenNowCards();
}

document.addEventListener('keydown', (event) => {
  if (!event.ctrlKey || !event.altKey) return;

  if (event.key === 'ArrowRight') { window.scCycleView(1); event.preventDefault(); }
  if (event.key === 'ArrowLeft') { window.scCycleView(-1); event.preventDefault(); }
});

/* ---------- live view ---------- */

let sessionStarted = null;
let nowState = null;
let briefingFor = null;

function renderNow(state) {
  nowState = state;
  $('#link').classList.toggle('live', !!state.connected);
  $('#link').title = state.connected ? 'live' : 'disconnected';

  $('#now-location').textContent = state.location || (state.inGame ? 'Unknown' : 'In menus');
  $('#now-location-sub').textContent = [state.locationBody, state.locationSystem].filter(Boolean).join(' · ');

  // The map follows the live feed, so the marker moves as the player does -
  // and shows the jump while it is happening, not only where it ended.
  setHere(state.locationId);
  setTravel(state.travelling ? state.travellingToId : null);
  tripArrived(state.locationId);

  // The trade card follows too, refreshing only when the place changes.
  refreshTradeAdvice(state.location).catch(() => {});
  refreshPilotBriefing(state).catch(() => {});

  const confidence = $('#now-confidence');
  confidence.textContent = state.location ? `${state.confidence.toLowerCase()} confidence` : '';
  confidence.className = `confidence ${(state.confidence || '').toLowerCase()}`;

  const travel = $('#now-travel');
  travel.hidden = !state.travelling;
  if (state.travelling) $('#now-travel-to').textContent = state.travellingTo || '';

  renderNowShip(state.ship);
  $('#now-handle').textContent = state.handle || '—';
  $('#now-version').textContent = state.gameVersion || '';
  $('#now-mode').textContent = state.inGame ? (state.gameRules || 'in game') : 'frontend / menus';
  $('#now-deaths').textContent = state.deaths ?? 0;
  $('#now-incaps').textContent = state.incapacitations ?? 0;

  // Be explicit about what each number is and is not. Deaths are inferred, so
  // the note is permanent rather than conditional: there is no state of the
  // game in which it stops being true. Kills are not shown at all - 4.9 logs
  // nothing that names a killer, and a counter stuck at zero reads as broken
  // rather than absent. The About page says where they went.
  $('#combat-note').textContent =
    'Deaths are inferred from corpse item-recovery bursts — 4.9 no longer writes '
    + '<Actor Death>, and an Incapacitated notification is not always raised.';

  sessionStarted = state.sessionStarted || null;

  const feed = $('#now-feed');
  feed.textContent = '';

  if (!state.recentEvents || state.recentEvents.length === 0) {
    feed.append(el('li', 'empty', state.connected ? 'Nothing yet this session.' : 'Waiting for the game…'));
  } else {
    // The widget shows a short tail; the full feed belongs on the page.
    const entries = isOverlay ? state.recentEvents.slice(0, 12) : state.recentEvents;

    for (const entry of entries) {
      const li = el('li');
      li.append(el('span', 't', timeOf(entry.at)));
      li.append(el('span', `k ${entry.kind}`, entry.kind));
      li.append(el('span', 'x', entry.text));
      if (entry.detail) li.append(el('span', 'd', entry.detail));
      feed.append(li);
    }
  }
}

/**
 * The briefing answers the decisions that depend on a live location. It only
 * re-fetches when that location changes; actions explicitly invalidate it.
 */
async function refreshPilotBriefing(state) {
  const card = $('#now-briefing-card');
  const key = state?.inGame && state.location
    ? `${state.locationId || ''}|${state.location}`
    : null;

  if (!key) {
    card.hidden = true;
    briefingFor = null;
    return;
  }

  if (key === briefingFor) return;

  // Claiming the key before the fetch keeps concurrent renders to one request,
  // so it has to be given back when that request fails - otherwise the card
  // keeps the previous location's stops, shopping and stash on screen as
  // though they described where the player is now, and nothing tries again
  // until they travel somewhere else entirely.
  briefingFor = key;

  let briefing;
  try {
    briefing = await getJson('/api/briefing');
  } catch (err) {
    if (briefingFor === key) {
      briefingFor = null;
      card.hidden = true;
    }
    throw err;
  }

  if (key !== `${nowState?.locationId || ''}|${nowState?.location || ''}`) return;

  renderPilotBriefing(briefing);
}

async function reloadPilotBriefing() {
  briefingFor = null;
  if (nowState) await refreshPilotBriefing(nowState);
}

function briefingMap(placeId, place) {
  showView('map');
  if (placeId) centreOn(placeId);
  else if (place) jumpToPlace(place);
}

async function addBriefingStop() {
  if (!nowState?.location) return;

  const button = $('#briefing-add-stop');
  button.disabled = true;
  try {
    await fetch('/api/trips/stops', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ placeId: nowState.locationId || '', place: nowState.location, note: null }),
    });
    await loadTrips();
    await reloadPilotBriefing();
  } finally {
    button.disabled = false;
  }
}

async function pinBriefingToOverlay() {
  const button = $('#briefing-overlay');
  button.disabled = true;
  try {
    await fetch('/api/overlay', { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ visible: true }) });
  } finally {
    button.disabled = false;
  }
}

function briefingStopRow(briefing, stop) {
  const row = el('div', 'briefing-row');
  const main = el('div', 'briefing-main');
  const name = el('button', 'briefing-link', stop.place);
  name.title = 'Show this stop on the map';
  name.addEventListener('click', () => briefingMap(stop.placeId, stop.place));
  main.append(name);
  if (stop.note) main.append(el('div', 'briefing-detail', stop.note));

  // What is still owed here. Without this the card keeps a landed stop on
  // screen and never says why it is keeping it, which reads as a stop that
  // will not cross off rather than as work outstanding.
  for (const action of stop.actions || []) {
    const quantity = action.quantity
      ? ` ${action.quantity.toLocaleString()}${action.unit ? ` ${action.unit}` : ''}`
      : '';
    main.append(el('div', 'briefing-detail', `${action.kind}${quantity} · ${action.text}`));
  }

  row.append(main);

  const done = el('button', 'ghost tiny', 'Mark collected');
  done.title = 'Cross this stop off';
  done.addEventListener('click', async () => {
    done.disabled = true;
    try {
      await tripCall(`/api/trips/${briefing.tripId}/stops/${stop.id}/toggle`);
      await reloadPilotBriefing();
    } finally {
      done.disabled = false;
    }
  });
  row.append(done);
  return row;
}

function briefingShoppingRow(item) {
  const row = el('div', 'briefing-row');
  const main = el('div', 'briefing-main');
  main.append(el('b', null, item.name));
  main.append(el('div', 'briefing-detail', `${item.needed} ${item.unit || 'needed'} · ${item.terminal} · ${money(item.price)}`));
  row.append(main);

  const map = el('button', 'ghost tiny', 'Map');
  map.title = 'Show this seller on the map';
  map.addEventListener('click', () => {
    showView('map');
    centreOnTerminal(item.terminal, null);
  });
  row.append(map);
  return row;
}

function renderPilotBriefing(briefing) {
  const card = $('#now-briefing-card');
  if (!briefing?.location) {
    card.hidden = true;
    return;
  }

  $('#briefing-title').textContent = briefing.location;
  $('#briefing-sub').textContent = briefing.tripTitle
    ? `Active flight plan · ${briefing.tripTitle}`
    : 'No active flight plan';

  const stops = $('#briefing-stops');
  stops.textContent = '';
  for (const stop of briefing.stops || []) stops.append(briefingStopRow(briefing, stop));
  $('#briefing-stops-section').hidden = !(briefing.stops || []).length;

  const shopping = $('#briefing-shopping');
  shopping.textContent = '';
  for (const item of briefing.shopping || []) shopping.append(briefingShoppingRow(item));
  $('#briefing-shopping-section').hidden = !(briefing.shopping || []).length;

  const trade = $('#briefing-trade');
  trade.textContent = '';
  for (const lead of briefing.trade || []) {
    const row = el('div', 'briefing-row');
    const main = el('div', 'briefing-main');
    const commodity = el('button', 'briefing-link', lead.commodity);
    commodity.title = 'Open this commodity in Market';
    commodity.addEventListener('click', () => openCommodity(lead.commodity));
    main.append(commodity);
    main.append(el('div', 'briefing-detail',
      `buy ${money(lead.buyHere)} → sell ${money(lead.sellThere)} at ${lead.sellTerminal}`));
    row.append(main);
    row.append(el('span', 'inward', `+${money(lead.marginPerScu)}/SCU`));
    trade.append(row);
  }
  if ((briefing.trade || []).length)
    trade.append(el('div', 'briefing-caveat', 'Leads only — cargo in your hold is not recorded by Game.log.'));
  $('#briefing-trade-section').hidden = !(briefing.trade || []).length;

  const services = $('#briefing-services');
  services.textContent = '';
  for (const service of briefing.services || []) {
    const key = serviceKey(service.name);
    const canMap = key && key !== 'repair' && service.dataEnabled;
    const chip = el(canMap ? 'button' : 'span',
      `briefing-service ${service.status.replaceAll(' ', '-')}`);
    if (canMap) {
      chip.type = 'button';
      chip.addEventListener('click', () => selectMapService(key, true));
    }
    chip.append(el('span', 'service-icon', SERVICE_META[key]?.icon || '•'));
    chip.append(el('span', 'service-text', `${service.name}: ${service.status}`));
    chip.title = canMap
      ? `Show ${SERVICE_META[key].label.toLowerCase()} on the map`
      : service.dataEnabled ? 'No map location is known for this service' : 'No installed data reports this service';
    services.append(chip);
  }
  $('#briefing-services-section').hidden = !(briefing.services || []).length;

  const stash = $('#briefing-stash');
  stash.textContent = '';
  for (const item of briefing.stash || []) {
    const row = el('div', 'briefing-row');
    const main = el('div', 'briefing-main');
    main.append(el('b', null, item.name));
    main.append(el('div', 'briefing-detail', item.category));
    row.append(main);
    stash.append(row);
  }
  $('#briefing-stash-section').hidden = !(briefing.stash || []).length;

  $('#briefing-map').onclick = () => briefingMap(briefing.locationId, briefing.location);
  $('#briefing-add-stop').onclick = addBriefingStop;
  $('#briefing-overlay').onclick = pinBriefingToOverlay;
  $('#briefing-overlay').hidden = isOverlay;
  card.hidden = false;
}

setInterval(() => { $('#now-clock').textContent = clock(sessionStarted); }, 1000);

function connectStream() {
  const source = new EventSource('/api/stream');

  source.onmessage = (event) => {
    try {
      renderNow(JSON.parse(event.data));
    } catch { /* ignore a malformed frame */ }
  };

  source.onerror = () => {
    $('#link').classList.remove('live');
    // EventSource reconnects on its own; nothing to do here.
  };
}

/* ---------- charts ---------- */

function bars(container, rows, format) {
  const node = $(container);
  node.textContent = '';

  if (!rows || rows.length === 0) {
    node.append(el('p', 'muted', 'No data yet.'));
    return;
  }

  const max = Math.max(...rows.map((r) => r.value)) || 1;

  for (const row of rows) {
    const wrapper = el('div', 'bar-row');

    // Rows can carry a click-through - the places charts fly to the map.
    if (row.onClick) {
      const label = el('div', 'label');
      const link = el('button', 'place-link', row.label);
      link.type = 'button';
      link.title = 'Show on the map';
      link.addEventListener('click', row.onClick);
      label.append(link);
      wrapper.append(label);
    } else {
      wrapper.append(el('div', 'label', row.label));
    }

    const track = el('div', 'bar-track');
    const fill = el('div', 'bar-fill');
    fill.style.width = `${Math.max(1, (row.value / max) * 100)}%`;
    if (row.colour) fill.style.background = row.colour;
    track.append(fill);

    wrapper.append(track);

    const amount = el('div', 'amount', format(row.value));
    if (row.note) {
      amount.append(el('span', 'note-inline', ` ${row.note}`));
    }
    wrapper.append(amount);
    node.append(wrapper);
  }
}

/* ---------- history ---------- */

/**
 * Runs one view's render in isolation.
 *
 * Without this a single failing view takes every later one down with it - a
 * throw in the fleet chart left Loadout and Stash blank - and boot()'s retry
 * loop swallowed the error, so the page just sat empty with no clue why.
 */
function safeRender(name, render) {
  const report = (error) => {
    console.error(`${name} failed to render`, error);

    const banner = $('#render-errors');
    banner.hidden = false;
    banner.append(el('div', null, `${name}: ${error && error.message ? error.message : error}`));
  };

  try {
    // Some renders became async once they had to fetch their own view; a
    // rejected promise would otherwise escape this net entirely.
    const result = render();
    if (result && typeof result.catch === 'function') result.catch(report);
  } catch (error) {
    report(error);
  }
}

async function loadHistory() {
  const [stats, sessions] = await Promise.all([getJson('/api/stats'), getJson('/api/sessions')]);

  $('#render-errors').textContent = '';
  $('#render-errors').hidden = true;

  allSessions = sessions;
  sessionPage = 0;

  safeRender('Sessions', () => renderSessions());
  safeRender('Fleet', () => renderFleet(stats));
  safeRender('Spending', () => renderSpending(stats));
  safeRender('Loadout', () => renderLoadout(stats));
  safeRender('Stash', () => renderStash(stats));
  loadAtlas().catch((e) => console.error('map', e));
  safeRender('Contracts', () => renderContracts(stats));
  safeRender('Places', () => renderPlaces(stats));
  // Routes can start before the ledger returns; rerun once the owned holds are
  // known so a selected ship cannot leave the table priced as one SCU.
  loadRoutes().catch((e) => console.error('routes after history', e));

  // These fetch their own data, so they are kicked off rather than awaited.
  loadLedger().catch((e) => console.error('ledger', e));
  loadLogbook().catch((e) => console.error('logbook', e));
  await loadManufacturers();
  loadShipsRef().catch((e) => console.error('ships', e));
  loadPartsRef().catch((e) => console.error('parts', e));
  loadMiningRef().catch((e) => console.error('mining', e));
  loadCraftingRef().catch((e) => console.error('crafting', e));
  loadJobs().catch((e) => console.error('jobs', e));
  loadChecklists().catch((e) => console.error('checklists', e));
  loadCasualties().catch((e) => console.error('casualties', e));
  loadCrew().catch((e) => console.error('crew', e));
  loadRespawn().catch((e) => console.error('respawn', e));
  loadOutfitting().catch((e) => console.error('outfitting', e));
  loadRoutes().catch((e) => console.error('routes', e));
  loadCargoReceipts().catch((e) => console.error('cargo receipts', e));
  loadTrips().catch((e) => console.error('trips', e));
  loadMapNotes().catch((e) => console.error('map notes', e));
  loadCommodities().catch((e) => console.error('cargo', e));
  loadMarket().catch((e) => console.error('market', e));
  loadLoot().catch((e) => console.error('loot', e));
  loadAssets().catch((e) => console.error('assets', e));
}

function renderContracts(stats) {
  tiles('#contract-summary', [
    ['Contracts seen', stats.contractsSeen],
    ['Completed', stats.contractsCompleted],
    ['Abandoned', stats.contractsAbandoned],
    ['Completion rate', stats.contractsSeen
      ? `${Math.round((stats.contractsCompleted / stats.contractsSeen) * 100)}%`
      : '—'],
  ]);

  bars('#issuers-chart',
    stats.contractIssuers.slice(0, 15).map((c) => ({ label: c.name, value: c.count })),
    (v) => `${v}`);

  bars('#types-chart',
    stats.contractTypes.slice(0, 15).map((c) => ({ label: c.name, value: c.count })),
    (v) => `${v}`);

  loadContractList().catch((e) => console.error('contracts', e));
  loadStanding().catch((e) => console.error('standing', e));
}

/**
 * The contract list, with the objective progress the game pushes but never
 * showed anywhere: how many journal steps a contract had and how many closed.
 */
/**
 * Work done per faction.
 *
 * Deliberately not called reputation. Nothing in the logs carries a rep value -
 * the client opens a channel to a reputation service and the numbers live on
 * the other side of it - so this counts the thing that moves it instead. Where
 * a text mod has written the reward onto a contract's title, that number is
 * shown as what it is: something somebody else worked out, on the few contracts
 * they got to.
 */
async function loadStanding() {
  const days = Number($('#contracts-period').value) || 0;
  const rows = await getJson(`/api/standing?days=${days}`).catch(() => []);
  const body = $('#standing-table tbody');

  if (!body) return;
  body.textContent = '';

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted', 'No contracts in that range.');
    td.colSpan = 6;
    tr.append(td);
    body.append(tr);
    return;
  }

  for (const row of rows) {
    const tr = el('tr');
    tr.append(el('td', null, row.issuer));
    tr.append(el('td', 'num', String(row.contracts)));

    const done = el('td', 'num');
    done.append(el('span', row.completed ? 'done' : 'muted', String(row.completed)));

    // Finishing rate is the part that reads as standing: nine of ten is a
    // different relationship from nine of thirty.
    if (row.contracts > 0)
      done.append(el('span', 'muted', ` · ${Math.round((row.completed / row.contracts) * 100)}%`));

    tr.append(done);
    tr.append(el('td', row.abandoned ? 'num outward' : 'num muted', String(row.abandoned)));

    // Never a bare zero: no annotated title is "nobody wrote it down", which is
    // not the same as "this pays nothing".
    const rep = el('td', 'num');

    if (row.repFrom > 0) {
      rep.append(el('span', null, row.rep.toLocaleString()));
      rep.append(el('span', 'muted', ` · from ${row.repFrom}`));
      rep.title = `Read from ${row.repFrom} contract title${row.repFrom === 1 ? '' : 's'} the StarStrings mod has annotated`;
    } else {
      rep.append(el('span', 'muted', '—'));
      rep.title = 'No annotated title among these, so nobody has written down what they pay';
    }

    tr.append(rep);

    const span = el('td', 'muted');
    span.textContent = row.first === row.last
      ? dateOf(row.first)
      : `${dateOf(row.first)} → ${dateOf(row.last)}`;
    tr.append(span);

    body.append(tr);
  }
}

async function loadContractList() {
  const days = Number($('#contracts-period').value) || 0;
  const rows = await getJson(`/api/contracts?days=${days}`);
  const body = $('#contracts-table tbody');
  body.textContent = '';

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted', 'No contracts in that range.');
    td.colSpan = 9;
    tr.append(td);
    body.append(tr);
    return;
  }

  const OUTCOMES = {
    Completed: ['done', 'completed'],
    Abandoned: ['outward', 'abandoned'],
    InProgress: ['muted', 'in progress'],
    Unknown: ['muted', '—'],
  };

  for (const row of rows.slice(0, 400)) {
    const tr = el('tr');
    tr.append(el('td', null, dateOf(row.at)));

    // The composed name repeats issuer, type and difficulty, so the columns
    // carry those and the full name rides the row's tooltip.
    tr.append(el('td', null, row.issuer || '—'));
    tr.append(el('td', 'muted', row.type || '—'));
    tr.append(el('td', 'muted', row.difficulty || '—'));
    tr.append(el('td', 'muted', row.system || '—'));
    tr.title = row.name;

    const [cls, label] = OUTCOMES[row.outcome] || OUTCOMES.Unknown;
    tr.append(el('td', cls === 'done' ? 'inward' : cls, label));

    // What the title says it pays, when a text mod has annotated it. Silence
    // is the honest default: most titles say nothing, and a zero would read as
    // "this pays nothing" rather than "nobody wrote it down".
    const pays = el('td');

    if (row.rep) {
      const chip = el('span', 'tag-rep', `${row.rep.toLocaleString()} rep`);
      chip.title = 'From the contract title, annotated by the StarStrings mod';
      pays.append(chip);
    }

    if (row.blueprint) {
      const chip = el('span', 'tag-bp', 'BP');
      chip.title = 'Tagged as awarding a blueprint';
      pays.append(chip);
    }

    if (!row.rep && !row.blueprint) pays.append(el('span', 'muted', '—'));

    tr.append(pays);

    // Steps only exist for missions whose objectives were pushed while the
    // log was being written; older contracts honestly show nothing.
    const steps = el('td', 'num');
    if (row.steps > 0) {
      steps.textContent = `${row.stepsDone} / ${row.steps}`;
      if (row.stepsDone < row.steps) steps.classList.add('muted');
    } else {
      steps.textContent = '—';
      steps.classList.add('muted');
    }
    tr.append(steps);

    tr.append(el('td', 'num muted', row.minutes
      ? (row.minutes < 60 ? `${Math.round(row.minutes)}m` : `${(row.minutes / 60).toFixed(1)}h`)
      : '—'));

    body.append(tr);
  }
}

function renderPlaces(stats) {
  libraryStats = stats;

  const term = ($('#places-search').value || '').trim().toLowerCase();
  const match = (name) => !term || name.toLowerCase().includes(term);

  bars('#places-chart',
    stats.locations.filter((l) => match(l.name)).slice(0, 25).map((l) => ({
      label: l.name,
      value: l.visits,
      colour: KIND_COLOURS[l.kind],
      onClick: () => jumpToPlace(l.name),
    })),
    (v) => `${v}`);

  bars('#dests-chart',
    stats.destinations.filter((d) => match(d.name)).slice(0, 25)
      .map((d) => ({ label: d.name, value: d.visits, onClick: () => jumpToPlace(d.name) })),
    (v) => `${v}`);
}

/* .NET serialises TimeSpan as "hh:mm:ss" or "d.hh:mm:ss". */
function toSeconds(timespan) {
  if (typeof timespan === 'number') return timespan;
  if (!timespan) return 0;

  const [head, ...rest] = String(timespan).split('.');
  let days = 0;
  let clockPart = timespan;

  if (rest.length && head.indexOf(':') === -1) {
    days = parseInt(head, 10);
    clockPart = rest.join('.');
  }

  const [h = 0, m = 0, s = 0] = String(clockPart).split(':').map(parseFloat);
  return days * 86400 + h * 3600 + m * 60 + s;
}

/* ---------- sessions ---------- */

const SESSIONS_PER_PAGE = 25;
let allSessions = [];
let sessionPage = 0;
let expandedSessionId = null;
const sessionDetails = new Map();

/** Applies the period and search filters. */
function filteredSessions() {
  const term = ($('#sessions-search').value || '').trim().toLowerCase();
  const days = Number($('#sessions-period').value) || 0;
  const cutoff = days ? Date.now() - days * 86400000 : null;

  return allSessions.filter((s) => {
    if (cutoff && new Date(s.startedAt).getTime() < cutoff) return false;

    if (term) {
      const haystack = `${s.primaryShip || ''} ${s.lastLocation || ''}`.toLowerCase();
      if (!haystack.includes(term)) return false;
    }

    return true;
  });
}

async function toggleSessionDebrief(id) {
  if (expandedSessionId === id) {
    expandedSessionId = null;
    renderSessions();
    return;
  }

  expandedSessionId = id;
  renderSessions();

  if (!sessionDetails.has(id)) {
    try {
      sessionDetails.set(id, await getJson(`/api/sessions/${encodeURIComponent(id)}`));
    } catch {
      sessionDetails.set(id, { error: true });
    }
  }

  if (expandedSessionId === id) renderSessions();
}

function sessionRoute(detail) {
  const usefulQuantumTarget = (name) => name
    && !/^(PartyMemberMarker_|MISSION_)/i.test(name)
    && !/\.socpak$/i.test(name)
    && !['Nav Point', 'Rest Stop', 'Mission Beacon'].includes(name);

  const points = [
    ...(detail.locations || []).map((place) => ({ ...place, routeKind: 'arrival' })),
    ...(detail.jumps || []).filter((jump) => usefulQuantumTarget(jump.toName)).map((jump) => ({
      at: jump.at,
      rawId: jump.toId,
      displayName: jump.toName,
      system: null,
      body: null,
      routeKind: 'quantum',
    })),
  ].sort((a, b) => new Date(a.at) - new Date(b.at));

  return points.filter((place, index) => {
    if (index === 0) return true;
    const previous = points[index - 1];
    return place.rawId !== previous.rawId
      && place.displayName.toLowerCase() !== previous.displayName.toLowerCase();
  });
}

async function repeatSessionRoute(detail) {
  const route = sessionRoute(detail);
  if (!route.length) return;

  await planTrip(`Repeat ${dateOf(detail.startedAt)} route`, route.map((place) => ({
    placeId: place.rawId || '',
    place: place.displayName,
    note: `${place.routeKind === 'quantum' ? 'Quantum target' : 'Arrival'} logged at ${shortTimeOf(place.at)}`,
  })));
}

function sessionMetric(label, value, cls = '') {
  const metric = el('div', `session-metric ${cls}`.trim());
  metric.append(el('div', 'session-metric-value', value));
  metric.append(el('div', 'session-metric-label', label));
  return metric;
}

function renderSessionDebrief(summary) {
  const row = el('tr', 'session-detail-row');
  const cell = el('td');
  cell.colSpan = 9;
  row.append(cell);

  const detail = sessionDetails.get(summary.id);
  if (!detail) {
    cell.append(el('div', 'session-debrief-loading muted', 'Building session debrief…'));
    return row;
  }

  if (detail.error) {
    cell.append(el('div', 'session-debrief-loading outward', 'This session detail could not be read.'));
    return row;
  }

  const debrief = el('article', 'session-debrief');
  const head = el('div', 'session-debrief-head');
  const title = el('div');
  title.append(el('div', 'session-debrief-title', `${dateOf(detail.startedAt)} debrief`));
  title.append(el('div', 'muted', `${shortTimeOf(detail.startedAt)} → ${shortTimeOf(detail.endedAt)} · ${detail.gameVersion || 'version unknown'}`));
  head.append(title);

  const route = sessionRoute(detail);
  const repeat = el('button', 'ghost tiny', 'Repeat these stops');
  repeat.type = 'button';
  repeat.disabled = route.length === 0;
  repeat.title = route.length
    ? 'Create a new flight plan from the places reached in this session'
    : 'No named locations were recorded in this session';
  repeat.addEventListener('click', () => repeatSessionRoute(detail));
  head.append(repeat);
  debrief.append(head);

  const ships = (detail.ships || []).map((ship) =>
    `${ship.displayName || ship.model}${ship.sorties ? ` · ${ship.sorties} sortie${ship.sorties === 1 ? '' : 's'}` : ''}`);
  const contracts = detail.contracts || [];
  const completed = contracts.filter((contract) => contract.outcome === 'Completed').length;
  const party = new Set((detail.partyNotes || []).map((note) => note.handle).filter(Boolean));
  const tradeCount = (detail.trades || []).length;
  const movementCount = (detail.purchases || []).length + tradeCount;
  const net = Number(detail.income || 0) - Number(detail.spend || 0) - Number(detail.commoditySpend || 0);

  const metrics = el('div', 'session-debrief-metrics');
  metrics.append(
    sessionMetric('In game', duration(summary.inGame)),
    sessionMetric('Ship', ships.join(' · ') || 'On foot'),
    sessionMetric('Recorded route', `${route.length} point${route.length === 1 ? '' : 's'} · ${(detail.jumps || []).length} jump${(detail.jumps || []).length === 1 ? '' : 's'}`),
    sessionMetric('Contracts', contracts.length ? `${completed} / ${contracts.length} completed` : 'None recorded'),
    sessionMetric(tradeCount ? 'Recorded net*' : 'Recorded net', movementCount
      ? `${net < 0 ? '−' : '+'}${tradeCount ? '~' : ''}${money(Math.abs(net))}`
      : 'No movements recorded', movementCount ? (net < 0 ? 'outward' : 'inward') : ''),
    sessionMetric('Crew observed*', party.size ? `${party.size} named` : 'None named'),
  );
  debrief.append(metrics);

  const content = el('div', 'session-debrief-grid');

  const routeSection = el('section', 'session-debrief-section session-route-section');
  routeSection.append(el('h3', null, 'Chronological route'));
  const routeList = el('ol', 'session-route');
  route.forEach((place) => {
    const item = el('li');
    item.append(el('span', 'session-route-time', shortTimeOf(place.at)));
    item.append(placeLink(place.displayName));
    const context = place.routeKind === 'quantum'
      ? 'quantum destination'
      : [place.body, place.system].filter(Boolean).join(' · ');
    if (context) item.append(el('span', 'muted', context));
    routeList.append(item);
  });
  if (!route.length) routeList.append(el('li', 'muted', 'No named locations were written in this session.'));
  routeSection.append(routeList);
  content.append(routeSection);

  const commerceSection = el('section', 'session-debrief-section');
  commerceSection.append(el('h3', null, 'Recorded economy'));
  const commerce = [
    ...(detail.purchases || []).map((purchase) => ({
      at: purchase.at,
      label: `${prettyItem(purchase.item)}${purchase.quantity > 1 ? ` ×${purchase.quantity}` : ''}`,
      amount: -Number(purchase.total ?? purchase.price ?? 0),
      approximate: !purchase.confirmed,
    })),
    ...(detail.trades || []).map((trade) => ({
      at: trade.at,
      label: `${trade.isSell ? 'Cargo sold' : 'Cargo bought'} · ${trade.quantity} SCU`,
      amount: (trade.isSell ? 1 : -1) * Number(trade.amount || 0),
      approximate: true,
    })),
  ].sort((a, b) => new Date(a.at) - new Date(b.at));
  const commerceList = el('ul', 'session-debrief-list');
  commerce.slice(-8).forEach((entry) => {
    const item = el('li');
    item.append(el('span', 'muted', shortTimeOf(entry.at)));
    item.append(el('span', null, entry.label));
    item.append(el('span', entry.amount >= 0 ? 'inward' : 'outward',
      `${entry.amount >= 0 ? '+' : '−'}${entry.approximate ? '~' : ''}${money(Math.abs(entry.amount))}`));
    commerceList.append(item);
  });
  if (!commerce.length) commerceList.append(el('li', 'muted', 'No purchases or cargo trades recorded.'));
  commerceSection.append(commerceList);
  content.append(commerceSection);

  const contractSection = el('section', 'session-debrief-section');
  contractSection.append(el('h3', null, 'Contracts'));
  const contractList = el('ul', 'session-debrief-list');
  contracts.slice(0, 8).forEach((contract) => {
    const item = el('li');
    item.append(el('span', contract.outcome === 'Completed' ? 'inward' : 'muted', contract.outcome || 'Unknown'));
    item.append(el('span', null, contract.displayName || contract.raw || 'Unnamed contract'));
    if (contract.steps > 0) item.append(el('span', 'muted', `${contract.stepsDone} / ${contract.steps} steps`));
    contractList.append(item);
  });
  if (!contracts.length) contractList.append(el('li', 'muted', 'No contracts recorded.'));
  contractSection.append(contractList);
  content.append(contractSection);

  const highlightSection = el('section', 'session-debrief-section');
  highlightSection.append(el('h3', null, 'Latest highlights'));
  const highlights = (detail.timeline || [])
    .filter((entry) => !['party', 'location', 'quantum', 'login'].includes(entry.kind))
    .slice(-10);
  const highlightList = el('ul', 'session-debrief-list');
  highlights.forEach((entry) => {
    const item = el('li');
    item.append(el('span', 'muted', shortTimeOf(entry.at)));
    item.append(el('span', null, entry.text));
    if (entry.detail) item.append(el('span', 'muted', entry.detail));
    highlightList.append(item);
  });
  if (!highlights.length) highlightList.append(el('li', 'muted', 'No additional highlights recorded.'));
  highlightSection.append(highlightList);
  content.append(highlightSection);

  debrief.append(content);
  const limits = el('p', 'session-debrief-note muted',
    '* Cargo amounts are kiosk requests, not confirmed settlements. Crew observed is a floor from party notifications, not a roster.');
  debrief.append(limits);
  cell.append(debrief);
  return row;
}

/** Paged because a real library runs to well over a hundred sessions. */
function renderSessions() {
  const body = $('#sessions-table tbody');
  body.textContent = '';

  const sessions = filteredSessions();

  // Totals reflect the selected period, so the tiles answer "how much did I
  // play this month" rather than always restating the lifetime figures.
  summariseSessions(sessions);

  const pages = Math.max(1, Math.ceil(sessions.length / SESSIONS_PER_PAGE));
  sessionPage = Math.min(Math.max(0, sessionPage), pages - 1);

  const start = sessionPage * SESSIONS_PER_PAGE;
  const page = sessions.slice(start, start + SESSIONS_PER_PAGE);

  if (page.length === 0) {
    const tr = el('tr');
    const td = el('td', 'muted', 'No sessions in that range.');
    td.colSpan = 9;
    tr.append(td);
    body.append(tr);
  }

  for (const session of page) {
    const tr = el('tr', 'session-row');
    const open = expandedSessionId === session.id;
    tr.classList.toggle('open', open);
    tr.tabIndex = 0;
    tr.setAttribute('aria-expanded', String(open));
    tr.title = open ? 'Close session debrief' : 'Open session debrief';
    tr.addEventListener('click', () => toggleSessionDebrief(session.id));
    tr.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        toggleSessionDebrief(session.id);
      }
    });
    const cells = [
      duration(session.inGame),
      duration(session.menu),
      session.primaryShip || '—',
      session.lastLocation || '—',
    ];
    const date = el('td');
    date.append(el('span', 'session-row-toggle', open ? '⌄' : '›'));
    date.append(el('span', null, dateOf(session.startedAt)));
    tr.append(date);
    cells.forEach((text) => tr.append(el('td', null, text)));
    [session.jumps, session.contracts, session.deaths ?? 0, session.incapacitations]
      .forEach((n) => tr.append(el('td', 'num', String(n))));
    body.append(tr);
    if (open) body.append(renderSessionDebrief(session));
  }

  renderPager(pages, start, page.length, sessions.length);
}

function summariseSessions(sessions) {
  const sum = (pick) => sessions.reduce((total, s) => total + (pick(s) || 0), 0);

  tiles('#lib-summary', [
    ['Sessions', sessions.length],
    ['In game', duration(sum((s) => s.inGame))],
    ['In menus', duration(sum((s) => s.menu))],
    ['Quantum jumps', sum((s) => s.jumps)],
    ['Contracts', sum((s) => s.contracts)],
    ['Deaths', sum((s) => s.deaths)],
  ]);
}

function renderPager(pages, start, shown, total) {
  const pager = $('#sessions-pager');
  pager.textContent = '';

  if (total === 0) return;

  pager.append(el('span', 'pager-info', `${start + 1}–${start + shown} of ${total}`));

  const nav = el('div', 'pager-nav');

  const step = (label, delta, disabled) => {
    const button = el('button', 'pager-btn', label);
    button.disabled = disabled;
    button.addEventListener('click', () => { sessionPage += delta; renderSessions(); });
    return button;
  };

  nav.append(step('‹ Newer', -1, sessionPage === 0));
  nav.append(el('span', 'pager-page', `${sessionPage + 1} / ${pages}`));
  nav.append(step('Older ›', 1, sessionPage >= pages - 1));

  pager.append(nav);
}

/* ---------- ledger ---------- */

const LEDGER_PER_PAGE = 40;
let ledgerEntries = [];
let ledgerPage = 0;

async function loadLedger() {
  const days = Number($('#ledger-period').value) || 0;
  ledgerEntries = await getJson(`/api/ledger?days=${days}`);
  ledgerPage = 0;
  renderLedger();
}

function renderLedger() {
  const body = $('#ledger-table tbody');
  body.textContent = '';

  const inbound = ledgerEntries.filter((e) => e.amount > 0);
  const outbound = ledgerEntries.filter((e) => e.amount < 0);

  const sum = (rows) => rows.reduce((total, e) => total + Number(e.amount), 0);
  const net = sum(ledgerEntries);

  tiles('#ledger-summary', [
    ['Money in', money(sum(inbound))],
    ['Money out', money(Math.abs(sum(outbound)))],
    [net >= 0 ? 'Net gain' : 'Net loss', money(Math.abs(net))],
    ['Movements', ledgerEntries.length],
  ]);

  const pages = Math.max(1, Math.ceil(ledgerEntries.length / LEDGER_PER_PAGE));
  ledgerPage = Math.min(Math.max(0, ledgerPage), pages - 1);

  const start = ledgerPage * LEDGER_PER_PAGE;
  const page = ledgerEntries.slice(start, start + LEDGER_PER_PAGE);

  if (!page.length) {
    const tr = el('tr');
    const td = el('td', 'muted', 'No transactions in that range.');
    td.colSpan = 7;
    tr.append(td);
    body.append(tr);
  }

  for (const entry of page) {
    const tr = el('tr');
    const inward = Number(entry.amount) > 0;

    tr.append(el('td', null, dateOf(entry.at)));
    tr.append(el('td', null, entry.kind));
    tr.append(el('td', null, prettyItem(entry.what)));
    tr.append(tdPlace(entry.where));
    tr.append(el('td', 'muted', entry.shop));

    // Unconfirmed amounts are marked rather than silently presented as settled.
    const amount = el('td', `num ${inward ? 'inward' : 'outward'}`,
      `${inward ? '+' : '−'}${entry.confirmed ? '' : '~'}${money(Math.abs(entry.amount))}`);
    tr.append(amount);

    tr.append(el('td', 'num muted', money(entry.running)));
    body.append(tr);
  }

  renderLedgerPager(pages, start, page.length);
}

function renderLedgerPager(pages, start, shown) {
  const pager = $('#ledger-pager');
  pager.textContent = '';

  if (!ledgerEntries.length) return;

  pager.append(el('span', 'pager-info',
    `${start + 1}–${start + shown} of ${ledgerEntries.length}`));

  const nav = el('div', 'pager-nav');
  const step = (label, delta, disabled) => {
    const button = el('button', 'pager-btn', label);
    button.disabled = disabled;
    button.addEventListener('click', () => { ledgerPage += delta; renderLedger(); });
    return button;
  };

  nav.append(step('‹ Newer', -1, ledgerPage === 0));
  nav.append(el('span', 'pager-page', `${ledgerPage + 1} / ${pages}`));
  nav.append(step('Older ›', 1, ledgerPage >= pages - 1));

  pager.append(nav);
}

/* ---------- cargo trading ---------- */

async function loadCommodities() {
  const days = Number($('#commodities-period').value) || 0;
  renderCommodities(await getJson(`/api/commodities?days=${days}`));
  refreshCommunityOffer().catch(() => {});
}

/**
 * The opt-in that names the cargo. The offer is shown only while the dataset is
 * absent; enabling it is a deliberate click, and the app's one network request.
 */
async function refreshCommunityOffer() {
  const community = await getJson('/api/community');

  $('#community-offer').hidden = community.enabled;

  if (community.enabled) {
    $('#cargo-caption').textContent =
      'Volume, price and place come straight from the kiosk. Commodity names '
      + `come from the community dataset (${community.commodities} commodities, `
      + 'StarCitizenWiki / scunpacked-data), fetched once at your request.';
  }
}

async function setCommunity(enabled, statusNode, button) {
  button.disabled = true;
  statusNode.textContent = enabled ? 'downloading…' : 'removing…';

  try {
    const result = await fetch(`/api/community/${enabled ? 'enable' : 'disable'}`, { method: 'POST' });
    if (!result.ok) throw new Error((await result.json()).title || result.statusText);

    statusNode.textContent = '';
    await loadCommodities();
    await loadHistory();       // the ledger names its cargo rows too
    await renderSettings();
  } catch (e) {
    statusNode.textContent = `failed: ${e.message}`;
  } finally {
    button.disabled = false;
  }
}

$('#community-enable').addEventListener('click', (e) =>
  setCommunity(true, $('#community-status'), e.currentTarget));

/* ---------- trade advice ---------- */

let adviceFor = null;

/**
 * The "trade from here" card on the Now page and the overlay: what this
 * place's terminal sells cheap and where it fetches the most. Fetched only
 * when the place changes, and only while UEX is enabled - the card simply
 * stays hidden otherwise.
 */
async function refreshTradeAdvice(place) {
  const card = $('#trade-advice-card');

  if (!place) {
    card.hidden = true;
    adviceFor = null;
    return;
  }

  if (place === adviceFor)
    return;

  adviceFor = place;

  const advice = await getJson(`/api/trade/advice?place=${encodeURIComponent(place)}`);

  if (!advice.terminal || !advice.opportunities.length) {
    card.hidden = true;
    return;
  }

  $('#trade-advice-sub').textContent =
    `${advice.terminal} · best margins per SCU, UEX community prices`;

  const list = $('#trade-advice');
  list.textContent = '';

  for (const o of advice.opportunities) {
    const li = el('li');
    li.append(el('b', null, o.commodity));
    li.append(el('span', 'muted', ` buy ${money(o.buyHere)} → sell ${money(o.sellThere)} at ${o.sellTerminal} `));
    li.append(el('span', 'inward', `+${money(o.marginPerScu)}/SCU`));
    list.append(li);
  }

  card.hidden = false;
}

/* ---------- market ---------- */

/** The community catalogue, cached for the map's commodity search too. */
let marketEntries = [];

/**
 * Whether Market rows show each price's age. On by default - a trader judges
 * a number by how old it is - and switchable from Settings for anyone who
 * finds the extra line noise.
 */
let showPriceAge = true;
try { showPriceAge = localStorage.getItem('qw-uex-age') !== '0'; } catch { /* fine */ }

/** Fine-grained "how long ago" for prices, where days are too coarse. */
function ago(iso) {
  if (!iso) return 'unknown';

  const mins = Math.floor((Date.now() - new Date(iso).getTime()) / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;

  const hours = Math.floor(mins / 60);
  if (hours < 48) return `${hours}h ago`;

  const days = Math.floor(hours / 24);
  if (days < 60) return `${days}d ago`;

  return `${Math.round(days / 30)}mo ago`;
}

async function loadMarket() {
  try {
    marketEntries = await getJson('/api/market');
  } catch {
    marketEntries = [];
  }

  // Fuel, when that feed is on: cheapest refill per terminal.
  try {
    const fuel = await getJson('/api/uex/fuel');
    $('#fuel-block').hidden = fuel.length === 0;

    const fuelBody = $('#fuel-table tbody');
    fuelBody.textContent = '';

    // Only the cheapest few of each fuel. Every terminal that sells hydrogen
    // is two hundred rows nobody reads, and it buried the commodities this
    // page is actually about.
    const perFuel = 8;
    const byFuel = new Map();

    for (const row of [...fuel].sort((a, b) => a.price - b.price)) {
      if (!byFuel.has(row.fuel)) byFuel.set(row.fuel, []);
      byFuel.get(row.fuel).push(row);
    }

    for (const [name, rows] of [...byFuel].sort((a, b) => a[0].localeCompare(b[0]))) {
      for (const row of rows.slice(0, perFuel)) {
        const tr = el('tr');
        tr.append(el('td', null, name));
        tr.append(tdPlace(row.terminal, 'muted'));
        tr.append(el('td', 'num', money(row.price)));
        fuelBody.append(tr);
      }
    }

    const hidden = fuel.length - [...byFuel.values()].reduce((n, r) => n + Math.min(perFuel, r.length), 0);
    $('#fuel-count').textContent = hidden > 0
      ? `Cheapest ${perFuel} per fuel shown; ${hidden.toLocaleString()} dearer terminals not listed.`
      : '';
  } catch { $('#fuel-block').hidden = true; }

  // The snapshot's own age and the shortcut to renew it, next to the search -
  // the Settings page should not be the only road to a fresh number.
  try {
    const uex = await getJson('/api/uex');
    $('#market-refresh').hidden = !uex.enabled;
    $('#market-age').textContent = uex.enabled && uex.fetchedAt
      ? `prices fetched ${ago(uex.fetchedAt)}`
      : '';
  } catch { /* leave the header as it is */ }

  // The group dropdown offers every group the catalogue actually uses,
  // rebuilt on load and keeping the user's pick when it still exists.
  const groupSelect = $('#market-group');
  const previous = groupSelect.value;
  const groups = [...new Set(marketEntries.flatMap((e) => e.groups))].sort();

  groupSelect.textContent = '';
  groupSelect.append(new Option('All groups', ''));
  for (const group of groups) groupSelect.append(new Option(group, group));
  if (groups.includes(previous)) groupSelect.value = previous;

  renderMarket();

  // The map's commodity search reads this catalogue, and the map usually draws
  // first. A search already standing (a ?q= link opened cold) would have found
  // nothing; redraw it now the names exist.
  if (($('#map-search')?.value || '').trim() && atlas.length) drawMap();
}

/* ---------- where a commodity actually trades ---------- */

/**
 * Every terminal for one commodity, fetched once and kept.
 *
 * The table's UEX column answers "what is the best price anywhere", which is
 * one number and often a bad plan: the best price can be four jumps into
 * lawless space, or be a counter that wants nine SCU. The choice belongs to the
 * player, so the rows behind that number are shown and ranked, with what it
 * would cost to take each one.
 */
const terminalCache = new Map();

async function terminalsFor(commodity) {
  if (!terminalCache.has(commodity)) {
    terminalCache.set(commodity,
      getJson(`/api/uex/market?commodity=${encodeURIComponent(commodity)}`).catch(() => []));
  }

  return terminalCache.get(commodity);
}

/** What the open detail is showing, so the controls can redraw it. */
const detailView = { commodity: null, buying: false, monitoredOnly: false };

/**
 * Demand small enough that the trip is the wrong shape.
 *
 * Not a hard rule - a hauler carrying two SCU is fine - but a counter wanting
 * 12 SCU is not where a full hold goes, and the number alone does not say so
 * at a glance.
 */
const THIN_SCU = 64;

function toggleMarketDetail(entry, row) {
  const open = row.nextElementSibling?.classList.contains('market-detail');

  // One at a time: two open tables in one page is a page nobody reads.
  $$('#market-table tr.market-detail').forEach((n) => n.remove());
  $$('#market-table tr.expanded').forEach((n) => n.classList.remove('expanded'));

  if (open) {
    detailView.commodity = null;
    return;
  }

  detailView.commodity = entry.name;
  row.classList.add('expanded');

  const holder = el('tr', 'market-detail');
  const cell = el('td');
  cell.colSpan = 8;
  holder.append(cell);
  row.after(holder);

  renderMarketDetail(entry, cell);
}

async function renderMarketDetail(entry, cell) {
  cell.textContent = '';

  const head = el('div', 'detail-head');
  head.append(el('b', null, `${entry.name} — every counter UEX knows`));

  const side = el('div', 'seg');
  for (const [value, label] of [['sell', 'Where to sell'], ['buy', 'Where to buy']]) {
    const button = el('button', (value === 'buy') === detailView.buying ? 'active' : null, label);
    button.addEventListener('click', () => {
      detailView.buying = value === 'buy';
      renderMarketDetail(entry, cell);
    });
    head.append(button);
    side.append(button);
  }

  head.textContent = '';
  head.append(el('b', null, `${entry.name} — every counter UEX knows`));

  // Beside the title rather than at the far end: the detail table is wider than
  // the panel, so anything pushed right is pushed off the edge.
  const more = el('button', 'ghost detail-more', 'Full picture ↗');
  more.title = 'Price and demand over time, both counter lists, and your receipts';
  more.addEventListener('click', () => openCommodity(entry.name));
  head.append(more);

  head.append(side);

  const safe = el('label', 'toggle');
  const box = el('input');
  box.type = 'checkbox';
  box.checked = detailView.monitoredOnly;
  box.addEventListener('change', () => {
    detailView.monitoredOnly = box.checked;
    renderMarketDetail(entry, cell);
  });

  safe.append(box);
  safe.append(el('span', null, 'Monitored space only'));
  head.append(safe);
  cell.append(head);

  // The strip goes in before the table is awaited, so the panel does not sit
  // empty while a fetch per counter runs. It fills itself in afterwards.
  const strip = el('div', 'detail-spark');
  cell.append(strip);
  drawDetailSpark(strip, entry.name).catch(() => strip.remove());

  const all = await terminalsFor(entry.name);
  const priceOf = (r) => (detailView.buying ? r.buy : r.sell);
  const scuOf = (r) => (detailView.buying ? r.buyScu : r.sellScu);

  const rows = all
    .filter((r) => priceOf(r) > 0)
    .filter((r) => !detailView.monitoredOnly || r.security === 'monitored')
    .sort((a, b) => (detailView.buying ? priceOf(a) - priceOf(b) : priceOf(b) - priceOf(a)));

  if (!rows.length) {
    cell.append(el('div', 'muted detail-empty', all.length
      ? 'Nothing left after that filter — every counter for this is outside monitored space.'
      : 'No prices for this commodity. UEX may be off, or nobody has reported one.'));
    return;
  }

  const best = priceOf(rows[0]);

  const table = el('table', 'detail-table');
  const header = el('tr');
  for (const [label, cls] of [['Terminal', null], ['Where', null], ['Space', null],
    [detailView.buying ? 'Buy' : 'Sell', 'num'], ['vs best', 'num'],
    [detailView.buying ? 'In stock' : 'Wanted', 'num'], ['Seen', 'num']]) {
    header.append(el('th', cls, label));
  }
  table.append(header);

  for (const row of rows.slice(0, 40)) {
    const tr = el('tr');
    const price = priceOf(row);
    const scu = Math.round(scuOf(row));

    const name = el('td');
    const jump = el('button', 'place-link', row.terminal);
    jump.title = row.placeId ? 'Show it on the map' : 'The map cannot place this terminal';
    jump.disabled = !row.placeId;
    jump.addEventListener('click', () => {
      showView('map');
      centreOnTerminal(row.terminal, row.placeId);
    });
    name.append(jump);
    tr.append(name);

    tr.append(el('td', 'muted', row.place || '—'));

    const space = el('td');
    space.append(el('span', `sec sec-${row.security}`, row.security));
    if (row.system) space.append(el('span', 'muted sec-system', ` ${row.system}`));
    tr.append(space);

    tr.append(el('td', 'num', money(price)));

    // What choosing this one costs against the best on offer, which is the
    // number the single "best price" column never showed.
    const gap = detailView.buying ? (price - best) / best : (best - price) / best;
    tr.append(el('td', gap > 0.001 ? 'num outward' : 'num muted',
      gap > 0.001 ? `−${(gap * 100).toFixed(0)}%` : 'best'));

    const stock = el('td', 'num');
    if (scu > 0) {
      stock.append(el('span', scu < THIN_SCU ? 'outward' : null, `${scu.toLocaleString()} SCU`));
      if (scu < THIN_SCU) {
        stock.title = detailView.buying
          ? 'Thin stock — a full hold will not be filled here'
          : 'Small demand — this counter will not take a full hold';
      }
    } else {
      stock.append(el('span', 'muted', '—'));
    }
    tr.append(stock);

    tr.append(el('td', 'num muted', row.seenAt ? ago(row.seenAt) : '—'));
    table.append(tr);
  }

  cell.append(table);

  const shown = Math.min(rows.length, 40);
  const note = detailView.monitoredOnly
    ? `${shown} of ${all.length} counters — lawless space hidden.`
    : `${shown} of ${all.length} counters.`;

  cell.append(el('div', 'muted detail-note',
    `${note} Security is by system: Stanton is policed, Pyro and Nyx are not. `
    + 'Nothing in the game logs threat, so this is where a place is, not what happened there.'));
}

function renderMarket() {
  $('#market-offer').hidden = marketEntries.length > 0;

  const term = ($('#market-search').value || '').trim().toLowerCase();
  const group = $('#market-group').value;
  const body = $('#market-table tbody');
  body.textContent = '';

  const rows = marketEntries.filter((e) =>
    (!group || e.groups.includes(group))
    && (!term
      || e.name.toLowerCase().includes(term)
      || e.groups.some((g) => g.toLowerCase().includes(term))));

  // Say how many matched, so picking a group visibly does something without
  // scrolling to the table to find out.
  const counter = $('#market-count');
  if (counter) {
    counter.textContent = marketEntries.length && (group || term)
      ? `${rows.length} of ${marketEntries.length}`
      : '';
  }

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted', marketEntries.length
      ? 'No commodities match that search.'
      : 'Enable the community dataset on the Settings page to fill this in.');
    td.colSpan = 8;
    tr.append(td);
    body.append(tr);
    return;
  }

  // The UEX column exists only while that integration is on.
  const anyUex = marketEntries.some((e) => e.uex);
  $('.uex-col').hidden = !anyUex;

  for (const entry of rows) {
    const tr = el('tr');

    // The name is the way in: one best price is an answer, and the rows behind
    // it are the choice.
    const nameCell = el('td');
    const opener = el('button', 'place-link commodity-open', entry.name);
    opener.title = 'Every counter that trades this, and what each one costs you';
    opener.addEventListener('click', () => toggleMarketDetail(entry, tr));
    nameCell.append(opener);

    // The row below answers "where"; the page answers "and what has it been
    // doing". Kept as a second control so the quick look stays one click.
    const drill = el('button', 'drill', '↗');
    drill.title = 'Open the full picture: price and demand over time';
    drill.setAttribute('aria-label', `Open ${entry.name} in full`);
    drill.addEventListener('click', () => openCommodity(entry.name));
    nameCell.append(drill);

    tr.append(nameCell);
    tr.append(el('td', 'muted', entry.groups.join(', ')));
    tr.append(el('td', 'num', entry.sold.length ? String(entry.sold.length) : '—'));
    tr.append(el('td', 'num', entry.bought.length ? String(entry.bought.length) : '—'));
    tr.append(el('td', 'num', entry.myScuSold ? entry.myScuSold.toLocaleString() : '—'));
    tr.append(el('td', 'num inward', entry.myRevenue ? money(entry.myRevenue) : '—'));

    if (anyUex) {
      const cell = el('td', 'num');
      if (entry.uex?.bestSell > 0) {
        cell.append(el('span', 'inward', money(entry.uex.bestSell)));

        // Against the 15-day average: is now a good moment to sell this?
        if (entry.uex.avgSell > 0) {
          const trend = ((entry.uex.bestSell - entry.uex.avgSell) / entry.uex.avgSell) * 100;
          if (Math.abs(trend) >= 1) {
            const arrow = el('span', trend > 0 ? 'inward' : 'outward',
              ` ${trend > 0 ? '▲' : '▼'}${Math.abs(trend).toFixed(0)}%`);
            arrow.title = `15-day average: ${money(entry.uex.avgSell)}/SCU`;
            cell.append(arrow);
          }
        }

        if (entry.uex.bestSellTerminal)
          cell.append(el('div', 'muted uex-terminal', entry.uex.bestSellTerminal));

        // How old the number is - the difference between a price and a rumour.
        // Ambers past three days, reds past two weeks.
        if (showPriceAge && entry.uex.seenAt) {
          const age = Date.now() - new Date(entry.uex.seenAt).getTime();
          const cls = age > 14 * 86400000 ? 'price-age stale'
            : age > 3 * 86400000 ? 'price-age old'
            : 'price-age';
          cell.append(el('div', cls, `seen ${ago(entry.uex.seenAt)}`));
        }
      } else {
        cell.textContent = '—';
      }
      tr.append(cell);
    }

    // Two map links, because "where is it" has two answers: where you can
    // sell what you hold, and where the shops stock it for buying.
    const actions = el('td', 'num map-links');

    const link = (label, query, title) => {
      const show = el('button', 'ghost', label);
      show.title = title;
      show.addEventListener('click', () => {
        $('#map-search').value = query;
        showView('map');
        drawMap();
      });
      actions.append(show);
    };

    if (entry.sold.length)
      link('sell on map', entry.name, 'Light every place that buys this from you');
    if (entry.bought.length)
      link('buy on map', `buy:${entry.name}`, 'Light every place that stocks this for sale');

    actions.append(trackButton(entry.name, 1, 'SCU'));
    tr.append(actions);

    body.append(tr);
  }
}

onInput('#market-search', renderMarket);
$('#market-group')?.addEventListener('change', renderMarket);

// The shortcut: re-fetch the whole UEX snapshot from right here.
$('#market-refresh')?.addEventListener('click', (e) =>
  uexAction('/api/uex/enable', $('#market-age'), e.currentTarget));

// The Settings switch for price ages, live on both pages at once.
const uexAgeToggle = $('#uex-age-toggle');
if (uexAgeToggle) {
  uexAgeToggle.checked = showPriceAge;
  uexAgeToggle.addEventListener('change', () => {
    showPriceAge = uexAgeToggle.checked;
    try { localStorage.setItem('qw-uex-age', showPriceAge ? '1' : '0'); } catch { /* fine */ }
    renderMarket();
  });
}

/**
 * Facility keys to search tokens the atlas can meet. The class name
 * DC_Stan_Hurston_S1_Farnesway carries exactly one atlas-matchable word;
 * everything else is scaffolding, listed here so it never becomes a token.
 */
const FACILITY_NOISE = new Set([
  'dc', 'outpost', 'reststop', 'landingzone', 'junksite', 'ugf', 'hangar',
  'spaceport', 'portolisar', 'inhabited', 'planet', 'distributioncentre',
  'stan', 'stanton', 'pyro', 'nyx', 'outlaw', 'sublocation',
  'hurston', 'microtech', 'crusader', 'arccorp',
  'admin', 'gate', 'habs', 'medical', 'metrostation', 'store', 'lobby',
]);

function facilityTokens(keys) {
  const tokens = new Set();

  for (const key of keys) {
    for (const part of key.split('_')) {
      const compact = part.toLowerCase().replace(/[^a-z0-9]/g, '');
      if (compact.length >= 4 && !FACILITY_NOISE.has(compact) && !/^s\d+$/.test(compact))
        tokens.add(compact);
    }
  }

  return tokens;
}

/**
 * If the map search term names a commodity, the nodes to light are the ones
 * whose name matches a facility token for that commodity. Null when the term
 * is not a commodity, in which case the search means places, as it always did.
 *
 * A "buy:" prefix flips the question - where the commodity is stocked for
 * buying, rather than where it can be sold. The Market page's two map links
 * write both forms into the search box, so they survive as shareable ?q= urls.
 */
/**
 * The commodity a typed term means.
 *
 * Exact names only was too strict to use: "medical" found nothing because the
 * commodity is "Medical Supplies", and nothing on the map offered the full
 * name, so the search looked broken. A prefix or a contained word now counts,
 * shortest name first so "gold" means Gold rather than Golden Medmon - but
 * only when no place answers to the same term, since a typed place name
 * should still find the place.
 */
function matchCommodity(name) {
  const lower = name.trim().toLowerCase();
  if (!lower) return null;

  const exact = marketEntries.find((e) => e.name.toLowerCase() === lower);
  if (exact) return exact;

  if (atlas.some((l) => l.name.toLowerCase().includes(lower)))
    return null;

  const byLength = (a, b) => a.name.length - b.name.length;

  return marketEntries.filter((e) => e.name.toLowerCase().startsWith(lower)).sort(byLength)[0]
    ?? marketEntries.filter((e) => e.name.toLowerCase().includes(lower)).sort(byLength)[0]
    ?? null;
}

function commoditySites(term) {
  const buying = term.startsWith('buy:');
  const entry = matchCommodity(buying ? term.slice(4) : term);
  if (!entry) return null;

  const keys = buying ? entry.bought : entry.sold;
  return keys.length ? facilityTokens(keys) : null;
}

/* ---------- loot ---------- */

async function loadLoot() {
  const days = Number($('#loot-period').value) || 0;
  renderLoot(await getJson(`/api/loot?days=${days}`));
}

/**
 * Fills a filter with what the rows actually contain.
 *
 * Only what is there, rather than every category the classifier knows: a
 * dropdown offering "Containers" on an install that has never seen one is a
 * filter that can only disappoint. The chosen value is kept across a redraw
 * when it still exists, and quietly falls back to everything when it does not -
 * which happens when the date window moves and takes the last of something
 * with it.
 */
function fillLootFilter(select, all, label) {
  if (!select) return '';

  const chosen = select.value;
  const values = [...new Set(all.filter(Boolean))].sort((a, b) => a.localeCompare(b));

  select.textContent = '';

  const any = document.createElement('option');
  any.value = '';
  any.textContent = label;
  select.append(any);

  for (const value of values) {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = value;
    select.append(option);
  }

  select.value = values.includes(chosen) ? chosen : '';
  return select.value;
}

function renderLoot(pickups) {
  const term = ($('#loot-search').value || '').trim().toLowerCase();

  // Built from everything the window holds, not from what survives the other
  // filters - or choosing a kind would empty the place list and strand you.
  const kind = fillLootFilter($('#loot-kind'), pickups.map((p) => p.category), 'Any kind');
  const place = fillLootFilter($('#loot-place'), pickups.map((p) => p.place), 'Anywhere');

  const rows = pickups.filter((p) =>
    (!term || p.item.toLowerCase().includes(term) || p.place.toLowerCase().includes(term))
    && (!kind || p.category === kind)
    && (!place || p.place === place));

  tiles('#loot-summary', [
    ['New items', rows.length],
    ['Last 7 days', rows.filter((p) => Date.now() - new Date(p.at).getTime() < 7 * 86400000).length],
    ['Places', new Set(rows.map((p) => p.place)).size],
  ]);

  const body = $('#loot-table tbody');
  body.textContent = '';

  if (!rows.length) {
    const tr = el('tr');

    // Name the filter that emptied it. "Nothing in that range" is the wrong
    // explanation for a table a dropdown hid every row from.
    const td = el('td', 'muted', kind || place
      ? `Nothing matching ${[kind, place].filter(Boolean).join(' at ')} in that range.`
      : 'Nothing in that range.');

    td.colSpan = 4;
    tr.append(td);
    body.append(tr);
    lastLootRows = pickups;
    return;
  }

  for (const pickup of rows) {
    const tr = el('tr');
    tr.append(el('td', null, dateOf(pickup.at)));
    tr.append(el('td', null, prettyItem(pickup.item)));
    tr.append(el('td', 'muted', pickup.category));
    tr.append(tdPlace(pickup.place, 'muted'));
    body.append(tr);
  }

  lastLootRows = pickups;
}

let lastLootRows = [];

onInput('#loot-search', () => renderLoot(lastLootRows));
onInput('#loot-period', loadLoot);

// The two filters redraw what is already loaded; only the date window refetches.
$('#loot-kind')?.addEventListener('change', () => renderLoot(lastLootRows));
$('#loot-place')?.addEventListener('change', () => renderLoot(lastLootRows));

/* ---------- reference catalogues: ships, parts ---------- */

let shipCatalogue = [];
let partCatalogue = [];

async function loadShipsRef() {
  try {
    shipCatalogue = await getJson('/api/reference/ships');
  } catch {
    shipCatalogue = [];
  }

  const careers = [...new Set(shipCatalogue.map((s) => s.career).filter(Boolean))].sort();
  const select = $('#ships-career');
  const previous = select.value;

  select.textContent = '';
  select.append(new Option('All careers', ''));
  for (const career of careers) select.append(new Option(career, career));
  if (careers.includes(previous)) select.value = previous;

  renderShipsRef();
}

function renderShipsRef() {
  const term = ($('#ships-search').value || '').trim().toLowerCase();
  const career = $('#ships-career').value;
  const body = $('#ships-table tbody');
  body.textContent = '';

  const rows = shipCatalogue.filter((s) =>
    (!career || s.career === career)
    && (!term || s.name.toLowerCase().includes(term)
      || (s.role || '').toLowerCase().includes(term)));

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted', shipCatalogue.length
      ? 'No ships match that filter.'
      : 'Enable the community dataset on the Settings page to fill this in.');
    td.colSpan = 13;
    tr.append(td);
    body.append(tr);
    return;
  }

  for (const ship of rows) {
    const tr = el('tr');
    const name = el('td', null, ship.name);
    if (!ship.isSpaceship) name.append(el('span', 'note-inline', ' · ground'));
    tr.append(name);

    tr.append(el('td', 'muted', ship.career ?? '—'));
    tr.append(el('td', 'muted', ship.role ?? '—'));
    tr.append(el('td', 'num', ship.crew > 0 ? String(ship.crew) : '—'));

    // The spec sheet: zeros mean a cache digested before the fields existed,
    // shown as dashes rather than lies.
    tr.append(el('td', 'num', ship.cargoScu > 0 ? `${ship.cargoScu} SCU` : '—'));
    tr.append(el('td', 'num muted', ship.scmSpeed > 0
      ? `${Math.round(ship.scmSpeed)} / ${Math.round(ship.maxSpeed)}`
      : '—'));
    tr.append(el('td', 'num muted', ship.shieldHp > 0 ? ship.shieldHp.toLocaleString() : '—'));
    tr.append(el('td', 'num muted', ship.health > 0 ? ship.health.toLocaleString() : '—'));

    tr.append(el('td', 'num muted', ship.expeditedCost > 0 ? money(ship.expeditedCost) : '—'));
    tr.append(el('td', 'num muted', ship.standardClaimTime > 0 ? `~${Math.round(ship.standardClaimTime)}m` : '—'));
    tr.append(el('td', ship.price ? 'num' : 'num muted', ship.price ? money(ship.price.price) : 'not sold'));
    tr.append(el('td', 'muted', ship.price?.terminal ?? '—'));

    // Rentals only exist when that feed is on; otherwise the column is dashes.
    const rent = el('td', ship.rental ? 'num' : 'num muted',
      ship.rental ? money(ship.rental.price) : '—');
    if (ship.rental) rent.title = `at ${ship.rental.terminal}`;
    tr.append(rent);

    body.append(tr);
  }
}

async function loadPartsRef() {
  try {
    partCatalogue = await getJson('/api/reference/items');
  } catch {
    partCatalogue = [];
  }

  const types = [...new Set(partCatalogue.map((p) => p.type).filter(Boolean))].sort();
  const select = $('#parts-type');
  const previous = select.value;

  select.textContent = '';
  select.append(new Option('All types', ''));
  for (const type of types) select.append(new Option(prettyType(type), type));
  if (types.includes(previous)) select.value = previous;

  renderPartsRef();
}

/** "Char_Clothing_Hat" -> "Clothing Hat": the digest's type keys, made legible. */
const prettyType = (type) => (type ? type.replace(/^Char_/, '').replace(/_/g, ' ') : '—');

/** The digest holds thousands of items; rendering caps so the page stays quick. */
const PARTS_CAP = 500;

function renderPartsRef() {
  const term = ($('#parts-search').value || '').trim().toLowerCase();
  const type = $('#parts-type').value;
  const body = $('#parts-table tbody');
  body.textContent = '';

  const rows = partCatalogue.filter((p) =>
    (!type || p.type === type)
    && (!term
      || p.className.toLowerCase().includes(term)
      || (p.name || '').toLowerCase().includes(term)
      || prettyItem(p.className).toLowerCase().includes(term)
      || (p.manufacturer || '').toLowerCase().includes(term)))

    // Priced items first: the ones a shop actually stocks are the ones worth
    // reading about; header sorting still reorders freely.
    .sort((a, b) => Number(Boolean(b.price)) - Number(Boolean(a.price))
      || a.className.localeCompare(b.className));

  const counter = $('#parts-count');
  counter.textContent = rows.length > PARTS_CAP
    ? `Showing ${PARTS_CAP.toLocaleString()} of ${rows.length.toLocaleString()} matches — refine the search or pick a type.`
    : '';

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted', partCatalogue.length
      ? 'Nothing matches that filter.'
      : 'Enable the community dataset on the Settings page to fill this in.');
    td.colSpan = 9;
    tr.append(td);
    body.append(tr);
    return;
  }

  for (const part of rows.slice(0, PARTS_CAP)) {
    const tr = el('tr');

    // The localised name when the data carries one; my class-name
    // prettification is the fallback, not the headline. The track button
    // shares the cell rather than claiming a tenth column.
    const shown = part.name || prettyItem(part.className);
    const label = el('td', 'with-track');
    label.append(el('span', null, shown));
    label.append(trackButton(shown));
    if (part.name) label.title = part.className;
    tr.append(label);
    tr.append(el('td', 'muted', prettyType(part.type)));
    tr.append(el('td', 'muted', part.subType ?? '—'));
    tr.append(el('td', 'num', part.size > 0 ? String(part.size) : '—'));
    tr.append(el('td', 'num', part.grade > 0 ? String(part.grade) : '—'));
    tr.append(el('td', 'muted', part.manufacturer ?? '—'));
    tr.append(el('td', part.price ? 'num' : 'num muted', part.price ? money(part.price) : '—'));

    // Where a part is actually stocked; the full shop list rides the tooltip.
    const stocked = el('td', 'num muted', part.stockedAt > 0 ? String(part.stockedAt) : '—');
    if (part.terminals?.length) stocked.title = part.terminals.join('\n');
    tr.append(stocked);
    tr.append(el('td', 'muted', part.cheapestAt ?? '—'));

    body.append(tr);
  }
}

onInput('#ships-search', renderShipsRef);
$('#ships-career')?.addEventListener('change', renderShipsRef);
onInput('#parts-search', renderPartsRef);
$('#parts-type')?.addEventListener('change', renderPartsRef);

/* ---------- mining and salvage spawns ---------- */

let miningCatalogue = [];

const KIND_LABELS = {
  mineable: 'Mineable',
  cave_harvestable: 'Cave harvestable',
  harvestable: 'Harvestable',
  salvageable: 'Salvageable',
};

async function loadMiningRef() {
  try {
    miningCatalogue = await getJson('/api/reference/resources');
  } catch {
    miningCatalogue = [];
  }

  const fill = (selector, values, allLabel, labelOf = (v) => v) => {
    const select = $(selector);
    const previous = select.value;
    select.textContent = '';
    select.append(new Option(allLabel, ''));
    for (const value of values) select.append(new Option(labelOf(value), value));
    if (values.includes(previous)) select.value = previous;
  };

  fill('#mining-kind',
    [...new Set(miningCatalogue.map((s) => s.kind))].sort(),
    'All kinds', (k) => KIND_LABELS[k] || k);

  fill('#mining-system',
    [...new Set(miningCatalogue.map((s) => s.system).filter(Boolean))].sort(),
    'All systems');

  renderMiningRef();
}

const MINING_CAP = 500;

function renderMiningRef() {
  const term = ($('#mining-search').value || '').trim().toLowerCase();
  const kind = $('#mining-kind').value;
  const system = $('#mining-system').value;
  const body = $('#mining-table tbody');
  body.textContent = '';

  const rows = miningCatalogue.filter((s) =>
    (!kind || s.kind === kind)
    && (!system || s.system === system)
    && (!term
      || s.resource.toLowerCase().includes(term)
      || s.location.toLowerCase().includes(term)
      || (s.deposit || '').toLowerCase().includes(term)))

    // Payers first, then the likeliest finds - the order a miner plans in.
    .sort((a, b) => (b.bestSell ?? 0) - (a.bestSell ?? 0)
      || (b.groupChance * b.share) - (a.groupChance * a.share));

  const counter = $('#mining-count');
  counter.textContent = rows.length > MINING_CAP
    ? `Showing ${MINING_CAP.toLocaleString()} of ${rows.length.toLocaleString()} matches — refine the search or filters.`
    : '';

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted', miningCatalogue.length
      ? 'Nothing matches that filter.'
      : 'Enable the community dataset on the Settings page to fill this in.');
    td.colSpan = 11;
    tr.append(td);
    body.append(tr);
    return;
  }

  const percent = (v) => `${(v * 100).toFixed(v * 100 >= 10 ? 0 : 1)}%`;

  for (const spawn of rows.slice(0, MINING_CAP)) {
    const tr = el('tr');

    // The track button rides in the name cell here: the table already carries
    // eleven columns and a twelfth falls off the screen.
    const name = el('td', 'with-track');
    name.append(el('span', null, spawn.resource));
    name.append(trackButton(spawn.resource, 1, spawn.kind === 'mineable' ? 'SCU' : ''));
    tr.append(name);
    tr.append(el('td', 'muted', spawn.deposit ?? '—'));
    tr.append(el('td', 'muted', KIND_LABELS[spawn.kind] || spawn.kind));
    tr.append(tdPlace(spawn.location));
    tr.append(el('td', 'muted', spawn.system ?? '—'));
    tr.append(el('td', 'muted', spawn.group));
    tr.append(el('td', 'num', percent(spawn.groupChance)));
    tr.append(el('td', 'num muted', percent(spawn.share)));

    const sell = el('td', spawn.bestSell ? 'num inward' : 'num muted',
      spawn.bestSell ? money(spawn.bestSell) : '—');
    if (spawn.bestSellTerminal) sell.title = `at ${spawn.bestSellTerminal}`;
    tr.append(sell);

    // Raw ore price and refinery yield: both from optional feeds, so both are
    // dashes until those are switched on.
    const raw = el('td', spawn.rawSell ? 'num' : 'num muted',
      spawn.rawSell ? money(spawn.rawSell) : '—');
    if (spawn.rawTerminal) raw.title = `at ${spawn.rawTerminal}`;
    tr.append(raw);

    const yieldCell = el('td', spawn.refineryYield ? 'num' : 'num muted',
      spawn.refineryYield ? `${spawn.refineryYield.toFixed(0)}%` : '—');
    if (spawn.refineryTerminal) yieldCell.title = `best at ${spawn.refineryTerminal}`;
    tr.append(yieldCell);

    body.append(tr);
  }
}

onInput('#mining-search', renderMiningRef);
$('#mining-kind')?.addEventListener('change', renderMiningRef);
$('#mining-system')?.addEventListener('change', renderMiningRef);

/* ---------- crafting blueprints ---------- */

let craftingCatalogue = [];

async function loadCraftingRef() {
  try {
    craftingCatalogue = await getJson('/api/reference/blueprints');
  } catch {
    craftingCatalogue = [];
  }

  const types = [...new Set(craftingCatalogue.map((b) => b.type).filter(Boolean))].sort();
  const select = $('#crafting-type');
  const previous = select.value;

  select.textContent = '';
  select.append(new Option('All types', ''));
  for (const type of types) select.append(new Option(prettyType(type), type));
  if (types.includes(previous)) select.value = previous;

  renderCraftingRef();
}

const CRAFTING_CAP = 500;

/** "540 s" is nobody's unit; craft times read as minutes and hours. */
function craftTime(seconds) {
  if (seconds <= 0) return '—';
  if (seconds < 3600) return `${Math.round(seconds / 60)}m`;
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.round((seconds % 3600) / 60);
  return minutes ? `${hours}h ${minutes}m` : `${hours}h`;
}

function renderCraftingRef() {
  const term = ($('#crafting-search').value || '').trim().toLowerCase();
  const type = $('#crafting-type').value;
  const obtained = $('#crafting-obtained').value;
  const body = $('#crafting-table tbody');
  body.textContent = '';

  const rows = craftingCatalogue.filter((b) =>
    (!type || b.type === type)
    && (!obtained
      || (obtained === 'owned' && b.owned)
      || (obtained === 'default' && b.default)
      || (obtained === 'reward' && !b.default))
    && (!term
      || b.output.toLowerCase().includes(term)
      || b.materials.some((m) => m.toLowerCase().includes(term))));

  const counter = $('#crafting-count');
  counter.textContent = rows.length > CRAFTING_CAP
    ? `Showing ${CRAFTING_CAP.toLocaleString()} of ${rows.length.toLocaleString()} matches — refine the search or filters.`
    : '';

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted', craftingCatalogue.length
      ? 'Nothing matches that filter.'
      : 'Enable the community dataset on the Settings page to fill this in.');
    td.colSpan = 8;
    tr.append(td);
    body.append(tr);
    return;
  }

  // Yours first: a blueprint you hold is one you can actually start.
  rows.sort((a, b) => Number(Boolean(b.owned)) - Number(Boolean(a.owned)));

  for (const bp of rows.slice(0, CRAFTING_CAP)) {
    const tr = el('tr');

    const makes = el('td');
    makes.append(el('span', null, bp.output));

    if (bp.owned) {
      const badge = el('span', 'job-kind owned', 'yours');
      badge.title = `Received ${relative(bp.receivedAt)}`;
      makes.append(badge);
    }
    tr.append(makes);
    tr.append(el('td', 'muted', prettyType(bp.type)));
    tr.append(el('td', 'num', bp.grade > 0 ? String(bp.grade) : '—'));
    tr.append(el('td', 'num muted', craftTime(bp.craftSeconds)));
    tr.append(el('td', 'muted materials', bp.materials.length ? bp.materials.join(', ') : '—'));

    // How you get the blueprint; the pool names ride the tooltip.
    const how = el('td', 'muted', bp.default
      ? 'Known by default'
      : bp.rewardPools.length
        ? `${bp.rewardPools.length} reward pool${bp.rewardPools.length === 1 ? '' : 's'}`
        : '—');
    if (bp.rewardPools.length) how.title = bp.rewardPools.join('\n');
    tr.append(how);

    tr.append(el('td', bp.shopPrice ? 'num' : 'num muted',
      bp.shopPrice ? money(bp.shopPrice) : 'not sold'));

    // Turning a recipe into a job is the whole point of having both pages.
    const plan = el('td', 'num');
    if (bp.materials.length) {
      const button = el('button', 'ghost', 'Plan');
      button.title = 'Track this build on the Jobs page';

      button.addEventListener('click', async () => {
        button.disabled = true;
        button.textContent = 'planned';

        await fetch('/api/jobs', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            title: `Craft ${bp.output}`,
            kind: 'craft',
            source: `${bp.output} blueprint · ${craftTime(bp.craftSeconds)} to craft`,
            items: parseJobItems(bp.materials.join('\n')),
          }),
        }).catch(() => { button.textContent = 'failed'; });
      });

      plan.append(button);
    }
    tr.append(plan);

    body.append(tr);
  }
}

onInput('#crafting-search', renderCraftingRef);
$('#crafting-type')?.addEventListener('change', renderCraftingRef);
$('#crafting-obtained')?.addEventListener('change', renderCraftingRef);

/* ---------- overlay layout ---------- */

/** Pretty names for the views and cards the layout page offers. */
const OVERLAY_LABELS = {
  now: 'Now', jobs: 'Jobs', map: 'Map', commodities: 'Cargo', market: 'Market',
  loadout: 'Loadout', stash: 'Stash', logbook: 'Logbook', fleet: 'Fleet', places: 'Places',
  location: 'Location', ship: 'Ship', session: 'Session', handle: 'Handle',
  feed: 'Live feed', stats: 'This session', job: 'Job in hand', trade: 'Trade from here',
  checklist: 'Checklist',
};

/**
 * The overlay layout editor, on its own page under Settings. Saving is
 * immediate - there is no Save button because there is nothing to lose by a
 * tick going straight through, and the widget picks it up on its next poll.
 */
async function renderOverlayLayout() {
  let data;
  try {
    data = await getJson('/api/overlay/layout');
  } catch {
    return;
  }

  const draw = (host, names, chosen) => {
    const node = $(host);
    node.textContent = '';

    for (const name of names) {
      const label = el('label', 'toggle');
      const box = document.createElement('input');
      box.type = 'checkbox';
      box.value = name;
      box.checked = chosen.includes(name);
      box.addEventListener('change', saveOverlayLayout);

      label.append(box);
      label.append(el('span', null, OVERLAY_LABELS[name] || name));
      node.append(label);
    }
  };

  draw('#overlay-tabs', data.tabs, data.current.tabs);
  draw('#overlay-cards', data.cards, data.current.cards);

  for (const radio of $$('#overlay-density input')) {
    radio.checked = radio.value === data.current.density;
    radio.onchange = saveOverlayLayout;
  }

  $('#overlay-layout-status').textContent = '';
}

$('#install-path-save')?.addEventListener('click', async (e) => {
  const status = $('#install-path-status');
  const path = $('#install-path').value.trim();

  if (!path) {
    status.textContent = 'Type the folder first.';
    return;
  }

  e.currentTarget.disabled = true;
  status.textContent = 'checking…';

  try {
    const response = await fetch('/api/install/path', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path }),
    });

    const result = await response.json();

    status.textContent = response.ok
      ? `Found ${result.channel}. Restart Quantum Wake to read it.`
      : result.message || 'That folder does not hold Star Citizen logs.';

    if (response.ok) status.classList.add('inward');
  } catch {
    status.textContent = 'Could not reach the server.';
  } finally {
    e.currentTarget.disabled = false;
  }
});

$('#overlay-reload')?.addEventListener('click', async (e) => {
  const status = $('#overlay-layout-status');
  e.currentTarget.disabled = true;

  try {
    await fetch('/api/overlay/reload', { method: 'POST' });
    status.textContent = 'the widget will reload within a few seconds';
  } catch {
    status.textContent = 'could not reach the server';
  } finally {
    e.currentTarget.disabled = false;
  }
});

async function saveOverlayLayout() {
  const pick = (host) => $$(`${host} input:checked`).map((b) => b.value);
  const density = $$('#overlay-density input').find((r) => r.checked)?.value || 'normal';

  const status = $('#overlay-layout-status');
  status.textContent = 'saving…';

  try {
    await fetch('/api/overlay/layout', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tabs: pick('#overlay-tabs'), cards: pick('#overlay-cards'), density }),
    });
    status.textContent = 'saved';
  } catch {
    status.textContent = 'could not save';
  }
}

/**
 * In the widget, applies the chosen layout: which tabs appear, which Now cards
 * appear, and the type scale. Polled rather than pushed, because the overlay
 * is a separate browser and a few seconds of lag costs nothing.
 */
let lastReloadToken = null;

async function applyOverlayLayout() {
  let data;
  try {
    data = await getJson('/api/overlay/layout');
  } catch {
    return;
  }

  // A reload asked for from the dashboard: anything a fresh page load would
  // pick up, without hunting for the widget's window.
  if (lastReloadToken !== null && data.reloadToken !== lastReloadToken) {
    location.reload();
    return;
  }
  lastReloadToken = data.reloadToken;

  const layout = data.current;

  const expanded = document.body.classList.contains('expanded');

  for (const button of $$('#tabs button[data-view]'))
    button.hidden = !expanded && !layout.tabs.includes(button.dataset.view);

  for (const card of $$('#view-now [data-card]')) {
    const wanted = expanded || layout.cards.includes(card.dataset.card);
    card.classList.toggle('layout-off', !wanted);
  }

  document.body.classList.remove('density-compact', 'density-tiny');
  if (layout.density !== 'normal') document.body.classList.add(`density-${layout.density}`);

  // The active view may have just been hidden; fall back to the first shown.
  const active = $('#tabs button.active[data-view]');
  if (active?.hidden) {
    const first = $$('#tabs button[data-view]').find((b) => !b.hidden);
    if (first) showView(first.dataset.view);
  }
}

/* ---------- trade routes ---------- */

/**
 * The route planner. UEX ranks margins; this ranks runs - the difference is
 * a hold and a wallet, which are the two things only your own logs know.
 */
let routeRequest = 0;

async function loadRoutes() {
  const select = $('#routes-ship');
  if (!select) return;

  // The ship list is the owned, ticked fleet with a known cargo grid.
  if (libraryStats && !select.dataset.filled) {
    const ships = libraryStats.ships
      .filter((s) => !excludedShips.has(s.name) && s.reference?.cargoScu > 0)
      .sort((a, b) => b.reference.cargoScu - a.reference.cargoScu);

    select.textContent = '';
    select.append(new Option('On foot / no hold', '0'));

    for (const ship of ships)
      select.append(new Option(`${ship.name} · ${ship.reference.cargoScu} SCU`, ship.reference.cargoScu));

    if (ships.length) select.selectedIndex = 1;
    select.dataset.filled = '1';
  }

  const scu = Number(select.value) || 0;
  const capital = Number($('#routes-capital').value) || 0;
  const ranking = $('#routes-ranking').value || 'reliable';
  const freshOnly = $('#routes-fresh-only').checked;
  const evidence = $('#routes-evidence').value || 'reported';
  // "From here" reads the live location the Now page is already showing.
  const here = $('#now-location').textContent.trim();
  const from = $('#routes-here').checked && here && here !== '—' && !here.startsWith('In menus')
    ? here
    : '';
  const originNote = $('#routes-origin-note');
  originNote.textContent = from
    ? ` Origin: ${from}; results require UEX to match this terminal.`
    : '';

  const body = $('#routes-table tbody');
  body.textContent = '';
  const request = ++routeRequest;

  let rows = [];
  try {
    rows = await getJson(
      `/api/routes?scu=${scu}&capital=${capital}&from=${encodeURIComponent(from)}`
      + `&ranking=${encodeURIComponent(ranking)}&freshOnly=${freshOnly}`
      + `&evidence=${encodeURIComponent(evidence)}`);
  } catch { /* UEX off */ }

  // The initial per-SCU request often leaves before the fleet has loaded. It
  // must not win the race back and overwrite the later request for the ship
  // now shown in the selector.
  if (request !== routeRequest) return;

  if (!rows.length) {
    const tr = el('tr');
    // Name the filter that emptied the table. "No route from here" is the
    // wrong explanation for a table that a tickbox hid every row from, and
    // there are now two tickboxes that can do it.
    const td = el('td', 'muted', freshOnly
      ? 'Nothing quoted in the last day. Untick "Fresh only" to see older prices.'
      : from
        ? 'No route starts from where you are - or UEX has no terminal here.'
        : evidence === 'full'
          ? 'No route has both sides reporting enough capacity for this load. Try Reported capacity or Include unknown capacity.'
          : evidence === 'reported'
            ? 'No route has stock and demand reported on both sides. Try Include unknown capacity to see price-only estimates.'
            : 'Nothing to show. Enable UEX prices on the Settings page.');
    td.colSpan = 12;
    tr.append(td);
    body.append(tr);
    return;
  }

  for (const route of rows) {
    const tr = el('tr');
    tr.append(el('td', null, route.commodity));
    tr.append(tdPlace(route.buyAt, 'muted'));
    tr.append(el('td', 'num muted', money(route.buyPrice)));
    tr.append(tdPlace(route.sellAt, 'muted'));
    tr.append(el('td', 'num muted', money(route.sellPrice)));
    tr.append(el('td', 'num', `+${money(route.marginPerScu)}`));
    tr.append(el('td', 'num', Math.floor(route.units).toLocaleString()));
    tr.append(el('td', 'num outward', money(route.outlay)));
    const projected = el('td', 'num inward', `~${money(route.profit)}`);
    projected.title = 'Arithmetic from the two UEX prices, not a promise of live availability.';
    tr.append(projected);

    const report = el('td', 'route-report');
    const availabilityWord = route.availability === 'reported-full'
      ? `Reported full load · ${Math.floor(route.desiredUnits).toLocaleString()} SCU`
      : route.availability === 'reported-partial'
        ? `Reported partial · ${Math.floor(route.units).toLocaleString()} / ${Math.floor(route.desiredUnits).toLocaleString()} SCU`
        : `Capacity unknown · projected ${Math.floor(route.units).toLocaleString()} SCU`;
    report.append(el('div', `route-feasibility ${route.availability || 'capacity-unknown'}`, availabilityWord));
    const reportWord = route.freshness === 'fresh' ? 'Fresh reports'
      : route.freshness === 'aging' ? 'Aging reports'
        : route.freshness === 'stale' ? 'Stale reports' : 'Report age unknown';
    report.append(el('div', `route-freshness ${route.freshness || 'unknown'}`, reportWord));
    const age = (at) => at ? ago(at) : 'unknown';
    report.append(el('div', 'muted route-age', `Buy ${age(route.buySeenAt)} · sell ${age(route.sellSeenAt)}`));
    const capacity = [];
    capacity.push(route.buyStockScu > 0
      ? `buy stock ${Math.floor(route.buyStockScu)} SCU (${route.buyAvailability})`
      : 'buy stock unknown');
    capacity.push(route.sellDemandScu > 0
      ? `sell demand ${Math.floor(route.sellDemandScu)} SCU (${route.sellAvailability})`
      : 'sell demand unknown');
    report.append(el('div', 'muted route-capacity', capacity.join(' · ')));
    if ((route.freshness !== 'fresh' || route.limitedBy === 'demand') && route.fallbackSells?.length) {
      const choices = route.fallbackSells.map((fallback) =>
        `${fallback.terminal} ${money(fallback.sellPrice)} (${fallback.freshness || 'unknown'})`).join(' · ');
      report.append(el('div', 'route-fallback', `Fallback: ${choices}`));
    }
    tr.append(report);

    // One click turns a haul into a plan: buy there, sell there, in order.
    const plan = el('td');
    const button = el('button', 'ghost tiny', route.mapReady ? 'Plan' : 'Text plan');
    button.title = route.mapReady
      ? 'Start a flight plan for this run and draw both stops on the map'
      : 'Add the stops to a flight plan without claiming both can be drawn on the map';
    button.addEventListener('click', () => planTrip(`${route.commodity} run`, [
      {
        placeId: route.buyAtId || placeIdForTerminal(route.buyAt),
        place: route.buyAt,
        note: `Buy ${Math.floor(route.units).toLocaleString()} SCU at ${money(route.buyPrice)}`,
      },
      {
        placeId: route.sellAtId || placeIdForTerminal(route.sellAt),
        place: route.sellAt,
        note: `Sell at ${money(route.sellPrice)} · +${money(route.profit)}`,
      },
    ]));
    plan.append(button);

    const capped = el('td', 'muted', route.limitedBy);
    capped.title = route.limitedBy === 'capital'
      ? 'Your capital runs out before the hold does'
      : route.limitedBy === 'stock'
        ? 'The shop does not stock enough to fill the hold'
        : route.limitedBy === 'demand'
          ? 'The buyer does not report enough demand to take the full run'
        : 'The hold is the limit - the good case';
    tr.append(capped);
    tr.append(plan);

    body.append(tr);
  }
}

onInput('#routes-capital', loadRoutes);
$('#routes-ship')?.addEventListener('change', loadRoutes);
$('#routes-here')?.addEventListener('change', loadRoutes);
$('#routes-ranking')?.addEventListener('change', loadRoutes);
$('#routes-evidence')?.addEventListener('change', loadRoutes);
$('#routes-fresh-only')?.addEventListener('change', loadRoutes);

/**
 * "Wake up at" on the dashboard: where the last death put you, which is as
 * close as the logs get to naming your regen point. Hidden until there is an
 * answer, and honest about how sure it is.
 */
async function loadRespawn() {
  const card = $('#now-respawn-card');
  if (!card) return;

  let data;
  try {
    data = await getJson('/api/respawn');
  } catch {
    card.hidden = true;
    return;
  }

  if (!data.known) {
    card.hidden = true;
    return;
  }

  // Two signals, neither promoted over the other: a bed is where a regen
  // location gets set, but the same toast fires when one is used only to
  // heal; waking somewhere proves only where you woke. Both are shown and
  // labelled, and the headline is whichever happened last.
  const bedIsNewer = data.bed && (!data.at || new Date(data.bed.at) > new Date(data.at));

  $('#now-respawn').textContent = bedIsNewer ? data.bed.place : (data.place ?? data.bed?.place ?? '—');

  // Each line names its own place. The headline is only the more recent of
  // the two, and saying "woke there" under a different place read as though
  // both signals agreed when they do not.
  $('#now-respawn-sub').textContent = data.bed
    ? `last medical bed · ${data.bed.place}, ${relative(data.bed.at)}`
    : '';

  // "Deaths" would be wrong for the commoner case: most wake-ups follow an
  // incapacitation rather than a corpse recovery, so both read as "times down".
  $('#now-respawn-bed').textContent = data.place
    ? (data.settled
      ? `last woke · ${data.place}, ${data.agreeing} of your last ${data.of} times down`
      : `last woke · ${data.place}, ${relative(data.at)}`)
    : '';

  card.hidden = false;
}

/* ---------- casualties ---------- */

async function loadCasualties() {
  const days = Number($('#casualties-period').value) || 0;

  let data;
  try {
    data = await getJson(`/api/casualties?days=${days}`);
  } catch {
    return;
  }

  tiles('#casualties-summary', [
    ['Deaths', data.deaths],
    ['Incapacitations', data.incapacitations],
    ['Sessions with a death', data.sessionsWithDeaths],
    ['Claim fees*', data.estimatedFees > 0 ? money(data.estimatedFees) : '—'],
  ]);

  bars('#casualties-places',
    data.byPlace.map((p) => ({
      label: p.place, value: p.deaths, onClick: () => jumpToPlace(p.place),
    })),
    (v) => `${v}`);

  bars('#casualties-ships',
    data.byShip.map((s) => ({ label: s.ship, value: s.deaths })),
    (v) => `${v}`);

  bars('#casualties-woke',
    (data.wokeAt || []).map((w) => ({
      label: w.place, value: w.times, onClick: () => jumpToPlace(w.place),
    })),
    (v) => `${v}`);

  bars('#casualties-beds',
    (data.bedsUsed || []).map((b) => ({
      label: b.afterDeath ? `${b.place} · ${b.afterDeath} after a death` : b.place,
      value: b.times,
      onClick: () => jumpToPlace(b.place),
    })),
    (v) => `${v}`);

  // The logins are counted and shown rather than hidden: leaving them out
  // silently would make the totals look wrong to anyone who remembers using a
  // bed more often than this.
  const kinds = data.bedKinds;

  if (kinds) {
    const parts = [];
    if (kinds.afterDeath) parts.push(`${kinds.afterDeath} after a death or incapacitation`);
    if (kinds.heal) parts.push(`${kinds.heal} used mid-session, which could be either`);
    if (kinds.hab) parts.push(`${kinds.hab} at places with no clinic, so hab beds - not counted above`);
    if (kinds.wake) parts.push(`${kinds.wake} were waking up at login, not counted above`);

    // The directory can only rule a bed out, and only if it is on disk. Say so
    // rather than leaving the mid-session pile unexplained.
    if (!kinds.clinicsKnown) {
      parts.push('enable the place directory in Settings to rule out beds at places with no clinic');
    }

    $('#beds-kinds').textContent = parts.join(' · ');
  }

  $('#casualties-woke-note').textContent = data.lastWokeAt
    ? `Last woke at ${data.lastWokeAt}, ${relative(data.lastWokeWhen)}. Inferred, `
      + 'not read: the game logs no respawn point, so this is the first place '
      + 'named after each death.'
    : 'Nothing to infer yet. This reads the first place named after a death, '
      + 'because the game logs no respawn point of its own.';

  const body = $('#casualties-fees tbody');
  body.textContent = '';

  // Guarded like every other read on this page: one absent array should not
  // take the whole page down when the rest of the answer arrived intact.
  for (const fee of data.fees || []) {
    const tr = el('tr');
    tr.append(el('td', null, fee.name));
    tr.append(el('td', 'num outward', money(fee.fee)));
    body.append(tr);
  }
}

onInput('#casualties-period', loadCasualties);

/* ---------- crew ---------- */

/**
 * The ships you and somebody else were both aboard.
 *
 * The party channel says who was online while grouped with you; this says who
 * was actually in the vehicle, and whose it was. It is the only thing in these
 * logs that ties a person to a ship.
 *
 * Deliberately counted in boardings rather than hours. There is no leave line
 * for you, a channel opens when somebody gets in rather than when the ship
 * flies, and a parked Cyclone reads the same as a crossing - so the caption
 * says so instead of letting a number imply time spent together.
 */
async function renderSharedShips(days) {
  const host = $('#crew-ships');
  if (!host) return;

  host.textContent = '';

  let ships;
  try {
    ships = await getJson(`/api/crew/ships?days=${days}`);
  } catch {
    return;
  }

  if (!ships.length) return;

  const card = el('section', 'shared-block');
  card.append(el('h3', null, 'Ships you have shared'));
  card.append(el('p', 'muted', 'Who was aboard which ship, from its comms channel — the only '
    + 'lines that put a person in a vehicle. Counted in boardings, not hours: nothing records '
    + 'how long anyone stayed, and a parked ship looks the same as a crossing.'));

  const table = el('table', 'data');
  const head = el('thead');
  const headRow = el('tr');
  for (const label of ['Pilot', 'Ship', 'Whose', 'Boardings', 'First', 'Last']) {
    headRow.append(el('th', label === 'Boardings' ? 'num' : null, label));
  }
  head.append(headRow);
  table.append(head);

  const body = el('tbody');
  const mine = ships.filter((s) => s.owner === s.handle).length;

  for (const ship of ships) {
    const tr = el('tr');
    tr.append(el('td', null, ship.handle));
    tr.append(el('td', null, ship.ship));

    // Whose ship it was is the interesting half: crewing for somebody is a
    // different evening from having them aboard yours.
    tr.append(el('td', 'muted', ship.owner === ship.handle ? 'theirs' : 'yours'));

    tr.append(el('td', 'num', String(ship.times)));
    tr.append(el('td', 'muted', dateOf(ship.first)));
    tr.append(el('td', 'muted', dateOf(ship.last)));
    body.append(tr);
  }

  table.append(body);
  card.append(table);

  card.append(el('p', 'muted', `${ships.length} pairing${ships.length === 1 ? '' : 's'}`
    + `${mine ? `, ${mine} of them in a ship that was not yours` : ''}.`));

  host.append(card);
}


/**
 * The people the party channel has named.
 *
 * The counts are arrivals and departures, not time together, and the page says
 * so rather than dressing them up as a friends list. Anyone who was already
 * online when you grouped up and stayed to the end never produced a toast, so
 * absence from this table means nothing at all - which is exactly why the
 * summary counts what was *seen* rather than claiming a total.
 */
async function loadCrew() {
  const table = $('#crew-table');
  if (!table) return;

  const days = Number($('#crew-period').value) || 0;

  let rows;
  try {
    rows = await getJson(`/api/crew?days=${days}`);
  } catch {
    return;
  }

  renderSharedShips(days).catch(() => {});

  const joins = rows.reduce((total, r) => total + (r.joined || 0), 0);
  const arrivals = rows.reduce((total, r) => total + r.connected, 0);

  tiles('#crew-summary', [
    ['People named', rows.length],

    // Joins rather than arrivals: this is the one that counts somebody who was
    // not there a moment before, which is what "flew with" means.
    ['Joined your party', joins],
    ['Came online', arrivals],
    ['Most flown with', rows.length ? rows[0].handle : '—'],
  ]);

  bars('#crew-chart',
    rows.slice(0, 12).map((r) => ({
      label: r.handle,
      value: r.sessions,
      note: `${r.connected} arrival${r.connected === 1 ? '' : 's'}`,
    })),
    (v) => `${v} session${v === 1 ? '' : 's'}`);

  const body = table.querySelector('tbody');
  body.textContent = '';

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted',
      'Nobody named in that range — the game only says so when someone joins, '
      + 'leaves, or connects while you are partied with them.');
    td.colSpan = 9;
    tr.append(td);
    body.append(tr);
    return;
  }

  for (const row of rows) {
    const tr = el('tr');
    tr.append(el('td', null, row.handle));
    tr.append(el('td', 'num', String(row.sessions)));

    // Blank rather than zero throughout: these are four different facts and a
    // zero in one of them is usually "the game did not say", not "never".
    tr.append(el('td', row.joined ? 'num' : 'num muted', row.joined || '—'));
    tr.append(el('td', row.left ? 'num' : 'num muted', row.left || '—'));
    tr.append(el('td', 'num', String(row.connected)));
    tr.append(el('td', 'num', String(row.dropped)));

    tr.append(el('td', row.ledParty ? 'num' : 'num muted', row.ledParty || '—'));

    tr.append(el('td', 'muted', dateOf(row.first)));
    tr.append(el('td', 'muted', dateOf(row.last)));
    body.append(tr);
  }
}

onInput('#crew-period', loadCrew);

/**
 * The one-line version of the price chart, for the expanded Market row.
 *
 * No axes and no grid: at this size a gridline is noise, and the two numbers
 * that matter - the range it moved in, and how long that covers - read better
 * as text beside it than as labels inside it. Anything more belongs on the
 * page the button next to it opens.
 */
async function drawDetailSpark(strip, commodity) {
  // One counter per side, not the page's four. Every counter is a separate
  // request to UEX, and expanding a row is the ordinary way to read this table
  // - somebody comparing twenty commodities should not cost a volunteer-run
  // API a hundred and sixty requests to draw twenty thumbnails. Opening the
  // full page later widens the sample; opening it first makes this free.
  const trend = await getJson(
    `/api/uex/history?commodity=${encodeURIComponent(commodity)}&perSide=1`);

  const daily = dailyMarket(trend).filter((d) => d.bestSell > 0);

  if (daily.length < 2) {
    strip.remove();
    return;
  }

  const values = daily.map((d) => d.bestSell);
  const low = Math.min(...values);
  const high = Math.max(...values);
  const span = Math.max(1, high - low);
  const days = Math.round((daily[daily.length - 1].t - daily[0].t) / 86400000);

  const svg = svgEl('svg', {
    class: 'spark', viewBox: '0 0 240 34', preserveAspectRatio: 'none',
    role: 'img', 'aria-label': `${commodity} price over the last ${days} days`,
  });

  svg.append(svgEl('path', {
    d: daily.map((d, i) =>
      `${i ? 'L' : 'M'} ${(i / (daily.length - 1) * 238 + 1).toFixed(1)} `
      + `${(31 - ((d.bestSell - low) / span) * 28).toFixed(1)}`).join(' '),
    fill: 'none', stroke: '#35c8f0', 'stroke-width': '1.5',
    'stroke-linejoin': 'round', 'stroke-linecap': 'round',
  }));

  strip.append(svg);

  // What it says depends on what came back, not on what was asked for: this
  // asks for one counter per side, but a wider sample already cached by the
  // commodity page is reused, and then the line really is a best-of.
  const from = trend.sampled > 1
    ? `best of ${trend.sampled} counters each day`
    : 'the busiest counter';

  strip.append(el('span', 'muted',
    `${money(low)} – ${money(high)} over ${days} days, ${from}`));
}

/**
 * Cargo earnings over time, as running totals.
 *
 * Deliberately cumulative. A hold is sold a handful of times a month, so a line
 * of per-week takings is mostly zero with occasional spikes - which reads as a
 * business collapsing between runs rather than as one being run occasionally.
 * A running total only ever goes up, and the gap between the two lines is the
 * number a hauler actually wants.
 *
 * Both lines start at zero on the day before the first trade, so a single
 * receipt still draws a line rather than a dot the chart cannot scale.
 */
function drawCargoEarnings(trades) {
  const svg = $('#cargo-chart');
  if (!svg) return;

  const ordered = [...trades].sort((a, b) => Date.parse(a.at) - Date.parse(b.at));

  const running = (side) => {
    const rows = ordered.filter((t) => (side === 'sell' ? t.isSell : !t.isSell));
    if (!rows.length) return [];

    let total = 0;
    const start = Date.parse(rows[0].at) - 86400000;

    return [{ t: start, v: 0 }, ...rows.map((t) => {
      total += Number(t.amount) || 0;
      return { t: Date.parse(t.at), v: total };
    })];
  };

  const series = [
    { label: 'Earned selling cargo', points: running('sell') },
    { label: 'Spent buying it', points: running('buy') },
  ];

  timeChart(svg, series, (v) => `${Math.round(v / 1000).toLocaleString()}k`);
  chartKey('#cargo-chart-key', series);
}

/* ---------- one commodity, in full ---------- */

const CHART_COLOURS = ['#35c8f0', '#ffb454', '#7fe4ff', '#ff7a8a'];

/**
 * A multi-line time chart.
 *
 * Days are the x axis rather than sample index: counters report when they feel
 * like it, so plotting by index would stretch a quiet fortnight to the same
 * width as a busy afternoon and invent a trend out of the reporting rate.
 */
function timeChart(svg, series, format) {
  svg.textContent = '';

  const drawable = drawableSeries(series);

  if (!drawable.length) {
    const text = svgEl('text', {
      x: 500, y: 110, 'text-anchor': 'middle', class: 'empty-note',
    });
    text.textContent = 'NOT ENOUGH HISTORY YET';
    svg.append(text);
    return;
  }

  const all = drawable.flatMap((s) => s.points);
  const times = all.map((p) => p.t);
  const values = all.map((p) => p.v);

  const t0 = Math.min(...times);
  const t1 = Math.max(...times);
  const span = Math.max(1, t1 - t0);

  // Zero is included so a line that halves looks halved. Starting the axis at
  // the lowest value makes every wobble look like a crash.
  const top = Math.max(...values, 0);
  const bottom = Math.min(...values, 0);
  const height = Math.max(1, top - bottom);

  const x = (t) => 46 + ((t - t0) / span) * 930;
  const y = (v) => 196 - ((v - bottom) / height) * 170;

  for (const value of [bottom, (bottom + top) / 2, top]) {
    svg.append(svgEl('line', { x1: 46, y1: y(value), x2: 976, y2: y(value), class: 'grid' }));
    const label = svgEl('text', { x: 4, y: y(value) + 4, class: 'axis' });
    label.textContent = format(value);
    svg.append(label);
  }

  for (const [edge, anchor] of [[t0, 'start'], [t1, 'end']]) {
    const label = svgEl('text', { x: x(edge), y: 214, 'text-anchor': anchor, class: 'axis' });
    label.textContent = new Date(edge).toLocaleDateString([], { month: 'short', day: '2-digit' });
    svg.append(label);
  }

  drawable.forEach((line, i) => {
    const colour = CHART_COLOURS[i % CHART_COLOURS.length];
    const d = line.points
      .map((p, n) => `${n ? 'L' : 'M'} ${x(p.t).toFixed(1)} ${y(p.v).toFixed(1)}`)
      .join(' ');

    svg.append(svgEl('path', {
      d, fill: 'none', stroke: colour, 'stroke-width': '2',
      'stroke-linejoin': 'round', 'stroke-linecap': 'round',
    }));
  });
}

/**
 * The lines a chart actually draws. Shared with the key because colours are
 * handed out by position: count the skipped lines on one side only and every
 * swatch names the line above it.
 */
function drawableSeries(series) {
  return series.filter((s) => s.points.length > 1);
}

/** The key under a chart: a line has no meaning without one. */
function chartKey(container, series) {
  const box = $(container);
  box.textContent = '';

  drawableSeries(series).forEach((line, i) => {
    const entry = el('span');
    const swatch = el('i');
    swatch.style.background = CHART_COLOURS[i % CHART_COLOURS.length];
    entry.append(swatch, document.createTextNode(line.label));
    box.append(entry);
  });
}

/**
 * Rolls per-counter history up into one series per question.
 *
 * Each counter is carried forward to the day before the next report, then the
 * days are combined. Without the carry-forward a day on which only two of eight
 * counters reported would read as demand collapsing and recovering, which is a
 * story about UEX's contributors rather than about the market.
 */
function dailyMarket(history) {
  const days = new Map();
  const DAY = 86400000;

  const counters = history.series
    .filter((terminal) => terminal.points.length)
    .map((terminal) => terminal.points.map((p) => ({ ...p, day: Math.floor(Date.parse(p.at) / DAY) })));

  if (!counters.length) return [];

  for (const points of counters) {
    const last = points[points.length - 1].day;

    let i = 0;
    let held = null;

    for (let day = points[0].day; day <= last; day++) {
      while (i < points.length && points[i].day <= day) held = points[i++];

      const bucket = days.get(day) || { bestSell: 0, bestBuy: 0, demand: 0, stock: 0, counters: 0 };
      if (held.sell > 0) bucket.bestSell = Math.max(bucket.bestSell, held.sell);
      if (held.buy > 0) bucket.bestBuy = bucket.bestBuy ? Math.min(bucket.bestBuy, held.buy) : held.buy;
      bucket.demand += held.demand;
      bucket.stock += held.stock;
      bucket.counters += 1;
      days.set(day, bucket);
    }
  }

  return [...days.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([day, v]) => ({
      t: day * DAY,
      bestSell: v.bestSell,
      bestBuy: v.bestBuy,
      counters: v.counters,

      // Per reporting counter rather than summed. Counters enter and leave
      // UEX's sample - on this install's Iron, eight report at the start and
      // five by the end - so a total slopes down as reporting thins, which is
      // the story about UEX's contributors the carry-forward above exists to
      // avoid. Clipping to the days all eight cover would say it honestly too,
      // and would throw away the most recent fortnight to do it.
      demand: v.counters ? v.demand / v.counters : 0,
      stock: v.counters ? v.stock / v.counters : 0,
    }));
}

/** What the page is showing, so Back and a redraw know where they are. */
let openCommodityName = null;

/**
 * The full picture for one commodity.
 *
 * The history is a request per counter, so it happens here - on the click that
 * opens the page - rather than anywhere a page merely loads.
 */
async function openCommodity(name) {
  openCommodityName = name;
  showView('commodity');

  // showView wrote #commodity; the subject belongs in the link too.
  const fragment = `#commodity/${encodeURIComponent(name)}`;
  if (location.hash !== fragment) history.replaceState(null, '', fragment);

  $('#commodity-name').textContent = name;
  $('#commodity-sub').textContent = 'Asking UEX what this has been doing…';

  // A link straight into this page arrives before Market has ever been opened,
  // so the catalogue its headline figures come from is not loaded yet.
  if (!marketEntries.length) await loadMarket().catch(() => {});

  const entry = marketEntries.find((e) => e.name === name);

  tiles('#commodity-summary', [
    ['Best sell now', entry?.uex?.bestSell > 0 ? money(entry.uex.bestSell) : '—'],
    ['15-day average', entry?.uex?.avgSell > 0 ? money(entry.uex.avgSell) : '—'],
    ['You have sold', entry?.myScuSold ? `${entry.myScuSold.toLocaleString()} SCU` : '—'],
    ['It earned you', entry?.myRevenue ? money(entry.myRevenue) : '—'],
  ]);

  renderCommodityCounters(await terminalsFor(name).catch(() => []), entry);
  renderCommodityReceipts(name);

  // Named "trend", not "history": a local called history shadows window.history
  // for this whole function, and the fragment is written above it.
  let trend;
  try {
    trend = await getJson(`/api/uex/history?commodity=${encodeURIComponent(name)}`);
  } catch {
    $('#commodity-sub').textContent = 'UEX could not be reached, so no history is drawn.';
    return;
  }

  // Racing clicks: the reader has moved on, and this answer is for a page that
  // is no longer open.
  if (openCommodityName !== name) return;

  const daily = dailyMarket(trend);

  $('#commodity-sub').textContent = trend.sampled
    ? `${entry?.groups?.join(', ') || 'Commodity'} · history from the ${trend.sampled} busiest `
      + `of ${trend.terminals} counters that trade it, by demand and by stock.`
    : trend.terminals
      ? `Live UEX quotes are available at ${trend.terminals} counters, but no price-history samples loaded. `
        + 'The counter tables above are still the current report.'
      : 'No UEX market counters are currently reported for this commodity.';

  const priceSeries = [
    { label: 'Best price paid to you', points: daily.filter((d) => d.bestSell > 0).map((d) => ({ t: d.t, v: d.bestSell })) },
    { label: 'Cheapest price charged to you', points: daily.filter((d) => d.bestBuy > 0).map((d) => ({ t: d.t, v: d.bestBuy })) },
  ];

  timeChart($('#commodity-price-chart'), priceSeries, (v) => Math.round(v).toLocaleString());
  chartKey('#commodity-price-key', priceSeries);

  // Only days where both ends are known: a margin needs a buy price as well as
  // a sell one, and subtracting from zero would draw the sale price again under
  // a name that means something else.
  const marginSeries = [{
    label: 'Margin per SCU',
    points: daily.filter((d) => d.bestSell > 0 && d.bestBuy > 0)
      .map((d) => ({ t: d.t, v: d.bestSell - d.bestBuy })),
  }];

  timeChart($('#commodity-margin-chart'), marginSeries, (v) => Math.round(v).toLocaleString());
  chartKey('#commodity-margin-key', marginSeries);

  // One line per counter, capped at the palette: past four the lines start
  // repeating colours and the chart stops being readable.
  const counterSeries = trend.series
    .map((s) => ({
      label: s.terminal,
      points: s.points.filter((p) => p.sell > 0)
        .map((p) => ({ t: Date.parse(p.at), v: p.sell })),
    }))
    .filter((s) => s.points.length > 1)
    .slice(0, CHART_COLOURS.length);

  timeChart($('#commodity-counter-chart'), counterSeries, (v) => Math.round(v).toLocaleString());
  chartKey('#commodity-counter-key', counterSeries);

  const scuSeries = [
    { label: 'Demand — SCU the average counter will take', points: daily.map((d) => ({ t: d.t, v: d.demand })) },
    { label: 'Supply — SCU on the average shelf', points: daily.map((d) => ({ t: d.t, v: d.stock })) },
  ];

  timeChart($('#commodity-scu-chart'), scuSeries, (v) => Math.round(v).toLocaleString());
  chartKey('#commodity-scu-key', scuSeries);
}

/** The two counter tables: where a hold empties, and where it fills. */
function renderCommodityCounters(rows, entry) {
  const sells = rows.filter((r) => r.sell > 0).sort((a, b) => b.sell - a.sell);
  const buys = rows.filter((r) => r.buy > 0).sort((a, b) => a.buy - b.buy);

  const best = sells.length ? sells[0].sell : 0;
  const cheapest = buys.length ? buys[0].buy : 0;

  fillCounterTable('#commodity-sell-table', sells, (r) => [
    money(r.sell),
    r.sellScu ? `${Math.round(r.sellScu).toLocaleString()} SCU` : '—',
    best && r.sell < best ? `−${money(best - r.sell)}` : 'best',
  ], (r) => best && r.sell < best);

  fillCounterTable('#commodity-buy-table', buys, (r) => [
    money(r.buy),
    r.buyScu ? `${Math.round(r.buyScu).toLocaleString()} SCU` : '—',
    cheapest && r.buy > cheapest ? `+${money(r.buy - cheapest)}` : 'cheapest',
  ], (r) => cheapest && r.buy > cheapest);

  // Unused here, but keeps the signature honest about what it was given.
  void entry;
}

function fillCounterTable(selector, rows, cells, isWorse) {
  const body = $(selector).querySelector('tbody');
  body.textContent = '';

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted', 'No counter known to trade it this way.');
    td.colSpan = 5;
    tr.append(td);
    body.append(tr);
    return;
  }

  for (const row of rows) {
    const tr = el('tr');
    tr.append(el('td', null, row.terminal));

    const [price, volume, delta] = cells(row);
    tr.append(el('td', 'num', price));
    tr.append(el('td', 'num', volume));
    tr.append(el('td', `num ${isWorse(row) ? 'outward' : 'inward'}`, delta));

    tr.append(el('td', 'muted', row.seen ? dateOf(new Date(row.seen * 1000).toISOString()) : '—'));
    body.append(tr);
  }
}

/** What this install actually paid and was paid for the thing. */
async function renderCommodityReceipts(name) {
  const body = $('#commodity-mine-table').querySelector('tbody');
  body.textContent = '';

  let trades = [];
  try {
    trades = (await getJson('/api/commodities?days=0')).filter((t) => t.commodity === name);
  } catch { /* the tables above still stand on their own */ }

  if (!trades.length) {
    const tr = el('tr');
    const td = el('td', 'muted', 'You have not traded this one.');
    td.colSpan = 6;
    tr.append(td);
    body.append(tr);
    return;
  }

  for (const trade of trades.sort((a, b) => new Date(b.at) - new Date(a.at))) {
    const tr = el('tr');
    tr.append(el('td', null, dateOf(trade.at)));
    tr.append(el('td', null, trade.isSell ? 'Sold' : 'Bought'));
    tr.append(tdPlace(trade.place));
    tr.append(el('td', 'num', String(trade.scu)));
    tr.append(el('td', `num ${trade.isSell ? 'inward' : 'outward'}`, money(trade.amount)));
    tr.append(el('td', 'num muted', money(trade.unitPrice)));
    body.append(tr);
  }
}

$('#commodity-back')?.addEventListener('click', () => showView('market'));

/* ---------- outfitting ---------- */

/** Which shop carries the most of the kit you were wearing when you died. */
async function loadOutfitting() {
  const block = $('#outfitting');
  if (!block) return;

  let data;
  try {
    data = await getJson('/api/outfitting');
  } catch {
    block.hidden = true;
    return;
  }

  if (!data.shops.length) {
    block.hidden = true;
    return;
  }

  tiles('#outfitting-summary', [
    ['Kit worn', `${data.priced} of ${data.kitSize} priced`],
    ['Cheapest anywhere', money(data.cheapest)],
    ['Best single shop', `${data.shops[0].covers} of ${data.kitSize} items`],
  ]);

  const body = $('#outfitting-table tbody');
  body.textContent = '';

  for (const shop of data.shops) {
    const tr = el('tr');
    tr.append(tdPlace(shop.terminal));
    tr.append(el('td', 'num', `${shop.covers} of ${data.kitSize}`));

    const cost = el('td', 'num outward', money(shop.total));
    cost.title = shop.items.map((i) => `${i.item} — ${Math.round(i.price).toLocaleString()} aUEC`).join('\n');
    tr.append(cost);

    // The whole point of knowing: put the trip on a list.
    const action = el('td', 'num');
    const add = el('button', 'ghost track', '+ list');
    add.title = `Add everything ${shop.terminal} carries to your shopping list`;

    add.addEventListener('click', async () => {
      add.disabled = true;
      for (const item of shop.items)
        await fetch('/api/jobs/collect', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ name: item.item, needed: 1, unit: '' }),
        }).catch(() => {});

      add.textContent = '✓ added';
    });

    action.append(add);
    tr.append(action);
    body.append(tr);
  }

  block.hidden = false;
}

/* ---------- jobs ---------- */

/**
 * The jobs page: contracts the logs already know about, plus the player's own
 * crafting jobs and shopping lists - which check themselves against the stash,
 * because knowing what is still missing is the whole point of a list.
 */
async function loadJobs() {
  await Promise.all([loadJobContracts(), loadJobList(), fillBlueprintGoals()]);
}

/**
 * The goal picker: blueprints the game has actually given you, so a craft can
 * be started from the page where progress is tracked rather than by hunting
 * the catalogue. Everything else in the catalogue stays available below it,
 * because a blueprint you have not been given is still worth planning for.
 */
async function fillBlueprintGoals() {
  const select = $('#jobs-blueprint');
  if (!select || !craftingCatalogue.length) return;

  const previous = select.value;
  const owned = craftingCatalogue.filter((b) => b.owned);
  const rest = craftingCatalogue.filter((b) => !b.owned && b.materials.length);

  select.textContent = '';

  if (owned.length) {
    const group = document.createElement('optgroup');
    group.label = `Yours (${owned.length})`;
    for (const bp of owned) group.append(new Option(bp.output, bp.output));
    select.append(group);
  }

  const others = document.createElement('optgroup');
  others.label = owned.length ? 'Everything else' : 'Blueprints';
  for (const bp of rest.slice(0, 400)) others.append(new Option(bp.output, bp.output));
  select.append(others);

  if (previous) select.value = previous;
}

$('#jobs-plan')?.addEventListener('click', async (e) => {
  const output = $('#jobs-blueprint').value;
  const bp = craftingCatalogue.find((b) => b.output === output);
  if (!bp) return;

  e.currentTarget.disabled = true;

  try {
    await fetch('/api/jobs', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        title: `Craft ${bp.output}`,
        kind: 'craft',
        source: `${bp.output} blueprint · ${craftTime(bp.craftSeconds)} to craft`
          + (bp.owned ? ' · in your library' : ''),
        items: parseJobItems(bp.materials.join('\n')),
      }),
    });
    await loadJobList();
  } finally {
    e.currentTarget.disabled = false;
  }
});

/**
 * The pinned job on the Now page - and so in the overlay, where what is still
 * missing is worth having in the corner of the screen while flying.
 */
async function renderPinnedJob(jobs) {
  const card = $('#now-job-card');
  if (!card) return;

  const job = (jobs ?? await getJson('/api/jobs').catch(() => [])).find((j) => j.pinned && !j.done);

  if (!job) {
    card.hidden = true;
    return;
  }

  $('#now-job-title').textContent = job.title;

  const progress = $('#now-job-progress');
  progress.textContent = '';
  progress.append(jobProgress(job.haveCount, job.totalCount,
    `${job.haveCount} of ${job.totalCount} in hand`));

  // What is still missing, which is the only part worth reading mid-flight.
  const list = $('#now-job-items');
  list.textContent = '';

  const missing = job.items.filter((i) => !i.have);

  if (!missing.length) {
    list.append(el('li', 'inward', 'Everything on this list is in hand.'));
  } else {
    for (const item of missing.slice(0, 6)) {
      const li = el('li');
      li.append(el('span', 'n', item.name
        + (item.needed > 0 ? ` ${item.needed}${item.unit ? ` ${item.unit}` : ''}` : '')));

      if (item.buyAt) li.append(el('span', 'muted', `buy at ${item.buyAt}`));
      list.append(li);
    }

    if (missing.length > 6)
      list.append(el('li', 'muted', `+${missing.length - 6} more`));
  }

  card.hidden = false;
}

async function loadJobContracts() {
  const host = $('#jobs-contracts');
  host.textContent = '';

  // Contracts do not survive a game restart, so an "in progress" contract
  // from a session that has ended is a ghost. Only the running session's
  // contracts are real - with the game closed, there are none.
  let live = null;
  try {
    live = await getJson('/api/now');
  } catch { /* server not answering; treat as not playing */ }

  if (!live?.inGame) {
    host.append(el('p', 'muted',
      'Nothing active — the game is not running. Contracts are dropped when you leave, '
      + 'so only a live session can have any.'));
    return;
  }

  let rows = [];
  try {
    rows = await getJson('/api/contracts?days=2');
  } catch { /* nothing to show */ }

  // This session only: anything taken before it started belongs to the past.
  const since = live.sessionStarted ? new Date(live.sessionStarted).getTime() : 0;
  const open = rows.filter((c) =>
    c.outcome === 'InProgress' && new Date(c.at).getTime() >= since);

  if (!open.length) {
    host.append(el('p', 'muted', 'No contract open in this session.'));
    return;
  }

  for (const contract of open) {
    const card = el('article', 'job-card');

    const head = el('div', 'job-head');
    head.append(el('b', null, `${contract.issuer} · ${contract.type}`));
    if (contract.difficulty) head.append(el('span', 'job-kind', contract.difficulty));
    card.append(head);

    const sub = [contract.system, `taken ${relative(contract.at)}`].filter(Boolean).join(' · ');
    card.append(el('div', 'muted', sub));

    if (contract.steps > 0) {
      card.append(jobProgress(contract.stepsDone, contract.steps,
        `${contract.stepsDone} of ${contract.steps} objectives done`));
    }

    host.append(card);
  }
}

/**
 * A "track this" button for any catalogue row: one click puts the thing on
 * the list the player is filling - the pinned job, else the newest open list,
 * else a new one - and says where it went.
 */
function trackButton(name, needed = 1, unit = '') {
  const button = el('button', 'ghost track', '+ list');
  button.title = `Add ${name} to your shopping list`;

  button.addEventListener('click', async () => {
    button.disabled = true;

    try {
      const result = await fetch('/api/jobs/collect', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, needed, unit }),
      }).then((r) => r.json());

      button.textContent = '✓ added';
      button.title = `On "${result.title}"`;
    } catch {
      button.textContent = 'failed';
      button.disabled = false;
    }
  });

  return button;
}

/** The shared progress bar: done over total, with its own wording. */
/**
 * What a job needs, and where each of it is.
 *
 * Shared by the reader's own cards and by imported ones. The stash and loadout
 * columns are about the reader either way, which is the whole value of seeing
 * somebody else's list: "Bob needs four Agricium" reads very differently beside
 * "and you have some at Port Tressler".
 */
function jobLines(job) {
  const table = el('table', 'job-items');
  const body = el('tbody');

  for (const item of job.items) {
    const tr = el('tr', item.have ? 'have' : null);

    const need = item.needed > 0
      ? `${item.needed}${item.unit ? ` ${item.unit}` : ''}`
      : '';
    tr.append(el('td', 'job-mark', item.have ? '✓' : '·'));
    tr.append(el('td', null, item.name));
    tr.append(el('td', 'num muted', need));

    // Where it is, or where to buy what is missing.
    const whereCell = el('td', 'muted');

    if (item.wornNow) {
      whereCell.textContent = 'worn now';
    } else if (item.where.length) {
      whereCell.append(placeLink(item.where[0]));
      if (item.where.length > 1) {
        const more = el('span', 'note-inline', ` +${item.where.length - 1}`);
        more.title = item.where.slice(1).join('\n');
        whereCell.append(more);
      }
    } else if (item.buyAt) {
      whereCell.append(el('span', 'note-inline', 'buy at '));
      whereCell.append(placeLink(item.buyAt));
    } else {
      whereCell.textContent = '—';
    }
    tr.append(whereCell);

    tr.append(el('td', item.buyPrice ? 'num' : 'num muted',
      item.buyPrice ? money(item.buyPrice) : '—'));

    body.append(tr);
  }

  table.append(body);
  return table;
}

/**
 * The mark on anything that came out of somebody else's file.
 *
 * Names them rather than saying "imported": the reader knows the difference
 * between a list from the friend they fly with and one from a stranger, and
 * the app does not.
 */
function importedChip(from) {
  const chip = el('span', 'job-kind from-a-file', `from ${from.handle || 'a file'}`);
  chip.title = `Imported ${dateOf(from.importedAt)} from a shared file. Not your data.`
    + (from.note ? `\n${from.note}` : '');
  return chip;
}

/**
 * The one thing an imported card can do.
 *
 * It posts to the ordinary authoring endpoint, so the copy is minted a fresh
 * local id through the normal path with the normal checks - the only route by
 * which somebody else's data becomes yours, and a deliberate one.
 *
 * A new list rather than merged into one you have: adding another person's 96
 * SCU to a line you already had produces a number you never chose.
 */
function copyToMine(job) {
  const copy = el('button', 'ghost tiny', 'Copy to my lists');
  copy.title = 'Makes your own copy. Theirs stays where it is.';

  copy.addEventListener('click', async () => {
    copy.disabled = true;

    try {
      await fetch('/api/jobs', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          title: job.title,
          kind: job.kind,
          source: `copied from ${job.imported.handle || 'a shared file'}`,
          items: job.items.map((i) => ({ name: i.name, needed: i.needed, unit: i.unit })),
        }),
      });
    } finally {
      copy.disabled = false;
    }

    loadJobList();
  });

  return copy;
}

function jobProgress(done, total, label) {
  const wrap = el('div', 'job-progress');
  const track = el('div', 'job-track');
  const fill = el('div', 'job-fill');

  fill.style.width = `${total > 0 ? Math.round((done / total) * 100) : 0}%`;
  if (done >= total && total > 0) fill.classList.add('full');

  track.append(fill);
  wrap.append(track);
  wrap.append(el('span', 'muted', label));
  return wrap;
}

async function loadJobList() {
  const host = $('#jobs-list');
  const craftHost = $('#blueprint-jobs');
  host.textContent = '';
  if (craftHost) craftHost.textContent = '';

  let jobs = [];
  try {
    jobs = await getJson(`/api/jobs${importedQuery()}`);
  } catch { /* server down; the page still shows contracts */ }

  renderPinnedJob(jobs);
  refreshMapFocusContext(mapFocusFilter === 'shopping' || mapFocusFilter === 'stash').catch(() => {});
  reloadPilotBriefing().catch(() => {});

  // Shopping and crafting are different work, so they live on different
  // pages; the cards themselves are identical.
  const lists = jobs.filter((j) => j.kind !== 'craft');
  const builds = jobs.filter((j) => j.kind === 'craft');

  if (!lists.length) {
    host.append(el('p', 'muted',
      'No lists yet. Start one here, or add anything from Market, Parts or Mining with "+ list".'));
  }

  if (craftHost && !builds.length) {
    craftHost.append(el('p', 'muted',
      'No builds planned. Pick a blueprint above, or hit Plan on Reference → Crafting.'));
  }

  for (const job of jobs) {
    const card = el('article', job.done ? 'job-card done' : 'job-card');
    if (job.imported) card.classList.add('from-a-file');

    const head = el('div', 'job-head');
    head.append(el('b', null, job.title));
    head.append(el('span', 'job-kind', job.kind === 'craft' ? 'craft' : 'list'));
    if (job.imported) head.append(importedChip(job.imported));

    // An imported card carries no control that changes anything.
    //
    // Not only because it is somebody else's: the id it arrives under would
    // address one of yours. Ids are minted per install with no namespace, so a
    // file exported from this machine and read back holds ids identical to your
    // own, and every button here builds its URL out of one. The server prefixes
    // them so those routes miss, and this leaves them undrawn as well - a
    // button that 404s is still a button somebody pressed expecting something.
    if (job.imported) {
      head.append(el('span', 'spacer'));
      head.append(copyToMine(job));
      card.append(head);
      card.append(jobProgress(job.haveCount, job.totalCount,
        `you have ${job.haveCount} of the ${job.totalCount} things this needs`));
      card.append(jobLines(job));

      // The same split as any other card: a build belongs on Crafting whoever
      // wrote it.
      (job.kind === 'craft' && craftHost ? craftHost : host).append(card);
      continue;
    }

    // Where this list is for, changeable here because it is a plan rather than
    // a fact: the run you meant on Tuesday is not the run you fly on Friday.
    const where = el('select', 'select job-place-edit');
    where.title = 'Where you mean to shop';
    fillPlaceOptions(where, job.destinationId || job.destination || '');

    where.addEventListener('change', async () => {
      const picked = pickedPlace(where);

      await fetch(`/api/jobs/${job.id}/destination`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ place: picked.name, placeId: picked.id }),
      });

      loadJobList();
    });

    head.append(where);

    const spacer = el('span', 'spacer');
    head.append(spacer);

    // Pinning puts the job on Now, which is also what the overlay shows.
    const pin = el('button', job.pinned ? 'ghost on' : 'ghost', job.pinned ? 'On Now' : 'Pin to Now');
    pin.title = job.pinned
      ? 'Showing on the Now page and in the overlay'
      : 'Show this job on the Now page and in the overlay';

    pin.addEventListener('click', async () => {
      await fetch(`/api/jobs/${job.id}/pin`, { method: 'POST' });
      loadJobList();
    });
    head.append(pin);

    const toggle = el('button', 'ghost', job.done ? 'Reopen' : 'Mark done');
    toggle.addEventListener('click', async () => {
      await fetch(`/api/jobs/${job.id}/toggle`, { method: 'POST' });
      loadJobList();
    });
    head.append(toggle);

    // A list knows where its missing things are sold, which is a shopping trip
    // waiting to be flown - once the player has said which seller they meant.
    const shop = el('button', 'ghost', 'Plan trip');
    shop.title = 'Turn what is still missing into a flight plan';
    shop.addEventListener('click', () => planShoppingTrip(job, card));
    head.append(shop);

    const remove = el('button', 'ghost danger', 'Delete');
    remove.addEventListener('click', async () => {
      await fetch(`/api/jobs/${job.id}`, { method: 'DELETE' });
      loadJobList();
    });
    head.append(remove);
    card.append(head);

    if (job.source) card.append(el('div', 'muted', `from ${job.source}`));

    card.append(jobProgress(job.haveCount, job.totalCount,
      `${job.haveCount} of ${job.totalCount} in hand`));

    card.append(jobLines(job));
    (job.kind === 'craft' && craftHost ? craftHost : host).append(card);
  }
}

/**
 * Parses a written list into job items: "Hadanite 23", "Agricium 1.16 SCU",
 * or a bare name. Quantities are optional - a list of names is still a list.
 */
function parseJobItems(text) {
  return text.split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const match = line.match(/^(.*?)[\s·x×]*?([\d.]+)\s*(SCU|scu)?$/);

      if (match && match[1].trim()) {
        return {
          name: match[1].replace(/[\s×x·]+$/, '').trim(),
          needed: Number(match[2]) || 0,
          unit: match[3] ? 'SCU' : '',
        };
      }

      return { name: line, needed: 0, unit: '' };
    });
}

/**
 * Fills a picker with every place the app knows, and selects one.
 *
 * Picked rather than typed: these are places the app already knows by name -
 * from the game's own data and from where you have actually been - so making
 * someone spell "CRU-L4 Shallow Fields Station" is asking them to guess at
 * something already on the screen. Somewhere you have visited heads the list,
 * because a run is usually to a place you have flown to before.
 *
 * @param selected The place id to select, or a plain name for a destination
 *   saved before this was a picker.
 */
function fillPlaceOptions(select, selected) {
  if (!select) return;

  select.textContent = '';

  const anywhere = document.createElement('option');
  anywhere.value = '';
  anywhere.textContent = 'Anywhere — no particular stop';
  select.append(anywhere);

  const been = atlas.filter((l) => l.visits > 0).sort((a, b) => b.visits - a.visits);
  const rest = atlas.filter((l) => !l.visits).sort((a, b) => a.name.localeCompare(b.name));

  const add = (label, places) => {
    if (!places.length) return;

    const group = document.createElement('optgroup');
    group.label = label;

    for (const place of places) {
      const option = document.createElement('option');
      option.value = place.rawId;
      option.textContent = place.system ? `${place.name} · ${place.system}` : place.name;
      group.append(option);
    }

    select.append(group);
  };

  add('Where you have been', been);
  add('Everywhere else', rest);

  if (!selected) {
    select.value = '';
    return;
  }

  select.value = selected;

  // A destination saved as free text, from before this was a picker, or a
  // place this atlas cannot name. Kept rather than silently dropped.
  if (select.value !== selected) {
    const kept = document.createElement('option');
    kept.value = selected;
    kept.textContent = selected;
    select.append(kept);
    select.value = selected;
  }
}

/** What a picked option means: its id, and the name to store beside it. */
const pickedPlace = (select) => {
  const id = select.value;
  if (!id) return { name: null, id: null };

  const place = atlas.find((l) => l.rawId === id);
  return { name: place?.name ?? id, id: place ? id : null };
};

/**
 * The things this install knows how to buy, fetched once.
 *
 * Offered as a picker beside the free-text box rather than instead of it: the
 * box is still the list, and this is a way to put a correctly spelled line in
 * it. Only what can actually be bought is listed, because a line nothing sells
 * is a line no plan can route.
 */
let catalogue = null;

async function fillItemOptions(select) {
  if (!select || select.childElementCount) return;

  const placeholder = document.createElement('option');
  placeholder.value = '';
  placeholder.textContent = 'Add something we know…';
  select.append(placeholder);

  catalogue ??= await getJson('/api/shopping/catalogue').catch(() => ({ commodities: [], items: [] }));

  const add = (label, names) => {
    if (!names?.length) return;

    const group = document.createElement('optgroup');
    group.label = label;

    for (const name of names) {
      const option = document.createElement('option');
      option.value = name;
      option.textContent = name;
      group.append(option);
    }

    select.append(group);
  };

  add('Cargo', catalogue.commodities);
  add('Ship parts and gear', catalogue.items);
}

$('#jobs-new')?.addEventListener('click', () => {
  const form = $('#job-form');
  form.hidden = !form.hidden;
  fillPlaceOptions($('#job-place'), '');
  fillItemOptions($('#job-add'));
  if (!form.hidden) $('#job-title').focus();
});

// Picking a name writes it into the box, where it can be given a quantity like
// any other line. The box stays the list; this only spells things.
$('#job-add')?.addEventListener('change', () => {
  const picked = $('#job-add').value;
  if (!picked) return;

  const box = $('#job-items');
  const lines = box.value.split('\n').filter((l) => l.trim());

  if (!lines.some((l) => l.trim().toLowerCase().startsWith(picked.toLowerCase())))
    lines.push(picked);

  box.value = `${lines.join('\n')}\n`;
  $('#job-add').value = '';
  box.focus();
});

$('#job-cancel')?.addEventListener('click', () => { $('#job-form').hidden = true; });

$('#job-form')?.addEventListener('submit', async (e) => {
  e.preventDefault();

  const items = parseJobItems($('#job-items').value);
  if (!items.length) return;

  const picked = pickedPlace($('#job-place'));

  await fetch('/api/jobs', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      title: $('#job-title').value,
      kind: 'list',
      items,

      // The name is what the card shows; the id is what a plan draws with.
      destination: picked.name,
      destinationId: picked.id,
    }),
  });

  $('#job-title').value = '';
  $('#job-items').value = '';
  $('#job-place').value = '';
  $('#job-form').hidden = true;
  loadJobList();
});

/* ---------- checklists ---------- */

// These are authored preparation, deliberately separate from a shopping list:
// "set a med bed" is useful on Now even though no shop, inventory event or log
// parser can prove it has happened.
let checklists = [];

const checklistDue = (dueAt) => dueAt
  ? new Date(dueAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })
  : '';

async function checklistCall(url, method = 'POST', body = null) {
  await fetch(url, {
    method,
    headers: body ? { 'Content-Type': 'application/json' } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  await loadChecklists();
}

function showChecklistAttachment(attachment) {
  if (attachment.kind === 'location') {
    const button = el('button', 'checklist-attachment place-link', `⌖ ${attachment.label}`);
    button.title = 'Show this place on the map';
    button.addEventListener('click', () => briefingMap(attachment.placeId, attachment.target || attachment.label));
    return button;
  }

  if (attachment.kind === 'commodity') {
    const button = el('button', 'checklist-attachment', `◇ ${attachment.label}`);
    button.title = 'Open this commodity';
    button.addEventListener('click', () => openCommodity(attachment.target || attachment.label));
    return button;
  }

  if (attachment.kind === 'item') {
    const button = el('button', 'checklist-attachment', `▦ ${attachment.label}`);
    button.title = 'Find this item in Parts';
    button.addEventListener('click', () => {
      showView('parts');
      $('#parts-search').value = attachment.target || attachment.label;
      renderPartsRef();
    });
    return button;
  }

  if (attachment.kind === 'url' && /^https?:\/\//i.test(attachment.target || '')) {
    const link = el('a', 'checklist-attachment', `↗ ${attachment.label}`);
    link.href = attachment.target;
    link.target = '_blank';
    link.rel = 'noopener noreferrer';
    return link;
  }

  return el('span', 'checklist-attachment', `• ${attachment.label}`);
}

function checklistItemRow(list, item, compact = false) {
  const row = el('li', `checklist-item${item.done ? ' done' : ''}`);
  const check = document.createElement('input');
  check.type = 'checkbox';
  check.checked = item.done;
  check.title = item.done ? 'Reopen task' : 'Mark task done';
  check.addEventListener('change', () => checklistCall(`/api/checklists/${list.id}/items/${item.id}/toggle`));
  row.append(check);

  const main = el('div', 'checklist-item-main');
  main.append(el('div', 'checklist-item-text', item.text));
  if (!compact && item.note) main.append(el('div', 'muted checklist-note', item.note));
  if (item.dueAt) main.append(el('div', 'checklist-due', `◷ ${checklistDue(item.dueAt)}`));

  const attachments = item.attachments || [];
  if (attachments.length) {
    const links = el('div', 'checklist-attachments');
    for (const attachment of attachments) links.append(showChecklistAttachment(attachment));
    main.append(links);
  }
  row.append(main);

  if (!compact) {
    const remove = el('button', 'ghost tiny', '×');
    remove.title = 'Remove task';
    remove.addEventListener('click', () => checklistCall(`/api/checklists/${list.id}/items/${item.id}`, 'DELETE'));
    row.append(remove);
  }
  return row;
}

function renderPinnedChecklist(lists = checklists) {
  const card = $('#now-checklist-card');

  // Imported rows arrive with pinned forced false, so this cannot pick one up -
  // but Now is the one card where being wrong would be loudest, so it says so.
  const list = lists.find((entry) => entry.pinned && !entry.imported);
  if (!card) return;

  if (!list) {
    card.hidden = true;
    return;
  }

  $('#now-checklist-title').textContent = list.title;
  const done = list.items.filter((item) => item.done).length;
  const progress = $('#now-checklist-progress');
  progress.textContent = '';
  progress.append(jobProgress(done, list.items.length, `${done} of ${list.items.length} done`));

  const host = $('#now-checklist-items');
  host.textContent = '';
  const open = list.items.filter((item) => !item.done);
  for (const item of open.slice(0, 5)) host.append(checklistItemRow(list, item, true));
  if (open.length > 5) host.append(el('li', 'muted', `+${open.length - 5} more`));
  if (!open.length) host.append(el('li', 'inward', 'Everything is checked off.'));

  $('#now-checklist-open').onclick = () => showView('checklists');
  card.hidden = false;
}

function checklistComposer(list) {
  const form = el('form', 'checklist-composer');
  const task = document.createElement('input');
  task.type = 'text';
  task.placeholder = 'Add a task, e.g. bring tractor beam';
  task.required = true;
  form.append(task);

  const fields = el('div', 'checklist-fields');
  const place = document.createElement('select');
  place.className = 'select';
  place.title = 'Optional map location';
  fillPlaceOptions(place, '');
  fields.append(place);

  const item = document.createElement('input');
  item.type = 'search';
  item.className = 'search';
  item.setAttribute('list', 'checklist-catalogue');
  item.placeholder = 'Optional commodity, part or gear';
  item.title = 'Optional commodity, part or gear reference';
  fields.append(item);
  fillChecklistReferenceOptions();

  const due = document.createElement('input');
  due.type = 'datetime-local';
  due.className = 'search';
  due.title = 'Optional date and time';
  fields.append(due);
  form.append(fields);

  const detail = el('div', 'checklist-fields');
  const note = document.createElement('input');
  note.type = 'text';
  note.className = 'search';
  note.placeholder = 'Optional note';
  detail.append(note);
  const url = document.createElement('input');
  url.type = 'url';
  url.className = 'search';
  url.placeholder = 'Optional https:// link';
  detail.append(url);
  form.append(detail);

  const add = el('button', 'ghost', 'Add task');
  form.append(add);
  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    const where = pickedPlace(place);
    const attachments = [];
    if (where.name) attachments.push({ kind: 'location', label: where.name, target: where.name, placeId: where.id });
    const reference = item.value.trim();
    if (reference) {
      // The catalogue is filled lazily, so submitting before it lands would
      // decide "not a commodity" from an empty list and file a real commodity
      // as an item - which sends the attachment into the Parts search.
      await fillChecklistReferenceOptions();
      const commodity = catalogue?.commodities?.includes(reference);
      attachments.push({ kind: commodity ? 'commodity' : 'item', label: reference, target: reference });
    }
    if (url.value.trim()) attachments.push({ kind: 'url', label: url.value.trim(), target: url.value.trim() });

    add.disabled = true;
    try {
      await checklistCall(`/api/checklists/${list.id}/items`, 'POST', {
        text: task.value,
        dueAt: due.value ? new Date(due.value).toISOString() : null,
        note: note.value,
        attachments,
      });
    } finally {
      // A success re-renders this form away, but a failed request leaves it on
      // screen - and without this, with a button that never comes back.
      add.disabled = false;
    }
  });
  return form;
}

let catalogueFill = null;

/** One shared searchable catalogue: duplicating thousands of options per list makes adding a task sluggish. */
function fillChecklistReferenceOptions() {
  const list = $('#checklist-catalogue');
  if (!list || list.childElementCount) return Promise.resolve();

  // The guard above cannot do this alone. renderChecklists starts one of these
  // per list without awaiting, so with three lists all three would pass an
  // empty datalist and each append the whole catalogue. Share the one fill,
  // and let a later composer retry the fetch if nothing landed.
  catalogueFill ??= (async () => {
    // A failed fetch is not cached as an empty catalogue: this is the one
    // caller that can be asked again, so let the next composer retry.
    const loaded = catalogue ?? await getJson('/api/shopping/catalogue').catch(() => null);
    if (!loaded) {
      catalogueFill = null;
      return;
    }

    catalogue = loaded;
    for (const name of [...(catalogue.commodities || []), ...(catalogue.items || [])]) {
      const option = document.createElement('option');
      option.value = name;
      list.append(option);
    }
  })();

  return catalogueFill;
}

function renderChecklists(lists) {
  const host = $('#checklists-list');
  if (!host) return;
  host.textContent = '';

  if (!lists.length) {
    host.append(el('p', 'muted', 'No checklists yet. Make one for a departure, an operation, or anything you do not want to forget.'));
    return;
  }

  for (const list of lists) {
    const card = el('article', 'checklist-card');
    const head = el('div', 'job-head');
    head.append(el('b', null, list.title));
    if (list.pinned) head.append(el('span', 'job-kind owned', 'on Now'));
    head.append(el('span', 'spacer'));

    const pin = el('button', list.pinned ? 'ghost on' : 'ghost', list.pinned ? 'On Now' : 'Pin to Now');
    pin.title = 'Only one checklist is shown on Now and in the overlay';
    pin.addEventListener('click', () => checklistCall(`/api/checklists/${list.id}/pin`));
    head.append(pin);
    const remove = el('button', 'ghost danger', 'Delete');
    remove.addEventListener('click', () => checklistCall(`/api/checklists/${list.id}`, 'DELETE'));
    head.append(remove);
    card.append(head);

    const done = list.items.filter((item) => item.done).length;
    card.append(jobProgress(done, list.items.length, `${done} of ${list.items.length} done`));
    const items = el('ul', 'checklist-items');
    for (const entry of list.items) items.append(checklistItemRow(list, entry));
    if (!list.items.length) items.append(el('li', 'muted', 'No tasks yet — add the first thing you want to remember.'));
    card.append(items, checklistComposer(list));
    host.append(card);
  }
}

async function loadChecklists() {
  checklists = await getJson(`/api/checklists${importedQuery()}`).catch(() => []);
  renderChecklists(checklists);
  renderPinnedChecklist(checklists);
}

$('#checklist-create')?.addEventListener('click', async () => {
  const title = $('#checklist-title');
  if (!title.value.trim()) {
    title.focus();
    return;
  }
  await checklistCall('/api/checklists', 'POST', { title: title.value });
  title.value = '';
});

/* ---------- StarStrings ---------- */

/**
 * MrKraken's text mod, installed on request.
 *
 * Kept at arm's length on purpose. This app is read-only everywhere else, and
 * this one card writes into somebody's game install, so the page says exactly
 * which two files it writes, offers to take them back out, and never touches
 * anything without a click. The state it shows is what is on disk rather than
 * what we once did: a game patch can drop the localisation file back without
 * telling anyone, and "installed" has to mean the files are still there.
 */
async function loadStarStrings(check = false) {
  const status = $('#starstrings-status');
  if (!status) return;

  const state = await getJson(`/api/starstrings${check ? '?check=true' : ''}`).catch(() => null);

  if (!state) {
    status.textContent = 'Could not read the install state.';
    return;
  }

  $('#starstrings-remove').hidden = !state.installed;
  $('#starstrings-install').textContent = state.installed ? 'Reinstall' : 'Install';

  const bits = [];

  if (state.installed) {
    bits.push(`Installed: ${state.release || 'unknown build'}`);
    if (state.installedAt) bits.push(`put in place ${relative(state.installedAt)}`);
  } else if (state.displaced) {
    bits.push('Installed by this app, but the files are gone — a game patch will do that. Install again to put it back.');
  } else {
    bits.push('Not installed.');
  }

  if (state.latest) {
    bits.push(state.newer
      ? `A newer build is out: ${state.latest.name}. Install to take it.`
      : `Newest build: ${state.latest.name}${state.installed ? ' — you have it' : ''}`);
  }

  if (!state.gameRoot) bits.push('No game folder found, so there is nowhere to install it.');

  status.textContent = bits.join(' · ');
  $('#starstrings-install').disabled = !state.gameRoot;
}

function initStarStrings() {
  const install = $('#starstrings-install');
  if (!install) return;

  $('#starstrings-check').addEventListener('click', async () => {
    $('#starstrings-status').textContent = 'Asking GitHub…';
    await loadStarStrings(true);
  });

  install.addEventListener('click', async () => {
    install.disabled = true;
    $('#starstrings-status').textContent = 'Downloading and writing…';

    const answer = await fetch('/api/starstrings/install', { method: 'POST' })
      .then((r) => r.json())
      .catch(() => ({ problem: 'The install could not be started.' }));

    install.disabled = false;

    if (answer.problem) {
      $('#starstrings-status').textContent = answer.problem;
      return;
    }

    await loadStarStrings(true);
    alertLine($('#starstrings-status').parentElement, 'Installed. Restart Star Citizen to see it.');
  });

  $('#starstrings-remove').addEventListener('click', async () => {
    await fetch('/api/starstrings/remove', { method: 'POST' }).catch(() => {});
    await loadStarStrings();
  });

  loadStarStrings();
}

/* ---------- logbook ---------- */


/**
 * The logbook page: one merged timeline of what the pilot actually did -
 * sessions, trades, purchases, first-seen loot - straight from /api/logbook.
 */
async function loadLogbook() {
  const days = Number($('#logbook-period').value) || 0;
  const rows = await getJson(`/api/logbook?days=${days}`);

  const feed = $('#logbook-feed');
  feed.textContent = '';

  if (!rows.length) {
    feed.append(el('li', 'empty', 'Nothing in that range.'));
    return;
  }

  for (const row of rows) {
    const li = el('li');

    const when = new Date(row.at);
    const stamp = `${dateOf(row.at)} · ${when.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
    li.append(el('span', 't', stamp));

    li.append(el('span', `k ${row.kind}`, row.kind));

    // Item-ish lines pretty their class names; a session line is prose.
    const text = row.kind === 'session' ? row.what : prettyItem(row.what);
    li.append(el('span', 'd what', text));

    if (row.place) {
      const place = el('span', 'd');
      place.append(placeLink(row.place));
      li.append(place);
    }

    if (row.detail) li.append(el('span', 'd detail', row.detail));

    if (row.amount != null && row.amount !== 0) {
      const inward = Number(row.amount) > 0;
      li.append(el('span', `amt ${inward ? 'inward' : 'outward'}`,
        `${inward ? '+' : '−'}${money(Math.abs(row.amount))}`));
    }

    feed.append(li);
  }
}

onInput('#logbook-period', loadLogbook);

/* ---------- assets ---------- */

let assetsData = null;

/**
 * Ships struck off the asset count. The fleet is inferred from the logs, so
 * rentals and ships since sold appear in it; which of those the player truly
 * owns is something only they know, so it is their tick-box, kept per-browser.
 */
let excludedShips = new Set();
try {
  excludedShips = new Set(JSON.parse(localStorage.getItem('qw-assets-excluded') || '[]'));
} catch { /* private mode; exclusions just will not stick */ }

async function loadAssets() {
  assetsData = await getJson('/api/assets');
  renderAssets();

  // Prices arrive after the roster first renders; redraw it with them in.
  if (libraryStats) renderFleet(libraryStats);
}

/**
 * The asset side of the blended Fleet page: the "other assets" strip and the
 * stash table. The roster and fleet value live in renderFleet/renderFleetShips.
 */
function renderAssets() {
  const assets = assetsData;
  if (!assets) return;

  // Totals are recomputed here rather than trusted from the server, because
  // exclusion is a client-side choice.
  const counted = assets.fleet.filter((s) => !excludedShips.has(s.name));
  const fleetValue = counted.reduce((sum, s) => sum + (s.price ? Number(s.price.price) : 0), 0);

  const total = fleetValue + Number(assets.loadoutValue) + Number(assets.stashValue);

  tiles('#assets-summary', assets.priced
    ? [
        ['Estimated worth*', money(total)],
        ['Kit worn', `${money(assets.loadoutValue)} (${assets.loadoutPriced}/${assets.loadoutItems})`],
        ['Stashed', money(assets.stashValue)],
        ['Claim exposure*', money(assets.claimExposure)],
      ]
    : [['Estimated worth', 'needs Settings → community dataset + UEX']]);

  const stashBody = $('#assets-stash tbody');
  stashBody.textContent = '';

  for (const s of [...assets.stash].sort((a, b) => b.value - a.value)) {
    const tr = el('tr');
    tr.append(tdPlace(s.location));
    tr.append(el('td', 'num', String(s.items)));
    tr.append(el('td', 'num muted', String(s.priced)));
    tr.append(el('td', 'num inward', s.value > 0 ? money(s.value) : '—'));
    stashBody.append(tr);
  }
}

/* ---------- settings ---------- */

/**
 * The Settings page: the overlay switch, the community dataset, the cache.
 * Everything here re-reads its state from the server on every render, so the
 * page cannot disagree with the tray menu or another browser tab for long.
 */
async function renderSettings() {
  // Overlay - only offered when the server is hosted inside QuantumWake.exe.
  try {
    const overlay = await getJson('/api/overlay');
    const toggle = $('#overlay-toggle');

    toggle.hidden = !overlay.available;
    $('#overlay-unavailable').hidden = overlay.available;
    $('#overlay-copy').hidden = !overlay.available;

    if (overlay.available) {
      toggle.textContent = overlay.visible ? 'Overlay is on — hide it' : 'Show the overlay';
      toggle.classList.toggle('on', overlay.visible);
      toggle.dataset.visible = String(overlay.visible);
    }
  } catch { /* server unreachable; the page will retry on next visit */ }

  // Community dataset.
  try {
    const community = await getJson('/api/community');

    $('#settings-community-enable').hidden = community.enabled;
    $('#settings-community-disable').hidden = !community.enabled;

    $('#settings-community-status').textContent = community.enabled
      ? `${community.commodities} commodities, fetched ${community.fetchedAt ? dateOf(community.fetchedAt) : '—'}`
      : 'not downloaded';
  } catch { /* as above */ }

  // UEX.
  try {
    const uex = await getJson('/api/uex');

    $('#uex-enable').hidden = uex.enabled;
    $('#uex-refresh').hidden = !uex.enabled;
    $('#uex-disable').hidden = !uex.enabled;

    $('#uex-status').textContent = uex.enabled
      ? `${uex.prices} commodities priced, fetched ${uex.fetchedAt ? dateOf(uex.fetchedAt) : '—'}`
      : 'not fetched';

    $('#uex-preview').hidden = !(uex.enabled && uex.hasCredentials);
    $('#uex-save-creds').textContent = uex.hasCredentials ? 'Replace keys' : 'Save keys';
  } catch { /* as above */ }

  await renderUexAuto();
  await renderUexFeeds();
  await renderSignals();
  await renderExportPreview();
}

/* ---------- files other pilots have shared ---------- */

let importBatches = [];

/**
 * Whether pages show imported rows beside the reader's own, and whose.
 *
 * One switch for the whole app rather than one per page: the question somebody
 * asks is "am I looking at Bob's things or mine", never "am I looking at Bob's
 * jobs but my own checklists". Off by default, because importing a friend's
 * forty jobs and finding your own page now has forty-three cards on it is an
 * ambush by a feature used once.
 */
let showImported = 'none';

try {
  showImported = localStorage.getItem('qw-show-imported') || 'none';
} catch { /* private browsing; the default is the safe one anyway */ }

/**
 * The suffix every list endpoint takes, so no page has to remember the name.
 *
 * Empty when nothing is being shown, which is the ordinary case: the request
 * then goes out exactly as it did before this feature existed, and the server
 * default and the client default cannot drift apart.
 */
function importedQuery() {
  return showImported === 'none' ? '' : `?imported=${encodeURIComponent(showImported)}`;
}

function setShowImported(value) {
  showImported = value || 'none';

  try {
    localStorage.setItem('qw-show-imported', showImported);
  } catch { /* as above */ }

  // Everything that can carry an imported row re-reads itself.
  loadJobs().catch(() => {});
  loadChecklists().catch(() => {});
  loadTrips?.().catch(() => {});
  renderSharedReceipts().catch(() => {});
  renderSharedBlueprints().catch(() => {});
}

/**
 * The control that turns other people's rows on.
 *
 * Drawn only when there is something to show. An empty dropdown offering to
 * filter by nobody is a worse answer than no dropdown.
 */
function renderImportedFilter() {
  const host = $('#imports-filter');
  if (!host) return;

  host.textContent = '';

  const usable = importBatches.filter((b) => b.readable && !b.hidden && b.classes.length);
  if (!usable.length) return;

  host.append(el('label', 'muted', 'Show shared rows on my pages: '));

  const select = el('select', 'select');
  const options = [['none', 'no'], ['all', 'from everyone']];
  for (const batch of usable) options.push([batch.id, `only ${batch.handle || batch.sourceName}`]);

  for (const [value, label] of options) {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = label;
    select.append(option);
  }

  select.value = options.some(([v]) => v === showImported) ? showImported : 'none';
  select.addEventListener('change', () => setShowImported(select.value));
  host.append(select);
}

/** A tally, with the zeroes left out. */
function countLine(counts) {
  const parts = [];
  if (counts.receipts) parts.push(`${counts.receipts.toLocaleString()} trades`);
  if (counts.blueprints) parts.push(`${counts.blueprints} blueprints`);
  if (counts.jobs) parts.push(`${counts.jobs} jobs`);
  if (counts.checklists) parts.push(`${counts.checklists} checklists`);
  if (counts.trips) parts.push(`${counts.trips} flight plans`);
  if (counts.runActions) parts.push(`${counts.runActions} run-sheet lines`);
  return parts.join(' · ');
}

/**
 * One imported file.
 *
 * Says when it was written as well as when it was imported, because a file
 * taken this morning can hold a price from March and the older date is the one
 * that decides whether the numbers are worth anything.
 */
function importCard(batch) {
  const card = el('article', 'import-card');

  const head = el('div', 'job-head');
  head.append(el('b', null, batch.handle || 'Someone'));
  if (batch.note) head.append(el('span', 'job-kind', batch.note));
  if (!batch.readable) head.append(el('span', 'job-kind danger', 'cannot be read'));
  if (batch.hidden) head.append(el('span', 'job-kind', 'hidden'));
  head.append(el('span', 'spacer'));

  const hide = el('button', 'ghost tiny', batch.hidden ? 'Show' : 'Hide');
  hide.title = 'Hiding keeps the file. Removing does not.';
  hide.addEventListener('click', () => importCall(`/api/imports/${batch.id}/hide`));
  head.append(hide);

  const remove = el('button', 'ghost danger tiny', 'Remove');
  remove.title = 'Takes this file away completely. Your own work is not touched.';
  remove.addEventListener('click', () => importCall(`/api/imports/${batch.id}`, 'DELETE'));
  head.append(remove);
  card.append(head);

  card.append(el('div', 'muted', `${batch.sourceName} — imported ${dateOf(batch.importedAt)}, `
    + `written ${dateOf(batch.exportedAt)} by Quantum Wake ${batch.producerVersion}`));

  const held = countLine(batch.counts);
  card.append(el('div', 'import-counts', held || 'Nothing left in this file.'));

  // Kept for ever and said plainly: "why does this show 41 when his file said
  // 43" has to be answerable a month later.
  const dropped = countLine(batch.rejected);
  if (dropped) card.append(el('div', 'muted', `Could not be read: ${dropped}.`));

  const cut = countLine(batch.truncated);
  if (cut) card.append(el('div', 'muted', `Too many to keep, so left out: ${cut}.`));

  if (!batch.readable) {
    card.append(el('div', 'muted', `This file is in format ${batch.formatVersion}, which this `
      + 'build does not read. It is kept rather than dropped, since the copy you were sent may '
      + 'be the only one. Update Quantum Wake, or remove it.'));
  }

  if (batch.classes.length) {
    const row = el('div', 'import-classes');
    const named = [['receipts', 'trades'], ['blueprints', 'blueprints'], ['authored', 'jobs and lists']];

    for (const [key, label] of named) {
      if (!batch.classes.includes(key)) continue;
      const drop = el('button', 'ghost tiny', `Remove the ${label}`);
      drop.addEventListener('click', () => importCall(`/api/imports/${batch.id}/${key}`, 'DELETE'));
      row.append(drop);
    }

    card.append(row);
  }

  return card;
}

function renderImports(payload) {
  const host = $('#imports-list');
  if (!host) return;

  importBatches = payload.batches || [];
  host.textContent = '';

  if (payload.quarantined) {
    host.append(el('p', 'muted', 'An earlier imports file could not be read. It was kept as '
      + `${payload.quarantined} rather than overwritten, because the files it held came from `
      + 'other people.'));
  }

  if (!importBatches.length) {
    host.append(el('p', 'muted', 'No shared files yet. Open one a friend sent you — it stays '
      + 'separate from your own history, and you can remove it whenever you like.'));
    return;
  }

  for (const batch of importBatches) host.append(importCard(batch));
  renderImportedFilter();
}

async function loadImports() {
  renderImports(await getJson('/api/imports').catch(() => ({ batches: [] })));
}

async function importCall(url, method = 'POST') {
  try {
    await fetch(url, { method });
  } finally {
    await loadImports();
  }
}

/**
 * Reads a file the user picked and offers it to the server.
 *
 * The text travels in a JSON body rather than as multipart: nothing else here
 * posts multipart, FileReader hands back the string for nothing, and the size
 * check stays one question about how many bytes arrived.
 */
async function importFile(file) {
  $('#imports-status').textContent = `Reading ${file.name}…`;

  const text = await new Promise((resolve) => {
    const reader = new FileReader();
    reader.onload = (event) => resolve(event.target.result);
    reader.readAsText(file);
  });

  await sendImport(text, file.name, false);
}

async function sendImport(text, sourceName, force) {
  const status = $('#imports-status');

  const response = await fetch(`/api/imports${force ? '?force=true' : ''}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ document: text, sourceName }),
  });

  const answer = await response.json().catch(() => null);

  // Already held. Asked rather than duplicated, because a double-clicked picker
  // and a deliberate re-import after a purge look identical from here.
  if (response.status === 409 && answer && answer.batch) {
    const already = answer.batch;
    status.textContent = `You imported this on ${dateOf(already.importedAt)} — `
      + `${countLine(already.counts) || 'nothing left in it'}. `;

    const again = el('button', 'ghost tiny', 'Import it again anyway');
    again.addEventListener('click', () => sendImport(text, sourceName, true).catch(() => {}));
    status.append(again);
    return;
  }

  if (!response.ok) {
    status.textContent = (answer && answer.message) || 'That file could not be read.';
    return;
  }

  status.textContent = `Imported ${countLine(answer.batch.counts) || 'nothing'} from `
    + `${answer.batch.handle || 'someone'}.`;

  await loadImports();
}

$('#imports-pick')?.addEventListener('click', () => $('#imports-file')?.click());

$('#imports-file')?.addEventListener('change', (event) => {
  const file = event.target.files && event.target.files[0];
  if (!file) return;

  importFile(file)
    .catch(() => { $('#imports-status').textContent = 'That file could not be read.'; })
    // Cleared so that picking the same file twice still fires a change.
    .finally(() => { event.target.value = ''; });
});

$('#imports-clear')?.addEventListener('click', () => {
  $('#imports-status').textContent = '';
  importCall('/api/imports', 'DELETE').catch(() => {});
});

/**
 * Trades and blueprints from other people's files, in their own sections.
 *
 * Kept out of the pages' own payloads on purpose. Cargo computes four totals
 * from /api/commodities and the Blueprints picker feeds "Set as goal" from
 * /api/blueprints/owned; mixing imported rows into either would mean
 * remembering to filter in five places, and the one that gets forgotten
 * produces lifetime earnings counting somebody else's sales, or a build plan
 * for a blueprint the reader does not hold.
 */
async function renderSharedReceipts() {
  const host = $('#cargo-shared');
  if (!host) return;

  host.textContent = '';
  if (showImported === 'none') return;

  const rows = await getJson(`/api/imports/receipts${importedQuery()}`).catch(() => []);
  if (!rows.length) return;

  const card = el('section', 'shared-block');
  card.append(el('h3', null, 'Trades from shared files'));
  card.append(el('p', 'muted', 'Other people’s receipts, kept out of your own totals '
    + 'above. Prices are what they were quoted, where and when they say.'));

  const table = el('table', 'data');
  const head = el('thead');
  const headRow = el('tr');
  for (const label of ['When', 'Who', 'Commodity', 'Place', 'SCU', 'Per SCU']) {
    headRow.append(el('th', label === 'SCU' || label === 'Per SCU' ? 'num' : null, label));
  }
  head.append(headRow);
  table.append(head);

  const body = el('tbody');

  for (const row of rows.slice(0, 200)) {
    const tr = el('tr');
    tr.append(el('td', 'muted', dateOf(row.at)));
    tr.append(el('td', null, row.imported.handle || 'someone'));

    // A name this install cannot resolve is not an error: their dataset knew
    // something ours does not, or the other way round.
    tr.append(el('td', row.commodity ? null : 'muted', row.commodity || 'unnamed'));
    tr.append(el('td', 'muted', row.place));
    tr.append(el('td', 'num', row.scu.toLocaleString()));
    tr.append(el('td', 'num', money(row.unitPrice)));
    body.append(tr);
  }

  table.append(body);
  card.append(table);

  if (rows.length > 200) {
    card.append(el('p', 'muted', `Showing the newest 200 of ${rows.length.toLocaleString()}.`));
  }

  host.append(card);
}

async function renderSharedBlueprints() {
  const host = $('#blueprints-shared');
  if (!host) return;

  host.textContent = '';
  if (showImported === 'none') return;

  const rows = await getJson(`/api/imports/blueprints${importedQuery()}`).catch(() => []);
  if (!rows.length) return;

  const card = el('section', 'shared-block');
  card.append(el('h3', null, 'Held by others'));
  card.append(el('p', 'muted', 'Blueprints the files you have been sent say somebody holds. '
    + 'They are not in the picker above, because you cannot craft from someone else’s '
    + 'library — this answers who to ask.'));

  const list = el('ul', 'shared-blueprints');

  // Grouped by name, so "who can craft this" is one line rather than a list to
  // read across. That is the question the section exists to answer.
  const byName = new Map();

  for (const row of rows) {
    const holders = byName.get(row.name) || [];
    const who = row.imported.handle || 'someone';
    if (!holders.includes(who)) holders.push(who);
    byName.set(row.name, holders);
  }

  for (const [name, holders] of [...byName.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
    const line = el('li');
    line.append(el('b', null, name));
    line.append(el('span', 'muted', ` — ${holders.join(', ')}`));
    list.append(line);
  }

  card.append(list);
  host.append(card);
}

/* ---------- sharing a file of your own ---------- */

/**
 * The chosen window in days, where zero legitimately means all time.
 *
 * Read carefully because the two failure values are far apart: an empty select
 * through Number() is zero, which would quietly turn "the last week" into every
 * trade ever made and send a hundred times what was asked for.
 */
function exportDays() {
  // Empty string through Number() is zero, not NaN, so the absent case has to
  // be caught before the conversion rather than after it.
  const raw = $('#export-days')?.value;
  if (raw === undefined || raw === null || raw === '') return 7;

  const chosen = Number(raw);
  return Number.isFinite(chosen) && chosen >= 0 ? chosen : 7;
}

/** What the boxes currently say, in the shape the API takes. */
function exportChoice() {
  return {
    receipts: Boolean($('#export-receipts')?.checked),
    blueprints: Boolean($('#export-blueprints')?.checked),
    authored: Boolean($('#export-authored')?.checked),
    handle: Boolean($('#export-handle')?.checked),
    days: exportDays(),
  };
}

/**
 * What would go, before it goes.
 *
 * Counts only - the preview endpoint never returns rows. The point is that a
 * click to share follows seeing what sharing means, not that the page gets a
 * second copy of the data.
 */
async function renderExportPreview() {
  const line = $('#export-preview');
  if (!line) return;

  const choice = exportChoice();

  if (!choice.receipts && !choice.blueprints && !choice.authored) {
    line.textContent = 'Nothing ticked, so there is nothing to save.';
    return;
  }

  try {
    const counts = await getJson(
      `/api/export/preview?receipts=${choice.receipts}&blueprints=${choice.blueprints}`
      + `&authored=${choice.authored}&days=${choice.days}`);

    const parts = [];
    if (choice.receipts) {
      parts.push(`${counts.receipts.toLocaleString()} ${counts.receipts === 1 ? 'trade' : 'trades'}`
        + (choice.days ? ` from the last ${choice.days === 1 ? 'day' : `${choice.days} days`}` : ', all time'));
    }
    if (choice.blueprints) parts.push(`${counts.blueprints} blueprints`);
    if (choice.authored) {
      parts.push(`${counts.jobs} jobs, ${counts.checklists} checklists, ${counts.trips} flight plans`);
    }

    line.textContent = `Would save ${parts.join(' · ')}.`;
  } catch {
    line.textContent = '';
  }
}

/**
 * Saves the document the server built.
 *
 * A POST rather than a link, because export must not be a GET: the LAN rule
 * lets reads through, and this is the one response that hands over the whole
 * history at once. So the blob comes back from fetch and is clicked into the
 * downloads folder here.
 */
async function saveExport() {
  const button = $('#export-save');
  const status = $('#export-status');
  const choice = exportChoice();

  if (!choice.receipts && !choice.blueprints && !choice.authored) {
    status.textContent = 'Tick at least one thing first.';
    return;
  }

  button.disabled = true;
  status.textContent = 'Building the file…';

  let url = null;

  try {
    const response = await fetch('/api/export', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(choice),
    });

    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      status.textContent = problem?.message || 'The file could not be built.';
      return;
    }

    // The server names it; the header is where that name is.
    const name = /filename="?([^";]+)"?/i.exec(
      response.headers.get('content-disposition') || '')?.[1] || 'quantumwake-export.json';

    const blob = await response.blob();
    url = URL.createObjectURL(blob);

    const link = el('a');
    link.href = url;
    link.download = name;
    link.click();

    status.textContent = `Saved ${name} — ${Math.round(blob.size / 1024).toLocaleString()} KB.`;
  } catch {
    status.textContent = 'The file could not be built.';
  } finally {
    // Revoking frees the blob; doing it before the click lands cancels the save.
    if (url) setTimeout(() => URL.revokeObjectURL(url), 30000);
    button.disabled = false;
  }
}

for (const id of ['#export-receipts', '#export-blueprints', '#export-authored', '#export-days']) {
  $(id)?.addEventListener('change', () => renderExportPreview().catch(() => {}));
}

$('#export-save')?.addEventListener('click', () => saveExport().catch(() => {}));

/**
 * Which signals are still arriving, and when each last did.
 *
 * The date is the point. Every telemetry removal so far has looked, from in
 * here, exactly like a quiet week - so the table leads with when a thing last
 * happened, and marks the rows that have gone quiet rather than leaving the
 * reader to compare eighteen dates by eye.
 *
 * "Quiet" is measured against this install's own last session, not against
 * today: someone coming back after a month away should not be told that
 * everything broke while they were gone.
 */
async function renderSignals() {
  const table = $('#signals-table');
  if (!table) return;

  let rows;
  try {
    rows = await getJson('/api/signals');
  } catch {
    return;
  }

  const body = table.querySelector('tbody');
  body.textContent = '';

  const played = Math.max(...rows.map((r) => (r.lastSeen ? Date.parse(r.lastSeen) : 0)), 0);
  const QUIET = 21 * 86400000;

  let group = null;

  for (const row of rows) {
    if (row.group !== group) {
      group = row.group;

      const head = el('tr', 'group-row');
      const cell = el('td', 'muted', group);
      cell.colSpan = 4;
      head.append(cell);
      body.append(head);
    }

    const seen = row.lastSeen ? Date.parse(row.lastSeen) : null;
    const quiet = seen !== null && played - seen > QUIET;

    const tr = el('tr');
    tr.append(el('td', null, row.name));
    tr.append(el('td', row.total ? 'num' : 'num muted', row.total ? row.total.toLocaleString() : '—'));
    tr.append(el('td', row.sessions ? 'num' : 'num muted', row.sessions || '—'));

    const last = el('td', seen === null ? 'muted' : (quiet ? 'outward' : 'muted'));
    last.textContent = seen === null ? 'never' : `${dateOf(row.lastSeen)}${quiet ? ' · gone quiet' : ''}`;
    tr.append(last);

    if (row.note) tr.title = row.note;
    body.append(tr);
  }
}

/**
 * The automatic refresh switch.
 *
 * Disabled rather than hidden while UEX is off: a switch that vanishes reads as
 * a feature that does not exist, whereas a greyed one with a reason reads as a
 * door you have not opened yet. The note carries the interval and the last
 * attempt, because "automatic" without a period is a promise with no shape.
 */
async function renderUexAuto() {
  const toggle = $('#uex-auto-toggle');
  const note = $('#uex-auto-note');
  if (!toggle) return;

  try {
    const auto = await getJson('/api/uex/auto');

    toggle.checked = auto.automatic;
    toggle.disabled = !auto.uexEnabled;

    if (!auto.uexEnabled) {
      note.textContent = 'fetch prices first';
      return;
    }

    const every = `every ${auto.staleAfterHours} hours`;

    note.textContent = auto.automatic
      ? (auto.lastCheckedAt ? `${every} · last tried ${dateOf(auto.lastCheckedAt)}` : every)
      : `off · prices stay as fetched`;
  } catch { /* Settings redraws on its next visit */ }
}

$('#uex-auto-toggle')?.addEventListener('change', async (e) => {
  const toggle = e.currentTarget;

  try {
    await fetch(`/api/uex/auto/answer?automatic=${toggle.checked}`, { method: 'POST' });
  } catch {
    toggle.checked = !toggle.checked;
  }

  await renderUexAuto();
});

/**
 * The optional UEX feeds, each with its own switch: they serve different
 * pages, so a trader and a miner should not have to take each other's bytes.
 */
async function renderUexFeeds() {
  const list = $('#uex-feed-list');
  if (!list) return;

  let feeds;
  try {
    feeds = await getJson('/api/uex/feeds');
  } catch {
    return;
  }

  list.textContent = '';

  for (const feed of feeds) {
    const row = el('div', 'uex-feed');

    const head = el('div', 'uex-feed-head');
    head.append(el('b', null, feed.title));
    head.append(el('span', 'cost', feed.cost));
    row.append(head);

    row.append(el('div', 'muted feed-copy', feed.description));

    const actions = el('div', 'uex-feed-actions');
    const button = el('button', 'ghost', feed.enabled ? 'Refresh' : 'Fetch');
    const status = el('span', 'muted');

    status.textContent = feed.enabled
      ? `on · fetched ${feed.fetchedAt ? ago(feed.fetchedAt) : '—'}`
      : 'off';

    button.addEventListener('click', () =>
      uexAction(`/api/uex/feeds/${feed.key}/enable`, status, button));

    actions.append(button);

    if (feed.enabled) {
      const drop = el('button', 'ghost', 'Drop');
      drop.addEventListener('click', () =>
        uexAction(`/api/uex/feeds/${feed.key}/disable`, status, drop));
      actions.append(drop);
    }

    actions.append(status);
    row.append(actions);
    list.append(row);
  }
}

async function uexAction(path, statusNode, button) {
  button.disabled = true;
  statusNode.textContent = 'working…';

  try {
    const result = await fetch(path, { method: 'POST' });
    if (!result.ok) throw new Error((await result.json()).title || result.statusText);
    statusNode.textContent = '';
  } catch (e) {
    statusNode.textContent = `failed: ${e.message}`;
  } finally {
    button.disabled = false;
    renderSettings();
    loadMarket().catch(() => {});
  }
}

$('#uex-enable').addEventListener('click', (e) => uexAction('/api/uex/enable', $('#uex-status'), e.currentTarget));
$('#uex-refresh').addEventListener('click', (e) => uexAction('/api/uex/enable', $('#uex-status'), e.currentTarget));
$('#uex-disable').addEventListener('click', (e) => uexAction('/api/uex/disable', $('#uex-status'), e.currentTarget));

$('#uex-save-creds').addEventListener('click', async (e) => {
  const button = e.currentTarget;
  button.disabled = true;

  try {
    await fetch('/api/uex/credentials', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: $('#uex-token').value, secret: $('#uex-secret').value }),
    });

    $('#uex-token').value = '';
    $('#uex-secret').value = '';
  } finally {
    button.disabled = false;
    renderSettings();
  }
});

/**
 * The two-step report: preview says exactly what would be sent and to which
 * terminals; only the second button transmits anything.
 */
$('#uex-preview').addEventListener('click', async () => {
  const status = $('#uex-push-status');

  try {
    const rows = await getJson('/api/uex/pushable');
    const matched = rows.filter((r) => r.terminalId && r.commodityId);
    const terminals = new Set(matched.map((r) => r.terminalName));
    const skipped = rows.length - matched.length;

    status.textContent = matched.length
      ? `${matched.length} price${matched.length === 1 ? '' : 's'} across `
        + `${terminals.size} terminal${terminals.size === 1 ? '' : 's'}`
        + (skipped ? ` · ${skipped} skipped (place not matched to a UEX terminal)` : '')
      : rows.length
        ? `nothing sendable: ${rows.length} sale${rows.length === 1 ? '' : 's'} but no place matched a UEX terminal`
        : 'no named sales in the last 30 days';

    $('#uex-send').hidden = matched.length === 0;
  } catch (e) {
    status.textContent = `failed: ${e.message}`;
  }
});

$('#uex-send').addEventListener('click', async (e) => {
  const button = e.currentTarget;
  const status = $('#uex-push-status');

  button.disabled = true;
  status.textContent = 'sending…';

  try {
    const result = await fetch('/api/uex/push', { method: 'POST' });
    const body = await result.json();
    if (!result.ok) throw new Error(body.title || result.statusText);

    status.textContent = body.results.join(' · ') || 'nothing sent';
    button.hidden = true;
  } catch (err) {
    status.textContent = `failed: ${err.message}`;
  } finally {
    button.disabled = false;
  }
});

$('#overlay-toggle').addEventListener('click', async (e) => {
  const next = e.currentTarget.dataset.visible !== 'true';

  try {
    await fetch(`/api/overlay?visible=${next}`, { method: 'POST' });
  } finally {
    renderSettings();
  }
});

$('#settings-community-enable').addEventListener('click', (e) =>
  setCommunity(true, $('#settings-community-status'), e.currentTarget));

$('#settings-community-disable').addEventListener('click', (e) =>
  setCommunity(false, $('#settings-community-status'), e.currentTarget));

$('#settings-rescan').addEventListener('click', async (e) => {
  const button = e.currentTarget;
  const status = $('#settings-rescan-status');

  button.disabled = true;
  status.textContent = 'rescanning…';

  try {
    const result = await getJson2('/api/scan?force=true');
    status.textContent = `${result.sessions} sessions from a full re-read`;
    await loadHistory();
  } catch (err) {
    status.textContent = `failed: ${err.message}`;
  } finally {
    button.disabled = false;
  }
});

/** POST that expects JSON back; getJson is GET-only. */
async function getJson2(url) {
  const response = await fetch(url, { method: 'POST' });
  if (!response.ok) throw new Error(`${url} -> ${response.status}`);
  return response.json();
}

function renderCommodities(trades) {
  const sells = trades.filter((t) => t.isSell);
  const buys = trades.filter((t) => !t.isSell);

  const revenue = sells.reduce((total, t) => total + Number(t.amount), 0);
  const scuSold = sells.reduce((total, t) => total + t.scu, 0);
  const outlay = buys.reduce((total, t) => total + Number(t.amount), 0);

  // What better selling would have earned: the gap between each sale's unit
  // price and UEX's best, over the sold volume. Zero rows when UEX is off.
  const leftOnTable = sells
    .filter((t) => t.uexBestSell > 0 && Number(t.uexBestSell) > Number(t.unitPrice))
    .reduce((total, t) => total + (Number(t.uexBestSell) - Number(t.unitPrice)) * t.scu, 0);

  const cargoTiles = [
    ['Revenue', money(revenue)],
    ['SCU sold', scuSold.toLocaleString()],
    ['Average per SCU', scuSold ? money(revenue / scuSold) : '—'],
    ['Cargo bought', money(outlay)],
    ['Sales', sells.length],
    ['Best sale', sells.length ? money(Math.max(...sells.map((t) => Number(t.amount)))) : '—'],
  ];

  if (sells.some((t) => t.uexBestSell > 0))
    cargoTiles.push(['Left on the table*', money(leftOnTable)]);

  tiles('#cargo-summary', cargoTiles);

  // Revenue by place, with the volume that produced it.
  const byShop = new Map();
  for (const trade of sells) {
    const current = byShop.get(trade.place) || { amount: 0, scu: 0 };
    current.amount += Number(trade.amount);
    current.scu += trade.scu;
    byShop.set(trade.place, current);
  }

  drawCargoEarnings(trades);

  bars('#cargo-shops',
    [...byShop.entries()]
      .sort((a, b) => b[1].amount - a[1].amount)
      .map(([shop, v]) => ({ label: shop, value: v.amount, note: `${v.scu} SCU` })),
    money);

  const body = $('#cargo-table tbody');
  body.textContent = '';

  if (!trades.length) {
    const tr = el('tr');
    const td = el('td', 'muted', 'No cargo trades in that range.');
    td.colSpan = 8;
    tr.append(td);
    body.append(tr);
    return;
  }

  const anyBest = trades.some((t) => t.uexBestSell > 0);
  $('#cargo-best-col').hidden = !anyBest;

  for (const trade of trades) {
    const tr = el('tr');
    tr.append(el('td', null, dateOf(trade.at)));
    tr.append(el('td', null, trade.isSell ? 'Sold' : 'Bought'));
    tr.append(el('td', trade.commodity ? null : 'muted', trade.commodity ?? '—'));
    tr.append(tdPlace(trade.place));
    tr.append(el('td', 'num', String(trade.scu)));
    tr.append(el('td', `num ${trade.isSell ? 'inward' : 'outward'}`, money(trade.amount)));
    tr.append(el('td', 'num muted', money(trade.unitPrice)));

    if (anyBest) {
      const cell = el('td', 'num');

      if (trade.uexBestSell > 0) {
        const delta = ((Number(trade.unitPrice) - Number(trade.uexBestSell)) / Number(trade.uexBestSell)) * 100;
        cell.className = `num ${delta >= -3 ? 'inward' : 'outward'}`;
        cell.textContent = `${delta >= 0 ? '+' : ''}${delta.toFixed(0)}%`;
        cell.title = `UEX best sell today: ${money(trade.uexBestSell)}/SCU`;
      } else {
        cell.className = 'num muted';
        cell.textContent = '—';
      }

      tr.append(cell);
    }

    body.append(tr);
  }
}

/* ---------- shared widgets ---------- */

/**
 * Every table sorts by clicking its headers. One generic pass over the DOM
 * rather than per-page wiring, so a table added next month is sortable for
 * free. Numbers sort as numbers, dates as dates, everything else as text;
 * re-rendering (a period change, a rescan) resets to the page's own order.
 */
function makeTablesSortable() {
  for (const table of document.querySelectorAll('main table')) {
    [...table.querySelectorAll('thead th')].forEach((th, index) => {
      if (!th.textContent.trim()) return;

      th.classList.add('sortable');
      th.title = 'Sort';

      th.addEventListener('click', () => {
        const descending = !th.classList.contains('sort-desc');

        for (const other of table.querySelectorAll('thead th'))
          other.classList.remove('sort-asc', 'sort-desc');
        th.classList.add(descending ? 'sort-desc' : 'sort-asc');

        const body = table.tBodies[0];
        if (!body) return;

        // Empty-state rows span the table and stay where they are.
        const rows = [...body.rows].filter((r) => r.cells.length > 1);

        rows.sort((a, b) => compareCells(a, b, index) * (descending ? -1 : 1));
        rows.forEach((r) => body.append(r));
      });
    });
  }
}

function compareCells(rowA, rowB, index) {
  const a = (rowA.cells[index]?.textContent ?? '').trim();
  const b = (rowB.cells[index]?.textContent ?? '').trim();

  // Dates first: "Aug 17, 2026" parsed numerically would sort by day-of-month.
  const dateish = /^[A-Z][a-z]{2} \d{1,2}, \d{4}/;
  if (dateish.test(a) && dateish.test(b))
    return Date.parse(a) - Date.parse(b);

  const numA = parseFloat(a.replace(/[^0-9.-]/g, ''));
  const numB = parseFloat(b.replace(/[^0-9.-]/g, ''));
  if (Number.isFinite(numA) && Number.isFinite(numB) && /\d/.test(a) && /\d/.test(b))
    return numA - numB;

  return a.localeCompare(b);
}

makeTablesSortable();

function tiles(container, entries) {
  const node = $(container);
  node.textContent = '';

  for (const [label, value] of entries) {
    const tile = el('div', 'tile');
    tile.append(el('div', 'n', String(value)));
    tile.append(el('div', 'l', label));
    node.append(tile);
  }
}

/* Manufacturer prefixes as they appear in vehicle ids. */
/**
 * Manufacturer codes to names. The hand-typed entries are the fallback and
 * the source of logo files; the community dataset's full table is merged over
 * them at boot when it is enabled, so every code the game knows resolves.
 */
const MANUFACTURERS = {
  DRAK: 'Drake Interplanetary', ANVL: 'Anvil Aerospace', RSI: 'Roberts Space Industries',
  MISC: 'MISC', ORIG: 'Origin Jumpworks', AEGS: 'Aegis Dynamics',
  CRUS: 'Crusader Industries', CNOU: 'Consolidated Outland', TMBL: 'Tumbril',
  ESPR: 'Esperia', BANU: 'Banu', KRIG: 'Kruger Intergalactic',
  ARGO: 'ARGO Astronautics', AOPO: 'Aopoa', GATS: 'Gatac', MRAI: 'Mirai',
};

/** The codes with a local logo image; the merged map must not grow this set. */
const MANUFACTURER_LOGOS = new Set(Object.keys(MANUFACTURERS));

/**
 * Every way a ship name can announce its maker, to the code that names its
 * logo file.
 *
 * Ship names used to arrive as log ids - "DRAK Corsair" - so the first word
 * was the code and the logo lookup was a dictionary hit. Once the community
 * dataset started resolving real names, the same ship reads "Drake Corsair"
 * and that lookup quietly missed for every maker whose code is not also its
 * name, which is most of them: RSI and MISC kept their badges and nobody else
 * did. So codes AND names, longest match first, because "Consolidated Outland"
 * must beat "Consolidated".
 */
let makerAliases = new Map();

function buildMakerAliases() {
  makerAliases = new Map();

  for (const [code, name] of Object.entries(MANUFACTURERS)) {
    makerAliases.set(code.toLowerCase(), code);
    makerAliases.set(name.toLowerCase(), code);
    makerAliases.set(name.split(' ')[0].toLowerCase(), code);
  }
}

buildMakerAliases();

/** Splits a ship name into its maker and the model that follows. */
function makerOf(shipName) {
  const words = String(shipName).trim().split(/\s+/);

  for (let take = Math.min(3, words.length); take >= 1; take--) {
    const code = makerAliases.get(words.slice(0, take).join(' ').toLowerCase());

    if (code) {
      return {
        code,
        name: MANUFACTURERS[code] || code,
        model: words.slice(take).join(' ') || shipName,
      };
    }
  }

  return { code: null, name: words[0], model: words.slice(1).join(' ') || shipName };
}

/** The active ship deserves the same manufacturer mark as the Fleet page. */
function renderNowShip(shipName) {
  $('#now-ship').textContent = shipName || '—';

  const badge = $('#now-ship-logo');
  badge.textContent = '';
  const maker = shipName && makerOf(shipName);
  const hasLogo = maker?.code && MANUFACTURER_LOGOS.has(maker.code);
  badge.hidden = !hasLogo;
  if (!hasLogo) return;

  const image = document.createElement('img');
  image.src = `assets/manufacturers/${maker.code}.png`;
  image.alt = maker.name;
  image.title = maker.name;
  badge.append(image);
}

async function loadManufacturers() {
  try {
    const table = await getJson('/api/manufacturers');
    for (const [code, name] of Object.entries(table))
      if (!MANUFACTURERS[code]) MANUFACTURERS[code] = name;

    // The new names are new ways for a ship to announce its maker.
    buildMakerAliases();
  } catch { /* community data off; the fallback covers the common fleet */ }
}

const money = (n) => `${Math.round(Number(n) || 0).toLocaleString()} aUEC`;

/* ---------- fleet ---------- */

/* Kept so the filter controls can re-render without another fetch. */
let libraryStats = null;

function renderFleet(stats) {
  libraryStats = stats;

  // Unticked ships are not owned - a rental, or since sold - so every total
  // ignores them. Flight time and sorties still count: those happened.
  const owned = stats.ships.filter((s) => !excludedShips.has(s.name));

  // The game's entitlement count bundles ships and ground vehicles into one
  // number and never names them, so it is labelled as its own thing rather
  // than pretending to agree with the ticked roster.
  const fleetTiles = [
    ['Owned per game*', stats.fleetSize ?? '—'],
    ['Roster ticked', `${owned.length} of ${stats.ships.length}`],
    ['Total flights', owned.reduce((sum, s) => sum + s.sorties, 0)],
    ['Time aboard', `~${duration(owned.reduce((sum, s) => sum + toSeconds(s.estimatedTime), 0))}`],
  ];

  if (assetsData?.priced) {
    const value = owned.reduce((sum, s) => sum + shipPriceOf(s.name), 0);
    fleetTiles.push(['Fleet value*', money(value)]);
  }

  tiles('#fleet-summary', fleetTiles);

  drawFleetChart(stats.fleetHistory || []);
  renderFleetShips();
}

/** UEX price for one roster ship, 0 when unpriced or assets are off. */
function shipPriceOf(name) {
  const row = assetsData?.fleet?.find((f) => f.name === name);
  return row?.price ? Number(row.price.price) : 0;
}

/** Applies the search box and the last-flown period filter. */
function renderFleetShips() {
  const grid = $('#fleet-ships');
  const vehicleGrid = $('#fleet-vehicles');
  grid.textContent = '';
  vehicleGrid.textContent = '';

  if (!libraryStats) return;

  const term = ($('#fleet-search').value || '').trim().toLowerCase();
  const days = Number($('#fleet-period').value) || 0;
  const cutoff = days ? Date.now() - days * 86400000 : null;

  // Unticked ships stay on the page, struck through - this is where the tick
  // lives, so hiding them would make the choice irreversible. They sort to
  // the back and count for nothing.
  const ships = libraryStats.ships.filter((s) => {
    if (term && !s.name.toLowerCase().includes(term)) return false;
    if (cutoff && new Date(s.lastFlown).getTime() < cutoff) return false;
    return true;
  }).sort((a, b) => Number(excludedShips.has(a.name)) - Number(excludedShips.has(b.name)));

  // Ships and ground vehicles part ways on the community reference; anything
  // unmatched is assumed to fly.
  const vehicles = ships.filter((s) => s.reference && !s.reference.isSpaceship);
  $('#fleet-vehicles-title').hidden = vehicles.length === 0;

  if (!ships.length) {
    grid.append(el('p', 'muted',
      libraryStats.ships.length ? 'No ships match that filter.' : 'No ships recorded yet.'));
    return;
  }

  for (const ship of ships) {
    const grounded = ship.reference && !ship.reference.isSpaceship;
    const off = excludedShips.has(ship.name);

    const maker = makerOf(ship.name);
    const card = el('article', off ? 'ship-card excluded' : 'ship-card');

    // The Owned tick: untick a rental or a ship since sold and it leaves
    // every total on this page. Remembered per browser.
    const tick = el('label', 'own-tick');
    const box = document.createElement('input');
    box.type = 'checkbox';
    box.checked = !off;
    box.title = off ? 'Not owned - tick to count it again' : 'Owned - untick if rented or sold';

    box.addEventListener('change', () => {
      if (box.checked) excludedShips.delete(ship.name);
      else excludedShips.add(ship.name);
      try {
        localStorage.setItem('qw-assets-excluded', JSON.stringify([...excludedShips]));
      } catch { /* fine */ }
      renderFleet(libraryStats);
      renderAssets();
    });

    tick.append(box);
    card.append(tick);

    const badge = el('div', 'ship-logo');
    if (maker.code && MANUFACTURER_LOGOS.has(maker.code)) {
      const img = document.createElement('img');
      img.src = `assets/manufacturers/${maker.code}.png`;
      img.alt = maker.name;
      img.loading = 'lazy';
      badge.append(img);
    } else {
      badge.append(el('span', 'ship-logo-text', maker.code || maker.name));
    }
    card.append(badge);

    const body = el('div', 'ship-body');
    body.append(el('div', 'ship-name', maker.model));
    body.append(el('div', 'ship-maker', maker.name));

    const stat = el('div', 'ship-stats');
    stat.append(el('b', null, String(ship.sorties)));
    stat.append(el('span', null, ` flight${ship.sorties === 1 ? '' : 's'}`));

    const seconds = toSeconds(ship.estimatedTime);
    if (seconds > 0) stat.append(el('span', 'note-inline', ` · ~${duration(seconds)}`));

    body.append(stat);
    body.append(el('div', 'ship-seen', `last flown ${relative(ship.lastFlown)}`));

    // Community reference, when enabled and matched: what the ship is for and
    // what losing one costs.
    if (ship.reference) {
      const r = ship.reference;
      const bits = [];

      if (r.role && r.career && r.role !== r.career) bits.push(`${r.career} · ${r.role}`);
      else if (r.role || r.career) bits.push(r.role || r.career);
      if (r.crew > 0) bits.push(`crew ${r.crew}`);

      if (bits.length)
        body.append(el('div', 'ship-ref', bits.join(' · ')));

      if (r.expeditedCost > 0) {
        body.append(el('div', 'ship-ref muted',
          `claim: expedite ${money(r.expeditedCost)}`
          + (r.standardClaimTime ? ` · ~${Math.round(r.standardClaimTime)}m wait` : '')));
      }
    }

    // The blended-in asset side: what buying one costs in game, when the
    // price tables know it.
    const assetRow = assetsData?.fleet?.find((f) => f.name === ship.name);

    if (assetsData?.priced) {
      body.append(assetRow?.price
        ? el('div', 'ship-price', `${money(assetRow.price.price)} · ${assetRow.price.terminal}`)
        : el('div', 'ship-price muted', 'not sold in game'));
    }

    // A rentable model might be a rental rather than yours; the card says so
    // and leaves the tick to the only person who knows.
    if (assetRow?.rental) {
      const hint = el('div', 'ship-rental muted',
        `rentable ${money(assetRow.rental.price)} at ${assetRow.rental.terminal}`);
      hint.title = 'If this one was rented, untick it above';
      body.append(hint);
    }

    // Ground vehicles have no ports anyone sells parts for, so the offer is
    // made only where it can be kept.
    if (!grounded) {
      const upgrade = el('button', 'ghost ship-upgrade', 'Upgrades');
      upgrade.title = `What fits ${ship.name}, and where to buy it`;
      upgrade.addEventListener('click', () => showUpgrades(ship.name, ship.className, card));
      body.append(upgrade);
    }

    card.append(body);
    (grounded ? vehicleGrid : grid).append(card);
  }
}

/* ---------- what fits a ship, and where it is sold ---------- */

/**
 * The upgrade panel for one ship, fetched once per ship.
 *
 * The game's own data says what each port accepts, so this is not a guess: a
 * size 2 shield port takes a size 2 shield, and the shops that stock one are
 * known. What the panel is for is the trip - every option carries where it can
 * be bought, and every line can go straight onto a shopping list.
 */
const upgradeCache = new Map();

async function upgradesFor(ship) {
  if (!upgradeCache.has(ship)) {
    upgradeCache.set(ship,
      getJson(`/api/fleet/upgrades?ship=${encodeURIComponent(ship)}`).catch(() => null));
  }

  return upgradeCache.get(ship);
}

/** Ports are named for the game's files; this is what a pilot calls them. */
const PORT_WORDS = {
  QuantumDrive: 'Quantum drive',
  Shield: 'Shield',
  PowerPlant: 'Power plant',
  Cooler: 'Cooler',
  WeaponGun: 'Gun',
  Turret: 'Gun mount',
  MissileLauncher: 'Missile rack',
  Missile: 'Missile',
  Radar: 'Radar',
  EMP: 'EMP',
  QuantumInterdictionGenerator: 'Quantum interdiction',
  MiningArm: 'Mining arm',
};

/**
 * @param ship The name to show.
 * @param key The game's class name, which is what the reference data is keyed
 *   by. "Drake Corsair" answers nothing; DRAK_Corsair answers everything.
 */
async function showUpgrades(ship, key, card) {
  const open = card.querySelector('.upgrade-panel');

  if (open) {
    open.remove();
    card.classList.remove('opened');
    return;
  }

  // One at a time: the cards are a grid, and two of them spanning the row
  // pushes everything else off the screen.
  $$('.ship-card.opened').forEach((other) => {
    other.classList.remove('opened');
    other.querySelector('.upgrade-panel')?.remove();
  });

  card.classList.add('opened');

  const panel = el('div', 'upgrade-panel');
  panel.append(el('div', 'muted', 'Reading the ship…'));
  card.append(panel);

  const answer = await upgradesFor(key || ship);
  panel.textContent = '';

  if (!answer?.known) {
    panel.append(el('div', 'muted',
      'The reference data on this machine predates ship ports. Refresh the '
      + 'community dataset on the Settings page and this fills in.'));
    return;
  }

  if (!answer.groups?.length) {
    panel.append(el('div', 'muted',
      'Nothing on this one is sold in game — every port it has is fixed, or '
      + 'nobody stocks a part for it.'));
    return;
  }

  const head = el('div', 'upgrade-head');
  head.append(el('b', null, `What fits ${ship}`));
  head.append(el('span', 'muted', 'the game’s own port list · prices from UEX'));
  panel.append(head);

  for (const group of answer.groups) {
    const row = el('div', 'upgrade-group');

    const title = el('button', 'upgrade-toggle');
    title.append(el('span', 'upgrade-kind', `${PORT_WORDS[group.kind] || group.kind} S${group.size}`));
    title.append(el('span', 'muted', `${group.ports} port${group.ports === 1 ? '' : 's'}`));

    // What it flies with now is the only thing a candidate can be judged
    // against, so it sits on the closed row rather than inside.
    if (group.fitted?.length)
      title.append(el('span', 'upgrade-fitted', `now: ${group.fitted.join(' · ')}`));

    title.append(el('span', 'muted upgrade-count', `${group.options.length} sold`));

    const body = el('div', 'upgrade-options');
    body.hidden = true;

    title.addEventListener('click', () => {
      body.hidden = !body.hidden;
      title.classList.toggle('open', !body.hidden);
      if (!body.hidden && !body.dataset.filled) fillUpgradeOptions(body, group);
    });

    row.append(title);
    row.append(body);
    panel.append(row);
  }
}

function fillUpgradeOptions(body, group) {
  body.dataset.filled = '1';
  body.textContent = '';

  const table = el('table', 'upgrade-table');
  const header = el('tr');
  for (const [label, cls] of [['Part', null], ['Maker', null], ['Grade', 'num'],
    ['Price', 'num'], ['Cheapest at', null], ['', 'num']]) {
    header.append(el('th', cls, label));
  }
  table.append(header);

  for (const option of group.options) {
    const tr = el('tr');
    tr.append(el('td', null, option.name));
    tr.append(el('td', 'muted', option.manufacturer || '—'));
    tr.append(el('td', 'num muted', option.grade ? `G${option.grade}` : '—'));
    tr.append(el('td', 'num', option.price ? money(option.price) : '—'));

    // One shop on the row and the rest in the tooltip: the choice of counter
    // belongs to the trip, and the trip is planned from the list.
    const shop = option.shops[0];
    const where = el('td');
    const jump = el('button', 'place-link', shop.terminal);
    jump.disabled = !shop.placeId;
    jump.title = option.shops.map((s) => `${s.terminal} — ${money(s.price)}`).join('\n');
    jump.addEventListener('click', () => {
      showView('map');
      centreOnTerminal(shop.terminal, shop.placeId);
    });
    where.append(jump);

    if (shop.security === 'lawless')
      where.append(el('span', 'sec sec-lawless', 'lawless'));

    tr.append(where);

    const add = el('td', 'num');
    add.append(trackButton(option.name, 1, ''));
    tr.append(add);

    table.append(tr);
  }

  body.append(table);
}

/** "3 days ago", "2 months ago" - easier to scan than a date. */
function relative(iso) {
  if (!iso) return 'unknown';

  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86400000);
  if (days <= 0) return 'today';
  if (days === 1) return 'yesterday';
  if (days < 30) return `${days} days ago`;

  const months = Math.round(days / 30);
  return months <= 1 ? 'a month ago' : `${months} months ago`;
}

/** Step chart of owned-vehicle count over time. */
function drawFleetChart(history) {
  const svg = $('#fleet-chart');
  svg.textContent = '';

  if (history.length < 2) {
    const text = svgEl('text', { x: 500, y: 110, 'text-anchor': 'middle', class: 'map-label' });
    text.textContent = 'NOT ENOUGH DATA YET';
    svg.append(text);
    return;
  }

  const max = Math.max(...history.map((p) => p.vehicles));
  const min = Math.min(...history.map((p) => p.vehicles));
  const span = Math.max(1, max - min);

  const x = (i) => 30 + (i / (history.length - 1)) * 940;
  const y = (v) => 190 - ((v - min) / span) * 150;

  // Baseline and top gridlines.
  for (const value of [min, max]) {
    svg.append(svgEl('line', {
      x1: 30, y1: y(value), x2: 970, y2: y(value),
      stroke: 'rgba(53,200,240,.12)', 'stroke-width': '1',
    }));
    const label = svgEl('text', { x: 8, y: y(value) + 4, class: 'map-label' });
    label.textContent = String(value);
    svg.append(label);
  }

  // Fleet size only ever steps up, so a step line is the honest shape.
  let path = `M ${x(0)} ${y(history[0].vehicles)}`;
  history.forEach((point, i) => {
    if (i === 0) return;
    path += ` L ${x(i)} ${y(history[i - 1].vehicles)} L ${x(i)} ${y(point.vehicles)}`;
  });

  svg.append(svgEl('path', {
    d: path, fill: 'none', stroke: '#35c8f0', 'stroke-width': '2', filter: 'url(#glow)',
  }));

  history.forEach((point, i) => {
    if (i % Math.ceil(history.length / 40) !== 0) return;
    svg.append(svgEl('circle', { cx: x(i), cy: y(point.vehicles), r: 2.5, fill: '#7fe4ff' }));
  });

  // Reuse the map's glow filter definition.
  const defs = svgEl('defs');
  const glow = svgEl('filter', { id: 'glow', x: '-60%', y: '-60%', width: '220%', height: '220%' });
  glow.append(svgEl('feGaussianBlur', { stdDeviation: '2.4', result: 'b' }));
  const merge = svgEl('feMerge');
  merge.append(svgEl('feMergeNode', { in: 'b' }));
  merge.append(svgEl('feMergeNode', { in: 'SourceGraphic' }));
  glow.append(merge);
  defs.append(glow);
  svg.prepend(defs);
}

/* ---------- spending ---------- */

function renderSpending(stats) {
  const net = Number(stats.net) || 0;

  tiles('#spend-summary', [
    ['Cargo income', money(stats.income)],
    ['Item spend', money(stats.spend)],
    ['Cargo spend', money(stats.commoditySpend)],
    [net >= 0 ? 'Net gain' : 'Net loss', money(Math.abs(net))],
    ['Purchases', stats.purchaseCount],
    ['Cargo trades', stats.tradeCount],
  ]);

  if (stats.tradeShops && stats.tradeShops.length) {
    bars('#trade-chart',
      stats.tradeShops.map((s) => ({
        label: s.name,
        value: Number(s.total),
        note: `${s.quantity} SCU`,
      })),
      money);
  } else {
    $('#trade-chart').textContent = '';
    $('#trade-chart').append(el('p', 'muted', 'No commodity sales recorded yet.'));
  }

  bars('#shops-chart',
    stats.shops.slice(0, 15).map((s) => ({ label: s.name, value: s.count })),
    (v) => `${v} buy${v === 1 ? '' : 's'}`);

  bars('#items-chart',
    stats.items.slice(0, 20).map((i) => ({
      label: i.name,
      value: Number(i.total),
      note: i.quantity > 1 ? `×${i.quantity}` : null,
    })),
    money);
}

/* ---------- loadout ---------- */

/**
 * Slot glyphs, drawn in the HUD's own line style.
 *
 * Not from the Fankit - it carries logos and wallpapers, not equipment icons -
 * and not lifted from the game files, which the Fankit Agreement does not
 * cover. Original 24x24 strokes on currentColor, so they follow the theme.
 */
const SLOT_ICONS = {
  helmet: 'M5 17v-4a7 7 0 0 1 14 0v4l-2 2H7z M8.5 13h7',
  torso: 'M7 4 4 7v7l3 6h10l3-6V7l-3-3z M9 12l3 3 3-3 M12 4v3',
  arms: 'M5 19v-7a7 7 0 0 1 7-7h7 M9 19v-4a5 5 0 0 1 5-5h5',
  legs: 'M8 4v7l-2 9 M16 4v7l2 9 M8 4h8 M12 11v9',
  undersuit: 'M12 8a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z M6 20v-2a6 6 0 0 1 12 0v2',
  backpack: 'M7 8h10v12H7z M9 8V6a3 3 0 0 1 6 0v2 M9 15h6',
  rifle: 'M2 11h12l1-3h4v3h3v2h-8l-1 3h-3l1-3H8l-1 3H4l1-3H2z',
  pistol: 'M4 9h14v4h-4l-1 5h-4l1-5H4z M16 9V7',
  magazine: 'M9 4h7l-1 8c-.3 2-1.2 4-3.2 4H9z M10 8h5',
  optic: 'M10 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M10 14a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M14 12h7',
  attachment: 'M4 10h12v4H4z M16 11h4v2h-4 M7 10V8h6v2',
  grenade: 'M9 9h6v7a3 3 0 0 1-3 3 3 3 0 0 1-3-3z M10 9V6h4v3 M14 5a2.5 2.5 0 1 1 5 0',
  medical: 'M10 4h4v6h6v4h-6v6h-4v-6H4v-4h6z',
  tool: 'M13 6a4 4 0 0 1 6-3l-3 3 2 2 3-3a4 4 0 0 1-5 5L8 18l-2-2z',
  light: 'M8 3h6v3l-2 3v11h-2V9L8 6z M16 5h3 M16 9h5 M16 13h3',
  mobiglas: 'M8 8h8v8H8z M4 10v4 M20 10v4 M10 11h4',
  shirt: 'M8 4 4 7l2 3 2-1v11h8V9l2 1 2-3-4-3a4 4 0 0 1-8 0z',
  box: 'M4 8l8-4 8 4v8l-8 4-8-4z M4 8l8 4 8-4 M12 12v8',
  diamond: 'M12 3l7 9-7 9-7-9z M12 8l3 4-3 4-3-4z',
};

/** Slot first, category as the fallback, diamond when nothing matches. */
function slotIconKey(slot) {
  const name = `${(slot.port || '')} ${(slot.label || '')}`.toLowerCase();

  const byName = [
    ['helmet', 'helmet'], ['undersuit', 'undersuit'], ['backpack', 'backpack'],
    ['core', 'torso'], ['torso', 'torso'], ['chest', 'torso'],
    ['arms', 'arms'], ['shoulder', 'arms'], ['legs', 'legs'],
    ['sidearm', 'pistol'], ['pistol', 'pistol'],
    ['magazine', 'magazine'], ['optic', 'optic'], ['scope', 'optic'],
    ['barrel', 'attachment'], ['underbarrel', 'attachment'],
    ['grenade', 'grenade'], ['medpen', 'medical'], ['oxypen', 'medical'],
    ['multitool', 'tool'], ['tool', 'tool'],
    ['flashlight', 'light'], ['light', 'light'], ['mobiglas', 'mobiglas'],
    ['wep', 'rifle'], ['weapon', 'rifle'],
  ];

  for (const [needle, icon] of byName)
    if (name.includes(needle)) return icon;

  return {
    'Weapons': 'rifle',
    'Weapon attachments': 'attachment',
    'Throwables': 'grenade',
    'Armour': 'torso',
    'Medical': 'medical',
    'Utility': 'tool',
    'Carried': 'box',
    'Appearance': 'shirt',
  }[slot.category] ?? 'diamond';
}

function slotIconSvg(slot) {
  const path = SLOT_ICONS[slotIconKey(slot)] ?? SLOT_ICONS.diamond;

  return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" '
    + 'stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round" '
    + `aria-hidden="true"><path d="${path}"/></svg>`;
}

function renderLoadout(stats) {
  libraryStats = stats;

  const host = $('#loadout-grid');
  host.textContent = '';

  if (!stats.loadout || !stats.loadout.length) {
    host.append(el('p', 'muted', 'No attachment events recorded yet.'));
    return;
  }

  const term = ($('#loadout-search').value || '').trim().toLowerCase();

  // Match on the slot name, what is currently in it, or anything it has held.
  const slots = stats.loadout.filter((slot) => {
    if (!term) return true;

    return (slot.label || slot.port).toLowerCase().includes(term)
      || slot.items.some((i) => i.name.toLowerCase().includes(term));
  });

  if (!slots.length) {
    host.append(el('p', 'muted', 'Nothing matches that search.'));
    return;
  }

  // The server already orders slots by category; preserve that grouping.
  const byCategory = new Map();
  for (const slot of slots) {
    if (!byCategory.has(slot.category)) byCategory.set(slot.category, []);
    byCategory.get(slot.category).push(slot);
  }

  for (const [category, slots] of byCategory) {
    host.append(sectionHeading(category, `${slots.length} slots`));

    const grid = el('div', 'card-grid');

    for (const slot of slots) {
      const card = el('article', 'card slot-card');

      const icon = el('div', 'slot-icon');
      icon.innerHTML = slotIconSvg(slot);
      card.append(icon);

      const body = el('div', 'slot-body');

      const label = slot.slotCount > 1
        ? `${slot.label} · ${slot.slotCount} slots`
        : slot.label || slot.port;

      body.append(el('div', 'card-label', label));

      // What is in the family now; the churn goes behind a toggle.
      for (const item of slot.items) {
        const line = el('div', 'slot-current');
        line.append(el('span', null, prettyItem(item.name)));
        if (item.count > 1) line.append(el('span', 'slot-multi', ` ×${item.count}`));

        // Community reference: size, grade, maker - when enabled and matched.
        if (item.reference) {
          const r = item.reference;
          const bits = [];
          if (r.size > 0) bits.push(`S${r.size}`);
          if (r.grade > 0) bits.push(`grade ${r.grade}`);
          if (r.manufacturer) bits.push(r.manufacturer);
          if (bits.length) line.append(el('span', 'slot-ref', ` · ${bits.join(' · ')}`));
        }

        body.append(line);
      }

      if (slot.currentSeen)
        body.append(el('div', 'slot-when', `equipped ${relative(slot.currentSeen)}`));

      card.append(body);
      grid.append(card);
    }

    host.append(grid);
  }
}

/**
 * Builds a list that shows `limit` items and reveals the rest on click.
 * Truncation is skipped while a search is active, since the user has already
 * narrowed the set and hiding matches would be perverse.
 */
function expandableList(card, items, limit, renderItem, expanded) {
  const list = el('ul', 'slot-list');

  items.forEach((item, index) => {
    const li = renderItem(item);
    if (!expanded && index >= limit) li.classList.add('extra');
    list.append(li);
  });

  card.append(list);

  if (expanded || items.length <= limit)
    return;

  const hidden = items.length - limit;
  const toggle = el('button', 'more-toggle', `show ${hidden} more`);

  toggle.addEventListener('click', () => {
    const showing = list.classList.toggle('show-all');
    toggle.textContent = showing ? 'show less' : `show ${hidden} more`;
  });

  card.append(toggle);
}

/** A category heading with a count on the right. */
function sectionHeading(title, meta) {
  const head = el('div', 'group-head');
  head.append(el('h3', null, title));
  if (meta) head.append(el('span', 'group-meta', meta));
  return head;
}

const prettyItem = (name) => name.replace(/_/g, ' ');

/* ---------- stash ---------- */

/**
 * "Everything ever seen" unions every listing instead of trusting the newest.
 * It matters because a listing is only a page: glancing at one tab of an
 * inventory replaces a full browse, and the place looks emptied. Fetched
 * separately, since the server decides which view to build.
 */
let stashEverSeen = null;

async function renderStash(stats) {
  libraryStats = stats;

  const grid = $('#stash-grid');
  grid.textContent = '';

  if ($('#stash-ever')?.checked) {
    if (!stashEverSeen) {
      try {
        stashEverSeen = await getJson('/api/stash?everSeen=true');
      } catch {
        stashEverSeen = [];
      }
    }

    stats = { ...stats, stash: stashEverSeen };
  }

  if (!stats.stash || !stats.stash.length) {
    grid.append(el('p', 'muted',
      'Nothing recorded yet. Open a local inventory in game and it will appear here.'));
    return;
  }

  const term = ($('#stash-search').value || '').trim().toLowerCase();
  const days = Number($('#stash-period').value) || 0;
  const cutoff = days ? Date.now() - days * 86400000 : null;
  const latestOnly = $('#stash-latest').checked;

  // The same item often sits in several stashes. When asked, keep only the
  // place it was most recently seen, so "where is my MedPen" has one answer.
  const newestPlace = new Map();

  if (latestOnly) {
    for (const place of stats.stash) {
      for (const group of place.groups) {
        for (const item of group.items) {
          const seen = new Date(item.lastSeen).getTime();
          const best = newestPlace.get(item.name);
          if (!best || seen > best.seen) newestPlace.set(item.name, { seen, place: place.locationId });
        }
      }
    }
  }

  // Searching by place keeps the whole location; searching by item narrows to
  // the matching items, so "where is my sniper" answers in one glance. The
  // period filter always applies per item, since a location's most recent visit
  // says nothing about when a particular item was last seen in it.
  const places = stats.stash
    .map((place) => {
      const placeHit = term && place.name.toLowerCase().includes(term);

      const groups = place.groups
        .map((g) => ({
          ...g,
          items: g.items.filter((i) => {
            if (cutoff && new Date(i.lastSeen).getTime() < cutoff) return false;
            if (term && !placeHit && !i.name.toLowerCase().includes(term)) return false;
            if (latestOnly && newestPlace.get(i.name)?.place !== place.locationId) return false;
            return true;
          }),
        }))
        .filter((g) => g.items.length);

      if (!groups.length) return null;

      return { ...place, groups, itemCount: groups.reduce((n, g) => n + g.items.length, 0) };
    })
    .filter(Boolean);

  if (!places.length) {
    grid.append(el('p', 'muted', 'Nothing matches that search.'));
    return;
  }

  for (const place of places) {
    const card = el('article', 'card');
    { const label = el('div', 'card-label'); label.append(placeLink(place.name)); card.append(label); }
    card.append(el('div', 'sub', `${place.itemCount} item types · last seen ${dateOf(place.lastSeen)}`));

    for (const group of place.groups) {
      const head = el('div', 'stash-group');
      head.append(el('span', 'stash-group-name', group.category));
      head.append(el('span', 'slot-count', String(group.items.length)));
      card.append(head);

      expandableList(card, group.items, 8, (item) => {
        const li = el('li');
        li.append(el('span', 'slot-item', prettyItem(item.name)));
        li.append(el('span', 'slot-count', relative(item.lastSeen)));
        return li;
      }, Boolean(term));
    }

    grid.append(card);
  }
}

/* ---------- map ---------- */

const SVG_NS = 'http://www.w3.org/2000/svg';
const svgEl = (tag, attrs = {}) => {
  const node = document.createElementNS(SVG_NS, tag);
  for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value);
  return node;
};

/**
 * Renders visited places as a system topology map.
 *
 * This is deliberately not a positional radar: Star Citizen logs no player
 * coordinates, so bodies are laid out on fixed rings and each location is
 * clustered around the body it belongs to. Node size encodes visit count.
 */
/* ---------- map ---------- */

/**
 * Every place in the game, visited or not, with the layout worked out once.
 * Positions are cached by raw id so the live marker and "centre on me" can find
 * a node without re-running the layout.
 */
let atlas = [];
const nodeAt = new Map();

// A system view is the honest default: its bodies preserve the bearings and
// relative distances supplied by the community starmap. The network is useful
// for planning jump legs, but says out loud that its triangle has no scale.
const MAP_MODE_KEY = 'qw-map-mode';
const MAP_SYSTEM_KEY = 'qw-map-system';

function mapMode() { return $('#map-mode')?.value || 'system'; }
function mapSystem() { return $('#map-system')?.value || ''; }

function preferredMapSystem() {
  const here = hereId && atlas.find((location) => location.rawId === hereId)?.system;
  return here && SYSTEM_COLOURS[here] ? here : 'Stanton';
}

function currentMapLocation() {
  return hereId ? atlas.find((location) => location.rawId === hereId) || null : null;
}

// This remains visible even when the selected system is not the player's. A
// one-system map is less misleading than a whole-system schematic, but should
// never turn "where am I?" into a hidden state.
function updateHereControl() {
  const control = $('#map-here');
  const label = $('#map-here-label');
  if (!control || !label) return;

  const here = currentMapLocation();
  control.disabled = !here;
  control.classList.toggle('located', !!here);
  label.textContent = here ? `You · ${here.name}` : 'Location unknown';

  if (!here) control.title = 'The live log has not named your location yet';
  else if (mapMode() === 'network') control.title = `Show ${here.name} in ${here.system}`;
  else if (mapSystem() !== here.system) control.title = `Show ${here.name} in ${here.system}`;
  else control.title = `Centre on ${here.name}`;
}

/** Makes the player's system and place visible without changing filters. */
function focusHere() {
  const here = currentMapLocation();
  if (!here) return false;

  if (here.system && SYSTEM_COLOURS[here.system]
    && (mapMode() !== 'system' || mapSystem() !== here.system)) {
    $('#map-mode').value = 'system';
    $('#map-system').value = here.system;
    syncMapModeControls();
    try {
      localStorage.setItem(MAP_MODE_KEY, 'system');
      localStorage.setItem(MAP_SYSTEM_KEY, here.system);
    } catch { /* private mode */ }
    drawMap();
  }

  return centreOn(here.rawId);
}

function syncMapModeControls() {
  const mode = $('#map-mode');
  const system = $('#map-system');
  if (!mode || !system) return;

  const systems = [...new Set(atlas.map((location) => location.system)
    .filter((name) => SYSTEM_COLOURS[name]))].sort();

  if (!system.dataset.filled || [...system.options].map((option) => option.value).join('|') !== systems.join('|')) {
    const selected = system.value || preferredMapSystem();
    system.textContent = '';
    for (const name of systems) system.append(new Option(name, name));
    system.value = systems.includes(selected) ? selected : (systems[0] || '');
    system.dataset.filled = '1';
  }

  system.hidden = mode.value !== 'system';
  const note = $('#map-mode-note');
  if (note) note.textContent = mode.value === 'system'
    ? `${system.value || 'This system'}: real body bearings and relative orbit distances. Bodies without a community coordinate are amber and explicitly unpositioned.`
    : 'Jump network: systems and jump connections only — schematic, not to scale. Select a system to inspect its bodies and locations.';
  updateHereControl();
}

// Service data is intentionally a set of place ids, not a claim about every
// facility at a location. UEX can identify counters, fuel prices and clinics;
// it cannot identify repair pads, so repair never becomes a reassuringly empty
// map filter.
const SERVICE_META = {
  shop: { icon: '▦', label: 'Shops' },
  refuel: { icon: '⛽', label: 'Refuel' },
  clinic: { icon: '✚', label: 'Clinic' },
  repair: { icon: '⚙', label: 'Repair' },
};
const mapServicesByPlace = new Map();
let mapServiceFilter = '';
let mapFocusFilter = '';
const mapShoppingIds = new Set();
const mapStashIds = new Set();
let mapNotes = [];
const mapNoteIds = new Set();
const MAP_SAVED_VIEW_KEY = 'qw-map-saved-view';
const MAP_LABEL_DENSITY_KEY = 'qw-map-label-density';

const serviceKey = (name) => ({
  Shops: 'shop',
  'Trade counter': 'shop',
  Refuel: 'refuel',
  Clinic: 'clinic',
  Repair: 'repair',
}[name] || '');

const servicesAt = (location) => mapServicesByPlace.get(location.rawId) || [];

// Service is a property of a place, not its identity. Badges sit outside the
// location glyph so a clinic at a station still reads as a station first.
function drawServiceBadges(group, x, y, radius, services) {
  const badges = svgEl('g', { class: 'map-service-badges' });
  const badgeRadius = Math.max(3.2, radius * .42);
  const orbit = radius + badgeRadius + 3;

  services.forEach((service, index) => {
    const angle = -Math.PI / 2 + (index - (services.length - 1) / 2) * .76;
    const bx = x + Math.cos(angle) * orbit;
    const by = y + Math.sin(angle) * orbit;
    const badge = svgEl('g', { class: `map-service-badge ${service}` });
    badge.append(svgEl('circle', { cx: bx, cy: by, r: badgeRadius }));

    if (service === 'shop') {
      badge.append(svgEl('rect', {
        x: bx - badgeRadius * .52, y: by - badgeRadius * .52,
        width: badgeRadius * 1.04, height: badgeRadius * 1.04, class: 'service-glyph',
      }));
      badge.append(svgEl('line', { x1: bx, y1: by - badgeRadius * .52, x2: bx, y2: by + badgeRadius * .52, class: 'service-glyph' }));
    } else if (service === 'refuel') {
      badge.append(svgEl('path', {
        d: `M ${bx} ${by - badgeRadius * .68} C ${bx + badgeRadius * .56} ${by - badgeRadius * .14}, ${bx + badgeRadius * .42} ${by + badgeRadius * .56}, ${bx} ${by + badgeRadius * .62} C ${bx - badgeRadius * .42} ${by + badgeRadius * .56}, ${bx - badgeRadius * .56} ${by - badgeRadius * .14}, ${bx} ${by - badgeRadius * .68} Z`,
        class: 'service-glyph',
      }));
    } else if (service === 'clinic') {
      badge.append(svgEl('path', {
        d: `M ${bx - badgeRadius * .22} ${by - badgeRadius * .64} H ${bx + badgeRadius * .22} V ${by - badgeRadius * .22} H ${bx + badgeRadius * .64} V ${by + badgeRadius * .22} H ${bx + badgeRadius * .22} V ${by + badgeRadius * .64} H ${bx - badgeRadius * .22} V ${by + badgeRadius * .22} H ${bx - badgeRadius * .64} V ${by - badgeRadius * .22} H ${bx - badgeRadius * .22} Z`,
        class: 'service-glyph',
      }));
    } else {
      badge.append(svgEl('path', {
        d: `M ${bx - badgeRadius * .58} ${by} H ${bx + badgeRadius * .58} M ${bx} ${by - badgeRadius * .58} V ${by + badgeRadius * .58}`,
        class: 'service-glyph',
      }));
    }

    const title = svgEl('title');
    title.textContent = SERVICE_META[service]?.label || service;
    badge.append(title);
    badges.append(badge);
  });

  group.append(badges);
}

// A small bookmark sits outside the location glyph: a personal note changes
// neither the place kind nor the UEX service facts already drawn around it.
function drawMapNoteBadge(group, x, y, radius) {
  const badge = svgEl('g', { class: 'map-note-badge', 'pointer-events': 'none' });
  const size = Math.max(3.2, radius * .48);
  const bx = x + radius + size + 2;
  const by = y + radius + size + 1;
  badge.append(svgEl('path', {
    d: `M ${bx} ${by - size} L ${bx + size} ${by} L ${bx} ${by + size} L ${bx - size} ${by} Z`,
  }));
  const title = svgEl('title');
  title.textContent = 'Personal map note';
  badge.append(title);
  group.append(badge);
}

function showServiceBadges(location, highlighted) {
  return servicesAt(location).length > 0
    && (isDetailed() || highlighted || !!mapServiceFilter || !!mapFocusFilter);
}

function selectMapService(service, openMap = false, redraw = true) {
  mapServiceFilter = service || '';
  for (const button of $$('#map-service-filter button'))
    button.classList.toggle('active', button.dataset.service === mapServiceFilter);

  if (openMap) showView('map');
  if (redraw) drawMap();
}

function planPlaceIds() {
  return new Set((tracked()?.stops || []).map((stop) => stop.placeId).filter(Boolean));
}

function mapFocusIds() {
  if (mapFocusFilter === 'plan') return planPlaceIds();
  if (mapFocusFilter === 'shopping') return mapShoppingIds;
  if (mapFocusFilter === 'stash') return mapStashIds;
  if (mapFocusFilter === 'notes') return mapNoteIds;
  return null;
}

function selectMapFocus(focus, redraw = true) {
  mapFocusFilter = focus || '';
  for (const button of $$('#map-focus-filter button'))
    button.classList.toggle('active', button.dataset.focus === mapFocusFilter);
  if (redraw) drawMap();
}

function mapLabelDensity() { return $('#map-label-density')?.value || 'auto'; }

function saveMapView() {
  const saved = {
    mode: mapMode(), system: mapSystem(), service: mapServiceFilter, focus: mapFocusFilter,
    visited: $('#map-visited-only').checked, goods: $('#map-goods').checked,
    labels: mapLabelDensity(), search: $('#map-search').value,
  };
  try { localStorage.setItem(MAP_SAVED_VIEW_KEY, JSON.stringify(saved)); } catch { /* private mode */ }

  const button = $('#map-save-preset');
  button.textContent = 'Saved';
  button.title = 'Saved view updated';
}

function applyMapPreset(name) {
  let preset = null;
  if (name === 'saved') {
    try { preset = JSON.parse(localStorage.getItem(MAP_SAVED_VIEW_KEY) || 'null'); } catch { /* private mode */ }
    if (!preset) return;
  } else if (name === 'plan') {
    preset = { focus: 'plan' };
  } else if (name === 'shopping') {
    preset = { focus: 'shopping', goods: true };
  } else if (name === 'services') {
    preset = { service: 'refuel', goods: false };
  } else if (name === 'visited') {
    preset = { visited: true, focus: '' };
  } else return;

  if (preset.mode) $('#map-mode').value = preset.mode;
  if (preset.system) $('#map-system').value = preset.system;
  if (typeof preset.visited === 'boolean') $('#map-visited-only').checked = preset.visited;
  if (typeof preset.goods === 'boolean') $('#map-goods').checked = preset.goods;
  if (preset.labels) $('#map-label-density').value = preset.labels;
  if (typeof preset.search === 'string') $('#map-search').value = preset.search;
  selectMapService(preset.service || '', false, false);
  selectMapFocus(preset.focus || '', false);
  syncMapModeControls();
  drawMap();
}

function atlasPlaceId(name) {
  if (!name) return null;
  const clean = String(name).toLowerCase().replace(/[^a-z0-9]/g, '');
  if (clean.length < 4) return null;
  return atlas.find((place) => {
    const candidate = `${place.name} ${place.rawId}`.toLowerCase().replace(/[^a-z0-9]/g, '');
    return candidate === clean || candidate.includes(clean) || clean.includes(candidate);
  })?.rawId || null;
}

// Jobs give us destinations and sellers; stash remembers presence by place.
// Both are useful focus layers, but neither is a live inventory or stock claim.
async function refreshMapFocusContext(redraw = false) {
  const [jobs, stats] = await Promise.all([
    getJson('/api/jobs').catch(() => []),
    getJson('/api/stats').catch(() => null),
  ]);

  mapShoppingIds.clear();
  for (const job of jobs.filter((job) => !job.done)) {
    if (job.destinationId) mapShoppingIds.add(job.destinationId);
    else if (job.destination) {
      const destination = atlasPlaceId(job.destination);
      if (destination) mapShoppingIds.add(destination);
    }

    for (const item of job.items || []) {
      if (item.have) continue;
      const seller = atlasPlaceId(item.buyAt);
      if (seller) mapShoppingIds.add(seller);
    }
  }

  mapStashIds.clear();
  for (const place of stats?.stash || []) {
    const id = atlasPlaceId(place.name);
    if (id) mapStashIds.add(id);
  }

  if (redraw && atlas.length) drawMap();
}

/** Non-null while a commodity search is active: the rawIds to light up. */
let highlightIds = null;

/**
 * Whether the map keeps itself centred on the live marker. Persisted: someone
 * who plays with the map on a second monitor wants this every session.
 */
let followHere = false;
try { followHere = localStorage.getItem('qw-map-follow') === '1'; } catch { /* private mode */ }

/** Where the player is, kept in step with the live feed. */
let hereId = null;

const SYSTEM_COLOURS = { Stanton: '#ffdc9a', Pyro: '#ff8f66', Nyx: '#9fb8ff' };

/** Jump lanes, drawn between the stars they connect. */
const JUMP_LANES = [
  ['Stanton', 'Pyro'],
  ['Pyro', 'Nyx'],
  ['Stanton', 'Nyx'],
];

/**
 * How far a body's sites reach, given how many it has.
 *
 * The phyllotaxis spacing that fits at system scale is far too tight once the
 * dots are big enough to read: a well-visited site is 17 units across on its
 * own while neighbours sit 5.2 apart, so microTech's twenty-two piled into
 * each other however far you zoomed in. Zoomed in the whole cluster opens up,
 * which is exactly when there is room for it - the same trade the bodies
 * themselves make.
 */
const clusterRadius = (count) =>
  (count <= 1 ? 14 : 13 + 5.2 * Math.sqrt(count - 1)) * (isDetailed() ? 2.1 : 1);

/**
 * Works out where each star sits and how far its bodies orbit, from what the
 * system actually contains.
 *
 * A fixed orbit cannot work once every location is on the map rather than only
 * the visited handful: microTech alone carries over a hundred sites, and its
 * cluster spilled straight over Calliope's next door. Each system is therefore
 * sized so neighbouring clusters clear each other with room for their labels,
 * which makes Stanton's disc several times Nyx's.
 *
 * Systems are then laid out as a triangle rather than a row - that is the real
 * topology, since Stanton, Pyro and Nyx each have a jump point to the other
 * two, and three discs in a line make a map that is all width and no height.
 */
function layoutSystems(grouped) {
  const layout = {};

  for (const [system, bodies] of Object.entries(grouped)) {
    if (system === 'other' || !bodies.size) continue;

    const reach = Math.max(...[...bodies.values()].map((sites) => clusterRadius(sites.length)));
    const spacing = reach * 2 + 34;
    const orbit = Math.max(190, (spacing * bodies.size) / (2 * Math.PI));

    layout[system] = {
      orbit,
      reach,
      radius: orbit + reach + 34,
      colour: SYSTEM_COLOURS[system] || '#9fb8ff',
    };
  }

  // Stanton and Pyro share the top row; anything else goes underneath.
  const pad = 70;
  const gap = 90;
  const top = ['Stanton', 'Pyro'].filter((s) => layout[s]);
  const rest = Object.keys(layout).filter((s) => !top.includes(s));

  const rowHeight = top.length ? Math.max(...top.map((s) => layout[s].radius)) : 0;
  let x = pad;

  for (const system of top) {
    layout[system].x = x + layout[system].radius;
    layout[system].y = pad + rowHeight;
    x = layout[system].x + layout[system].radius + gap;
  }

  const rowWidth = Math.max(pad, x - gap);
  let y = top.length ? pad + rowHeight * 2 + gap : pad;

  for (const system of rest) {
    layout[system].x = Math.max(rowWidth / 2, pad + layout[system].radius);
    layout[system].y = y + layout[system].radius;
    y = layout[system].y + layout[system].radius + gap;
  }

  const width = Math.max(rowWidth, ...Object.values(layout).map((s) => s.x + s.radius)) + pad;
  const height = Math.max(y - gap, pad + rowHeight * 2) + pad;

  return { layout, width, height };
}

let SYSTEM_LAYOUT = {};
let HOME_VIEW = { x: 0, y: 0, w: 1340, h: 1240 };
let view = { ...HOME_VIEW };

/**
 * Label size in map units, chosen so text keeps a constant size on screen
 * however far the view is zoomed. Without this, zooming in magnifies the labels
 * along with everything else and they pile into each other.
 */
const labelSize = (scale = 1) => (view.w / HOME_VIEW.w) * 9.5 * scale;

async function loadAtlas() {
  const [data, servicePlaces] = await Promise.all([
    getJson('/api/map'),
    getJson('/api/map/services').catch(() => []),
  ]);
  atlas = data.nodes || [];
  bodyPositions = data.positions || {};
  mapServicesByPlace.clear();
  for (const place of servicePlaces)
    mapServicesByPlace.set(place.placeId, place.services || []);
  await refreshMapFocusContext();
  syncMapModeControls();
  drawMap();

  // A detail card can stay open while the history refreshes. Its facts should
  // catch up when the supporting service map does.
  if (mapInfoLocation) renderMapInfoServices(mapInfoLocation);
}

/** Real body coordinates per system, when the community dataset supplies them. */
let bodyPositions = {};

/**
 * Where each of a system's bodies sits on its disc.
 *
 * With real coordinates the answer is geometry: the true bearing from the
 * star, distance compressed by a square root so the outer planets do not push
 * the inner ones into the star. But raw geometry has a catch at system scale —
 * a moon sits so close to its planet that both land on the same pixel and
 * their site clusters pile up. So bodies are first grouped by proximity, the
 * group anchored at its true position, and the moons fanned on a short local
 * ring around their planet, which is how the game's own starmap solves it too.
 * Systems without coordinates keep the even ring the map has always drawn.
 *
 * Returns a Map of body name → {x, y, angle}; sizeOf gives each body's
 * cluster radius so a moon lands clear of its planet's spread of sites.
 */
function bodyLayout(system, present, centre, sizeOf) {
  const placements = new Map();
  const real = bodyPositions[system.toLowerCase()] || {};
  const lookup = (name) => {
    const key = Object.keys(real).find((k) => k.toLowerCase() === name.toLowerCase());
    return key && (real[key].x || real[key].y) ? real[key] : null;
  };

  // Zoomed in, the bodies move apart: every site carries a label at that
  // range and the names need the room. applyView anchors the view across the
  // redraw so the spread happens around what the eye is on.
  const spread = isDetailed() ? 1.6 : 1;
  const orbit = centre.orbit * spread;
  const physical = mapMode() === 'system';

  if (!present.some((name) => lookup(name))) {
    present.forEach((bodyName, index) => {
      const angle = (index / Math.max(1, present.length)) * Math.PI * 2 - Math.PI / 2;
      placements.set(bodyName, {
        x: centre.x + Math.cos(angle) * orbit,
        y: centre.y + Math.sin(angle) * orbit,
        angle, positioned: false,
        from: { x: centre.x, y: centre.y },
      });
    });
    return placements;
  }

  const maxR = Math.max(1, ...Object.values(real).map((b) => Math.hypot(b.x, b.y)));

  // Bodies within 6% of the system's radius of each other are one group: a
  // planet and its moons. First member in orbit order is the planet.
  const groups = [];
  let orphanIndex = 0;

  for (const bodyName of present) {
    const pos = lookup(bodyName);

    if (pos) {
      const near = groups.find((g) => g.pos
        && Math.hypot(g.pos.x - pos.x, g.pos.y - pos.y) < maxR * 0.06);
      if (near) { near.members.push(bodyName); continue; }
    }

    groups.push({ members: [bodyName], pos });
  }

  for (const group of groups) {
    let angle;
    let gx;
    let gy;

    if (group.pos) {
      angle = Math.atan2(group.pos.y, group.pos.x);
      const fraction = Math.hypot(group.pos.x, group.pos.y) / maxR;
      // A local system map can retain the actual radial relationship. The
      // older all-systems view compresses it because it has to fit several
      // dense systems in one frame without pretending their frames share scale.
      const radius = orbit * (physical ? fraction : 0.3 + 0.7 * Math.sqrt(fraction));
      gx = centre.x + Math.cos(angle) * radius;
      gy = centre.y + Math.sin(angle) * radius;
    } else {
      // No coordinates for this body — park it on the outer ring, stepping
      // around so uncharted bodies do not stack on one another.
      angle = Math.PI / 3 + (orphanIndex++) * (Math.PI / 2.5);
      gx = centre.x + Math.cos(angle) * orbit * 1.1;
      gy = centre.y + Math.sin(angle) * orbit * 1.1;
    }

    group.members.forEach((bodyName, index) => {
      if (index === 0) {
        placements.set(bodyName, {
          x: gx, y: gy, angle, positioned: Boolean(group.pos), from: { x: centre.x, y: centre.y },
        });
        return;
      }

      // Moons fan across the outward-facing half so they stay clear of the
      // star and of the planet's own label, spaced past both clusters.
      const arc = angle + (index - (group.members.length) / 2) * (Math.PI / 3.2);
      const gap = (sizeOf(group.members[0]) + sizeOf(bodyName) + 34) * (spread > 1 ? 1.25 : 1);

      placements.set(bodyName, {
        x: gx + Math.cos(arc) * gap,
        y: gy + Math.sin(arc) * gap,
        angle: arc, positioned: Boolean(group.pos),
        from: { x: gx, y: gy },
      });
    });
  }

  return placements;
}

/**
 * True once the view is close enough for every node to carry a label, rather
 * than only the ones with visit history. Held as a fraction of the whole map so
 * it still means the same thing when the layout resizes.
 */
const isDetailed = () => view.w < HOME_VIEW.w * 0.34;

/** Applies the current pan/zoom to the SVG, redrawing when detail changes. */
function applyView() {
  const map = $('#starmap');
  const wasDetailed = map.dataset.detailed === 'true';
  const detailed = isDetailed();

  map.setAttribute('viewBox', `${view.x} ${view.y} ${view.w} ${view.h}`);
  map.dataset.detailed = String(detailed);

  // Labels appear on zoom-in, so crossing the threshold means redrawing. The
  // redraw calls back into applyView, but the flag now matches, so it stops.
  // The detailed layout also spreads bodies apart to give the names room, so
  // the node nearest mid-view is re-found afterwards and the view shifted to
  // keep it exactly where the eye left it.
  if (detailed !== wasDetailed && atlas.length) {
    // Anchor on where the eye is headed: the glide's destination when one is
    // in flight, the current centre otherwise.
    const goal = viewAnimTarget ?? view;
    const cx = goal.x + goal.w / 2;
    const cy = goal.y + goal.h / 2;
    let anchor = null;
    let nearest = Infinity;

    for (const [id, p] of nodeAt) {
      const d = (p.x - cx) ** 2 + (p.y - cy) ** 2;
      if (d < nearest) {
        nearest = d;
        anchor = { id, x: p.x, y: p.y };
      }
    }

    drawMap();

    const moved = anchor && nodeAt.get(anchor.id);
    if (moved && (moved.x !== anchor.x || moved.y !== anchor.y)) {
      const dx = moved.x - anchor.x;
      const dy = moved.y - anchor.y;

      view.x += dx;
      view.y += dy;

      // A glide in progress must move with the world, or it lands where its
      // destination used to be.
      if (viewAnimFrom) {
        viewAnimFrom.x += dx;
        viewAnimFrom.y += dy;
        viewAnimTarget.x += dx;
        viewAnimTarget.y += dy;
      }

      map.setAttribute('viewBox', `${view.x} ${view.y} ${view.w} ${view.h}`);
    }
  }
}

/**
 * Eases the viewport towards a target rectangle. A cut to a new view loses the
 * reader - the glide is what tells them where on the map they went. Cancelled
 * the moment the user pans or zooms by hand, so the map never fights them.
 */
let viewAnimation = null;

// Endpoints live outside the closure so a mid-flight layout change (the
// detailed zoom spreads bodies apart) can shift them with the world instead
// of letting the glide land on stale coordinates.
let viewAnimFrom = null;
let viewAnimTarget = null;

function animateViewTo(target, ms = 420) {
  cancelAnimationFrame(viewAnimation);

  viewAnimFrom = { ...view };
  viewAnimTarget = { ...target };
  const start = performance.now();
  const ease = (t) => 1 - Math.pow(1 - t, 3);

  const step = (now) => {
    const k = ease(Math.min(1, (now - start) / ms));
    const from = viewAnimFrom;
    const to = viewAnimTarget;

    view = {
      x: from.x + (to.x - from.x) * k,
      y: from.y + (to.y - from.y) * k,
      w: from.w + (to.w - from.w) * k,
      h: from.h + (to.h - from.h) * k,
    };
    applyView();

    if (k < 1) viewAnimation = requestAnimationFrame(step);
    else viewAnimFrom = viewAnimTarget = null;
  };

  viewAnimation = requestAnimationFrame(step);
}

/**
 * Moves the view so a place sits in the middle, zooming in if the map is still
 * showing the whole system.
 */
function centreOn(rawId, zoom = true) {
  const point = nodeAt.get(rawId);
  if (!point) return false;

  const close = HOME_VIEW.w * 0.28;
  const w = zoom && view.w > close ? close : view.w;
  const h = zoom && view.w > close ? close * (HOME_VIEW.h / HOME_VIEW.w) : view.h;

  animateViewTo({ x: point.x - w / 2, y: point.y - h / 2, w, h });
  return true;
}

/**
 * Frames the current search matches: the answer to "where is it?" should not
 * require finding the lit dots on a full-width map by eye. Runs once per new
 * term so it never fights a pan the user makes while the term stands, and
 * glides home when the search is cleared.
 */
let lastFitTerm = null;

function fitToHighlights(term) {
  if (term === lastFitTerm) return;
  const hadTerm = Boolean(lastFitTerm);
  lastFitTerm = term;

  if (!term || !highlightIds || highlightIds.size === 0) {
    if (hadTerm && !term) animateViewTo(HOME_VIEW);
    return;
  }

  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;

  for (const id of highlightIds) {
    const p = nodeAt.get(id);
    if (!p) continue;
    minX = Math.min(minX, p.x);
    minY = Math.min(minY, p.y);
    maxX = Math.max(maxX, p.x);
    maxY = Math.max(maxY, p.y);
  }

  if (minX === Infinity) return;

  const padX = Math.max((maxX - minX) * 0.22, HOME_VIEW.w * 0.07);
  const padY = Math.max((maxY - minY) * 0.22, HOME_VIEW.h * 0.07);

  let w = maxX - minX + padX * 2;
  let h = maxY - minY + padY * 2;

  // Preserve the map's aspect so the frame is a zoom, not a stretch.
  const aspect = HOME_VIEW.h / HOME_VIEW.w;
  if (h < w * aspect) h = w * aspect;
  else w = h / aspect;

  // Clamped both ways: never wider than home, and never closer than the
  // "centre on" zoom - a single match filling the frame reads as an error.
  w = Math.max(Math.min(w, HOME_VIEW.w), HOME_VIEW.w * 0.26);
  h = Math.max(Math.min(h, HOME_VIEW.h), HOME_VIEW.h * 0.26);

  animateViewTo({
    x: (minX + maxX) / 2 - w / 2,
    y: (minY + maxY) / 2 - h / 2,
    w,
    h,
  });
}

/**
 * Marks the player's current place, and is safe to call before the map has been
 * drawn - the marker is re-applied on every draw from {@link hereId}.
 */
function setHere(rawId) {
  const changed = (rawId || null) !== hereId;
  hereId = rawId || null;

  /*
   * Only when it actually moves.
   *
   * The live feed repeats the same place every second, and this used to redraw
   * on every one of them. The marker's pulse is an SVG animation of a growing
   * ring over 2.2 seconds, and rebuilding the element restarts it - so it got
   * a fifth of the way out, was replaced, and started again. The marker was
   * there the whole time and never appeared to pulse, which is worse than not
   * being drawn: it looks like a dead dot rather than a missing feature.
   */
  if (changed || !$('#starmap').querySelector('.map-here'))
    drawHere();

  updateHereControl();

  // Follow mode: the map pans itself as the player moves, so a second monitor
  // shows the journey without being touched.
  if (followHere && changed && hereId)
    centreOn(hereId);
}

/** Where the live feed says the player is quantum-travelling to, if anywhere. */
let travelId = null;

/**
 * Marks a jump in progress, and is safe to call before the map is drawn.
 *
 * The same rule as the here-marker: only rebuild when the destination changes,
 * or the marching dashes restart every time the feed repeats itself and the
 * line looks painted on.
 */
function setTravel(rawId) {
  const changed = (rawId || null) !== travelId;
  travelId = rawId || null;

  if (changed || (travelId && !$('#starmap').querySelector('.map-travel')))
    drawTravel();
}

/**
 * The line from where you are to where you are headed.
 *
 * The map has always known both ends - the live feed names the destination the
 * moment the drive spools - and drew neither as a journey. A jump is the one
 * time the map can show something happening rather than something recorded, so
 * it gets a marching dashed vector and a ring waiting at the far end.
 */
function drawTravel() {
  const map = $('#starmap');
  map.querySelectorAll('.map-travel').forEach((n) => n.remove());

  const from = hereId && nodeAt.get(hereId);
  const to = travelId && nodeAt.get(travelId);

  // Either end can be a place the map cannot draw - an unmapped outpost, or a
  // destination in a system this atlas has no layout for. A half-drawn vector
  // pointing at nothing would be worse than none.
  if (!from || !to) return;

  const zoom = view.w / HOME_VIEW.w;
  const group = svgEl('g', { class: 'map-travel' });

  const line = svgEl('line', {
    x1: from.x, y1: from.y, x2: to.x, y2: to.y,
    class: 'travel-line',
    'stroke-width': 1.5 * zoom,
    'stroke-dasharray': `${7 * zoom} ${5 * zoom}`,
    filter: 'url(#glow)',
  });

  line.append(svgEl('animate', {
    attributeName: 'stroke-dashoffset',
    values: `${12 * zoom};0`,
    dur: '0.85s',
    repeatCount: 'indefinite',
  }));

  group.append(line);

  const target = svgEl('circle', {
    cx: to.x, cy: to.y, r: 11 * zoom,
    class: 'travel-target',
    'stroke-width': 1.3 * zoom,
  });

  target.append(svgEl('animate', {
    attributeName: 'r',
    values: `${9 * zoom};${19 * zoom}`,
    dur: '1.6s',
    repeatCount: 'indefinite',
  }));

  target.append(svgEl('animate', {
    attributeName: 'opacity', values: '.8;0', dur: '1.6s', repeatCount: 'indefinite',
  }));

  group.append(target);
  map.append(group);
}

// The jump map has deliberately no place nodes. It can still locate the player
// honestly at system level, which makes its otherwise abstract graph useful
// without pretending it knows a position inside that system.
function drawNetworkHere(map) {
  const here = currentMapLocation();
  const point = here?.system && SYSTEM_LAYOUT[here.system];
  if (!point) return;

  const group = svgEl('g', { class: 'map-here' });
  group.append(svgEl('circle', {
    cx: point.x, cy: point.y, r: point.radius + 15, class: 'here-ring', 'stroke-width': '2',
  }));
  group.append(svgEl('circle', { cx: point.x, cy: point.y, r: 6, class: 'here-dot', 'stroke-width': '1.4' }));

  const pulse = svgEl('circle', {
    cx: point.x, cy: point.y, r: point.radius + 12, class: 'here-pulse', 'stroke-width': '1.2',
  });
  pulse.append(svgEl('animate', {
    attributeName: 'r', values: `${point.radius + 10};${point.radius + 30}`, dur: '2.2s', repeatCount: 'indefinite',
  }));
  pulse.append(svgEl('animate', {
    attributeName: 'opacity', values: '.65;0', dur: '2.2s', repeatCount: 'indefinite',
  }));
  group.append(pulse);

  const label = svgEl('text', {
    x: point.x, y: point.y - point.radius - 26, 'text-anchor': 'middle',
    class: 'map-label here-label', style: `font-size:${labelSize(0.85)}px`,
  });
  label.textContent = `YOU · ${here.name}`;
  group.append(label);
  map.append(group);
}

function drawHere() {
  const map = $('#starmap');
  map.querySelectorAll('.map-here').forEach((n) => n.remove());

  if (mapMode() === 'network') {
    drawNetworkHere(map);
    return;
  }

  const point = hereId && nodeAt.get(hereId);
  updateHereControl();
  if (!point) return;

  // Two rings: a steady one to read against the dot, and an expanding pulse.
  // Sized in map units against the current zoom so the marker stays the same
  // size on screen however far in the view is.
  const zoom = view.w / HOME_VIEW.w;
  const ring = 15 * zoom;
  const group = svgEl('g', { class: 'map-here' });

  group.append(svgEl('circle', {
    cx: point.x, cy: point.y, r: ring, class: 'here-ring', 'stroke-width': 1.6 * zoom,
  }));
  group.append(svgEl('line', {
    x1: point.x - ring - 5 * zoom, y1: point.y, x2: point.x + ring + 5 * zoom, y2: point.y,
    class: 'here-tick', 'stroke-width': zoom,
  }));
  group.append(svgEl('line', {
    x1: point.x, y1: point.y - ring - 5 * zoom, x2: point.x, y2: point.y + ring + 5 * zoom,
    class: 'here-tick', 'stroke-width': zoom,
  }));
  group.append(svgEl('circle', {
    cx: point.x, cy: point.y, r: 4 * zoom, class: 'here-dot', 'stroke-width': zoom,
  }));

  const pulse = svgEl('circle', {
    cx: point.x, cy: point.y, r: ring, class: 'here-pulse', 'stroke-width': zoom,
  });
  pulse.append(svgEl('animate', {
    attributeName: 'r', values: `${13 * zoom};${30 * zoom}`, dur: '2.2s', repeatCount: 'indefinite',
  }));
  pulse.append(svgEl('animate', {
    attributeName: 'opacity', values: '.65;0', dur: '2.2s', repeatCount: 'indefinite',
  }));
  group.append(pulse);

  const label = svgEl('text', {
    x: point.x + ring + 16 * zoom, y: point.y - ring - 15 * zoom, 'text-anchor': 'start',
    class: 'map-label here-label', style: `font-size:${labelSize(0.85)}px`,
  });
  // The place already owns the node's usual label. A short, offset callout
  // makes the live marker legible without printing the same place name twice
  // on top of itself.
  label.textContent = 'YOU ARE HERE';
  group.append(svgEl('line', {
    x1: point.x + ring * .62, y1: point.y - ring * .62,
    x2: point.x + ring + 10 * zoom, y2: point.y - ring - 11 * zoom,
    class: 'here-leader', 'stroke-width': zoom,
  }));
  group.append(label);

  map.append(group);
}

/** Wheel zoom, drag pan, and the toolbar. Wired once. */
function initMap() {
  const map = $('#starmap');

  map.addEventListener('wheel', (e) => {
    e.preventDefault();
    cancelAnimationFrame(viewAnimation);

    // Zoom about the cursor rather than the centre, so the place being
    // inspected stays under the pointer.
    const box = map.getBoundingClientRect();
    const fx = (e.clientX - box.left) / box.width;
    const fy = (e.clientY - box.top) / box.height;

    const factor = e.deltaY > 0 ? 1.15 : 1 / 1.15;
    const w = Math.min(HOME_VIEW.w * 1.6, Math.max(HOME_VIEW.w * 0.05, view.w * factor));
    const h = w * (HOME_VIEW.h / HOME_VIEW.w);

    view.x += (view.w - w) * fx;
    view.y += (view.h - h) * fy;
    view.w = w;
    view.h = h;
    applyView();
  }, { passive: false });

  let drag = null;

  /*
   * Pressing is not yet dragging.
   *
   * The map captures the pointer so a drag that leaves the window still pans,
   * and capturing on pointerdown looked like the place to do it. It is not:
   * while an element holds the capture, the browser delivers the click to that
   * element rather than to whatever is under the cursor - so every click meant
   * for a place was delivered to the map, and no place ever opened its card,
   * its trade panel, or went onto a plan. The map has been unclickable with a
   * real mouse since it was written; only a synthetic click dispatched
   * straight at a node ever worked, which is how it passed its own tests.
   *
   * So the capture waits for movement. Below the slop a press is a click and
   * is left entirely alone; past it the drag begins and takes the pointer.
   */
  const DRAG_SLOP = 4;

  map.addEventListener('pointerdown', (e) => {
    cancelAnimationFrame(viewAnimation);
    drag = { x: e.clientX, y: e.clientY, vx: view.x, vy: view.y, moving: false };
  });

  map.addEventListener('pointermove', (e) => {
    if (!drag) return;

    if (!drag.moving) {
      if (Math.hypot(e.clientX - drag.x, e.clientY - drag.y) < DRAG_SLOP)
        return;

      drag.moving = true;
      map.setPointerCapture?.(e.pointerId);
      map.classList.add('dragging');
    }

    const box = map.getBoundingClientRect();
    view.x = drag.vx - ((e.clientX - drag.x) / box.width) * view.w;
    view.y = drag.vy - ((e.clientY - drag.y) / box.height) * view.h;
    applyView();
  });

  const endDrag = (e) => {
    if (!drag) return;

    if (drag.moving) {
      map.releasePointerCapture?.(e.pointerId);
      map.classList.remove('dragging');
    }

    drag = null;
  };

  map.addEventListener('pointerup', endDrag);
  map.addEventListener('pointercancel', endDrag);

  $('#map-reset').addEventListener('click', () => animateViewTo(HOME_VIEW));

  $('#map-here').addEventListener('click', focusHere);
  $('#map-visited-only').addEventListener('change', () => drawMap());
  $('#map-shade').addEventListener('change', () => drawMap());
  const mode = $('#map-mode');
  const system = $('#map-system');
  try {
    mode.value = localStorage.getItem(MAP_MODE_KEY) || 'system';
    system.value = localStorage.getItem(MAP_SYSTEM_KEY) || '';
  } catch { /* private mode */ }
  mode.addEventListener('change', () => {
    syncMapModeControls();
    try { localStorage.setItem(MAP_MODE_KEY, mode.value); } catch { /* fine */ }
    drawMap();
  });
  system.addEventListener('change', () => {
    try { localStorage.setItem(MAP_SYSTEM_KEY, system.value); } catch { /* fine */ }
    drawMap();
  });
  for (const button of $$('#map-service-filter button'))
    button.addEventListener('click', () => selectMapService(button.dataset.service));
  for (const button of $$('#map-focus-filter button'))
    button.addEventListener('click', () => selectMapFocus(button.dataset.focus));

  const labelDensity = $('#map-label-density');
  try { labelDensity.value = localStorage.getItem(MAP_LABEL_DENSITY_KEY) || 'auto'; } catch { /* private mode */ }
  labelDensity.addEventListener('change', () => {
    try { localStorage.setItem(MAP_LABEL_DENSITY_KEY, labelDensity.value); } catch { /* private mode */ }
    drawMap();
  });

  $('#map-preset').addEventListener('change', (event) => applyMapPreset(event.target.value));
  $('#map-save-preset').addEventListener('click', saveMapView);
  initCargoPanel();
  onInput('#map-search', () => { drawMap(); renderSearchResults(); });

  // Escape clears the search and glides home; clicking anywhere else just
  // closes the suggestion list.
  $('#map-search').addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      e.target.value = '';
      $('#map-results').hidden = true;
      drawMap();
    }
  });

  document.addEventListener('click', (e) => {
    if (!e.target.closest('.map-search-box')) $('#map-results').hidden = true;
  });

  const follow = $('#map-follow');
  follow.classList.toggle('active', followHere);

  follow.addEventListener('click', () => {
    followHere = !followHere;
    follow.classList.toggle('active', followHere);
    try { localStorage.setItem('qw-map-follow', followHere ? '1' : '0'); } catch { /* fine */ }

    if (followHere && hereId) centreOn(hereId);
  });

  // Clicking empty map space dismisses the detail card.
  map.addEventListener('click', () => $('#map-info').hidden = true);
  $('#map-info-close').addEventListener('click', () => $('#map-info').hidden = true);

  // The Goods checkbox: one switch for goods on hover tips and on the detail
  // card, remembered per browser.
  const goodsToggle = $('#map-goods');
  try { goodsToggle.checked = localStorage.getItem('qw-map-sold') === '1'; } catch { /* fine */ }

  goodsToggle.addEventListener('change', () => {
    try { localStorage.setItem('qw-map-sold', goodsToggle.checked ? '1' : '0'); } catch { /* fine */ }
    renderMapInfoSold();
  });

  // Keyboard zoom for anyone without a wheel: +/- around the view centre.
  document.addEventListener('keydown', (e) => {
    if (!$('#view-map').classList.contains('active')) return;
    if (e.target.matches('input, select, textarea')) return;

    if (e.key === '+' || e.key === '=' || e.key === '-') {
      const factor = e.key === '-' ? 1.25 : 1 / 1.25;
      const w = Math.min(HOME_VIEW.w * 1.6, Math.max(HOME_VIEW.w * 0.05, view.w * factor));
      const h = w * (HOME_VIEW.h / HOME_VIEW.w);
      view.x += (view.w - w) / 2;
      view.y += (view.h - h) / 2;
      view.w = w;
      view.h = h;
      applyView();
      e.preventDefault();
    }
  });
}

/**
 * Flies to a named place: the map view, centred, detail card open. Falls back
 * to a highlight search when the name is not an exact atlas match - a quantum
 * destination or a shop name still lights whatever answers to it.
 */
function jumpToPlace(name) {
  const wanted = String(name).trim();
  const entry = atlas.find((l) => l.name.toLowerCase() === wanted.toLowerCase());

  $('#map-search').value = entry ? '' : wanted;
  $('#map-results').hidden = true;
  if (entry?.system && SYSTEM_COLOURS[entry.system]) {
    $('#map-mode').value = 'system';
    $('#map-system').value = entry.system;
    syncMapModeControls();
  }
  showView('map');
  drawMap();

  if (entry) {
    centreOn(entry.rawId);
    showMapInfo(entry);
  }
}

/**
 * A place name that flies to the map when clicked. Used everywhere a location
 * appears in a table or card, so "where is that?" is always one click.
 */
function placeLink(name) {
  const link = el('button', 'place-link', name);
  link.type = 'button';
  link.title = 'Show on the map';
  link.addEventListener('click', (e) => {
    e.stopPropagation();
    jumpToPlace(name);
  });
  return link;
}

/** A table cell holding a place link, or a plain dash when there is nothing. */
function tdPlace(name, cls = null) {
  const td = el('td', cls);
  if (name && name !== '—' && name !== '?') td.append(placeLink(name));
  else td.textContent = name || '—';
  return td;
}

/**
 * The suggestion list under the search box: the top place-name matches, most
 * visited first. Highlighting shows WHERE matches are; this list is for
 * jumping straight to ONE of them by name.
 */
function renderSearchResults() {
  const box = $('#map-results');
  if (!box) return;

  const term = ($('#map-search')?.value || '').trim().toLowerCase();

  if (!term) {
    box.hidden = true;
    return;
  }

  const matches = atlas
    .filter((l) => l.name.toLowerCase().includes(term))
    .sort((a, b) => b.visits - a.visits || a.name.localeCompare(b.name))
    .slice(0, 6);

  // Commodities belong here too: without them there was no way to learn that
  // the thing you wanted is spelled "Medical Supplies", and the search looked
  // broken when it was only being literal.
  const goods = marketEntries
    .filter((e) => e.name.toLowerCase().includes(term) && (e.sold.length || e.bought.length))
    .sort((a, b) => a.name.length - b.name.length)
    .slice(0, 6);

  box.textContent = '';

  if (matches.length === 0 && goods.length === 0) {
    box.hidden = true;
    return;
  }

  for (const match of matches) {
    const row = el('button', 'map-result');
    row.type = 'button';
    row.append(el('span', 'name', match.name));
    row.append(el('span', 'where',
      [match.body, match.system].filter(Boolean).join(' · ') || 'unmapped'));

    row.addEventListener('click', () => {
      box.hidden = true;
      centreOn(match.rawId);
      showMapInfo(match);
    });

    box.append(row);
  }

  for (const good of goods) {
    const row = el('button', 'map-result');
    row.type = 'button';
    row.append(el('span', 'name good', good.name));
    row.append(el('span', 'where',
      `commodity · ${good.sold.length} sellers`));

    row.addEventListener('click', () => {
      box.hidden = true;
      $('#map-search').value = good.name;
      drawMap();
    });

    box.append(row);
  }

  box.hidden = false;
}

/* ---------- commodity shading ---------- */

/**
 * In commodity mode the highlight can carry a second dimension: UEX terminal
 * prices grade each lit place by how good it is - best sell price (or, when
 * asked, how much SCU it takes), and for a buy: search the cheapest price or
 * deepest stock. The legend swaps to the gradient while this is on.
 */
let shadeRows = { name: null, rows: null };
let shadeScale = null;
const nodeShade = new Map();
let mapPriceFreshness = null;

function priceFreshness(seenAt) {
  const at = Date.parse(seenAt || '');
  if (!Number.isFinite(at)) return null;
  const hours = Math.max(0, (Date.now() - at) / 3600000);
  return hours > 14 * 24
    ? { state: 'stale', label: `UEX report ${Math.floor(hours / 24)}d old` }
    : hours > 72
      ? { state: 'aging', label: `UEX report ${Math.floor(hours / 24)}d old` }
      : { state: 'fresh', label: `UEX report ${Math.max(1, Math.round(hours))}h old` };
}

const SHADE_STOPS = ['#24543f', '#4fd48a', '#ffe08a'];

/*
 * The colour of a place that has nothing to do with the chosen commodity.
 *
 * Slate rather than its own kind colour: with a commodity picked the map stops
 * being a map of kinds and becomes a map of one price, and a station left
 * cyan among a scale running green to gold reads as a value on that scale when
 * it is not one. Every mark takes the grade or takes this.
 */
const NO_TRADE = '#42525f';


/** Interpolates the poor-to-best ramp; 1 is the gold end. */
function shadeColour(t) {
  const seg = t < 0.5 ? [SHADE_STOPS[0], SHADE_STOPS[1]] : [SHADE_STOPS[1], SHADE_STOPS[2]];
  const k = t < 0.5 ? t * 2 : (t - 0.5) * 2;

  const mix = (i) => {
    const a = parseInt(seg[0].slice(1 + i * 2, 3 + i * 2), 16);
    const b = parseInt(seg[1].slice(1 + i * 2, 3 + i * 2), 16);
    return Math.round(a + (b - a) * k);
  };

  return `rgb(${mix(0)},${mix(1)},${mix(2)})`;
}

function terminalMatchesPlace(terminal, place) {
  const t = terminal.toLowerCase().replace(/[^a-z0-9]/g, '');
  const p = place.toLowerCase().replace(/[^a-z0-9]/g, '');
  if (t.length < 4 || p.length < 4) return false;
  return t.includes(p) || p.includes(t);
}

/**
 * Builds the per-node metric for the current search, fetching the commodity's
 * terminal rows on first sight and redrawing once they land.
 */
function prepareShading(term, sites) {
  nodeShade.clear();
  shadeScale = null;
  mapPriceFreshness = null;

  const shadeSelect = $('#map-shade');
  shadeSelect.hidden = !sites;
  if (!sites || !highlightIds) return;

  const buying = term.startsWith('buy:');
  const name = (buying ? term.slice(4) : term).trim();
  const entry = marketEntries.find((e) => e.name.toLowerCase() === name);
  if (!entry) return;

  if (shadeSelect.value !== 'mine') mapPriceFreshness = priceFreshness(entry.uex?.seenAt);

  // Shading by your own receipts needs no fetch and works with UEX off: the
  // question it answers is "where did I do best with this", not "what is it
  // worth today". Both are useful; only the player knows which they meant.
  if (shadeSelect.value === 'mine') {
    shadeFromReceipts(entry.name, buying);
    return;
  }

  if (shadeRows.name !== entry.name) {
    shadeRows = { name: entry.name, rows: null };

    getJson(`/api/uex/market?commodity=${encodeURIComponent(entry.name)}`)
      .then((rows) => {
        if (shadeRows.name === entry.name) {
          shadeRows.rows = rows;
          drawMap();
        }
      })
      .catch(() => { /* UEX off or unreachable; highlight stays ungraded */ });

    return;
  }

  if (!shadeRows.rows?.length) return;

  const byScu = shadeSelect.value === 'scu';

  const metricOf = (row) => {
    if (buying) return byScu ? row.buyScu : row.buy;
    return byScu ? row.sellScu : row.sell;
  };

  // Lower is better only for a buy price; capacity and sell price want big.
  const invert = buying && !byScu;

  for (const id of highlightIds) {
    const place = atlas.find((l) => l.rawId === id);
    if (!place) continue;

    // The server resolves each terminal to a place; the name match is only for
    // rows it could not place, which would otherwise shade nothing at all.
    const matched = shadeRows.rows.filter((r) => metricOf(r) > 0
      && (r.placeId ? r.placeId === id : terminalMatchesPlace(r.terminal, place.name)));

    if (!matched.length) continue;

    const value = invert
      ? Math.min(...matched.map(metricOf))
      : Math.max(...matched.map(metricOf));

    nodeShade.set(id, { value });
  }

  if (nodeShade.size === 0) return;

  const values = [...nodeShade.values()].map((s) => s.value);
  const min = Math.min(...values);
  const max = Math.max(...values);

  for (const shade of nodeShade.values()) {
    const t = max === min ? 1 : (shade.value - min) / (max - min);
    shade.colour = shadeColour(invert ? 1 - t : t);
  }

  shadeScale = {
    min,
    max,
    invert,
    unit: byScu ? 'SCU' : 'aUEC/SCU',
    label: buying
      ? (byScu ? 'stock on offer' : 'buy price, cheapest is gold')
      : (byScu ? 'sell capacity' : 'sell price, best is gold'),
  };
}

/* ---------- cargo panel ---------- */

/**
 * Every cargo receipt this install holds, fetched once.
 *
 * The catalogue and UEX say what a commodity is worth *now*; these say what the
 * player was actually paid, and where. Both belong in the same panel, because
 * the question - is this still the run it was last week? - needs the two side
 * by side. A few hundred receipts is nothing to loop over, and the panel
 * changes commodity, side and window often enough that a round trip per twiddle
 * would be the slow part.
 */
let cargoReceipts = [];

/** What the panel is showing. */
const cargo = {
  name: null,
  buying: false,
  days: 0,

  /** Set when the panel is showing one station rather than one commodity. */
  place: null,
};

async function loadCargoReceipts() {
  cargoReceipts = (await getJson('/api/commodities?days=0'))
    .filter((t) => t.commodity && t.unitPrice > 0);

  if (!$('#cargo-panel').hidden) drawMap();
}

/** Receipts inside the panel's window. */
function receiptsInWindow() {
  const cutoff = cargo.days ? Date.now() - cargo.days * 86400000 : null;
  return cutoff ? cargoReceipts.filter((t) => new Date(t.at).getTime() >= cutoff) : cargoReceipts;
}

/** The catalogue entry a search term names, or null when it names a place. */
function commodityEntry(term) {
  const name = (term.startsWith('buy:') ? term.slice(4) : term).trim();
  return marketEntries.find((e) => e.name.toLowerCase() === name) || null;
}

/**
 * One row per place for a commodity on one side of the counter.
 *
 * Best is side-aware - the most you were paid selling, the least you paid
 * buying - and the average is weighted by volume rather than by receipt, so one
 * 320 SCU run does not count the same as one 2 SCU top-up.
 */
function receiptsFor(name, buying) {
  const byPlace = new Map();

  for (const trade of receiptsInWindow()) {
    if (trade.commodity !== name || trade.isSell === buying) continue;

    const key = trade.placeId || trade.place;
    let row = byPlace.get(key);

    if (!row) {
      row = {
        id: trade.placeId, name: trade.place,
        trades: 0, scu: 0, amount: 0, best: 0, average: 0, latest: 0, latestAt: null,
      };
      byPlace.set(key, row);
    }

    row.trades += 1;
    row.scu += trade.scu;
    row.amount += Number(trade.amount);

    row.best = row.trades === 1
      ? trade.unitPrice
      : (buying ? Math.min(row.best, trade.unitPrice) : Math.max(row.best, trade.unitPrice));

    if (!row.latestAt || new Date(trade.at) > new Date(row.latestAt)) {
      row.latestAt = trade.at;
      row.latest = trade.unitPrice;
    }
  }

  for (const row of byPlace.values()) row.average = row.scu ? row.amount / row.scu : 0;

  return [...byPlace.values()].sort((a, b) => (buying ? a.best - b.best : b.best - a.best));
}

/** Keeps the toolbar's controls in step with the search term driving them. */
function syncCargoControls() {
  for (const button of $$('#map-side button')) {
    button.classList.toggle('active', (button.dataset.side === 'buy') === cargo.buying);
  }
}

/**
 * Shows, hides or refills the panel for whatever the map is currently showing.
 * Called from drawMap, so the panel can never disagree with the shading.
 */
function syncCargoPanel(term, sites) {
  const entry = sites ? commodityEntry(term) : null;

  cargo.name = entry ? entry.name : null;
  cargo.buying = term.startsWith('buy:');

  $('#map-side').hidden = !entry;
  $('#map-window').hidden = !entry && !cargo.place;
  syncCargoControls();

  // A flight plan holds the panel until it is closed: it is the thing being
  // worked on, and a redraw must not pull it out from under the player.
  if (cargo.trip) {
    renderTripPanel();
    return;
  }

  if (cargo.place) {
    renderStationPanel();
    return;
  }

  if (!entry) {
    $('#cargo-panel').hidden = true;
    return;
  }

  renderCommodityPanel(entry);
}

function cargoPanelHead(kicker, title) {
  $('#cargo-kicker').textContent = kicker;
  $('#cargo-title').textContent = title;
  $('#cargo-panel').hidden = false;

  const body = $('#cargo-body');
  body.textContent = '';
  return body;
}

const windowWord = () =>
  (PERIODS.find((p) => p.days === cargo.days)?.label || 'All time').toLowerCase();

function renderCommodityPanel(entry) {
  const body = cargoPanelHead(cargo.buying ? 'Commodity · buying' : 'Commodity · selling', entry.name);

  // What the market says today, from the rows the shading already fetched.
  body.append(el('div', 'cargo-h', cargo.buying ? 'Cheapest terminals' : 'Best terminals'));

  if (shadeRows.name === entry.name && shadeRows.rows?.length) {
    const priceOf = (row) => (cargo.buying ? row.buy : row.sell);
    const scuOf = (row) => (cargo.buying ? row.buyScu : row.sellScu);

    const rows = shadeRows.rows
      .filter((r) => priceOf(r) > 0)
      .sort((a, b) => (cargo.buying ? priceOf(a) - priceOf(b) : priceOf(b) - priceOf(a)))
      .slice(0, 10);

    const values = rows.map(priceOf);
    const lo = Math.min(...values);
    const hi = Math.max(...values);

    for (const row of rows) {
      const t = hi === lo ? 1 : (priceOf(row) - lo) / (hi - lo);

      body.append(cargoRow({
        colour: shadeColour(cargo.buying ? 1 - t : t),
        name: row.terminal,
        sub: `${Math.round(scuOf(row)).toLocaleString()} SCU ${cargo.buying ? 'in stock' : 'of demand'}`,
        value: priceOf(row),
        onClick: () => centreOnTerminal(row.terminal, row.placeId),
      }));
    }
  } else {
    body.append(el('div', 'cargo-empty', shadeRows.name === entry.name
      ? 'No live prices for this commodity. UEX may be off — see Settings.'
      : 'Fetching live prices…'));
  }

  // What the player was actually paid, which the market cannot tell them.
  const mine = receiptsFor(entry.name, cargo.buying);
  body.append(el('div', 'cargo-h', `Your receipts · ${windowWord()}`));

  if (!mine.length) {
    body.append(el('div', 'cargo-empty',
      `You have never ${cargo.buying ? 'bought' : 'sold'} this in that window.`));
  }

  const best = mine.length ? mine[0].best : 0;
  const worst = mine.length ? mine[mine.length - 1].best : 0;

  for (const row of mine) {
    const span = Math.abs(best - worst);
    const t = span === 0 ? 1 : Math.abs(row.best - worst) / span;

    body.append(cargoRow({
      colour: shadeColour(cargo.buying ? 1 - t : t),
      name: row.name,
      sub: `${row.trades} receipt${row.trades === 1 ? '' : 's'} · ${row.scu.toLocaleString()} SCU`
        + ` · avg ${Math.round(row.average).toLocaleString()} · ${dayOf(row.latestAt)}`,
      value: row.best,
      onClick: () => {
        if (row.id) centreOn(row.id);
        showStation(row.id, row.name);
      },
    }));
  }

  const history = receiptsInWindow().filter((t) => t.commodity === entry.name);
  body.append(el('div', 'cargo-h', `Trade history · ${history.length}`));
  cargoHistory(body, history, (t) => t.place);
}

/**
 * One station's trade, opened by double-clicking it.
 *
 * The detail card answers "what is this place"; this answers "what moves
 * through it" - which the card has no room for once a station has a dozen
 * commodities on each side of the counter.
 */
function renderStationPanel() {
  const place = cargo.place;
  const key = place.id || place.name;
  const body = cargoPanelHead('Station', place.name);

  const at = receiptsInWindow().filter((t) => (t.placeId || t.place) === key);

  const add = el('button', 'ghost tiny wide', 'Add as a stop');
  add.title = 'Put this place on the flight plan';
  add.addEventListener('click', () => addStop(place.id, place.name, null));
  body.append(add);

  cargoSection(body, 'Accepts · you sold here', at.filter((t) => t.isSell), false);
  cargoSection(body, 'Offers · you bought here', at.filter((t) => !t.isSell), true);

  // The catalogue's own answer, which covers commodities never traded here.
  const catalogue = place.location ? commoditiesSoldAt(place.location) : [];

  if (catalogue.length) {
    body.append(el('div', 'cargo-h', `Catalogue says it sells · ${catalogue.length}`));

    const list = el('div', 'cargo-goods');
    list.textContent = catalogue.slice(0, 24).join(', ')
      + (catalogue.length > 24 ? `, +${catalogue.length - 24} more` : '');
    body.append(list);
  }

  body.append(el('div', 'cargo-h', `Trade history · ${at.length}`));
  cargoHistory(body, at, (t) => t.commodity);
}

/** One side of a station's counter, grouped by commodity. */
function cargoSection(body, title, trades, buying) {
  body.append(el('div', 'cargo-h', title));

  if (!trades.length) {
    body.append(el('div', 'cargo-empty', 'Nothing on record in that window.'));
    return;
  }

  const byName = new Map();

  for (const trade of trades) {
    const row = byName.get(trade.commodity)
      || { name: trade.commodity, trades: 0, scu: 0, amount: 0, latestAt: null };

    row.trades += 1;
    row.scu += trade.scu;
    row.amount += Number(trade.amount);
    if (!row.latestAt || new Date(trade.at) > new Date(row.latestAt)) row.latestAt = trade.at;

    byName.set(trade.commodity, row);
  }

  for (const row of [...byName.values()].sort((a, b) => b.scu - a.scu)) {
    body.append(cargoRow({
      colour: buying ? '#ffab3d' : '#4fd48a',
      name: row.name,
      sub: `${row.trades} receipt${row.trades === 1 ? '' : 's'} · ${row.scu.toLocaleString()} SCU`
        + ` · ${dayOf(row.latestAt)}`,
      value: row.amount / (row.scu || 1),

      // Drilling into a commodity from a station shades the whole map for it,
      // so the next question - where else does this go - is already answered.
      onClick: () => searchCommodity(row.name, buying),
    }));
  }
}

function cargoRow({ colour, name, sub, value, onClick }) {
  const row = el('div', 'cargo-row');

  const swatch = el('span', 'swatch');
  swatch.style.background = colour;
  row.append(swatch);

  const middle = el('div', 'cargo-row-main');
  middle.append(el('div', 'name', name));
  middle.append(el('div', 'sub', sub));
  row.append(middle);

  const price = el('div', 'price', Math.round(Number(value) || 0).toLocaleString());
  price.append(el('span', 'u', 'aUEC / SCU'));
  row.append(price);

  if (onClick) {
    row.tabIndex = 0;
    row.addEventListener('click', onClick);
    row.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        onClick();
      }
    });
  }

  return row;
}

/** The receipt list: newest first, capped so a long history stays scrollable. */
function cargoHistory(body, trades, describe) {
  if (!trades.length) {
    body.append(el('div', 'cargo-empty', 'No trades in that window.'));
    return;
  }

  for (const trade of trades.slice(0, 30)) {
    const line = el('div', `cargo-line ${trade.isSell ? 'sell' : 'buy'}`);
    line.append(el('span', 'when', dayOf(trade.at)));
    line.append(el('span', 'what',
      `${trade.isSell ? 'Sold' : 'Bought'} ${trade.scu} SCU · ${describe(trade)}`));
    line.append(el('span', 'rate', Math.round(trade.unitPrice).toLocaleString()));
    body.append(line);
  }

  if (trades.length > 30) {
    body.append(el('div', 'cargo-empty', `+ ${trades.length - 30} older, on the Cargo page.`));
  }
}

/** Opens the panel on one station. */
function showStation(rawId, name) {
  const location = atlas.find((l) => l.rawId === rawId);

  cargo.trip = false;
  cargo.place = { id: rawId, name: name || location?.name || rawId, location };
  $('#map-window').hidden = false;
  $('#map-info').hidden = true;
  renderStationPanel();
}

/** Puts a commodity in the search box, which is what drives the whole map. */
function searchCommodity(name, buying) {
  cargo.trip = false;
  cargo.place = null;
  $('#map-search').value = buying ? `buy:${name}` : name;
  $('#map-results').hidden = true;
  drawMap();
}

/**
 * Centres on the atlas place a UEX terminal name belongs to.
 *
 * Terminal names are not atlas names - "TDD, Area 18" against "Area18" - so
 * this uses the same loose match the shading joins on, and simply does nothing
 * when the map cannot name the place.
 */
function centreOnTerminal(terminal, placeId) {
  const match = placeId
    ? atlas.find((l) => l.rawId === placeId)
    : atlas.find((l) => terminalMatchesPlace(terminal, l.name));

  if (!match) return;

  centreOn(match.rawId);
  showStation(match.rawId, match.name);
}

/** The panel's own controls. Wired once, from initMap. */
function initCargoPanel() {
  $('#map-side').addEventListener('click', (e) => {
    const button = e.target.closest('button');
    if (!button || !cargo.name) return;

    // The search box is the single source of truth for which side the map is
    // showing, so the toggle rewrites the term rather than keeping its own flag.
    searchCommodity(cargo.name, button.dataset.side === 'buy');
  });

  $('#map-window').addEventListener('change', (e) => {
    cargo.days = Number(e.target.value) || 0;
    drawMap();
  });

  $('#map-plan').addEventListener('click', () => {
    showTripPanel();
    drawMap();
  });

  // The detail card can put the place it is describing on the plan.
  $('#map-info-stop').addEventListener('click', () => {
    if (mapInfoLocation) addStop(mapInfoLocation.rawId, mapInfoLocation.name, null);
  });

  $('#cargo-close').addEventListener('click', () => {
    if (!cargo.trip && cargo.place && cargo.name) {
      cargo.place = null;
      drawMap();
      return;
    }

    cargo.trip = false;
    cargo.place = null;
    $('#cargo-panel').hidden = true;
    $('#map-window').hidden = true;
  });
}

/**
 * Grades the lit places by what the player was paid at them, as the offline
 * alternative to UEX's view of today.
 */
function shadeFromReceipts(name, buying) {
  const rows = receiptsFor(name, buying).filter((r) => r.id);
  if (!rows.length) return;

  // A place you have traded at is an answer even when the catalogue has never
  // heard of it, so receipts light their own nodes rather than only grading
  // the ones the catalogue lit.
  for (const row of rows) {
    highlightIds.add(row.id);
    nodeShade.set(row.id, { value: row.best });
  }

  const values = rows.map((r) => r.best);
  const min = Math.min(...values);
  const max = Math.max(...values);

  for (const shade of nodeShade.values()) {
    const t = max === min ? 1 : (shade.value - min) / (max - min);
    shade.colour = shadeColour(buying ? 1 - t : t);
  }

  shadeScale = {
    min,
    max,
    invert: buying,
    unit: 'aUEC/SCU',
    label: buying
      ? `your buy price, ${windowWord()}, cheapest is gold`
      : `your sell price, ${windowWord()}, best is gold`,
    plain: 'no receipt of yours here',
  };
}

/* ---------- flight plans ---------- */

/**
 * The player's own routes: where they mean to go, in order.
 *
 * Everything else on this page is observed or downloaded. A plan is authored,
 * so it lives on the server rather than in the browser - the overlay is a
 * second window onto the same session, and a plan that existed in only one of
 * them would be worse than none.
 */
let trips = [];

/** The plan the Now card and the map are following. */
const tracked = () => trips.find((t) => t.tracked) || null;

/** Where to go now, or what remains to do after reaching the current stop. */
const nextStop = (trip) => trip?.stops.find((s) =>
  !s.done || (s.actions || []).some((action) => !action.done)) || null;

async function loadTrips() {
  trips = await getJson(`/api/trips${importedQuery()}`);
  renderTripCard();
  reloadPilotBriefing().catch(() => {});
  if (!$('#cargo-panel').hidden && cargo.trip) renderTripPanel();
  // A plan can itself be the active map layer; rebuild then so adding,
  // reordering, or crossing off a stop never leaves a ghost destination.
  if (mapFocusFilter === 'plan') drawMap();
  else drawTripPath();
}

/** POST/DELETE against the trip API, then re-read: plans are small. */
async function tripCall(url, method = 'POST') {
  await fetch(url, { method });
  await loadTrips();
}

/** A run sheet is authored work, so checks and edits use the same small trip API. */
async function runActionCall(url, method = 'POST', body = null) {
  const options = { method };
  if (body) {
    options.headers = { 'Content-Type': 'application/json' };
    options.body = JSON.stringify(body);
  }
  await fetch(url, options);
  await loadTrips();
}

const runActionLabel = (kind) => ({
  load: 'Load', unload: 'Unload', buy: 'Buy', sell: 'Sell', collect: 'Collect',
  refuel: 'Refuel', repair: 'Repair', do: 'Do',
}[kind] || 'Do');

function runActionRow(trip, stop, action, editable = false) {
  const row = el('div', `run-action${action.done ? ' done' : ''}`);
  const check = document.createElement('input');
  check.type = 'checkbox';
  check.checked = !!action.done;
  check.title = action.done ? 'Mark as still to do' : 'Mark action complete';
  check.addEventListener('change', () =>
    runActionCall(`/api/trips/${trip.id}/stops/${stop.id}/actions/${action.id}/toggle`));
  row.append(check);

  const text = el('span', 'run-action-text');
  text.append(el('b', null, runActionLabel(action.kind)));
  text.append(document.createTextNode(` · ${action.text}`));
  if (action.quantity !== null && action.quantity !== undefined) {
    const quantity = Number(action.quantity).toLocaleString();
    text.append(el('span', 'run-action-quantity', `${quantity}${action.unit ? ` ${action.unit}` : ''}`));
  }
  row.append(text);

  if (editable) {
    const remove = el('button', 'ghost tiny', '×');
    remove.title = 'Remove this action';
    remove.addEventListener('click', () =>
      runActionCall(`/api/trips/${trip.id}/stops/${stop.id}/actions/${action.id}`, 'DELETE'));
    row.append(remove);
  }

  return row;
}

function runActionForm(trip, stop) {
  const form = el('div', 'run-action-form');
  const kind = document.createElement('select');
  kind.className = 'select';
  for (const value of ['load', 'unload', 'buy', 'sell', 'collect', 'refuel', 'repair', 'do'])
    kind.append(new Option(runActionLabel(value), value));

  const text = document.createElement('input');
  text.type = 'text';
  text.className = 'search';
  text.placeholder = 'What needs doing?';

  const quantity = document.createElement('input');
  quantity.type = 'number';
  quantity.className = 'search run-action-amount';
  quantity.placeholder = 'Qty';
  quantity.min = '0';
  quantity.step = 'any';

  const unit = document.createElement('select');
  unit.className = 'select';
  unit.append(new Option('No unit', ''));
  unit.append(new Option('SCU', 'SCU'));
  unit.append(new Option('units', 'units'));
  unit.append(new Option('aUEC', 'aUEC'));

  const save = el('button', 'ghost tiny', 'Add');
  save.addEventListener('click', () => {
    if (!text.value.trim()) {
      text.focus();
      return;
    }
    runActionCall(`/api/trips/${trip.id}/stops/${stop.id}/actions`, 'POST', {
      kind: kind.value,
      text: text.value,
      quantity: quantity.value === '' ? null : Number(quantity.value),
      unit: unit.value || null,
    });
  });

  form.append(kind, text, quantity, unit, save);
  return form;
}

/**
 * Adds a stop to whatever plan the player is filling, and says where it went.
 *
 * The map, the routes table and a shopping list all funnel through here, so a
 * stop means the same thing wherever it came from.
 */
async function addStop(placeId, place, note) {
  await fetch('/api/trips/stops', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ placeId: placeId || '', place, note: note || null }),
  });

  await loadTrips();
  showTripPanel();
}

/** Starts a plan from a list of stops - a trade run, or a shopping trip. */
async function planTrip(title, stops) {
  await fetch('/api/trips', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ title, stops }),
  });

  await loadTrips();
  showView('map');
  showTripPanel();
  drawMap();
}

/* ---------- the Now card ---------- */

/**
 * The tracked plan on the Now page, and so in the overlay: where to go next,
 * then the rest of the run.
 */
function renderTripCard() {
  const card = $('#now-trip-card');
  if (!card) return;

  const trip = tracked();

  if (!trip || !trip.stops.length) {
    card.hidden = true;
    return;
  }

  const done = trip.stops.filter((s) => s.done).length;
  const next = nextStop(trip);

  $('#now-trip-title').textContent = trip.title;

  const progress = $('#now-trip-progress');
  progress.textContent = '';
  progress.append(jobProgress(done, trip.stops.length,
    `${done} of ${trip.stops.length} stops`));

  const jump = $('#now-trip-next');
  jump.textContent = '';

  if (next) {
    jump.append(el('div', 'now-trip-label', next.done ? 'At this stop' : 'Jump next'));
    jump.append(el('div', 'now-trip-where', next.place));
    if (next.note) jump.append(el('div', 'now-trip-note', next.note));
  } else {
    jump.append(el('div', 'inward', 'Every stop is crossed off. Good run.'));
  }

  // The whole path, in order, ticked as it goes - the flight plan proper.
  const list = $('#now-trip-stops');
  list.textContent = '';

  trip.stops.forEach((stop, index) => {
    list.append(tripStopRow(trip, stop, index, stop === next));
  });

  card.hidden = false;
}

/**
 * One stop, wherever it is shown. Clicking the number crosses it off; clicking
 * the name flies the map to it.
 */
function tripStopRow(trip, stop, index, isNext, editable = false) {
  const row = el('div', `trip-stop${stop.done ? ' done' : ''}${isNext ? ' next' : ''}`);

  const number = el('button', 'trip-number', stop.done ? '✓' : String(index + 1));
  number.title = stop.done ? 'Not done after all' : 'Cross this stop off';
  number.addEventListener('click',
    () => tripCall(`/api/trips/${trip.id}/stops/${stop.id}/toggle`));
  row.append(number);

  const main = el('div', 'trip-stop-main');
  const name = el('button', 'trip-place', stop.place);
  name.title = 'Show it on the map';
  name.addEventListener('click', () => {
    showView('map');
    if (stop.placeId) centreOn(stop.placeId);
    showTripPanel();
  });

  main.append(name);
  if (stop.note) main.append(el('div', 'trip-note', stop.note));
  for (const action of (stop.actions || []))
    main.append(runActionRow(trip, stop, action, editable));
  row.append(main);

  return row;
}

/* ---------- the map's trip panel ---------- */

/** Opens the side panel on the tracked plan. */
function showTripPanel() {
  cargo.place = null;
  cargo.trip = true;
  renderTripPanel();
}

function renderTripPanel() {
  const trip = tracked();
  const body = cargoPanelHead('Flight plan', trip ? trip.title : 'No plan yet');

  if (!trip || !trip.stops.length) {
    body.append(el('div', 'cargo-empty',
      'Double-click a place, or use Add stop on its card, to start a plan. '
      + 'A trade route or a shopping list can start one for you.'));
    return;
  }

  const done = trip.stops.filter((s) => s.done).length;
  body.append(jobProgress(done, trip.stops.length, `${done} of ${trip.stops.length} stops`));

  const next = nextStop(trip);

  trip.stops.forEach((stop, index) => {
    const row = tripStopRow(trip, stop, index, stop === next, true);

    // The panel is where a plan is edited; the Now card only reads it.
    const tools = el('div', 'trip-tools');

    const up = el('button', 'ghost tiny', '↑');
    up.title = 'Earlier in the run';
    up.disabled = index === 0;
    up.addEventListener('click',
      () => tripCall(`/api/trips/${trip.id}/stops/${stop.id}/move?delta=-1`));

    const down = el('button', 'ghost tiny', '↓');
    down.title = 'Later in the run';
    down.disabled = index === trip.stops.length - 1;
    down.addEventListener('click',
      () => tripCall(`/api/trips/${trip.id}/stops/${stop.id}/move?delta=1`));

    const drop = el('button', 'ghost tiny', '×');
    drop.title = 'Drop this stop';
    drop.addEventListener('click',
      () => tripCall(`/api/trips/${trip.id}/stops/${stop.id}`, 'DELETE'));

    const addAction = el('button', 'ghost tiny', '+');
    addAction.title = 'Add a run-sheet action at this stop';
    addAction.addEventListener('click', () => {
      const existing = row.querySelector('.run-action-form');
      if (existing) existing.remove();
      else row.append(runActionForm(trip, stop));
    });

    tools.append(addAction, up, down, drop);
    row.append(tools);
    body.append(row);
  });

  const actions = el('div', 'trip-actions');

  const track = el('button', 'ghost', trip.tracked ? 'Stop tracking' : 'Track');
  track.title = 'Show this plan on the Now page';
  track.addEventListener('click', () => tripCall(`/api/trips/${trip.id}/track`));
  actions.append(track);

  const scrap = el('button', 'ghost danger', 'Delete plan');
  scrap.addEventListener('click', async () => {
    await tripCall(`/api/trips/${trip.id}`, 'DELETE');
    cargo.trip = false;
    $('#cargo-panel').hidden = true;
    drawMap();
  });
  actions.append(scrap);

  body.append(actions);

  // Other plans, so one can be picked up again without a management screen.
  const others = trips.filter((t) => t !== trip);

  if (others.length) {
    body.append(el('div', 'cargo-h', 'Other plans'));

    for (const other of others) {
      const row = el('div', 'cargo-row');
      row.append(el('span', 'swatch'));

      const main = el('div', 'cargo-row-main');
      main.append(el('div', 'name', other.title));
      main.append(el('div', 'sub',
        `${other.stops.filter((s) => s.done).length} of ${other.stops.length} stops`));
      row.append(main);

      row.tabIndex = 0;
      row.addEventListener('click', () => tripCall(`/api/trips/${other.id}/track`));
      body.append(row);
    }
  }
}

/* ---------- the plan on the map ---------- */

/**
 * Draws the tracked plan over the map: numbered stops in running order, joined
 * by the path between them.
 *
 * Numbers rather than colour, because the question is "where next", not "how
 * good": a plan has an order and the map has to show it. Stops the map cannot
 * place - somewhere with no engine id - are still listed in the panel, so a
 * plan is never silently short of a stop.
 */
function drawTripPath() {
  const map = $('#starmap');
  if (!map) return;

  map.querySelectorAll('.trip-layer').forEach((n) => n.remove());

  const trip = tracked();
  if (!trip) return;

  const points = trip.stops
    .map((stop, index) => ({ stop, index, at: nodeAt.get(stop.placeId) }))
    .filter((p) => p.at);

  if (!points.length) return;

  const layer = svgEl('g', { class: 'trip-layer' });
  const zoom = view.w / HOME_VIEW.w;
  const next = nextStop(trip);

  // The path first, so the numbers sit on top of it.
  for (let i = 1; i < points.length; i++) {
    const from = points[i - 1];
    const to = points[i];

    layer.append(svgEl('line', {
      x1: from.at.x, y1: from.at.y, x2: to.at.x, y2: to.at.y,
      class: `trip-leg${to.stop.done ? ' done' : ''}`,
      'stroke-width': 1.6 * zoom,
      'stroke-dasharray': `${6 * zoom} ${5 * zoom}`,
    }));
  }

  for (const point of points) {
    const badge = svgEl('g', {
      class: `trip-mark${point.stop.done ? ' done' : ''}${point.stop === next ? ' next' : ''}`,
    });

    badge.append(svgEl('circle', {
      cx: point.at.x, cy: point.at.y - 15 * zoom, r: 8 * zoom, 'stroke-width': 1.4 * zoom,
    }));

    const number = svgEl('text', {
      x: point.at.x, y: point.at.y - 15 * zoom, 'text-anchor': 'middle',
      'dominant-baseline': 'central', style: `font-size:${9 * zoom}px`,
    });

    number.textContent = point.stop.done ? '✓' : String(point.index + 1);
    badge.append(number);

    const title = svgEl('title');
    title.textContent = `Stop ${point.index + 1}: ${point.stop.place}`
      + (point.stop.note ? ` — ${point.stop.note}` : '');
    badge.append(title);

    badge.addEventListener('click', (e) => {
      e.stopPropagation();
      showTripPanel();
    });

    layer.append(badge);
  }

  map.append(layer);
}

/**
 * Opens the plan when the player lands on one of its stops.
 *
 * Arriving is the moment the stop's note matters - what was I here to buy? -
 * and the server has already crossed it off by the time this runs.
 */
let tripHere = null;

function tripArrived(locationId) {
  if (!locationId || locationId === tripHere) return;
  tripHere = locationId;

  const trip = tracked();
  if (!trip || !trip.stops.some((s) => s.placeId === locationId)) return;

  loadTrips().then(() => {
    cargo.trip = true;
    cargo.place = null;
    renderTripPanel();
  });
}

/**
 * The atlas place a UEX terminal name belongs to, or empty.
 *
 * Terminal names are not atlas names - "TDD, Area 18" against "Area18" - and
 * the server does that join once, from the atlas, so every page agrees on the
 * answer. Rows that carry a `placeId` are believed; this is only the fallback
 * for a terminal name arriving from somewhere that has not been given one.
 * A stop the map cannot place still belongs on the plan.
 */
const placeIdForTerminal = (terminal) =>
  atlas.find((l) => terminalMatchesPlace(terminal, l.name))?.rawId || '';

/**
 * Where a line on a list can be bought, cheapest first, cached for the session.
 *
 * The list's own `buyAt` is only ever the cheapest one UEX knows. That is a
 * fine default and a poor answer: the cheapest seller of a common good is
 * routinely three jumps out of the way, and the player is the only one who
 * knows what else the run has to fit around.
 *
 * A line is whatever the player wrote, so the server is asked rather than the
 * commodity market alone: "Agricium" is a trade good, "Bulwark" is a shield
 * bought from a shop counter, and a shopping list is allowed to hold both.
 */
const sellerCache = new Map();

async function sellersOf(item) {
  if (sellerCache.has(item.name)) return sellerCache.get(item.name);

  const fallback = item.buyAt
    ? [{ terminal: item.buyAt, placeId: placeIdForTerminal(item.buyAt), price: item.buyPrice || 0, scu: 0 }]
    : [];

  const found = await getJson(`/api/shopping/sellers?name=${encodeURIComponent(item.name)}`)
    .catch(() => null);

  const sellers = (found?.sellers ?? [])
    .slice(0, 12)
    .map((r) => ({
      terminal: r.terminal,
      placeId: r.placeId || placeIdForTerminal(r.terminal),
      place: r.place || '',
      system: r.system || '',
      security: r.security || 'unknown',
      price: r.price,
      scu: r.scu || 0,
      kind: r.kind,
    }));

  const answer = sellers.length ? sellers : fallback;
  sellerCache.set(item.name, answer);
  return answer;
}

/**
 * Stock is per terminal and the list says how much is wanted, so the two can
 * be compared: flying to the cheapest seller of 10 SCU to find 3 is a wasted
 * landing, and the page knew before you left.
 */
const shortStock = (seller, item) =>
  item.needed > 0 && seller.scu > 0 && seller.scu < item.needed;

/**
 * The seller a line starts on.
 *
 * A list written for a place is a decision already made, so a counter there
 * wins even when somewhere else is cheaper - that is the whole point of saying
 * where you are going. Otherwise: the cheapest that can actually fill the
 * order, else the cheapest.
 */
const defaultSeller = (sellers, item, destination) => {
  const there = destination && sellers.filter((seller) => atSameStop(seller, destination));

  if (there?.length)
    return there.find((seller) => !shortStock(seller, item)) ?? there[0];

  return sellers.find((seller) => !shortStock(seller, item)) ?? sellers[0];
};

/** Whether a seller stands at the place a list is pointed at. */
const atSameStop = (seller, destination) => {
  if (!destination) return false;

  if (destination.id && seller.placeId) return seller.placeId === destination.id;

  return !!destination.name && (
    terminalMatchesPlace(seller.terminal, destination.name)
    || (!!seller.place && terminalMatchesPlace(seller.place, destination.name)));
};

function sellerLabel(seller, item) {
  const stock = seller.scu
    ? ` · ${Math.round(seller.scu).toLocaleString()} SCU${shortStock(seller, item) ? ' — short' : ''}`
    : '';

  // Where it is and whether the law reaches it: the cheapest counter is
  // routinely the one in Pyro, and that is a decision, not a detail.
  const where = seller.system ? ` · ${seller.system}` : '';
  const risk = seller.security === 'lawless' ? ' — lawless' : '';

  return seller.price > 0
    ? `${seller.terminal} · ${money(seller.price)}${stock}${where}${risk}`
    : `${seller.terminal}${stock}${where}${risk}`;
}

/**
 * Turns what a list is still missing into a run, asking where to buy each thing.
 *
 * One stop per terminal rather than per item, because a trip is a sequence of
 * places: three things bought at Area18 is one landing. Anything with no known
 * seller is shown and left off rather than quietly dropped.
 */
async function planShoppingTrip(job, card) {
  card.querySelectorAll('.trip-chooser').forEach((n) => n.remove());

  const missing = job.items.filter((i) => !i.have);

  if (!missing.length) {
    alertLine($('#jobs-list'), 'Everything on this list is already in hand.');
    return;
  }

  // Where this list said it was for, when it said anything.
  const destination = job.destination
    ? { name: job.destination, id: job.destinationId || '' }
    : null;

  const chooser = el('div', 'trip-chooser');

  const head = el('div', 'chooser-head');
  head.append(el('span', null, destination
    ? `Where to buy — ${destination.name} first`
    : 'Where to buy — one stop per terminal'));
  chooser.append(head);

  const rows = el('div', 'chooser-rows');
  rows.append(el('div', 'muted', 'Looking up sellers…'));
  chooser.append(rows);
  card.append(chooser);

  const options = await Promise.all(missing.map(async (item) => ({
    item,
    sellers: await sellersOf(item),
  })));

  rows.textContent = '';

  // What the player picked, item name to terminal. Empty means "leave it off".
  const chosen = new Map();
  const foot = el('div', 'chooser-foot');
  const count = el('span', 'muted');

  const retally = () => {
    const stops = new Set([...chosen.values()].filter(Boolean));
    count.textContent = stops.size
      ? `${stops.size} stop${stops.size === 1 ? '' : 's'}`
      : 'nothing chosen';
  };

  /*
   * The same decision, seen from either end.
   *
   * By item is "where do I get this", which is how a list is written. By
   * location is "what is this landing worth", which is how a run is flown -
   * the point of a shopping list is to rotate around the map picking things
   * up, and that question cannot be answered one dropdown at a time. Both
   * write into `chosen`, so switching view never loses a choice, and the plan
   * is built from the one answer either of them wrote.
   */
  const stops = [];

  const renderItems = () => {
    rows.textContent = '';

    for (const { item, sellers } of options) {
      const row = el('div', 'chooser-row');

      const what = el('div', 'chooser-what');
      what.append(el('div', 'name', item.name));
      what.append(el('div', 'sub', item.needed > 0
        ? `${item.needed}${item.unit ? ` ${item.unit}` : ''} needed`
        : 'any amount'));
      row.append(what);

      if (!sellers.length) {
        row.append(el('div', 'chooser-none', 'no known seller — left off'));
        rows.append(row);
        continue;
      }

      const select = el('select', 'select');

      for (const seller of sellers) {
        const option = document.createElement('option');
        option.value = seller.terminal;
        option.textContent = sellerLabel(seller, item);
        select.append(option);
      }

      const skip = document.createElement('option');
      skip.value = '';
      skip.textContent = 'Leave this one off';
      select.append(skip);

      select.value = chosen.get(item.name) ?? defaultSeller(sellers, item, destination).terminal;
      chosen.set(item.name, select.value);

      select.addEventListener('change', () => {
        chosen.set(item.name, select.value);

        // A choice made here is the player's, so a ticked stop must not
        // silently overwrite it later.
        stops.length = 0;
        retally();
      });

      row.append(select);
      rows.append(row);
    }
  };

  const renderLocations = () => {
    rows.textContent = '';

    // Every terminal any of the list can be bought at, and what it would
    // supply. Ranked by how much of the list one landing covers, then by what
    // that landing costs.
    const counters = new Map();

    for (const { item, sellers } of options)
      for (const seller of sellers) {
        const counter = counters.get(seller.terminal) || { seller, supplies: [] };

        counter.supplies.push({ item, seller });
        counters.set(seller.terminal, counter);
      }

    const ranked = [...counters.values()]
      .map((counter) => ({
        ...counter,
        cost: counter.supplies.reduce((sum, s) =>
          sum + (s.seller.price > 0 ? s.seller.price * Math.max(1, s.item.needed) : 0), 0),
        chosen: atSameStop(counter.seller, destination),
      }))

      // The place the list is for leads, whatever it carries: you are landing
      // there regardless, so what it can supply is the first question.
      .sort((a, b) => Number(b.chosen) - Number(a.chosen)
        || b.supplies.length - a.supplies.length
        || a.cost - b.cost)
      .slice(0, 20);

    if (!ranked.length) {
      rows.append(el('div', 'muted', 'Nothing on this list has a known seller.'));
      return;
    }

    // The default plan buys each thing wherever it is cheapest, which is one
    // landing per thing. Fuel and time cost more than the difference, so the
    // panel offers the other answer outright.
    const fewest = el('div', 'chooser-actions');
    const pack = el('button', 'ghost', 'Fewest stops');
    pack.title = 'Cover the list with as few landings as possible';

    pack.addEventListener('click', () => {
      stops.length = 0;

      const left = new Set(options.filter((o) => o.sellers.length).map((o) => o.item.name));

      while (left.size) {
        // Most of what is still missing, and the cheapest of those.
        const best = ranked
          .map((counter) => ({
            counter,
            covers: counter.supplies.filter((s) => left.has(s.item.name)),
          }))
          .filter((c) => c.covers.length)
          .sort((a, b) => b.covers.length - a.covers.length
            || a.covers.reduce((sum, s) => sum + s.seller.price * Math.max(1, s.item.needed), 0)
             - b.covers.reduce((sum, s) => sum + s.seller.price * Math.max(1, s.item.needed), 0))[0];

        if (!best) break;

        stops.push(best.counter.seller.terminal);
        best.covers.forEach((s) => left.delete(s.item.name));
      }

      assignFromStops();
      renderLocations();
      retally();
    });

    fewest.append(pack);
    rows.append(fewest);

    for (const counter of ranked) {
      const row = el('div', 'chooser-stop');

      const tick = el('label', 'stop-tick');
      const box = document.createElement('input');
      box.type = 'checkbox';
      box.checked = stops.includes(counter.seller.terminal);

      box.addEventListener('change', () => {
        const already = stops.indexOf(counter.seller.terminal);

        if (box.checked && already < 0) stops.push(counter.seller.terminal);
        else if (!box.checked && already >= 0) stops.splice(already, 1);

        assignFromStops();
        renderLocations();
        retally();
      });

      tick.append(box);
      row.append(tick);

      const what = el('div', 'stop-what');
      const name = el('div', 'name');
      name.append(el('span', null, counter.seller.terminal));

      if (counter.seller.security === 'lawless')
        name.append(el('span', 'sec sec-lawless', 'lawless'));

      what.append(name);

      // What this landing is actually for: the things it can supply, greyed
      // where a stop ticked earlier already has them.
      const supplies = el('div', 'stop-items');

      for (const { item } of counter.supplies) {
        const taken = chosen.get(item.name);
        const mine = taken === counter.seller.terminal;

        supplies.append(el('span',
          mine ? 'stop-item mine' : taken ? 'stop-item taken' : 'stop-item',
          item.name));
      }

      what.append(supplies);
      row.append(what);

      const sum = el('div', 'stop-sum');
      sum.append(el('div', null, `${counter.supplies.length} of ${options.length}`));
      if (counter.cost > 0) sum.append(el('div', 'muted', money(counter.cost)));
      if (counter.seller.system) sum.append(el('div', 'muted', counter.seller.system));
      row.append(sum);

      rows.append(row);
    }
  };

  /** Ticked stops fill the list in the order they were ticked, first come. */
  const assignFromStops = () => {
    for (const { item } of options) chosen.set(item.name, '');

    for (const terminal of stops)
      for (const { item, sellers } of options)
        if (!chosen.get(item.name) && sellers.some((s) => s.terminal === terminal))
          chosen.set(item.name, terminal);
  };

  const views = el('div', 'seg chooser-views');

  for (const [key, label] of [['item', 'By item'], ['location', 'By location']]) {
    const button = el('button', key === 'item' ? 'active' : null, label);

    button.addEventListener('click', () => {
      views.querySelectorAll('button').forEach((b) => b.classList.remove('active'));
      button.classList.add('active');

      if (key === 'item') {
        renderItems();
        return;
      }

      // The plan arrives here already made - by item, or by the defaults -
      // so the ticks show it rather than starting blank and quietly
      // disagreeing with the count at the bottom of the panel.
      if (!stops.length)
        for (const terminal of chosen.values())
          if (terminal && !stops.includes(terminal)) stops.push(terminal);

      renderLocations();
    });

    views.append(button);
  }

  head.append(views);

  renderItems();
  retally();

  const create = el('button', 'ghost', 'Create plan');
  create.addEventListener('click', async () => {
    const byPlace = new Map();

    for (const { item, sellers } of options) {
      const terminal = chosen.get(item.name);
      if (!terminal) continue;

      const seller = sellers.find((s) => s.terminal === terminal);
      const need = item.needed > 0
        ? `${item.name} ${item.needed}${item.unit ? ` ${item.unit}` : ''}`
        : item.name;

      const line = byPlace.get(terminal)
        || { items: [], cost: 0, priced: true, placeId: seller?.placeId || '' };

      line.items.push(need);

      if (seller?.price > 0 && item.needed > 0) line.cost += seller.price * item.needed;
      else line.priced = false;

      byPlace.set(terminal, line);
    }

    if (!byPlace.size) {
      alertLine($('#jobs-list'), 'Nothing is chosen, so there is no run to fly.');
      return;
    }

    const stops = [...byPlace.entries()].map(([place, line]) => ({
      placeId: line.placeId || placeIdForTerminal(place),
      place,

      // The estimate is only shown when every item at the stop has a price;
      // a partial total reads as the cost of the landing and is not.
      note: line.items.join(', ') + (line.priced && line.cost > 0 ? ` · ~${money(line.cost)}` : ''),
    }));

    chooser.remove();
    await planTrip(`${job.title} run`, stops);
  });

  const cancel = el('button', 'ghost', 'Cancel');
  cancel.addEventListener('click', () => chooser.remove());

  foot.append(count);
  foot.append(el('span', 'spacer'));
  foot.append(create);
  foot.append(cancel);
  chooser.append(foot);
}

/** A one-line notice under a section, for when a button cannot do its job. */
function alertLine(host, text) {
  host.querySelectorAll('.trip-alert').forEach((n) => n.remove());

  const line = el('p', 'muted trip-alert', text);
  host.prepend(line);
  setTimeout(() => line.remove(), 6000);
}

/** What the hover tip currently shows, so pointermove does not rebuild it. */
let tipKey = null;

/** Appends a capped goods line to the tip when the Goods checkbox is on. */
function appendTipGoods(tip, names) {
  if (!names.length) return;

  const shown = names.slice(0, 6).join(', ');
  tip.append(el('span', 'goods', `Sells ${shown}${names.length > 6 ? '…' : ''}`));

  // A tooltip cannot hold a hundred names, so it says where they are rather
  // than trailing off into "+109 more" with no way to see them.
  if (names.length > 6)
    tip.append(el('span', 'goods hint', `click for all ${names.length}`));
}

/** The hover tooltip: name, kind and history at the cursor, instantly. */
function showMapTip(location) {
  const tip = $('#map-tip');
  if (!tip) return;
  tipKey = `site:${location.rawId}`;

  tip.textContent = '';
  tip.append(el('strong', null, location.name));
  tip.append(el('span', 'muted', location.kind.replace(/([a-z])([A-Z])/g, '$1 $2')));
  tip.append(el('span', null, location.visits > 0
    ? `${location.visits} visit${location.visits === 1 ? '' : 's'}`
    : 'never visited'));

  // In shaded commodity mode, the number behind this node's colour.
  const shade = nodeShade.get(location.rawId);
  if (shade && shadeScale) {
    tip.append(el('span', 'price',
      `${shadeScale.invert ? 'buy at' : shadeScale.unit === 'SCU' ? 'capacity' : 'sells at'} ` +
      `${Math.round(shade.value).toLocaleString()} ${shadeScale.unit}`));
  }

  if (highlightIds?.has(location.rawId) && mapPriceFreshness)
    tip.append(el('span', `price-age ${mapPriceFreshness.state}`, mapPriceFreshness.label));

  if ($('#map-goods').checked) appendTipGoods(tip, commoditiesSoldAt(location));

  const services = servicesAt(location);
  if (services.length)
    tip.append(el('span', 'service-tip', services
      .map((service) => `${SERVICE_META[service]?.icon || '•'} ${SERVICE_META[service]?.label || service}`)
      .join(' · ')));

  tip.hidden = false;
}

/**
 * The body-level tooltip, for hovering the space a planet's cluster occupies
 * rather than one of its dots: the rollup a pilot actually wants at a glance.
 * Goods are the union across the body's sites, cached per draw - the answer
 * does not change until the map does.
 */
const bodyGoodsCache = new Map();

function showBodyTip(bodyName, system, sites) {
  const tip = $('#map-tip');
  if (!tip) return;
  tipKey = `body:${system}/${bodyName}`;

  const visited = sites.filter((s) => s.visits > 0);
  const visits = sites.reduce((sum, s) => sum + s.visits, 0);
  const last = sites.reduce((max, s) =>
    (s.lastVisit && (!max || s.lastVisit > max) ? s.lastVisit : max), null);

  tip.textContent = '';
  tip.append(el('strong', null, bodyName));
  tip.append(el('span', 'muted', system));
  tip.append(el('span', null, `${sites.length} place${sites.length === 1 ? '' : 's'} · ${visited.length} visited`));

  if (visits > 0)
    tip.append(el('span', null, `${visits} visit${visits === 1 ? '' : 's'} · last ${relative(last)}`));

  const plan = sites.filter((site) => planPlaceIds().has(site.rawId)).length;
  const shopping = sites.filter((site) => mapShoppingIds.has(site.rawId)).length;
  const stash = sites.filter((site) => mapStashIds.has(site.rawId)).length;
  const work = [
    plan && `${plan} plan stop${plan === 1 ? '' : 's'}`,
    shopping && `${shopping} shopping place${shopping === 1 ? '' : 's'}`,
    stash && `${stash} stash place${stash === 1 ? '' : 's'}`,
  ].filter(Boolean);
  if (work.length) tip.append(el('span', 'service-tip', work.join(' · ')));

  const serviceCounts = new Map();
  for (const site of sites)
    for (const service of servicesAt(site))
      serviceCounts.set(service, (serviceCounts.get(service) || 0) + 1);
  if (serviceCounts.size) {
    const summary = [...serviceCounts.entries()]
      .map(([service, count]) => `${SERVICE_META[service]?.icon || '•'} ${count} ${SERVICE_META[service]?.label || service}`)
      .join(' · ');
    tip.append(el('span', 'service-tip', summary));
  }

  if ($('#map-goods').checked) {
    if (!bodyGoodsCache.has(tipKey)) {
      const union = new Set();
      for (const site of sites)
        for (const name of commoditiesSoldAt(site)) union.add(name);
      bodyGoodsCache.set(tipKey, [...union].sort());
    }

    appendTipGoods(tip, bodyGoodsCache.get(tipKey));
  }

  tip.hidden = false;
}

function moveMapTip(e) {
  const tip = $('#map-tip');
  if (!tip || tip.hidden) return;

  const wrap = tip.parentElement.getBoundingClientRect();
  let x = e.clientX - wrap.left + 16;
  let y = e.clientY - wrap.top + 12;

  // Flip to the other side of the cursor rather than clipping at the frame.
  if (x + tip.offsetWidth > wrap.width - 8) x = e.clientX - wrap.left - tip.offsetWidth - 12;
  if (y + tip.offsetHeight > wrap.height - 8) y = e.clientY - wrap.top - tip.offsetHeight - 10;

  tip.style.left = `${x}px`;
  tip.style.top = `${y}px`;
}

function hideMapTip() {
  const tip = $('#map-tip');
  if (tip) tip.hidden = true;
  tipKey = null;
}

/**
 * Every commodity the catalogue says this place sells, via the same facility
 * tokens the map search uses - so the card and the search never disagree.
 * Token sets are cached per entry; the catalogue does not change under us.
 */
function commoditiesSoldAt(location) {
  const compact = `${location.name} ${location.rawId}`.toLowerCase().replace(/[^a-z0-9]/g, '');

  return marketEntries
    .filter((e) => {
      if (!e.sold.length) return false;
      e.soldTokens ??= facilityTokens(e.sold);
      return [...e.soldTokens].some((token) => compact.includes(token));
    })
    .map((e) => e.name)
    .sort();
}

/** Personal POIs are local notes, never an assertion about the map's telemetry. */
async function loadMapNotes() {
  mapNotes = await getJson('/api/map-notes');
  mapNoteIds.clear();
  for (const note of mapNotes) if (note.placeId) mapNoteIds.add(note.placeId);
  if (mapInfoLocation) renderMapInfoNotes(mapInfoLocation);
  if (atlas.length) drawMap();
}

async function saveMapNote(body) {
  const response = await fetch('/api/map-notes', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!response.ok) throw new Error('Could not save note');
  await loadMapNotes();
}

async function removeMapNote(id) {
  await fetch(`/api/map-notes/${id}`, { method: 'DELETE' });
  await loadMapNotes();
}

function renderMapInfoNotes(location) {
  const host = $('#map-info-notes');
  if (!host) return;
  host.textContent = '';

  const notes = mapNotes.filter((note) => note.placeId === location.rawId);
  host.append(el('div', 'map-note-heading', notes.length ? `Your notes · ${notes.length}` : 'Personal map note'));

  for (const note of notes) {
    const card = el('div', 'map-note');
    const top = el('div', 'map-note-top');
    top.append(el('b', null, note.title));
    const remove = el('button', 'ghost tiny', '×');
    remove.title = 'Remove this map note';
    remove.addEventListener('click', () => removeMapNote(note.id));
    top.append(remove);
    card.append(top);
    if (note.note) card.append(el('div', 'map-note-text', note.note));
    if (note.tags?.length) card.append(el('div', 'map-note-tags', note.tags.join(' · ')));
    card.append(el('div', 'map-note-age', `written ${relative(note.updatedAt)}`));
    host.append(card);
  }

  const form = el('div', 'map-note-form');
  const title = document.createElement('input');
  title.type = 'text';
  title.className = 'search';
  title.placeholder = 'Title, e.g. cargo entrance';
  const detail = document.createElement('input');
  detail.type = 'text';
  detail.className = 'search';
  detail.placeholder = 'Optional note';
  const tags = document.createElement('input');
  tags.type = 'text';
  tags.className = 'search';
  tags.placeholder = 'Tags, comma separated';
  const save = el('button', 'ghost tiny', 'Save note');
  save.addEventListener('click', async () => {
    save.disabled = true;
    try {
      await saveMapNote({
        placeId: location.rawId,
        place: location.name,
        title: title.value,
        note: detail.value || null,
        tags: tags.value.split(',').map((tag) => tag.trim()).filter(Boolean),
      });
    } finally {
      save.disabled = false;
    }
  });
  form.append(title, detail, tags, save);
  host.append(form);
}

/** The place the detail card currently shows, for re-rendering on toggle. */
let mapInfoLocation = null;

/** Fills the card's sold-here list, honouring the toolbar's Goods checkbox. */
function renderMapInfoSold() {
  const list = $('#map-info-sold');
  const wanted = $('#map-goods').checked;

  if (!wanted || !mapInfoLocation) {
    list.hidden = true;
    return;
  }

  list.textContent = '';

  if (!marketEntries.length) {
    list.append(el('span', 'muted', 'Needs the community dataset (Settings).'));
  } else {
    const names = commoditiesSoldAt(mapInfoLocation);

    if (!names.length) {
      list.append(el('span', 'muted', 'Nothing known to sell here.'));
    } else {
      // The count matters when there are a hundred: the tooltip promised them
      // all, and this is where they are.
      list.append(el('div', 'sold-count muted', `${names.length} sold here · click one to light its sellers`));

      for (const name of names) {
        const chip = el('button', 'sold-chip', name);
        chip.type = 'button';
        chip.title = 'Light every place that sells this';
        chip.addEventListener('click', () => {
          $('#map-search').value = name;
          drawMap();
        });
        list.append(chip);
      }
    }
  }

  list.hidden = false;
}

/** The card that opens when a node is clicked. */
function showMapInfo(location) {
  mapInfoLocation = location;
  const info = $('#map-info');

  $('#map-info-name').textContent = location.name;
  $('#map-info-where').textContent =
    [location.body, location.system].filter(Boolean).join(' · ') || 'unmapped';
  $('#map-info-kind').textContent = location.kind.replace(/([a-z])([A-Z])/g, '$1 $2');

  $('#map-info-visits').textContent = location.visits > 0
    ? `${location.visits} visit${location.visits === 1 ? '' : 's'}`
    : 'never visited';

  $('#map-info-last').textContent = location.lastVisit
    ? `last there ${relative(location.lastVisit)}`
    : '';

  renderMapInfoServices(location);
  renderMapInfoNotes(location);

  // In commodity mode, say which side of the search this place is on.
  const trade = $('#map-info-trade');
  if (highlightIds) {
    const sells = highlightIds.has(location.rawId);
    trade.textContent = sells ? 'sells the searched commodity' : 'does not sell it';
    trade.className = sells ? 'inward' : 'muted';
    trade.hidden = false;
  } else {
    trade.hidden = true;
  }

  renderMapInfoSold();

  // The starmap's own paragraph about this place, fetched on first open and
  // cached; the card must not wait for it.
  const loreNode = $('#map-info-lore');
  loreNode.hidden = true;

  if (!loreCache.has(location.name)) {
    loreCache.set(location.name,
      getJson(`/api/map/lore?name=${encodeURIComponent(location.name)}`)
        .then((r) => r.lore)
        .catch(() => null));
  }

  loreCache.get(location.name).then((lore) => {
    if (lore && mapInfoLocation === location) {
      loreNode.textContent = lore;
      loreNode.hidden = false;
    }
  });

  info.hidden = false;
}

/** Service facts on a place card use the same map-id join as the filter. */
function renderMapInfoServices(location) {
  const host = $('#map-info-services');
  const services = servicesAt(location);
  host.textContent = '';

  for (const service of services) {
    const meta = SERVICE_META[service];
    if (!meta) continue;
    const chip = el('button', 'map-service-chip');
    chip.type = 'button';
    chip.title = `Filter the map to ${meta.label.toLowerCase()}`;
    chip.append(el('span', 'service-icon', meta.icon));
    chip.append(el('span', 'service-text', meta.label));
    chip.addEventListener('click', () => selectMapService(service));
    host.append(chip);
  }

  host.hidden = host.children.length === 0;
}

/** Lore paragraphs already asked for, name to promise of text-or-null. */
const loreCache = new Map();

/**
 * Systems have no shared coordinate frame in the installed game data. Showing
 * only the jump graph makes that limitation legible instead of turning an
 * arbitrary triangle into a false atlas of planetary distances.
 */
function drawJumpNetwork(map, locations) {
  const systems = Object.keys(SYSTEM_COLOURS)
    .filter((system) => locations.some((location) => location.system === system));
  const home = { x: 0, y: 0, w: 1200, h: 760 };
  const wasHome = view.w === HOME_VIEW.w && view.h === HOME_VIEW.h
    && view.x === HOME_VIEW.x && view.y === HOME_VIEW.y;

  HOME_VIEW = home;
  if (wasHome) view = { ...home };
  SYSTEM_LAYOUT = {};

  const anchors = {
    Stanton: { x: 255, y: 240 },
    Pyro: { x: 945, y: 240 },
    Nyx: { x: 600, y: 560 },
  };

  for (const system of systems) {
    const point = anchors[system] || { x: 600, y: 380 };
    SYSTEM_LAYOUT[system] = { ...point, radius: 46, colour: SYSTEM_COLOURS[system] };
  }

  for (const [fromName, toName] of JUMP_LANES) {
    const from = SYSTEM_LAYOUT[fromName];
    const to = SYSTEM_LAYOUT[toName];
    if (!from || !to) continue;

    map.append(svgEl('line', {
      x1: from.x, y1: from.y, x2: to.x, y2: to.y,
      class: 'map-edge', 'stroke-width': '2.2', 'stroke-dasharray': '7 8', filter: 'url(#glow)',
    }));
  }

  for (const [system, point] of Object.entries(SYSTEM_LAYOUT)) {
    const group = svgEl('g', { class: 'map-network-system', tabindex: '0' });
    group.append(svgEl('circle', {
      cx: point.x, cy: point.y, r: point.radius, fill: point.colour,
      'fill-opacity': '.13', stroke: point.colour, 'stroke-width': '2', filter: 'url(#glow)',
    }));
    group.append(svgEl('circle', { cx: point.x, cy: point.y, r: 9, fill: point.colour, filter: 'url(#glow)' }));
    const label = svgEl('text', {
      x: point.x, y: point.y + 72, 'text-anchor': 'middle', class: 'map-sys-label',
      style: `font-size:${labelSize(1.6)}px`,
    });
    label.textContent = system;
    group.append(label);
    group.addEventListener('click', () => {
      $('#map-mode').value = 'system';
      $('#map-system').value = system;
      syncMapModeControls();
      drawMap();
    });
    map.append(group);
  }

  const title = svgEl('text', {
    x: home.w / 2, y: 92, 'text-anchor': 'middle', class: 'map-label',
    style: `font-size:${labelSize(1.35)}px;fill:#7796b0;letter-spacing:.18em`,
  });
  title.textContent = 'JUMP NETWORK · SCHEMATIC · NOT TO SCALE';
  map.append(title);

  $('#map-count').textContent = `${systems.length} systems · jump network`;
  drawNetworkHere(map);
  updateHereControl();
  drawLegend([]);
  applyView();
}

function drawMap() {
  const map = $('#starmap');
  map.textContent = '';
  nodeAt.clear();
  hideMapTip();
  pendingLabels.length = 0;
  claimedBoxes.length = 0;
  bodyGoodsCache.clear();

  const visitedOnly = $('#map-visited-only')?.checked;
  const term = ($('#map-search')?.value || '').trim().toLowerCase();
  const focusIds = mapFocusIds();
  const serviceIds = mapServiceFilter
    ? new Set(atlas.filter((location) => servicesAt(location).includes(mapServiceFilter))
      .map((location) => location.rawId))
    : null;

  // Any search HIGHLIGHTS rather than filters. Filtering removed the context:
  // the remaining nodes re-clustered into what looked like the whole map with
  // nothing marked, and there was no way to see WHERE the matches sat among
  // everything else. Keeping every node, dimming the rest and framing the lit
  // ones reads as an answer. A term that names a commodity lights where it
  // sells; any other term lights the places whose names match. Either search
  // wins over the visited-only toggle.
  const sites = term ? commoditySites(term) : null;

  highlightIds = null;

  if (sites) {
    highlightIds = new Set(atlas
      .filter((l) => {
        const compact = `${l.name} ${l.rawId}`.toLowerCase().replace(/[^a-z0-9]/g, '');
        return [...sites].some((token) => compact.includes(token));
      })
      .map((l) => l.rawId));
  } else if (term) {
    highlightIds = new Set(atlas
      .filter((l) => l.name.toLowerCase().includes(term) || l.rawId.toLowerCase().includes(term))
      .map((l) => l.rawId));
  }

  // An empty highlight set would dim the whole map to say "nothing"; saying it
  // in the counter and leaving the map lit is kinder.
  if (highlightIds && highlightIds.size === 0) highlightIds = null;

  prepareShading(term, sites);
  syncCargoPanel(term, sites);

  const allLocations = atlas.filter((l) =>
    // Service and visit filters answer a different question from position. The
    // player never vanishes merely because their current place lacks the
    // selected service or has not been recorded as a visit yet.
    l.rawId === hereId ||
    (!focusIds || focusIds.has(l.rawId)) &&
    (!serviceIds || serviceIds.has(l.rawId)) &&
    (term || !visitedOnly || l.visits > 0));
  const selectedSystem = mapSystem();
  const locations = mapMode() === 'system' && selectedSystem
    ? allLocations.filter((location) => location.system === selectedSystem)
    : allLocations;

  const count = $('#map-count');
  if (count) {
    const seen = atlas.filter((l) => l.visits > 0).length;
    if (mapServiceFilter && serviceIds?.size === 0)
      count.textContent = `no ${SERVICE_META[mapServiceFilter]?.label.toLowerCase() || 'service'} locations known`;
    else if (term && !highlightIds) count.textContent = 'no match';
    else if (sites) {
      // Name what was matched: the term may have been a fragment, and the
      // user should see which commodity the map decided they meant.
      const matched = matchCommodity(term.startsWith('buy:') ? term.slice(4) : term);
      const what = matched ? matched.name : term;

      count.textContent = term.startsWith('buy:')
        ? `${what} — stocked at ${highlightIds.size} places the map can name`
        : `${what} — sells at ${highlightIds.size} places the map can name`;
    }
    else if (term) count.textContent = `${highlightIds.size} place${highlightIds.size === 1 ? '' : 's'} lit`;
    else if (mapFocusFilter && focusIds?.size === 0)
      count.textContent = `no ${mapFocusFilter} locations can be placed yet`;
    else if (mapFocusFilter)
      count.textContent = `${locations.length} ${mapFocusFilter} location${locations.length === 1 ? '' : 's'} shown`;
    else if (mapServiceFilter)
      count.textContent = `${locations.length} ${SERVICE_META[mapServiceFilter]?.label.toLowerCase() || 'service'} location${locations.length === 1 ? '' : 's'} shown`;
    else if (mapMode() === 'system')
      count.textContent = `${locations.length} shown in ${selectedSystem} · ${seen} of ${atlas.length} visited`;
    else count.textContent = `${locations.length} shown · ${seen} of ${atlas.length} visited`;
  }

  // Soft glow, applied to stars and jump lanes for the HUD look.
  const defs = svgEl('defs');
  const glow = svgEl('filter', { id: 'glow', x: '-60%', y: '-60%', width: '220%', height: '220%' });
  glow.append(svgEl('feGaussianBlur', { stdDeviation: '3.4', result: 'blur' }));
  const merge = svgEl('feMerge');
  merge.append(svgEl('feMergeNode', { in: 'blur' }));
  merge.append(svgEl('feMergeNode', { in: 'SourceGraphic' }));
  glow.append(merge);
  defs.append(glow);
  map.append(defs);

  if (mapMode() === 'network') {
    drawJumpNetwork(map, allLocations);
    return;
  }

  // A sparse starfield behind everything. Deterministic - a hash of the index,
  // not Math.random - so redraws do not make the sky shimmer.
  for (let i = 0; i < 140; i++) {
    const h1 = Math.abs(Math.sin(i * 127.1 + 311.7) * 43758.5453) % 1;
    const h2 = Math.abs(Math.sin(i * 269.5 + 183.3) * 28001.8384) % 1;
    const h3 = Math.abs(Math.sin(i * 419.2 + 371.9) * 12345.6789) % 1;

    map.append(svgEl('circle', {
      cx: (h1 * 1340).toFixed(1),
      cy: (h2 * 1240).toFixed(1),
      r: (0.5 + h3 * 0.9).toFixed(2),
      class: 'map-star',
      style: `opacity:${(0.10 + h3 * 0.3).toFixed(2)}`,
    }));
  }

  const grouped = { other: [] };

  for (const location of locations) {
    if (location.system && SYSTEM_COLOURS[location.system]) {
      grouped[location.system] ??= new Map();

      const key = location.body || '—';
      if (!grouped[location.system].has(key)) grouped[location.system].set(key, []);
      grouped[location.system].get(key).push(location);
    } else {
      grouped.other.push(location);
    }
  }

  // The layout depends on what is on screen, so it is worked out per draw -
  // filtering to visited places shrinks every system.
  const sized = layoutSystems(grouped);
  SYSTEM_LAYOUT = sized.layout;

  const home = { x: 0, y: 0, w: sized.width, h: sized.height };
  const wasHome = view.w === HOME_VIEW.w && view.h === HOME_VIEW.h
    && view.x === HOME_VIEW.x && view.y === HOME_VIEW.y;

  HOME_VIEW = home;
  if (wasHome) view = { ...home };

  const maxVisits = Math.max(1, ...locations.map((l) => l.visits));
  // Visits still nudge the size, but gently. At four times the range a busy
  // outpost dwarfed a station and the map read as a jumble of sizes rather
  // than a set of places; the count lives in the tip and on the Places page,
  // where a number can be a number.
  const radiusFor = (visits) => 7 + Math.sqrt(visits / maxVisits) * 4;


  // Stars, orbit rings and system labels.
  for (const [system, centre] of Object.entries(SYSTEM_LAYOUT)) {
    map.append(svgEl('circle', {
      cx: centre.x, cy: centre.y, r: 9, fill: centre.colour, filter: 'url(#glow)',
    }));
    map.append(svgEl('circle', { cx: centre.x, cy: centre.y, r: centre.orbit, class: 'map-orbit' }));

    // Reticle ticks around each star, echoing the in-game starmap.
    for (let tick = 0; tick < 4; tick++) {
      const angle = (tick / 4) * Math.PI * 2 + Math.PI / 4;
      map.append(svgEl('line', {
        x1: centre.x + Math.cos(angle) * 22, y1: centre.y + Math.sin(angle) * 22,
        x2: centre.x + Math.cos(angle) * 31, y2: centre.y + Math.sin(angle) * 31,
        stroke: '#1e4763', 'stroke-width': '1',
      }));
    }

    const label = svgEl('text', {
      x: centre.x, y: centre.y + centre.radius - 6, 'text-anchor': 'middle',
      class: 'map-sys-label', style: `font-size:${labelSize(1.5)}px`,
    });
    label.textContent = system;
    map.append(label);
  }

  // Jump lanes, drawn star to star and stopping short of each orbit ring.
  for (const [fromName, toName] of JUMP_LANES) {
    const from = SYSTEM_LAYOUT[fromName];
    const to = SYSTEM_LAYOUT[toName];
    if (!from || !to) continue;

    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const length = Math.hypot(dx, dy) || 1;
    const ux = dx / length;
    const uy = dy / length;

    const ax = from.x + ux * (from.radius + 10);
    const ay = from.y + uy * (from.radius + 10);
    const bx = to.x - ux * (to.radius + 10);
    const by = to.y - uy * (to.radius + 10);

    // Bowed away from the midpoint so the three lanes stay distinguishable.
    const mx = (ax + bx) / 2 - uy * 26;
    const my = (ay + by) / 2 + ux * 26;

    map.append(svgEl('path', {
      d: `M ${ax} ${ay} Q ${mx} ${my} ${bx} ${by}`,
      class: 'map-edge', 'stroke-width': '1.5', 'stroke-dasharray': '5 7', filter: 'url(#glow)',
    }));

    map.append(svgEl('rect', {
      x: mx - 6, y: my - 6, width: 12, height: 12,
      fill: 'none', stroke: '#35c8f0', 'stroke-width': '1.2',
      transform: `rotate(45 ${mx} ${my})`, filter: 'url(#glow)',
    }));

    const jumpLabel = svgEl('text', {
      x: mx, y: my - 14, 'text-anchor': 'middle', class: 'map-label',
      style: `font-size:${labelSize()}px`,
    });
    jumpLabel.textContent = 'JUMP POINT';
    map.append(jumpLabel);
  }

  // Every body's hover disc goes in one layer, added before any node is
  // drawn. Appending them as each body was built put a later body's disc on
  // top of an earlier body's dots - and in SVG the thing on top takes the
  // pointer, so clicking those dots did nothing at all. Widening the clusters
  // made the discs bigger and the dead zones with them.
  const hoverLayer = svgEl('g');

  // The bodies go in a layer of their own, under everything, for the same
  // reason: drawn as each cluster was built, a later planet's disc painted over
  // an earlier one's marks. It cannot steal a click - the disc is
  // pointer-events: none - but it could still hide the places on a neighbour.
  const bodyLayer = svgEl('g');
  map.append(bodyLayer);
  map.append(hoverLayer);

  // Bodies and their locations.
  for (const [system, bodies] of Object.entries(grouped)) {
    if (system === 'other') continue;

    const centre = SYSTEM_LAYOUT[system];
    const order = SYSTEMS[system] || [];
    const present = [...bodies.keys()].sort((a, b) => {
      const ia = order.indexOf(a);
      const ib = order.indexOf(b);
      return (ia === -1 ? 99 : ia) - (ib === -1 ? 99 : ib);
    });

    const layout = bodyLayout(system, present, centre,
      (name) => clusterRadius(bodies.get(name).length));

    // Faint orbit rings through each planet, drawn under the nodes. They give
    // the disc its structure - the eye reads rings as orbits and the layout
    // stops looking like scattered dots.
    const ringRadii = new Set();

    for (const placement of layout.values()) {
      if (placement.from.x !== centre.x || placement.from.y !== centre.y) continue;
      const r = Math.round(Math.hypot(placement.x - centre.x, placement.y - centre.y));
      if (r > 12) ringRadii.add(r);
    }

    for (const r of ringRadii)
      map.append(svgEl('circle', { cx: centre.x, cy: centre.y, r, class: 'map-orbit' }));

    present.forEach((bodyName) => {
      const place = layout.get(bodyName);
      const angle = place.angle;
      const bx = place.x;
      const by = place.y;

      map.append(svgEl('line', {
        x1: place.from.x, y1: place.from.y, x2: bx, y2: by,
        stroke: 'rgba(53,200,240,.13)', 'stroke-width': '1',
      }));

      const sites = bodies.get(bodyName);
      const reach = clusterRadius(sites.length);

      // Before the sites, so their marks sit on the world rather than behind it.
      if (bodyName !== '—') drawBodyDisc(bodyLayer, bx, by, reach, system, `${system}/${bodyName}`);

      // Body names sit outside the cluster they head, so the sites below have
      // clear air to put their own labels in. They are placed first and claim
      // their box, so site labels flow around them.
      const bodyLabelX = bx + Math.cos(angle) * (reach + 16);
      const bodyLabelY = by + Math.sin(angle) * (reach + 16);
      const bodyLabelSize = labelSize(1.15);

      const bodyLabel = svgEl('text', {
        x: bodyLabelX, y: bodyLabelY,
        'text-anchor': 'middle', class: 'map-label',
        style: `fill:${!place.positioned && mapMode() === 'system' ? '#ffab3d' : '#7796b0'};font-size:${bodyLabelSize}px;letter-spacing:.14em;text-transform:uppercase`,
      });
      bodyLabel.textContent = bodyName === '—' ? ''
        : `${bodyName}${!place.positioned && mapMode() === 'system' ? ' · position unavailable' : ''}`;
      map.append(bodyLabel);

      if (bodyLabel.textContent) {
        claimedBoxes.push(labelBox(
          bodyLabelX, bodyLabelY - bodyLabelSize * 0.4, 'middle', bodyLabelSize * 1.25, bodyLabel.textContent));
      }

      // An invisible disc under the cluster: hovering the space a planet
      // occupies - rather than one of its dots, which sit on top and win the
      // pointer - shows the body's rollup tip.
      if (bodyName !== '—') {
        const bodyKey = `body:${system}/${bodyName}`;
        const bodyHover = svgEl('circle', {
          cx: bx, cy: by, r: reach + 12,
          fill: '#000', 'fill-opacity': '0', 'pointer-events': 'fill',
        });

        const bubble = () => map.querySelector(`.map-body[data-body="${system}/${bodyName}"]`);

        bodyHover.addEventListener('pointermove', (e) => {
          if (tipKey !== bodyKey) {
            showBodyTip(bodyName, system, sites);

            // Lit only while the pointer is in it: at rest the bubble is a
            // ground the eye can ignore, and under the pointer it is the
            // answer to "which of these belong together".
            bubble()?.classList.add('lit');
          }

          moveMapTip(e);
        });

        bodyHover.addEventListener('pointerleave', () => {
          bubble()?.classList.remove('lit');
          hideMapTip();
        });

        // Clicking the space between a body's places frames that body. The
        // dots themselves keep their own click - this is only the gaps, which
        // did nothing at all before.
        bodyHover.addEventListener('click', (e) => {
          e.stopPropagation();
          animateViewTo({
            x: bx - (reach + 26),
            y: by - (reach + 26),
            w: (reach + 26) * 2,
            h: (reach + 26) * 2,
          });
        });

        hoverLayer.append(bodyHover);
      }

      // Sites are spread by golden angle rather than in rings. Rings of a fixed
      // size put every twelfth node on the same spoke, which reads as spokes
      // rather than a cluster and stacks the labels on top of each other;
      // phyllotaxis fills the disc evenly at any count, and microTech alone has
      // over a hundred.
      // Every position first, then each node is told how much room it has.
      // A cluster is drawn tighter than the pad that makes a lone dot easy to
      // click, so without this the last site drawn owns every point it covers
      // and its neighbours cannot be clicked at all.
      const spots = sites.map((site, siteIndex) => {
        const spin = siteIndex * 2.39996;
        const distance = clusterRadius(siteIndex + 1);

        return { x: bx + Math.cos(spin) * distance, y: by + Math.sin(spin) * distance };
      });

      sites.forEach((site, siteIndex) => {
        const spot = spots[siteIndex];

        let nearest = Infinity;
        spots.forEach((other, j) => {
          if (j !== siteIndex) nearest = Math.min(nearest, Math.hypot(other.x - spot.x, other.y - spot.y));
        });

        drawNode(map, spot.x, spot.y, site, radiusFor(site.visits), { x: bx, y: by }, nearest / 2);
      });
    });
  }

  // Anything the resolver could not place: shown below the systems, never
  // dropped, since an unmapped place the player has been is still worth seeing.
  const perRow = Math.max(6, Math.floor(sized.width / 150));

  grouped.other.forEach((location, index) => {
    const x = 70 + (index % perRow) * 150;
    const y = sized.height + 40 + Math.floor(index / perRow) * 34;
    drawNode(map, x, y, location, radiusFor(location.visits));
  });

  if (grouped.other.length) {
    HOME_VIEW.h = sized.height + 80 + Math.ceil(grouped.other.length / perRow) * 34;
    if (wasHome) view.h = HOME_VIEW.h;
  }

  placeLabels(map);
  drawLegend(locations);
  applyView();
  drawHere();
  drawTravel();
  drawTripPath();
  fitToHighlights(term || null);
}

/* ---------- what a place looks like ---------- */

/**
 * A mark per kind, drawn in a box from -1 to 1 so size stays the caller's
 * business.
 *
 * Colour alone was carrying the whole taxonomy: nine kinds, nine dots, and a
 * legend to memorise. A shape can be read without the legend - a headframe is a
 * mine whether or not you remember that mines are brown - and the colour stays
 * exactly as it was, so anyone who had learnt it loses nothing.
 *
 * Deliberately blunt geometry. These are drawn between four and seventeen
 * pixels across, where a detailed glyph turns to mush; a silhouette that
 * survives being tiny beats one that looks good in a design tool.
 */
const KIND_SHAPES = {
  // A skyline. Two steps rather than three: at eight pixels a third is a smudge.
  City: [{ tag: 'path', attrs: { d: 'M-1 .9 L-1 -.15 L-.05 -.15 L-.05 -1 L1 -1 L1 .9 Z' } }],

  // A ring - the one shape that reads as "you dock inside it".
  Station: [{ tag: 'path', attrs: { d: 'M0 -1 A 1 1 0 1 1 0 1 A 1 1 0 1 1 0 -1 Z M0 -.42 A .42 .42 0 1 0 0 .42 A .42 .42 0 1 0 0 -.42 Z' }, evenodd: 1 }],

  // A horizontal berth: it stays distinct from an asteroid's uneven rock
  // silhouette even when a map icon is only a handful of pixels across.
  RestStop: [{ tag: 'rect', attrs: { x: -1, y: -.62, width: 2, height: 1.24, rx: .34 } }],

  // A dome on the ground.
  Outpost: [{ tag: 'path', attrs: { d: 'M-1 .55 A 1 1 0 0 1 1 .55 L1 .8 L-1 .8 Z' } }],

  // A spoil heap. Nothing on top of it - the headframe it used to carry turned
  // to mush at map size, which is the size it is always drawn at.
  Mine: [{ tag: 'polygon', attrs: { points: '0,-1 1,.85 -1,.85' } }],

  // An uneven rock: the lopsided outline is intentional, otherwise it reads
  // too much like a rest-stop berth at a glance.
  Asteroid: [{ tag: 'polygon', attrs: { points: '-.72,-.92 .5,-.72 1,.02 .42,.9 -.74,.62 -1,-.18' } }],

  // A cross: legible at any size, and nothing else on the map is one.
  Research: [{ tag: 'path', attrs: { d: 'M-.32 -1 L.32 -1 L.32 -.32 L1 -.32 L1 .32 L.32 .32 L.32 1 L-.32 1 L-.32 .32 L-1 .32 L-1 -.32 L-.32 -.32 Z' } }],

  // Cargo moving: an arrow, not a crate with a band nobody could see.
  DistributionCentre: [{ tag: 'polygon', attrs: { points: '-1,-.85 .95,0 -1,.85 -1,.3 -.15,0 -1,-.3' } }],

  // The same diamond the jump lanes wear.
  JumpPoint: [{ tag: 'polygon', attrs: { points: '0,-1 1,0 0,1 -1,0' } }],

  MissionBeacon: [{ tag: 'polygon', attrs: { points: '0,1 -1,-.85 0,-.3 1,-.85' } }],
};

/** Anything without a mark of its own keeps the dot it always had. */
const PLAIN_MARK = [{ tag: 'circle', attrs: { cx: 0, cy: 0, r: 1 } }];

/**
 * Draws a place's mark at a size.
 *
 * @param solid Somewhere with history is filled; somewhere never visited is an
 *   outline, exactly as when every kind was a circle.
 */
/**
 * Equal radius is not equal weight: a triangle inside a circle covers under
 * half of it, so the same number drew a mine that looked half the size of a
 * rest stop beside it. Each shape is nudged until they read as one set.
 */
const SHAPE_WEIGHT = {
  City: 0.92,
  Station: 1,
  RestStop: 0.95,
  Outpost: 1.05,
  Mine: 1.18,
  Asteroid: 1,
  Research: 1.06,
  DistributionCentre: 1.12,
  JumpPoint: 1.15,
  MissionBeacon: 1.15,
};

function kindMark(kind, x, y, radius, colour, solid) {
  const size = radius * (SHAPE_WEIGHT[kind] ?? 1);

  const group = svgEl('g', {
    class: 'map-mark',
    transform: `translate(${x} ${y}) scale(${size})`,
  });

  for (const part of KIND_SHAPES[kind] ?? PLAIN_MARK) {
    group.append(svgEl(part.tag, {
      ...part.attrs,

      // Line parts are strokes whatever the history: a filled orbit or shaft is
      // a blob. Everything else fills once the place has been visited.
      fill: solid ? colour : 'none',
      stroke: colour,

      // Heavy enough to survive being drawn six pixels across, which is the
      // size these are actually used at; an outline at .14 disappeared.
      'stroke-width': 0.22,
      'stroke-linejoin': 'round',
      'fill-rule': part.evenodd ? 'evenodd' : 'nonzero',
      opacity: solid ? 0.92 : 0.6,
    }));
  }

  return group;
}

/**
 * The body a cluster belongs to, drawn as the disc its places sit on.
 *
 * Planets and moons are not in the atlas at all - the game names locations, not
 * the rocks they are on - so a body was a label and nothing else, and a station
 * ended up looking larger than the planet holding it. Drawing the disc puts the
 * hierarchy back: the big quiet circle is the world, the marks on it are the
 * places you can actually go.
 */
/**
 * The bubble a body's places sit in.
 *
 * The layout is already polar - each site is placed by golden angle around its
 * body - but a ring of dots does not say "these belong to Hurston" on its own.
 * The disc does, and it is deliberately faint: it is a ground, not a thing to
 * look at. Hovering brings it up, which is when the grouping is the question
 * being asked.
 *
 * @param key Identifies the bubble so the hover area over the same body can
 *   light it without either having to know where the other one is drawn.
 */
function drawBodyDisc(layer, x, y, reach, system, key) {
  const disc = svgEl('circle', {
    cx: x, cy: y, r: reach + 7,
    class: 'map-body',
    fill: SYSTEM_COLOURS[system] || '#9fb8ff',
  });

  if (key) disc.dataset.body = key;

  layer.append(disc);
}

/**
 * @param anchor The body this site belongs to, if any. Labels are pushed away
 *   from it so a cluster fans its names outwards instead of stacking them.
 */
/**
 * How far a mark's invisible click target reaches.
 *
 * A small mark needs a pad around it to be clickable at all, but the pad must
 * stop inside the neighbour's half of the gap: SVG gives a shared point to
 * whatever was drawn last, so an overlapping pad does not make a click
 * ambiguous - it makes the covered node unclickable. In a cluster that was
 * most of them, which is why clicking a station, opening its trade panel and
 * adding it to a plan all failed on the same places. Never below the mark
 * itself, which is always its own target.
 */
const hitPad = (radius, room) => Math.max(radius + 1, Math.min(radius + 8, room));

/**
 * @param room Half the distance to the nearest neighbour, when there is one.
 *   The click pad stops there rather than reaching across it.
 */
function drawNode(map, x, y, location, radius, anchor = null, room = Infinity) {

  const colour = KIND_COLOURS[location.kind] || KIND_COLOURS.Unknown;
  const been = location.visits > 0;

  let cls = been ? 'map-node' : 'map-node unvisited';

  // In commodity mode the sellers glow and everything else recedes.
  const highlighted = highlightIds?.has(location.rawId) ?? false;
  if (highlightIds) cls += highlighted ? ' hl' : ' dim';

  const group = svgEl('g', { class: cls });

  nodeAt.set(location.rawId, { x, y });

  // The dot itself claims its square so no neighbour's label sits on it.
  claimedBoxes.push({ x0: x - radius - 1, y0: y - radius - 1, x1: x + radius + 1, y1: y + radius + 1 });

  group.append(svgEl('circle', {
    cx: x, cy: y, r: hitPad(radius, room), fill: colour, opacity: '0', class: 'hit',
  }));


  // In shaded commodity mode the ring and dot carry the price grade; a lit
  // place UEX has no price for keeps the plain green ring.
  const shade = highlighted ? nodeShade.get(location.rawId) : null;

  if (highlighted) {
    group.append(svgEl('circle', {
      cx: x, cy: y, r: radius + 5, fill: 'none',
      stroke: shade?.colour ?? '#4fd48a', 'stroke-width': '1.6', class: 'hl-ring', filter: 'url(#glow)',
    }));

    // The report age belongs to the commodity source, not the place itself.
    // Keep it on a commodity result only, so a stale UEX quote cannot make a
    // reliable visit or service fact look stale as well.
    if (mapPriceFreshness && mapPriceFreshness.state !== 'fresh') {
      group.append(svgEl('circle', {
        cx: x, cy: y, r: radius + 8, class: `map-price-age ${mapPriceFreshness.state}`,
      }));
    }
  }

  // Somewhere never visited is drawn as an outline, so the places that carry
  // history read as solid against the rest of the map. A price shade
  // overrides the kind colour - in that mode the colour IS the price, and
  // that has to hold for the whole map: a place with no price for the chosen
  // commodity goes slate, including one the catalogue lit but nobody has
  // priced. Otherwise the kind colours sit in the same picture as the scale
  // and the eye has two colour languages to read at once.
  const dotColour = shade?.colour ?? (shadeScale ? NO_TRADE : colour);

  // A price shade means the colour IS the price, so the mark keeps its shape
  // and takes the graded colour: a mine is still a mine at 1,872 aUEC.
  group.append(kindMark(location.kind, x, y, radius, dotColour, been || !!shade));

  if (showServiceBadges(location, highlighted))
    drawServiceBadges(group, x, y, radius, servicesAt(location));

  if (mapNoteIds.has(location.rawId))
    drawMapNoteBadge(group, x, y, radius);

  // A styled tooltip that appears instantly - the native <title> takes a
  // second to show and cannot be read against the game-HUD styling.
  group.addEventListener('pointerenter', () => showMapTip(location));
  group.addEventListener('pointermove', moveMapTip);
  group.addEventListener('pointerleave', hideMapTip);

  // Click for the detail card; the hit circle above makes small nodes easy to
  // land on.
  group.addEventListener('click', (e) => {
    e.stopPropagation();
    showMapInfo(location);
  });

  // Double-click for the fuller story: what this station takes and offers. The
  // card has no room for a dozen commodities on each side of the counter.
  group.addEventListener('dblclick', (e) => {
    e.stopPropagation();
    e.preventDefault();
    showStation(location.rawId, location.name);
  });

  // Labelling everything is unreadable at this density, so only places with
  // history get one until the view is zoomed in far enough to have room.
  // Search matches are the exception - a lit dot the user cannot name is not
  // an answer - but only while there are few enough for the names to have
  // air; two hundred sellers of a common ore label themselves on zoom instead.
  const nameable = highlighted && (highlightIds.size <= 40 || isDetailed());

  if ((been && radius > 7) || isDetailed() || nameable) {
    // Not drawn here: every wanted label goes through placeLabels at the end
    // of the draw, which resolves collisions instead of stacking neighbours.
    pendingLabels.push({
      group,
      x,
      y,
      radius,
      anchor,
      text: location.name.length > 24 ? `${location.name.slice(0, 23)}…` : location.name,
      priority: (highlighted ? 1_000_000 : 0) + location.visits,
      pinned: highlighted,
    });
  }

  map.append(group);
}

/* ---------- label placement ---------- */

/**
 * Labels wanted by this draw, and rectangles already spoken for (body names,
 * placed labels, the nodes themselves). Both reset per draw.
 */
const pendingLabels = [];
const claimedBoxes = [];

/** Approximate bounding box of a text laid out at x,y with the given anchor. */
function labelBox(x, y, anchorMode, size, text) {
  const width = text.length * size * 0.62;
  const height = size * 1.3;
  const x0 = anchorMode === 'start' ? x : anchorMode === 'end' ? x - width : x - width / 2;
  return { x0, y0: y - height / 2, x1: x0 + width, y1: y + height / 2 };
}

function boxesCollide(a, b) {
  return a.x0 < b.x1 && a.x1 > b.x0 && a.y0 < b.y1 && a.y1 > b.y0;
}

/**
 * Greedy label placement with collision avoidance. Names used to be placed
 * blind - radially out from the planet - which stacked neighbours on top of
 * each other the moment two sites shared a bearing. Now every label tries a
 * handful of positions around its dot, most-visited first, and a label that
 * fits nowhere is dropped: an unreadable pile names nothing, while a bare dot
 * still answers on hover and click.
 */
/**
 * How many names the map will show at the current zoom.
 *
 * Collision avoidance alone is not decluttering: it stops names overlapping,
 * but at system scale microTech still asks for twenty-two of them and the ones
 * that fit form a wall of text around the disc. So the whole view gets a
 * budget, spent on the busiest places first, and it grows with the square of
 * the zoom - zooming in is asking for detail, and that is when there is room
 * to put it. Past the detail threshold every label that fits is drawn.
 */
const labelBudget = () => {
  if (mapLabelDensity() === 'all') return Infinity;
  if (mapLabelDensity() === 'quiet') return isDetailed() ? 28 : 8;
  if (isDetailed()) return Infinity;

  const zoom = HOME_VIEW.w / view.w;
  return Math.max(12, Math.round(14 * zoom * zoom));
};

function placeLabels(map) {
  const size = labelSize();
  const budget = labelBudget();
  let spent = 0;

  pendingLabels.sort((a, b) => b.priority - a.priority);

  for (const want of pendingLabels) {
    // A search match is the answer to a question, so it is never rationed;
    // everything else queues for the budget.
    if (!want.pinned && spent >= budget) continue;

    const { x, y, radius, anchor } = want;

    const dx = anchor ? x - anchor.x : 0;
    const dy = anchor ? y - anchor.y : 0;
    const length = Math.hypot(dx, dy);
    const gap = radius + size * 0.5 + 2;

    const candidates = [];

    // The radial spot first - it is the one that fans a cluster outwards.
    if (length > 0.5) {
      candidates.push({
        x: x + (dx / length) * gap,
        y: y + (dy / length) * gap,
        anchorMode: dx >= 0 ? 'start' : 'end',
      });
    }

    candidates.push(
      { x: x + radius + 3, y, anchorMode: 'start' },
      { x: x - radius - 3, y, anchorMode: 'end' },
      { x, y: y + radius + size * 0.85, anchorMode: 'middle' },
      { x, y: y - radius - size * 0.85, anchorMode: 'middle' },
    );

    for (const spot of candidates) {
      const box = labelBox(spot.x, spot.y, spot.anchorMode, size, want.text);
      if (claimedBoxes.some((other) => boxesCollide(box, other))) continue;

      const label = svgEl('text', {
        x: spot.x,
        y: spot.y,
        'text-anchor': spot.anchorMode,
        'dominant-baseline': 'middle',
        class: 'map-label',
        style: `font-size:${size}px`,
      });
      label.textContent = want.text;
      want.group.append(label);

      claimedBoxes.push(box);
      if (!want.pinned) spent++;
      break;
    }
  }
}

function drawLegend(locations) {
  const legend = $('#map-legend');
  legend.textContent = '';

  const appendPriceFreshness = () => {
    if (!mapPriceFreshness) return;
    const item = el('div', `item price-age ${mapPriceFreshness.state}`);
    const swatch = el('span', 'swatch');
    swatch.style.background = mapPriceFreshness.state === 'stale' ? '#e85d75'
      : mapPriceFreshness.state === 'aging' ? '#ffab3d' : '#4fd48a';
    item.append(swatch, el('span', null, mapPriceFreshness.label));
    legend.append(item);
  };

  const appendServiceLegend = () => {
    const shown = new Set();
    for (const location of locations) {
      const highlighted = highlightIds?.has(location.rawId) ?? false;
      if (!showServiceBadges(location, highlighted)) continue;
      for (const service of servicesAt(location)) shown.add(service);
    }

    for (const service of shown) {
      const item = el('div', 'item');
      item.append(el('span', 'service-tip', SERVICE_META[service]?.icon || '•'));
      item.append(el('span', null, `${SERVICE_META[service]?.label || service} badge`));
      legend.append(item);
    }
  };

  // Shaded commodity mode swaps the kind legend for the price gradient: in
  // that mode colour means price, so the legend must say so.
  if (shadeScale) {
    const item = el('div', 'item');

    for (let i = 0; i <= 4; i++) {
      const swatch = el('span', 'swatch');
      swatch.style.background = shadeColour(i / 4);
      item.append(swatch);
    }

    const lo = Math.round(shadeScale.invert ? shadeScale.max : shadeScale.min).toLocaleString();
    const hi = Math.round(shadeScale.invert ? shadeScale.min : shadeScale.max).toLocaleString();
    item.append(el('span', null, `${shadeScale.label} · ${lo} → ${hi} ${shadeScale.unit}`));
    legend.append(item);

    const plain = el('div', 'item');
    const swatch = el('span', 'swatch');
    swatch.style.background = NO_TRADE;
    plain.append(swatch);
    plain.append(el('span', null, shadeScale.plain ?? 'no UEX price for it here'));
    legend.append(plain);
    appendPriceFreshness();
    appendServiceLegend();
    return;
  }

  const kinds = [...new Set(locations.map((l) => l.kind))].sort();

  for (const kind of kinds) {
    const item = el('div', 'item');

    // The legend draws the mark itself rather than a square of its colour -
    // there is no point naming a shape the key does not show.
    const swatch = document.createElementNS(SVG_NS, 'svg');
    swatch.setAttribute('viewBox', '-1.35 -1.35 2.7 2.7');
    swatch.setAttribute('class', 'swatch-mark');
    swatch.append(kindMark(kind, 0, 0, 1, KIND_COLOURS[kind] || KIND_COLOURS.Unknown, true));
    item.append(swatch);
    item.append(el('span', null, kind.replace(/([a-z])([A-Z])/g, '$1 $2')));
    legend.append(item);
  }

  appendPriceFreshness();
  appendServiceLegend();
}

/* ---------- filters ---------- */

/** Debounced so typing does not re-render on every keystroke. */
function onInput(selector, handler) {
  const node = $(selector);
  if (!node) return;

  let timer = null;
  const run = () => {
    clearTimeout(timer);
    timer = setTimeout(handler, 120);
  };

  node.addEventListener('input', run);
  node.addEventListener('change', handler);
}

/**
 * Re-fetches totals for one view's chosen window and re-renders just that view.
 *
 * Aggregation happens server-side, so a period change is a round trip rather
 * than a client-side filter - the browser never holds per-session detail for
 * spending, contracts or places.
 */
async function refreshForPeriod(selectId, render) {
  const days = Number($(selectId).value) || 0;

  try {
    const stats = await getJson(`/api/stats?days=${days}`);
    render(stats);
  } catch (error) {
    console.error(`period refresh failed for ${selectId}`, error);
  }
}

onInput('#fleet-search', renderFleetShips);
onInput('#fleet-period', renderFleetShips);

onInput('#spending-period', () => refreshForPeriod('#spending-period', renderSpending));
onInput('#contracts-period', () => refreshForPeriod('#contracts-period', renderContracts));
onInput('#places-period', () => refreshForPeriod('#places-period', renderPlaces));
onInput('#places-search', () => libraryStats && renderPlaces(libraryStats));
onInput('#ledger-period', loadLedger);
onInput('#commodities-period', loadCommodities);
onInput('#loadout-search', () => libraryStats && renderLoadout(libraryStats));
onInput('#stash-search', () => libraryStats && renderStash(libraryStats));
onInput('#stash-period', () => libraryStats && renderStash(libraryStats));
onInput('#stash-latest', () => libraryStats && renderStash(libraryStats));
$('#stash-ever')?.addEventListener('change', () => libraryStats && renderStash(libraryStats));

/* ---------- stale prices ---------- */

/**
 * Offers to renew the price table when it has gone stale.
 *
 * UEX is fetched on a click unless the automatic refresh has been turned on,
 * and a click-only table has one cost: one pulled a fortnight ago looks exactly
 * like one pulled this morning, and every margin on the page is quietly wrong.
 * So the app looks once at startup, and if the snapshot has turned a day old it
 * says so and offers the one click that fixes it.
 *
 * This is the path for everyone who left the refresh off, and it stays for
 * them: with it on the table never reaches a day old, so the offer simply never
 * fires rather than needing to know about it.
 *
 * An offer, not an alert: nothing is blocked, dismissing it lasts the day, and
 * the check runs once per load rather than on any schedule.
 */
const STALE_AFTER_HOURS = 24;
const STALE_DISMISSED = 'qw-uex-stale-dismissed';

/** Feeds enabled alongside the price table, which age at the same rate. */
let staleFeeds = [];

function dismissedToday() {
  try {
    const until = Number(localStorage.getItem(STALE_DISMISSED) || 0);
    return until > Date.now();
  } catch {
    return false;
  }
}

const hoursSince = (iso) => (Date.now() - new Date(iso).getTime()) / 3600000;

async function checkPriceAge() {
  const notice = $('#stale');
  if (!notice || dismissedToday()) return;

  let uex;
  try {
    uex = await getJson('/api/uex');
  } catch {
    return;
  }

  // Nothing to renew when the integration is off or has never been fetched:
  // the Settings page already offers to turn it on, and being nagged about a
  // feature you have not enabled is noise.
  if (!uex.enabled || !uex.fetchedAt) return;

  const age = hoursSince(uex.fetchedAt);

  staleFeeds = await getJson('/api/uex/feeds')
    .then((feeds) => feeds.filter((f) => f.enabled && (!f.fetchedAt || hoursSince(f.fetchedAt) >= STALE_AFTER_HOURS)))
    .catch(() => []);

  if (age < STALE_AFTER_HOURS && !staleFeeds.length) return;

  const parts = [`Prices last fetched ${ago(uex.fetchedAt)}`];

  if (staleFeeds.length) {
    parts.push(`${staleFeeds.length} other feed${staleFeeds.length === 1 ? '' : 's'} as old`);
  }

  parts.push('margins and best-price rankings are only as fresh as this');

  $('#stale-detail').textContent = `${parts.join(' · ')}.`;
  notice.hidden = false;
}

function initStaleNotice() {
  const notice = $('#stale');
  if (!notice) return;

  $('#stale-refresh').addEventListener('click', async (event) => {
    const button = event.currentTarget;
    button.disabled = true;
    button.textContent = 'Fetching…';

    try {
      // The prices first, then whatever else had gone stale with them: a
      // refresh that renewed half of it would leave the same problem behind.
      await fetch('/api/uex/enable', { method: 'POST' });

      for (const feed of staleFeeds) {
        await fetch(`/api/uex/feeds/${encodeURIComponent(feed.key)}/enable`, { method: 'POST' });
      }

      notice.hidden = true;
      loadMarket().catch(() => { /* the page keeps the numbers it has */ });
      renderSettings().catch(() => { /* Settings redraws on its next visit */ });
    } catch {
      button.textContent = 'Could not fetch';
    } finally {
      button.disabled = false;
    }
  });

  $('#stale-dismiss').addEventListener('click', () => {
    notice.hidden = true;

    // Until tomorrow rather than forever: the answer to "not now" is "ask me
    // again when it matters", and by then the prices are a day older still.
    try {
      localStorage.setItem(STALE_DISMISSED, String(Date.now() + 86400000));
    } catch { /* private browsing: the offer returns on the next load */ }
  });
}

/* ---------- the wipe ---------- */

/**
 * The line under the player's history, on the Settings page.
 *
 * Every total the app reports describes the account being played now, and a
 * data wipe ends one account and starts another. The date is a setting because
 * CIG decides when wipes land, and because someone looking back at an older
 * patch should be able to wind it back and see the lot.
 */
async function loadWipe() {
  const field = $('#wipe-at');
  if (!field) return;

  let wipe;
  try {
    wipe = await getJson('/api/wipe');
  } catch {
    return;
  }

  field.value = wipe.at ? new Date(wipe.at).toISOString().slice(0, 10) : '';
  $('#wipe-patch').value = wipe.patch === 'no wipe' ? '' : wipe.patch;

  for (const [name, box] of Object.entries(WIPE_SCOPES)) {
    $(box).checked = (wipe.covers || []).includes(name);
  }

  showWipeStatus(wipe);
}

/** What a wipe can take, and the box that says whether this one did. */
const WIPE_SCOPES = {
  money: '#wipe-money',
  ships: '#wipe-ships',
  inventory: '#wipe-inventory',
  history: '#wipe-history',
};

const chosenScopes = () =>
  Object.entries(WIPE_SCOPES).filter(([, box]) => $(box).checked).map(([name]) => name);

function showWipeStatus(wipe) {
  const status = $('#wipe-status');
  if (!status) return;

  if (!wipe.at) {
    status.textContent = `counting all ${(wipe.stored ?? 0).toLocaleString()} sessions`;
    return;
  }

  if (wipe.hidden === 0) {
    status.textContent = 'nothing on record from before this';
    return;
  }

  const covers = wipe.covers || [];
  const sessions = `${wipe.hidden.toLocaleString()} session${wipe.hidden === 1 ? '' : 's'} before this`;

  // A partial wipe hides nothing outright, so saying "not counted" flat would
  // be a lie: those sessions still count towards everything it did not take.
  status.textContent = covers.length === Object.keys(WIPE_SCOPES).length
    ? `${sessions} are kept but not counted`
    : `${sessions} still count, except for ${covers.join(', ')}`;
}

async function saveWipe(at, patch) {
  const status = $('#wipe-status');

  try {
    const response = await fetch('/api/wipe', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ at, patch, covers: chosenScopes() }),
    });

    if (!response.ok) throw new Error(response.statusText);

    // Every page's numbers just changed, so they are all re-read rather than
    // left to be discovered stale on the next visit.
    await loadHistory();
    await loadWipe();

    if (status) status.textContent = `${status.textContent} · saved`;
  } catch (e) {
    if (status) status.textContent = `could not save: ${e.message}`;
  }
}

function initWipe() {
  const field = $('#wipe-at');
  if (!field) return;

  $('#wipe-save').addEventListener('click', () => {
    if (!field.value) {
      $('#wipe-status').textContent = 'pick a date, or use "count everything"';
      return;
    }

    saveWipe(`${field.value}T00:00:00Z`, $('#wipe-patch').value.trim());
  });

  $('#wipe-clear').addEventListener('click', () => saveWipe(null, null));

  // A depth changed without a date is still a change worth keeping, and there
  // is no second Save button to reach for.
  for (const box of Object.values(WIPE_SCOPES)) {
    $(box).addEventListener('change', () => {
      if (field.value) saveWipe(`${field.value}T00:00:00Z`, $('#wipe-patch').value.trim());
    });
  }
}

/* ---------- new versions ---------- */

/**
 * Asks, once, whether this copy may look for a newer one.
 *
 * The app's standing promise is that it connects only when asked, and a version
 * check is a connection. So the first start puts the question on screen with
 * three answers - every start, just this once, or never - and never asks again
 * whichever way it goes. The check itself is a plain GET of a public release
 * feed: it sends nothing, and what it learns is what anyone can read.
 */
async function checkForUpdate() {
  const notice = $('#update');
  if (!notice || isOverlay) return;

  let state;
  try {
    state = await getJson('/api/updates');
  } catch {
    return;
  }

  if (!state.asked) {
    askAboutUpdates(state);
    return;
  }

  if (state.automatic) await runUpdateCheck({ quiet: true });
}

/** The question, with its three answers. */
function askAboutUpdates(state) {
  const notice = $('#update');

  $('#update-title').textContent = 'Look for a newer version?';
  $('#update-detail').textContent =
    'One request to GitHub, sending nothing about you or this machine. '
    + 'Nothing is downloaded or installed — you would get a link.';

  const actions = $('#update-actions');
  actions.textContent = '';

  const answer = async (automatic, thenCheck) => {
    await fetch(`/api/updates/answer?automatic=${automatic}`, { method: 'POST' });
    notice.hidden = true;
    renderUpdateSettings().catch(() => { /* Settings redraws on its next visit */ });
    if (thenCheck) await runUpdateCheck({ quiet: false });
  };

  const every = el('button', 'ghost', 'Yes, every start');
  every.addEventListener('click', () => answer(true, true));

  const once = el('button', 'ghost', 'Just this once');
  once.addEventListener('click', () => answer(false, true));

  const never = el('button', 'ghost', 'No thanks');
  never.addEventListener('click', () => answer(false, false));

  actions.append(every, once, never);
  notice.hidden = false;
}

/**
 * Runs a check and says what it found.
 *
 * @param quiet Say nothing when this copy is current - true for a startup
 *   check, which nobody asked a question of, and false for a click, which is a
 *   question and deserves an answer either way.
 */
async function runUpdateCheck({ quiet }) {
  const notice = $('#update');

  let result;
  try {
    const response = await fetch('/api/updates/check', { method: 'POST' });
    result = await response.json();
  } catch {
    if (!quiet) $('#update-status').textContent = 'could not reach GitHub just now';
    return;
  }

  renderUpdateSettings().catch(() => { /* the toggle is still right */ });

  if (!result.newer) {
    if (!quiet) $('#update-status').textContent = `up to date — ${result.current} is the newest`;
    notice.hidden = true;
    return;
  }

  $('#update-title').textContent = `Quantum Wake ${result.latest} is out`;
  $('#update-detail').textContent = `You are running ${result.current}.`
    + (result.publishedAt ? ` Published ${dayUtc(result.publishedAt)}.` : '');

  const actions = $('#update-actions');
  actions.textContent = '';

  const open = el('button', 'ghost', 'Open the release page');
  open.addEventListener('click', () => {
    window.open(result.url, '_blank', 'noreferrer');
    notice.hidden = true;
  });

  const later = el('button', 'ghost', 'Later');
  later.addEventListener('click', () => { notice.hidden = true; });

  actions.append(open, later);
  notice.hidden = false;
}

/** The Settings block: the toggle, and what the last look found. */
async function renderUpdateSettings() {
  const toggle = $('#update-auto');
  if (!toggle) return;

  const state = await getJson('/api/updates');
  toggle.checked = !!state.automatic;

  const status = $('#update-status');
  if (!status) return;

  status.textContent = state.lastCheckedAt
    ? `last checked ${ago(state.lastCheckedAt)}`
    : 'never checked';
}

function initUpdates() {
  const toggle = $('#update-auto');
  if (!toggle) return;

  toggle.addEventListener('change', async () => {
    await fetch(`/api/updates/answer?automatic=${toggle.checked}`, { method: 'POST' });
    renderUpdateSettings().catch(() => { /* the box already shows the choice */ });
  });

  $('#update-check').addEventListener('click', async (event) => {
    const button = event.currentTarget;
    button.disabled = true;
    $('#update-status').textContent = 'looking…';

    try {
      await runUpdateCheck({ quiet: false });
    } finally {
      button.disabled = false;
    }
  });
}

/**
 * Offers to move the wipe line when a new patch has landed since it.

 *
 * Nothing in the logs says an account was reset - there is no such line. What
 * they do say is when a patch arrived, and wipes arrive with patches, so the
 * app brings the date and the player answers the one question it cannot: did
 * that one wipe? Answering either way is remembered for that patch, so it is
 * asked once rather than every launch.
 */
const PATCH_ANSWERED = 'qw-patch-answered';

function patchAnswered(patch) {
  try {
    return (localStorage.getItem(PATCH_ANSWERED) || '').split(',').includes(patch);
  } catch {
    return false;
  }
}

function rememberPatchAnswer(patch) {
  try {
    const seen = (localStorage.getItem(PATCH_ANSWERED) || '').split(',').filter(Boolean);
    if (!seen.includes(patch)) seen.push(patch);
    localStorage.setItem(PATCH_ANSWERED, seen.join(','));
  } catch { /* private browsing: it asks again next time, which is not harmful */ }
}

/** The patch offered right now, so the buttons know what they are answering. */
let offeredPatch = null;

async function checkForWipe() {
  const notice = $('#patch');
  if (!notice) return;

  let wipe;
  try {
    wipe = await getJson('/api/wipe');
  } catch {
    return;
  }

  const found = wipe.suggested;
  if (!found || patchAnswered(found.patch)) return;

  offeredPatch = found;

  $('#patch-title').textContent = `${found.patch} arrived on ${dayUtc(found.at)}`;
  $('#patch-detail').textContent = wipe.at
    ? `Your history is counted from ${dayUtc(wipe.at)}. If that patch wiped, the line belongs there instead.`
    : 'Nothing is being held back at the moment. If that patch wiped, your totals are counting an account you no longer have.';

  notice.hidden = false;
}

function initWipePrompt() {
  const notice = $('#patch');
  if (!notice) return;

  $('#patch-wiped').addEventListener('click', async () => {
    if (!offeredPatch) return;

    rememberPatchAnswer(offeredPatch.patch);
    notice.hidden = true;

    // Straight to the full depth: a patch wipe is the ordinary kind, and the
    // Settings page is where a partial one gets its detail.
    await saveWipe(offeredPatch.at, offeredPatch.patch);
  });

  $('#patch-kept').addEventListener('click', () => {
    if (offeredPatch) rememberPatchAnswer(offeredPatch.patch);
    notice.hidden = true;
  });
}

/* Wired at load, like the other page-level controls
: neither belongs to a
   view, and both must work before anything has been rendered. */
initStaleNotice();
initWipe();
initWipePrompt();
initUpdates();
initStarStrings();

/* ---------- scan progress ---------- */

/**
 * Polls the scan and reflects it in a progress bar.
 *
 * A cold backfill reads 400 MB across ~145 files. Previously the page simply sat
 * empty while boot() retried in silence, which is indistinguishable from being
 * broken.
 */
async function watchScan() {
  const panel = $('#scan');
  let sawRunning = false;

  for (;;) {
    let status;
    try {
      status = await getJson('/api/scan/status');
    } catch {
      await wait(1000);
      continue;
    }

    if (status.running) {
      sawRunning = true;
      panel.hidden = false;

      $('#scan-fill').style.width = `${status.percent}%`;
      $('#scan-count').textContent = `${status.done} / ${status.total} · ${status.elapsedSeconds}s`;
      $('#scan-file').textContent = status.file || '';

      $('#scan-label').textContent = status.parsed > 0
        ? `Parsing logs — ${status.parsed} new`
        : 'Checking logs…';
    } else if (sawRunning) {
      // Finished: fill the bar, then reload the views with the new data.
      $('#scan-fill').style.width = '100%';
      $('#scan-label').textContent = 'Scan complete';
      $('#scan-count').textContent = `${status.parsed} parsed · ${status.elapsedSeconds}s`;
      $('#scan-file').textContent = '';

      await wait(1200);
      panel.hidden = true;

      try {
        await loadHistory();
      } catch { /* the retry loop in boot covers this */ }

      return;
    }

    await wait(status.running ? 400 : 1000);
  }
}

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/* ---------- boot ---------- */

/**
 * The first-flight wizard: shown once, over everything, while the initial
 * backfill runs. It fronts the three choices that matter on day one - the
 * overlay, the community dataset, UEX prices - because they are opt-in and
 * would otherwise hide in Settings while the app looked half-empty. Applying
 * them reloads the page so every view boots with the datasets in.
 */
async function maybeShowSetup() {
  if (isOverlay) return;

  // ?setup=1 forces the wizard - a preview that skips nothing permanent.
  if (!params.has('setup')) {
    if (isSnapshot) return;
    if ((await getJson('/api/setup')).done) return;
  }

  const panel = $('#setup');
  panel.hidden = false;

  // The overlay choice only exists when this server lives inside the app.
  try {
    const overlay = await getJson('/api/overlay');
    $('#setup-overlay-row').hidden = !overlay.available;
  } catch { $('#setup-overlay-row').hidden = true; }

  // The extra UEX feeds, offered here too - but only once UEX itself is
  // ticked, since they are useless without the core price tables.
  try {
    const feeds = await getJson('/api/uex/feeds');
    const list = $('#setup-feed-list');
    list.textContent = '';

    for (const feed of feeds) {
      const row = el('label', 'setup-feed');
      const box = document.createElement('input');
      box.type = 'checkbox';
      box.dataset.feed = feed.key;

      const text = el('div');
      text.append(el('b', null, feed.title));
      text.append(el('span', 'cost', ` ${feed.cost}`));
      text.append(el('span', null, feed.description));

      row.append(box);
      row.append(text);
      list.append(row);
    }

    // The wipe, offered before anything has been counted. The date defaults to
    // the newest patch this install has logs from, because a first run on an
    // old machine is exactly when "why is my total so big" starts.
    try {
      const wipe = await getJson('/api/wipe');
      const suggested = wipe.suggested;

      $('#setup-wipe').value = new Date(suggested ? suggested.at : wipe.at || Date.now())
        .toISOString().slice(0, 10);

      $('#setup-wipe-note').textContent = suggested
        ? `${suggested.patch} arrived then — change it if your last wipe was another one.`
        : `${wipe.patch} — change it if your last wipe was another one.`;
    } catch { /* the field keeps whatever the markup had */ }

    const uexBox = $('#setup-uex');

    // The feed list and the refresh switch are both conditions of taking UEX at
    // all, so they appear with it and go away with it - and the switch is
    // cleared on the way out, or unticking UEX would leave an agreement behind
    // for a thing that was never enabled.
    const syncFeeds = () => {
      $('#setup-feeds').hidden = !uexBox.checked;
      $('#setup-uex-auto-row').hidden = !uexBox.checked;

      if (!uexBox.checked) $('#setup-uex-auto').checked = false;
    };

    uexBox.addEventListener('change', syncFeeds);
    syncFeeds();
  } catch { /* no feed list; the wizard still works */ }

  // The wizard has its own copy of the scan bar - the page's one sits
  // underneath it where nobody can see it.
  const poll = setInterval(async () => {
    try {
      const status = await getJson('/api/scan/status');

      if (status.running) {
        $('#setup-scan-fill').style.width = `${status.percent}%`;
        $('#setup-scan-label').textContent = status.parsed > 0
          ? `Reading logs — ${status.parsed} events so far`
          : 'Checking logs…';
        $('#setup-scan-count').textContent = `${status.done} / ${status.total} files`;
      } else {
        $('#setup-scan-fill').style.width = '100%';
        $('#setup-scan-label').textContent = 'Logs read — history ready';
        $('#setup-scan-count').textContent = '';
      }
    } catch { /* server between restarts; the next tick answers */ }
  }, 700);

  $('#setup-start').addEventListener('click', async () => {
    const button = $('#setup-start');
    const status = $('#setup-status');
    button.disabled = true;

    try {
      if (!$('#setup-overlay-row').hidden && $('#setup-overlay').checked)
        await fetch('/api/overlay?visible=true', { method: 'POST' }).catch(() => {});

      if ($('#setup-community').checked) {
        status.textContent = 'Fetching the community dataset — a minute or two…';
        await fetch('/api/community/enable', { method: 'POST' });
      }

      if ($('#setup-uex').checked) {
        status.textContent = 'Fetching UEX prices…';
        await fetch('/api/uex/enable', { method: 'POST' });

        // Extra feeds one at a time, so one failing does not take the rest.
        for (const box of $$('#setup-feed-list input:checked')) {
          status.textContent = `Fetching the ${box.dataset.feed} feed…`;
          await fetch(`/api/uex/feeds/${box.dataset.feed}/enable`, { method: 'POST' })
            .catch(() => {});
        }

        // Recorded as an answer either way: someone who read the option and
        // left it alone has declined, and should not be asked a second time.
        await fetch(
          `/api/uex/auto/answer?automatic=${$('#setup-uex-auto').checked}`,
          { method: 'POST' }).catch(() => {});
      }

      const chosenWipe = $('#setup-wipe').value;

      if (chosenWipe) {
        status.textContent = 'Setting where your history starts…';
        await fetch('/api/wipe', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ at: `${chosenWipe}T00:00:00Z`, patch: 'set at first run' }),
        }).catch(() => { /* Settings can set it later */ });
      }

      await fetch('/api/setup/done', { method: 'POST' });
      clearInterval(poll);

      // A clean reboot with everything enabled beats patching each view.
      location.reload();
    } catch {
      status.textContent = 'Something did not fetch — you can finish any of this in Settings.';
      button.disabled = false;
    }
  });
}

async function boot() {
  initNowCardCollapsers();

  if (isOverlay) {
    document.body.classList.add('overlay');

    // The widget's layout is chosen in the dashboard, a different browser, so
    // it is polled rather than pushed.
    applyOverlayLayout().catch(() => {});
    setInterval(() => applyOverlayLayout().catch(() => {}), 5000);
  }

  const deepCommodity = commodityFromHash();
  const requested = viewFromHash();

  if (deepCommodity) openCommodity(deepCommodity).catch(() => {});
  else if (requested) showView(requested);

  // ?q= pre-fills the map search, so a commodity view is a shareable link:
  // /?q=Copper#map opens the map with the sellers lit.
  const q = params.get('q');
  if (q) $('#map-search').value = q;

  initMap();

  // The current location on Now flies to the map like every other place name.
  $('#now-location').classList.add('now-jump');
  $('#now-location').title = 'Show on the map';
  $('#now-location').addEventListener('click', () => {
    const name = $('#now-location').textContent.trim();
    if (name && name !== '—') jumpToPlace(name);
  });

  try {
    const install = await getJson('/api/install');
    $('#install').textContent = `${install.channel} · ${install.backups} logs`;
    $('#about-install').textContent = `${install.channel} · ${install.backups} logs`;
  } catch {
    $('#install').textContent = 'no install found';
    $('#about-install').textContent = 'none found';

    // Nothing to read: ask where the game lives rather than showing an
    // app full of empty pages.
    if (!isOverlay) $('#no-install').hidden = false;
  }

  try {
    const info = await getJson('/api/version');

    // The build string carries the commit ("0.2.0+9c3cdf7..."); seven hash
    // characters identify it and forty just wrap the layout.
    const build = (info.build || info.version).replace(/\+([0-9a-f]{7})[0-9a-f]*$/i, '+$1');

    $('#about-version').textContent = build;
    $('#foot-version').textContent = `v${info.version}`;
    $('#foot-version').title = `Build ${build}`;
  } catch {
    $('#about-version').textContent = 'unknown';
  }

  if (!isSnapshot) {
    connectStream();
    watchScan();

    // Once per load, never on a timer: the offer to renew a price table that
    // has gone a day old, and the line the wipe draws under the history.
    checkPriceAge().catch(() => { /* prices are usable whatever their age */ });
    checkForWipe().catch(() => { /* the Settings page still carries the line */ });
    checkForUpdate().catch(() => { /* an unanswered question is not a failure */ });
  }

  renderUpdateSettings().catch(() => { /* Settings fills in on its next visit */ });

  loadWipe().catch(() => { /* Settings shows it on its next visit */ });

  maybeShowSetup().catch(() => { /* wizard is a nicety, never a blocker */ });

  // The first scan may still be running; retry until sessions appear.
  for (let attempt = 0; attempt < 30; attempt++) {
    try {
      await loadHistory();
      const count = (await getJson('/api/sessions')).length;
      if (count > 0) break;
    } catch { /* server still warming up */ }
    await wait(2000);
  }
}

boot();
