# Roleplay mods

Three mods written for the ReadyM Roleplay server before this gamemode, brought over as they are: each is its own
OblivionMP mod (its own `manifest.json`, client and server halves, tests and packaging script), not yet part of
`CyrodiilRP`. The code of Jobs and of HungerAndThirst has moved to `src/Roleplay.*` and `tests/Roleplay.*.Tests`, in
`CyrodiilRP.slnx` and under the repository's build rules; their folders here keep the content, packaging, docs and
tools.

| Folder | Mod id | What |
|---|---|---|
| [`HungerAndThirst/`](HungerAndThirst/README.md) | `Roleplay.HungerAndThirst` | food and drink meters, a satchel (Alt+H), effects per tier through SDK statuses |
| [`Jobs/`](Jobs/README.md) | `Roleplay.Jobs` | woodcutting (vanilla trees), mining ore nodes and quarrying |
| [`StatusTest/`](StatusTest/README.md) | `Roleplay.StatusTest` | test bench for SDK statuses on the numpad, not for players |
| [`Emotes/`](Emotes/README.md) | `Roleplay.Emotes` | emotes on the G wheel, the game's idles and any a mod registers (`IEmotes`), code in `src/Roleplay.Emotes.*` |

## They need an unreleased SDK

The SDK features these mods use are in no released `ReadyM.SDK.Oblivion.*` package. They are on oblivion-mod's
`rp-framework` branch, main plus the commits below, and each server package from that branch's CI carries its own SDK
packages in `sdk/`.

- **Statuses** (`IStatuses`, client and server): Fortify, Drain, Damage, Restore or any magic effect on a player, for a
  time or until removed. Used by HungerAndThirst and StatusTest. Design: [StatusTest/docs/SDK-CHANGES.md](StatusTest/docs/SDK-CHANGES.md).
- **Jobs' APIs**: `IFoliage`, `IProps` (client and server), `IAnimations`, `IWeaponHits`, `ILocalProps` (with
  `FindRest` for carrying), `IEquippedWeapon` (client). Design: [Jobs/docs/SDK-CHANGES.md](Jobs/docs/SDK-CHANGES.md). Emotes plays through
  `IAnimations` too (`Play`, and `PlayOn` for another player's character).
- **Custom items** (`ICustomItems`, server): an item defined at run time as a copy of a game-file item with its name,
  value, weight, damage, mesh or scale replaced. Jobs makes its firewood and lumber axe so. Design: oblivion-mod's
  `docs/features/custom-items/manual.md`.

So Jobs, HungerAndThirst and Emotes build against the packages of the server they are for, as the gamemode does
([DEVELOPMENT.md](../DEVELOPMENT.md)): `pack.ps1` takes them from the server and builds, packs and deploys the three. A
server running them needs the `rp-framework` build of the game's OblivionMp mod (`main.dll`, `OblivionMpCSharpMod.dll`).

StatusTest is outside `CyrodiilRP.slnx` and `pack.ps1`, and still builds against the SDK projects of an oblivion-mod
checkout on `rp-framework` ([`Directory.Build.props`](Directory.Build.props)): the `oblivion-mod` folder next to this
repository, `OBLIVION_MOD_ROOT` or `-p:OblivionModRoot=<path>`. Its `readym-gameserver` submodule must be checked out,
recursively. The repository's `global.json` applies (.NET 10).

## Not done

- Folding them into `CyrodiilRP`: saving per character in its database instead of JSON files, its pages and texts
  (`locales/`), its coding standard. Until then StatusTest keeps its own style and is not in `CyrodiilRP.slnx`.
