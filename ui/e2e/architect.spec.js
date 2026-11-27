import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const t = readTexts("ui.architect");
const server = readTexts("building");

// Where on the screen, as a fraction of it: left of 0.5 the pretend game picks the table, right of 0.8 nothing fits.
async function at(page, x, y) {
  const size = page.viewportSize();
  return { x: size.width * x, y: size.height * y };
}

async function drag(page, from, to) {
  await page.mouse.move(from.x, from.y);
  await page.mouse.down();
  await page.mouse.move((from.x + to.x) / 2, (from.y + to.y) / 2, { steps: 4 });
  await page.mouse.move(to.x, to.y, { steps: 4 });
}

test("the catalogue opens over the screen, a piece dragged out of it lands on the plot and C brings it back", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html");
  const status = page.getByRole("status");
  const catalogue = page.getByRole("region", { name: t("drawer") });
  await expect(catalogue).toBeVisible();
  await expect(page.locator("[data-facts]")).toHaveText("Residential, 12 by 8 tiles, 15 m high, pieces: 4");
  await expect(catalogue.getByRole("tab", { name: t("category.Houses") })).toHaveAttribute("aria-selected", "true");
  await expect(catalogue.locator("[data-piece]")).toHaveCount(6);
  await expect(catalogue.locator("img.architect__picture")).toHaveCount(6);
  await expectNoHorizontalOverflow(page, ".architect__catalogue-panel");
  await screenshot(page, testInfo, "architect-catalogue");

  await catalogue.getByRole("tab", { name: t("category.Furniture") }).click();
  await expect(catalogue.getByRole("tab", { name: t("category.Furniture") })).toHaveAttribute("aria-selected", "true");
  const chest = catalogue.locator('[data-piece="furniture.chest"]');
  const box = await chest.boundingBox();
  await drag(page, { x: box.x + box.width / 2, y: box.y + box.height / 2 }, await at(page, 0.9, 0.45));
  await expect(page.locator("[data-catalogue]")).toHaveCount(0);
  await expect(status).toHaveText(t("fit.Outside"));
  await screenshot(page, testInfo, "architect-outside");
  await page.mouse.move(...Object.values(await at(page, 0.3, 0.45)), { steps: 4 });
  await expect(status).toHaveText(t("fit.Fits"));
  await page.mouse.up();

  await expect(status).toHaveText(server("placed").replace("{name}", "Chest"));
  await expect(page.locator("[data-facts]")).toHaveText("Residential, 12 by 8 tiles, 15 m high, pieces: 5");
  await expect(page.locator("[data-catalogue]")).toHaveCount(0);
  await screenshot(page, testInfo, "architect-placed");
  const grabs = await readSent(page, "ArchitectGrab");
  expect(grabs[0][2]).toBe("furniture.chest");
  expect((await readSent(page, "ArchitectDrop")).length).toBe(1);

  await page.keyboard.press("c");
  await expect(catalogue).toBeVisible();
  await expect(catalogue.getByRole("tab", { name: t("category.Furniture") })).toHaveAttribute("aria-selected", "true");
  await page.keyboard.press("Escape");
  await expect(page.locator("[data-catalogue]")).toHaveCount(0);
  await page.getByRole("button", { name: t("pieces") }).click();
  await expect(catalogue).toBeVisible();
});

test("Esc while a piece is dragged puts it back, and a piece not drawn yet shows its frame", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html#blank");
  const piece = page.locator('[data-piece="house.tent-1"]');
  await expect(piece).toBeVisible();
  await expect(piece.locator(".architect__picture--waiting")).toHaveCount(1);
  await screenshot(page, testInfo, "architect-blank");
  const box = await piece.boundingBox();
  await drag(page, { x: box.x + box.width / 2, y: box.y + box.height / 2 }, await at(page, 0.3, 0.4));
  await page.keyboard.press("Escape");
  await page.mouse.up();
  expect((await readSent(page, "ArchitectCancel")).length).toBe(1);
  expect(await readSent(page, "ArchitectDrop")).toEqual([]);
});

test("the staff pick a piece, turn it with the keys, take it away and let go with Esc", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html");
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.locator("[data-catalogue]")).toHaveCount(0);
  const point = await at(page, 0.3, 0.4);
  await page.mouse.click(point.x, point.y);
  const picked = page.locator("[data-picked]");
  await expect(picked).toContainText(t("picked").replace("{name}", "Table"));
  await screenshot(page, testInfo, "architect-picked");

  // The gizmo shows over the world where the game projects it, and the arrow under the mouse lights up
  const gizmo = page.locator("[data-gizmo]");
  await expect(gizmo.locator("[data-axis]")).toHaveCount(3);
  const handle = await at(page, 0.34, 0.62);
  await page.mouse.move(handle.x, handle.y);
  await expect(gizmo.locator('[data-axis="x"]')).toHaveClass(/is-hot/);
  await expect(gizmo.locator('[data-axis="z"]')).not.toHaveClass(/is-hot/);
  await screenshot(page, testInfo, "architect-gizmo");

  // A click only picks: a shake of the mouse under the drag's threshold sends no point, so the piece stays put
  await page.mouse.move(point.x, point.y);
  await page.mouse.down();
  await page.mouse.move(point.x + 3, point.y + 2);
  await page.mouse.up();
  expect(await readSent(page, "ArchitectPoint")).toEqual([]);
  await expect(picked).toBeVisible();

  // Picked, the keys edit the piece: the view's keys and its line give way
  await expect(page.locator("[data-keys]")).toHaveText(t("edit_keys"));
  // Copied while the game has nothing left to answer, so no answer comes over the copy's note
  await page.keyboard.press("Control+c");
  await expect(page.getByRole("status")).toHaveText(t("copied").replace("{name}", "Table"));
  await page.keyboard.press("q");
  await page.keyboard.press("Shift+E");
  await page.keyboard.down("z");
  await page.keyboard.press("w");
  await page.keyboard.press("a");
  await page.keyboard.up("z");
  expect(await readSent(page, "ArchitectTurn")).toEqual([[-15], [1]]);
  expect(await readSent(page, "ArchitectRaise")).toEqual([[10]]);
  await expect(page.getByRole("status")).toHaveText(server("moved").replace("{name}", "Table"));

  await page.keyboard.press("w");
  await page.keyboard.press("d");
  await page.keyboard.press("ArrowUp");
  await page.keyboard.press("ArrowLeft");
  await picked.getByRole("button", { name: t("move_right") }).click();
  await picked.getByRole("button", { name: t("move_back") }).click();
  expect(await readSent(page, "ArchitectNudge")).toEqual([[1, 0], [0, 1], [1, 0], [0, -1], [0, 1], [-1, 0]]);
  expect(await readSent(page, "ArchitectPan")).toEqual([]);
  await page.keyboard.press("Shift+ArrowUp");
  await page.keyboard.press("Shift+ArrowDown");
  await page.keyboard.down("z");
  await page.keyboard.press("s");
  await page.keyboard.up("z");
  expect(await readSent(page, "ArchitectRaise")).toEqual([[10], [10], [-10], [-10]]);

  await page.keyboard.press("f");
  expect((await readSent(page, "ArchitectSnapToFloor")).length).toBe(1);
  await page.keyboard.press("r");
  expect(await readSent(page, "ArchitectRaise")).toHaveLength(4);

  await page.keyboard.press("Control+d");
  await picked.getByRole("button", { name: t("duplicate") }).click();
  expect((await readSent(page, "ArchitectCopy")).length).toBe(1);
  expect((await readSent(page, "ArchitectDuplicate")).length).toBe(2);
  await picked.getByRole("button", { name: t("snap_to_floor") }).click();
  expect((await readSent(page, "ArchitectSnapToFloor")).length).toBe(2);
  // Ctrl+C neither opened the pieces nor C took Ctrl's place
  await expect(page.locator("[data-catalogue]")).toHaveCount(0);

  await page.keyboard.press("g");
  await expect(page.locator('[data-step="2"]')).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator('[data-step="1"]')).toHaveAttribute("aria-pressed", "false");

  await picked.getByRole("button", { name: t("remove") }).click();
  await expect(page.getByRole("status")).toHaveText(server("removed").replace("{name}", "Table"));
  await expect(picked).toHaveCount(0);
  await expect(page.locator("[data-gizmo]")).toHaveCount(0);

  // Ctrl+Z takes the removal back, a held one only once, and the button asks again when there is nothing left
  await page.keyboard.down("Control");
  await page.keyboard.down("z");
  await page.keyboard.down("z");
  await page.keyboard.up("z");
  await page.keyboard.up("Control");
  expect(await readSent(page, "ArchitectUndo")).toHaveLength(1);
  await expect(page.getByRole("status")).toHaveText(server("undone_remove").replace("{name}", "Table"));
  await screenshot(page, testInfo, "architect-undone");
  await page.locator('[data-action="undo"]').click();
  expect(await readSent(page, "ArchitectUndo")).toHaveLength(2);
  await expect(page.getByRole("status")).toHaveText(server("undo_nothing"));

  // Nothing picked: Ctrl+V puts the copy under the mouse, and WASD move the view again
  const under = await at(page, 0.35, 0.55);
  await page.mouse.move(under.x, under.y);
  await page.keyboard.press("Control+v");
  const [pasted] = await readSent(page, "ArchitectPaste");
  expect(pasted[0]).toBeCloseTo(0.35, 2);
  expect(pasted[1]).toBeCloseTo(0.55, 2);
  await expect(page.getByRole("status")).toHaveText(server("placed").replace("{name}", "Table"));
  await page.keyboard.down("w");
  await page.keyboard.up("w");
  expect(await readSent(page, "ArchitectPan")).toEqual([[1, 0], [0, 0]]);

  await page.keyboard.press("Escape");
  expect((await readSent(page, "ArchitectLeave")).length).toBe(1);
  await expect(page.locator(".architect")).toHaveCount(0);
});

test("the panel sets the grid, how far a turn goes and what a piece stands on", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html");
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.locator("[data-catalogue]")).toHaveCount(0);
  const panel = page.getByRole("complementary", { name: t("panel") });
  await expect(panel).toBeVisible();
  await expectNoHorizontalOverflow(page, ".architect__panel");

  await panel.getByRole("button", { name: t("grid_4") }).click();
  await expect(panel.locator('[data-step="4"]')).toHaveAttribute("aria-pressed", "true");
  await panel.getByRole("button", { name: t("degrees").replace("{degrees}", "90") }).click();
  await panel.getByRole("button", { name: t("stand_ground") }).click();
  await expect(panel.locator('[data-settle="ground"]')).toHaveAttribute("aria-pressed", "true");
  await panel.getByRole("button", { name: t("centimetres").replace("{centimetres}", "25") }).click();
  await expect(panel.locator('[data-height="25"]')).toHaveAttribute("aria-pressed", "true");
  // The gizmo's height snaps to the step: the game hears the first one and every change
  await expect.poll(() => readSent(page, "ArchitectSetHeight")).toEqual([[10], [25]]);
  await screenshot(page, testInfo, "architect-panel");

  await page.keyboard.press("e");
  await page.keyboard.press("Shift+Q");
  // Nothing picked, R turns the plot's front, as its button does
  await page.keyboard.press("r");
  await panel.getByRole("button", { name: t("turn_front") }).click();
  expect((await readSent(page, "ArchitectTurnFront")).length).toBe(2);
  await expect(page.getByRole("status")).toHaveText(server("sign_moved"));
  expect(await readSent(page, "ArchitectSetStep")).toEqual([[4]]);
  expect(await readSent(page, "ArchitectSetSettle")).toEqual([[false]]);
  expect(await readSent(page, "ArchitectTurn")).toEqual([[90], [-1]]);
});

test("a picked piece goes up and down by the height chosen, X lets go of it", async ({ page }) => {
  await page.goto("/architect/index.html#picked");
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  await page.keyboard.press("Escape");
  const panel = page.getByRole("complementary", { name: t("panel") });
  await panel.getByRole("button", { name: t("centimetres").replace("{centimetres}", "25") }).click();
  await page.keyboard.down("z");
  await page.keyboard.press("s");
  await page.keyboard.press("Shift+w");
  await page.keyboard.up("z");
  expect(await readSent(page, "ArchitectRaise")).toEqual([[-25], [1]]);

  await page.keyboard.press("x");
  expect((await readSent(page, "ArchitectDeselect")).length).toBe(1);
  await expect(page.locator("[data-picked]")).toHaveCount(0);
  await expect(page.locator("[data-keys]")).toHaveText(t("view_keys"));
});

test("off a plot, /abuild lists the plots near to build on, a new plot to mark, and Plots goes back", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html#choose");
  const list = page.getByRole("complementary", { name: t("choose_title") });
  await expect(list).toBeVisible();
  await expect(page.locator(".architect__title")).toHaveText(`${t("title")}: ${t("choose_title")}`);
  await expect(page.getByRole("status")).toHaveText(t("choose_hint"));
  await expect(page.locator("[data-catalogue]")).toHaveCount(0);
  await expect(list.locator("li")).toHaveCount(2);
  await expectNoHorizontalOverflow(page, ".architect__panel");
  await screenshot(page, testInfo, "architect-choose");

  await list.getByRole("button", { name: t("build_on").replace("{plot}", t("plot").replace("{id}", "7")) }).click();
  expect(await readSent(page, "ArchitectChoose")).toEqual([[7]]);
  await expect(page.locator(".architect__title")).toHaveText(`${t("title")}: ${t("plot").replace("{id}", "7")}`);
  await expect(page.locator("[data-catalogue]")).toBeVisible();

  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: t("plots") }).click();
  expect(await readSent(page, "ArchitectChoose")).toEqual([[7], [0]]);
  await expect(list).toBeVisible();

  await page.getByRole("button", { name: t("new_plot_button") }).click();
  expect((await readSent(page, "ArchitectNewPlot")).length).toBe(1);
  await expect(page.getByRole("status")).toHaveText(t("mark_hint"));
});

test("a plot is removed from the list or while building on it, only once the page asked, and Esc keeps it", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html#choose");
  const list = page.getByRole("complementary", { name: t("choose_title") });
  await list.getByRole("button", { name: t("remove_named").replace("{plot}", t("plot").replace("{id}", "7")) }).click();
  const confirm = page.getByRole("alertdialog");
  await expect(confirm).toContainText(t("remove_confirm").replace("{id}", "7").replace("{pieces}", "0"));
  await screenshot(page, testInfo, "architect-remove-plot");
  await page.keyboard.press("Escape");
  await expect(confirm).toHaveCount(0);
  expect(await readSent(page, "ArchitectRemovePlot")).toEqual([]);
  expect(await readSent(page, "ArchitectLeave")).toEqual([]);

  await list.getByRole("button", { name: t("build_on").replace("{plot}", t("plot").replace("{id}", "3")) }).click();
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  await page.keyboard.press("Escape");
  await page.getByRole("complementary", { name: t("panel") }).getByRole("button", { name: t("remove_plot") }).click();
  await expect(confirm).toContainText(t("remove_confirm").replace("{id}", "3").replace("{pieces}", "4"));
  await confirm.getByRole("button", { name: t("remove_no") }).click();
  expect(await readSent(page, "ArchitectRemovePlot")).toEqual([]);

  await page.getByRole("complementary", { name: t("panel") }).getByRole("button", { name: t("remove_plot") }).click();
  await confirm.getByRole("button", { name: t("remove_yes") }).click();
  expect(await readSent(page, "ArchitectRemovePlot")).toEqual([[3]]);
  await expect(page.getByRole("status")).toHaveText(server("plot_removed").other.replace("{id}", "3").replace("{count}", "4"));
  await expect(list).toBeVisible();
});

test("with no plot near, the list says so", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html#far");
  await expect(page.locator("[data-no-plots]")).toHaveText(t("no_plots"));
  await screenshot(page, testInfo, "architect-choose-none");
});

test("the staff mark a new plot, a wrong price is refused, and the plot opens to build on", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html#mark");
  await expect(page.getByRole("status")).toHaveText(t("mark_hint"));
  await expect(page.locator("[data-facts]")).toHaveCount(0);
  await expect(page.getByRole("region", { name: t("drawer") })).toHaveCount(0);
  await screenshot(page, testInfo, "architect-mark");

  await drag(page, await at(page, 0.3, 0.4), await at(page, 0.6, 0.6));
  await page.mouse.up();
  const form = page.getByRole("region", { name: t("form") });
  await expect(form).toBeVisible();
  await expect(page.locator("[data-size]")).toHaveText("12 by 8 tiles, 21.9 m by 14.6 m");
  // R and the form's button turn the front, the sign with it
  await page.keyboard.press("r");
  await form.getByRole("button", { name: t("turn_front") }).click();
  expect((await readSent(page, "ArchitectTurnFront")).length).toBe(2);

  await form.getByRole("button", { name: t("kind.Industrial") }).click();
  await form.getByLabel(t("price")).fill("a lot");
  await form.getByRole("button", { name: t("save") }).click();
  await expect(page.getByRole("alert")).toHaveText(t("gold_invalid").replace("{most}", "1000000"));
  await expect(form.getByLabel(t("price"))).toHaveAttribute("aria-invalid", "true");
  await expect(form.getByLabel(t("height"))).not.toHaveAttribute("aria-invalid", "true");
  await screenshot(page, testInfo, "architect-form-refused");
  expect(await readSent(page, "ArchitectSavePlot")).toEqual([]);

  await form.getByLabel(t("price")).fill("2500");
  await form.getByLabel(t("height")).fill("12");
  await form.getByRole("button", { name: t("save") }).click();
  const [[json]] = await readSent(page, "ArchitectSavePlot");
  expect(JSON.parse(json)).toEqual({ kind: "Industrial", price: 2500, weeklyTax: 100, heightMetres: 12 });
  await expect(page.getByRole("status")).toHaveText(server("plot_made").replace("{id}", "4").replace("{width}", "12").replace("{depth}", "8"));
  await expect(page.locator("[data-facts]")).toHaveText("Industrial, 12 by 8 tiles, 12 m high, pieces: 0");
  await expect(page.getByRole("region", { name: t("drawer") })).toBeVisible();
});

test("the marked plot's form shows at once, and Esc clears the mark before it leaves", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html#marked");
  await expect(page.getByRole("region", { name: t("form") })).toBeVisible();
  await screenshot(page, testInfo, "architect-marked");
  await page.keyboard.press("Escape");
  await expect(page.getByRole("region", { name: t("form") })).toHaveCount(0);
  expect((await readSent(page, "ArchitectClearPlot")).length).toBe(1);
  expect(await readSent(page, "ArchitectLeave")).toEqual([]);
});

test("WASD moves the view, the right mouse button turns it and the wheel brings it nearer", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html");
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.locator("[data-catalogue]")).toHaveCount(0);
  await expect(page.locator(".architect__keys").first()).toHaveText(t("view_keys"));

  // Held keys: each change is sent once, not every repeat
  await page.keyboard.down("w");
  await page.keyboard.down("w");
  await page.keyboard.down("d");
  await page.keyboard.up("w");
  await page.keyboard.up("d");
  expect(await readSent(page, "ArchitectPan")).toEqual([[1, 0], [1, 1], [0, 1], [0, 0]]);

  const point = await at(page, 0.6, 0.5);
  await page.mouse.move(point.x, point.y);
  await page.mouse.down({ button: "right" });
  await page.mouse.move(point.x + 40, point.y + 10);
  await page.mouse.up({ button: "right" });
  await expect
    .poll(async () => (await readSent(page, "ArchitectOrbit")).reduce((sum, [across, down]) => [sum[0] + across, sum[1] + down], [0, 0]))
    .toEqual([40, 10]);
  expect(await readSent(page, "ArchitectGrab")).toEqual([]);

  await page.mouse.wheel(0, 100);
  await page.mouse.wheel(0, -100);
  await expect.poll(() => readSent(page, "ArchitectZoom")).toEqual([[1], [-1]]);
  expect(await readSent(page, "ArchitectTurn")).toEqual([]);
  await screenshot(page, testInfo, "architect-view");
});

test("the Kits tab lists a kit's parts, another kit's on its chip, and the game hears what is shown", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html");
  const catalogue = page.locator("[data-catalogue]");
  await expect(catalogue).toBeVisible();
  await catalogue.getByRole("tab", { name: t("category.Kits") }).click();
  const kits = catalogue.getByRole("group", { name: t("kit_label") });
  await expect(kits.locator('[data-kit="bruma-lower"]')).toHaveAttribute("aria-pressed", "true");
  await expect(catalogue.locator("[data-piece]")).toHaveCount(3);
  await expect(catalogue.locator('[data-piece="kit.brumahouselower01_wall_a"]')).toContainText("Wall A (01)");
  await screenshot(page, testInfo, "architect-kits");

  await kits.getByRole("button", { name: "Coastal cottage, inside" }).click();
  await expect(catalogue.locator("[data-piece]")).toHaveCount(1);
  await expect.poll(async () => (await readSent(page, "ArchitectShowing")).at(-1)).toEqual(["kit.anvillchouse_balk_a_01"]);
});

test("a picked house breaks apart into its kit's parts", async ({ page }) => {
  await page.goto("/architect/index.html#house");
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  await page.keyboard.press("Escape");
  const picked = page.locator("[data-picked]");
  await picked.getByRole("button", { name: t("break_apart") }).click();
  expect((await readSent(page, "ArchitectBreakApart")).length).toBe(1);
  await expect(page.getByRole("status")).toHaveText(server("broken_apart").other.replace("{name}", "Town house").replace("{count}", "8"));
  await expect(picked).toHaveCount(0);
});

test("the game's own furniture says it cannot be broken apart", async ({ page }) => {
  await page.goto("/architect/index.html#picked");
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  await page.keyboard.press("Escape");
  await page.locator("[data-picked]").getByRole("button", { name: t("break_apart") }).click();
  await expect(page.getByRole("status")).toHaveText(t("break.GameObject"));
});

test("in the player's own view the game has the mouse until something is picked or the pieces open, and L hides the rest", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html#player");
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  // The pieces open hold the mouse for the page
  await page.keyboard.press("Escape");
  await expect(page.locator("[data-playing]")).toHaveCount(1);
  await expect(page.locator("[data-crosshair]")).toBeVisible();
  await expect(page.locator("[data-panel]")).toHaveCount(0);
  await expect(page.locator("[data-keys]")).toHaveText(t("player_keys"));
  expect((await readSent(page, "ArchitectWantsInput")).at(-1)).toEqual([false]);
  await screenshot(page, testInfo, "architect-player");

  // C from the game opens the pieces, and the page asks for the mouse again
  await page.evaluate(() => window.__readym.emit("OpenCatalogue"));
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  expect((await readSent(page, "ArchitectWantsInput")).at(-1)).toEqual([true]);
  await page.keyboard.press("Escape");
  expect((await readSent(page, "ArchitectWantsInput")).at(-1)).toEqual([false]);

  // L from the game hides the bar too, and brings it back
  await page.evaluate(() => window.__readym.emit("ToggleHidden"));
  await expect(page.locator("[data-hidden]")).toHaveCount(1);
  await expect(page.locator("header, aside, section, [data-catalogue], [data-gizmo], [data-crosshair]")).toHaveCount(0);
  await page.evaluate(() => window.__readym.emit("ToggleHidden"));
  await expect(page.locator("[data-keys]")).toBeVisible();
});

test("L hides the interface over the plot, a click on the world still builds, and L or C brings it back", async ({ page }, testInfo) => {
  await page.goto("/architect/index.html");
  await expect(page.locator("[data-catalogue]")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.locator("[data-catalogue]")).toHaveCount(0);
  await page.keyboard.press("l");
  await expect(page.locator("[data-hidden]")).toHaveCount(1);
  await expect(page.locator("header, aside, section, [data-catalogue], [data-gizmo]")).toHaveCount(0);
  const point = await at(page, 0.3, 0.4);
  await page.mouse.click(point.x, point.y);
  expect(await readSent(page, "ArchitectGrab")).toHaveLength(1);
  await screenshot(page, testInfo, "architect-hidden");
  await page.keyboard.press("l");
  await expect(page.locator("[data-keys]")).toBeVisible();
  await page.keyboard.press("l");
  await page.keyboard.press("c");
  await expect(page.locator("[data-catalogue]")).toBeVisible();
});

test("the page reads in Polish", async ({ page }, testInfo) => {
  const pl = readTexts("ui.architect", "pl");
  await page.goto("/architect/index.html#polish");
  await expect(page.getByRole("status")).toHaveText(pl("build_hint"));
  await expectNoHorizontalOverflow(page, ".architect__bar");
  await screenshot(page, testInfo, "architect-polish");
});
