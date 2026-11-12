import { test, expect, readTexts, screenshot, expectNoHorizontalOverflow } from "./fixtures.js";

// Polish runs longer than English: every page in Polish, as the game names it once the page is up. Each page shows the
// text of its group and key, the chat's in its field's placeholder.
const PAGES = [
  { url: "/creator/index.html", root: ".creator", text: ["ui.creator", "title"] },
  { url: "/chat/index.html#open", root: ".chat", text: ["ui.chat", "placeholder"], placeholder: true },
  { url: "/radial/index.html", root: ".radial", text: ["ui.radial", "hint"] },
  { url: "/radial/index.html#staff", root: ".radial", text: ["ui.radial", "hint_paged"] },
  { url: "/radial/index.html#emotes", root: ".radial", text: ["ui.radial", "hint_paged"] },
  { url: "/exchange/index.html#offered", root: ".exchange", text: ["ui.exchange", "cancel"] },
  { url: "/party/index.html#member", root: ".party", text: ["ui.party", "away"] },
  { url: "/asks/index.html#trade", root: ".asks", text: ["ui.asks", "title.trade"] },
  { url: "/trader/index.html", root: ".trader__panel", text: ["ui.trader", "shop.general"] },
  { url: "/bank/index.html", root: ".bank__panel", text: ["ui.bank", "all_carried"] },
  { url: "/notices/index.html", root: ".notices__panel", text: ["ui.notices", "post"] },
  { url: "/notices/index.html#full", root: ".notices__panel", text: ["ui.notices", "take_down"] },
  { url: "/menu/index.html", root: ".menu", text: ["ui.menu", "tab.character"] },
  { url: "/menu/index.html#account", root: ".menu", text: ["ui.menu", "account.language"] },
  { url: "/admin/index.html", root: ".admin", text: ["ui.admin", "players.search"] },
  { url: "/admin/index.html#card", root: ".admin", text: ["ui.admin", "card.inventory"] },
  { url: "/admin/index.html#sanctions", root: ".admin", text: ["ui.admin", "sanctions.by"] },
  { url: "/admin/index.html#audit", root: ".admin", text: ["ui.admin", "audit.older"] },
  { url: "/admin/index.html#world", root: ".admin", text: ["ui.admin", "world.spawn"] },
  { url: "/frozen/index.html", root: ".frozen", text: ["ui.frozen", "title"] },
  { url: "/menu/index.html#lore", root: ".menu", text: ["ui.menu", "lore.open"] },
  { url: "/lore/index.html", root: ".lore", text: ["ui.lore", "part.identity"] },
  { url: "/lore/index.html#view", root: ".lore", text: ["ui.lore", "viewer_lead"] },
  { url: "/asks/index.html#introduce", root: ".asks", text: ["ui.asks", "title.introduce"] },
  { url: "/menu/index.html#faction", root: ".menu", text: ["ui.menu", "faction.roll"] },
  { url: "/menu/index.html#factionless", root: ".menu", text: ["ui.menu", "faction.found"] },
  { url: "/admin/index.html#factions", root: ".admin", text: ["ui.admin", "factions.search"] },
  { url: "/asks/index.html#faction", root: ".asks", text: ["ui.asks", "title.faction"] },
];

for (const each of PAGES) {
  const [, path, scenario] = each.url.match(/^\/(\w+)\/index\.html(?:#(\w+))?$/);
  const name = scenario ? `pl-${path}-${scenario}` : `pl-${path}`;

  test(`${name}: the page speaks Polish and keeps within its frame`, async ({ page }, testInfo) => {
    const [group, key] = each.text;
    const text = readTexts(group, "pl")(key);
    await page.goto(each.url);
    await expect(page.locator(each.root).first()).toBeVisible();
    await page.evaluate(() => window.__readym.emit("Locale", "pl"));
    await expect((each.placeholder ? page.getByPlaceholder(text) : page.getByText(text)).first()).toBeVisible();
    await expect(page.locator("html")).toHaveAttribute("lang", "pl");
    await expectNoHorizontalOverflow(page, each.root);
    await screenshot(page, testInfo, name);
  });
}
