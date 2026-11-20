import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.guides");
const guides = readTexts("guides");
const pl = readTexts("guides", "pl");
const ARM_MS = 600;
const ANSWER_MS = 100;
// Every guide at its first use, as a scenario of the preview names it.
const TOPICS = ["", "trader", "stable", "mentor", "foreman", "noticeboard", "gambling", "dice", "lore", "exchange"];

const START = new Date("2026-11-20T12:00:00Z");

// The clock stands still from before the page loads, so the card comes only with runFor and is not armed until the test
// says so.
async function openCard(page, url) {
  await page.clock.install({ time: START });
  await page.clock.pauseAt(new Date(START.getTime() + ANSWER_MS));
  await page.goto(url);
  await expect.poll(() => readSent(page, "GuideReady")).toHaveLength(1);
  await page.clock.runFor(ANSWER_MS);
  await expect(page.getByRole("dialog")).toBeVisible();
}

async function expectWhole(page) {
  await expect(page.locator(".guide__picture")).toHaveJSProperty("complete", true);
  expect(await page.locator(".guide__picture").evaluate((picture) => picture.naturalWidth)).toBeGreaterThan(0);
  const scrolls = await page.locator(".guide__page").evaluate((element) => element.scrollHeight > element.clientHeight + 1);
  expect(scrolls, "the card fits on the screen without scrolling").toBe(false);
  await expectNoHorizontalOverflow(page, ".guide__page");
}

test("the bank's guide comes before its window, and E lets the window open", async ({ page }, testInfo) => {
  await openCard(page, "/guides/index.html");
  const card = page.getByRole("dialog", { name: guides("bank.title") });
  await expect(card).toContainText(guides("bank.purpose"));
  await expect(page.locator(".guide__number")).toHaveText(["I", "II", "III"]);
  await expect(page.locator(".guide__fact dd")).toHaveText(["1\u00A0000\u00A0000 gold", guides("value.free")]);
  await expect(page.getByRole("button", { name: t("go_on") })).toBeFocused();
  await expectWhole(page);
  await screenshot(page, testInfo, "guide-bank");

  await page.keyboard.press("e");
  expect(await readSent(page, "GuideContinue")).toEqual([]);

  await page.clock.runFor(ARM_MS);
  await page.keyboard.press("e");
  await page.clock.runFor(ANSWER_MS);
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(await readSent(page, "GuideContinue")).toEqual([[]]);
  expect(await readSent(page, "GuideClose")).toEqual([]);
});

test("Esc only closes a guide, and nothing waits behind it", async ({ page }) => {
  await openCard(page, "/guides/index.html#gambling");
  await page.keyboard.press("Escape");
  await page.clock.runFor(ANSWER_MS);
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(await readSent(page, "GuideClose")).toEqual([[]]);
  expect(await readSent(page, "GuideContinue")).toEqual([]);
});

test("the welcome shows the keys with a line each and closes", async ({ page }, testInfo) => {
  await openCard(page, "/guides/index.html#welcome");
  await expect(page.getByRole("heading", { name: guides("welcome.title") })).toBeVisible();
  await expect(page.locator(".guide__eyebrow")).toHaveText(t("eyebrow_welcome"));
  await expect(page.locator(".guide__keycap")).toHaveText(["G", "H", "K", "Y", "N", "F10", "Home", "End"]);
  await expect(page.getByRole("button", { name: t("go_on") })).toHaveCount(0);
  await expectWhole(page);
  await screenshot(page, testInfo, "guide-welcome");

  await page.getByRole("button", { name: t("close") }).click();
  await page.clock.runFor(ANSWER_MS);
  expect(await readSent(page, "GuideClose")).toEqual([[]]);
});

test("a guide read again from the menu leads back to it", async ({ page }, testInfo) => {
  await openCard(page, "/guides/index.html#read");
  await expect(page.locator(".guide__eyebrow")).toHaveText(t("eyebrow_read"));
  const back = page.getByRole("button", { name: t("back") });
  await expect(back).toBeFocused();
  await screenshot(page, testInfo, "guide-read");

  await page.keyboard.press("Enter");
  expect(await readSent(page, "GuideClose")).toEqual([]);
  await page.clock.runFor(ARM_MS);
  await page.keyboard.press("Enter");
  expect(await readSent(page, "GuideClose")).toEqual([[]]);
});

test("every guide fits whole on the screen with its picture", async ({ page }, testInfo) => {
  for (const topic of TOPICS) {
    await page.goto(`/guides/index.html?shot=${topic}#${topic}`);
    await expect(page.getByRole("dialog")).toBeVisible();
    await expect(page.locator(".guide__step").first()).toBeVisible();
    await expectWhole(page);
    if (["gambling", "mentor"].includes(topic)) await screenshot(page, testInfo, `guide-${topic}`);
  }
});

test("the longest guide fits in Polish too", async ({ page }, testInfo) => {
  for (const topic of ["mentor", "gambling", "welcome"]) {
    await page.goto(`/guides/index.html?locale=pl&shot=${topic}#${topic}`);
    await expect(page.locator(".guide__title")).toHaveText(pl(`${topic}.title`));
    await expectWhole(page);
    await screenshot(page, testInfo, `guide-${topic}-pl`);
  }
});
