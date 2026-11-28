---
name: game-assets
description: What the mode draws and plays from Oblivion Remastered (meshes, effects, sounds, materials) and how it makes assets the game lacks (Blender, Unreal 5.3.2, a container shipped with the mod), with the crashes that taught each rule and how to diagnose the next one. Use before picking a game mesh, effect or sound for the client, before making or changing a model, texture, material or animation of the mode's own, and when the game crashes on loading one.
---

# Game assets

The client draws and plays the game's assets by path: `ILocalProps.Place` a static mesh (`/Game/...SM_X.SM_X`),
`IVisualEffects.Play` a Niagara system, `ISounds.PlayAt` an `AkAudioEvent` asset (`/Game/WwiseAudio/<folder>/<event>.<event>`,
not a `.bnk`). What the game lacks, the mode makes itself and ships as a container (`mod/paks`, Git LFS) in the mod's
client half: the launcher copies every `.pak`, `.utoc` and `.ucas` of a server's mods into the game's
`Content\Paks\~OblivionMp` as a player joins, before the game starts and mounts them, and clears the folder at the
next join.

## Picking the game's own

- **Judge by the asset, never by its name.** The first reel sound was `obj_trp_gears_slow`, picked by name: a looping,
  ringing trap mechanism that players heard as a bell. The first fish shoal was `NS_Static_SmallFishes_01`: hundreds of
  fish, a count no SDK call sets. Look at a render, listen to or measure a sound, before it goes in.
- **The research repo (oblivion-research) has the catalogues:** rendered props and items under
  `out/catalog/*/renders*`, exported meshes as `.glb` and materials as `.raw.json` under `out/catalog/*/ue`, Wwise
  events in `out/wwise/events/events.tsv` with their media in `media.tsv`, each event's `IsInfinite` (loops),
  `MinimumDuration` and `MaximumDuration` and `MaxAttenuationRadius` (0 is a 2D sound) in
  `out/ue/wwise-events/OblivionRemastered/Content/WwiseAudio/<folder>/<event>.json`, the folder giving the asset path.
- **Sounds:** `python -I tools/wwise/extract_wwise.py GAME_DIR OUT "pattern*"` in the research repo decodes the events'
  media to WAV. Without listening, measure them: length, how fast the envelope falls, onsets (clicks), how much of the
  energy sits in a few frequency bins (high means a ringing tone). The reel's ratchet is
  `Interface/Lockpick/ui_lock_tumbler_move` (19 dry clicks in 1.4 to 1.6 s), its drag
  `Interface/MiniGames/Redesign/ui_lockpicking_tumbler_fast`. A sound that does not loop is played again on a beat just
  under its `MinimumDuration`.
- **How loud a sound is in the game** is its WAV's level plus the Volume and MakeUpGain its event's bank sets on it and
  its parents (HIRC, version 150: property 0 Volume, 1 Pitch, 5 MakeUpGain), which shifts sounds by -17 to +20 dB: a
  raw WAV misleads. Media on bus 3570388967 are controller haptics, not audio: an interface event "0.11 to 0.24 s" long
  is haptics only.
- **Measure when a sound speaks, not only how loud it gets.** The fishing's first strain creak, `obj_trp_rope_stretch`,
  peaks 1.2 s in and was never heard as a warning on a 0.45 s beat. A warning must differ in kind from what it warns
  over (a tone over clicks, not more clicks): a squeak shows as harmonic partials, a bell as inharmonic ones.
- **The game has no fishing rod mesh,** and Nexus has none for the remaster (the original Oblivion's are in its old
  engine's format). Its staves (`SM_Staff01` and kin) hang on `Weapon_Socket` by the grip at the mesh's origin, the
  shaft along the mesh's Y, the head on Unreal's -Y.

## Making the mode's own

`design/models/` holds each model's source as code, `design/README.md` the commands. The fishing rods are the worked
example: `make_rods.py` (Blender 4.2, from code), `export_rods.py` (bake and FBX), `unreal/build_pak.ps1` (Unreal Engine
5.3.2 at `C:\Program Files\Epic Games\UE_5.3`, from the Epic Games Launcher). The client holds the mode's mesh only
where `ILocalProps.TryMeasure` finds it and the game's stand-in elsewhere, since a player without the container must
still see something (`AnglerScenery.HeldRodOf`).

### Blender

- A model lies as the game asset it stands beside: a held thing along +Y, the hand at the origin. Blender's
  (x, y, z) in metres becomes Unreal's (x, -y, z) in centimetres.
- Bake on a UV map of its own. Long thin parts (a 2 m rod, a line) make long islands that the packer scales the whole
  sheet down for: cut them into pieces of about half a metre (`lathe_pieces`, `run_line`) and the sheet fills.
- Procedural patterns read their UVs through a named UV Map node, so the bake map can be made active without moving
  them.
- Cycles bakes no metallic pass, and its diffuse colour pass is black for a metal: route the Principled input into an
  emission and bake EMIT (`bake_input`).
- Hide every other object while baking ambient occlusion: objects at the origin shade each other.
- Preview lights: an area light's power over its distance, P / (pi d^2), is the irradiance in W/m2; keep it near
  daylight's 2 to 5. Ten times that bleaches every colour.
- Check the export by importing the FBX back into an empty Blender with only the baked textures and rendering it
  (`check_export.py`).
- A variant of a baked model (the bent rods) is the baked mesh with its vertices moved, not a model baked again: it
  keeps the UVs, so it wears the same textures and material instance and the container grows by mesh data only.

### Unreal, and the game's own engine

The remaster runs `5.3.2-237883`, Unreal 5.3.2 changed by its makers, partly with 5.4's code. Assets cooked by the
stock 5.3.2 must allow for that. With the rules below the rods load and show in the game (seen 2026-11-26):

- **Cook with each property saved by its name** (`BuildCookRun ... -VersionCookedContent`). The default cook saves
  properties by their place in the class, and the game's classes differ: its `FBodyInstance` holds
  `bOneWayInteraction`, `bOverrideSolverAsyncDeltaTime` and `SolverAsyncDeltaTime`, so a stock mesh's collision
  profile name landed on a bool and the game crashed (`ObjectSerializationError ... BodySetup ... Bad name index`).
- **Ship no material of the mode's own.** A stock-cooked `Material` crashes the game on load (`... Material ... Bad
  export index`). Make material instances of the game's materials instead: held things take
  `/Game/Materials/Template/MI_T_BaseMaterial_FPSClippingFix` with `BaseColor Map` (sRGB colour) and `NNRM Map`
  (non-sRGB: R and G the normal's X and Y as DirectX has them, green down, B roughness, A metallic), as the staves'
  `MIC_Staff01` does. It has no occlusion input, so the colour carries it. The project makes a stand-in at that path
  so the instance can name it, and the stand-in is never shipped (below).
- **The project is named as the game** (`OblivionRemastered`), so `/Game/` mounts where the game's does. The mode's
  assets live under `/Game/CyrodiilRP/`, a `PrimaryAssetLabel` there (chunk 1, `apply_recursively` off, so nothing it
  refers to follows it) puts them in `pakchunk1`, which ships as `CyrodiilRP_P.pak`, `.utoc` and `.ucas`; IoStore, shaders
  not shared. `build_pak.ps1` refuses a container holding anything outside `/Game/CyrodiilRP`: a copy of a game asset
  would replace the game's own.
- FBX from Blender needs `convert_scene_unit` on import, or a 2 m rod comes in 2 cm long. `import_rods.py` refuses
  a mesh of the wrong length.
- The PythonScript commandlet reads backslashes in `-script` as escapes: pass paths with forward slashes.
- A reimport over an existing asset keeps the import settings stored on it: import into an emptied folder.

### Checking a container without the game

Read it the way the game would, with the game's class layout:

1. A folder whose `OblivionRemastered/Content/Paks` holds hard links to the game's `.pak`, `.utoc` and `.ucas` (no copy,
   same drive) and the container.
2. `dotnet run -c Release --project tools/ue/UeExport -- json <folder> <out> --usmap tools/bin/usmap/Mappings.usmap
   OblivionRemastered/Content/CyrodiilRP/` in the research repo.
3. It reports no error even for a container that crashes the game. Read the JSON: a field where another belongs (the
   crashing rod showed `bOneWayInteraction` where `CollisionProfileName` is), a GUID of zeros, `CookedFormatData`
   null. A container the game will read has its properties, render data, mips and parents whole.

`python -I usmap_fields.py <Mappings.usmap> <oodle dll> <Class>...` (in this skill's folder) lists a class's fields in
the game, to compare with the stock headers in `UE_5.3\Engine\Source\Runtime`.

### Animations (not yet loaded in the game)

`design/models/fishing-anim/` makes the mode's first animation sequence, `/Game/CyrodiilRP/Fishing/A_Fishing_Reel`, and
cooks it into `CyrodiilRPAnim_P`. **The game has never loaded it:** it passes the offline check below and nothing
more, so everything here is what the cook and the readers showed, not what the game did. It goes into `~mods` by hand
first, as any container.

- **A sequence is the game's own with other keys.** The base is a game sequence read out by `UeExport anims` (the
  research repo, `out/catalog/animations`: 2044 sequences decoded, rendered and listed). Only the bones that must
  move are keyed anew, every other track keeps the base's keys, so the pose layers over the base in the `UpperBody`
  slot. The game's rig keys helper bones from the main ones (a forearm's twist bones take 0.8 and 0.2 of the hand's
  twist, the corrective roots turn half way back): `make_reel.py` has the rules, fitted over the game's sequences.
- **Bring every quaternion to unit length.** `UeExport` leaves the decoded ones up to 0.17% short at some keys.
  Unreal takes a key as given and made rotations 7 degrees off of them, on every bone at the same keys.
- **A stand-in for the game's skeleton, at the game's path, never shipped** (chunk 0), as the rods' material parent.
  A cooked sequence is tied to its skeleton by three things, and the stand-in must share each:
  - the bones' order, since a track names its bone by index. Unreal's FBX import orders a mesh's bones by a
    depth-first walk and adds what a later mesh brings at the end. The game's skeleton (401 bones) is such a walk of
    371 bones and four additions, so five meshes imported in turn give its order. `import_reel.py` refuses any other.
  - the guid, which the sequence saves and compares when it loads. Python cannot set it: `import_reel.py` writes the
    game's into the stand-in's `.uasset` (16 bytes after the bones' name-to-index map) before the editor loads it.
    Any later change to the skeleton gives it a new one (adding a virtual bone did), so the skeleton is finished in
    a first run of the editor and only read in the second.
  - the virtual bones (the game has six, for the feet): the cook bakes a track for each, as the game's sequences have.
- **Keys go in through the animation data controller** (`sequence.controller`: `add_bone_curve`,
  `set_bone_track_keys`), not through an FBX, so no axes or units lie between the keys written and the keys cooked.
- **Name the engine's compression settings.** A new sequence in a stock 5.3 project takes
  `/ACLPlugin/ACLAnimBoneCompressionSettings`. The game's humanoid sequences name
  `/Engine/Animation/DefaultAnimBoneCompressionSettings` (and the curve and frame stripping defaults beside it), which
  holds the same eleven codecs in the game as in stock 5.3.2. The cook picks one and writes its name into the sequence
  (`AnimCompress_PerTrackCompression_0`, as the base has), and the game looks that name up in its own copy.
- **`TargetFrameRate`.** The game's sequences carry 30. Stock 5.3.2 leaves the class's 60000 and saves nothing, and
  Python holds the property read-only. The struct `get_editor_property` hands out is the object's own: setting its
  `numerator` sets the sequence's. For a read-only name on one object, the console's `set <object> <property> <value>`
  works (the stand-in's sockets).
- **What the stock cook does differently from the game's.** It orders the tracks by the skeleton, not as they were
  added (the map names the bones, so the order is free). It rounds to none a small constant rotation of a bone that
  moves no bone under it: three leg helpers (`calf_twist_02_l` and `_r`, `thigh_twist_01_l`) lose up to 5.7 degrees of
  twist that the game's cook of the base kept. The legs are outside the `UpperBody` slot.
- **Checking it:** `check_reel.py` on what `UeExport json` and `anims` read from the container beside the game's (the
  commands are in `design/README.md`). `UeExport`'s JSON shows `CompressedDataStructure` only when the codec the
  sequence names is found in the game's settings asset. `preview_reel.py` draws the decoded keys on the game's figure
  with a rod on `Weapon_Socket`, through the catalogue's own renderer.
- **Still unknown until the game loads it:** whether the game's engine reads a stock-cooked sequence's own data
  (after the properties: the skeleton guid, the compressed bytes and the codec names), which no `-VersionCookedContent`
  covers and the rods never exercised. A crash there would name `AnimSequence` in `CrashContext.runtime-xml`.

## When the game crashes on an asset

- The report: `%USERPROFILE%\Documents\My Games\Oblivion Remastered\Saved\Crashes\<newest>\CrashContext.runtime-xml`,
  its `ErrorMessage` names the package and the export. The client's log: the game's `Binaries\Win64\UE4SS.log`.
  `[ASSET] Asset not found in registry` there is harmless: the container brings no asset registry, the load goes on.
- The game mounts containers when it starts and holds the files while it runs: a running game keeps the old one (and
  crashes again) until it is closed. A container put in `Content\Paks\~mods` by hand, to try one before it ships,
  goes out by deleting `CyrodiilRP_P.*` there; the launcher's copy in `~OblivionMp` comes back at every join until
  `mod/paks` changes on the server.
- A container is tried by hand in `~mods` first, and checked offline before that (above): once in `mod/paks` on the
  server, it reaches every player who joins.
