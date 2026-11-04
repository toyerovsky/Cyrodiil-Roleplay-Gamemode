#!powershell.exe -ExecutionPolicy Bypass -File

# Edit these lists to match your mod. MakeModFolder.ps1 uses them to build
#
#   Output/mods/<modName>/manifest.json
#   Output/mods/<modName>/client/...
#   Output/mods/<modName>/server/...
#
# The server hands the client folder to connecting players and keeps the server folder to itself.

# Folder name of the packaged mod. Conventionally the uniqueId from manifest.json.
$modName = "Roleplay.HungerAndThirst"

# The projects the two halves build from.
$clientProject = "Roleplay.HungerAndThirst.Client"
$serverProject = "Roleplay.HungerAndThirst.Server"

# --- Client half, from <clientProject>/bin/<Configuration>/net10.0 ---
$clientBuildFiles = @(
    "Roleplay.HungerAndThirst.Client.dll",
    "Roleplay.HungerAndThirst.Common.dll"   # shared assembly both halves need
)

# --- Server half, from <serverProject>/bin/<Configuration>/net10.0 ---
$serverBuildFiles = @(
    "Roleplay.HungerAndThirst.Server.dll",
    "Roleplay.HungerAndThirst.Common.dll"
)

# Copied from "Content" into the mod root, alongside the two halves.
$manifestFiles = @(
    "manifest.json"
)

# Copied from "Content" into client/ and server/. Add any non-code files your mod ships.
$clientContentFiles = @(
    "web"           # the HUD and satchel pages, loaded as readym://hungerandthirst/
)

$serverContentFiles = @(
    "config.json"   # every setting with its default; edit it on the server
)

# Added only in Debug builds.
$clientDebugBuildFiles = @(
    "Roleplay.HungerAndThirst.Client.pdb",
    "Roleplay.HungerAndThirst.Common.pdb"
)

$serverDebugBuildFiles = @(
    "Roleplay.HungerAndThirst.Server.pdb",
    "Roleplay.HungerAndThirst.Common.pdb"
)
