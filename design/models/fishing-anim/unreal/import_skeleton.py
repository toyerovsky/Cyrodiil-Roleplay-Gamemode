# Runs inside Unreal Editor 5.3 (the PythonScript commandlet), first of two: makes the stand-in for the game's
# humanoid skeleton at the game's path, so the mode's animation can name it. The stand-in is cooked only into chunk 0,
# which is never shipped: in the game the reference finds the game's own skeleton.
# usage: UnrealEditor-Cmd.exe OblivionRemastered.uproject -run=pythonscript -script="import_skeleton.py SOURCE_DIR"
#
# A cooked animation names its bones by their place in the skeleton, so the stand-in must have the game's bones in the
# game's order. make_reel.py wrote the skeleton as meshes that each hold a longer run of it: the first import makes the
# skeleton, each later one adds its new bones at the end, as the game's skeleton grew. import_reel.py holds the result
# to the game's list before it makes anything. The stand-in also gets the game's virtual bones and sockets, which
# both shape what the cook writes into a sequence.
import json
import os
import sys

import unreal

SOURCE = os.path.abspath(sys.argv[1])
with open(os.path.join(SOURCE, "A_Fishing_Reel.json")) as made:
    SKELETON = json.load(made)["skeleton"]
FOLDER, NAME = SKELETON["path"].rsplit("/", 1)

tools = unreal.AssetToolsHelpers.get_asset_tools()
assets = unreal.EditorAssetLibrary


def options(skeleton):
    ui = unreal.FbxImportUI()
    ui.import_mesh = True
    ui.import_as_skeletal = True
    ui.import_materials = False
    ui.import_textures = False
    ui.import_animations = False
    ui.create_physics_asset = False
    ui.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    ui.skeleton = skeleton
    # The FBX is in centimetres already (make_reel.py sets Blender's unit to it).
    ui.skeletal_mesh_import_data.convert_scene_unit = False
    ui.skeletal_mesh_import_data.import_mesh_lo_ds = False
    return ui


if assets.does_directory_exist(FOLDER):
    assets.delete_directory(FOLDER)
skeleton = None
number = 1
while os.path.exists(os.path.join(SOURCE, f"{NAME}_{number}.fbx")):
    job = unreal.AssetImportTask()
    job.filename = os.path.join(SOURCE, f"{NAME}_{number}.fbx")
    job.destination_path = FOLDER
    job.automated = True
    job.replace_existing = True
    job.save = True
    job.options = options(skeleton)
    tools.import_asset_tasks([job])
    mesh = unreal.load_asset(f"{FOLDER}/{NAME}_{number}")
    if mesh is None:
        raise RuntimeError(f"Unreal imported nothing from {job.filename}")
    if skeleton is None:
        skeleton = mesh.skeleton
    elif mesh.skeleton != skeleton:
        raise RuntimeError(f"{job.filename} came in on a skeleton of its own: its bones do not continue the first mesh's")
    unreal.log(f"{NAME}_{number} on {skeleton.get_path_name()}")
    number += 1
if skeleton is None:
    raise RuntimeError(f"No {NAME}_1.fbx in {SOURCE}")
made = skeleton.get_path_name().split(".")[0]
if not assets.rename_asset(made, f"{FOLDER}/{NAME}"):
    raise RuntimeError(f"Could not rename {made} to {FOLDER}/{NAME}")

# The game's virtual bones, in the game's order: the cook bakes a track for each into a sequence, as the game's own
# sequences have them. Unreal's Python adds one only through a sequence of the skeleton, and names it after the two
# bones it joins. They are added here and not beside the animation, because a change to the skeleton gives it a new
# guid, and import_reel.py needs the guid it writes to stay.
factory = unreal.AnimSequenceFactory()
factory.set_editor_property("target_skeleton", skeleton)
scratch = tools.create_asset("A_VirtualBones", FOLDER, unreal.AnimSequence, factory)
for source, target, name in SKELETON["virtual"]:
    added = unreal.AnimationLibrary.add_virtual_bone(scratch, source, target)
    if str(added) != name:
        raise RuntimeError(f"The virtual bone from {source} to {target} is named {added}, the game's is {name}")

# The game's sockets, by name and bone: the codecs that remove keys hold a bone with a socket to a tighter error. The
# reel's codec (the per-track one without key removal) does not look at them, so today they change nothing in the
# cooked sequence: they are here so that the stand-in is the game's skeleton in everything a cook can read. Python holds
# a socket's two names read-only, the console's `set` on the one object does not. Where a socket sits on its bone is
# left out.
for number, (socket_name, bone_name) in enumerate(SKELETON["sockets"]):
    socket = unreal.new_object(unreal.SkeletalMeshSocket, outer=mesh, name=f"CyrodiilRPStandIn_{number}")
    unreal.SystemLibrary.execute_console_command(None, f"set CyrodiilRPStandIn_{number} SocketName {socket_name}")
    unreal.SystemLibrary.execute_console_command(None, f"set CyrodiilRPStandIn_{number} BoneName {bone_name}")
    if (str(socket.get_editor_property("socket_name")), str(socket.get_editor_property("bone_name"))) != (socket_name, bone_name):
        raise RuntimeError(f"The socket {socket_name} on {bone_name} did not take its names")
    mesh.add_socket(socket, True)
assets.save_directory(FOLDER, only_if_is_dirty=False, recursive=True)
assets.delete_asset(f"{FOLDER}/A_VirtualBones")
# The rename leaves a redirector under the old name.
if assets.does_asset_exist(made):
    assets.delete_asset(made)
unreal.log("CyrodiilRP stand-in skeleton imported")
