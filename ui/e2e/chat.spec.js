import { test, expect, readTexts, screenshot, readSent } from "./fixtures.js";

const t = readTexts("ui.chat");
const FADE_AFTER_MS = 8000;

const emitLine = (page, text) =>
  page.evaluate((line) => window.__readym.emit("Line", JSON.stringify([{ kind: "System", text: line, at: Date.now() }])), text);

test("the open chat shows the history by kind, sends a line and stays open, and Escape puts it away", async ({ page }, testInfo) => {
  await page.goto("/chat/index.html#open");

  const entry = page.getByRole("textbox", { name: t("entry") });
  const log = page.getByRole("log", { name: t("log") });
  await expect(entry).toBeVisible();
  await expect(entry).toBeFocused();
  await expect(entry).toHaveAttribute("placeholder", t("placeholder"));

  const lines = log.getByRole("listitem");
  await expect(lines).toHaveCount(7);
  await expect(log).toContainText("[Staff] toyer: Market event at dusk.");
  await expect(log).toContainText("Guard Captain shouts: Stop right there, criminal scum!");
  await expect(log).toContainText("* Ra'jirr leans on the market stall");
  await screenshot(page, testInfo, "chat-open");

  await entry.fill("Good morning, Lucia.");
  await entry.press("Enter");
  await expect(lines.last()).toContainText("Marius Varro says: Good morning, Lucia.");
  await expect(entry).toBeVisible();
  await expect(entry).toHaveValue("");

  await entry.press("Escape");
  await expect(entry).toBeHidden();
  await expect(log).toBeVisible();
  expect(await readSent(page, "Submit")).toEqual([["Good morning, Lucia."]]);
  expect(await readSent(page, "Close")).toHaveLength(1);
  await screenshot(page, testInfo, "chat-closed");
});

test("a closed chat shows new lines, then fades after a while", async ({ page }) => {
  await page.clock.install();
  await page.goto("/chat/index.html");
  const log = page.getByRole("log", { name: t("log") });
  await expect(log.getByRole("listitem")).toHaveCount(7);
  await expect(log).toHaveCSS("opacity", "1");

  await page.clock.runFor(FADE_AFTER_MS + 1000);
  await expect(page.locator(".chat")).toHaveClass(/chat--idle/);

  await emitLine(page, "A courier arrives.");
  await expect(page.locator(".chat")).not.toHaveClass(/chat--idle/);
  await expect(log.getByRole("listitem").last()).toHaveText(/A courier arrives\./);
});

test("a long history opens at its end, Page Up reads back, and a new line keeps the reader's place", async ({ page }, testInfo) => {
  await page.goto("/chat/index.html#long");
  const log = page.getByRole("log", { name: t("log") });
  const entry = page.getByRole("textbox", { name: t("entry") });
  await expect(entry).toBeFocused();
  await expect(log.getByRole("listitem").last()).toBeInViewport();

  await entry.press("PageUp");
  await expect.poll(() => log.evaluate((element) => element.scrollHeight - element.scrollTop - element.clientHeight)).toBeGreaterThan(10);
  const place = await log.evaluate((element) => element.scrollTop);
  await emitLine(page, "Lucia Gallus says: Still here?");
  await expect(log.getByRole("listitem").last()).toHaveText(/Still here\?/);
  expect(await log.evaluate((element) => element.scrollTop)).toBe(place);
  await screenshot(page, testInfo, "chat-reading-back");

  for (let i = 0; i < 3; i++) await entry.press("PageDown");
  await expect(log.getByRole("listitem").last()).toBeInViewport();
});

test("the arrows bring back what was sent", async ({ page }) => {
  await page.goto("/chat/index.html#open");
  const entry = page.getByRole("textbox", { name: t("entry") });
  await entry.fill("first");
  await entry.press("Enter");
  await entry.fill("second");
  await entry.press("Enter");

  await entry.press("ArrowUp");
  await expect(entry).toHaveValue("second");
  await entry.press("ArrowUp");
  await expect(entry).toHaveValue("first");
  await entry.press("ArrowDown");
  await entry.press("ArrowDown");
  await expect(entry).toHaveValue("");
});

test("Tab keeps the keyboard in the chat", async ({ page }) => {
  await page.goto("/chat/index.html#open");
  const entry = page.getByRole("textbox", { name: t("entry") });
  await expect(entry).toBeFocused();

  await entry.press("Tab");
  await expect(entry).toBeFocused();
  expect(await readSent(page, "Blurred")).toEqual([]);
});
