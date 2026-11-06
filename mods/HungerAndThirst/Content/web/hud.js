// Hunger and Thirst pages. The game loads hud.html (the bars) and satchel.html (the satchel), each in its own
// viewport, and both run this script: each finds only its own part of the page and leaves the rest alone. index.html
// has both, for the browser preview. In game, ReadyM's CEF bridge supplies window.readym and SustenancePage.cs pushes
// the whole state as one JSON message (HatState) to both pages whenever it changes and once a second. The satchel
// sends back Ready, Use (Enter on a row: a form id, or 0 for the water), Close (Esc) and LostFocus; both pages send
// Cursor, whether the game shows its mouse cursor (one of its menus is open).
//
// In a normal browser there is no readym, so a small mock plays the game and the server: the meters fall quickly,
// H opens the satchel and Enter eats. Add ?open to start with the satchel open, ?low for low meters.

(function () {
  'use strict';

  const MAX = 1000;
  const LOW = 500;       // SustenanceScale.Low
  const CRITICAL = 250;  // SustenanceScale.Critical

  const $ = (id) => document.getElementById(id);
  const hud = $('hud');
  const satchel = $('satchel');
  const list = $('items');
  const empty = $('empty');
  const message = $('message');
  const outlook = $('outlook');

  // A drop bigger than this (points) leaves a trail; slow decay just moves the fill.
  const TRAIL_MIN = 8;
  // How long the trail holds before sliding after the fill, in ms.
  const TRAIL_HOLD = 600;

  let state = null;
  let cursorEpoch = 0;              // the game's cursorEpoch: a new one means "forget what you reported"
  let forgetCursor = () => {};      // set by watchCursor
  let selected = null; // the id of the selected row, kept across updates
  const meters = {};   // per meter: the value last drawn, where the trail is, and its pending timer

  // ---- drawing ----

  function render(next) {
    state = next;
    if (typeof next.cursorEpoch === 'number' && next.cursorEpoch !== cursorEpoch) {
      cursorEpoch = next.cursorEpoch;
      forgetCursor();
    }
    if (hud) {
      hud.classList.toggle('hidden', !state.ready || !!state.hudHidden);
      meter('satiety', state.satiety);
      meter('hydration', state.hydration);
    }
    if (!satchel) return;

    const open = state.satchel.open;
    satchel.classList.toggle('hidden', !open);
    if (!open) return;

    const rows = rowsOf(state.satchel);
    if (!rows.some((row) => row.id === selected)) selected = rows.length ? rows[0].id : null;
    list.replaceChildren(...rows.map(row));
    empty.classList.toggle('hidden', rows.length > 0);
    message.textContent = state.satchel.message || '';
    outlook.textContent = [
      until('Hungry', state.satiety, state.satietyRate),
      until('Thirsty', state.hydration, state.hydrationRate),
    ].filter(Boolean).join(' · ');
    const current = list.querySelector('.selected');
    if (current) current.scrollIntoView({ block: 'nearest' });
  }

  // Draws a meter: the fill drains from the right like fatigue. A sudden drop leaves a lighter trail at the old
  // value, which slides after the fill once TRAIL_HOLD has passed. Going up (a meal) moves the trail with it.
  function meter(id, value) {
    const el = $(id);
    const m = meters[id] || (meters[id] = { trail: value, timer: 0 });
    const width = (v) => `${(Math.max(0, Math.min(MAX, v)) / MAX) * 100}%`;

    el.querySelector('.fill').style.width = width(value);
    if (value >= m.trail || m.trail - value < TRAIL_MIN && !m.timer) {
      m.trail = value;
    } else if (!m.timer) {
      m.timer = setTimeout(() => {
        m.timer = 0;
        m.trail = meters[id].value;
        el.querySelector('.trail').style.width = width(m.trail);
      }, TRAIL_HOLD);
    }
    m.value = value;
    el.querySelector('.trail').style.width = width(m.trail);
    el.classList.toggle('low', value < LOW && value >= CRITICAL);
    el.classList.toggle('critical', value < CRITICAL);
  }

  function rowsOf(s) {
    const rows = s.items.slice();
    if (s.water) rows.unshift({ id: 0, name: 'Drink from the water', count: 0, satiety: 0, hydration: -1 });
    return rows;
  }

  function row(item) {
    const li = document.createElement('li');
    if (item.id === selected) li.className = 'selected';
    const name = document.createElement('span');
    name.className = 'name';
    name.textContent = item.name;
    const count = document.createElement('span');
    count.className = 'count';
    count.textContent = item.count > 0 ? `×${item.count}` : '';
    const gives = document.createElement('span');
    gives.className = 'gives';
    if (item.hydration < 0) {
      gives.innerHTML = '<span class="water">Quenches thirst</span>';
    } else {
      const parts = [];
      if (item.satiety > 0) parts.push(`<span class="food">+${percent(item.satiety)} food</span>`);
      if (item.hydration > 0) parts.push(`<span class="water">+${percent(item.hydration)} drink</span>`);
      gives.innerHTML = parts.join(' &nbsp; ');
    }
    li.append(name, count, gives);
    return li;
  }

  const percent = (points) => `${Math.round((points / MAX) * 100)}%`;

  // "Hungry in ~40 min" while above the warning line, "Hungry now" below it.
  function until(label, value, perMinute) {
    if (value < LOW) return `${label} now`;
    if (!(perMinute > 0)) return '';
    const minutes = Math.round((value - LOW) / perMinute);
    return minutes >= 120 ? `${label} in ~${Math.round(minutes / 60)} h` : `${label} in ~${Math.max(1, minutes)} min`;
  }

  // ---- keys (only while the satchel has the keyboard) ----

  document.addEventListener('keydown', (e) => {
    if (!satchel || !state || !state.satchel.open) return;
    const rows = rowsOf(state.satchel);
    const at = rows.findIndex((r) => r.id === selected);
    switch (e.key) {
      case 'ArrowUp':
      case 'ArrowDown': {
        if (!rows.length) break;
        const step = e.key === 'ArrowUp' ? -1 : 1;
        selected = rows[(Math.max(at, 0) + step + rows.length) % rows.length].id;
        render(state);
        break;
      }
      case 'Home':
        if (rows.length) { selected = rows[0].id; render(state); }
        break;
      case 'End':
        if (rows.length) { selected = rows[rows.length - 1].id; render(state); }
        break;
      case 'Enter':
        if (selected !== null) bridge.use(selected);
        break;
      case 'Escape':
        bridge.close();
        break;
      default:
        return;
    }
    e.preventDefault();
  });

  window.addEventListener('blur', () => {
    if (satchel && state && state.satchel.open) bridge.lostFocus();
  });

  // ---- the game ----

  function gameBridge() {
    const r = window.readym;
    const onState = (a, b) => {
      // HatState carries (seq, json); accept a bare json too.
      const json = typeof b === 'string' ? b : a;
      try {
        render(JSON.parse(json));
      } catch (e) {
        console.error('[HungerAndThirst] bad state message', e);
      }
    };
    try {
      if (typeof r.onHatState === 'function') r.onHatState(onState);
      else r.on('HatState', onState);
    } catch (e) {
      console.error('[HungerAndThirst] could not listen for HatState', e);
    }
    const send = (name, ...args) => {
      try {
        r['send' + name](...args);
      } catch (e) {
        console.warn('[HungerAndThirst] send' + name + ' did not go through', e);
      }
    };
    watchCursor((shown) => send('HatCursor', shown ? 1 : 0, cursorEpoch));
    return {
      ready: () => send('HatReady'),
      use: (id) => send('HatUse', id),
      close: () => send('HatClose'),
      lostFocus: () => send('HatLostFocus'),
    };
  }

  // ---- whether the game shows its mouse cursor ----
  //
  // The game shows it exactly while one of its menus is open (inventory, containers, map, dialogue, pause), and SDK 1.0
  // gives mods no other sign of a menu. Every page gets the cursor each tick while it is shown, through
  // window.__readym_cursor, the SDK's own hit-test hook, and is told once with (-1, -1) when it goes. This wraps that
  // hook, keeps calling the SDK's function, and reports only changes. The SDK may install its function before or
  // after this script runs, so the property is taken over with a setter that keeps whichever it installs. After a load
  // the game sends a new cursorEpoch and this forgets its last report, so the next call reports afresh.
  function watchCursor(report) {
    let inner = window.__readym_cursor;
    let shown = null;
    forgetCursor = () => { shown = null; };
    function wrapper(x, y) {
      const now = !(x === -1 && y === -1);
      if (now !== shown) {
        shown = now;
        report(now);
      }
      if (typeof inner === 'function') return inner.apply(this, arguments);
      return undefined;
    }
    try {
      Object.defineProperty(window, '__readym_cursor', {
        configurable: true,
        get: () => wrapper,
        set: (fn) => { inner = fn; },
      });
    } catch (e) {
      console.warn('[HungerAndThirst] could not watch the cursor; the bars stay up in menus', e);
    }
  }

  // ---- the browser preview ----

  function mockBridge() {
    const params = new URLSearchParams(location.search);
    const foods = [
      { id: 1, name: 'Apple', count: 3, satiety: 60, hydration: 30 },
      { id: 2, name: 'Bread Loaf', count: 1, satiety: 180, hydration: 0 },
      { id: 3, name: 'Cheese Wedge', count: 2, satiety: 100, hydration: 0 },
      { id: 4, name: 'Ale', count: 2, satiety: 30, hydration: 200 },
      { id: 5, name: 'Venison', count: 1, satiety: 220, hydration: 0 },
    ];
    const s = {
      ready: true,
      satiety: params.has('low') ? 140 : 720,
      hydration: params.has('low') ? 60 : 430,
      satietyRate: 5.6,
      hydrationRate: 8.3,
      satchel: { open: params.has('open'), water: true, message: '', items: [] },
    };
    let messageUntil = 0;
    const push = () => {
      s.satchel.items = s.satchel.open ? foods.filter((f) => f.count > 0) : [];
      if (Date.now() > messageUntil) s.satchel.message = '';
      render(JSON.parse(JSON.stringify(s)));
    };
    const say = (text) => { s.satchel.message = text; messageUntil = Date.now() + 3000; };
    setInterval(() => {
      // Sped up 60x so the bars visibly move in the preview.
      s.satiety = Math.max(0, s.satiety - s.satietyRate);
      s.hydration = Math.max(0, s.hydration - s.hydrationRate);
      push();
    }, 1000);
    document.addEventListener('keydown', (e) => {
      const key = e.key.toLowerCase();
      if (key === 'h') { s.satchel.open = !s.satchel.open; push(); }
      // A sudden loss, to see the trail.
      if (key === 'x' && !s.satchel.open) {
        s.satiety = Math.max(0, s.satiety - 150);
        s.hydration = Math.max(0, s.hydration - 150);
        push();
      }
    });
    const note = document.createElement('div');
    note.className = 'preview-note';
    note.textContent = 'Browser preview. H opens the satchel, arrows and Enter eat, Esc closes, X takes a bite out of '
      + 'both bars to show the trail. Meters fall 60x faster.';
    document.body.append(note);
    document.body.style.background = 'linear-gradient(160deg, #4a5a3a, #23301f 60%, #1a1a14)';
    return {
      ready: () => push(),
      use: (id) => {
        if (id === 0) {
          s.hydration = Math.min(MAX, s.hydration + 150);
          say('You have some water.');
        } else {
          const food = foods.find((f) => f.id === id);
          if (!food || food.count < 1) return;
          food.count -= 1;
          s.satiety = Math.min(MAX, s.satiety + food.satiety);
          s.hydration = Math.min(MAX, s.hydration + food.hydration);
          say(`You have some ${food.name}.`);
        }
        push();
      },
      close: () => { s.satchel.open = false; push(); },
      lostFocus: () => {},
    };
  }

  const bridge = typeof window.readym === 'object' && window.readym !== null ? gameBridge() : mockBridge();
  bridge.ready();
})();
