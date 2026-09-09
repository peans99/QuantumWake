/* Shared by the placement editor, display and tests. Coordinates are monitor-local pixels. */
'use strict';
window.QwMfd = (() => {
  const clamp = (n, min, max) => Math.max(min, Math.min(max, n));
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
  const pages = ['NAV', 'TASK', 'STATUS'];
  function action(button) {
    if (!Number.isInteger(button)) return null;
    if (button >= 1 && button <= pages.length) return { page: button - 1 };
    return ({ 6: { text: 1 }, 7: { text: -1 }, 11: { cycle: 1 }, 12: { scroll: 1 },
      13: { page: 0 }, 14: { scroll: -1 }, 15: { cycle: -1 },
      19: { brightness: -1 }, 20: { brightness: 1 } })[button] || null;
  }
  function rows(page, state, briefing, briefingUnavailable = false) {
    const s = state || {};
    const stop = briefing?.stops?.[0];
    switch (page) {
      case 0: return [
        ['LOCATION', s.location || 'Location not yet identified'],
        [s.travelling ? 'QUANTUM DESTINATION' : (briefingUnavailable ? 'PLAN UNAVAILABLE' : 'NEXT PLANNED STOP'),
          s.travelling ? (s.travellingTo || 'Destination not identified')
            : briefingUnavailable ? 'Open the dashboard to check your route.'
              : !briefing ? 'Loading your tracked plan…' : stop?.place || 'No outstanding stop in the tracked plan'],
        ['SHIP', s.ship || 'No ship identified in the logs'],
        ['LOCATION SOURCE', s.location ? `${s.confidence || 'Unknown'} confidence · game logs` : 'Waiting for a location signal']
      ];
      case 1: {
        if (briefingUnavailable) return [['PLAN UNAVAILABLE', 'Cannot read the flight plan. Check the dashboard connection.']];
        if (!briefing) return [['FLIGHT PLAN', 'Loading your tracked plan…']];
        if (!stop) return [['NO OUTSTANDING STOP', 'Track a flight plan in the dashboard to show the next task here.']];
        const next = (stop.actions || []).find(a => !a.done);
        const amount = next?.quantity != null ? `${next.quantity}${next.unit ? ' ' + next.unit : ''}` : '';
        return [['NEXT STOP', stop.place], ['NEXT ACTION', next
          ? [next.kind, amount, next.text].filter(Boolean).join(' · ') : stop.note || 'Travel to this stop'],
          ['PLAN', briefing.tripTitle || 'Tracked flight plan'],
          ['PLAN ONLY', 'Not a detected cargo manifest.']];
      }
      case 2: return [
        ['SESSION', s.inGame ? (s.gameRules || 'In game') : 'Menus / no active game session'],
        ['PILOT', s.handle || 'Waiting for pilot identity'],
        ...(s.screen ? [['LAST SCREENSHOT', s.screen.summary, s.screen.shotAt]]
          : [['NO SCREENSHOT READING', 'Read a screenshot in the dashboard to add a cross-check.']]),
        ['INSTRUMENTS', 'Screenshot readings are saved observations. Live shields, fuel and power are not supplied.']
      ];
      default: return [];
    }
  }
  return { fit, move, extent, action, pages, rows, clamp };
})();
