/* SC Companion dashboard.
 *
 * No framework and no external requests: the page is served by the local
 * process and also loaded by the overlay's WebView2, so it stays dependency
 * free. Live updates arrive over Server-Sent Events, which every browser
 * supports natively. */

const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => Array.from(document.querySelectorAll(sel));

/** True when hosted in the overlay shell, which wants a denser layout. */
const isOverlay = new URLSearchParams(location.search).has('overlay');

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
  const buttons = $$('#tabs button');
  const target = buttons.find((b) => b.dataset.view === name);
  if (!target) return;

  buttons.forEach((b) => b.classList.toggle('active', b === target));
  $$('.view').forEach((v) => v.classList.toggle('active', v.id === `view-${name}`));

  // Keep the active tab in view when the strip scrolls, as it does in overlay mode.
  target.scrollIntoView({ block: 'nearest', inline: 'center' });
}

$('#tabs').addEventListener('click', (event) => {
  const button = event.target.closest('button');
  if (button) showView(button.dataset.view);
});

/* Driven by the overlay shell's global hotkeys, so views can be changed without
   unlocking click-through. Also bound to the arrow keys for browser use. */
window.scCycleView = (delta) => {
  const views = $$('#tabs button').map((b) => b.dataset.view);
  const current = $$('#tabs button').findIndex((b) => b.classList.contains('active'));
  const next = (current + delta + views.length) % views.length;
  showView(views[next]);
};

window.scShowView = showView;

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
    wrapper.append(el('div', 'label', row.label));

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
  safeRender('Map', () => drawMap(stats.locations));
  safeRender('Contracts', () => renderContracts(stats));
  safeRender('Places', () => renderPlaces(stats));

  // These fetch their own data, so they are kicked off rather than awaited.
  loadLedger().catch((e) => console.error('ledger', e));
  loadCommodities().catch((e) => console.error('cargo', e));
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
      label: l.name, value: l.visits, colour: KIND_COLOURS[l.kind],
    })),
    (v) => `${v}`);

  bars('#dests-chart',
    stats.destinations.filter((d) => match(d.name)).slice(0, 25)
      .map((d) => ({ label: d.name, value: d.visits })),
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
    tr.append(el('td', null, entry.where));
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
}

function renderCommodities(trades) {
  const sells = trades.filter((t) => t.isSell);
  const buys = trades.filter((t) => !t.isSell);

  const revenue = sells.reduce((total, t) => total + Number(t.amount), 0);
  const scuSold = sells.reduce((total, t) => total + t.scu, 0);
  const outlay = buys.reduce((total, t) => total + Number(t.amount), 0);

  tiles('#cargo-summary', [
    ['Revenue', money(revenue)],
    ['SCU sold', scuSold.toLocaleString()],
    ['Average per SCU', scuSold ? money(revenue / scuSold) : '—'],
    ['Cargo bought', money(outlay)],
    ['Sales', sells.length],
    ['Best sale', sells.length ? money(Math.max(...sells.map((t) => Number(t.amount)))) : '—'],
  ]);

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
    td.colSpan = 6;
    tr.append(td);
    body.append(tr);
    return;
  }

  for (const trade of trades) {
    const tr = el('tr');
    tr.append(el('td', null, dateOf(trade.at)));
    tr.append(el('td', null, trade.isSell ? 'Sold' : 'Bought'));
    tr.append(el('td', null, trade.place));
    tr.append(el('td', 'num', String(trade.scu)));
    tr.append(el('td', `num ${trade.isSell ? 'inward' : 'outward'}`, money(trade.amount)));
    tr.append(el('td', 'num muted', money(trade.unitPrice)));
    body.append(tr);
  }
}

/* ---------- shared widgets ---------- */

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

  tiles('#fleet-summary', [
    ['Ships owned', stats.fleetSize ?? '—'],
    ['Models flown', stats.ships.length],
    ['Total flights', stats.ships.reduce((sum, s) => sum + s.sorties, 0)],
    ['Time aboard', `~${duration(stats.ships.reduce((sum, s) => sum + toSeconds(s.estimatedTime), 0))}`],
  ]);

  drawFleetChart(stats.fleetHistory || []);
  renderFleetShips();
}

/** Applies the search box and the last-flown period filter. */
function renderFleetShips() {
  const grid = $('#fleet-ships');
  grid.textContent = '';

  if (!libraryStats) return;

  const term = ($('#fleet-search').value || '').trim().toLowerCase();
  const days = Number($('#fleet-period').value) || 0;
  const cutoff = days ? Date.now() - days * 86400000 : null;

  const ships = libraryStats.ships.filter((s) => {
    if (term && !s.name.toLowerCase().includes(term)) return false;
    if (cutoff && new Date(s.lastFlown).getTime() < cutoff) return false;
    return true;
  });

  if (!ships.length) {
    grid.append(el('p', 'muted',
      libraryStats.ships.length ? 'No ships match that filter.' : 'No ships recorded yet.'));
    return;
  }

  for (const ship of ships) {
    // "DRAK Clipper" -> prefix + model.
    const [prefix, ...rest] = ship.name.split(' ');
    const card = el('article', 'ship-card');

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
    card.append(body);
    grid.append(card);
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
      const card = el('article', 'card');

      const label = slot.slotCount > 1
        ? `${slot.label} · ${slot.slotCount} slots`
        : slot.label || slot.port;

      card.append(el('div', 'card-label', label));

      // What is in the family now; the churn goes behind a toggle.
      for (const item of slot.items) {
        const line = el('div', 'slot-current');
        line.append(el('span', null, prettyItem(item.name)));
        if (item.count > 1) line.append(el('span', 'slot-multi', ` ×${item.count}`));
        card.append(line);
      }

      if (slot.currentSeen)
        card.append(el('div', 'slot-when', `equipped ${relative(slot.currentSeen)}`));

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
    card.append(el('div', 'card-label', place.name));
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
function drawMap(locations) {
  const map = $('#map');
  map.textContent = '';

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

  const centres = { Stanton: { x: 290, y: 300 }, Pyro: { x: 790, y: 300 } };
  const grouped = { Stanton: new Map(), Pyro: new Map(), other: [] };

  for (const location of locations) {
    if (location.system && grouped[location.system]) {
      const key = location.body || '—';
      if (!grouped[location.system].has(key)) grouped[location.system].set(key, []);
      grouped[location.system].get(key).push(location);
    } else {
      grouped.other.push(location);
    }
  }

  const maxVisits = Math.max(1, ...locations.map((l) => l.visits));
  const radiusFor = (visits) => 4 + Math.sqrt(visits / maxVisits) * 13;

  // Stars, orbit rings and system labels.
  for (const [system, centre] of Object.entries(centres)) {
    map.append(svgEl('circle', {
      cx: centre.x, cy: centre.y, r: 8,
      fill: system === 'Stanton' ? '#ffdc9a' : '#ff8f66',
      filter: 'url(#glow)',
    }));
    map.append(svgEl('circle', { cx: centre.x, cy: centre.y, r: 165, class: 'map-orbit' }));

    // Reticle ticks around each star, echoing the in-game starmap.
    for (let tick = 0; tick < 4; tick++) {
      const angle = (tick / 4) * Math.PI * 2 + Math.PI / 4;
      map.append(svgEl('line', {
        x1: centre.x + Math.cos(angle) * 20, y1: centre.y + Math.sin(angle) * 20,
        x2: centre.x + Math.cos(angle) * 28, y2: centre.y + Math.sin(angle) * 28,
        stroke: '#1e4763', 'stroke-width': '1',
      }));
    }

    const label = svgEl('text', { x: centre.x, y: centre.y + 205, 'text-anchor': 'middle', class: 'map-sys-label' });
    label.textContent = system;
    map.append(label);
  }

  // Jump-point link between the two systems.
  map.append(svgEl('path', {
    d: `M ${centres.Stanton.x + 170} 300 Q 540 238 ${centres.Pyro.x - 170} 300`,
    class: 'map-edge', 'stroke-width': '1.5', 'stroke-dasharray': '5 7', filter: 'url(#glow)',
  }));

  map.append(svgEl('rect', {
    x: 534, y: 262, width: 12, height: 12,
    fill: 'none', stroke: '#35c8f0', 'stroke-width': '1.2',
    transform: 'rotate(45 540 268)', filter: 'url(#glow)',
  }));

  const jumpLabel = svgEl('text', { x: 540, y: 252, 'text-anchor': 'middle', class: 'map-label' });
  jumpLabel.textContent = 'JUMP POINT';
  map.append(jumpLabel);

  // Bodies and their locations.
  for (const [system, bodies] of Object.entries(grouped)) {
    if (system === 'other') continue;

    const centre = centres[system];
    const order = SYSTEMS[system] || [];
    const present = [...bodies.keys()].sort((a, b) => {
      const ia = order.indexOf(a);
      const ib = order.indexOf(b);
      return (ia === -1 ? 99 : ia) - (ib === -1 ? 99 : ib);
    });

    present.forEach((bodyName, index) => {
      const angle = (index / Math.max(1, present.length)) * Math.PI * 2 - Math.PI / 2;
      const bx = centre.x + Math.cos(angle) * 165;
      const by = centre.y + Math.sin(angle) * 165;

      map.append(svgEl('line', {
        x1: centre.x, y1: centre.y, x2: bx, y2: by,
        stroke: 'rgba(53,200,240,.13)', 'stroke-width': '1',
      }));

      const bodyLabel = svgEl('text', {
        x: bx, y: by - 16, 'text-anchor': 'middle', class: 'map-label',
        style: 'fill:#7796b0;font-size:10.5px;letter-spacing:.14em;text-transform:uppercase',
      });
      bodyLabel.textContent = bodyName === '—' ? '' : bodyName;
      map.append(bodyLabel);

      const sites = bodies.get(bodyName);
      sites.forEach((site, siteIndex) => {
        const spread = (siteIndex - (sites.length - 1) / 2) * 0.55;
        const sx = bx + Math.cos(angle + Math.PI / 2) * spread * 26;
        const sy = by + Math.sin(angle + Math.PI / 2) * spread * 26 + (siteIndex % 2 ? 14 : -2);
        drawNode(map, sx, sy, site, radiusFor(site.visits));
      });
    });
  }

  // Anything the resolver could not place: shown, never dropped.
  grouped.other.forEach((location, index) => {
    const x = 60 + (index % 12) * 78;
    const y = 560 + Math.floor(index / 12) * 30;
    drawNode(map, x, y, location, radiusFor(location.visits));
  });

  drawLegend(locations);
}

function drawNode(map, x, y, location, radius) {
  const colour = KIND_COLOURS[location.kind] || KIND_COLOURS.Unknown;
  const group = svgEl('g', { class: 'map-node' });

  group.append(svgEl('circle', { cx: x, cy: y, r: radius + 8, fill: colour, opacity: '0', class: 'hit' }));
  group.append(svgEl('circle', { cx: x, cy: y, r: radius, fill: colour, opacity: '.85' }));

  const title = svgEl('title');
  title.textContent = `${location.name} — ${location.visits} visit${location.visits === 1 ? '' : 's'}`;
  group.append(title);

  if (radius > 7) {
    const label = svgEl('text', { x, y: y + radius + 11, 'text-anchor': 'middle', class: 'map-label' });
    label.textContent = location.name.length > 22 ? `${location.name.slice(0, 21)}…` : location.name;
    group.append(label);
  }

  map.append(group);
}

function drawLegend(locations) {
  const kinds = [...new Set(locations.map((l) => l.kind))].sort();
  const legend = $('#map-legend');
  legend.textContent = '';

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

async function boot() {
  if (isOverlay) document.body.classList.add('overlay');

  try {
    const install = await getJson('/api/install');
    $('#install').textContent = `${install.channel} · ${install.backups} logs`;
  } catch {
    $('#install').textContent = 'no install found';
  }

  connectStream();
  watchScan();

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
