import { createApp } from "vue";
import "../rp/index.js";
import "./scale.css";

const PREVIEW_BACKDROP = "#3f4a3a";

export async function startPage(App, createBridge, loadPretendGame) {
  if (import.meta.env.DEV && !window.readym) {
    // There is no game scene behind a page in a browser.
    document.documentElement.style.background = PREVIEW_BACKDROP;
    const { installReadym } = await import("../dev/readym.js");
    const readym = installReadym();
    const { pretendGame } = await loadPretendGame();
    pretendGame(readym);
  }

  createApp(App, { bridge: createBridge(window.readym) }).mount("#root");
}
