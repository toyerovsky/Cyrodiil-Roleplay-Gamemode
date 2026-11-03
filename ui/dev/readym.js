// The game's readym for the browser preview: window.__readym.sent and .emit(name, ...args) drive it. As in the game, a
// name has one handler, the last one given.
export function installReadym() {
  const handlers = new Map();
  const senders = new Set();
  const sent = [];

  const control = {
    sent,
    emit(name, ...args) {
      handlers.get(name)?.(...args);
    },
    onSend(handler) {
      senders.add(handler);
    },
  };

  const base = {
    on(name, handler) {
      handlers.set(name, handler);
    },
  };

  window.readym = new Proxy(base, {
    get(target, property) {
      if (property in target) return target[property];
      if (typeof property !== "string" || !property.startsWith("send")) return undefined;
      const name = property.slice("send".length);
      return (...args) => {
        sent.push({ name, args });
        // Answers come later, as the game's would.
        setTimeout(() => senders.forEach((handler) => handler(name, args)), 0);
      };
    },
  });
  window.__readym = control;
  return control;
}
