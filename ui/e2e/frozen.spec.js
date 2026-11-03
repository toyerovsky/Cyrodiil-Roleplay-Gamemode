import { test, expect, readTexts, screenshot } from "./fixtures.js";

const t = readTexts("ui.frozen");

test("the frozen player reads why, and a long reason wraps on the plate", async ({ page }, testInfo) => {
  await page.goto("/frozen/index.html");
  await expect(page.getByRole("alert")).toContainText("Running from the guards mid scene");
  await expect(page.getByRole("heading", { name: t("title") })).toBeVisible();
  await screenshot(page, testInfo, "frozen");

  await page.goto("/frozen/index.html?shot=long#long");
  const plate = page.locator(".frozen__plate");
  await expect(plate).toContainText("after two warnings");
  const fits = await plate.evaluate((element) => element.scrollWidth <= element.clientWidth + 1);
  expect(fits).toBe(true);
  await screenshot(page, testInfo, "frozen-long");
});
