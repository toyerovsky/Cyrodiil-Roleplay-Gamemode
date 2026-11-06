import { test, expect, readTexts, screenshot, readSent, pauseClock, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.bank");

test("gold is put into the bank and taken out again, and both sums follow", async ({ page }, testInfo) => {
  await page.goto("/bank/index.html");
  const window = page.getByRole("region", { name: t("title") });
  const amount = page.getByLabel(t("amount"));
  const status = page.getByRole("status");
  await expect(window).toBeVisible();
  await expect(amount).toBeFocused();
  await expect(page.locator('[data-sum="balance"] dd')).toHaveText("1,250 gold");
  await expect(page.locator('[data-sum="gold"] dd')).toHaveText("340 gold");
  await expectNoHorizontalOverflow(page, ".bank__panel");
  await screenshot(page, testInfo, "bank-open");

  await amount.fill("140");
  await page.getByRole("button", { name: t("deposit") }).click();
  await expect(status).toHaveText("You put 140 gold into the bank.");
  await expect(page.locator('[data-sum="balance"] dd')).toHaveText("1,390 gold");
  await expect(page.locator('[data-sum="gold"] dd')).toHaveText("200 gold");
  await expect(amount).toHaveValue("");
  await screenshot(page, testInfo, "bank-deposited");

  await page.getByRole("button", { name: t("all_banked") }).click();
  await expect(amount).toHaveValue("1390");
  await page.getByRole("button", { name: t("withdraw") }).click();
  await expect(status).toHaveText("You took 1390 gold out of the bank.");
  await expect(page.locator('[data-sum="balance"] dd')).toHaveText("0 gold");
  await expect(page.locator('[data-sum="gold"] dd')).toHaveText("1,590 gold");
  expect(await readSent(page, "BankDeposit")).toEqual([[140]]);
  expect(await readSent(page, "BankWithdraw")).toEqual([[1390]]);
});

test("an amount the bank would refuse is marked at its field and not sent", async ({ page }, testInfo) => {
  await page.goto("/bank/index.html");
  const amount = page.getByLabel(t("amount"));
  await amount.fill("one hundred");
  await page.getByRole("button", { name: t("deposit") }).click();
  await expect(page.getByRole("alert")).toHaveText("Give a whole amount from 1 to 1,000,000 gold.");
  await expect(amount).toHaveAttribute("aria-invalid", "true");
  await expect(amount).toBeFocused();

  await amount.fill("5000");
  await page.getByRole("button", { name: t("withdraw") }).click();
  await expect(page.getByRole("alert")).toHaveText("The bank holds only 1,250 gold of yours.");
  await screenshot(page, testInfo, "bank-refused");
  expect(await readSent(page, "BankWithdraw")).toEqual([]);
  expect(await readSent(page, "BankDeposit")).toEqual([]);
});

test("the window waits for the banker and takes no second move meanwhile", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/bank/index.html");
  await page.clock.runFor(100);
  await expect(page.getByLabel(t("amount"))).toBeVisible();
  await pauseClock(page);

  await page.getByLabel(t("amount")).fill("40");
  await page.getByRole("button", { name: t("deposit") }).click();
  await expect(page.getByRole("status")).toHaveText(t("waiting"));
  await expect(page.getByRole("button", { name: t("deposit") })).toBeDisabled();
  await expect(page.getByRole("button", { name: t("withdraw") })).toBeDisabled();
  await screenshot(page, testInfo, "bank-waiting");

  await page.clock.runFor(100);
  await expect(page.getByRole("status")).toHaveText("You put 40 gold into the bank.");
  await expect(page.getByRole("button", { name: t("deposit") })).toBeEnabled();
});

test("a character with nothing at the bank yet can only fill in what they carry", async ({ page }, testInfo) => {
  await page.goto("/bank/index.html#empty");
  await expect(page.locator('[data-sum="balance"] dd')).toHaveText("0 gold");
  await expect(page.getByRole("button", { name: t("all_banked") })).toBeDisabled();
  await page.getByRole("button", { name: t("all_carried") }).click();
  await expect(page.getByLabel(t("amount"))).toHaveValue("85");
  await screenshot(page, testInfo, "bank-empty");
});

test("long sums stay inside the window", async ({ page }, testInfo) => {
  await page.goto("/bank/index.html#rich");
  await expect(page.locator('[data-sum="balance"] dd')).toHaveText("98,765,432,100 gold");
  await expectNoHorizontalOverflow(page, ".bank__panel");
  await page.getByRole("button", { name: t("all_carried") }).click();
  await expect(page.getByLabel(t("amount"))).toHaveValue("1000000");
  await screenshot(page, testInfo, "bank-rich");
});

test("Escape leaves the bank, from the amount's field too", async ({ page }) => {
  await page.goto("/bank/index.html");
  await expect(page.getByLabel(t("amount"))).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.locator(".bank")).toHaveCount(0);
  expect(await readSent(page, "BankClose")).toEqual([[]]);
});
