# Sanctuary of the Imperial City (Sanctuary 0.2.0 in the core)

Branch `2026-11-06-sanctuary`, commit `ac2ff5c` "Bring Sanctuary into the gamemode", on top of
`2026-11-06-playtest` (dice, lore card, playtest guide and docs). A separate branch on purpose: Sanctuary is for
Pete's internal tests first and is not part of the team playtest. Nothing of it has been tried in game yet.

## 1. What it is

Inside the walls of the Imperial City (not the Arena) blood may not be spilled: NPCs and guards get every hit undone,
and peaceful players are protected until they attack someone. The SDK has no hit event, so a server-side guardian
watches health every tick, puts drops back and guesses who caused them from who was fighting nearby.

Origin: Sanctuary 0.2.0 was Pete's standalone OB:MP mod. It is ported into the core so it can use the gamemode's own
staff roles and duty, database, F10 menu, `/admin` panel, G wheel and notices, which a separate mod cannot reach. The
rules are unchanged.

## 2. Rules (0.2.0, unchanged)

Inside the Imperial City walls, not in the Arena:

- **NPCs and guards are always untouchable.** Every drop in their health is put back at once.
- **Players are untouchable** until they attack someone, a player or an NPC.
- **An attacker becomes an aggressor:** no protection for 5 minutes after their last attack (or until they die), and a
  bounty of 40 for the guards. Every new attack restarts the clock.
- **The one attacked keeps the protection.** If they strike back, they become an aggressor too.
- **No revive.** A player killed by one blow gets up through OB:MP's own rebirth; dying ends an aggressor streak. An NPC
  killed by one blow stays dead and is logged.
- **The Arena is out:** its floor and stands are never part of the Sanctuary. Sewers, Subterrane, catacombs and the
  Waterfront are outside too. The Bloodworks is inside.
- Messages: "You enter the Sanctuary of the Imperial City. Blood may not be spilled here.", "You leave the Sanctuary of
  the Imperial City.", "The Sanctuary shields you." (at most every 3 s), "You attacked someone in the Imperial City. The
  Sanctuary no longer protects you.", and new in the port "The Sanctuary protects you again." when a streak ends.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| HUD badge | Top right while inside: shield, "Protected", or "Aggressor" with a countdown. Display only, never takes input | `ui/sanctuary/`, `Client/Sanctuary/SanctuaryOverlay.cs` |
| `/sanctuary` | Everyone (General): inside or outside, protected or aggressor, time left, or "the Sanctuary is off" | `Sanctuary/SanctuaryCommands.cs` |
| F10 | Tab `sanctuary`: state, countdown, the rules with the configured minutes and bounty | `ui/menu/SanctuaryTab.vue`, `PlayerMenu/MenuSanctuaryView.cs` |
| G wheel | "Sanctuary" (`self.sanctuary`, Order 65, `shield` icon), shown only while inside or an aggressor; opens the F10 tab | `SanctuaryCommands.cs` |
| H wheel | Nothing (Pete's rule: no invented player actions) | |
| `/asanctuary` | Staff on duty with `asanctuary`: state and the aggressors online | `SanctuaryCommands.Manage` |
| `/asanctuary <number> clear` | End a player's aggressor streak (audited `sanctuary.clear`) | `Guardian.TryClear` |
| `/asanctuary on\|off` | Turn the Sanctuary on or off at runtime (audited `sanctuary.on` / `sanctuary.off`) | `Guardian.Turn` |
| `/admin` | Tab `sanctuary` (needs `asanctuary`): on/off button, aggressors with "End the streak", incident log 50 a page with a name filter on the victim or an aggressor | `ui/admin/SanctuaryTab.vue` |
| Keys | No key of its own; the badge has no input. The F10 and `/admin` tabs use the mouse like the rest of those pages | |

## 4. How it works

`Guardian` runs on the update thread (`[UpdateOrder(2)]`, inside `GameThread` and `Watchdog`). Each tick:

1. **See:** one pass over `MainCharacter`s: area (from `PlayerAreas`, fed by the server's join/leave area events),
   position, `Hp`, `HpBase`, dead, `InCombat`, and whether they swung (`AttackingRequest`) or cast (`CastingRequest`).
2. **Watch players:** enter/leave messages; a `Ward` per player keeps the health it should have. A drop over 0.5 counts;
   smaller steps add up. Outside the zone the ward just follows the health.
3. **Blame** (`Blame.Judge`): every player who swung within `meleeRange` in the last `attackWindowSeconds` is a suspect;
   if none, every player within `rangedRange` who swung or cast while `InCombat`. Verdict `None`, `Suspect` or `Several`.
   Every suspect is marked an aggressor (bounty, staff notice on the first mark of a streak).
4. **Guard NPCs:** every NPC with a ref id in a zone area has its own ward; any drop is written back.
5. **Decide:** only after all drops and marks, a protected player gets the health back (`healAllDamage` true: any drop;
   false: only blamed drops). An aggressor takes the blow.
6. **Streaks and incidents:** expired streaks tell the player "protects you again"; incidents quiet for
   `incidentSeconds` are closed and written; NPCs no longer seen are swept.
7. **Badges:** `SanctuaryState(json)` goes to a player only when inside / aggressor / streak end changes.

Server-authoritative: everything. The client only shows the badge and the menu/panel pages.

- **Reconnect:** an aggressor who leaves keeps the streak by ReadyM account (`PlayerWatchRegistry`) and picks it up on
  return.
- **`/ahp`:** `AdminCommands` calls `Guardian.AcceptStaffHealth`: the ward takes the new value, and drops within 1 s after
  it are neither blamed nor put back.
- **Staff notices** ("X attacked Y in Z", "the aggressor X died in Z") go to staff with `asanctuary` and `/duty` on, as
  corner notices, with real names, and to the server log. Players are never told another player's name by the Sanctuary.

### RPCs and page messages

| Direction | Message |
|---|---|
| S->C | `RpContracts.SanctuaryState(string json)`: a `SanctuaryHudView` `{inside, aggressor, secondsLeft}` |
| Page | `sanctuary` calls `sendSanctuaryReady`; the game sends `Locale` and `State`. The page is shown always once the first state arrives |
| `/admin` | `AdminAsk("sanctuary")` -> `AdminSanctuaryView {enabled, aggressors}`; `AdminAsk("incidents", AdminIncidentFilter {name, before})` -> `AdminIncidentsView`; buttons run `/asanctuary <number> clear`, `/asanctuary on`, `/asanctuary off` |
| F10 | `MenuView.sanctuary` = `MenuSanctuaryView {enabled, inside, area, aggressor, secondsLeft, aggressorMinutes, bounty}` |

## 5. Implementation map

| Area | Path | What |
|---|---|---|
| Server | `src/CyrodiilRP.Server/Sanctuary/Guardian.cs` | `[Service]`, the per-tick loop, on/off, describe, aggressors, clear, `/ahp` hook |
| Server | `Sanctuary/Ward.cs`, `Blame.cs`, `Aggression.cs`, `Zone.cs` | The logic, free of the game, unit-tested |
| Server | `Sanctuary/ImperialCity.cs`, `CityArea.cs` | Generated city list: 11 districts, 313 interiors, the 5 Arena cells kept apart |
| Server | `Sanctuary/PlayerWatch.cs`, `PlayerWatchRegistry.cs`, `NpcWatch.cs`, `NpcWatchRegistry.cs` | Per-player and per-NPC state, streaks across reconnects |
| Server | `Sanctuary/IncidentTrack.cs`, `OpenIncident.cs`, `IncidentPerson.cs`, `Verdict.cs`, `Bystander.cs` | Incident grouping and blame inputs |
| Server | `Sanctuary/PlayerAreas.cs` | Area per player from server events |
| Server | `Sanctuary/SanctuaryNotices.cs`, `SanctuaryLog.cs`, `SanctuaryCommands.cs` | Messages and badge, DB log, commands and G option |
| Server | `Sanctuary/SanctuaryHudView.cs`, `HudState.cs`, `SanctuaryStatus.cs`, `SanctuaryAggressor.cs`, `SanctuaryJson.cs` | Views and JSON context |
| Server | `src/CyrodiilRP.Server/SanctuarySettings.cs`, `RpConfig.cs`, `config.json` | Config section |
| Server | `Admin/AdminSanctuaryView.cs`, `AdminAggressorView.cs`, `AdminIncidentFilter.cs`, `AdminIncidentView.cs`, `AdminIncidentsView.cs`, `AdminPanel.cs`, `AdminActions.cs`, `AdminCommands.cs` | `/admin` tab and the `/ahp` hook |
| Server | `PlayerMenu/MenuSanctuaryView.cs`, `PlayerMenus.cs`, `MenuTabs.Sanctuary` | F10 tab |
| Client | `src/CyrodiilRP.Client/Sanctuary/SanctuaryOverlay.cs` | Badge page, display only |
| Data | `src/CyrodiilRP.Data/Entities/SanctuaryIncident.cs`, `SanctuaryVerdict.cs`, `SanctuaryVictim.cs`, `Sanctuary/SanctuaryBook.cs` | Entity, write, paged read with ILIKE on victim or any aggressor |
| UI | `ui/sanctuary/` (`App.vue`, `clock.js`, `bridge.js`, `sanctuary.css`) | The badge and its countdown |
| UI | `ui/menu/SanctuaryTab.vue`, `ui/admin/SanctuaryTab.vue`, `ui/admin/sanctuary.css`, `ui/admin/actions.js` | F10 and `/admin` tabs |
| Art | `ui/rp/art/icons/sanctuary.webp` | Placeholder medallion (parchment shield with a red cross, drawn by script) |

**Database** (migration `20261105235740_Sanctuary`): table `sanctuary_incidents`: `id` identity, `at`, `area`
varchar(256), `area_name` varchar(128), `victim_kind` varchar(32) (`Player` | `Npc`), `victim_account` uuid null,
`victim_name` varchar(256), `damage` real, `died` bool, `verdict` varchar(32) (`None` | `Suspect` | `Several`),
`aggressors` text[], `aggressor_accounts` uuid[]. Checks `ck_sanctuary_incidents_damage` (`damage >= 0`), `_victim`
(a player victim has an account, an NPC none), `_aggressors` (both arrays the same length). Index on `at`. The migration
also adds `asanctuary` to the `admin` and `moderator` roles. No FK: victims and aggressors are kept by name and ReadyM
account.

**Config** (`sanctuary` section of `server/config.json`, defaults as 0.2.0): `enabled` true, `extraAreas` [],
`excludedAreas` [], `healAllDamage` true, `meleeRange` 400, `rangedRange` 4000 (game units, about cm),
`attackWindowSeconds` 1.5, `incidentSeconds` 3, `aggressorSeconds` 300, `bounty` 40. `staffAccounts` is gone. The section
is in the shipped `config.json` and in `DEVELOPMENT.md`; a deployed config without it uses the defaults, so the
Sanctuary starts on.

**Permissions:** `StaffPermissions.Sanctuary = "asanctuary"`, granted to moderator and admin; owners hold it anyway. It
gates `/asanctuary`, the `/admin` tab and the staff notices (plus `/duty` on).

**Locales:** `locales/{en,pl}/sanctuary.json` (generated `SanctuaryText`), `locales/{en,pl}/ui.sanctuary.json`, plus
`ui.admin.json` and `ui.menu.json`.

**CEF page and layer:** `sanctuary` on `WebPages.SanctuaryLayer = 2` (between party and frozen). Every layer above it
moved up by one: lore 5, dice 6, admin 11, welcome 13.

**New npm dependencies:** none.

## 6. Design decisions and why

- **Incidents in the DB, not `moddata/*.jsonl`.** The core has PostgreSQL and a panel: the staff page and filter the log in
  `/admin` like the dice log, and the rows are written by `SanctuaryBook` through the one writer, off the tick. Several
  aggressors per incident are arrays, kept in step by a check.
- **The permission replaces `staffAccounts`.** Staff are the core's roles, and notices follow `/duty`.
- **`/ahp` window of 1 s.** A health the staff write reaches the player's game and comes back within about a second; until
  then a drop is the staff's, not a hit.
- **Turning it off** ends every aggressor streak (online and away), closes and writes open incidents, hides the badges and
  resets the wards. Turning it on starts the wards fresh, without an enter message.
- **The guardian's clock runs while off** (one clock since the server started, for every streak and incident).
- **Gather, then decide.** Each tick collects every drop and marks every aggressor before any health goes back, so the
  order players are visited in changes nothing (from 0.2.0).
- **Areas from server events,** because a character's scope can be empty right after joining.
- **G option only where it matters:** inside the Sanctuary, or while a streak holds against the player.
- **No shown names needed:** no player-facing text names another player. Staff and the log use real names and accounts.
- **Zone list generated, the Arena never addable** (`Zone` removes the Arena cells after `extraAreas`).

## 7. Tested / not tested

Automated (counted from the commit):

- xUnit: 63 cases (51 facts, 2 theories with 12 rows) in `tests/CyrodiilRP.Tests/Sanctuary/` (`WardTests` 11,
  `BlameTests` 11, `ZoneTests` 7, `IncidentTrackTests` 6, `AggressionTests` 5, `PlayerWatchRegistryTests` 5), the 0.2.0
  tests adapted, plus `Database/SanctuaryBookTests.cs` (7, real PostgreSQL) and 1 in `Admin/AdminRulesTests.cs`.
- Vitest: 12 hand-written (`ui/tests/sanctuary.test.js` 6, `admin.test.js` 3, `menu.test.js` 3), plus the page contract
  tests generated from `ui/pages.js`.
- Playwright: 5 (`ui/e2e/sanctuary.spec.js` 3, `admin.spec.js` 1, `menu.spec.js` 1), at the four sizes; the full run green
  at 1920x1080 (147).
- Suite on the branch (`pack.ps1 -Test`): 359 Vitest, 562 C# plus 2 server smoke tests, migration applied locally.

In game: **not tested at all** in this port. From 0.2.0 only version 0.1.1 (players only, one client) was tested in game.

Not verified: blame with two clients; NPC and guard wards; the bounty reaching the guards; the `/ahp` settle window against
the real write round trip; the badge placement over the real HUD; `/asanctuary` and the `/admin` tab against a live
server; anything that needs 2 players.

## 8. Known gaps and next steps

- **SDK limits (from 0.2.0):** no real invulnerability (health is put back after the hit, one full blow still kills); no
  attack order for NPCs (the bounty is there for the guards); only health is protected (fatigue, paralysis, magicka,
  pickpocketing work as usual); blame is a guess from who was fighting nearby; NPCs without a ref id are not watched.
- **Icon placeholder:** `ui/rp/art/icons/sanctuary.webp` is drawn by script, for Pete to replace.
- **G wheel cap** (`WheelOptions.MaxOptions = 8`, see `dice.md`): staff already lose "Staff panel". Inside the
  Sanctuary the new option pushes further: a staff member inside also loses "Staff chat", and a non-staff player in a
  party inside the city has 9 options, so "Commands" (Order 70) is cut. Needs Przemysław's decision.
- **Open incidents at shutdown are not written.** 0.2.0 closed them in `Guardian.Stop()`; the port has no `Stop`, so up to
  `incidentSeconds` of hits still open when the server stops are lost.
- **Staff notice outside the city:** "the aggressor X died in Z" also fires for an aggressor who dies outside; `Zone.NameOf`
  then returns the raw area id.
- **Deployed configs** without a `sanctuary` section start with the Sanctuary on (default `enabled: true`).
- **Page bundle:** 1013 KB of the 1024 KB budget after all three features.
