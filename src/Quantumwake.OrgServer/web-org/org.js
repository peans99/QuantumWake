// Shared plumbing for the org server's few pages. Everything drawn goes
// through textContent - these pages render text other people typed.

async function api(path, options) {
  const settings = Object.assign({ headers: {} }, options);
  // The header a cross-origin page cannot attach: proof the request came from
  // this server's own pages rather than a hostile tab riding the cookie.
  if (settings.method && settings.method !== 'GET') settings.headers['X-Qw-Org'] = '1';
  if (settings.body !== undefined) {
    settings.headers['Content-Type'] = 'application/json';
    settings.body = JSON.stringify(settings.body);
  }
  const response = await fetch(path, settings);
  let data = null;
  try { data = await response.json(); } catch { /* empty responses are fine */ }
  return { ok: response.ok, status: response.status, data };
}

function el(tag, text, className) {
  const node = document.createElement(tag);
  if (text !== undefined && text !== null) node.textContent = text;
  if (className) node.className = className;
  return node;
}

function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
}

function signInLink(returnTo, provider) {
  const which = provider ? 'provider=' + encodeURIComponent(provider) + '&' : '';
  return '/auth/login?' + which + 'return=' + encodeURIComponent(returnTo);
}

// Which doors this server has. Asked once and shared: four pages each wanting
// the answer is still one request.
let authAsked = null;
function authConfig() {
  if (!authAsked) {
    authAsked = api('/api/auth/providers')
      .then(r => (r.ok && r.data) ? r.data : { lanMode: false, providers: [] })
      .catch(() => ({ lanMode: false, providers: [] }));
  }
  return authAsked;
}

// One button per provider the server actually configured, so a page never
// offers a door that is not there. The LAN-mode banner is spliced in by the
// server; this only draws the choice of door, and in LAN mode there is none.
async function signInChoices(container, returnTo) {
  const config = await authConfig();
  if (config.lanMode) return false;

  if (!config.providers.length) {
    container.appendChild(el('p', 'No sign-in provider is configured on this server.', 'muted'));
    return false;
  }

  const row = el('div', null, 'row');
  for (const provider of config.providers) {
    const a = document.createElement('a');
    a.href = signInLink(returnTo, provider.key);
    a.appendChild(el('button', 'Sign in with ' + provider.name, 'primary'));
    row.appendChild(a);
  }
  container.appendChild(row);
  return true;
}

function ago(iso) {
  if (!iso) return 'never';
  const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 90) return 'just now';
  if (seconds < 5400) return Math.round(seconds / 60) + ' min ago';
  if (seconds < 129600) return Math.round(seconds / 3600) + ' h ago';
  return Math.round(seconds / 86400) + ' d ago';
}
