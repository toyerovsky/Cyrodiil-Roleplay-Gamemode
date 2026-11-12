import { test, expect, readTexts, screenshot, readSent } from "./fixtures.js";

const t = readTexts("ui.radial");
const chat = readTexts("chat");
const dice = readTexts("dice");
const exchange = readTexts("exchange");
const emotes = readTexts("emotes");
const admin = readTexts("admin");
const stable = readTexts("stable");
const fill = (text, args) => text.replace(/\{(\w+)\}/g, (_, name) => String(args[name]));

test("the own wheel picks with the mouse, opens again on G and picks by number", async ({ page }, testInfo) => {
  await page.goto("/radial/index.html");
  const wheel = page.getByRole("listbox", { name: t("wheel") });
  await expect(wheel).toBeFocused();
  await expect(wheel.getByRole("option")).toHaveCount(4);
  await expect(page.getByText("Marius Varro")).toBeVisible();

  // The icons let the pointer through: the wheel picks by the pointer's angle, as a player points.
  await page.mouse.move(...(await findCentre(wheel.getByRole("option", { name: dice("option.board") }))));
  await expect(page.locator(".rp-radial__label")).toHaveText(dice("option.board"));
  await screenshot(page, testInfo, "wheel-self");
  await page.mouse.click(...(await findCentre(wheel.getByRole("option", { name: dice("option.board") }))));
  await expect(wheel).toBeHidden();

  await page.keyboard.press("g");
  await expect(wheel).toBeFocused();
  await expect(wheel.getByRole("option", { name: stable("option.call") })).toBeVisible();
  await page.keyboard.press("4");
  await expect(wheel).toBeHidden();
  expect(await readSent(page, "WheelPick")).toEqual([["self.roll"], ["self.horse"]]);
});

test("the wheel over another player says why an option is closed and the same key puts it away", async ({ page }, testInfo) => {
  await page.goto("/radial/index.html#interaction");
  const wheel = page.getByRole("listbox", { name: t("wheel") });
  await expect(wheel).toBeFocused();
  const pay = wheel.getByRole("option", { name: exchange("option") });
  await expect(pay).toHaveAttribute("aria-disabled", "true");

  await page.mouse.move(...(await findCentre(pay)));
  await expect(page.locator(".rp-radial__reason")).toHaveText("Lucia Gallus is busy with another trade.");
  await screenshot(page, testInfo, "wheel-refused");
  await page.mouse.click(...(await findCentre(pay)));
  await expect(wheel).toBeVisible();

  await page.keyboard.press("h");
  await expect(wheel).toBeHidden();
  expect(await readSent(page, "WheelPick")).toEqual([]);
  expect(await readSent(page, "WheelClose")).toHaveLength(1);
});

test("a staff member's wheel over another player of ten options pages, and the right mouse button closes it", async ({ page }, testInfo) => {
  await page.goto("/radial/index.html#staff");
  const wheel = page.getByRole("listbox", { name: t("wheel") });
  await expect(wheel.getByRole("option")).toHaveCount(10);
  await expect(page.locator(".rp-radial__caption")).toHaveText(fill(t("page"), { page: 1, pages: 2 }));
  await screenshot(page, testInfo, "wheel-staff");

  await wheel.click({ button: "right" });
  await expect(wheel).toBeHidden();
  expect(await readSent(page, "WheelClose")).toHaveLength(1);

  // The own wheel keeps the staff's panel, on one page.
  await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 0)));
  await page.keyboard.press("g");
  await expect(wheel.getByRole("option")).toHaveCount(5);
  await expect(wheel.getByRole("option", { name: admin("option.panel") })).toBeVisible();
});

test("the emotes turn their pages from the wedges by the top, the keys and the mouse wheel, and pick by number", async ({ page }, testInfo) => {
  await page.goto("/radial/index.html#emotes");
  const wheel = page.getByRole("listbox", { name: t("wheel") });
  const caption = page.locator(".rp-radial__caption");
  await expect(wheel).toBeFocused();
  await expect(wheel.getByRole("option")).toHaveCount(10);
  await expect(page.getByText(emotes("title"))).toBeVisible();
  await expect(caption).toHaveText(fill(t("page"), { page: 1, pages: 3 }));
  await expect(page.getByText(t("hint_paged"))).toBeVisible();

  const next = wheel.getByRole("option", { name: t("next") });
  await page.mouse.move(...(await findCentre(next)));
  await expect(page.locator(".rp-radial__label")).toHaveText(t("next"));
  await screenshot(page, testInfo, "wheel-emotes");
  await page.mouse.click(...(await findCentre(next)));
  await expect(caption).toHaveText(fill(t("page"), { page: 2, pages: 3 }));
  await expect(wheel).toBeVisible();

  await page.keyboard.press("e");
  await expect(caption).toHaveText(fill(t("page"), { page: 3, pages: 3 }));
  await expect(wheel.getByRole("option")).toHaveCount(7);
  await screenshot(page, testInfo, "wheel-emotes-last");

  await page.mouse.move(...(await findCentre(wheel)));
  await page.mouse.wheel(0, 120);
  await expect(caption).toHaveText(fill(t("page"), { page: 1, pages: 3 }));
  await page.keyboard.press("q");
  await page.keyboard.press("q");
  await expect(caption).toHaveText(fill(t("page"), { page: 2, pages: 3 }));
  await page.keyboard.press("3");
  await expect(wheel).toBeHidden();

  // The game hears a pick a moment later and opens no wheel before it has.
  await expect.poll(() => readSent(page, "WheelPick")).toHaveLength(1);
  await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 0)));
  await page.keyboard.press("g");
  await expect(caption).toHaveText(fill(t("page"), { page: 1, pages: 3 }));
  await page.keyboard.press("1");
  expect(await readSent(page, "WheelPick")).toEqual([["emote:exhausted"], ["stop"]]);
});

test("an option that needs more from the player opens the chat with the command started", async ({ page }, testInfo) => {
  await page.goto("/chat/index.html#draft");
  const entry = page.getByRole("textbox", { name: readTexts("ui.chat")("entry") });
  await expect(entry).toBeFocused();
  await expect(entry).toHaveValue("/pm 3 ");
  await screenshot(page, testInfo, "chat-draft");

  await page.keyboard.type("got a minute?");
  await page.keyboard.press("Enter");
  expect(await readSent(page, "Submit")).toEqual([["/pm 3 got a minute?"]]);
});

async function findCentre(locator) {
  const box = await locator.boundingBox();
  return [box.x + box.width / 2, box.y + box.height / 2];
}
