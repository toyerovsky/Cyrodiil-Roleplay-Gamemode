# Cooks the rods export_rods.py made into the mode's own IoStore container for Oblivion Remastered: makes an Unreal
# 5.3 project named as the game in WORK, imports the rods with import_rods.py, cooks chunk 1 and copies its three files
# to OUT as CyrodiilRP_P.pak, .utoc and .ucas, for the game's Content\Paks\~mods.
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
# A fresh import each time: a reimport over an asset keeps the import settings stored on it.
foreach ($folder in 'Content\CyrodiilRP', 'Content\Materials') {
    $imported = Join-Path $project $folder
    if (Test-Path $imported) { Remove-Item $imported -Recurse -Force }
}
$uproject = Join-Path $project 'OblivionRemastered.uproject'
# The commandlet reads backslashes in -script as escapes (a \t in a path turns into a tab), so both paths go with forward ones.
$script = (Join-Path $PSScriptRoot 'import_rods.py') -replace '\\', '/'
$sourceDir = (Resolve-Path $Source).Path -replace '\\', '/'

& $editor $uproject -run=pythonscript "-script=$script $sourceDir" -unattended -nop4 -nosplash -stdout -FullStdOutLogOutput
if ($LASTEXITCODE -ne 0) { throw "The import failed ($LASTEXITCODE)." }

# Versioned: each property is saved with its name. The game's engine has fields stock 5.3.2 lacks (three more in
# FBodyInstance, from 5.4), so properties saved by their place in the class, as a cook does by default, land on the
# wrong fields and crash it ("Bad name index").
& $uat BuildCookRun "-project=$uproject" -noP4 -platform=Win64 -clientconfig=Shipping -cook -stage -pak -iostore -skipbuild -nocompileeditor -unattended -utf8output -VersionCookedContent
if ($LASTEXITCODE -ne 0) { throw "The cook failed ($LASTEXITCODE)." }

$paks = Join-Path $project 'Saved\StagedBuilds\Windows\OblivionRemastered\Content\Paks'
foreach ($extension in 'pak', 'utoc', 'ucas') {
    $cooked = Join-Path $paks "pakchunk1-Windows.$extension"
    if (-not (Test-Path $cooked)) { throw "The cook made no $cooked." }
    Copy-Item $cooked (Join-Path $Out "CyrodiilRP_P.$extension") -Force
}

# Only the mode's own assets may ship: the stand-in for the game's base material would replace the game's own.
$listing = & (Join-Path $Engine 'Engine\Binaries\Win64\UnrealPak.exe') (Join-Path $Out 'CyrodiilRP_P.utoc') -List 2>&1 | Out-String
$foreign = [regex]::Matches($listing, '"\.\./\.\./\.\./OblivionRemastered/Content/(?!CyrodiilRP/)[^"]+"') | ForEach-Object Value
if ($foreign) { throw "The container holds assets outside /Game/CyrodiilRP: $($foreign -join ', ')" }
Get-ChildItem $Out -Filter 'CyrodiilRP_P.*' | Select-Object Name, Length
