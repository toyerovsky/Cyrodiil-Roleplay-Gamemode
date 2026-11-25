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
$imported = Join-Path $project 'Content\CyrodiilRP'
if (Test-Path $imported) { Remove-Item $imported -Recurse -Force }
$uproject = Join-Path $project 'OblivionRemastered.uproject'
# The commandlet reads backslashes in -script as escapes (a \t in a path turns into a tab), so both paths go with forward ones.
$script = (Join-Path $PSScriptRoot 'import_rods.py') -replace '\\', '/'
$sourceDir = (Resolve-Path $Source).Path -replace '\\', '/'

& $editor $uproject -run=pythonscript "-script=$script $sourceDir" -unattended -nop4 -nosplash -stdout -FullStdOutLogOutput
if ($LASTEXITCODE -ne 0) { throw "The import failed ($LASTEXITCODE)." }

& $uat BuildCookRun "-project=$uproject" -noP4 -platform=Win64 -clientconfig=Shipping -cook -stage -pak -iostore -skipbuild -nocompileeditor -unattended -utf8output
if ($LASTEXITCODE -ne 0) { throw "The cook failed ($LASTEXITCODE)." }

$paks = Join-Path $project 'Saved\StagedBuilds\Windows\OblivionRemastered\Content\Paks'
foreach ($extension in 'pak', 'utoc', 'ucas') {
    $cooked = Join-Path $paks "pakchunk1-Windows.$extension"
    if (-not (Test-Path $cooked)) { throw "The cook made no $cooked." }
    Copy-Item $cooked (Join-Path $Out "CyrodiilRP_P.$extension") -Force
}
Get-ChildItem $Out -Filter 'CyrodiilRP_P.*' | Select-Object Name, Length
