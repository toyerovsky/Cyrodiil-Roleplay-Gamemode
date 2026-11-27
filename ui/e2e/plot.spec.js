import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.plot");
const server = readTexts("building");
const fill = (text, args) => text.replace(/\{(\w+)\}/g, (_, name) => String(args[name]));

test("a plot for sale is bought at its sign with the keys and then shows as the reader's own", async ({ page }, testInfo) => {
  await page.goto("/plot/index.html");
  const window = page.getByRole("region", { name: fill(t("title"), { id: 6 }) });
  await expect(window).toBeVisible();
  await expect(page.locator('[data-standing="ForSale"]')).toHaveText(t("lead.ForSale"));
  await expect(page.locator('[data-fact="size"] dd')).toHaveText(fill(t("size"), { width: 12, depth: 11, widthMetres: "21.9", depthMetres: "20.1" }));
  await expect(page.locator('[data-fact="price"] dd')).toHaveText("5,000 gold");
  await expect(page.locator('[data-fact="tax"] dd')).toHaveText("100 gold");
  await expect(page.locator('[data-fact="gold"] dd')).toHaveText("6,200 gold");
  const buy = page.getByRole("button", { name: fill(t("buy"), { price: "5,000" }) });
  await expect(buy).toBeFocused();
  await expectNoHorizontalOverflow(page, ".plot__panel");
  await screenshot(page, testInfo, "plot-for-sale");

  await page.keyboard.press("Enter");
  await expect(page.getByRole("status")).toHaveText(fill(server("sale_bought"), { id: 6, price: 5000, tax: 100 }));
  await expect(page.locator('[data-standing="Yours"]')).toHaveText(t("lead.Yours"));
  await expect(page.locator('[data-action="buy"]')).toHaveCount(0);
  await expect(page.locator('[data-fact="gold"] dd')).toHaveText("1,200 gold");
  await expect(page.getByRole("button", { name: t("close") })).toBeFocused();
  await screenshot(page, testInfo, "plot-bought");
  expect((await readSent(page, "PlotBuy")).length).toBe(1);

  await page.keyboard.press("Escape");
  expect((await readSent(page, "PlotClose")).length).toBe(1);
  await expect(window).toHaveCount(0);
});

test("too little gold, or as many plots as a character may own, leaves Buy shut and says why", async ({ page }, testInfo) => {
  await page.goto("/plot/index.html#poor");
  await expect(page.getByRole("button", { name: t("too_poor") })).toBeDisabled();
  await expect(page.locator(".plot__dear")).toHaveText("5,000 gold");
  await expect(page.getByRole("button", { name: t("close") })).toBeFocused();
  await screenshot(page, testInfo, "plot-poor");

  await page.goto("about:blank");
  await page.goto("/plot/index.html#many");
  await expect(page.getByRole("button", { name: t("too_many") })).toBeDisabled();
  await expect(page.locator(".plot__note")).toHaveText(fill(t("most_note"), { most: 1 }));
  expect(await readSent(page, "PlotBuy")).toEqual([]);
});

test("another's plot and the reader's own show no Buy", async ({ page }, testInfo) => {
  await page.goto("/plot/index.html#taken");
  await expect(page.locator('[data-standing="Taken"]')).toHaveText(t("lead.Taken"));
  await expect(page.locator('[data-action="buy"]')).toHaveCount(0);
  await screenshot(page, testInfo, "plot-taken");

  await page.goto("about:blank");
  await page.goto("/plot/index.html#yours");
  await expect(page.locator('[data-standing="Yours"]')).toHaveText(t("lead.Yours"));
  await expect(page.locator('[data-action="buy"]')).toHaveCount(0);
});

test("the sign reads in Polish", async ({ page }, testInfo) => {
  const pl = readTexts("ui.plot", "pl");
  await page.goto("/plot/index.html#polish");
  await expect(page.getByRole("region", { name: fill(pl("title"), { id: 6 }) })).toBeVisible();
  await expectNoHorizontalOverflow(page, ".plot__panel");
  await screenshot(page, testInfo, "plot-polish");
});
