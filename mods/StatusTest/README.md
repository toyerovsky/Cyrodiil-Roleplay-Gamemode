# Status test

A test bench for the SDK's statuses (`IStatuses`). Not for players. Numpad keys, with Num Lock on:

| Key | Does | Path |
|---|---|---|
| 1 | Fortify Health 50, until removed | server |
| 2 | Fortify Fatigue 100, 30 s | server |
| 3 | Drain Strength 20, 30 s | server |
| 4 | Damage Health 2 per second, 10 s (20 health in all) | server |
| 5 | Restore Magicka 5 per second, 20 s | server |
| 6 | Fortify Blade 25, until removed | server |
| 7 | "Starving": Drain Fatigue 50, until removed | server |
| 8 | "Light pack": Feather 100, 60 s | server |
| 9 | "Heavy pack": Burden 100, 30 s | server |
| 0 | clear every server status | server |
| - | remove the last server status still active | server |
| * | list the server's statuses with time left | server |
| . | "Local haste": Fortify Speed 50, 20 s, this client only | client |
| / | remove this client's own statuses | client |

Each press shows what happened in the top left. Server presses are also logged by the relay (`StatusTest: ...`).

## Build and deploy

The statuses themselves are in the SDK, on oblivion-mod's rp-framework branch ("Implement IStatuses", with a
usage doc in `docs/features/statuses/manual.md`). The mod builds against that checkout, since `IStatuses` is in no
published package yet (see [../README.md](../README.md)):

```powershell
dotnet build Roleplay.StatusTest.Server -c Release
dotnet build Roleplay.StatusTest.Client -c Release
```

Lay it out in the relay's `mods/Roleplay.StatusTest/` as `manifest.json` (from `Content/`), `client/` with
`Roleplay.StatusTest.Client.dll` and `Roleplay.StatusTest.Common.dll`, and `server/` with
`Roleplay.StatusTest.Server.dll` and `Roleplay.StatusTest.Common.dll`. The relay and the game must run the
statuses build of the OblivionMp mod and SDK too.
