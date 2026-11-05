import { fileURLToPath } from "node:url";
import vue from "@vitejs/plugin-vue";
import pxToRem from "postcss-pxtorem";
import { defineConfig } from "vite";
import { PAGES } from "./pages.js";
import { diceModels } from "./scripts/dice-models.mjs";

const repo = fileURLToPath(new URL("..", import.meta.url));

// The game's Chromium reads woff2: the woff copy @fontsource names beside it would only double what players download.
const FONTSOURCE_CSS = /\/@fontsource\/.+\.css(\?|$)/;
const WOFF_SOURCE = /,\s*url\([^)]*\)\s*format\(\s*["']woff["']\s*\)/g;
function woff2Only() {
  return {
    name: "woff2-only",
    enforce: "pre",
    transform: (code, id) => (FONTSOURCE_CSS.test(id) ? code.replace(WOFF_SOURCE, "") : null),
  };
}

const DICE_PAGE = "dice";

const ROOT_PX = 16;
// Hairlines stay 1px, crisp.
const SMALLEST_SCALED_PX = 2;

export default defineConfig({
  // Relative, because the game serves the pages from readym://rpui/<page>/index.html.
  base: "./",
  css: {
    postcss: {
      plugins: [pxToRem({ rootValue: ROOT_PX, propList: ["*"], minPixelValue: SMALLEST_SCALED_PX })],
    },
  },
  plugins: [woff2Only(), vue(), diceModels()],
  server: { host: "127.0.0.1", port: 5173, strictPort: true, fs: { allow: [repo] } },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    // A page may load nothing but its own files, fonts included.
    assetsInlineLimit: 0,
    // The dice board carries three.js, about 680 kB: the size check counts it apart (scripts/check-dist.mjs).
    chunkSizeWarningLimit: 800,
    rollupOptions: {
      input: Object.fromEntries(PAGES.map(({ name }) => [name, fileURLToPath(new URL(`${name}/index.html`, import.meta.url))])),
      // The dice board's own files (three.js, the dice's pictures) under dice/, so the size check counts them apart.
      output: {
        entryFileNames: (chunk) => (chunk.name === DICE_PAGE ? `${DICE_PAGE}/assets/[name]-[hash].js` : "assets/[name]-[hash].js"),
        assetFileNames: (asset) =>
          asset.originalFileNames?.some((name) => name.startsWith(`${DICE_PAGE}/`)) ? `${DICE_PAGE}/assets/[name]-[hash][extname]` : "assets/[name]-[hash][extname]",
      },
    },
  },
  test: {
    environment: "jsdom",
    include: ["tests/**/*.test.js"],
    setupFiles: ["tests/setup.js"],
  },
});
