import { test, expect, readTexts, screenshot, readSent } from "./fixtures.js";

const t = readTexts("ui.radial");
const chat = readTexts("chat");
const exchange = readTexts("exchange");

test("the own wheel picks with the mouse, opens again on G and picks by number", async ({ page }, testInfo) => {
  await page.goto("/radial/index.html");
  const wheel = page.getByRole("listbox", { name: t("wheel") });
  await expect(wheel).toBeFocused();
  await expect(wheel.getByRole("option")).toHaveCount(6);
  await expect(page.getByText("Marius Varro")).toBeVisible();

  // The icons let the pointer through: the wheel picks by the pointer's angle, as a player points.
  await page.mouse.move(...(await findCentre(wheel.getByRole("option", { name: chat("option.roll") }))));
  await expect(page.locator(".rp-radial__label")).toHaveText(chat("option.roll"));
  await screenshot(page, testInfo, "wheel-self");
  await page.mouse.click(...(await findCentre(wheel.getByRole("option", { name: chat("option.roll") }))));
  await expect(wheel).toBeHidden();

  await page.keyboard.press("g");
  await expect(wheel).toBeFocused();
  await page.keyboard.press("4");
  await expect(wheel).toBeHidden();
  expect(await readSent(page, "WheelPick")).toEqual([["self.roll"], ["self.gold"]]);
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

test("a staff member's wheel holds all eight places, and the right mouse button closes it", async ({ page }, testInfo) => {
  await page.goto("/radial/index.html#staff");
  const wheel = page.getByRole("listbox", { name: t("wheel") });
  await expect(wheel.getByRole("option")).toHaveCount(8);
  await screenshot(page, testInfo, "wheel-staff");

  await wheel.click({ button: "right" });
  await expect(wheel).toBeHidden();
  expect(await readSent(page, "WheelClose")).toHaveLength(1);
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
