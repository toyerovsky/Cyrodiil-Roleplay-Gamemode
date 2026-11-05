// One request out, one answer back by its id. A request the server does not answer in time goes again once, then
// resolves as a failure, so the page never waits for good.
export const ASK_TIMEOUT_MS = 6000;
export const PUSH_ID = 0;

export function createAsker(bridge, { timeoutMs = ASK_TIMEOUT_MS, noAnswer, badAnswer }) {
  const waiting = new Map();
  let nextId = PUSH_ID + 1;

  function ask(request, retries = 1) {
    return new Promise((resolve) => {
      const id = nextId++;
      const timer = setTimeout(() => {
        waiting.delete(id);
        if (retries > 0) resolve(ask(request, retries - 1));
        else resolve({ ok: false, message: noAnswer() });
      }, timeoutMs);
      waiting.set(id, { resolve, timer });
      bridge.ask(id, JSON.stringify(request));
    });
  }

  function answer(id, json) {
    const entry = waiting.get(id);
    if (!entry) return;
    clearTimeout(entry.timer);
    waiting.delete(id);
    try {
      entry.resolve(JSON.parse(json));
    } catch {
      entry.resolve({ ok: false, message: badAnswer() });
    }
  }

  // The window shut: what was asked is answered as nothing, and the page stops waiting.
  function drop() {
    for (const { resolve, timer } of waiting.values()) {
      clearTimeout(timer);
      resolve({ ok: false, message: "", dropped: true });
    }
    waiting.clear();
  }

  return { ask, answer, drop };
}
