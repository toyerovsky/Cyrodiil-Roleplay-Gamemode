#!powershell.exe -ExecutionPolicy Bypass -File

# The lists MakeModFolder.ps1 builds Output/mods/Roleplay.Emotes/ from.

$modName = "Roleplay.Emotes"

$clientProject = "Roleplay.Emotes.Client"
$serverProject = "Roleplay.Emotes.Server"

$clientBuildFiles = @(
    "Roleplay.Emotes.Client.dll",
    "Roleplay.Emotes.Common.dll"
)

$serverBuildFiles = @(
    "Roleplay.Emotes.Server.dll",
    "Roleplay.Emotes.Common.dll"
)

$manifestFiles = @(
    "manifest.json"
)

$clientContentFiles = @()

$serverContentFiles = @()

# Added for Debug builds only
$clientDebugBuildFiles = @(
    "Roleplay.Emotes.Client.pdb",
    "Roleplay.Emotes.Common.pdb"
)

$serverDebugBuildFiles = @(
    "Roleplay.Emotes.Server.pdb",
    "Roleplay.Emotes.Common.pdb"
)
