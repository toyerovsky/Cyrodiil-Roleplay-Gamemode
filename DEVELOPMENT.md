# Cyrodiil RP: development

A roleplay gamemode for OblivionMP: a mod with a server half (`src/CyrodiilRP.Server`) and a client half
(`src/CyrodiilRP.Client`) on the SDK, PostgreSQL through EF Core (`src/CyrodiilRP.Data`) with its own
migrator (`src/CyrodiilRP.Migrator`), Vue pages (`ui/`) and texts in `locales/`.

The SDK comes with the server, not from nuget.org: the server half feeds players through the Hunger and Thirst mod
(`src/Roleplay.HungerAndThirst.*`, `ISustenance`), which needs the statuses of oblivion-mod's `rp-framework` branch, and
that branch's SDK is released nowhere. A server package from *rp-framework CI* carries it as NuGet packages in `sdk/`.
`pack.ps1` copies them into `.sdk/`, the source `nuget.config` names, and `Directory.Build.props` reads the version from
them, so the mods compile against what that server loads and no oblivion-mod checkout is needed. After unpacking a new
server, run `./pack.ps1 -PackOnly` once before building in an IDE. The server needs `Roleplay.HungerAndThirst` installed
(a manifest dependency) and the `rp-framework` build of the game's OblivionMp mod.

`src/CyrodiilRP.Common` goes to every player with the client, so it holds only what both halves read: the RPC contract
(`RpContracts`), the chat's line and limits (`ChatLine`, `ChatLineKind`, `ChatLimits`), the mark of the mode's
traders, bankers and stablemasters (`TraderOrigin`) and the staff's mark in a nickname (`Nameplate`). What a page shows (the `*View` records) and its JSON context (`*Json`) are the server's, in the
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
./pack.ps1                 # build, database up, migrations, the mods into the server's mods/ (their config.json kept)
./pack.ps1 -Test           # the tests first, and the packed mod on the real server before it is installed
./pack.ps1 -SkipDatabase   # leave the database alone
./pack.ps1 -PackOnly       # only build the mods into out/mods
```

`pack.ps1` needs PowerShell 7, on Linux `pwsh`. It packs for a Windows or a Linux server from either OS. It builds
`CyrodiilRP`, `Roleplay.HungerAndThirst`, `Roleplay.Jobs` and `Roleplay.Emotes`, refuses while that server runs and keeps each deployed
`server/config.json`: a new mod is put together in `<server>/mods-staging`, the deployed config copied into it as it is (an empty one too), and the
folders are swapped. Until the swap the deployed mod stays as it was. A deploy that stops half way leaves the mod
deployed before, config and all, in `mods-staging/<mod>.previous`, and the next one refuses until it is gone.
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
| `stable` | the gold each horse costs at a stablemaster: `paint`, `bay`, `chestnut`, `white`, `black` (500, 1000, 2500, 4000 and 5000 by default, as the game's stables ask; 0 gives it away, below 0 stops the mod) |
| `noticeBoards` | the notice boards: `shown` (10), `minLength` and `maxLength` (1 and 300), `perCharacter` (3), `cooldownMinutes` (5), `expiryDays` (7) and `asset`, the game's form a board stands as (`docs/features/notice-board.md`) |
| `lore` | the lore card: `catalogMode` (`Canon` by default) and `allowImplausibleSubtypes` (`false`: no kinds that do not fit the race's body, such as the Senche) |