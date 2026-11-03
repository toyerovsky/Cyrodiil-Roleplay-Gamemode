import { flushPromises, mount } from "@vue/test-utils";

// The game's readym as a page's bridge sees it: one handler a name, and every sendX kept.
function fakeReadym() {
  const handlers = new Map();
  const sent = [];
  const readym = new Proxy({ on: (name, handler) => handlers.set(name, handler) }, {
    get(target, property) {
      if (property in target) return target[property];
      if (typeof property !== "string" || !property.startsWith("send")) return undefined;
      return (...args) => sent.push([property.slice("send".length), ...args]);
    },
  });

  return {
    readym,
    sent,
    async emit(name, ...args) {
      handlers.get(name)?.(...args);
      await flushPromises();
    },
    readSent: (name) => sent.filter(([sentName]) => sentName === name).map(([, ...args]) => args),
  };
}

export async function mountPage(App, createBridge) {
  const game = fakeReadym();
  const page = mount(App, { props: { bridge: createBridge(game.readym) }, attachTo: document.body });
  await flushPromises();
  return { game, page };
}
