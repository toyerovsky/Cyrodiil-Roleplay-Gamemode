# Draws an animation sequence as the research repo's animation catalogue draws the game's: the keys UeExport decoded
# (its `anims` command: sequences.tsv, <package>.anim.bin, the game's SK_HumanoidFull as glTF) on the clay figure, with
# a fishing rod on Weapon_Socket where the client hangs it. It takes the game's own sequences and the mode's cooked one
# alike, so what is looked at is what the game would sample. Writes contact sheets of FRAMES poses spread over the loop:
#   OUT_DIR/<name>_full.png    the whole figure from its front left
#   OUT_DIR/<name>_front.png   the reel from the front
#   OUT_DIR/<name>_side.png    the reel from the figure's left
#   OUT_DIR/<name>_knob.png    the reel's face with the handle, from in front of the belly (the fight pose turns that
#                              face toward the body)
# usage: blender -b --factory-startup -P preview_reel.py -- CATALOG_TOOLS UE_DIR SEQUENCE ROD_FBX OUT_DIR
#            [--figure FIGURE_DIR] [--frames 8] [--size 640] [--samples 24]
#   CATALOG_TOOLS  tools/catalog of the research repo (render_animations.py and render_props.py are used as they are)
#   UE_DIR         what `UeExport anims` wrote, or make_reel.py's `authored` folder
#   FIGURE_DIR     where `UeExport anims` wrote the figure (mesh.tsv, skeleton.tsv, the glTF), when UE_DIR has none
#   SEQUENCE       the object path as sequences.tsv has it
#   ROD_FBX        a rod export_rods.py made, its colour texture beside it
import csv
import math
import os
import sys

import bpy
import numpy
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
NAMED = {"frames": "8", "size": "640", "samples": "24"}
PLACED = []
while ARGS:
    value = ARGS.pop(0)
    if value.startswith("--"):
        NAMED[value[2:]] = ARGS.pop(0)
    else:
        PLACED.append(value)
CATALOG_TOOLS, UE_DIR, SEQUENCE, ROD_FBX, OUT = PLACED
UE_DIR, OUT = os.path.abspath(UE_DIR), os.path.abspath(OUT)
FIGURE = os.path.abspath(NAMED.get("figure", UE_DIR))
FRAMES, SIZE, SAMPLES = int(NAMED["frames"]), int(NAMED["size"]), int(NAMED["samples"])
os.makedirs(OUT, exist_ok=True)

sys.path.insert(0, os.path.abspath(CATALOG_TOOLS))
import render_animations as catalog
import render_props as props

# Where the client hangs the rod (AnglerLooks.HandSocket, RodOffset, RodRotation): the socket's bone, centimetres,
# then pitch, yaw and roll in degrees.
SOCKET = {"bone": "Weapon_Anchor", "offset": [0.0, -10.0, 0.0], "rotation": [180.0, 0.0, 0.0]}
# The middle of the reel on the rod's mesh in Unreal's centimetres (make_reel.py has the same), which the close views
# look at.
REEL = (0.0, 24.0, -7.0)
# Each view: the camera's place from what it looks at, in Blender's metres (+X the figure's left, -Y its front), and
# its lens. The whole figure is looked at from the chest.
VIEWS = {"full": ((1.5, -3.0, 0.5), 50, None), "front": ((0.2, -0.85, 0.2), 50, REEL), "side": ((0.8, -0.15, 0.1), 50, REEL),
         "knob": ((-0.11, 0.30, -0.21), 24, REEL)}
CHEST = Vector((-0.2, -0.2, 1.25))


def sequence_row():
    with open(os.path.join(UE_DIR, "sequences.tsv"), encoding="utf-8") as listing:
        rows = [row for row in csv.DictReader(listing, delimiter="\t") if row["sequence"] == SEQUENCE and not row["error"]]
    if not rows:
        raise SystemExit(f"{SEQUENCE} is not decoded in {UE_DIR}/sequences.tsv")
    return rows[-1]


def read_rod(look):
    bpy.ops.import_scene.fbx(filepath=ROD_FBX)
    rod = next(obj for obj in bpy.context.selected_objects if obj.type == "MESH")
    rod.data.transform(rod.matrix_world)
    rod.matrix_world = Matrix.Identity(4)
    rod.data.materials.clear()
    rod.data.materials.append(look)
    return rod


def rod_look():
    made = bpy.data.materials.new("Rod")
    made.use_nodes = True
    bsdf = made.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.5
    grade = os.path.basename(ROD_FBX).split("_")[2].split(".")[0]
    texture = os.path.join(os.path.dirname(ROD_FBX), f"T_FishingRod_{grade}_BaseColor.png")
    if os.path.exists(texture):
        image = made.node_tree.nodes.new("ShaderNodeTexImage")
        image.image = bpy.data.images.load(texture)
        made.node_tree.links.new(image.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (0.3, 0.2, 0.12, 1.0)
    return made


def look_from(camera, target, offset, lens):
    camera.data.lens = lens
    camera.location = target + Vector(offset)
    camera.rotation_mode = "QUATERNION"
    camera.rotation_quaternion = (target - camera.location).to_track_quat("-Z", "Y")
    camera.data.clip_start, camera.data.clip_end = 0.02, 50.0


def sheet(paths, target):
    # The frames side by side, in rows of four, left to right and top to bottom.
    columns = min(4, len(paths))
    rows = math.ceil(len(paths) / columns)
    whole = numpy.zeros((rows * SIZE, columns * SIZE, 4), dtype=numpy.float32)
    whole[..., 3] = 1.0
    for place, path in enumerate(paths):
        image = bpy.data.images.load(path, check_existing=False)
        pixels = numpy.empty(SIZE * SIZE * 4, dtype=numpy.float32)
        image.pixels.foreach_get(pixels)
        bpy.data.images.remove(image)
        row, column = divmod(place, columns)
        top = (rows - 1 - row) * SIZE
        whole[top:top + SIZE, column * SIZE:(column + 1) * SIZE] = pixels.reshape(SIZE, SIZE, 4)
    made = bpy.data.images.new("sheet", columns * SIZE, rows * SIZE, alpha=False)
    made.pixels.foreach_set(whole.ravel())
    made.filepath_raw = target
    made.file_format = "PNG"
    made.save()
    bpy.data.images.remove(made)


row = sequence_row()
name = SEQUENCE.split(".")[-1]
length, keys = float(row["length"]), int(row["keys"])
scene, camera, floor, device = props.setup(SIZE, SAMPLES)
scene.render.film_transparent = False
scene.render.image_settings.color_mode = "RGB"
scene.render.fps = catalog.FPS
floor.scale = (60, 60, 1)
floor.is_shadow_catcher = False
floor.data.materials.append(catalog.material("Floor", (0.33, 0.35, 0.38), 0.9))
scene.objects["Sun"].rotation_euler = (math.radians(40), 0, math.radians(-25))

with open(os.path.join(FIGURE, "mesh.tsv"), encoding="utf-8") as listing:
    mesh_row = next(csv.DictReader(listing, delimiter="\t"))
armature, figure = catalog.import_gltf(os.path.join(FIGURE, mesh_row["gltf"]))
catalog.paint(figure, catalog.material("Clay", catalog.CLAY, 0.6))
rig = catalog.Rig(FIGURE, armature)
# The sequence's keys lie in UE_DIR, which the rig reads them from.
rig.ue_dir = UE_DIR
segment = {"anim": row["anim"], "keys": keys, "length": length, "bones": row["bones"].split(","), "start": 0.0, "animStart": 0.0,
           "animEnd": length, "rate": 1.0, "loops": 1}
# The loop's own keys, one a frame, its last the same pose as its first.
count = int(round(length * catalog.FPS)) + 1
local = rig.timeline([segment], count)
world = rig.world(local)
rig.keyframe(rig.pose_matrices(world))
rod = read_rod(rod_look())
hung = catalog.socket_matrices(world, rig, SOCKET)
catalog.animate_object(rod, hung)
scene.frame_start, scene.frame_end = 0, count - 1

shown = [int(round(i * (count - 1) / FRAMES)) for i in range(FRAMES)]
print(f"{name}: {keys} keys, {length} s, frames {shown} on {device}")
for view, (offset, lens, looked_at) in VIEWS.items():
    paths = []
    for frame in shown:
        scene.frame_set(frame)
        if looked_at is None:
            target = CHEST
        else:
            # The reel of this frame, so the view follows the rod's sway.
            at = hung[frame] @ numpy.array([looked_at[0] * 0.01, -looked_at[1] * 0.01, looked_at[2] * 0.01, 1.0])
            target = Vector(at[:3].tolist())
        look_from(camera, target, offset, lens)
        path = os.path.join(OUT, "frames", f"{name}_{view}_{frame:04d}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    sheet(paths, os.path.join(OUT, f"{name}_{view}.png"))
    print(f"{name}_{view}.png")
