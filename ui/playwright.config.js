import { defineConfig } from "@playwright/test";

const PORT = process.env.E2E_PORT ?? "5174";

// One size unless E2E_SIZES=all: the four are for the screenshots of a screen just made or changed.
const viewports = [
  { width: 1366, height: 768 },
  { width: 1920, height: 1080 },
  { width: 1280, height: 1024 },
  { width: 2560, height: 1080 },
].filter((v) => process.env.E2E_SIZES === "all" || v.width === 1920);

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  workers: Number(process.env.E2E_WORKERS ?? 2),
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: `http://127.0.0.1:${PORT}`,
    browserName: "chromium",
    headless: true,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: viewports.map((viewport) => ({ name: `${viewport.width}x${viewport.height}`, use: { viewport } })),
  webServer: {
    command: `npm run dev -- --port ${PORT}`,
    url: `http://127.0.0.1:${PORT}`,
    reuseExistingServer: false,
    timeout: 30_000,
  },
});
