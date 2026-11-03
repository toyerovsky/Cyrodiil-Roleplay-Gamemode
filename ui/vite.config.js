import { fileURLToPath } from "node:url";
import vue from "@vitejs/plugin-vue";
import pxToRem from "postcss-pxtorem";
import { defineConfig } from "vite";
import { PAGES } from "./pages.js";

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
  plugins: [woff2Only(), vue()],
  server: { host: "127.0.0.1", port: 5173, strictPort: true, fs: { allow: [repo] } },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    // A page may load nothing but its own files, fonts included.
    assetsInlineLimit: 0,
    rollupOptions: {
      input: Object.fromEntries(PAGES.map(({ name }) => [name, fileURLToPath(new URL(`${name}/index.html`, import.meta.url))])),
    },
  },
  test: {
    environment: "jsdom",
    include: ["tests/**/*.test.js"],
    setupFiles: ["tests/setup.js"],
  },
});
