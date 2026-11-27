# Reads back what export_rods.py made, before Unreal takes it: imports every FBX of rods.json into an empty scene,
# measures it (the box and the tip ring against rods.json, a bent mesh against its straight rod: the same UVs, the held
# end untouched, the tip turned by the bend's angle), prints the numbers as Unreal has them and renders previews with
# only the baked textures on.
# usage: blender -b --factory-startup -P check_export.py -- EXPORT_DIR OUT_DIR [--no-render]
import json
import math
import os
import sys

import bpy
import bmesh
import numpy
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
SOURCE, OUT = os.path.abspath(ARGS[0]), os.path.abspath(ARGS[1])
RENDER = "--no-render" not in ARGS
os.makedirs(OUT, exist_ok=True)
# Centimetres and degrees a measurement may be off by.
NEAR_CM, NEAR_DEGREES = 0.01, 0.2

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
with open(os.path.join(SOURCE, "rods.json")) as listing:
    MANIFEST = json.load(listing)


def read(name):
    bpy.ops.import_scene.fbx(filepath=os.path.join(SOURCE, f"{name}.fbx"))
    rod = next(obj for obj in bpy.context.selected_objects if obj.type == "MESH")
    rod.data.transform(rod.matrix_world)
    rod.matrix_world = Matrix.Identity(4)
    return rod


def points(rod):
    flat = numpy.empty(len(rod.data.vertices) * 3)
    rod.data.vertices.foreach_get("co", flat)
    return flat.reshape(-1, 3)


def uvs(rod):
    flat = numpy.empty(len(rod.data.loops) * 2)
    rod.data.uv_layers.active.data.foreach_get("uv", flat)
    return flat.reshape(-1, 2)


def unreal(at):
    return at * numpy.array([100.0, -100.0, 100.0])


# The tip ring is the piece of the mesh whose middle lies farthest from the hand: its vertices.
def tip_ring(rod):
    mesh = bmesh.new()
    mesh.from_mesh(rod.data)
    left, farthest = set(mesh.verts), []
    while left:
        piece, edge = [], [left.pop()]
        while edge:
            vertex = edge.pop()
            piece.append(vertex)
            for other in (e.other_vert(vertex) for e in vertex.link_edges):
                if other in left:
                    left.remove(other)
                    edge.append(other)
        middle = sum((v.co for v in piece), Vector()) / len(piece)
        if not farthest or middle.length > farthest[0]:
            farthest = [middle.length, numpy.array([v.co for v in piece])]
    mesh.free()
    return farthest[1]


# The way a ring faces: the direction its vertices spread least in.
def facing(ring):
    values, vectors = numpy.linalg.eigh(numpy.cov((ring - ring.mean(axis=0)).T))
    return vectors[:, 0]


def close(found, wanted, what, name):
    off = numpy.abs(numpy.asarray(found) - numpy.asarray(wanted)).max()
    if off > NEAR_CM:
        raise RuntimeError(f"{name}: {what} is {found}, rods.json says {wanted}")


def check(name, rod, straight):
    listed = MANIFEST[name]
    at = unreal(points(rod))
    low, high = at.min(axis=0), at.max(axis=0)
    ring = tip_ring(rod)
    centre = unreal(ring.mean(axis=0))
    close(low, listed["box_min"], "box min", name)
    close(high, listed["box_max"], "box max", name)
    close(centre, listed["tip_ring"], "tip ring", name)
    print(f"MESH {name} tip ring ({centre[0]:.3f}, {centre[1]:.3f}, {centre[2]:.3f}) cm, {len(ring)} vertices,"
          f" box min ({low[0]:.3f}, {low[1]:.3f}, {low[2]:.3f}) max ({high[0]:.3f}, {high[1]:.3f}, {high[2]:.3f})")
    if straight is None:
        return
    was = points(straight)
    if was.shape != at.shape or uvs(straight).shape != uvs(rod).shape:
        raise RuntimeError(f"{name} is not its straight rod's mesh")
    if numpy.abs(uvs(straight) - uvs(rod)).max() > 1e-6:
        raise RuntimeError(f"{name} does not keep its straight rod's UVs")
    held = was[:, 1] * 100 <= listed["bend_from"] + 1e-4
    moved = numpy.abs(points(rod)[held] - was[held]).max()
    if moved > 1e-6:
        raise RuntimeError(f"{name} moves its held end by {moved} m")
    turn = math.degrees(math.acos(min(1.0, abs(float(numpy.dot(facing(ring), facing(tip_ring(straight))))))))
    if abs(turn - listed["bend"]) > NEAR_DEGREES:
        raise RuntimeError(f"{name} turns its tip by {turn:.2f} degrees, not {listed['bend']}")
    print(f"BENT {name} tip turned {turn:.2f} degrees, {int(held.sum())} of {len(held)} vertices held as the straight rod's")


rods = {}
for name, listed in MANIFEST.items():
    rods[name] = read(name)
    check(name, rods[name], None if listed["bend"] == 0 else rods[f"SM_FishingRod_{listed['grade']}"])
print("CHECKED", len(rods), "meshes")
if not RENDER:
    sys.exit(0)


# ---------------------------------------------------------------- previews

# The baked textures as the game's base material reads them: colour, and NNRM with the normal's green down.
def baked(grade):
    material = bpy.data.materials.new(f"MI_FishingRod_{grade}")
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    bsdf = nodes["Principled BSDF"]
    color = nodes.new("ShaderNodeTexImage")
    color.image = bpy.data.images.load(os.path.join(SOURCE, f"T_FishingRod_{grade}_BaseColor.png"))
    nnrm = nodes.new("ShaderNodeTexImage")
    nnrm.image = bpy.data.images.load(os.path.join(SOURCE, f"T_FishingRod_{grade}_NNRM.png"))
    nnrm.image.colorspace_settings.name = "Non-Color"
    nnrm.image.alpha_mode = "CHANNEL_PACKED"
    split = nodes.new("ShaderNodeSeparateColor")
    up = nodes.new("ShaderNodeMath")
    up.operation = "SUBTRACT"
    up.inputs[0].default_value = 1.0
    join = nodes.new("ShaderNodeCombineColor")
    join.inputs["Blue"].default_value = 1.0
    normal = nodes.new("ShaderNodeNormalMap")
    links.new(color.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(nnrm.outputs["Color"], split.inputs["Color"])
    links.new(split.outputs["Green"], up.inputs[1])
    links.new(split.outputs["Red"], join.inputs["Red"])
    links.new(up.outputs[0], join.inputs["Green"])
    links.new(join.outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
    links.new(split.outputs["Blue"], bsdf.inputs["Roughness"])
    links.new(nnrm.outputs["Alpha"], bsdf.inputs["Metallic"])
    return material


def setup_render(width, height, samples):
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for kind in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
        except TypeError:
            continue
        if any(d.type == kind for d in prefs.devices):
            for d in prefs.devices:
                d.use = d.type == kind
            scene.cycles.device = "GPU"
            break
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.view_settings.view_transform = "AgX"


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


# A sun's strength is its irradiance in W/m2: daylight's 2 to 5.
def sun(name, toward, strength):
    light = bpy.data.lights.new(name, "SUN")
    light.energy = strength
    obj = bpy.data.objects.new(name, light)
    scene.collection.objects.link(obj)
    aim(obj, toward)


def camera(at, target, lens=None, ortho=None):
    data = bpy.data.cameras.new("view")
    if ortho:
        data.type = "ORTHO"
        data.ortho_scale = ortho
    else:
        data.lens = lens
    data.clip_start = 0.01
    obj = bpy.data.objects.new("view", data)
    scene.collection.objects.link(obj)
    obj.location = at
    aim(obj, target)
    scene.camera = obj


def render(name):
    scene.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)


def show(shown):
    for rod in rods.values():
        rod.hide_render = rod not in shown


# The middle of the stretch of a rod that lies between two places along its straight rod.
def stretch(rod, straight, start, end):
    along = points(straight)[:, 1]
    return Vector(points(rod)[(along >= start) & (along <= end)].mean(axis=0))


world = bpy.data.worlds.new("World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.25, 0.26, 0.28, 1)
scene.world = world
sun("key", (-1.0, 0.3, -0.8), 3.0)
sun("fill", (-0.6, -1.0, 0.4), 0.8)
bpy.ops.mesh.primitive_plane_add(size=40, location=(-1.2, 1.0, 0.0), rotation=(0, math.pi / 2, 0))
wall = bpy.context.active_object
wall.data.materials.append(bpy.data.materials.new("Wall"))
wall.data.materials[0].diffuse_color = (0.16, 0.17, 0.19, 1)

for grade in sorted({listed["grade"] for listed in MANIFEST.values()}):
    material = baked(grade)
    family = [name for name, listed in MANIFEST.items() if listed["grade"] == grade]
    straight, most = rods[family[0]], rods[max(family, key=lambda name: MANIFEST[name]["bend"])]
    for depth, name in enumerate(family):
        rods[name].data.materials.clear()
        rods[name].data.materials.append(material)
        # One behind another, seen from the side: the held ends cover each other and no two surfaces share a place.
        rods[name].location = (-0.08 * depth, 0, 0)

    # The whole family from the side, the hand on the left, the guides and the bend downward.
    setup_render(2400, 900, 64)
    show([rods[name] for name in family])
    camera((6.0, 1.1, -0.35), (0.0, 1.1, -0.35), ortho=3.3)
    render(f"bend_{grade}.png")

    setup_render(1600, 1000, 64)
    tip = MANIFEST[family[0]]["tip_ring"][1] / -100
    views = [("grip", straight, -0.25, 0.55), ("grip", most, -0.25, 0.55), ("middle", most, 0.9, 1.6), ("tip", most, tip - 0.5, tip)]
    for part, rod, start, end in views:
        show([rod])
        target = stretch(rod, straight, start, end) + Vector(rod.location)
        camera(target + Vector((0.95, -0.25, 0.3)), target, lens=40)
        render(f"close_{rod.name}_{part}.png")
