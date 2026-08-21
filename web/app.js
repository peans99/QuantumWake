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
const dateOf = (iso) => new Date(iso).toLocaleDateString([], { year: 'numeric', month: 'short', day: '2-digit' });

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
  const target = buttons.find((b) => b.dataset.view === name);
  if (!target) return;

  // Settings reflects live state (the tray can change it), so re-read on entry.
  if (name === 'settings') renderSettings().catch(() => {});

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

window.addEventListener('hashchange', () => {
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
window.scOverlayExpanded = (on) => document.body.classList.toggle('expanded', Boolean(on));

document.addEventListener('keydown', (event) => {
  if (!event.ctrlKey || !event.altKey) return;

  if (event.key === 'ArrowRight') { window.scCycleView(1); event.preventDefault(); }
  if (event.key === 'ArrowLeft') { window.scCycleView(-1); event.preventDefault(); }
});

/* ---------- live view ---------- */

let sessionStarted = null;

function renderNow(state) {
  $('#link').classList.toggle('live', !!state.connected);
  $('#link').title = state.connected ? 'live' : 'disconnected';

  $('#now-location').textContent = state.location || (state.inGame ? 'Unknown' : 'In menus');
  $('#now-location-sub').textContent = [state.locationBody, state.locationSystem].filter(Boolean).join(' · ');

  // The map follows the live feed, so the marker moves as the player does.
  setHere(state.locationId);

  // The trade card follows too, refreshing only when the place changes.
  refreshTradeAdvice(state.location).catch(() => {});

  const confidence = $('#now-confidence');
  confidence.textContent = state.location ? `${state.confidence.toLowerCase()} confidence` : '';
  confidence.className = `confidence ${(state.confidence || '').toLowerCase()}`;

  const travel = $('#now-travel');
  travel.hidden = !state.travelling;
  if (state.travelling) $('#now-travel-to').textContent = state.travellingTo || '';

  $('#now-ship').textContent = state.ship || '—';
  $('#now-handle').textContent = state.handle || '—';
  $('#now-version').textContent = state.gameVersion || '';
  $('#now-mode').textContent = state.inGame ? (state.gameRules || 'in game') : 'frontend / menus';
  $('#now-deaths').textContent = state.deaths ?? 0;
  $('#now-incaps').textContent = state.incapacitations ?? 0;
  $('#now-kills').textContent = state.kills ?? 0;

  // Be explicit about what each number is and is not. Deaths are inferred, and
  // zero kills is the game not reporting them rather than a bug.
  $('#combat-note').textContent = (state.kills ?? 0) === 0
    ? 'Deaths are inferred from corpse item-recovery bursts — 4.9 no longer writes '
      + '<Actor Death>, and an Incapacitated notification is not always raised. '
      + 'Kills cannot be counted at all: no surviving event names a killer.'
    : '';

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
  try {
    render();
  } catch (error) {
    console.error(`${name} failed to render`, error);

    const banner = $('#render-errors');
    banner.hidden = false;
    banner.append(el('div', null, `${name}: ${error && error.message ? error.message : error}`));
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

  // These fetch their own data, so they are kicked off rather than awaited.
  loadLedger().catch((e) => console.error('ledger', e));
  loadLogbook().catch((e) => console.error('logbook', e));
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
    const tr = el('tr');
    const cells = [
      dateOf(session.startedAt),
      duration(session.inGame),
      duration(session.menu),
      session.primaryShip || '—',
      session.lastLocation || '—',
    ];
    cells.forEach((text) => tr.append(el('td', null, text)));
    [session.jumps, session.contracts, session.deaths ?? 0, session.incapacitations]
      .forEach((n) => tr.append(el('td', 'num', String(n))));
    body.append(tr);
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

async function loadMarket() {
  try {
    marketEntries = await getJson('/api/market');
  } catch {
    marketEntries = [];
  }

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
    tr.append(el('td', null, entry.name));
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

    tr.append(actions);

    body.append(tr);
  }
}

onInput('#market-search', renderMarket);
$('#market-group')?.addEventListener('change', renderMarket);

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
function commoditySites(term) {
  const buying = term.startsWith('buy:');
  const name = (buying ? term.slice(4) : term).trim();

  const entry = marketEntries.find((e) => e.name.toLowerCase() === name);
  if (!entry) return null;

  const keys = buying ? entry.bought : entry.sold;
  return keys.length ? facilityTokens(keys) : null;
}

/* ---------- loot ---------- */

async function loadLoot() {
  const days = Number($('#loot-period').value) || 0;
  renderLoot(await getJson(`/api/loot?days=${days}`));
}

function renderLoot(pickups) {
  const term = ($('#loot-search').value || '').trim().toLowerCase();

  const rows = pickups.filter((p) =>
    !term || p.item.toLowerCase().includes(term) || p.place.toLowerCase().includes(term));

  tiles('#loot-summary', [
    ['New items', rows.length],
    ['Last 7 days', rows.filter((p) => Date.now() - new Date(p.at).getTime() < 7 * 86400000).length],
    ['Places', new Set(rows.map((p) => p.place)).size],
  ]);

  const body = $('#loot-table tbody');
  body.textContent = '';

  if (!rows.length) {
    const tr = el('tr');
    const td = el('td', 'muted', 'Nothing in that range.');
    td.colSpan = 3;
    tr.append(td);
    body.append(tr);
    return;
  }

  for (const pickup of rows) {
    const tr = el('tr');
    tr.append(el('td', null, dateOf(pickup.at)));
    tr.append(el('td', null, prettyItem(pickup.item)));
    tr.append(tdPlace(pickup.place, 'muted'));
    body.append(tr);
  }

  lastLootRows = pickups;
}

let lastLootRows = [];

onInput('#loot-search', () => renderLoot(lastLootRows));
onInput('#loot-period', loadLoot);

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
const MANUFACTURERS = {
  DRAK: 'Drake Interplanetary', ANVL: 'Anvil Aerospace', RSI: 'Roberts Space Industries',
  MISC: 'MISC', ORIG: 'Origin Jumpworks', AEGS: 'Aegis Dynamics',
  CRUS: 'Crusader Industries', CNOU: 'Consolidated Outland', TMBL: 'Tumbril',
  ESPR: 'Esperia', BANU: 'Banu', KRIG: 'Kruger Intergalactic',
  ARGO: 'ARGO Astronautics', AOPO: 'Aopoa', GATS: 'Gatac', MRAI: 'Mirai',
};

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

    // "DRAK Clipper" -> prefix + model.
    const [prefix, ...rest] = ship.name.split(' ');
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
    if (MANUFACTURERS[prefix]) {
      const img = document.createElement('img');
      img.src = `assets/manufacturers/${prefix}.png`;
      img.alt = MANUFACTURERS[prefix];
      img.loading = 'lazy';
      badge.append(img);
    } else {
      badge.append(el('span', 'ship-logo-text', prefix));
    }
    card.append(badge);

    const body = el('div', 'ship-body');
    body.append(el('div', 'ship-name', rest.join(' ') || ship.name));
    body.append(el('div', 'ship-maker', MANUFACTURERS[prefix] || prefix));

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
    if (assetsData?.priced) {
      const row = assetsData.fleet?.find((f) => f.name === ship.name);
      body.append(row?.price
        ? el('div', 'ship-price', `${money(row.price.price)} · ${row.price.terminal}`)
        : el('div', 'ship-price muted', 'not sold in game'));
    }

    card.append(body);
    (grounded ? vehicleGrid : grid).append(card);
  }
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

function renderStash(stats) {
  libraryStats = stats;

  const grid = $('#stash-grid');
  grid.textContent = '';

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

/** How far a body's sites reach, given how many it has. */
const clusterRadius = (count) => (count <= 1 ? 14 : 13 + 5.2 * Math.sqrt(count - 1));

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
  const data = await getJson('/api/map');
  atlas = data.nodes || [];
  bodyPositions = data.positions || {};
  drawMap();
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

  if (!present.some((name) => lookup(name))) {
    present.forEach((bodyName, index) => {
      const angle = (index / Math.max(1, present.length)) * Math.PI * 2 - Math.PI / 2;
      placements.set(bodyName, {
        x: centre.x + Math.cos(angle) * orbit,
        y: centre.y + Math.sin(angle) * orbit,
        angle,
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
      const radius = orbit
        * (0.3 + 0.7 * Math.sqrt(Math.hypot(group.pos.x, group.pos.y) / maxR));
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
          x: gx, y: gy, angle, from: { x: centre.x, y: centre.y },
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
        angle: arc,
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
  drawHere();

  // Follow mode: the map pans itself as the player moves, so a second monitor
  // shows the journey without being touched.
  if (followHere && changed && hereId)
    centreOn(hereId);
}

function drawHere() {
  const map = $('#starmap');
  map.querySelectorAll('.map-here').forEach((n) => n.remove());

  const point = hereId && nodeAt.get(hereId);
  $('#map-here').disabled = !point;
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
    x: point.x, y: point.y - ring - 7 * zoom, 'text-anchor': 'middle',
    class: 'map-label here-label', style: `font-size:${labelSize(0.85)}px`,
  });
  label.textContent = 'YOU ARE HERE';
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

  map.addEventListener('pointerdown', (e) => {
    cancelAnimationFrame(viewAnimation);
    drag = { x: e.clientX, y: e.clientY, vx: view.x, vy: view.y };
    map.setPointerCapture(e.pointerId);
    map.classList.add('dragging');
  });

  map.addEventListener('pointermove', (e) => {
    if (!drag) return;

    const box = map.getBoundingClientRect();
    view.x = drag.vx - ((e.clientX - drag.x) / box.width) * view.w;
    view.y = drag.vy - ((e.clientY - drag.y) / box.height) * view.h;
    applyView();
  });

  const endDrag = (e) => {
    if (!drag) return;
    drag = null;
    map.releasePointerCapture?.(e.pointerId);
    map.classList.remove('dragging');
  };

  map.addEventListener('pointerup', endDrag);
  map.addEventListener('pointercancel', endDrag);

  $('#map-reset').addEventListener('click', () => animateViewTo(HOME_VIEW));

  $('#map-here').addEventListener('click', () => centreOn(hereId));
  $('#map-visited-only').addEventListener('change', () => drawMap());
  $('#map-shade').addEventListener('change', () => drawMap());
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
    .slice(0, 8);

  box.textContent = '';

  if (matches.length === 0) {
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

const SHADE_STOPS = ['#24543f', '#4fd48a', '#ffe08a'];

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

  const shadeSelect = $('#map-shade');
  shadeSelect.hidden = !sites;
  if (!sites || !highlightIds) return;

  const buying = term.startsWith('buy:');
  const name = (buying ? term.slice(4) : term).trim();
  const entry = marketEntries.find((e) => e.name.toLowerCase() === name);
  if (!entry) return;

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

    const matched = shadeRows.rows.filter(
      (r) => metricOf(r) > 0 && terminalMatchesPlace(r.terminal, place.name));
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

/** What the hover tip currently shows, so pointermove does not rebuild it. */
let tipKey = null;

/** Appends a capped goods line to the tip when the Goods checkbox is on. */
function appendTipGoods(tip, names) {
  if (!names.length) return;

  const shown = names.slice(0, 6).join(', ');
  const more = names.length > 6 ? ` +${names.length - 6} more` : '';
  tip.append(el('span', 'goods', `Sells ${shown}${more}`));
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

  if ($('#map-goods').checked) appendTipGoods(tip, commoditiesSoldAt(location));

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

  info.hidden = false;
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

  const locations = atlas.filter((l) => term || !visitedOnly || l.visits > 0);

  const count = $('#map-count');
  if (count) {
    const seen = atlas.filter((l) => l.visits > 0).length;
    if (term && !highlightIds) count.textContent = 'no match';
    else if (sites) {
      count.textContent = term.startsWith('buy:')
        ? `stocked at ${highlightIds.size} places the map can name`
        : `sells at ${highlightIds.size} places the map can name`;
    }
    else if (term) count.textContent = `${highlightIds.size} place${highlightIds.size === 1 ? '' : 's'} lit`;
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
  const radiusFor = (visits) => 4 + Math.sqrt(visits / maxVisits) * 13;

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

      // Body names sit outside the cluster they head, so the sites below have
      // clear air to put their own labels in. They are placed first and claim
      // their box, so site labels flow around them.
      const bodyLabelX = bx + Math.cos(angle) * (reach + 16);
      const bodyLabelY = by + Math.sin(angle) * (reach + 16);
      const bodyLabelSize = labelSize(1.15);

      const bodyLabel = svgEl('text', {
        x: bodyLabelX, y: bodyLabelY,
        'text-anchor': 'middle', class: 'map-label',
        style: `fill:#7796b0;font-size:${bodyLabelSize}px;letter-spacing:.14em;text-transform:uppercase`,
      });
      bodyLabel.textContent = bodyName === '—' ? '' : bodyName;
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

        bodyHover.addEventListener('pointermove', (e) => {
          if (tipKey !== bodyKey) showBodyTip(bodyName, system, sites);
          moveMapTip(e);
        });
        bodyHover.addEventListener('pointerleave', hideMapTip);
        map.append(bodyHover);
      }

      // Sites are spread by golden angle rather than in rings. Rings of a fixed
      // size put every twelfth node on the same spoke, which reads as spokes
      // rather than a cluster and stacks the labels on top of each other;
      // phyllotaxis fills the disc evenly at any count, and microTech alone has
      // over a hundred.
      sites.forEach((site, siteIndex) => {
        const spin = siteIndex * 2.39996;
        const distance = clusterRadius(siteIndex + 1);

        drawNode(
          map,
          bx + Math.cos(spin) * distance,
          by + Math.sin(spin) * distance,
          site,
          radiusFor(site.visits),
          { x: bx, y: by });
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
  fitToHighlights(term || null);
}

/**
 * @param anchor The body this site belongs to, if any. Labels are pushed away
 *   from it so a cluster fans its names outwards instead of stacking them.
 */
function drawNode(map, x, y, location, radius, anchor = null) {
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

  group.append(svgEl('circle', { cx: x, cy: y, r: radius + 8, fill: colour, opacity: '0', class: 'hit' }));

  // In shaded commodity mode the ring and dot carry the price grade; a lit
  // place UEX has no price for keeps the plain green ring.
  const shade = highlighted ? nodeShade.get(location.rawId) : null;

  if (highlighted) {
    group.append(svgEl('circle', {
      cx: x, cy: y, r: radius + 5, fill: 'none',
      stroke: shade?.colour ?? '#4fd48a', 'stroke-width': '1.6', class: 'hl-ring', filter: 'url(#glow)',
    }));
  }

  // Somewhere never visited is drawn as an outline, so the places that carry
  // history read as solid against the rest of the map. A price shade
  // overrides the kind colour - in that mode the colour IS the price.
  const dotColour = shade?.colour ?? colour;

  group.append(been || shade
    ? svgEl('circle', { cx: x, cy: y, r: radius, fill: dotColour, opacity: '.85' })
    : svgEl('circle', {
        cx: x, cy: y, r: radius, fill: 'none',
        stroke: dotColour, 'stroke-width': '1.1', opacity: '.42',
      }));

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
function placeLabels(map) {
  const size = labelSize();

  pendingLabels.sort((a, b) => b.priority - a.priority);

  for (const want of pendingLabels) {
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
      break;
    }
  }
}

function drawLegend(locations) {
  const legend = $('#map-legend');
  legend.textContent = '';

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
    swatch.style.background = '#4fd48a';
    plain.append(swatch);
    plain.append(el('span', null, 'no UEX price for it here'));
    legend.append(plain);
    return;
  }

  const kinds = [...new Set(locations.map((l) => l.kind))].sort();

  for (const kind of kinds) {
    const item = el('div', 'item');
    const swatch = el('span', 'swatch');
    swatch.style.background = KIND_COLOURS[kind] || KIND_COLOURS.Unknown;
    item.append(swatch);
    item.append(el('span', null, kind.replace(/([a-z])([A-Z])/g, '$1 $2')));
    legend.append(item);
  }
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
  if (isOverlay) document.body.classList.add('overlay');

  const requested = viewFromHash();
  if (requested) showView(requested);

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
  }

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
