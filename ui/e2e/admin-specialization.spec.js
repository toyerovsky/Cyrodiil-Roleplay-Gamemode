import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.admin");
const mentor = readTexts("ui.mentor");
const fill = (text, args) => text.replace(/\{(\w+)\}/g, (_, name) => String(args[name]));

test("the staff fix a character's specialization, a cap and a value from the card", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html");
  await page.locator('[data-player="4"]').click();
  const section = page.getByRole("region", { name: t("card.spec") });
  await expect(section).toBeVisible();
  await expect(section.getByRole("button", { name: mentor("name.Knight") })).toHaveAttribute("aria-pressed", "true");
  await expect(section.locator('[data-stat="Blade"] td').first()).toHaveText("67");
  await expect(section.locator('[data-stat="Destruction"] td').nth(1)).toHaveText("30");
  await expect(section.locator("[data-stat]")).toHaveCount(29);
  await section.scrollIntoViewIfNeeded();
  await expectNoHorizontalOverflow(page, ".admin-card");
  await screenshot(page, testInfo, "admin-spec");

  const wizard = section.getByRole("button", { name: mentor("name.Wizard") });
  await wizard.click();
  await expect(section.getByRole("button", { name: t("card.spec_again") })).toBeVisible();
  expect(await readSent(page, "AdminRun")).toEqual([]);
  await section.getByRole("button", { name: t("card.spec_again") }).click();
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/aspec 4 Wizard"]);
  await expect(section.getByRole("button", { name: mentor("name.Wizard") })).toHaveAttribute("aria-pressed", "true");

  await section.locator('[data-stat="Destruction"] [data-act="cap"]').click();
  const capDialog = page.getByRole("dialog");
  await expect(capDialog).toContainText(fill(t("action.cap"), { statName: "Destruction" }));
  await capDialog.getByLabel(fill(t("field.cap"), { max: 100 })).fill("90");
  await screenshot(page, testInfo, "admin-spec-cap");
  await page.keyboard.press("Enter");
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/acap 4 Destruction 90"]);
  await expect(section.locator('[data-stat="Destruction"] td').nth(1)).toContainText("90");
  await expect(section.locator('[data-stat="Destruction"] [data-act="clear"]')).toBeVisible();

  await section.locator('[data-stat="Destruction"] [data-act="value"]').click();
  const valueDialog = page.getByRole("dialog");
  await valueDialog.getByLabel(fill(t("field.value"), { max: 90 })).fill("91");
  await page.keyboard.press("Enter");
  await expect(valueDialog.getByRole("alert")).toHaveText(fill(t("dialog.value_range"), { max: 90 }));
  await valueDialog.getByLabel(fill(t("field.value"), { max: 90 })).fill("75");
  await page.keyboard.press("Enter");
  expect((await readSent(page, "AdminRun")).at(-1)).toEqual(["/astat 4 Destruction 75"]);
  await expect(section.locator('[data-stat="Destruction"] td').first()).toHaveText("75");
  await screenshot(page, testInfo, "admin-spec-fixed");
});
