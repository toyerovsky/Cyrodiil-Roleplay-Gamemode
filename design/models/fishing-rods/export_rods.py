# Bakes each rod of fishing_rods.blend (made by make_rods.py) into one texture set on a UV map of its own and exports
# it for Unreal: SM_FishingRod_<Grade>.fbx with one material slot, and the two textures the game's own base material
# takes: T_FishingRod_<Grade>_BaseColor.png (colour, darkened by the ambient occlusion) and _NNRM.png (the normal's X
# and Y as DirectX has them, green down, then roughness and metallic).
# usage: blender -b --factory-startup -P export_rods.py -- BLEND OUT_DIR [SIZE]
import math
import os
import sys

import bpy
import numpy

ARGS = sys.argv[sys.argv.index("--") + 1:]
BLEND, OUT = os.path.abspath(ARGS[0]), os.path.abspath(ARGS[1])
SIZE = int(ARGS[2]) if len(ARGS) > 2 else 2048
SHAPE_UV = "UVMap"
BAKE_UV = "Bake"
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=BLEND)
scene = bpy.context.scene
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
scene.render.bake.margin = 12
scene.render.bake.use_clear = True


def select_only(obj):
    for other in bpy.context.selected_objects:
        other.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def unwrap(rod):
    layer = rod.data.uv_layers.get(BAKE_UV) or rod.data.uv_layers.new(name=BAKE_UV)
    rod.data.uv_layers.active = layer
    layer.active_render = True
    select_only(rod)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode="OBJECT")


def target(rod, name, color):
    image = bpy.data.images.new(name, SIZE, SIZE, alpha=False, float_buffer=False)
    image.colorspace_settings.name = "sRGB" if color else "Non-Color"
    for material in rod.data.materials:
        nodes = material.node_tree.nodes
        node = nodes.get("BakeTarget") or nodes.new("ShaderNodeTexImage")
        node.name = "BakeTarget"
        node.image = image
        nodes.active = node
    return image


def bake(rod, kind, image, samples, **passes):
    select_only(rod)
    scene.cycles.samples = samples
    bake_settings = scene.render.bake
    if kind == "DIFFUSE":
        bake_settings.use_pass_direct = passes.get("direct", False)
        bake_settings.use_pass_indirect = passes.get("indirect", False)
        bake_settings.use_pass_color = passes.get("color", True)
    if kind == "NORMAL":
        bake_settings.normal_space = "TANGENT"
    bpy.ops.object.bake(type=kind)
    print("BAKED", rod.name, kind)
    return image


def principled(material):
    return next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")


# What feeds one input of each material's Principled BSDF, lit as an emission for the bake: Cycles bakes no metallic
# pass, and its diffuse colour pass is black for a metal.
def bake_input(rod, image, name):
    saved = []
    for material in rod.data.materials:
        nodes, links = material.node_tree.nodes, material.node_tree.links
        out = next(node for node in nodes if node.type == "OUTPUT_MATERIAL")
        surface = out.inputs["Surface"].links[0].from_socket
        source = principled(material).inputs[name]
        glow = nodes.new("ShaderNodeEmission")
        if source.links:
            links.new(source.links[0].from_socket, glow.inputs["Color"])
        elif source.type == "RGBA":
            glow.inputs["Color"].default_value = tuple(source.default_value)
        else:
            glow.inputs["Color"].default_value = (source.default_value, source.default_value, source.default_value, 1)
        links.new(glow.outputs["Emission"], out.inputs["Surface"])
        saved.append((material, out, surface, glow))
    bake(rod, "EMIT", image, 4)
    for material, out, surface, glow in saved:
        material.node_tree.links.new(surface, out.inputs["Surface"])
        material.node_tree.nodes.remove(glow)
    return image


def pixels(image):
    values = numpy.empty(SIZE * SIZE * 4, dtype=numpy.float32)
    image.pixels.foreach_get(values)
    return values.reshape(SIZE, SIZE, 4)


def save(image, path):
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    print("SAVED", path)


# The base material has no occlusion input: the colour carries it, at this share.
OCCLUSION_IN_COLOR = 0.6


def shade(color, occlusion):
    shaded = pixels(color).copy()
    darkening = 1.0 - OCCLUSION_IN_COLOR * (1.0 - pixels(occlusion)[:, :, :1])
    shaded[:, :, :3] *= darkening
    color.pixels.foreach_set(shaded.ravel())
    return color


def pack_nnrm(grade, normal, roughness, metallic):
    nnrm = bpy.data.images.new(f"T_FishingRod_{grade}_NNRM", SIZE, SIZE, alpha=True, float_buffer=False)
    nnrm.colorspace_settings.name = "Non-Color"
    packed = numpy.ones((SIZE, SIZE, 4), dtype=numpy.float32)
    baked = pixels(normal)
    packed[:, :, 0] = baked[:, :, 0]
    packed[:, :, 1] = 1.0 - baked[:, :, 1]
    packed[:, :, 2] = pixels(roughness)[:, :, 0]
    packed[:, :, 3] = pixels(metallic)[:, :, 0]
    nnrm.pixels.foreach_set(packed.ravel())
    return nnrm


def export(rod, grade):
    for layer in [layer for layer in rod.data.uv_layers if layer.name != BAKE_UV]:
        rod.data.uv_layers.remove(layer)
    rod.data.materials.clear()
    rod.data.materials.append(bpy.data.materials.new(f"MI_FishingRod_{grade}"))
    select_only(rod)
    path = os.path.join(OUT, f"{rod.name}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             use_tspace=True, use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
    print("SAVED", path)


rods = sorted((obj for obj in scene.objects if obj.type == "MESH" and obj.name.startswith("SM_FishingRod_")), key=lambda o: o.name)
for rod in rods:
    grade = rod.name.removeprefix("SM_FishingRod_")
    # All the rods stand at the origin: the others would shade this one's occlusion.
    for other in rods:
        other.hide_render = other is not rod
    unwrap(rod)
    color = bake_input(rod, target(rod, f"T_FishingRod_{grade}_BaseColor", True), "Base Color")
    normal = bake(rod, "NORMAL", target(rod, f"normal_{grade}", False), 8)
    roughness = bake(rod, "ROUGHNESS", target(rod, f"rough_{grade}", False), 1)
    metallic = bake_input(rod, target(rod, f"metal_{grade}", False), "Metallic")
    occlusion = bake(rod, "AO", target(rod, f"ao_{grade}", False), 64)
    save(shade(color, occlusion), os.path.join(OUT, f"T_FishingRod_{grade}_BaseColor.png"))
    save(pack_nnrm(grade, normal, roughness, metallic), os.path.join(OUT, f"T_FishingRod_{grade}_NNRM.png"))
for rod in rods:
    export(rod, rod.name.removeprefix("SM_FishingRod_"))
