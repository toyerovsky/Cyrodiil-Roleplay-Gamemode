# Roleplay mods

Three mods written for the ReadyM Roleplay server before this gamemode, brought over as they are: each is its own
OblivionMP mod (its own `manifest.json`, client and server halves, tests and packaging script), not yet part of
`CyrodiilRP`.

| Folder | Mod id | What |
|---|---|---|
| [`HungerAndThirst/`](HungerAndThirst/README.md) | `Roleplay.HungerAndThirst` | food and drink meters, a satchel (Alt+H), effects per tier through SDK statuses |
| [`Jobs/`](Jobs/README.md) | `Roleplay.Jobs` | woodcutting (vanilla trees), mining ore nodes and quarrying |
| [`StatusTest/`](StatusTest/README.md) | `Roleplay.StatusTest` | test bench for SDK statuses on the numpad, not for players |

## They need an unreleased SDK

The SDK features these mods use are in no `ReadyM.SDK.Oblivion.*` package yet. They are on oblivion-mod's
`ai-playground` branch (main plus two commits):

- **Statuses** (`IStatuses`, client and server): Fortify, Drain, Damage, Restore or any magic effect on a player, for a
  time or until removed. Used by HungerAndThirst and StatusTest. Design: [StatusTest/docs/SDK-CHANGES.md](StatusTest/docs/SDK-CHANGES.md).
- **Jobs' APIs**: `IFoliage`, `IProps` (client and server), `IAnimations`, `IWeaponHits`, `ILocalProps`,
  `IEquippedWeapon` (client). Design: [Jobs/docs/SDK-CHANGES.md](Jobs/docs/SDK-CHANGES.md).

So the mods build against the SDK projects of an oblivion-mod checkout, not the packages
([`Directory.Build.props`](Directory.Build.props)), and a server running them needs that build of the relay, the
game's OblivionMp mod (`main.dll`, `OblivionMpCSharpMod.dll`) and the SDK mod.

```powershell
git clone --branch ai-playground git@github.com:readycodeio/oblivion-mod.git
git -C oblivion-mod submodule update --init --recursive readym-gameserver
$env:OBLIVION_MOD_ROOT = "$PWD\oblivion-mod\"   # or leave oblivion-mod next to this repository
dotnet test mods/Jobs/Roleplay.Jobs.sln
```

`-p:OblivionModRoot=<path>` works too. The repository's `global.json` applies (.NET 10). Each mod's `MakeModFolder.ps1
Release` lays it out in its `Output/mods/<id>/`, ready to copy into a server's `mods/`.

## Not done

- Folding them into `CyrodiilRP`: saving per character in its database instead of JSON files, its pages and texts
  (`locales/`), its coding standard. Until then they keep their own style and are not in `CyrodiilRP.slnx`.
- Switching to the packages once a release carries these SDK features.
