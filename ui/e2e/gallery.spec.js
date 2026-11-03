import { PAGES } from "../pages.js";
import { test, expect, screenshot } from "./fixtures.js";

test("the gallery lists every page and its scenarios and shows the design system", async ({ page }, testInfo) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Cyrodiil RP pages" })).toBeVisible();

  for (const { title, scenarios } of PAGES) {
    await expect(page.getByRole("link", { name: title, exact: true })).toBeVisible();
    await expect(page.getByRole("navigation", { name: `Scenarios: ${title}` }).getByRole("link")).toHaveText(scenarios.filter(Boolean));
  }

  await expect(page.getByRole("button", { name: "Disabled" })).toBeDisabled();
  await expect(page.getByLabel("Refused field", { exact: true })).toHaveAttribute("aria-invalid", "true");
  await expect(page.getByRole("region", { name: "Letter" })).toBeVisible();
  await expect(page.getByLabel("Refused field on a letter")).toHaveAttribute("aria-invalid", "true");

  const kinds = page.getByRole("group", { name: "Kinds" });
  await kinds.getByRole("button", { name: "tools" }).click();
  await expect(kinds.getByRole("button", { pressed: true })).toHaveText("tools");
  await expect(page.getByRole("group", { name: "Weather" }).getByRole("button", { name: "Snow" })).toBeDisabled();
  const large = page.getByRole("tablist", { name: "Large tabs" });
  await large.getByRole("tab", { name: "Help" }).click();
  await expect(large.getByRole("tab", { selected: true })).toHaveText("Help");
  await expect(page.getByRole("tablist", { name: "Small tabs" }).getByRole("tab")).toHaveCount(5);
  await screenshot(page, testInfo, "gallery");
});

// A page that has talked to the game is mounted and has run its start.
for (const { name, scenarios } of PAGES) {
  for (const scenario of scenarios) {
    test(`the preview of ${name}${scenario ? ` #${scenario}` : ""} renders without the game`, async ({ page }) => {
      await page.goto(`/${name}/index.html${scenario ? `#${scenario}` : ""}`);
      await expect.poll(() => page.evaluate(() => window.__readym?.sent.length ?? 0)).toBeGreaterThan(0);
      await expect(page.locator("#root")).not.toBeEmpty();
    });
  }
}
