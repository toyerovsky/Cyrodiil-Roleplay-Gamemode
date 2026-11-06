---
name: oblivionmp-platform
description: What Cyrodiil RP relies on in the OblivionMP platform and SDK 1.0, and its quirks found the hard way. Use before touching the mod's entry points, services, RPC, CEF pages, the character creator, packing or the server.
---

# OblivionMP platform

The SDK is the NuGet family `ReadyM.SDK.Oblivion.{Common,Client,Server}` (1.0.0, one version for the three; the server
is 1.0.1, which keeps SDK 1.0.0). The documentation is https://docs.ready.mp/oblivion-mp/docs/what-is-oblivion-mp.
Each fact below was checked in this repo.

## A mod

- `mods/<name>/manifest.json`, `client/` (handed to every player) and `server/` (stays on the server).
- The server loads every DLL of a mod's `server/` into the mod's own load context. An assembly the server or another mod
  already ships must not be in it: a second copy is refused, and a second `Microsoft.Extensions.Logging` would not be the
  host's. `pack.ps1` leaves them out.
- The host turns reflection-based `System.Text.Json` off for the whole process: JSON goes through a
  `JsonSerializerContext` (`PageJson`). A context built on options of its own (`LoreJson.Page`) does not take the
  naming policy of `[JsonSourceGenerationOptions]`, so those options name it too.
- `[ModEntry]` on a `partial` class: `Init()` runs once. A throw there logs `[ERR] Failed to start the mod in <assembly>`
  and the server goes on without the mod. `Mod.Init` refuses a database at another migration this way.
- `[ModConfig]` on a `partial` class with a public constructor: `server/config.json` beside the DLL, comments allowed.

## Services

- `[Service]` on a `sealed partial` class, built by the container with its constructor's services. `Start()`,
  `Update()` (every tick, `[UpdateOrder(n)]` on the method, lower first) and `Stop()` are found by name. `Time` is the
  tick's `UpdateTime`. A service is registered by its own type only (`ServiceRegistry`), so a constructor asks for the
  class, never an interface it implements (`SystemClock`, not `IClock`).
- An entity kept from one tick to the next (`TraderNpcs` keeps the traders it found) may be gone by then:
  `EntityHandle.Of(npc).IsAlive()` says so, a read of a gone one throws. `WorldRefs.TryFindNpc` walks every NPC.
- The host never disposes the services, so `Stop()` never runs on a shutdown: nothing may wait for it. What must not be
  lost is written as it happens, and `AppDomain.ProcessExit` is a best effort (`ServerLifecycle`).
- One update thread for every mod, 10 ms a tick. RPC handlers and server events run on it. Entities
  (`IEntities.TryLookup(playerId, out MainCharacter main)`) only there.
- A character's `Position` is in centimetres and its `Rotation` is the yaw in degrees, from +X towards +Y. The server holds
  both for every player, so it can tell who stands in front of whom (`FrontCone`).
- Right after a player enters an area the server already has it there, but its `Position` is still the old one until
  the game first reports: the save's, or 0,0,0 (under the ground of `L_Tamriel`) without a save. Never take it as where
  the character stands before it holds for a second check (`HeldSpot`).
- The server's inventories (`Inventories`: `TryMove`, `TryTake`, `CountCarried`, `Add`) hold form ids, counts, health and
  charge, and the server applies every move of an item. SDK 1.0 has no name of an item for its form id, on either side:
  the staff's tools take the names from the game's files (`Admin/items.json`, the research's
  `tools/esm/export_admin_data.py`). An entry's form id is `entry.FormId.FormId.Value`, and `TryTake` takes one kind of
  copy at a time (same health and charge), worn ones last.
- `Inventories.Add` adds counts as they come: a stack past `int.MaxValue` wraps (`Wallet.TryGive` refuses it).

## Players

- `PlayerId` is per session (`RawValue` is the number commands take), and a reconnecting player keeps it. The ReadyM
  account id (a `Guid`) is the one that lasts: accounts are keyed by it.
- The player's game save (items, looks, vitals, attributes, skills, cell, level) is one per ReadyM account, its slot the
  account id, loaded before `OnPlayerConnected`: a mod cannot pick another. So an account has one character
  (`CharacterSlots`) until the platform keeps a save per character.
- A player's nickname is set with `player.SetNickname(...)` inside an `entities.Query<Player>()` loop.
- The character's name in the game's own menus is `MainCharacter.Name` (`CharacterIdentity`, SDK 1.0.0, with race, sex,
  class and birthsign, in the player save). A server write sends the whole identity, which the owner's game applies,
  and the game reads it all back after the creator's race, class and birthsign menus. So write the name only once a mismatch
  holds for a second check: a write landing before the new race reached the server would take that race back
  (`Nicknames`).
- The server moves a character (`main.Position`) only within the area it stands in (`IEntities.TryGetScope`). An area is
  an interior (`L_PersistentDungeon-<cell>`) or a whole world space (`L_Tamriel`, and each city is its own,
  `OblivionAreaIds`). There is no teleport to another area, no kill, no damage hook (so no party friendly fire either:
  the victim's own game decides what a blow takes) and no camera in SDK 1.0.
- The game's units become the server's centimetres at 100/70 cm a unit, with Y the other way round
  (`Admin/GameSpace.cs`).
- `ResurrectLocalPlayer()` works on the local player only, so the server asks the player's own game (`Revive`). It is
  the platform's rebirth: a heal and a quick load.
- An area's `Time` (`SetGameHour`) and `Weather` (`SetWeatherFormId`, `SetTransitionRatio`) are the server's to write,
  but the area's master client also sends its own (`RequestTimeChange`), so a value set may not hold.
- A game does not take a clock going back, and the master client then sends its own time over the one set. An hour
  earlier than the clock needs `SetGameDaysPassed` a day on, as waiting past midnight does.
- Nothing in the SDK stops a player walking: a freeze holds the input on a page (`FrozenOverlay`) and the server puts
  the character back.

## RPC

- `[RpcContracts]` in `CyrodiilRP.Common` declares both directions. `[RpcHandlersFor(typeof(RpContracts))]` on a
  `partial` class gets `Send<Name>` methods and `partial void On<Name>` handlers on each side.
- A message's code is its name's place among the contract's names in order (`ServerRpcManifest`, generated), one code
  for a name whatever its directions. So a new or renamed message moves the codes after it: the client and the server
  come from one build, as the server hands every player its `client/`. Each direction has a name of its own, for what
  it carries (`SubmitName` goes up, `NameAnswer` comes back).

## CEF pages

- `CefHost.RegisterFolder(host, dir)` only while the mods start (`Start()`): the CEF configuration is frozen after.
- `readym://<host>/` serves `index.html` only at the host's root, so a page is addressed in full:
  `readym://rpui/chat/index.html` (`WebPages.BuildUrl`).
- On a page: `readym.on("Name", handler)` for a `[SendToCef] partial void SendName(BrowserHandle page, ...)`, and
  `readym.sendName(...)` for a `[ReceiveFromCef] void OnName(BrowserHandle page, ...)`. A name has one handler on the
  page, the last one given. Every page shares one registry, so a received name is unique across all pages. The
  `[Service]` that declares them is registered in the container as `ICefRpcHandler` too (`Mod.Init` of the client).
  A message sent to a page that has not loaded, or is reloading, is dropped: `OverlayPage`
  knows when its page is ready, and `PageWindow` keeps the server's state until then.
- Ctrl+Shift+F5 reloads every page (`CefHost.ReloadAll`, the SDK's own mod). A page says it is there when it loads,
  and its owner sends it its state again (`OnCreatorReady`, `OnRequestHistory`).
- `CefHost.SetVisible(viewport, true)` arms the page on the game's overlay widget, which exists only in the world and
  is rebuilt after every load. Called before it is there (at the main menu, during a load) the arm fails without a word
  and nothing tries again, also in 1.0.0, so the page never comes up: the log never says
  "Viewport N page shown". A page that stays up (the chat, the party) goes through `WebPages.ShowAlways`, which shows it
  again a second after each new pawn of the character (`PawnWatch`, `WebPages.OverlaySettleSeconds`). Pages shown through
  `PageInput.Take` are shown in the world.
- Markers (`IMarkers`, client) are a line of text over a character, seen only by the player whose game makes them. Since
  1.0.0 they go over an NPC too (`CreateMarker(Npc, text, color, style)`) with a `MarkerStyle` (font size, outline,
  background, height, the bone to follow: `head`). A marker needs the pawn: until there is one it is `MarkerHandle.None`,
  and a pawn that goes with its cell takes the marker along, so `TraderMarkers` looks the traders over every second and
  makes them again for a new pawn. The client finds the mode's traders by `SpawnerRefId == TraderOrigin.SpawnerRefId`
  (`SpawnOrigin` is replicated), and the server sends the label in the player's language (`TraderLabel`).
- The platform's `OblivionMp.MarkerMod` writes a player's `Nickname` into a marker once, as it makes it: a nickname the
  server writes later shows only after the area loads again. So the mode draws the names
  itself (`Nameplates`) and `pack.ps1` parks MarkerMod. A marker grows upward from `OffsetZ`, so a second one over the
  same head at the same `OffsetZ` and font size ends in an empty line (a no-break space) to stand over the first.
  `MarkerStyle.MaxWidth` wraps a long text.
- A full-screen viewport (`ViewportPlacement.FullScreen`) lets a page scale with the window. `pointer-events: none` on a
  page leaves the mouse to the game.
- Keyboard: `SetFocused(viewport)` and `SetPageFocused(browser)`. A new document starts unfocused, so a page that loads
  while it should have the keyboard is focused again. `SetFocused(viewport, false)` hands the keyboard to the game
  viewport, whoever had it. A form takes the mouse with `SetUiInputMode(true)` and `SetCursorVisible(true)`, and gives
  both back.
- Both need the player controller, which exists only in the world: at the main menu the calls do nothing but log
  `[CefInput] No player controller`. The game sets input back to itself on its own, with no event and nothing to read
  it by, after a save loads and as its pause menu closes. A page holding the input takes it again every second
  (`PageInput`), and a form the server asks for waits for the character's pawn (`PawnWatch`, from `MappedActor.Pawn`).
- The mouse goes by `CefHost.SetInputMode(viewport, mode)`, a mode per page since 1.0.0 (a new viewport starts in
  `Bubbling`). `WebPages.Open` makes every page `DisplayOnly`, a picture every click passes through, and `PageInput`
  gives the page holding the input `Exclusive` (every click over its screen, so a click beside a window keeps the
  keyboard in it) and takes it back on release. The open chat holds in `Bubbling`: a click beside it goes to the game,
  which takes the focus, and the page's blur closes the chat. So the chat's
  log and the party's list never take a click from the game's own menus (its character creator). Before 1.0.0 one mode
  held for every page and `Exclusive`, the default then, took every click while the game showed a cursor. Pages still
  set `pointer-events: none` on `html` and `body` and draw nothing when closed (`v-if`, `visibility: hidden`), which
  `Bubbling` would need.
- Key binds are `KeySource.Polled` (K chat, G own wheel, H the one in front, Y and N an invitation, F10 the player's
  menu, Home the dice board, End and Shift+End the lore card). `UnhandledByUi` never fires in play: the game takes every key as handled. A polled
  press reaches the game too, so a bind takes a key the game leaves alone. The Remaster's own keys: F1 to F8 open the
  tabs of its menu (character, inventory, magic, map, quests, help, system), F5 and F9 save and load, and I, M, Tab, Q,
  E, R, T, F, C, 1-8. G, H and K are free (https://www.magicgameworld.com/?p=109775, which leaves out the map's F3).
  F9, the game's quick load, does nothing in multiplayer ("QuickLoadInput_Pressed called but ignored" in the log). F10 is free unless UE4SS's ConsoleEnablerMod is on (off in
  the platform's `mods.txt`), which adds F10 as a console key. A polled press also fires while a page has the keyboard,
  so a bind checks that none does (`PageInput.IsHeld`, `ChatOverlay.IsOpen`), and the chat page leaves out the K that
  opened it. A bind hears the press only, never the release, so a wheel opens on a tap and stays until picked. The
  platform's chat mod (`OblivionMP.Chat`) answers K too, so `pack.ps1` moves it to `mods-disabled/`.
- A page with the keyboard gets the press first and the polled bind after it. So a key that both opens a window (a
  bind) and closes it (on the page) closes it on the page and opens it again from the bind: the bind alone toggles it
  (F10 and the player's menu).
- The game hands a page the Windows key code alone, with no scan code, so `KeyboardEvent.code` is empty in game.
  `key` and `keyCode` arrive. A page reading a key by its place takes `readCode` (`ui/shared/press.js`), which falls
  back on `keyCode`.

## People the mode places

- `IEntities.Create<Npc>(cell, cell.MasterClient)` with `RefId` from `ServerRefIds.Next()`, `BaseId`, `Position`,
  `Rotation`, `Level` and a `SpawnerRefId` of the mode's own marks a person every client in the cell shows and the
  cell's master moves. The cell's save keeps it.
  `entities.TryLookup(FullCellId, out Cell)` finds a loaded cell, `ParentCell.ToFullCellId(area)` names a character's.
- Its equipment starts empty and the game gives it nothing: the clothes go in as entries marked worn
  (`InventoryEntry(form, 1, worn: 1)`), and the master's game equips what the server's first list marks so.
  `AiIntents.Enqueue` gives it orders
  (`ExploreWander` around a spot).
- Its base decides what the game adds on its own: services (the game's barter, rooms, training), factions (greetings,
  crime allies), AI packages (where it walks off to) and lines of its own. The research's `npcs.tsv`
  (`tools/esm/export_npcs.py`) lists them all. The game has hardly any generic people: almost every base is named, hostile
  or a quest's. Its name comes from the base, a name of our own needs an `.esp` record of the platform.
- The server may write its `Hp`: putting it back to `HpBase` often keeps it standing.
- The platform has no event for using an NPC (no activation on the client): the mode's H reaches it through
  `InteractionMenu.AddTarget`, from the positions the server holds.
- There are no parties in the platform: `Parties` keeps the mode's own.

## The character creator

- `players.LocalMainCharacter.OpenCharacterCreator()` opens the game's own creator, `IUserInterface.IsCharacterCreatorOpen()`
  says whether it is up. There is no event when it closes: `CharacterCreator` watches it every tick.
- Asked right after joining, the creator can be asked for before the character is in the world: it is asked again every
  3 s until it shows. Asked as a save finishes loading, it turns the camera to the face and hides the HUD but shows no
  window, so it waits until the character's pawn has been in the world for 3 s (`CharacterCreator`, `PawnWatch`).

## The server

- `server.exe` in `oblivion-server-*`, its port from `RELAY_Server__Port`. Logs are Serilog lines (`[INF]`, `[WRN]`,
  `[ERR]`) on the console and in `logs/`.
- `config.json` of the server is the host's own (port, server list, saves), not the mod's.

See also the `code-rules` and `performance` skills.
