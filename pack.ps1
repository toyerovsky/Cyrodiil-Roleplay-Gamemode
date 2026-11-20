#Requires -Version 7

<#
    Builds everything and puts it into the server: the pages, the client half and the server half go to
    <server>/mods/CyrodiilRP, the Hunger and Thirst, Jobs and Emotes mods beside it, the database gets the pending migrations,
    and the platform's chat and MarkerMod go to mods-disabled/: both chats answer K, and the mode draws the names over
    heads itself. Each deployed server/config.json (database, owners) is kept.

    The server is OBLIVIONMP_SERVER, or the newest oblivion-server-* folder next to this script, or -ServerDir. Its
    sdk/ folder must hold the SDK of the repository's .sdk/, which the mods compile against, so they match the server
    whatever OS it runs on.

      ./pack.ps1                 build, migrate, install
      ./pack.ps1 -Test           run the tests first, and the packed mod on the real server before it is installed
      ./pack.ps1 -SkipDatabase   leave the database alone
      ./pack.ps1 -PackOnly       only build the mods into out/mods
      ./pack.ps1 -TakeSdk        move .sdk/ to the server's SDK first, for the commit that takes a new one
      ./pack.ps1 -ServerDir D:\other-server
#>

param(
    [string] $Configuration = 'Release',
    [string] $ServerDir = $env:OBLIVIONMP_SERVER,
    [switch] $Test,
    [switch] $SkipDatabase,
    [switch] $PackOnly,
    [switch] $TakeSdk
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$outMods = Join-Path $root 'out/mods'
$out = Join-Path $outMods 'CyrodiilRP'
$publish = Join-Path $root 'out/publish'
$tests = Join-Path $root 'tests/CyrodiilRP.Tests'
$sdkFeed = Join-Path $root '.sdk'

# The mods with a folder of their own under mods/, by the id they deploy as.
$roleplayMods = [ordered] @{ 'Roleplay.HungerAndThirst' = 'HungerAndThirst'; 'Roleplay.Jobs' = 'Jobs'; 'Roleplay.Emotes' = 'Emotes' }

function Write-Step([string] $text) { Write-Host "`n== $text" -ForegroundColor Cyan }

function Invoke-Checked([string] $what, [scriptblock] $command)
{
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit code $LASTEXITCODE)." }
}

# The new mod is put together beside mods/, where the server does not look, with the deployed server/config.json
# (database, owners) copied in as it is, then swapped in whole. Until the swap the deployed folder stays as it was, and
# one that stops half way leaves the mod deployed before in the staging folder, config and all.
function Install-Mod([string] $from, [string] $mods, [string] $staging)
{
    $name = Split-Path $from -Leaf
    $target = Join-Path $mods $name
    $fresh = Join-Path $staging $name
    $previous = Join-Path $staging "$name.previous"
    if (Test-Path $previous)
    {
        throw "$previous is left by a deploy that stopped half way: it is the mod deployed before, with its server/config.json. Take what $target needs from it, delete it and deploy again."
    }

    if (Test-Path $fresh) { Remove-Item $fresh -Recurse -Force }
    New-Item -ItemType Directory -Force $staging | Out-Null
    Copy-Item $from $fresh -Recurse

    $config = Join-Path $target 'server/config.json'
    $kept = Test-Path $config
    if ($kept) { Copy-Item $config (Join-Path $fresh 'server/config.json') -Force }

    # A clean folder, so a file removed from the repository does not stay behind in the server.
    if (Test-Path $target) { Move-Item $target $previous }
    try
    {
        Move-Item $fresh $target
    }
    catch
    {
        if (Test-Path $previous) { Move-Item $previous $target }
        throw
    }

    if (Test-Path $previous) { Remove-Item $previous -Recurse -Force }
    if (-not (Get-ChildItem $staging -Force)) { Remove-Item $staging }
    if ($kept) { Write-Host "  $name/server/config.json kept" }
}

function Find-SdkVersion([string] $folder)
{
    $common = Get-ChildItem $folder -Filter 'ReadyM.SDK.Oblivion.Common.*.nupkg' -ErrorAction Ignore
    if ($common) { $common.BaseName -replace '^ReadyM\.SDK\.Oblivion\.Common\.', '' }
}

# The repository keeps the SDK its code is written against in .sdk/, and the server's package carries the one it was
# built with in sdk/. A server built with another is refused, so a mod cannot compile against one SDK and be deployed
# to another. -TakeSdk moves .sdk/ to the server's instead.
function Use-ServerSdk([string] $server)
{
    $serverSdk = Join-Path $server 'sdk'
    $theirs = Find-SdkVersion $serverSdk
    if (-not $theirs) { throw "No SDK in ${serverSdk}: take a server package from rp-framework CI, which carries one." }
    if (@($theirs).Count -gt 1) { throw "$serverSdk holds the packages of more than one SDK ($($theirs -join ', ')): unpack the server again." }

    $ours = Find-SdkVersion $sdkFeed
    if (@($ours).Count -gt 1) { throw ".sdk/ holds the packages of more than one SDK ($($ours -join ', ')): delete the ones git does not track, with git clean -f .sdk." }
    if ($ours -ne $theirs)
    {
        if (-not $TakeSdk)
        {
            throw "The server in $server is built with SDK $theirs and the repository's .sdk/ holds $($ours ?? 'none'). Use the server package of $($ours ?? 'that SDK'), or run with -TakeSdk to move .sdk/ to $theirs and commit it."
        }

        Get-ChildItem $sdkFeed -Filter '*.nupkg' | Remove-Item
        Get-ChildItem $serverSdk -Filter '*.nupkg' | Copy-Item -Destination $sdkFeed
    }

    Write-Host "SDK: $theirs" -ForegroundColor Green
}

# Either OS's server: a Linux one can be packed for on Windows.
function Find-ServerBinary([string] $server)
{
    $binary = 'server', 'server.exe' | ForEach-Object { Join-Path $server $_ } | Where-Object { Test-Path $_ -PathType Leaf } | Select-Object -First 1
    if (-not $binary) { throw "No OblivionMP server in $server (no server or server.exe)." }
    $binary
}

# "oblivion-server-win64-1.0.0-rc.2", "oblivion-server-linux64-v1.1-rp-framework-688fc210": by version, a release is
# newer than the version's other builds, and those go by when the server was built, as a commit's hash has no order.
function Find-NewestServer
{
    $servers = foreach ($folder in Get-ChildItem $root -Directory -Filter 'oblivion-server-*')
    {
        $parts = $folder.Name.Split('-')
        $at = [Array]::FindIndex($parts, [Predicate[string]] { param($part) [version]::TryParse($part.TrimStart('v'), [ref] $null) })
        if ($at -ge 0)
        {
            $built = (Get-Item (Find-ServerBinary $folder.FullName)).LastWriteTimeUtc
            # [version] gives a missing part -1, which would sort "1.1" below "1.1.0".
            $named = [version] $parts[$at].TrimStart('v')
            $version = [version]::new($named.Major, $named.Minor, [Math]::Max($named.Build, 0), [Math]::Max($named.Revision, 0))
            [pscustomobject] @{ Path = $folder.FullName; Version = $version; Release = $at -eq $parts.Length - 1; Built = $built }
        }
    }
    $newest = $servers | Sort-Object Version, Release, Built | Select-Object -Last 1
    if (-not $newest) { throw "No oblivion-server-* folder in ${root}: unpack the server there or give -ServerDir." }
    $newest.Path
}

if (-not $ServerDir) { $ServerDir = Find-NewestServer }
$ServerDir = (Resolve-Path $ServerDir).Path
Find-ServerBinary $ServerDir | Out-Null
Write-Host "Server: $ServerDir" -ForegroundColor Green
Use-ServerSdk $ServerDir

# The running server holds the mod's DLLs, and a half copied mod is worse than none.
if (-not $PackOnly)
{
    $running = Get-Process server -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($ServerDir, [StringComparison]::OrdinalIgnoreCase) }
    if ($running) { throw "The server in $ServerDir is running (pid $($running.Id -join ', ')). Stop it first." }
}

Write-Step 'Pages'
Push-Location (Join-Path $root 'ui')
try
{
    if (-not (Test-Path 'node_modules')) { Invoke-Checked 'npm ci' { npm ci --no-audit --no-fund } }
    if ($Test) { Invoke-Checked 'The page tests' { npx vitest run } }
    Invoke-Checked 'The pages build' { npm run build }
}
finally { Pop-Location }

# The database tests start a PostgreSQL of their own in Docker, so Docker must run.
if ($Test)
{
    Write-Step 'Tests'
    Invoke-Checked 'The C# tests' { dotnet test $tests --filter 'Category!=Server' --nologo }
}

Write-Step "Mod into $out"
Invoke-Checked 'The server publish' { dotnet publish (Join-Path $root 'src/CyrodiilRP.Server') -c $Configuration -o (Join-Path $publish 'server') --nologo }
Invoke-Checked 'The client build' { dotnet build (Join-Path $root 'src/CyrodiilRP.Client') -c $Configuration --nologo }

# What the server and its other mods already load: a second copy in our load context is refused.
$shipped = @{}
Get-ChildItem $ServerDir -Filter '*.dll' | ForEach-Object { $shipped[$_.Name] = $true }
Get-ChildItem (Join-Path $ServerDir 'mods') -Directory |
    Where-Object { $_.Name -ne 'CyrodiilRP' -and -not $roleplayMods.Contains($_.Name) } |
    ForEach-Object { Get-ChildItem $_.FullName -Filter '*.dll' -Recurse } |
    ForEach-Object { $shipped[$_.Name] = $true }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$client = Join-Path $out 'client'
$server = Join-Path $out 'server'
New-Item -ItemType Directory -Force $client, $server | Out-Null

Copy-Item (Join-Path $root 'mod/manifest.json') $out

$clientBuild = Join-Path $root "src/CyrodiilRP.Client/bin/$Configuration/net10.0"
Copy-Item (Join-Path $clientBuild 'CyrodiilRP.Client.dll'), (Join-Path $clientBuild 'CyrodiilRP.Common.dll') $client
Copy-Item (Join-Path $root 'ui/dist') (Join-Path $client 'web') -Recurse

# The SDK and what it is built on come with the server, whether or not the server folder names each file.
$sdkFamily = 'ReadyM.*', 'OblivionMp.*', 'Friflo.*', 'Yooni.*', 'LiteNetLib*', 'DryIoc*', 'Serilog*', 'Superpower*', 'Roleplay.HungerAndThirst.*', 'Roleplay.Emotes.*', 'Roleplay.Jobs.*'
$serverPublish = Join-Path $publish 'server'
Get-ChildItem $serverPublish -Filter '*.dll' |
    Where-Object { $name = $_.Name; -not $shipped.ContainsKey($name) -and -not ($sdkFamily | Where-Object { $name -like $_ }) } |
    Copy-Item -Destination $server
Copy-Item (Join-Path $serverPublish 'locales') (Join-Path $server 'locales') -Recurse
Copy-Item (Join-Path $serverPublish 'config.json') $server

foreach ($id in $roleplayMods.Keys)
{
    $folder = Join-Path $root "mods/$($roleplayMods[$id])"
    Invoke-Checked "The $id build" { & (Join-Path $folder 'MakeModFolder.ps1') -Configuration $Configuration -NoExplorer }
    $packed = Join-Path $outMods $id
    if (Test-Path $packed) { Remove-Item $packed -Recurse -Force }
    Copy-Item (Join-Path $folder "Output/mods/$id") $outMods -Recurse
}

# The packed mod on a copy of the real server, before it goes into this one.
if ($Test)
{
    Write-Step 'The mod on the real server'
    Invoke-Checked 'The server smoke tests' { dotnet test $tests --filter 'Category=Server' --nologo }
}

if ($PackOnly)
{
    Write-Host "`nPacked: $outMods" -ForegroundColor Green
    return
}

if (-not $SkipDatabase)
{
    Write-Step 'Database'
    Invoke-Checked 'docker compose' { docker compose -f (Join-Path $root 'infra/docker-compose.yml') up -d --wait }
    Invoke-Checked 'The migrations' { dotnet run --project (Join-Path $root 'src/CyrodiilRP.Migrator') -c $Configuration }
}

$mods = Join-Path $ServerDir 'mods'
$target = Join-Path $mods 'CyrodiilRP'
Write-Step "Mods into $mods"
foreach ($packed in Get-ChildItem $outMods -Directory) { Install-Mod $packed.FullName $mods (Join-Path $ServerDir 'mods-staging') }

# The platform's chat answers K as ours does, and its MarkerMod writes a name over a head once and never again.
foreach ($platformMod in 'OblivionMP.Chat', 'OblivionMp.MarkerMod')
{
    $installed = Join-Path $mods $platformMod
    if (-not (Test-Path $installed)) { continue }

    $parked = Join-Path $ServerDir "mods-disabled/$platformMod"
    New-Item -ItemType Directory -Force (Split-Path $parked) | Out-Null
    if (Test-Path $parked) { Remove-Item $parked -Recurse -Force }
    Move-Item $installed $parked
    Write-Host "  $platformMod moved to mods-disabled/"
}

Write-Host "`nReady: $target" -ForegroundColor Green
