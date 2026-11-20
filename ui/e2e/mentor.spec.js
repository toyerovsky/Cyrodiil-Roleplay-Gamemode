import { test, expect, readTexts, screenshot, readSent, pauseClock, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.mentor");
const server = readTexts("specialization");
const fill = (text, args) => text.replace(/\{(\w+)\}/g, (_, name) => String(args[name]));
const action = (key, id) => fill(t("action"), { action: t(key), specialization: t(`name.${id}`) });

test("a first specialization is chosen with the keys and the window shows it as the character's own", async ({ page }, testInfo) => {
  await page.goto("/mentor/index.html");
  const window = page.getByRole("region", { name: t("title.Mage") });
  await expect(window).toBeVisible();
  await expect(page.getByRole("list", { name: t("offered") }).getByRole("listitem")).toHaveCount(3);
  await expect(page.locator('[data-sum="specialization"] dd')).toHaveText(t("none"));
  await expect(page.locator('[data-sum="gold"] dd')).toHaveText("1,340 gold");
  await expect(page.locator('[data-sum="next"] dd')).toHaveText(t("next_free"));
  await expect(page.locator('[data-specialization="Wizard"] [data-stats="trained"]')).toContainText("Alchemy");
  await expect(page.getByRole("button", { name: action("choose", "Wizard") })).toBeFocused();
  await expectNoHorizontalOverflow(page, ".mentor__panel");
  await screenshot(page, testInfo, "mentor-open");

  await page.keyboard.press("Tab");
  await expect(page.getByRole("button", { name: action("choose", "Alchemist") })).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("status")).toHaveText(fill(server("chosen"), { specialization: server("name.alchemist") }));
  await expect(page.locator('[data-sum="specialization"] dd')).toHaveText(t("name.Alchemist"));
  await expect(page.locator('[data-sum="next"] dd')).toHaveText(/^In (60|59) s$/);
  await expect(page.getByRole("button", { name: action("yours", "Alchemist") })).toBeDisabled();
  await expect(page.getByRole("button", { name: action("wait", "Wizard") })).toBeDisabled();
  await expect(page.getByRole("button", { name: t("close") })).toBeFocused();
  await screenshot(page, testInfo, "mentor-chosen");
  expect(await readSent(page, "MentorChoose")).toEqual([["Alchemist"]]);
});

test("a change is made only once the player says the specialization before may go", async ({ page }, testInfo) => {
  await page.goto("/mentor/index.html#chosen");
  await expect(page.locator('[data-sum="specialization"] dd')).toHaveText(t("name.Wizard"));
  await expect(page.locator('[data-sum="next"] dd')).toHaveText("1,000 gold");
  await expect(page.locator(".mentor__note")).toHaveText(t("change_note"));
  await screenshot(page, testInfo, "mentor-change");

  const illusionist = page.getByRole("button", { name: action("choose", "Illusionist") });
  await illusionist.click();
  const ask = page.getByRole("alertdialog");
  await expect(ask).toContainText(fill(t("confirm"), { specialization: t("name.Illusionist"), price: "1,000" }));
  await expect(ask.getByRole("button", { name: t("confirm_yes") })).toBeFocused();
  await expectNoHorizontalOverflow(page, ".mentor__panel");
  await screenshot(page, testInfo, "mentor-confirm");

  await page.keyboard.press("Escape");
  await expect(ask).toHaveCount(0);
  await expect(illusionist).toBeFocused();
  expect(await readSent(page, "MentorChoose")).toEqual([]);

  await illusionist.click();
  await page.getByRole("button", { name: t("confirm_yes") }).click();
  await expect(page.getByRole("status")).toHaveText(
    fill(server("changed"), { specialization: server("name.illusionist"), price: 1000, old: server("name.wizard") }),
  );
  await expect(page.locator('[data-sum="gold"] dd')).toHaveText("1,500 gold");
  expect(await readSent(page, "MentorChoose")).toEqual([["Illusionist"]]);
  expect(await readSent(page, "MentorClose")).toEqual([]);
});

test("while a change must wait every choice is held, the window counts down and then opens them", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/mentor/index.html#wait");
  await page.clock.runFor(100);
  await expect(page.locator('[data-sum="next"] dd')).toHaveText(fill(t("next_wait_seconds"), { count: 45 }));
  for (const id of ["Wizard", "Illusionist"]) await expect(page.getByRole("button", { name: action("wait", id) })).toBeDisabled();
  await expect(page.getByRole("button", { name: action("yours", "Alchemist") })).toBeDisabled();
  await expect(page.getByRole("button", { name: t("close") })).toBeFocused();
  await screenshot(page, testInfo, "mentor-wait");

  await page.clock.runFor(30_000);
  await expect(page.locator('[data-sum="next"] dd')).toHaveText(fill(t("next_wait_seconds"), { count: 15 }));
  await page.clock.runFor(15_000);
  await expect(page.locator('[data-sum="next"] dd')).toHaveText("1,000 gold");
  await expect(page.getByRole("button", { name: action("choose", "Wizard") })).toBeEnabled();
  await expect(page.getByRole("button", { name: t("close") }), "the focus does not jump when the wait ends").toBeFocused();
});

test("the registrar teaches all nine and its list scrolls to the last", async ({ page }, testInfo) => {
  await page.goto("/mentor/index.html#registrar");
  await expect(page.getByRole("region", { name: t("title.registrar") })).toBeVisible();
  await expect(page.getByRole("list", { name: t("offered") }).getByRole("listitem")).toHaveCount(9);
  await expect(page.locator('[data-sum="specialization"] dd')).toHaveText(t("name.Knight"));
  await expect(page.getByRole("button", { name: action("too_poor", "Thief") })).toBeDisabled();
  const pictures = page.locator(".mentor__picture");
  await expect(pictures).toHaveCount(9);
  await expect
    .poll(() => pictures.evaluateAll((all) => all.filter((each) => each.complete && each.naturalWidth > 0).length), {
      message: "every specialization's picture loads",
    })
    .toBe(9);
  await expectNoHorizontalOverflow(page, ".mentor__panel");
  await screenshot(page, testInfo, "mentor-registrar");

  await expect(page.getByRole("button", { name: t("close") })).toBeInViewport();
  const panelScrolls = await page.locator(".mentor__panel").evaluate((panel) => panel.scrollHeight > panel.clientHeight + 1);
  expect(panelScrolls, "the letter stays put and only its list scrolls").toBe(false);

  const list = page.locator(".mentor__list");
  await list.evaluate((element) => element.scrollTo(0, element.scrollHeight));
  expect(await list.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
  await expect(page.locator('[data-specialization="Thief"]')).toBeInViewport();
  await screenshot(page, testInfo, "mentor-registrar-end");
});

test("the window waits for the mentor and takes no other choice meanwhile", async ({ page }, testInfo) => {
  await page.clock.install();
  await page.goto("/mentor/index.html");
  await page.clock.runFor(100);
  await expect(page.getByRole("button", { name: action("choose", "Wizard") })).toBeVisible();
  await pauseClock(page);

  await page.getByRole("button", { name: action("choose", "Wizard") }).click();
  await expect(page.getByRole("status")).toHaveText(t("waiting"));
  await expect(page.getByRole("button", { name: action("choose", "Alchemist") })).toBeDisabled();
  await screenshot(page, testInfo, "mentor-waiting");

  await page.clock.runFor(100);
  await expect(page.getByRole("status")).toHaveText(fill(server("chosen"), { specialization: server("name.wizard") }));
  expect(await readSent(page, "MentorChoose")).toEqual([["Wizard"]]);
});

test("Escape leaves the mentor", async ({ page }) => {
  await page.goto("/mentor/index.html");
  await expect(page.getByRole("button", { name: action("choose", "Wizard") })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.locator(".mentor")).toHaveCount(0);
  expect(await readSent(page, "MentorClose")).toEqual([[]]);
});
