# Hunger and Thirst

> **Needs the rp-framework build of OblivionMP.** The tiers below are SDK statuses (`IStatuses`), which no published
> package has yet, so the mod builds against an oblivion-mod checkout (see [../README.md](../README.md)). The relay,
> the game's OblivionMp mod and the SDK mod must be that build too.
>
> The meters replicate on a `Sustenance` mixin on `Player`. That needs the client to register mod mixins as networked
> components (`ReplicatedShapeRegistration` in `OblivionMpCSharpMod/DI.cs`), which is in oblivion-mod's main. Test: the
> client log has no `No reader job registered for component ID` lines, and the bars count down between meals.


Survival needs for the ReadyM Roleplay server on OblivionMP. Characters get hungry and thirsty in real time, eat and
drink from their satchel, and suffer when a meter runs dry. The server owns every number; the client draws two bars
and keeps them moving smoothly between updates.

Built on the [OblivionMP mod template](https://github.com/readycodeio/oblivionmp-mod-template), first against SDK
`1.0.0-rc.3` (the scope doc still says so). Scope, design decisions and what still needs an in-game test:
[docs/SCOPE.md](docs/SCOPE.md).

## In game

- Two bars at the bottom left: food (amber) and drink (blue). They turn red and pulse when nearly empty, and hide
  in the game's menus and dialogue (inventory, containers, map, pause, talking to someone): the mod watches for the
  game's mouse cursor, which is up exactly while one of its menus is.
- **Alt+H** opens the satchel: every food and drink you carry and what it gives. Up and Down to choose, Enter to eat
  or drink, Esc or Alt+H to close. While swimming, "Drink from the water" is at the top.
- Warnings in the top left as you get hungry, then starving (same for thirst).
- Each tier puts effects on the character, listed with the active effects under the tier's name:

  | Meter | Tier | Effects (defaults) |
  |---|---|---|
  | Food below 50% | Hungry | Drain Fatigue 20%, Willpower 5 |
  | Food below 25% | Starving | Drain Fatigue 40%, Strength 10, Endurance 10, Willpower 10 |
  | Food empty | Wasting Away | Drain Fatigue 60%, Strength 25, Endurance 25, Willpower 15, Health 25% |
  | Drink below 50% | Thirsty | Drain Fatigue 20%, Intelligence 5 |
  | Drink below 25% | Parched | Drain Fatigue 50%, Intelligence 15, Agility 10, Willpower 10 |
  | Drink empty | Dehydrated | Drain Fatigue 80%, Intelligence 25, Agility 20, Speed 30; Damage Health 1 per second |

  Hunger is tiredness first, then muscle wasting (less Strength also means less carry weight), then organs failing.
  Thirst goes to the head first, then dizziness, then a body that can barely walk but can still reach water.
  Percentages are of the character's own maximum. Fatigue never drains below 1, counting both meters and the
  attributes that add up to it, and health never goes below 10 by default (`healthFloor`; 0 lets them kill).
  Everything is in `config.json`.
- Food and drink used from the game's own inventory menu count too: the game reports each meal (the SDK's
  `PlayerConsumeEvent`) and the server feeds by the same food table, with no cooldown since the item is gone. A
  server that only trusts the satchel sets `countInventoryMeals` to false, see the scope doc.
- One player can feed another standing near (`feedRange`, 300 units) from their own pack. CyrodiilRP offers it on
  the H wheel (Feed, then the foods carried); any mod can through the APIs below.

## For other mods

Every meal is one networked event, `MealEvent` (Common): `Feeder`, `Eater` (the same player for one's own meal) and
`FormId` (0 for a sip of water). The server checks it (`MealRefusal`), takes the item from the feeder's pack and fills
the eater. Another mod can refuse meals in `Stage.Validate` or react to them in `Stage.Observe`, on the server or on
any client nearby (Area scope).

A mod using the two services below lists `Roleplay.HungerAndThirst` in its manifest's `dependencies`, references the
mod's projects to build, and leaves the mod's DLLs out of its own package.

**Server, `ISustenance`:** any player's meters, rates and tier (`TryGetMeters`), the food table (`Foods`), what a player
carries (`CarriedBy`), `Check(feeder, eater, formId)` for a wheel or a button to show why not, and `Feed(feeder, eater,
formId)`, which raises `MealEvent` as the feeder.

**Client, `ILocalSustenance`:** for a UI of your own. The local player's meters as the bars show them (counted down
between updates, a meal on its way included), rates, bands (`SustenanceBand`), `InWater`, the food table and what the
player carries; `Eat`, `DrinkWater` and `Feed(player, formId)`; `SatchelOpen` to open or close the satchel;
`HudEnabled = false` to hide the mod's bars; the `Meal` and `MealRefused` events. Other players' meters replicate on
every `Player` as `Satiety` and `Hydration`.

## Server owners

1. Build and package: `.\MakeModFolder.ps1 Release`
2. Copy `Output/mods/Roleplay.HungerAndThirst/` into the server's `mods/` folder and restart it.
3. Tune `mods/Roleplay.HungerAndThirst/server/config.json`. Every setting is in there with its default and a comment:
   decay times, activity multipliers, the tiers and their effects, respawn levels and the food table. When updating the mod, keep your
   own `config.json` instead of the shipped one.

Meters are saved per ReadyM account to `server/sustenance.json` every 5 minutes, when a player leaves and on
shutdown. If that file can't be read it is copied to `sustenance.json.broken` and everyone starts fresh.

## Layout

The code is in the repository's `src/` and `tests/`, beside the gamemode's, and builds by its rules
(`Directory.Build.props`: warnings as errors, central package versions, a file a type). This folder keeps the rest of
the mod.

| Path | What |
|---|---|
| `src/Roleplay.HungerAndThirst.Common/` | the `Sustenance` mixin (`Satiety`, `Hydration` on every player), `MealEvent`, the meter scale and the RPC contract |
| `src/Roleplay.HungerAndThirst.Server/` | the rules, the food table, saving, the service that runs it all, and `ISustenance` |
| `src/Roleplay.HungerAndThirst.Client/` | prediction, warnings, the CEF page service, and `ILocalSustenance` |
| `tests/Roleplay.HungerAndThirst.Tests/` | xUnit tests for the rules, food table, shipped config and save file |
| `Content/web/` | the pages: `hud.html` (the bars), `satchel.html`, and `index.html` to preview both in a browser |
| `Content/config.json` | the server config shipped with the mod |

## Develop

```powershell
dotnet test Roleplay.HungerAndThirst.sln
.\MakeModFolder.ps1 Debug     # with .pdb files
```

Open `Content/web/index.html?open` in a browser to work on the page without the game: a small mock plays the server,
H opens the satchel and the meters fall 60 times faster. Add `&low` for low meters.

The template's own notes (packaging script, renaming, unblocking downloaded scripts) still apply:
[oblivionmp-mod-template](https://github.com/readycodeio/oblivionmp-mod-template).
