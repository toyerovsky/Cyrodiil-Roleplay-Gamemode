# Runs inside Unreal Editor 5.3 (the PythonScript commandlet): imports the rods export_rods.py made into
# /Game/CyrodiilRP/Fishing, each with its three textures and a material instance of M_FishingRod, and labels
# /Game/CyrodiilRP for chunk 1, so the cook puts the mode's assets into a container of their own.
# usage: UnrealEditor-Cmd.exe OblivionRemastered.uproject -run=pythonscript -script="import_rods.py SOURCE_DIR"
import os
import sys

import unreal

SOURCE = os.path.abspath(sys.argv[1])
ROOT = "/Game/CyrodiilRP"
FISHING = ROOT + "/Fishing"
GRADES = ("Basic", "Sturdy", "Master")
CHUNK = 1
# The rods are about 2.7 to 3.1 m long: anything else means the FBX came in at the wrong scale.
SHORTEST_CM, LONGEST_CM = 250.0, 330.0

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
    jobs = []
    for grade in GRADES:
        jobs.append(task(os.path.join(SOURCE, f"SM_FishingRod_{grade}.fbx"), FISHING, mesh_options()))
        for kind in ("BaseColor", "Normal", "ORM"):
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
    elif kind == "Normal":
        # Blender bakes OpenGL normals, green up: Unreal's are DirectX.
        found.set_editor_property("srgb", False)
        found.set_editor_property("flip_green_channel", True)
        found.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_NORMALMAP)
    elif kind == "ORM":
        found.set_editor_property("srgb", False)
        found.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_MASKS)
    else:
        raise ValueError(kind)
    assets.save_loaded_asset(found)
    return found


def master_material():
    path = f"{FISHING}/M_FishingRod"
    if assets.does_asset_exist(path):
        assets.delete_asset(path)
    material = tools.create_asset("M_FishingRod", FISHING, unreal.Material, unreal.MaterialFactoryNew())

    def parameter(name, y, sampler, default):
        node = materials.create_material_expression(material, unreal.MaterialExpressionTextureSampleParameter2D, -500, y)
        node.set_editor_property("parameter_name", name)
        node.set_editor_property("sampler_type", sampler)
        node.set_editor_property("texture", default)
        return node

    color = parameter("BaseColor", -300, unreal.MaterialSamplerType.SAMPLERTYPE_COLOR, texture("Basic", "BaseColor"))
    orm = parameter("ORM", 0, unreal.MaterialSamplerType.SAMPLERTYPE_MASKS, texture("Basic", "ORM"))
    normal = parameter("Normal", 300, unreal.MaterialSamplerType.SAMPLERTYPE_NORMAL, texture("Basic", "Normal"))
    materials.connect_material_property(color, "RGB", unreal.MaterialProperty.MP_BASE_COLOR)
    materials.connect_material_property(orm, "R", unreal.MaterialProperty.MP_AMBIENT_OCCLUSION)
    materials.connect_material_property(orm, "G", unreal.MaterialProperty.MP_ROUGHNESS)
    materials.connect_material_property(orm, "B", unreal.MaterialProperty.MP_METALLIC)
    materials.connect_material_property(normal, "RGB", unreal.MaterialProperty.MP_NORMAL)
    materials.recompile_material(material)
    assets.save_loaded_asset(material)
    return material


def instance(grade, parent):
    name = f"MI_FishingRod_{grade}"
    path = f"{FISHING}/{name}"
    if assets.does_asset_exist(path):
        assets.delete_asset(path)
    made = tools.create_asset(name, FISHING, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    materials.set_material_instance_parent(made, parent)
    for kind in ("BaseColor", "ORM", "Normal"):
        materials.set_material_instance_texture_parameter_value(made, kind, texture(grade, kind))
    materials.update_material_instance(made)
    assets.save_loaded_asset(made)
    return made


def dress(grade, made):
    mesh = unreal.load_asset(f"{FISHING}/SM_FishingRod_{grade}")
    mesh.set_material(0, made)
    bounds = mesh.get_bounds()
    length = 2 * max(bounds.box_extent.x, bounds.box_extent.y, bounds.box_extent.z)
    unreal.log(f"SM_FishingRod_{grade}: {length:.1f} cm long, box {bounds.box_extent}, origin {bounds.origin}")
    if not SHORTEST_CM <= length <= LONGEST_CM:
        raise RuntimeError(f"SM_FishingRod_{grade} came in {length:.1f} cm long, not {SHORTEST_CM} to {LONGEST_CM}: the FBX scale is off")
    assets.save_loaded_asset(mesh)


def chunk_label():
    path = f"{ROOT}/PAL_CyrodiilRP"
    if assets.does_asset_exist(path):
        assets.delete_asset(path)
    factory = unreal.DataAssetFactory()
    factory.set_editor_property("data_asset_class", unreal.PrimaryAssetLabel)
    label = tools.create_asset("PAL_CyrodiilRP", ROOT, unreal.PrimaryAssetLabel, factory)
    rules = label.get_editor_property("rules")
    rules.set_editor_property("chunk_id", CHUNK)
    rules.set_editor_property("cook_rule", unreal.PrimaryAssetCookRule.ALWAYS_COOK)
    label.set_editor_property("rules", rules)
    label.set_editor_property("label_assets_in_my_directory", True)
    assets.save_loaded_asset(label)


import_all()
parent = master_material()
for grade in GRADES:
    dress(grade, instance(grade, parent))
chunk_label()
assets.save_directory(ROOT, only_if_is_dirty=False, recursive=True)
unreal.log("CyrodiilRP rods imported")
