# Cooks the animation make_reel.py made into an IoStore container of the mode's own for Oblivion Remastered: makes an
# Unreal 5.3 project named as the game in WORK, imports the stand-in skeleton (import_skeleton.py) and the animation
# (import_reel.py), cooks chunk 1 and copies its three files to OUT as CyrodiilRPAnim_P.pak, .utoc and .ucas, beside
# the rods' CyrodiilRP_P in the game's Content\Paks\~mods.
# NOT YET LOADED IN THE GAME: the container passes the offline check (check_reel.py), nothing more. It stays out of
# mod/paks until it has been tried by hand in ~mods.
param(
    [Parameter(Mandatory)] [string] $Source,
    [Parameter(Mandatory)] [string] $Work,
    [Parameter(Mandatory)] [string] $Out,
    [Parameter(Mandatory)] [string] $Engine
)
$ErrorActionPreference = 'Stop'

$editor = Join-Path $Engine 'Engine\Binaries\Win64\UnrealEditor-Cmd.exe'
$uat = Join-Path $Engine 'Engine\Build\BatchFiles\RunUAT.bat'
foreach ($tool in $editor, $uat) {
    if (-not (Test-Path $tool)) { throw "No ${tool}: point -Engine at an Unreal Engine 5.3 install." }
}

$project = Join-Path $Work 'OblivionRemastered'
New-Item -ItemType Directory -Force (Join-Path $project 'Config'), (Join-Path $project 'Content'), $Out | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'OblivionRemastered.uproject') $project -Force
Copy-Item (Join-Path $PSScriptRoot 'Config\*.ini') (Join-Path $project 'Config') -Force
# A fresh import each time: the stand-in skeleton's bone order comes from the order of its imports.
foreach ($folder in 'Content\CyrodiilRP', 'Content\Art') {
    $imported = Join-Path $project $folder
    if (Test-Path $imported) { Remove-Item $imported -Recurse -Force }
}
$uproject = Join-Path $project 'OblivionRemastered.uproject'
# The commandlet reads backslashes in -script as escapes (a \t in a path turns into a tab), so the paths go with forward ones.
$sourceDir = (Resolve-Path $Source).Path -replace '\\', '/'

# Two runs of the editor: the second writes the game skeleton's guid into the stand-in's file before it loads it.
foreach ($step in 'import_skeleton.py', 'import_reel.py') {
    $script = (Join-Path $PSScriptRoot $step) -replace '\\', '/'
    & $editor $uproject -run=pythonscript "-script=$script $sourceDir" -unattended -nop4 -nosplash -stdout -FullStdOutLogOutput
    if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)." }
}

# Versioned: each property is saved with its name, as the game's classes are not laid out as stock 5.3.2's (see the
# rods' build_pak.ps1).
& $uat BuildCookRun "-project=$uproject" -noP4 -platform=Win64 -clientconfig=Shipping -cook -stage -pak -iostore -skipbuild -nocompileeditor -unattended -utf8output -VersionCookedContent
if ($LASTEXITCODE -ne 0) { throw "The cook failed ($LASTEXITCODE)." }

$paks = Join-Path $project 'Saved\StagedBuilds\Windows\OblivionRemastered\Content\Paks'
foreach ($extension in 'pak', 'utoc', 'ucas') {
    $cooked = Join-Path $paks "pakchunk1-Windows.$extension"
    if (-not (Test-Path $cooked)) { throw "The cook made no $cooked." }
    Copy-Item $cooked (Join-Path $Out "CyrodiilRPAnim_P.$extension") -Force
}

# Only the mode's own assets may ship: the stand-in skeleton would replace the game's own.
$listing = & (Join-Path $Engine 'Engine\Binaries\Win64\UnrealPak.exe') (Join-Path $Out 'CyrodiilRPAnim_P.utoc') -List 2>&1 | Out-String
$foreign = [regex]::Matches($listing, '"\.\./\.\./\.\./OblivionRemastered/Content/(?!CyrodiilRP/)[^"]+"') | ForEach-Object Value
if ($foreign) { throw "The container holds assets outside /Game/CyrodiilRP: $($foreign -join ', ')" }
if ($listing -notmatch 'Content/CyrodiilRP/Fishing/A_Fishing_Reel\.uasset') { throw 'The container holds no A_Fishing_Reel.' }
Get-ChildItem $Out -Filter 'CyrodiilRPAnim_P.*' | Select-Object Name, Length
