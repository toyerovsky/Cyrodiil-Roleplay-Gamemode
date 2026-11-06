# Cyrodiil RP: development

A roleplay gamemode for OblivionMP: a mod with a server half (`src/CyrodiilRP.Server`) and a client half
(`src/CyrodiilRP.Client`) on the SDK, PostgreSQL through EF Core (`src/CyrodiilRP.Data`) with its own
migrator (`src/CyrodiilRP.Migrator`), Vue pages (`ui/`) and texts in `locales/`.

The SDK is built from an oblivion-mod checkout on its `rp-framework` branch, not the 1.0 packages: the server half
feeds players through the Hunger and Thirst mod (`src/Roleplay.HungerAndThirst.*`, `ISustenance`), which needs that
branch's statuses. The checkout is the `oblivion-mod` folder next to this repository, or
`-p:OblivionModRoot=<path>`
(`Directory.Build.props`), at the commit pinned in [mods/README.md](mods/README.md). The server needs `Roleplay.HungerAndThirst` installed (a manifest dependency) and the
`rp-framework` build of the relay, the game's OblivionMp mod and the SDK mod.

`src/CyrodiilRP.Common` goes to every player with the client, so it holds only what both halves read: the RPC contract
(`RpContracts`), the chat's line and limits (`ChatLine`, `ChatLineKind`, `ChatLimits`), the mark of the mode's
traders and bankers (`TraderOrigin`) and the staff's mark in a nickname (`Nameplate`). What a page shows (the `*View` records) and its JSON context (`*Json`) are the server's, in the
folder of the service that builds them: the client hands the JSON to the page as it comes and never reads it.

## Setup

Requirements: .NET SDK 10 (`global.json`), Node.js 22+, Docker, and the OblivionMP server unpacked in this folder as
`oblivion-server-*/` (the newest is used, or the one `OBLIVIONMP_SERVER` names).

```powershell
docker compose -f infra/docker-compose.yml up -d --wait   # PostgreSQL on localhost:5433
dotnet tool restore
dotnet run --project src/CyrodiilRP.Migrator              # or: CyrodiilRP.Migrator "<connection string>"
cd ui; npm ci; npx playwright install chromium
```

## Build and deploy

```powershell
./pack.ps1                 # build, database up, migrations, the mod into the server's mods/ (its config.json kept)
./pack.ps1 -Test           # the tests first, and the packed mod on the real server before it is installed
./pack.ps1 -SkipDatabase   # leave the database alone
./pack.ps1 -PackOnly       # only build the mod into out/mods/CyrodiilRP
```

`pack.ps1` needs PowerShell 7, on Linux `pwsh`, where the server's file is `server` and not `server.exe`. It refuses
while that server runs and keeps the deployed `server/config.json`: the new mod is put together in `<server>/mods-staging`, the deployed config copied into it as it is (an empty one too), and the
folders are swapped. Until the swap the deployed mod stays as it was. A deploy that stops half way leaves the mod
deployed before, config and all, in `mods-staging/CyrodiilRP.previous`, and the next one refuses until it is gone.
The server refuses a config without a connection string and a database at another migration than its own build
(`Run CyrodiilRP.Migrator` in its log). After a model change:
`dotnet ef migrations add <Name> --project src/CyrodiilRP.Data --output-dir Migrations`.

`mods/CyrodiilRP/server/config.json`:

| Key | What |
|---|---|
| `database.connectionString` | the PostgreSQL the migrator prepared |
| `owners` | ReadyM account ids (the admin panel's Players view) holding every permission |
| `startingGold` | gold a new character is owed once, in the same write that makes the character, delivered after the starting kit |
| `chat` | ranges of say, shout, whisper and emotes, in game units (about centimetres) |
| `economy` | `maxPayment`: the most gold one trade or `/agold` moves |
| `factions` | `foundingFee`: the gold a founder pays from what they carry (500 by default, 0 founds for free) |
| `lore` | the lore card: `catalogMode` (`Canon` by default) and `allowImplausibleSubtypes` (`false`: no kinds that do not fit the race's body, such as the Senche) |