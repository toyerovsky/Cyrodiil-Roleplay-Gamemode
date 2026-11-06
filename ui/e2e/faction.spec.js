import { test, expect, readTexts, screenshot, readSent, expectNoHorizontalOverflow } from "./fixtures.js";

const menu = readTexts("ui.menu");
const admin = readTexts("ui.admin");
const asks = readTexts("ui.asks");

const factionTab = (page) => page.getByRole("tab", { name: menu("tab.faction") });
const answer = (page) => page.locator(".menu__faction-answer");
const member = (page, name) => page.locator(".menu__faction-member").filter({ hasText: name });
const rank = (page, name) => page.locator(".menu__faction-rank-row").filter({ has: page.getByText(name, { exact: true }) });
const ran = async (page) => (await readSent(page, "MenuFaction")).map(([line]) => line);

test("a player in no faction founds one: a short name refused at its field, a taken one by the server", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#factionless");
  await expect(factionTab(page)).toHaveAttribute("aria-selected", "true");
  const panel = page.getByRole("tabpanel");
  await expect(panel).toContainText("Breton invites you to join Mages Guild.");
  await expect(panel).toContainText("Founding costs 500 gold");
  await expectNoHorizontalOverflow(page, ".menu__content");
  await screenshot(page, testInfo, "faction-none");

  const name = panel.getByLabel(menu("faction.name"));
  await name.fill("ab");
  await name.press("Enter");
  await expect(panel.getByRole("alert")).toHaveText("The name has 3 to 32 characters.");
  await expect(name).toHaveAttribute("aria-invalid", "true");
  expect(await ran(page)).toEqual([]);

  // Q and E typed into the field are letters, not the turn of a tab.
  await name.fill("");
  await page.keyboard.type("Fighters Guild");
  await expect(name).toHaveValue("Fighters Guild");
  await expect(factionTab(page)).toHaveAttribute("aria-selected", "true");
  await panel.getByRole("button", { name: menu("faction.found_button") }).click();
  await expect(answer(page)).toHaveText("There is a faction called Fighters Guild already.");
  await expect(answer(page)).toHaveAttribute("role", "alert");
  await expect(name).toHaveValue("Fighters Guild");
  await screenshot(page, testInfo, "faction-name-taken");

  await name.fill("Blackwood Company");
  await name.press("Enter");
  await expect(answer(page)).toHaveText("You founded Blackwood Company for 500 gold. You are its leader.");
  await expect(panel.locator(".menu__faction-member")).toHaveCount(1);
  await expect(member(page, "Marcus Varro")).toContainText("Leader");
  await expect(panel).toContainText(menu("faction.last_member"));
  expect((await ran(page)).at(-1)).toBe("/faction create Blackwood Company");
  await screenshot(page, testInfo, "faction-founded");
});

test("an invitation is taken up from the tab, and a member without permissions only reads the roll", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#factionless");
  const panel = page.getByRole("tabpanel");
  await panel.getByRole("button", { name: menu("faction.join") }).click();
  await expect(answer(page)).toHaveText("You joined Mages Guild as Associate.");
  await expect(member(page, "Marcus Varro")).toContainText(menu("faction.you"));
  await expect(panel.getByRole("button", { name: menu("faction.remove") })).toHaveCount(0);
  await expect(panel.getByRole("button", { name: menu("faction.change_rank") })).toHaveCount(0);
  await expect(panel.getByLabel(menu("faction.new_rank"))).toHaveCount(0);
  await expect(rank(page, "Warder")).toContainText("May: Invite");
  expect(await ran(page)).toEqual(["/faction accept"]);
  await screenshot(page, testInfo, "faction-member");

  const leave = panel.getByRole("button", { name: menu("faction.leave") });
  await leave.scrollIntoViewIfNeeded();
  await leave.click();
  await expect(panel.getByRole("button", { name: menu("faction.sure") })).toBeVisible();
  await panel.getByRole("button", { name: menu("faction.sure") }).click();
  await expect(answer(page)).toHaveText("You left Mages Guild.");
  await expect(panel.getByLabel(menu("faction.name"))).toBeVisible();
});

test("a leader gives ranks and removes members, the removal on the second click", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#faction");
  const panel = page.getByRole("tabpanel");
  await expect(panel).toContainText("Fighters Guild");
  await expect(panel).toContainText("6, in the game: 3");
  await expect(panel.locator(".menu__faction-member")).toHaveCount(6);
  await expect(panel).toContainText(menu("faction.sole_leader"));
  await expectNoHorizontalOverflow(page, ".menu__content");
  await screenshot(page, testInfo, "faction-leader");

  // The leader's own row has no buttons: a sole leader keeps the rank.
  await expect(member(page, "Marcus Varro").getByRole("button")).toHaveCount(0);

  const burz = member(page, "Burz gro-Khash");
  await burz.getByRole("button", { name: menu("faction.change_rank") }).click();
  const choice = burz.getByRole("group");
  await expect(choice.getByRole("button")).toHaveText(["Master", "Champion", "Associate"]);
  await screenshot(page, testInfo, "faction-rank-choice");
  await choice.getByRole("button", { name: "Champion" }).click();
  await expect(answer(page)).toHaveText("Burz gro-Khash is now Champion.");
  await expect(burz).toContainText("Champion");

  const vigdis = member(page, "Vigdis");
  await vigdis.getByRole("button", { name: menu("faction.remove") }).click();
  await expect(vigdis.getByRole("button", { name: menu("faction.sure") })).toBeVisible();
  await expect(panel.locator(".menu__faction-member")).toHaveCount(6);
  await vigdis.getByRole("button", { name: menu("faction.sure") }).click();
  await expect(answer(page)).toHaveText("You removed Vigdis from the faction.");
  await expect(panel.locator(".menu__faction-member")).toHaveCount(5);
  expect(await ran(page)).toEqual(["/faction rank #3 #3", "/faction kick #6"]);
});

test("a leader sets what each rank may do, adds, renames, moves and deletes ranks", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#faction");
  const panel = page.getByRole("tabpanel");
  await expect(rank(page, "Master")).toContainText(menu("faction.rank_leader"));
  await expect(rank(page, "Associate")).toContainText(menu("faction.rank_entry"));

  const warder = rank(page, "Warder");
  const kick = warder.getByRole("button", { name: menu("faction.perm.Kick") });
  await expect(kick).toHaveAttribute("aria-pressed", "false");
  await kick.click();
  await expect(kick).toHaveAttribute("aria-pressed", "true");
  await expect(answer(page)).toHaveText("The rank Warder now has the permission: removing members.");

  const added = panel.getByLabel(menu("faction.new_rank"));
  await added.fill("Warder");
  await added.press("Enter");
  await expect(answer(page)).toHaveText("There is a rank called Warder already.");
  await added.fill("Journeyman");
  await added.press("Enter");
  await expect(answer(page)).toHaveText("The rank Journeyman is added, above the entry rank.");
  await expect(added).toHaveValue("");
  await expect(panel.locator(".menu__faction-rank-name")).toHaveText(["Master", "Champion", "Warder", "Journeyman", "Associate"]);

  const journeyman = rank(page, "Journeyman");
  await journeyman.getByRole("button", { name: menu("faction.up") }).click();
  await expect(panel.locator(".menu__faction-rank-name")).toHaveText(["Master", "Champion", "Journeyman", "Warder", "Associate"]);

  await journeyman.getByRole("button", { name: menu("faction.rename") }).click();
  const renamed = panel.getByLabel(menu("faction.rank_name"));
  await expect(renamed).toHaveValue("Journeyman");
  await renamed.fill("Swordsman of the Guild");
  await screenshot(page, testInfo, "faction-rank-rename");
  await renamed.press("Enter");
  await expect(answer(page)).toHaveText("The rank Journeyman is now called Swordsman of the Guild.");
  await expect(panel.getByLabel(menu("faction.rank_name"))).toHaveCount(0);

  const swordsman = rank(page, "Swordsman of the Guild");
  await swordsman.getByRole("button", { name: menu("faction.delete") }).click();
  await swordsman.getByRole("button", { name: menu("faction.sure") }).click();
  await expect(panel.locator(".menu__faction-rank-name")).toHaveText(["Master", "Champion", "Warder", "Associate"]);
  expect(await ran(page)).toEqual([
    "/faction perm #4 kick on",
    "/faction rankadd Warder",
    "/faction rankadd Journeyman",
    "/faction rankmove #5 up",
    "/faction rankname #5 Swordsman of the Guild",
    "/faction rankdel #5",
  ]);

  // The tab is longer than the letter: its end scrolls into view.
  const content = page.locator(".menu__content");
  await content.evaluate((element) => element.scrollTo({ top: element.scrollHeight }));
  await expect(panel.getByRole("heading", { name: menu("faction.leaving") })).toBeInViewport();
  await expectNoHorizontalOverflow(page, ".menu__content");
  await screenshot(page, testInfo, "faction-leader-end");
});

test("a member of the entry rank sees no button but the one to leave", async ({ page }, testInfo) => {
  await page.goto("/menu/index.html#factionmember");
  const panel = page.getByRole("tabpanel");
  await expect(member(page, "Lum gro-Baroth")).toContainText(menu("faction.you"));
  await expect(panel.getByRole("button")).toHaveText([menu("faction.leave")]);
  await expect(panel.getByRole("textbox")).toHaveCount(0);
  await screenshot(page, testInfo, "faction-associate");
});

test("the staff read every faction, rename one, make a leader, remove a member and dissolve with a reason", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#factions");
  await expect(page.getByRole("tab", { name: admin("tab.factions") })).toHaveAttribute("aria-selected", "true");
  const list = page.getByRole("region", { name: admin("factions.list") });
  await expect(list.locator(".admin-faction")).toHaveCount(3);
  await expect(page.getByText(admin("factions.pick"))).toBeVisible();
  await screenshot(page, testInfo, "admin-factions");

  await list.getByLabel(admin("factions.search")).fill("guild");
  await expect(list.locator(".admin-faction")).toHaveCount(2);
  await list.getByLabel(admin("factions.search")).fill("");

  await list.locator('[data-faction="7"]').click();
  const card = page.locator('[data-card="7"]');
  await expect(card.getByRole("heading", { name: /Fighters Guild/ })).toBeVisible();
  await expect(card.locator("[data-member]")).toHaveCount(13);
  await expect(card.locator('[data-rank="3"]')).toContainText("Invite, Remove members, Manage ranks");
  await expect(card.locator('[data-member="1"]')).toContainText("In the game, [1]");
  await expect(card.locator('[data-member="1"]').getByRole("button", { name: admin("action.factionLead") })).toHaveCount(0);
  await expectNoHorizontalOverflow(page, ".admin__panel");
  await expectNoHorizontalOverflow(page, ".admin-factions__card");
  await screenshot(page, testInfo, "admin-faction-card");

  await card.getByRole("button", { name: admin("action.factionRename") }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel(admin("field.factionName"))).toBeFocused();
  await dialog.getByLabel(admin("field.factionName")).fill("ab");
  await page.keyboard.press("Enter");
  await expect(dialog.getByRole("alert")).toHaveText("The name has 3 to 32 characters.");
  await screenshot(page, testInfo, "admin-faction-rename");
  await dialog.getByLabel(admin("field.factionName")).fill("Blackwood Company");
  await page.keyboard.press("Enter");
  await expect(page.locator(".admin__status")).toHaveText("Fighters Guild is now called Blackwood Company.");
  await expect(card.getByRole("heading", { name: /Blackwood Company/ })).toBeVisible();
  await expect(list.locator('[data-faction="7"]')).toContainText("Blackwood Company");

  await card.locator('[data-member="2"]').getByRole("button", { name: admin("action.factionLead") }).click();
  await expect(page.locator(".admin__status")).toHaveText("Modryn Oreyn is now a leader of Blackwood Company.");
  await expect(card.locator('[data-member="2"]').getByRole("button", { name: admin("action.factionLead") })).toHaveCount(0);

  await card.locator('[data-member="5"]').getByRole("button", { name: admin("action.factionRemove") }).click();
  await expect(page.locator(".admin__status")).toHaveText("Lum gro-Baroth is removed from Blackwood Company.");
  await expect(card.locator("[data-member]")).toHaveCount(12);

  await card.getByRole("button", { name: admin("action.factionDissolve") }).click();
  await dialog.getByRole("button", { name: admin("dialog.run") }).click();
  await expect(dialog.getByRole("alert")).toBeVisible();
  await dialog.getByLabel(admin("field.reason")).fill("Griefing the market, after two warnings");
  await page.keyboard.press("Enter");
  await expect(page.locator(".admin__status")).toHaveText("Blackwood Company is dissolved.");
  await expect(page.locator("[data-card]")).toHaveCount(0);
  await expect(list.locator(".admin-faction")).toHaveCount(2);
  expect((await readSent(page, "AdminRun")).map(([line]) => line)).toEqual([
    "/afaction rename #7 Blackwood Company",
    "/afaction lead #7 #2",
    "/afaction remove #7 #5",
    "/afaction dissolve #7 Griefing the market, after two warnings",
  ]);
  await screenshot(page, testInfo, "admin-faction-dissolved");
});

test("a moderator off duty reads a faction, its buttons waiting for the duty", async ({ page }, testInfo) => {
  await page.goto("/admin/index.html#offduty");
  await page.getByRole("tab", { name: admin("tab.factions") }).click();
  await page.locator('[data-faction="9"]').click();
  const card = page.locator('[data-card="9"]');
  await expect(card.locator("[data-member]")).toHaveCount(2);
  await expect(card.getByRole("button")).toHaveCount(0);
  await screenshot(page, testInfo, "admin-faction-moderator");
});

test("an invitation to a faction shows at the top with Y and N", async ({ page }, testInfo) => {
  await page.goto("/asks/index.html#faction");
  const invitation = page.getByRole("region", { name: asks("label") }).getByRole("article");
  await expect(invitation).toContainText(asks("title.faction"));
  await expect(invitation).toContainText("Imperial invites you to their faction.");
  await expect(invitation.locator("kbd")).toHaveText(["Y", "N"]);
  await screenshot(page, testInfo, "asks-faction");
  await page.keyboard.press("y");
  await expect(invitation).toHaveCount(0);
});
