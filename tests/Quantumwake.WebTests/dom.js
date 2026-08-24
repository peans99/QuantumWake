/* A DOM small enough to run web/app.js headlessly, and no smaller.
 *
 * The page is a single script against the browser's globals, so testing it at
 * all means providing those globals. This stub answers every selector with an
 * element rather than modelling the document: the assertions are about what the
 * code puts INTO the page - rows, classes, numbers, colours - not about how the
 * markup is nested, which index.html already decides.
 *
 * Anything the app cannot sensibly do without a browser (network, timers,
 * animation) is stubbed inert, so a test drives the app explicitly rather than
 * racing it.
 *
 * One thing it cannot answer: Jint settles an `await` by draining the job queue
 * where it stands, so a request is never *in flight* - the first caller's fetch
 * has already returned by the time the second one starts. Withholding a
 * response to arrange that hangs the engine rather than overlapping the
 * callers. Guards against two callers racing to fill the same thing are
 * therefore not testable here; assert the result, not the overlap. */

class ClassList {
  constructor() { this.set = new Set(); }
  add(...names) { names.forEach((n) => n && this.set.add(n)); }
  remove(...names) { names.forEach((n) => this.set.delete(n)); }
  contains(name) { return this.set.has(name); }
  toggle(name, on) {
    const wanted = on === undefined ? !this.set.has(name) : !!on;
    if (wanted) this.set.add(name); else this.set.delete(name);
    return wanted;
  }
  toString() { return [...this.set].join(' '); }
}

class El {
  constructor(tag, id) {
    this.tagName = String(tag || 'div').toLowerCase();
    this.id = id || '';
    this.children = [];
    this.attrs = {};
    this.dataset = {};
    this.style = {};
    this.classList = new ClassList();
    this.listeners = {};
    this.own = '';
    this.value = '';
    this.title = '';
    this.checked = false;
    this.hidden = false;
    this.disabled = false;
    this.tabIndex = 0;

    /* A file input's picked files. Empty until a test puts one there, as an
       empty picker is in a browser. */
    this.files = [];
    this.parentElement = null;
  }

  get className() { return this.classList.toString(); }
  set className(value) {
    this.classList.set = new Set(String(value || '').split(' ').filter(Boolean));
  }

  get options() { return this.children.filter((c) => c.tagName === 'option'); }

  /* Elements only, so a text node does not make an "is this list already
     filled?" guard answer yes. The app leans on that guard. */
  get childElementCount() { return this.children.filter((c) => c instanceof El).length; }

  get selectedOptions() {
    const chosen = this.options.find((o) => o.value === this.value);
    return chosen ? [chosen] : this.options.slice(0, 1);
  }

  get selectedIndex() { return this.options.findIndex((o) => o.value === this.value); }
  set selectedIndex(index) {
    const option = this.options[index];
    if (option) this.value = option.value;
  }

  get textContent() {
    return this.own + this.children.map((c) => (c && c.textContent) || '').join('');
  }

  set textContent(value) {
    this.children = [];
    this.own = value === undefined || value === null ? '' : String(value);
  }

  append(...nodes) {
    for (const node of nodes) {
      if (node instanceof El) node.parentElement = this;
      this.children.push(node);
    }
  }

  prepend(node) {
    if (node instanceof El) node.parentElement = this;
    this.children.unshift(node);
  }

  remove() {
    const parent = this.parentElement;
    if (!parent) return;
    parent.children = parent.children.filter((c) => c !== this);
    this.parentElement = null;
  }

  /* The class attribute and the class list are one thing, which matters here:
     the map is SVG, and SVG elements are classed with setAttribute. */
  setAttribute(name, value) {
    this.attrs[name] = String(value);
    if (name === 'class') this.className = value;
  }

  getAttribute(name) {
    if (name === 'class') return this.className;
    return Object.prototype.hasOwnProperty.call(this.attrs, name) ? this.attrs[name] : null;
  }
  removeAttribute(name) { delete this.attrs[name]; }
  addEventListener(type, handler) { (this.listeners[type] ||= []).push(handler); }
  removeEventListener() {}
  getBoundingClientRect() { return { left: 0, top: 0, width: 900, height: 700 }; }
  setPointerCapture() {}
  releasePointerCapture() {}
  scrollIntoView() {}
  focus() {}

  /* A synthetic click, as the download path does to an anchor it never adds to
     the document. Recorded so a test can assert what would have been saved -
     there is no downloads folder here, and the assertion worth making is about
     the name and the href, not about the file system. */
  click() {
    if (this.tagName === 'a' && this.download) {
      globalThis.__downloads.push({ name: this.download, href: this.href });
    }
    this.fire('click');
  }
  closest() { return null; }
  matches() { return false; }

  /**
   * Fires a listener the app attached, as a click or change would.
   *
   * The last handler's return value comes back so a test can await an async
   * one: half these handlers are submit handlers that post and re-render, and
   * asserting before that settles tests the moment before the answer.
   */
  fire(type, event) {
    let last;
    for (const handler of this.listeners[type] || []) {
      last = handler(event || { target: this, preventDefault() {}, stopPropagation() {} });
    }
    return last;
  }

  /** Every element beneath this one, for assertions. */
  descendants() {
    const out = [];
    for (const child of this.children) {
      if (!(child instanceof El)) continue;
      out.push(child, ...child.descendants());
    }
    return out;
  }

  /** Elements below here carrying a class, in document order. */
  byClass(name) {
    return this.descendants().filter((n) => n.classList.contains(name));
  }

  querySelectorAll(selector) {
    if (typeof selector === 'string' && selector.startsWith('.')) return this.byClass(selector.slice(1));
    return [];
  }

  querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
}

/* One element per selector, made on demand: the app asks for ids the tests do
   not care about, and a missing node would throw where the browser would not. */
const NODES = new Map();

function node(selector) {
  if (!NODES.has(selector)) {
    const el = new El(selector.includes('select') ? 'select' : 'div', selector.replace(/^#/, ''));
    NODES.set(selector, el);
  }

  return NODES.get(selector);
}

/** Selectors whose contents the app iterates, rather than merely writing to. */
const GROUPS = {
  'select.period': ['#map-window'],
  '#map-side button': ['#side-sell', '#side-buy'],
  '#view-now .card[data-card]': [
    '#now-location-card', '#now-briefing-card', '#now-ship-card', '#now-session-card',
    '#now-handle-card', '#now-feed-card', '#now-stats-card', '#now-respawn-card',
    '#now-job-card', '#now-checklist-card', '#now-trip-card', '#trade-advice-card',
  ],
};

globalThis.__dom = {
  node,
  reset() {
    NODES.clear();
    globalThis.__fetch.routes = {};
    globalThis.__fetch.calls = [];
    globalThis.__fetch.unreachable = [];
    globalThis.__fetch.headers = {};
    globalThis.__downloads = [];
  },
};

globalThis.Option = function Option(text, value) {
  const option = new El('option');
  option.textContent = text;
  option.value = value === undefined ? text : value;
  return option;
};

globalThis.document = {
  body: new El('body'),
  documentElement: new El('html'),
  createElement: (tag) => new El(tag),
  createElementNS: (ns, tag) => new El(tag),

  /* A text node is only ever read back through textContent, so it needs to be
     no more than something carrying one - and staying outside El is what keeps
     childElementCount counting elements. */
  createTextNode: (text) => ({ textContent: text === undefined || text === null ? '' : String(text) }),
  addEventListener() {},
  removeEventListener() {},

  querySelector(selector) {
    const group = GROUPS[selector];
    return group ? node(group[0]) : node(selector);
  },

  querySelectorAll(selector) {
    const group = GROUPS[selector];
    if (group) return group.map(node);

    // Everything else the app sweeps over is markup this stub does not model.
    return [];
  },
};

node('#side-sell').dataset.side = 'sell';
node('#side-buy').dataset.side = 'buy';

for (const [selector, card] of [
  ['#now-location-card', 'location'], ['#now-briefing-card', 'briefing'],
  ['#now-ship-card', 'ship'], ['#now-session-card', 'session'],
  ['#now-handle-card', 'handle'], ['#now-feed-card', 'feed'],
  ['#now-stats-card', 'stats'], ['#now-respawn-card', 'respawn'],
  ['#now-job-card', 'job'], ['#now-checklist-card', 'checklist'], ['#now-trip-card', 'trip'], ['#trade-advice-card', 'trade'],
]) node(selector).dataset.card = card;

/* Network: a routing table the test fills in, and a record of what was asked. */
globalThis.__fetch = { routes: {}, calls: [], unreachable: [], headers: {} };

globalThis.fetch = (url, options) => {
  globalThis.__fetch.calls.push({ url, method: (options && options.method) || 'GET', body: options && options.body });

  // A dropped connection rejects rather than answering with a status, and the
  // app's error paths are reached only by that shape - not by a 404.
  if (globalThis.__fetch.unreachable.includes(url))
    return Promise.reject(new Error(`unreachable: ${url}`));

  const body = Object.prototype.hasOwnProperty.call(globalThis.__fetch.routes, url)
    ? globalThis.__fetch.routes[url]
    : null;

  /* Headers a test asked for, keyed lowercase as a browser does. Only the ones
     a route was given: a response nobody described carries none. */
  const headers = globalThis.__fetch.headers[url] || {};
  const serialized = JSON.stringify(body);

  return Promise.resolve({
    ok: body !== null,
    status: body !== null ? 200 : 404,
    headers: { get: (name) => headers[String(name).toLowerCase()] ?? null },
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(serialized),
    blob: () => Promise.resolve(new Blob([serialized], { type: 'application/json' })),
  });
};

globalThis.window = globalThis;
globalThis.self = globalThis;
globalThis.location = { search: '', hash: '', href: 'http://localhost/', reload() {} };
globalThis.history = { replaceState() {} };
globalThis.navigator = { userAgent: 'quantumwake-tests', clipboard: { writeText: () => Promise.resolve() } };

/* Saving a file: enough of Blob and URL for the download path to run.
   Blob keeps its own text so a test can read what would have been written. */
globalThis.__downloads = [];

globalThis.Blob = class {
  constructor(parts = [], options = {}) {
    this.parts = parts;
    this.type = options.type || '';
    this.text = parts.map((p) => String(p)).join('');
    this.size = this.text.length;
  }
};

globalThis.URL = {
  objects: new Map(),
  next: 0,

  createObjectURL(blob) {
    const url = `blob:quantumwake/${globalThis.URL.next++}`;
    globalThis.URL.objects.set(url, blob);
    return url;
  },

  revokeObjectURL(url) { globalThis.URL.objects.delete(url); },
};

/* Reading a file the user picked. onload is called synchronously because the
   engine settles awaits where they stand anyway - see the note at the top. */
globalThis.FileReader = class {
  readAsText(file) {
    this.result = typeof file === 'string' ? file : (file && file.text) || '';
    if (this.onload) this.onload({ target: this });
  }
};

globalThis.localStorage = {
  store: {},
  getItem(key) { return Object.prototype.hasOwnProperty.call(this.store, key) ? this.store[key] : null; },
  setItem(key, value) { this.store[key] = String(value); },
  removeItem(key) { delete this.store[key]; },
};

globalThis.setTimeout = () => 0;
globalThis.clearTimeout = () => {};
globalThis.setInterval = () => 0;
globalThis.clearInterval = () => {};
globalThis.requestAnimationFrame = () => 0;
globalThis.cancelAnimationFrame = () => {};

globalThis.EventSource = class {
  constructor() { this.readyState = 0; }
  addEventListener() {}
  close() {}
};

globalThis.Event = class {
  constructor(type) { this.type = type; }
};

globalThis.console = {
  log: (...args) => host_log(args.join(' ')),
  info: () => {},
  warn: () => {},
  error: (...args) => host_log(`error: ${args.join(' ')}`),
  debug: () => {},
};

globalThis.URLSearchParams = class {
  constructor(search) {
    this.map = new Map();

    for (const pair of String(search || '').replace(/^\?/, '').split('&')) {
      if (!pair) continue;
      const [key, value = ''] = pair.split('=');
      this.map.set(decodeURIComponent(key), decodeURIComponent(value));
    }
  }

  has(key) { return this.map.has(key); }
  get(key) { return this.map.has(key) ? this.map.get(key) : null; }
};

/* The page listens on window for hashchange and keyboard shortcuts. Inert:
   a test drives the app by calling it, not by faking browser events. */
globalThis.addEventListener = () => {};
globalThis.removeEventListener = () => {};
globalThis.dispatchEvent = () => true;

/* Diagnostics: a page that will not settle is nearly always a poll loop whose
   timer never fires, so count what it keeps asking for. */
globalThis.__fetch.counts = {};

const countedFetch = globalThis.fetch;
globalThis.fetch = (url, options) => {
  const key = String(url).split('?')[0];
  const seen = (globalThis.__fetch.counts[key] || 0) + 1;
  globalThis.__fetch.counts[key] = seen;

  if (seen % 500 === 0) host_log(`${key} fetched ${seen} times`);

  return countedFetch(url, options);
};

globalThis.__error = null;
