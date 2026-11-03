import { test, expect, readTexts, screenshot } from "./fixtures.js";

const t = readTexts("ui.party");

test("the panel lists the party with its leader crowned and the player by name, and lets the mouse through", async ({ page }, testInfo) => {
  await page.goto("/party/index.html");
  const panel = page.getByRole("region", { name: t("title") });
  await expect(panel.getByRole("listitem")).toHaveText([/Marius Varro/, /Lucia Gallus/, /Ra'jirr/]);
  await expect(panel.getByRole("listitem").first()).toContainText(t("you"));
  await expect(panel.getByRole("meter", { name: "Health of Lucia Gallus" })).toHaveAttribute("aria-valuenow", "45");
  await expect(panel).toContainText("3/6");
  expect(await page.evaluate(() => getComputedStyle(document.body).pointerEvents)).toBe("none");
  await screenshot(page, testInfo, "party-leader");
});

test("six members with a long name, one down and two away, stay inside the panel", async ({ page }, testInfo) => {
  await page.goto("/party/index.html#member");
  const panel = page.getByRole("region", { name: t("title") });
  await expect(panel.getByRole("listitem")).toHaveCount(6);
  await expect(panel.getByRole("listitem").nth(2)).toContainText(t("down"));
  await expect(panel.getByRole("listitem").nth(3)).toContainText(t("away"));
  await expect(panel.getByRole("meter")).toHaveCount(5);
  const overflows = await page.locator(".party__line").evaluateAll((lines) =>
    lines.some((line) => [...line.children].some((child) => child.getBoundingClientRect().right > line.closest(".party").getBoundingClientRect().right)));
  expect(overflows).toBe(false);
  await screenshot(page, testInfo, "party-member");
});
