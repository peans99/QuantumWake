/* SC Companion dashboard.
 *
 * No framework and no external requests: the page is served by the local
 * process and also loaded by the overlay's WebView2, so it stays dependency
 * free. Live updates arrive over Server-Sent Events, which every browser
 * supports natively. */

const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => Array.from(document.querySelectorAll(sel));

const KIND_COLOURS = {
  City: '#35c8f0',
  RestStop: '#4fd48a',
  Outpost: '#f0a935',
  Research: '#b58cf0',
  DistributionCentre: '#f0553f',
  JumpPoint: '#e8eef7',
  Mine: '#c78b4a',
  Asteroid: '#8fa0b6',
  Planet: '#7c8ba1',
  Moon: '#63718a',
  NavPoint: '#4d5a6d',
  MissionBeacon: '#f0a935',
  Unknown: '#4d5a6d',
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

/* ---------- tabs ---------- */

$('#tabs').addEventListener('click', (event) => {
  const button = event.target.closest('button');
  if (!button) return;

  $$('#tabs button').forEach((b) => b.classList.toggle('active', b === button));
  $$('.view').forEach((v) => v.classList.toggle('active', v.id === `view-${button.dataset.view}`));
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
  $('#now-incaps').textContent = state.incapacitations ?? 0;
  $('#now-kills').textContent = state.kills ?? 0;

  sessionStarted = state.sessionStarted || null;

  const feed = $('#now-feed');
  feed.textContent = '';

  if (!state.recentEvents || state.recentEvents.length === 0) {
    feed.append(el('li', 'empty', state.connected ? 'Nothing yet this session.' : 'Waiting for the game…'));
  } else {
    for (const entry of state.recentEvents) {
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

async function loadHistory() {
  const [stats, sessions] = await Promise.all([getJson('/api/stats'), getJson('/api/sessions')]);

  // Summary tiles.
  const strip = $('#lib-summary');
  strip.textContent = '';

  const tiles = [
    ['Sessions', stats.sessions],
    ['In game', duration(toSeconds(stats.inGameTime))],
    ['In menus', duration(toSeconds(stats.menuTime))],
    ['Ships flown', stats.ships.length],
    ['Places visited', stats.locations.length],
    ['Incapacitations', stats.incapacitations],
  ];

  for (const [label, value] of tiles) {
    const tile = el('div', 'tile');
    tile.append(el('div', 'n', String(value)));
    tile.append(el('div', 'l', label));
    strip.append(tile);
  }

  // Sessions table.
  const body = $('#sessions-table tbody');
  body.textContent = '';

  for (const session of sessions) {
    const tr = el('tr');
    const cells = [
      dateOf(session.startedAt),
      duration(session.inGame),
      duration(session.menu),
      session.primaryShip || '—',
      session.lastLocation || '—',
    ];
    cells.forEach((text) => tr.append(el('td', null, text)));
    [session.jumps, session.contracts, session.incapacitations].forEach((n) => tr.append(el('td', 'num', String(n))));
    body.append(tr);
  }

  // Flights lead, because Star Citizen 4.9 logs no seat-entry event: every
  // vehicle line is a control-token release. Time aboard is an estimate and is
  // shown as a secondary, clearly-approximate figure.
  bars('#ships-chart',
    stats.ships.slice(0, 20).map((s) => ({
      label: s.name,
      value: s.sorties,
      note: toSeconds(s.estimatedTime) > 0 ? `~${duration(toSeconds(s.estimatedTime))}` : null,
    })),
    (v) => `${v} flight${v === 1 ? '' : 's'}`);

  bars('#places-chart',
    stats.locations.slice(0, 20).map((l) => ({
      label: l.name, value: l.visits, colour: KIND_COLOURS[l.kind],
    })),
    (v) => `${v}`);

  bars('#dests-chart',
    stats.destinations.slice(0, 20).map((d) => ({ label: d.name, value: d.visits })),
    (v) => `${v}`);

  bars('#issuers-chart',
    stats.contractIssuers.slice(0, 15).map((c) => ({ label: c.name, value: c.count })),
    (v) => `${v}`);

  bars('#types-chart',
    stats.contractTypes.slice(0, 15).map((c) => ({ label: c.name, value: c.count })),
    (v) => `${v}`);

  drawMap(stats.locations);
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
      cx: centre.x, cy: centre.y, r: 9,
      fill: system === 'Stanton' ? '#ffd98a' : '#ff9a6e',
      opacity: '.85',
    }));
    map.append(svgEl('circle', { cx: centre.x, cy: centre.y, r: 165, class: 'map-orbit' }));

    const label = svgEl('text', { x: centre.x, y: centre.y + 205, 'text-anchor': 'middle', class: 'map-sys-label' });
    label.textContent = system;
    map.append(label);
  }

  // Jump-point link between the two systems.
  map.append(svgEl('path', {
    d: `M ${centres.Stanton.x + 170} 300 Q 540 240 ${centres.Pyro.x - 170} 300`,
    class: 'map-edge', 'stroke-width': '2', 'stroke-dasharray': '6 6',
  }));

  const jumpLabel = svgEl('text', { x: 540, y: 258, 'text-anchor': 'middle', class: 'map-label' });
  jumpLabel.textContent = 'jump point';
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
        stroke: '#141d29', 'stroke-width': '1',
      }));

      const bodyLabel = svgEl('text', {
        x: bx, y: by - 16, 'text-anchor': 'middle', class: 'map-label',
        style: 'fill:#93a3b8;font-size:11px',
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

/* ---------- boot ---------- */

async function boot() {
  if (new URLSearchParams(location.search).has('overlay')) {
    document.body.classList.add('overlay');
  }

  try {
    const install = await getJson('/api/install');
    $('#install').textContent = `${install.channel} · ${install.backups} logs`;
  } catch {
    $('#install').textContent = 'no install found';
  }

  connectStream();

  // The first scan may still be running; retry until sessions appear.
  for (let attempt = 0; attempt < 30; attempt++) {
    try {
      await loadHistory();
      const count = (await getJson('/api/sessions')).length;
      if (count > 0) break;
    } catch { /* server still warming up */ }
    await new Promise((resolve) => setTimeout(resolve, 2000));
  }
}

boot();
