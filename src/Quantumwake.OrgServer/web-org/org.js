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

function signInLink(returnTo) {
  return '/auth/login?return=' + encodeURIComponent(returnTo);
}

function ago(iso) {
  if (!iso) return 'never';
  const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 90) return 'just now';
  if (seconds < 5400) return Math.round(seconds / 60) + ' min ago';
  if (seconds < 129600) return Math.round(seconds / 3600) + ' h ago';
  return Math.round(seconds / 86400) + ' d ago';
}
