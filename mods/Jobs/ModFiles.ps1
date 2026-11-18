#!powershell.exe -ExecutionPolicy Bypass -File

# The lists MakeModFolder.ps1 builds Output/mods/Roleplay.Jobs/ from.

$modName = "Roleplay.Jobs"

$clientProject = "Roleplay.Jobs.Client"
$serverProject = "Roleplay.Jobs.Server"

$clientBuildFiles = @(
    "Roleplay.Jobs.Client.dll",
    "Roleplay.Jobs.Common.dll"
)

$serverBuildFiles = @(
    "Roleplay.Jobs.Server.dll",
    "Roleplay.Jobs.Common.dll"
)

$manifestFiles = @(
    "manifest.json"
)

$clientContentFiles = @(
    "QuarryJob.esp"
    "web"           # the progress panel, loaded as readym://jobs/panel.html
)

$serverContentFiles = @(
    "config.json"
    "herbs.json"    # the plants herbalism gates, with their names and the Alchemy each asks
)

# Added for Debug builds only
$clientDebugBuildFiles = @(
    "Roleplay.Jobs.Client.pdb",
    "Roleplay.Jobs.Common.pdb"
)

$serverDebugBuildFiles = @(
    "Roleplay.Jobs.Server.pdb",
    "Roleplay.Jobs.Common.pdb"
)
