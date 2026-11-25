import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow, pauseClock } from "./fixtures.js";

const t = readTexts("ui.fishing");
const CHARGE_UP_MS = 1100;
const NIBBLE_SHOWN_MS = 800;
const CARD_SHOWN_MS = 6000;
// A card's fade in or out, whose frames the paused clock gives too.
const LEAVE_MS = 1500;
// What shows behind the page in the game: a sunlit lake and a night sky, the hardest backgrounds for its text.
const SCENES = {
  lake: "linear-gradient(180deg, #bfe3ff 0%, #f4fbff 36%, #ffffff 44%, #a9d3ea 50%, #74b3d6 70%, #4f8fb5 100%)",
  night: "linear-gradient(180deg, #03050b 0%, #0a1428 55%, #0d1d30 70%, #060b14 100%)",
};

const LINE = {
  stage: "Aim",
  nibbles: 0,
  bites: 0,
  strikeWindowMs: 1000,
  fight: null,
  rod: { name: "Fishing Rod", tier: "Basic", condition: "Good", reachCm: 700 },
  bait: { kind: "Worms", name: "Fishing Worms", count: 12 },
  skill: { level: 45, xp: 280, xpNext: 290 },
  outcome: null,
};
const FIGHT = { tension: 0.42, tensionWarn: 0.8, stamina: 0.7, distanceCm: 640, pull: "Left", lean: 0, reeling: false, slack: 0, feel: "Heavy" };

async function setScene(page, scene) {
  await page.evaluate((background) => (document.documentElement.style.background = background), SCENES[scene]);
}

async function tell(page, name, ...args) {
  await page.evaluate(([message, values]) => window.__readym.emit(message, ...values), [name, args]);
}

const showLine = (page, view) => tell(page, "State", JSON.stringify({ ...LINE, ...view }));

async function openLine(page, scenario) {
  await page.goto(`/fishing/index.html${scenario ? `?shot=${scenario}#${scenario}` : ""}`);
  await expect.poll(() => readSent(page, "FishingReady")).toHaveLength(1);
  await expect(page.locator(".fishing__plate")).toBeVisible();
}

// Every part of the page lies wholly on the screen and none scrolls sideways, and the card's picture covers none of its
// lines.
async function expectWithinScreen(page) {
  const picture = page.locator(".fishing-card__picture");
  if ((await picture.count()) > 0) {
    const bottom = await picture.evaluate((element) => (element.querySelector("img") ?? element).getBoundingClientRect().bottom);
    const next = await picture.evaluate((element) => element.nextElementSibling?.getBoundingClientRect().top ?? Infinity);
    expect(bottom, "the card's picture over its lines").toBeLessThanOrEqual(next + 1);
  }

  const viewport = page.viewportSize();
  for (const part of [".fishing__plate", ".fishing-gear", ".fishing-card"]) {
    if ((await page.locator(part).count()) === 0) continue;
    const box = await page.locator(part).boundingBox();
    expect(box.x, `${part} from the left`).toBeGreaterThanOrEqual(0);
    expect(box.y, `${part} from the top`).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width, `${part} to the right`).toBeLessThanOrEqual(viewport.width);
    expect(box.y + box.height, `${part} to the bottom`).toBeLessThanOrEqual(viewport.height);
    await expectNoHorizontalOverflow(page, part);
  }
}

test("a whole bite from the cast to the catch, as the game tells it", async ({ page }, testInfo) => {
  await page.clock.install();
  await openLine(page, "");
  await pauseClock(page);
  await setScene(page, "lake");
  await expect(page.locator(".fishing__title")).toHaveText(t("aim"));
  await expect(page.locator(".fishing__power-end")).toHaveText(["1.5 m", "7 m"]);
  await expect(page.locator('[data-key="cast"]')).toHaveClass(/fishing__key--wanted/);

  await tell(page, "Charge", true, CHARGE_UP_MS);
  await page.clock.runFor(CHARGE_UP_MS * 0.7);
  const power = page.getByRole("meter", { name: t("power") });
  await expect.poll(async () => Number(await power.getAttribute("aria-valuenow"))).toBeGreaterThan(0.6);
  await expect(page.locator('[data-key="cast"]')).toHaveClass(/fishing__key--on/);
  await expectWithinScreen(page);
  await screenshot(page, testInfo, "fishing-charging-lake");

  await page.clock.runFor(CHARGE_UP_MS * 0.6);
  await expect.poll(async () => Number(await power.getAttribute("aria-valuenow"))).toBeLessThan(0.95);
  await tell(page, "Charge", false, 0);
  await showLine(page, { stage: "Waiting" });
  await expect(page.locator(".fishing__title")).toHaveText(t("waiting"));
  await expect(page.locator(".fishing__hint")).toHaveText(t("waiting_hint"));

  await showLine(page, { stage: "Waiting", nibbles: 1 });
  await expect(page.locator(".fishing__title")).toHaveText(t("nibble"));
  await page.clock.runFor(NIBBLE_SHOWN_MS);
  await expect(page.locator(".fishing__title")).toHaveText(t("waiting"));

  await showLine(page, { stage: "Bite", nibbles: 1, bites: 1 });
  await tell(page, "Anchor", 0.5, 0.45, true);
  await expect(page.locator(".fishing__title")).toHaveText(t("bite"));
  await expect(page.locator(".fishing__mark--bite .fishing__ring")).toBeVisible();
  await expect(page.locator('[data-key="strike"]')).toHaveClass(/fishing__key--wanted/);

  await showLine(page, { stage: "Fight", bites: 1, fight: FIGHT });
  await expect(page.locator(".fishing__title")).toHaveText(t("pull.Left"));
  await expect(page.locator(".fishing__hint")).toHaveText(t("counter.Left"));
  await expect(page.locator(".fishing__key--wanted")).toHaveAttribute("data-key", "right");
  await expect(page.locator(".fishing__run--left")).toBeVisible();

  await showLine(page, { stage: "Fight", bites: 1, fight: { ...FIGHT, lean: 1 } });
  await expect(page.locator(".fishing__hint--done")).toBeVisible();
  await expect(page.locator(".fishing-fight__lean--against")).toHaveText(t("lean.right"));
  await expectWithinScreen(page);
  await screenshot(page, testInfo, "fishing-fight-lake");

  await setScene(page, "night");
  await showLine(page, { stage: "Fight", bites: 1, fight: { ...FIGHT, pull: "Right", lean: 1, reeling: true, tension: 0.9 } });
  await expect(page.locator('[data-gauge="tension"]')).toHaveClass(/fishing-fight__gauge--red/);
  await expect(page.locator('[data-key="right"]')).toHaveClass(/fishing__key--strain/);
  await expect(page.locator('[data-key="reel"]')).toHaveClass(/fishing__key--strain/);
  await screenshot(page, testInfo, "fishing-strain-night");

  await showLine(page, {
    stage: "Rest",
    bites: 1,
    skill: { level: 46, xp: 2, xpNext: 296 },
    outcome: { number: 1, kind: "Caught", species: "GoldenCarp", name: "Emperor's Golden Carp", lengthCm: 88, grams: 11176, trophy: true, record: true, xp: 270, levelUp: 46, rare: true, rod: null, baitLost: false },
  });
  const card = page.locator("[data-card]");
  await page.clock.runFor(LEAVE_MS);
  await expect(card).toHaveCSS("opacity", "1");
  await expect(card.locator(".fishing-card__name")).toHaveText("Emperor's Golden Carp");
  await expect(card.locator("[data-badge]")).toHaveText([t("trophy"), t("record"), t("rare")]);
  await expect(card.locator("[data-level]")).toHaveText("Fishing level 46!");
  await expect(page.locator('[data-gear="skill"]')).toContainText("level 46");
  await expectWithinScreen(page);
  await screenshot(page, testInfo, "fishing-rare-night");

  await showLine(page, { stage: "Aim", outcome: { number: 1, kind: "Caught" } });
  await expect(page.locator(".fishing__title")).toHaveText(t("aim"));
  await expect(power).toHaveAttribute("aria-valuenow", "0");
  await page.clock.runFor(CARD_SHOWN_MS + LEAVE_MS);
  await expect(card).toHaveCount(0);

  await tell(page, "Ended");
  await expect(page.locator(".fishing")).toHaveCount(0);
});

test("every way a fish gets away is named, with what it cost the gear", async ({ page }, testInfo) => {
  for (const [scenario, key] of [
    ["lostearly", "lost_early"],
    ["lostnibble", "lost_nibble"],
    ["lostlate", "lost_late"],
    ["snapped", "lost_snapped"],
    ["slipped", "lost_slack"],
    ["runaway", "lost_runaway"],
  ]) {
    await openLine(page, scenario);
    await expect(page.locator(".fishing__title")).toHaveText(t(key));
    await expect(page.locator(".fishing-card__eyebrow")).toHaveText(t("card.lost"));
  }

  await expect(page.locator("[data-rod]")).toHaveText(t("rod_now.Broken"));
  await expect(page.locator("[data-bait]")).toHaveText(t("lure_lost"));
  await setScene(page, "lake");
  await expectWithinScreen(page);
  await screenshot(page, testInfo, "fishing-runaway-lake");

  await openLine(page, "snapped");
  await expect(page.locator("[data-rod]")).toHaveText(t("rod_now.Damaged"));
  await expect(page.locator('[data-gear="bait"] .fishing-gear__name')).toHaveText(t("bait_none"));
  await setScene(page, "night");
  await screenshot(page, testInfo, "fishing-snapped-night");
});

// Each state worth seeing, over the lake and the night, at the sizes the run is given.
const SHOTS = [
  ["", "lake"],
  ["waiting", "night"],
  ["bite", "lake"],
  ["dive", "lake"],
  ["rest", "night"],
  ["slack", "lake"],
  ["monster", "night"],
  ["caught", "lake"],
  ["found", "night"],
  ["nobait", "lake"],
];
for (const [scenario, scene] of SHOTS) {
  test(`#${scenario || "aim"} over the ${scene} keeps within the screen`, async ({ page }, testInfo) => {
    await openLine(page, scenario);
    await setScene(page, scene);
    await expectWithinScreen(page);
    await screenshot(page, testInfo, `fishing-${scenario || "aim"}-${scene}`);
  });
}

test("the page never takes a click or the focus from the game", async ({ page }) => {
  await openLine(page, "fight");
  const takesClicks = await page.evaluate(() =>
    [document.body, ".fishing", ".fishing__plate", ".fishing-gear", ".fishing-fight__track", ".fishing__mark"]
      .map((part) => (typeof part === "string" ? document.querySelector(part) : part))
      .some((element) => getComputedStyle(element).pointerEvents !== "none"),
  );
  expect(takesClicks).toBe(false);

  await page.keyboard.press("Tab");
  expect(await page.evaluate(() => document.activeElement === document.body)).toBe(true);
});
