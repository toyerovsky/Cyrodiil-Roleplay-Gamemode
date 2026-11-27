# Runs inside Unreal Editor 5.3 (the PythonScript commandlet): imports the rods export_rods.py made into
# /Game/CyrodiilRP/Fishing, each with its two textures and a material instance of the game's own base material, which
# the rod's bent meshes take too, and labels /Game/CyrodiilRP for chunk 1, so the cook puts the mode's assets into a
# container of their own.
# usage: UnrealEditor-Cmd.exe OblivionRemastered.uproject -run=pythonscript -script="import_rods.py SOURCE_DIR"
#
# The game runs Unreal 5.3.2 changed by its makers: a material cooked by the stock engine crashes it on load ("Bad
# export index"). So the rods own no material. Their instances take the base material the game's staves use, which
# this project stands in for with a stand-in at the same path, cooked only into chunk 0, which is never shipped: in the
# game the reference finds the game's own.
import json
import os
import sys

import unreal

SOURCE = os.path.abspath(sys.argv[1])
# Every mesh export_rods.py made, straight and bent, with its grade and its box as Unreal should have it.
with open(os.path.join(SOURCE, "rods.json")) as listing:
    MESHES = json.load(listing)
ROOT = "/Game/CyrodiilRP"
FISHING = ROOT + "/Fishing"
GRADES = ("Basic", "Sturdy", "Master")
CHUNK = 1
# The rods are about 2.7 to 3.1 m long: anything else means the FBX came in at the wrong scale.
SHORTEST_CM, LONGEST_CM = 250.0, 330.0
# Centimetres a mesh's box may differ by from the one rods.json lists.
BOX_NEAR_CM = 0.05

# The game's base material for held things and its parameters, as the staves' MIC_Staff01 names them.
GAME_MATERIALS = "/Game/Materials/Template"
GAME_BASE = "MI_T_BaseMaterial_FPSClippingFix"
STAND_IN_PARENT = "M_CyrodiilRP_StandIn"
COLOR_MAP = "BaseColor Map"
NNRM_MAP = "NNRM Map"

tools = unreal.AssetToolsHelpers.get_asset_tools()
materials = unreal.MaterialEditingLibrary
assets = unreal.EditorAssetLibrary


def task(filename, destination, options=None):
    job = unreal.AssetImportTask()
    job.filename = filename
    job.destination_path = destination
    job.automated = True
    job.replace_existing = True
    job.save = True
    if options is not None:
        job.options = options
    return job


def mesh_options():
    ui = unreal.FbxImportUI()
    ui.import_mesh = True
    ui.import_as_skeletal = False
    ui.import_materials = False
    ui.import_textures = False
    ui.import_animations = False
    ui.mesh_type_to_import = unreal.FBXImportType.FBXIT_STATIC_MESH
    data = ui.static_mesh_import_data
    data.combine_meshes = True
    data.generate_lightmap_u_vs = False
    data.auto_generate_collision = False
    data.normal_import_method = unreal.FBXNormalImportMethod.FBXNIM_IMPORT_NORMALS_AND_TANGENTS
    # Blender writes metres with the FBX's unit saying so: without the conversion a 2.7 m rod comes in 2.7 cm long.
    data.convert_scene_unit = True
    return ui


def import_all():
    jobs = [task(os.path.join(SOURCE, f"{name}.fbx"), FISHING, mesh_options()) for name in MESHES]
    for grade in GRADES:
        for kind in ("BaseColor", "NNRM"):
            jobs.append(task(os.path.join(SOURCE, f"T_FishingRod_{grade}_{kind}.png"), FISHING))
    tools.import_asset_tasks(jobs)
    for job in jobs:
        if not job.imported_object_paths:
            raise RuntimeError(f"Unreal imported nothing from {job.filename}")


def texture(grade, kind):
    found = unreal.load_asset(f"{FISHING}/T_FishingRod_{grade}_{kind}")
    if kind == "BaseColor":
        found.set_editor_property("srgb", True)
        found.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_DEFAULT)
    elif kind == "NNRM":
        found.set_editor_property("srgb", False)
        found.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_BC7)
    else:
        raise ValueError(kind)
    assets.save_loaded_asset(found)
    return found


def fresh(path):
    if assets.does_asset_exist(path):
        assets.delete_asset(path)


# A material with the game's parameter names, and an instance of it at the game's base material's path.
def stand_in():
    fresh(f"{GAME_MATERIALS}/{GAME_BASE}")
    fresh(f"{GAME_MATERIALS}/{STAND_IN_PARENT}")
    parent = tools.create_asset(STAND_IN_PARENT, GAME_MATERIALS, unreal.Material, unreal.MaterialFactoryNew())
    color = materials.create_material_expression(parent, unreal.MaterialExpressionTextureSampleParameter2D, -500, -200)
    color.set_editor_property("parameter_name", COLOR_MAP)
    color.set_editor_property("sampler_type", unreal.MaterialSamplerType.SAMPLERTYPE_COLOR)
    color.set_editor_property("texture", texture("Basic", "BaseColor"))
    nnrm = materials.create_material_expression(parent, unreal.MaterialExpressionTextureSampleParameter2D, -500, 200)
    nnrm.set_editor_property("parameter_name", NNRM_MAP)
    nnrm.set_editor_property("sampler_type", unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_COLOR)
    nnrm.set_editor_property("texture", texture("Basic", "NNRM"))
    materials.connect_material_property(color, "RGB", unreal.MaterialProperty.MP_BASE_COLOR)
    materials.connect_material_property(nnrm, "B", unreal.MaterialProperty.MP_ROUGHNESS)
    materials.recompile_material(parent)
    assets.save_loaded_asset(parent)
    base = tools.create_asset(GAME_BASE, GAME_MATERIALS, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    materials.set_material_instance_parent(base, parent)
    assets.save_loaded_asset(base)
    return base


def instance(grade, parent):
    name = f"MI_FishingRod_{grade}"
    fresh(f"{FISHING}/{name}")
    made = tools.create_asset(name, FISHING, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    materials.set_material_instance_parent(made, parent)
    materials.set_material_instance_texture_parameter_value(made, COLOR_MAP, texture(grade, "BaseColor"))
    materials.set_material_instance_texture_parameter_value(made, NNRM_MAP, texture(grade, "NNRM"))
    materials.update_material_instance(made)
    assets.save_loaded_asset(made)
    return made


def dress(name, listed, made):
    mesh = unreal.load_asset(f"{FISHING}/{name}")
    mesh.set_material(0, made)
    bounds = mesh.get_bounds()
    length = 2 * max(bounds.box_extent.x, bounds.box_extent.y, bounds.box_extent.z)
    unreal.log(f"{name}: {length:.1f} cm long, box {bounds.box_extent}, origin {bounds.origin}")
    if listed["bend"] == 0 and not SHORTEST_CM <= length <= LONGEST_CM:
        raise RuntimeError(f"{name} came in {length:.1f} cm long, not {SHORTEST_CM} to {LONGEST_CM}: the FBX scale is off")
    low = [bounds.origin.x - bounds.box_extent.x, bounds.origin.y - bounds.box_extent.y, bounds.origin.z - bounds.box_extent.z]
    high = [bounds.origin.x + bounds.box_extent.x, bounds.origin.y + bounds.box_extent.y, bounds.origin.z + bounds.box_extent.z]
    off = max(abs(found - wanted) for found, wanted in zip(low + high, listed["box_min"] + listed["box_max"]))
    if off > BOX_NEAR_CM:
        raise RuntimeError(f"{name} came in with the box {low} to {high}, {off:.3f} cm off the one rods.json lists")
    assets.save_loaded_asset(mesh)


# Chunk 1 takes /Game/CyrodiilRP and nothing it refers to, so the stand-in stays in chunk 0.
def chunk_label():
    path = f"{ROOT}/PAL_CyrodiilRP"
    fresh(path)
    factory = unreal.DataAssetFactory()
    factory.set_editor_property("data_asset_class", unreal.PrimaryAssetLabel)
    label = tools.create_asset("PAL_CyrodiilRP", ROOT, unreal.PrimaryAssetLabel, factory)
    rules = label.get_editor_property("rules")
    rules.set_editor_property("chunk_id", CHUNK)
    rules.set_editor_property("cook_rule", unreal.PrimaryAssetCookRule.ALWAYS_COOK)
    rules.set_editor_property("apply_recursively", False)
    label.set_editor_property("rules", rules)
    label.set_editor_property("label_assets_in_my_directory", True)
    assets.save_loaded_asset(label)


import_all()
parent = stand_in()
for grade in GRADES:
    made = instance(grade, parent)
    for name, listed in MESHES.items():
        if listed["grade"] == grade:
            dress(name, listed, made)
chunk_label()
assets.save_directory(ROOT, only_if_is_dirty=False, recursive=True)
assets.save_directory(GAME_MATERIALS, only_if_is_dirty=False, recursive=True)
unreal.log("CyrodiilRP rods imported")
