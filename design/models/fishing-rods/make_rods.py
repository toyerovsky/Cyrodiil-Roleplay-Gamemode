# Builds the three fishing rods of Cyrodiil RP in Blender from code, renders previews and saves a .blend.
# Units: metres. Each rod lies along +Y: the hand at the origin, the tip at +Y, the butt and the reel at -Y, the reel
# and the guides under the rod (-Z). That matches how the game's staff hangs on the hand (grip at the origin, its long
# end along the mesh's long axis).
# usage: blender -b --factory-startup -P make_rods.py -- OUT_DIR [--no-render] [--staff STAFF_GLB]
# STAFF_GLB: a staff of the game exported as glTF, laid beside the rods in the overview for scale.
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(ARGS[0] if ARGS else ".")
RENDER = "--no-render" not in ARGS
STAFF = ARGS[ARGS.index("--staff") + 1] if "--staff" in ARGS else ""
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
SHAPE_UV = "UVMap"


# ---------------------------------------------------------------- materials

def node_material(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    out.location = (400, 0)
    mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat, nodes, mat.node_tree.links, bsdf


def plain(name, color, roughness, metallic=0.0, coat=0.0):
    mat, nodes, links, bsdf = node_material(name)
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Coat Weight"].default_value = coat
    return mat


# The patterns follow the lathe's own UVs by name, so the bake can make another UV map active.
def uv_parts(nodes, links):
    coords = nodes.new("ShaderNodeUVMap")
    coords.uv_map = SHAPE_UV
    split = nodes.new("ShaderNodeSeparateXYZ")
    links.new(coords.outputs["UV"], split.inputs["Vector"])
    return split


def math_node(nodes, links, op, a, b=None, value=None):
    node = nodes.new("ShaderNodeMath")
    node.operation = op
    links.new(a, node.inputs[0])
    if b is not None:
        links.new(b, node.inputs[1])
    elif value is not None:
        node.inputs[1].default_value = value
    return node.outputs[0]


# Blender 4.2's Mix node keeps float, vector and colour sockets side by side under the same names: 0 is the factor,
# 6 and 7 the colours A and B, output 2 the colour.
def mix_color(nodes, links, factor, a, b, blend="MIX"):
    node = nodes.new("ShaderNodeMix")
    node.data_type = "RGBA"
    node.blend_type = blend
    for socket, value in ((node.inputs[0], factor), (node.inputs[6], a), (node.inputs[7], b)):
        if isinstance(value, bpy.types.NodeSocket):
            links.new(value, socket)
        else:
            socket.default_value = value
    return node.outputs[2]


def ramp(nodes, links, factor, stops):
    node = nodes.new("ShaderNodeValToRGB")
    links.new(factor, node.inputs["Fac"])
    elements = node.color_ramp.elements
    elements[0].position, elements[0].color = stops[0][0], (*stops[0][1], 1)
    elements[1].position, elements[1].color = stops[-1][0], (*stops[-1][1], 1)
    for position, color in stops[1:-1]:
        element = elements.new(position)
        element.color = (*color, 1)
    return node.outputs["Color"]


# Wood with its grain along the rod: streaks round it, slow change along it. UV: u round, v along in metres.
def wood(name, dark, light, roughness, coat, streaks=9.0, knots=False):
    mat, nodes, links, bsdf = node_material(name)
    uv = uv_parts(nodes, links)
    combine = nodes.new("ShaderNodeCombineXYZ")
    links.new(math_node(nodes, links, "MULTIPLY", uv.outputs["X"], value=streaks), combine.inputs["X"])
    links.new(math_node(nodes, links, "MULTIPLY", uv.outputs["Y"], value=0.35), combine.inputs["Y"])
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 3.0
    noise.inputs["Detail"].default_value = 8.0
    noise.inputs["Roughness"].default_value = 0.6
    links.new(combine.outputs["Vector"], noise.inputs["Vector"])
    color = ramp(nodes, links, noise.outputs["Fac"], [(0.3, dark), (0.55, light), (0.75, dark)])
    links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Coat Weight"].default_value = coat
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.15
    bump.inputs["Distance"].default_value = 0.0005
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


# Bark of a cut hazel pole: grey-brown, with fine cracks along it and pale lenticels.
def bark(name):
    mat, nodes, links, bsdf = node_material(name)
    uv = uv_parts(nodes, links)
    combine = nodes.new("ShaderNodeCombineXYZ")
    links.new(math_node(nodes, links, "MULTIPLY", uv.outputs["X"], value=14.0), combine.inputs["X"])
    links.new(math_node(nodes, links, "MULTIPLY", uv.outputs["Y"], value=4.0), combine.inputs["Y"])
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 2.5
    noise.inputs["Detail"].default_value = 10.0
    links.new(combine.outputs["Vector"], noise.inputs["Vector"])
    spots = nodes.new("ShaderNodeTexVoronoi")
    spots.inputs["Scale"].default_value = 9.0
    links.new(combine.outputs["Vector"], spots.inputs["Vector"])
    base = ramp(nodes, links, noise.outputs["Fac"], [(0.35, (0.16, 0.11, 0.07)), (0.6, (0.33, 0.25, 0.17)), (0.8, (0.22, 0.17, 0.11))])
    spot = math_node(nodes, links, "LESS_THAN", spots.outputs["Distance"], value=0.08)
    links.new(mix_color(nodes, links, spot, base, (0.55, 0.48, 0.38, 1)), bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.78
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.35
    bump.inputs["Distance"].default_value = 0.0008
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


# A cord or a leather strip wound round the grip: a spiral of ridges.
def wrap(name, color, turns_per_metre, roughness, sheen=0.0):
    mat, nodes, links, bsdf = node_material(name)
    uv = uv_parts(nodes, links)
    along = math_node(nodes, links, "MULTIPLY", uv.outputs["Y"], value=turns_per_metre)
    spiral = math_node(nodes, links, "ADD", along, uv.outputs["X"])
    ridge = math_node(nodes, links, "PINGPONG", spiral, value=0.5)
    shaped = math_node(nodes, links, "POWER", math_node(nodes, links, "MULTIPLY", ridge, value=2.0), value=0.6)
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 60.0
    noise.inputs["Detail"].default_value = 6.0
    shade = ramp(nodes, links, noise.outputs["Fac"], [(0.3, tuple(c * 0.75 for c in color)), (0.7, color)])
    edge = ramp(nodes, links, shaped, [(0.0, (0.35, 0.35, 0.35)), (0.5, (1, 1, 1))])
    links.new(mix_color(nodes, links, 0.6, shade, edge, blend="MULTIPLY"), bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Sheen Weight"].default_value = sheen
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.6
    bump.inputs["Distance"].default_value = 0.0012
    links.new(shaped, bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def cork(name):
    mat, nodes, links, bsdf = node_material(name)
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 180.0
    noise.inputs["Detail"].default_value = 4.0
    pits = nodes.new("ShaderNodeTexVoronoi")
    pits.inputs["Scale"].default_value = 260.0
    # Object space, in metres: the generated space would stretch the grain along a 3 m rod.
    coords = nodes.new("ShaderNodeTexCoord")
    links.new(coords.outputs["Object"], noise.inputs["Vector"])
    links.new(coords.outputs["Object"], pits.inputs["Vector"])
    color = ramp(nodes, links, noise.outputs["Fac"], [(0.35, (0.42, 0.29, 0.17)), (0.65, (0.62, 0.47, 0.30))])
    pit = math_node(nodes, links, "LESS_THAN", pits.outputs["Distance"], value=0.12)
    links.new(mix_color(nodes, links, pit, color, (0.2, 0.13, 0.07, 1)), bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.4
    bump.inputs["Distance"].default_value = 0.0006
    links.new(math_node(nodes, links, "SUBTRACT", noise.outputs["Fac"], pit), bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


IRON = plain("Iron", (0.22, 0.22, 0.23), 0.42, metallic=1.0)
BLACK_IRON = plain("BlackIron", (0.08, 0.08, 0.085), 0.5, metallic=1.0)
BRASS = plain("Brass", (0.80, 0.58, 0.26), 0.28, metallic=1.0)
LINE = plain("Line", (0.50, 0.46, 0.38), 0.6)
BONE = plain("Bone", (0.80, 0.74, 0.62), 0.45)
HAZEL = bark("Hazel")
ASH = wood("Ash", (0.36, 0.23, 0.12), (0.58, 0.41, 0.23), 0.4, coat=0.3)
WALNUT = wood("Walnut", (0.12, 0.07, 0.04), (0.27, 0.16, 0.08), 0.45, coat=0.2)
EBONY = wood("Ebony", (0.018, 0.012, 0.01), (0.06, 0.036, 0.024), 0.25, coat=0.8, streaks=14.0)
CORD = wrap("Cord", (0.47, 0.38, 0.25), 260.0, 0.8)
LEATHER = wrap("Leather", (0.20, 0.10, 0.05), 70.0, 0.55, sheen=0.2)
SILK_RED = wrap("SilkRed", (0.42, 0.04, 0.04), 900.0, 0.4, sheen=0.4)
SILK_GREEN = wrap("SilkGreen", (0.06, 0.16, 0.08), 900.0, 0.45, sheen=0.3)
CORK = cork("Cork")


# ---------------------------------------------------------------- geometry

def mesh_object(name, verts, faces, uvs, material):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    layer = mesh.uv_layers.new(name=SHAPE_UV)
    for poly in mesh.polygons:
        for loop_index, corner in zip(poly.loop_indices, range(poly.loop_total)):
            layer.data[loop_index].uv = uvs[poly.index][corner]
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    mesh.shade_smooth()
    mesh.set_sharp_from_angle(angle=math.radians(50))
    return obj


# A solid of revolution round an axis through `center`: profile is (t, r) along the axis, `side` the way round the
# ring starts. bend(t) moves each ring aside (dx, dz) for a pole that is not quite straight.
def lathe(name, profile, material, segments=20, axis="Y", center=(0, 0, 0), bend=None, caps=True):
    center = Vector(center)
    verts, faces, uvs = [], [], []
    for t, r in profile:
        dx, dz = bend(t) if bend else (0.0, 0.0)
        for s in range(segments):
            a = 2 * math.pi * s / segments
            c, d = r * math.cos(a), r * math.sin(a)
            if axis == "Y":
                local = Vector((c + dx, t, d + dz))
            elif axis == "X":
                local = Vector((t, c, d))
            else:
                local = Vector((c, d, t))
            verts.append(center + local)
    rings = len(profile)
    for i in range(rings - 1):
        for s in range(segments):
            n = (s + 1) % segments
            faces.append((i * segments + s, i * segments + n, (i + 1) * segments + n, (i + 1) * segments + s))
            u0, u1 = s / segments, (s + 1) / segments
            v0, v1 = profile[i][0], profile[i + 1][0]
            uvs.append([(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
    cap_start, cap_end = (caps, caps) if isinstance(caps, bool) else caps
    for ring, flip, wanted in ((0, True, cap_start), (rings - 1, False, cap_end)):
        if wanted and profile[ring][1] > 0:
            cap = [ring * segments + s for s in range(segments)]
            faces.append(tuple(reversed(cap)) if flip else tuple(cap))
            uvs.append([(0.5 + 0.5 * math.cos(2 * math.pi * s / segments), 0.5 + 0.5 * math.sin(2 * math.pi * s / segments))
                        for s in (reversed(range(segments)) if flip else range(segments))])
    return mesh_object(name, verts, faces, uvs, material)


# A long lathe in pieces of about `piece` metres, each ring at a cut shared by both pieces: a texture bake packs short
# pieces far tighter than one 3 m strip.
def lathe_pieces(name, profile, material, piece=0.5, **options):
    parts, start = [], 0
    while start < len(profile) - 1:
        end = start + 1
        while end < len(profile) - 1 and profile[end][0] - profile[start][0] < piece:
            end += 1
        caps = (start == 0, end == len(profile) - 1)
        parts.append(lathe(f"{name}{len(parts)}", profile[start:end + 1], material, caps=caps, **options))
        start = end
    return parts


def ring(name, at, major, minor, material, axis="Y"):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=20, minor_segments=8, location=at)
    obj = bpy.context.active_object
    obj.name = name
    if axis == "Y":
        obj.rotation_euler = (math.pi / 2, 0, 0)
    elif axis == "X":
        obj.rotation_euler = (0, math.pi / 2, 0)
    obj.data.materials.append(material)
    obj.data.shade_smooth()
    return obj


def box(name, at, size, material, rotation=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(material)
    return obj


def polyline(name, points, radius, material):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = 1
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, at in zip(spline.points, points):
        point.co = (*at, 1)
    obj = bpy.data.objects.new(name, curve)
    scene.collection.objects.link(obj)
    curve.materials.append(material)
    select_only([obj])
    bpy.ops.object.convert(target="MESH")
    return bpy.context.active_object


# The line from guide to guide, a piece for each gap, for the same reason as lathe_pieces.
def run_line(name, points, radius, material):
    return [polyline(f"{name}{index}", [start, end], radius, material) for index, (start, end) in enumerate(zip(points, points[1:]))]


def select_only(objects):
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def join(name, parts):
    select_only(parts)
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    return obj


# Joined into one mesh, with what export_rods.py bends it by: where the blank leaves the grip, where it ends and the
# centre of the tip ring, where the line leaves the rod.
def assemble(name, parts, blank, bend_from):
    rod = join(name, parts)
    rod["bend_from"] = bend_from
    rod["tip"] = blank.tip
    rod["tip_ring"] = tuple(blank.tip_ring)
    return rod


# ---------------------------------------------------------------- rods

class Blank:
    # The rod's taper and its bend: radius and sideways offset at a point along it.
    def __init__(self, start, tip, butt_radius, tip_radius, sag=0.0, wobble=0.0):
        self.start, self.tip = start, tip
        self.butt_radius, self.tip_radius = butt_radius, tip_radius
        self.sag, self.wobble = sag, wobble
        self.tip_ring = self.point(tip) + Vector((0, 0.004, 0))

    def radius(self, y):
        share = min(max((y - self.start) / (self.tip - self.start), 0.0), 1.0)
        return self.butt_radius + (self.tip_radius - self.butt_radius) * share ** 0.85

    def bend(self, y):
        share = min(max((y - self.start) / (self.tip - self.start), 0.0), 1.0)
        dz = -self.sag * share ** 2 + self.wobble * math.sin(y * 6.1) * share
        dx = self.wobble * 0.7 * math.sin(y * 4.3 + 1.0) * share
        return dx, dz

    def profile(self, steps, swells=()):
        rows = []
        for i in range(steps + 1):
            y = self.start + (self.tip - self.start) * i / steps
            r = self.radius(y)
            for at, width, height in swells:
                r += height * math.exp(-((y - at) / width) ** 2)
            rows.append((y, r))
        return rows

    def point(self, y, below=0.0):
        dx, dz = self.bend(y)
        return Vector((dx, y, dz - below))


def guides(prefix, blank, places, ring_material, foot_material, wrap_material, sizes):
    parts = []
    for index, (y, size) in enumerate(zip(places, sizes)):
        r = blank.radius(y)
        center = blank.point(y, below=r + size + 0.004)
        parts.append(ring(f"{prefix}_guide{index}", center, size, max(0.0009, size * 0.16), ring_material))
        foot_top = blank.point(y, below=r * 0.6)
        foot_bottom = blank.point(y, below=r + 0.004)
        parts.append(box(f"{prefix}_foot{index}", (foot_top + foot_bottom) / 2, (0.0016, 0.012, (foot_top - foot_bottom).length + 0.002), foot_material))
        if wrap_material is not None:
            parts.append(lathe(f"{prefix}_wrap{index}", [(y - 0.011, r * 1.18), (y + 0.011, r * 1.18)], wrap_material, segments=16, bend=blank.bend))
    return parts


def line_points(blank, reel_top, places, sizes, tip):
    points = [reel_top]
    for y, size in zip(places, sizes):
        points.append(blank.point(y, below=blank.radius(y) + size + 0.004))
    points.append(blank.point(tip, below=0.0))
    return points


def centrepin(prefix, at, radius, width, wood_material, rim_material, line_material, spokes, handle_material):
    # A wooden drum on an axle across the rod (along X): two flanges, line wound between them, a handle on the face.
    x0 = at.x
    parts = []
    half = width / 2
    flange = 0.0035
    parts.append(lathe(f"{prefix}_back", [(x0 - half - flange, 0.0), (x0 - half - flange, radius * 0.98), (x0 - half - flange * 0.5, radius),
                                         (x0 - half, radius * 0.98), (x0 - half, 0.0)], wood_material, segments=40, axis="X",
                       center=(0, at.y, at.z)))
    parts.append(lathe(f"{prefix}_front", [(x0 + half, 0.0), (x0 + half, radius * 0.98), (x0 + half + flange * 0.5, radius),
                                          (x0 + half + flange, radius * 0.98), (x0 + half + flange, 0.0)], wood_material, segments=40,
                       axis="X", center=(0, at.y, at.z)))
    parts.append(lathe(f"{prefix}_line", [(x0 - half, radius * 0.72), (x0 + half, radius * 0.72)], line_material, segments=40, axis="X",
                       center=(0, at.y, at.z)))
    parts.append(ring(f"{prefix}_rimb", (x0 - half - flange * 0.5, at.y, at.z), radius, 0.0017, rim_material, axis="X"))
    parts.append(ring(f"{prefix}_rimf", (x0 + half + flange * 0.5, at.y, at.z), radius, 0.0017, rim_material, axis="X"))
    parts.append(lathe(f"{prefix}_axle", [(x0 - half - flange - 0.004, 0.0), (x0 - half - flange - 0.004, 0.007), (x0 + half + flange + 0.005, 0.007),
                                         (x0 + half + flange + 0.006, 0.0)], rim_material, segments=16, axis="X", center=(0, at.y, at.z)))
    for index in range(spokes):
        a = 2 * math.pi * index / spokes
        mid = Vector((x0 + half + flange + 0.0006, at.y + math.cos(a) * radius * 0.5, at.z + math.sin(a) * radius * 0.5))
        parts.append(box(f"{prefix}_spoke{index}", mid, (0.0012, radius * 0.75, 0.004), rim_material, rotation=(a, 0, 0)))
    knob_at = Vector((x0 + half + flange, at.y + radius * 0.62, at.z))
    parts.append(lathe(f"{prefix}_knob", [(0.0, 0.0028), (0.006, 0.0028), (0.007, 0.0045), (0.022, 0.005), (0.024, 0.0042), (0.025, 0.0)],
                       handle_material, segments=14, axis="X", center=knob_at))
    return parts


def reel_foot(prefix, blank, y, reel_center, material):
    top = blank.point(y, below=blank.radius(y) * 0.5)
    return [box(f"{prefix}_stem", (top + reel_center) / 2 + Vector((0, 0, 0.004)), (0.006, 0.014, (top - reel_center).length - 0.012), material),
            box(f"{prefix}_plate", top + Vector((0, 0, -blank.radius(y) * 0.5)), (0.009, 0.06, 0.002), material)]


def basic_rod():
    # A cut hazel pole, one piece and a little crooked, cord wound for the hand, an iron-bound wooden spool and bent
    # iron wire for guides.
    blank = Blank(start=-0.40, tip=2.30, butt_radius=0.0165, tip_radius=0.0032, sag=0.05, wobble=0.004)
    knots = [(0.55, 0.012, 0.0022), (1.15, 0.01, 0.0018), (1.7, 0.01, 0.0013)]
    parts = lathe_pieces("basic_pole", blank.profile(90, knots), HAZEL, segments=18, bend=blank.bend)
    parts.append(lathe("basic_butt", [(-0.408, 0.0), (-0.408, 0.0135), (-0.403, 0.0162), (-0.398, 0.0166)], HAZEL, segments=18))
    grip = [(y, blank.radius(y) + 0.0022) for y in (-0.34, -0.33, 0.06, 0.07)]
    grip[0] = (grip[0][0], blank.radius(-0.34) + 0.0005)
    grip[-1] = (grip[-1][0], blank.radius(0.07) + 0.0005)
    parts.append(lathe("basic_grip", grip, CORD, segments=18, bend=blank.bend))
    reel_y = -0.235
    center = blank.point(reel_y, below=blank.radius(reel_y) + 0.052)
    parts += centrepin("basic_reel", center, 0.040, 0.026, WALNUT, IRON, LINE, spokes=0, handle_material=WALNUT)
    parts += reel_foot("basic_reelfoot", blank, reel_y, center, IRON)
    parts.append(lathe("basic_band0", [(reel_y - 0.045, blank.radius(reel_y) + 0.0016), (reel_y - 0.03, blank.radius(reel_y) + 0.0016)], IRON, segments=18, bend=blank.bend))
    parts.append(lathe("basic_band1", [(reel_y + 0.03, blank.radius(reel_y) + 0.0016), (reel_y + 0.045, blank.radius(reel_y) + 0.0016)], IRON, segments=18, bend=blank.bend))
    places = [0.45, 0.95, 1.45, 1.9]
    sizes = [0.0085, 0.007, 0.0055, 0.0045]
    parts += guides("basic", blank, places, IRON, IRON, CORD, sizes)
    parts.append(ring("basic_tiptop", blank.tip_ring, 0.0035, 0.0009, IRON))
    parts += run_line("basic_runline", line_points(blank, center + Vector((0, 0.0, 0.040 * 0.72)), places, sizes, 2.30), 0.00045, LINE)
    return assemble("SM_FishingRod_Basic", parts, blank, bend_from=0.07)


def sturdy_rod():
    # Turned ash in two pieces joined by an iron ferrule, a leather grip between iron caps, a walnut centrepin and
    # five iron guides whipped with green silk.
    blank = Blank(start=-0.42, tip=2.45, butt_radius=0.0145, tip_radius=0.0028, sag=0.025)
    parts = lathe_pieces("sturdy_blank", blank.profile(80), ASH, segments=20, bend=blank.bend)
    parts.append(lathe("sturdy_butt", [(-0.435, 0.0), (-0.435, 0.012), (-0.43, 0.0175), (-0.40, 0.0178), (-0.395, 0.0165)], BLACK_IRON, segments=24))
    grip_rows = [(-0.395, 0.0165), (-0.385, 0.0185), (-0.2, 0.0192), (-0.08, 0.0205), (0.05, 0.0185), (0.075, 0.0172)]
    parts.append(lathe("sturdy_grip", grip_rows, LEATHER, segments=24))
    parts.append(lathe("sturdy_cap", [(0.075, 0.0176), (0.09, 0.0176), (0.1, blank.radius(0.1) + 0.001)], BLACK_IRON, segments=24))
    ferrule = 1.05
    parts.append(lathe("sturdy_ferrule", [(ferrule - 0.03, blank.radius(ferrule) + 0.0004), (ferrule - 0.025, blank.radius(ferrule) + 0.0024),
                                         (ferrule + 0.025, blank.radius(ferrule) + 0.0024), (ferrule + 0.03, blank.radius(ferrule) + 0.0004)],
                       BLACK_IRON, segments=20, bend=blank.bend))
    reel_y = -0.25
    center = blank.point(reel_y, below=0.0185 + 0.05)
    parts += centrepin("sturdy_reel", center, 0.045, 0.028, WALNUT, BLACK_IRON, LINE, spokes=0, handle_material=BONE)
    parts += reel_foot("sturdy_reelfoot", blank, reel_y, center, BLACK_IRON)
    places = [0.35, 0.75, 1.2, 1.6, 1.95, 2.22]
    sizes = [0.0095, 0.008, 0.0068, 0.0057, 0.0048, 0.004]
    parts += guides("sturdy", blank, places, IRON, BLACK_IRON, SILK_GREEN, sizes)
    parts.append(ring("sturdy_tiptop", blank.tip_ring, 0.0033, 0.0009, IRON))
    parts += run_line("sturdy_runline", line_points(blank, center + Vector((0, 0.0, 0.045 * 0.72)), places, sizes, 2.45), 0.00045, LINE)
    return assemble("SM_FishingRod_Sturdy", parts, blank, bend_from=0.10)


def master_rod():
    # Ebony in three pieces with brass ferrules, a cork grip between brass collars with a turned brass butt, a brass
    # spoked centrepin and seven brass guides whipped with crimson silk.
    blank = Blank(start=-0.43, tip=2.60, butt_radius=0.0135, tip_radius=0.0025, sag=0.02)
    parts = lathe_pieces("master_blank", blank.profile(90), EBONY, segments=22, bend=blank.bend)
    parts.append(lathe("master_butt", [(-0.465, 0.0), (-0.465, 0.009), (-0.458, 0.0165), (-0.448, 0.0185), (-0.44, 0.0172),
                                      (-0.43, 0.019), (-0.42, 0.0178)], BRASS, segments=28))
    grip_rows = [(-0.42, 0.0172), (-0.41, 0.0186), (-0.3, 0.0186), (-0.27, 0.0178), (-0.2, 0.0178), (-0.17, 0.019), (-0.05, 0.0205),
                 (0.04, 0.019), (0.07, 0.0175)]
    parts.append(lathe("master_grip", grip_rows, CORK, segments=28))
    parts.append(lathe("master_seat", [(-0.3, 0.0181), (-0.297, 0.0192), (-0.173, 0.0192), (-0.17, 0.0181)], BRASS, segments=28))
    parts.append(lathe("master_collar", [(0.07, 0.0178), (0.075, 0.019), (0.085, 0.019), (0.1, blank.radius(0.1) + 0.0012), (0.11, blank.radius(0.11) + 0.0006)],
                       BRASS, segments=28))
    for ferrule in (0.78, 1.7):
        r = blank.radius(ferrule)
        parts.append(lathe(f"master_ferrule{ferrule}", [(ferrule - 0.03, r + 0.0003), (ferrule - 0.026, r + 0.002), (ferrule - 0.004, r + 0.002),
                                                       (ferrule - 0.002, r + 0.0028), (ferrule + 0.002, r + 0.0028), (ferrule + 0.004, r + 0.002),
                                                       (ferrule + 0.026, r + 0.002), (ferrule + 0.03, r + 0.0003)],
                           BRASS, segments=22, bend=blank.bend))
        parts.append(lathe(f"master_ferrulewrap{ferrule}", [(ferrule - 0.05, r * 1.16), (ferrule - 0.03, r * 1.16)], SILK_RED, segments=16, bend=blank.bend))
        parts.append(lathe(f"master_ferrulewrapb{ferrule}", [(ferrule + 0.03, r * 1.16), (ferrule + 0.05, r * 1.16)], SILK_RED, segments=16, bend=blank.bend))
    reel_y = -0.235
    center = blank.point(reel_y, below=0.0192 + 0.054)
    parts += centrepin("master_reel", center, 0.048, 0.03, EBONY, BRASS, LINE, spokes=5, handle_material=BONE)
    parts += reel_foot("master_reelfoot", blank, reel_y, center, BRASS)
    places = [0.3, 0.62, 0.98, 1.32, 1.62, 1.92, 2.2, 2.42]
    sizes = [0.01, 0.0088, 0.0076, 0.0066, 0.0058, 0.005, 0.0044, 0.0039]
    parts += guides("master", blank, places, BRASS, BRASS, SILK_RED, sizes)
    parts.append(ring("master_tiptop", blank.tip_ring, 0.0032, 0.0009, BRASS))
    parts += run_line("master_runline", line_points(blank, center + Vector((0, 0.0, 0.048 * 0.72)), places, sizes, 2.60), 0.00045, LINE)
    return assemble("SM_FishingRod_Master", parts, blank, bend_from=0.11)


rods = [basic_rod(), sturdy_rod(), master_rod()]
for rod in rods:
    print("ROD", rod.name, "verts", len(rod.data.vertices), "tris", sum(len(p.vertices) - 2 for p in rod.data.polygons),
          "materials", [m.name for m in rod.data.materials])

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "fishing_rods.blend"))
if not RENDER:
    sys.exit(0)


# ---------------------------------------------------------------- previews

def setup_render(width, height, samples):
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    device = "NONE"
    for kind in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            usable = [d for d in prefs.devices if d.type == kind]
            if usable:
                for d in prefs.devices:
                    d.use = d.type == kind
                device = kind
                break
        except TypeError:
            continue
    scene.cycles.device = "GPU" if device != "NONE" else "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    print("DEVICE", device)


def world(color, strength):
    sky = bpy.data.worlds.new("World")
    sky.use_nodes = True
    sky.node_tree.nodes["Background"].inputs["Color"].default_value = (*color, 1)
    sky.node_tree.nodes["Background"].inputs["Strength"].default_value = strength
    scene.world = sky


def area_light(name, at, target, power, size, color=(1, 1, 1)):
    light = bpy.data.lights.new(name, "AREA")
    light.energy = power
    light.size = size
    light.color = color
    obj = bpy.data.objects.new(name, light)
    scene.collection.objects.link(obj)
    obj.location = at
    look_at(obj, target)
    return obj


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def camera(name, at, target, lens=50, ortho=None):
    cam = bpy.data.cameras.new(name)
    if ortho:
        cam.type = "ORTHO"
        cam.ortho_scale = ortho
    else:
        cam.lens = lens
    cam.clip_start = 0.01
    obj = bpy.data.objects.new(name, cam)
    scene.collection.objects.link(obj)
    obj.location = at
    look_at(obj, target)
    scene.camera = obj
    return obj


def label(text, at, size):
    curve = bpy.data.curves.new(text, "FONT")
    curve.body = text
    curve.size = size
    curve.align_x = "LEFT"
    obj = bpy.data.objects.new("label_" + text, curve)
    scene.collection.objects.link(obj)
    obj.location = at
    curve.materials.append(plain("Label", (0.85, 0.8, 0.7), 0.6))
    return obj


def backdrop(z, material_color):
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, z))
    plane = bpy.context.active_object
    plane.data.materials.append(plain("Backdrop", material_color, 0.9))
    return plane


setup_render(2400, 1100, 96)
world((0.045, 0.05, 0.06), 0.4)

# Overview: the three rods and the game's staff laid flat along +X, hands at one x, seen from above. Turned about their
# own axis first, so the reels and guides lie beside the rods (+Y) instead of under them.
FLAT = (0, math.pi / 2, -math.pi / 2)
HAND_X = -1.05
ROW = 0.25
layout = []
for index, rod in enumerate(rods):
    rod.rotation_euler = FLAT
    rod.location = (HAND_X, 0.33 - index * ROW, 0.1)
    layout.append(rod)
staff = []
if STAFF and os.path.exists(STAFF):
    bpy.ops.import_scene.gltf(filepath=STAFF)
    for obj in bpy.context.selected_objects:
        if obj.type == "MESH":
            obj.rotation_mode = "XYZ"
            obj.rotation_euler = FLAT
            obj.data.materials.clear()
            obj.data.materials.append(plain("StaffStandIn", (0.25, 0.17, 0.1), 0.7))
            obj.location = (HAND_X, 0.33 - 3 * ROW, 0.1)
            staff.append(obj)
labels = [label(t, (HAND_X + 0.05, 0.33 - i * ROW - 0.085, 0.06), 0.045) for i, t in enumerate(("Basic", "Sturdy", "Master", "Staff (now)"))]
floor = backdrop(0.0, (0.07, 0.075, 0.085))
lights = [area_light("key", (0.5, -1.0, 3.0), (0.3, 0, 0), 120, 3.0, (1.0, 0.95, 0.88)),
          area_light("fill", (-2.5, 1.5, 2.0), (0.3, 0, 0), 35, 3.0, (0.8, 0.88, 1.0))]
camera("overview", (0.05, -0.05, 4.0), (0.05, -0.05, 0.0), ortho=3.6)
scene.render.filepath = os.path.join(OUT, "rods_overview.png")
bpy.ops.render.render(write_still=True)

# Close-ups of the butt, the grip, the reel and the first guide, one rod at a time, from the reel's side and above.
setup_render(1600, 1000, 128)
for obj in labels + staff:
    obj.hide_render = True
for light in lights:
    light.data.energy *= 0.5
area_light("closekey", (HAND_X + 0.4, 1.4, 1.2), (HAND_X - 0.1, 0.33, 0.1), 25, 1.0, (1.0, 0.95, 0.88))
for index, rod in enumerate(rods):
    for other in layout:
        other.hide_render = other is not rod
    base = Vector(rod.location)
    target = base + Vector((-0.14, 0.04, 0.0))
    camera(f"close{index}", base + Vector((-0.06, 0.62, 0.32)), target, lens=40)
    scene.render.filepath = os.path.join(OUT, f"rods_close_{rod.name}.png")
    bpy.ops.render.render(write_still=True)

# The tip end, with its guides and the line, of the master's rod.
for other in layout:
    other.hide_render = other is not rods[2]
base = Vector(rods[2].location)
camera("tip", base + Vector((2.25, 0.45, 0.25)), base + Vector((2.3, 0.0, 0.0)), lens=50)
scene.render.filepath = os.path.join(OUT, "rods_tip_master.png")
bpy.ops.render.render(write_still=True)
