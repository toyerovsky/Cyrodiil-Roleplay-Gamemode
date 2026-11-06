# Hunger and Thirst: scope

Version 0.1 of the survival needs for the ReadyM Roleplay server on OblivionMP (SDK `1.0.0-rc.3`).

## Goal

Every character slowly gets hungry and thirsty. Food and drink from the base game fill them up again. Running out
hurts, but the defaults never kill, so a player who logs in after a break is not punished with a death. The server
owns every number. The client only draws them, and predicts between updates so the bars move smoothly.

## What we learned (Phase 1)

**PeteRoman's RpRoll** ([obmp-rproll](https://github.com/pete-roman/obmp-rproll)) is the reference for how a 1.0 mod is
put together, and we follow it:

- The server decides, the client asks. A client RPC names *what* it wants, never a number.
- Services stay small and single-purpose, and handlers never throw into the SDK (`Guard` and try/catch at the edges).
- A CEF page sits in a hidden full-screen viewport created at start, and gets the whole state as one JSON message,
  resent a few times after each change, so a page that is still loading catches up. 1.0 pages get no mouse, so menus
  are keyboard only, and the game is put in UI-only input while one is open.
- Anything saved is keyed by the ReadyM account id (`IPlayers.GetReadyMId`), because `PlayerId` changes on reconnect.
- Pure logic lives apart from SDK calls, so unit tests can cover it.

**Detecting edible items.** The SDK ships the game's item catalogue as typed form ids:
`ReadyM.Relay.Common.Oblivion.Items.Items.Ingredients` (Apple, Breadloaf, CheeseWheel, Venison, ...) and
`Items.Potions` (DrinkAle, DrinkMead, DrinkWine0Cheap, PotionCyrodiilicBrandy, ...). There is no "food" flag on
them, so the mod keeps a curated nutrition table that points at those fields. Server owners can change values, add
items by editor name or hex form id, and remove defaults in `config.json`.

**Detecting that someone ate.** Two ways in, both ending in `SustenanceService.Feed`:

| Way | How | Trust |
|---|---|---|
| **The satchel** | the client asks, the server checks the item is a known food and carried, takes one out of the inventory itself, then feeds; a cooldown between meals | fully server-authoritative |
| **The game's inventory menu** | the game's own `DrinkPotion` and `EatIngredient` raise the SDK's `PlayerConsumeEvent` (the character, the item's form id, `Eat` or `Drink`), a networked event the eater's client sends; `SustenanceService.OnPlayerConsume` feeds by the same food table, for the sender's own character only, with no cooldown since the game used the item already | the client reports it, so a modified client could report meals it never had; `countInventoryMeals: false` turns this way off |

The two never count one meal twice: a satchel meal is the server taking the item, which does not go through the
game's `DrinkPotion` or `EatIngredient`. Statuses applied as potions (`directApply`) raise no event either.

Rejected along the way: diffing the player's inventory on the server and taking a missing apple as eaten (cannot
tell eating from dropping, selling or stashing; stash and take back is infinite food).

**UI: CEF over OblivionUI.** [OblivionUI](https://github.com/QueenlyDObnkey7/OblivionUI) (the README link has a stray
`----` suffix) looks great, and its `HudPanels` progress bars are a natural fit. But 0.9.4 declares SDK `0.2.0`,
drives Unreal widgets through native function signatures that disable themselves on a mismatch, and would add a
prebuilt third-party DLL to our build. CEF is first-party, ships in the SDK we build against, and RpRoll proves it in
1.0. The page is plain HTML with no build step. If OblivionUI confirms 1.0 support, its HUD panels can replace the
CEF bars without touching the server.

## In scope for 0.1

**Server (authoritative)**

- Two meters per character, `Satiety` and `Hydration`, from 0 (starving, parched) to 1000 (full), kept on the
  server and sent to their owner with the `Meters` RPC whenever one has moved 5 points, and at once after a meal, a
  respawn or a hello. Not a replicated mixin: see "Found in game" below.
- Decay in real time. Defaults: full to empty in 3 hours (food) and 2 hours (drink). Faster while running, swimming
  or in combat (configurable multipliers). Paused while dead.
- Eating and drinking through `Consume(formId)`: known item, actually carried, not on cooldown. One item is taken
  from the inventory on the server, then the meters go up.
- Eating and drinking from the game's inventory menu: `PlayerConsumeEvent` from the eater's own client, a known item,
  no cooldown (`countInventoryMeals`, on by default).
- Drinking from water: allowed while the server sees the character swimming.
- Effects: three tiers per meter (below 50%, below 25%, empty), each a set of SDK statuses (`IStatuses`): Drain
  Fatigue as a share of the character's maximum, attribute drains modelled on the real thing (hunger: Willpower,
  then Strength and Endurance; thirst: Intelligence, then Agility and Willpower, then Speed), Drain Health and Damage
  Health at empty. Configurable per tier, any Fortify, Drain, Damage or Restore or any effect code. Fatigue never
  drains below 1, attribute drains included; health never goes below `healthFloor` (10) by default.
  The server works them out again only when a meter changes tier or health reaches the floor, and sends only the
  difference. Death takes them off.
- After death and respawn the meters restart at a configurable level (default half).
- Saved per ReadyM account to a JSON file in the server mod folder: on disconnect, every few minutes and on
  shutdown. New characters start full.
- Everything above is set in `server/config.json`.

**Client (presentation and prediction)**

- Two small bars at the bottom left, shown as a CEF overlay that never takes input.
- Prediction: the server sends the current decay rate whenever it changes. Between server updates, the client counts
  down locally and snaps back to the server value when one arrives. Eating shows the expected result at once and is
  corrected or rolled back by the server's answer. The server's meal cooldown comes with the food table, so a meal
  too soon is refused on the client and the bar never jumps for a meal that would be rolled back.
- The bars hide in the game's menus, which are told apart by the game's mouse cursor: it is up exactly while one
  of them is (inventory, containers, barter, map, pause, wait, and the dialogue menu). See "Found in game".
- Warnings in the game's own HUD message when a meter crosses "hungry", "starving", "thirsty" and "parched".
- The satchel: Alt+H opens a keyboard menu that lists what you carry that can be eaten or drunk, with what each one
  gives. Up and Down to pick, Enter to use, Esc or Alt+H to close. "Drink from the water" shows up while swimming.

## Out of scope for 0.1

- Cooking, recipes, food spoiling, wells and water skins.
- Buffs for being well fed, and vanilla food effects (the game still applies those only when eaten the vanilla way).
- Admin commands (`/feed`, `/setthirst`). Easy to add on top of `SustenanceService` with the chat mod.
- RP emotes in chat ("Pete eats an apple"). Would add a dependency on `OblivionMP.Chat`.
- Other players seeing your meters. Only the owner is sent them.

## Components

| Where | Piece | Job |
|---|---|---|
| Common | `SustenanceScale` | the 0..1000 scale, the warning thresholds and the sync step |
| Common | `SustenanceRpc` | `Hello`, `Consume`, `DrinkWater`, `Consumed`, `ConsumeRefused`, `Meters`, `DecayRates`, `Foods` |
| Server | `FoodCatalog` | default nutrition per base-game item, with display names |
| Server | `SustenanceConfig` | `config.json` |
| Server | `FoodTable` | defaults plus config overrides, resolved to form ids |
| Server | `SustenanceRules` | pure math: decay, multipliers (unit tested) |
| Server | `SustenanceEffects` | the tiers: reading them from the config, which one a meter is in, the statuses they want (unit tested) |
| Server | `SustenanceStore` | load and save per account |
| Server | `SustenanceService` | per-player meters, decay tick, tier statuses (`IStatuses`), feeding, connect and disconnect (`IServerEvents`) |
| Server | `SustenanceRpcHandlers` | validates requests, answers the sender; `SustenanceRpcWiring` sends meters and rates |
| Client | `SustenanceClientRpc` | server messages as events |
| Client | `SustenanceState` | local countdown, optimistic meals, what the satchel holds, hello to the server |
| Client | `SustenancePage` | the two CEF viewports (bars, satchel), Alt+H, and when the bars step aside |
| Client | `SustenanceWarnings` | threshold messages in the game HUD |

## Checked against the SDK source

- Taking the item: the satchel uses `Inventories.TryTake(character.As<Equipment>(), stack)`, the same call the SDK's
  own `ItemTransfers` uses for drops, and selling adds gold to a player's inventory the same way. Server writes to a
  player's inventory are how the SDK itself works.
- Health and fatigue: the SDK test mod (`ImmortalPlayers`) writes `main.Hp` on the server for players.
- `IServerEvents` is registered for every server mod (`ModHost`). Its `DisconnectReason` is `LiteNetLib`'s, which the
  SDK package brings in.
- The relay server is published AOT and switches reflection-based `System.Text.Json` off for the whole process, so
  the save file names `DefaultJsonTypeInfoResolver` explicitly, like the SDK's config reader.

## Found in game

- **Replicated mod mixins don't reach Oblivion clients (SDK bug).** The first version kept the meters on a
  `ServerAuthoritative` mixin on `Player`. The server sent it, and the client logged `No reader job registered for
  component ID: NetworkedComponentId(55)` thousands of times and never saw a value. `ReplicatedShapeRegistration`
  (`ReadyM.SDK.Client`), which registers mod mixins as networked components in the server's order, was moved out of
  the Wukong API into the shared SDK but is not registered in the Oblivion client's `DI.cs`. Without it no mod mixin
  replicates on Oblivion, the template's `Wallet` included. It also does harm: `ApplySnapshot` stops reading at the
  unknown component, so whatever follows it in a snapshot is dropped. The mod now sends the meters by RPC. **SDK ask:**
  register `ReplicatedShapeRegistration` after the game's own registrations in `OblivionMpCSharpMod/DI.cs`.
- **No menu state for mods.** `IInput.CanApplyInput()` is UE4SS's `can_process_events()`, which is about window focus,
  not game menus, and the menu state only exists in the native `MenuFreezeBypassHook`. Guessing from the menu keys
  failed in game (containers opened with E were missed, closing with R or pressing Esc quickly left it backwards). What
  works: the game shows its mouse cursor exactly while a menu is open, and the CEF layer hands every page the cursor
  each tick through `window.__readym_cursor` (its hit-test hook), and `(-1, -1)` once when it goes. `hud.js` wraps that
  hook and reports the changes. It is an internal hook, not an API. **SDK ask:** a "game menu open" flag or event on
  the client (for example on `IUserInterface`), or at least a getter for the game's cursor (`bShowMouseCursor`).
- **A visible overlay takes the mouse from the game's menus.** CEF's input mode defaults to `Exclusive`
  (`PageHitTestPolicy.h`): while the game shows a cursor, every visible overlay takes every click on its whole
  rectangle. One full-screen HUD page made the inventory deaf to the mouse. `CefHost.SetInputMode` is global, so a mod
  can't switch it for itself alone. The bars now have a viewport only their size, collapsed in menus, and the satchel
  one is collapsed unless open. Chat and RpRoll only show their viewports while in use, so they never noticed.
- **`PlayerActivity.InDialogue` is also true while NPCs nearby talk to each other.** It reads the player's
  `ClosesConversation` pointer (`OblivionMp/src/Control/ActivityControl.cpp`), which is set for NPC-to-NPC
  conversations in earshot too: two NPCs gossiping in the Aleswell inn hid the bars. The mod no longer uses it (the
  dialogue menu's cursor covers real conversations). Worth reporting: `OblivionMpRpc` refuses group time skips with
  `PlayerInDialogue` from the same flag, so NPC gossip near any player may block a group wait or sleep.
- **`SetUiInputMode(false)` is not "back to normal".** It is Unreal's `SetInputModeGameOnly`; after it the game's menus
  stop taking the mouse. Chat never calls it. The satchel only moves keyboard focus. (RpRoll's board calls it on close.)
- **Shown too early.** Showing a CEF viewport arms it against the overlay widget, which only exists in gameplay. Shown
  at `Start`, the arm failed without a word; the HUD is now shown once the character is in the world, and again
  after every load.

## To check in game

1. Eat from the satchel: the item leaves the game inventory and does not come back on the next inventory report.
   Eat an apple and drink an ale from the game's inventory menu: the bars go up once each, the relay logs
   "had ... from the inventory", and a healing potion moves nothing.
2. Each tier's effects show in the active effects under its name, come off when eating moves the meter up a tier,
   and come off at death. Attribute drains show in the stats menu and come back on eating. Starving and Dehydrated
   together leave 1 fatigue (does a power attack knock the character down?). Drain Speed 30 on a low-Speed character
   still lets them move. Damage Health stops near 10 health.
3. `CharacterStateFlags.IsSwimming` and `PlayerActivity.InCombat` reach the server for players (faster decay,
   drinking from water).
4. The CEF overlay stays click-through and takes no keys while the satchel is closed.
5. Alt+H does not clash with another bind on our server.
