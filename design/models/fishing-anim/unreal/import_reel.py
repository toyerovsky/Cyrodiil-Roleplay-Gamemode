# Runs inside Unreal Editor 5.3 (the PythonScript commandlet), second of two, after import_skeleton.py: makes
# /Game/CyrodiilRP/Fishing/A_Fishing_Reel from the keys make_reel.py wrote, on the stand-in for the game's skeleton, and
# labels /Game/CyrodiilRP for chunk 1, so the cook puts the mode's animation into a container of its own.
# usage: UnrealEditor-Cmd.exe OblivionRemastered.uproject -run=pythonscript -script="import_reel.py SOURCE_DIR"
#
# What a cooked sequence carries of its skeleton, and so what the stand-in must share with the game's:
# - the guid: an animation saves its skeleton's and holds the two to each other when it loads. Unreal's Python cannot
#   set it, so it is written into the stand-in's file before the editor loads it (give_guid);
# - the bones' order: a track names its bone by index (same_bones);
# - the virtual bones: the cook bakes a track for each, as the game's own sequences have them (import_skeleton.py
#   added them: nothing here changes the skeleton, which would give it a new guid).
# The keys go in through the animation data controller, bone by bone as make_reel.py lists them (the base's order),
# so no FBX, with its axes and units, lies between the keys written and the keys cooked.
import json
import os
import struct
import sys

import unreal

SOURCE = os.path.abspath(sys.argv[1])
with open(os.path.join(SOURCE, "A_Fishing_Reel.json")) as source:
    MADE = json.load(source)
SKELETON = MADE["skeleton"]
ROOT = "/Game/CyrodiilRP"
FISHING = ROOT + "/Fishing"
LABEL = "PAL_CyrodiilRPAnim"
CHUNK = 1
ENGINE_SETTINGS = "/Engine/Animation/"
# Centimetres and quaternion parts a key read back from the sequence may differ by from the key written.
NEAR_CM, NEAR_TURN = 0.001, 0.0001

tools = unreal.AssetToolsHelpers.get_asset_tools()
assets = unreal.EditorAssetLibrary
library = unreal.AnimationLibrary
poses = unreal.AnimPoseExtensions


# The guid sits in the skeleton's file after the bones' name-to-index map (a count, then a name and an index a bone,
# the indices counting up) and the empty map of retarget sources. Anything else found there is refused.
def give_guid():
    path = os.path.join(unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_content_dir()),
                        SKELETON["path"][len("/Game/"):] + ".uasset")
    with open(path, "rb") as stored:
        data = bytearray(stored.read())
    count = len(SKELETON["bones"])
    wanted = struct.pack("<4I", *(int(SKELETON["guid"][i:i + 8], 16) for i in range(0, 32, 8)))
    maps = []
    at = data.find(struct.pack("<i", count))
    while at != -1:
        end = at + 4 + 12 * count
        if end + 20 <= len(data) and all(struct.unpack_from("<i", data, at + 4 + 12 * i + 8)[0] == i for i in range(count)):
            maps.append(end)
        at = data.find(struct.pack("<i", count), at + 1)
    if len(maps) != 1 or struct.unpack_from("<i", data, maps[0])[0] != 0:
        raise RuntimeError(f"{path} is not laid out as a skeleton of {count} bones without retarget sources: its guid was not found")
    start = maps[0] + 4
    had = bytes(data[start:start + 16])
    if had == wanted:
        unreal.log("The stand-in skeleton has the game's guid already")
        return
    if data.count(had) != 1:
        raise RuntimeError(f"{path} holds the bytes of its guid more than once")
    data[start:start + 16] = wanted
    with open(path, "wb") as stored:
        stored.write(data)
    unreal.log(f"The stand-in skeleton's guid is now the game's, {SKELETON['guid']}")


# The stand-in's bones and virtual bones are the game's, one for one and in the game's order.
def same_bones(skeleton):
    names = [str(name) for name in poses.get_bone_names(skeleton.get_reference_pose())]
    wanted = [bone[0] for bone in SKELETON["bones"]] + [bone[2] for bone in SKELETON["virtual"]]
    if names != wanted:
        off = next((i for i, (a, b) in enumerate(zip(names, wanted)) if a != b), min(len(names), len(wanted)))
        raise RuntimeError(f"The stand-in skeleton has {len(names)} bones, the game's {len(wanted)}, and they part at bone {off}: "
                           f"{names[off:off + 1]} where the game has {wanted[off:off + 1]}")


def fresh(path):
    if assets.does_asset_exist(path):
        assets.delete_asset(path)


def sequence_of(skeleton):
    name = MADE["name"]
    fresh(f"{FISHING}/{name}")
    factory = unreal.AnimSequenceFactory()
    factory.set_editor_property("target_skeleton", skeleton)
    sequence = tools.create_asset(name, FISHING, unreal.AnimSequence, factory)
    keys = MADE["keys"]
    controller = sequence.controller
    controller.open_bracket(unreal.Text("Reel keys"), False)
    controller.set_frame_rate(unreal.FrameRate(MADE["rate"], 1), False)
    controller.set_number_of_frames(unreal.FrameNumber(keys - 1), False)
    for track in MADE["tracks"]:
        if len(track["rotation"]) != keys:
            raise RuntimeError(f"{track['bone']} has {len(track['rotation'])} keys, not {keys}")
        # A quaternion that is not of unit length goes in as it is and comes out as another rotation.
        if any(abs(sum(part * part for part in key) - 1.0) > 1e-5 for key in track["rotation"]):
            raise RuntimeError(f"{track['bone']} has a rotation key that is not of unit length")
        if not controller.add_bone_curve(track["bone"], False):
            raise RuntimeError(f"The skeleton took no track for {track['bone']}")
        controller.set_bone_track_keys(track["bone"],
                                       [unreal.Vector(*key) for key in track["translation"]],
                                       [unreal.Quat(*key) for key in track["rotation"]],
                                       [unreal.Vector(*key) for key in track["scale"]], False)
    controller.close_bracket(False)
    return sequence


# The rate the cooked keys are sampled at. The game's sequences carry it as TargetFrameRate (30), which the game's
# engine writes at its cook and stock 5.3.2 leaves at the class's 60000. Python holds the property read-only, but the
# struct it hands out is the sequence's own.
def target_rate(sequence):
    rate = sequence.get_editor_property("target_frame_rate")
    rate.numerator, rate.denominator = MADE["rate"], 1
    kept = sequence.get_editor_property("target_frame_rate")
    if (kept.numerator, kept.denominator) != (MADE["rate"], 1):
        raise RuntimeError(f"The sequence's TargetFrameRate stayed {kept.numerator}/{kept.denominator}")


# The engine's own settings assets, which the game's humanoid sequences name and the game ships. A new sequence in a
# stock 5.3 project takes the ACL plugin's bone compression instead (/ACLPlugin/ACLAnimBoneCompressionSettings): the
# game has that plugin too, but in its own build, and none of the sequences the mode plays goes through it.
def compression(sequence):
    library.set_bone_compression_settings(sequence, unreal.load_asset(ENGINE_SETTINGS + "DefaultAnimBoneCompressionSettings"))
    library.set_curve_compression_settings(sequence, unreal.load_asset(ENGINE_SETTINGS + "DefaultAnimCurveCompressionSettings"))
    library.set_variable_frame_stripping_settings(sequence, unreal.load_asset(ENGINE_SETTINGS + "DefaultVariableFrameStrippingSettings"))
    named = [library.get_bone_compression_settings(sequence), library.get_curve_compression_settings(sequence),
             library.get_variable_frame_stripping_settings(sequence)]
    paths = [found.get_path_name() if found else "none" for found in named]
    unreal.log("Compression settings: " + ", ".join(paths))
    if any(not path.startswith(ENGINE_SETTINGS) for path in paths):
        raise RuntimeError("The sequence names compression settings outside " + ENGINE_SETTINGS)


# What the sequence gives back against what was written: every key of the left arm, the pelvis and the right hand, three
# keys of every other bone.
def read_back(sequence):
    keys = MADE["keys"]
    worst_cm = worst_turn = 0.0
    for track in MADE["tracks"]:
        for key in range(keys) if track["bone"] in MADE["changed"] + ["pelvis", "hand_r"] else (0, keys // 3, keys - 1):
            pose = library.get_bone_pose_for_frame(sequence, track["bone"], key, False)
            worst_cm = max(worst_cm, max(abs(a - b) for a, b in zip((pose.translation.x, pose.translation.y, pose.translation.z), track["translation"][key])))
            found = (pose.rotation.x, pose.rotation.y, pose.rotation.z, pose.rotation.w)
            wanted = track["rotation"][key]
            sign = 1.0 if sum(a * b for a, b in zip(found, wanted)) >= 0 else -1.0
            worst_turn = max(worst_turn, max(abs(a - b * sign) for a, b in zip(found, wanted)))
    unreal.log(f"Read back: translations within {worst_cm:.6f} cm, rotations within {worst_turn:.7f}")
    if worst_cm > NEAR_CM or worst_turn > NEAR_TURN:
        raise RuntimeError("The sequence does not give back the keys it was given")


# Chunk 1 takes /Game/CyrodiilRP and nothing it refers to, so the stand-in skeleton stays in chunk 0.
def chunk_label():
    fresh(f"{ROOT}/{LABEL}")
    factory = unreal.DataAssetFactory()
    factory.set_editor_property("data_asset_class", unreal.PrimaryAssetLabel)
    label = tools.create_asset(LABEL, ROOT, unreal.PrimaryAssetLabel, factory)
    rules = label.get_editor_property("rules")
    rules.set_editor_property("chunk_id", CHUNK)
    rules.set_editor_property("cook_rule", unreal.PrimaryAssetCookRule.ALWAYS_COOK)
    rules.set_editor_property("apply_recursively", False)
    label.set_editor_property("rules", rules)
    label.set_editor_property("label_assets_in_my_directory", True)
    assets.save_loaded_asset(label)


give_guid()
skeleton = unreal.load_asset(SKELETON["path"])
if skeleton is None:
    raise RuntimeError(f"No {SKELETON['path']}: run import_skeleton.py first")
same_bones(skeleton)
sequence = sequence_of(skeleton)
# As the base, A_Humanoid_Staff_Block_Idle: root motion on, the root locked at zero.
library.set_root_motion_enabled(sequence, True)
library.set_root_motion_lock_type(sequence, unreal.RootMotionRootLock.ZERO)
compression(sequence)
target_rate(sequence)
read_back(sequence)
unreal.log(f"{MADE['name']}: {library.get_num_frames(sequence)} frames, {library.get_sequence_length(sequence):.4f} s, "
           f"{len(library.get_animation_track_names(sequence))} tracks")
assets.save_loaded_asset(sequence)
chunk_label()
assets.save_directory(ROOT, only_if_is_dirty=False, recursive=True)
unreal.log("CyrodiilRP reel animation imported")
