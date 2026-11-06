// The Jobs panel. In the game, ReadyM's CEF bridge supplies window.readym and JobsPanel.cs pushes the whole state as
// one JSON message (JobsState) whenever it changes and once a second:
//   { hidden, cursorEpoch, seq, title, damage, health, lines: [{ kind: 'gain' | 'note' | 'refusal', text }] }
// seq goes up with every hit or outcome; the plate shows then and fades FADE_AFTER_MS after the last one. The page
// sends back JobsReady, and JobsCursor whether the game shows its mouse cursor (one of its menus is open), so the
// game can take the viewport away while a menu wants the mouse.
//
// In a normal browser there is no readym, so a small mock plays the game: C chops, P power-attacks, M mines, R a
// refusal, Q a quarry hint.

(function () {
  'use strict';

  const FADE_AFTER_MS = 6000;
  const FADE_MS = 400;

  const $ = (id) => document.getElementById(id);
  const panel = $('panel');
  const title = $('title');
  const count = $('count');
  const fill = $('fill');
  const lines = $('lines');

  let lastSeq = -1;
  let fadeTimer = 0;
  let hideTimer = 0;
  let cursorEpoch = 0;
  let forgetCursor = () => {};

  function render(state) {
    if (typeof state.cursorEpoch === 'number' && state.cursorEpoch !== cursorEpoch) {
      cursorEpoch = state.cursorEpoch;
      forgetCursor();
    }
    if (state.hidden || !state.seq) {
      panel.classList.add('hidden');
      return;
    }

    const hasWork = !!state.title && state.health > 0;
    title.textContent = state.title || '';
    count.textContent = hasWork ? `${Math.min(state.damage, state.health)} / ${state.health}` : '';
    fill.style.width = hasWork ? `${Math.max(0, Math.min(1, state.damage / state.health)) * 100}%` : '0';
    panel.classList.toggle('lines-only', !hasWork);
    lines.replaceChildren(...(state.lines || []).map((line) => {
      const li = document.createElement('li');
      li.className = line.kind;
      li.textContent = line.text;
      if (line.kind === 'refusal') li.setAttribute('role', 'alert');
      return li;
    }));

    if (state.seq !== lastSeq) {
      lastSeq = state.seq;
      show();
    }
  }

  // Shows the plate at once and fades it FADE_AFTER_MS after the last change
  function show() {
    clearTimeout(fadeTimer);
    clearTimeout(hideTimer);
    panel.classList.remove('hidden', 'fading');
    fadeTimer = setTimeout(() => {
      panel.classList.add('fading');
      hideTimer = setTimeout(() => panel.classList.add('hidden'), FADE_MS);
    }, FADE_AFTER_MS);
  }

  // ---- the game ----

  function gameBridge() {
    const r = window.readym;
    const onState = (a, b) => {
      // JobsState carries (seq, json); accept a bare json too.
      const json = typeof b === 'string' ? b : a;
      try {
        render(JSON.parse(json));
      } catch (e) {
        console.error('[Jobs] bad state message', e);
      }
    };
    try {
      if (typeof r.onJobsState === 'function') r.onJobsState(onState);
      else r.on('JobsState', onState);
    } catch (e) {
      console.error('[Jobs] could not listen for JobsState', e);
    }
    const send = (name, ...args) => {
      try {
        r['send' + name](...args);
      } catch (e) {
        console.warn('[Jobs] send' + name + ' did not go through', e);
      }
    };
    watchCursor((shown) => send('JobsCursor', shown ? 1 : 0, cursorEpoch));
    return { ready: () => send('JobsReady') };
  }

  // Whether the game shows its mouse cursor, which it does exactly while one of its menus is open. Every page gets
  // the cursor each tick while it is shown through window.__readym_cursor, the SDK's hit-test hook, and (-1, -1) once
  // when it goes. This wraps the hook, keeps calling the SDK's own, and reports only changes. As Hunger's hud.js does.
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
      console.warn('[Jobs] could not watch the cursor; the panel stays up in menus', e);
    }
  }

  // ---- the browser preview ----

  function mockBridge() {
    document.documentElement.classList.add('preview');
    const stage = document.createElement('div');
    stage.className = 'stage';
    panel.replaceWith(stage);
    stage.append(panel);

    const s = { hidden: false, cursorEpoch: 0, seq: 0, title: '', damage: 0, health: 0, lines: [] };
    const push = () => { s.seq++; render(JSON.parse(JSON.stringify(s))); };
    const hit = (what, dealt, health) => {
      if (s.title !== what) s.damage = 0;
      s.title = what;
      s.health = health;
      s.damage = Math.min(health, s.damage + dealt);
      s.lines = [];
      if (s.damage >= health) {
        s.lines = what.endsWith('vein')
          ? [{ kind: 'gain', text: '+3 Iron Ore' }, { kind: 'gain', text: '+1 Flawed Topaz' }]
          : [{ kind: 'gain', text: '+3 Gnarl Bark' }];
        push();
        s.damage = 0;
        return;
      }
      push();
    };
    document.addEventListener('keydown', (e) => {
      switch (e.key.toLowerCase()) {
        case 'c': hit('Oak', 1, 20); break;
        case 'p': hit('Oak', 5, 20); break;
        case 'm': hit('Iron vein', 5, 30); break;
        case 'r':
          s.title = ''; s.health = 0;
          s.lines = [{ kind: 'refusal', text: 'Only a battle axe fells a tree.' }];
          push();
          break;
        case 'q':
          s.title = ''; s.health = 0;
          s.lines = [{ kind: 'note', text: "This stone won't split cleanly. Quarries are marked on your map." }];
          push();
          break;
        default:
          return;
      }
    });

    const params = new URLSearchParams(location.search);
    const note = document.createElement('div');
    note.className = 'preview-note';
    note.textContent = 'Browser preview, the dashed box is the panel\'s viewport. C chops (1), P power-attacks (5), '
      + 'M mines, R a refusal, Q a hint. ?felled, ?mined, ?refusal or ?hint start there.';
    document.body.append(note);

    return {
      ready: () => {
        if (params.has('felled')) { hit('Oak', 15, 20); hit('Oak', 5, 20); }
        else if (params.has('mined')) { hit('Iron vein', 25, 30); hit('Iron vein', 5, 30); }
        else if (params.has('refusal')) document.dispatchEvent(new KeyboardEvent('keydown', { key: 'r' }));
        else if (params.has('hint')) document.dispatchEvent(new KeyboardEvent('keydown', { key: 'q' }));
        else hit('Oak', 12, 20);
      },
    };
  }

  const bridge = typeof window.readym === 'object' && window.readym !== null ? gameBridge() : mockBridge();
  bridge.ready();
})();
