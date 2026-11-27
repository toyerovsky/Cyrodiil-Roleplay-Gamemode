# Holds the cooked animation to the game's own, without the game: reads what the research repo's UeExport wrote for a
# folder with the game's containers and CyrodiilRPAnim_P together (the game-assets skill, "Checking a container without
# the game"), and compares the mode's sequence with the game's base sequence and with the keys make_reel.py wrote.
# usage: python -I check_reel.py MADE_DIR JSON_DIR DECODED_DIR     (needs numpy)
#   MADE_DIR     make_reel.py's output (A_Fishing_Reel.json)
#   JSON_DIR     UeExport `json` of that folder for these prefixes: OblivionRemastered/Content/CyrodiilRP/,
#                Engine/Content/Animation/, OblivionRemastered/Content/Art/Character/Humanoid/SKEL_HumanoidSkeleton and
#                the base sequence's package (OblivionRemastered/Content/Art/Animation/.../A_Humanoid_Staff_Block_Idle)
#   DECODED_DIR  UeExport `anims` of that folder for the mode's sequence and the base sequence
# It passes for a container the game may still refuse: it shows that the asset reads as a game sequence does, with the
# game's class layouts, and that the keys are the ones meant. Nothing but the game shows that the game plays it.
import csv
import json
import math
import pathlib
import sys

import numpy

MADE_DIR, JSON_DIR, DECODED_DIR = (pathlib.Path(path) for path in sys.argv[1:4])
MADE = json.loads((MADE_DIR / "A_Fishing_Reel.json").read_text())
NAME = MADE["name"]
PACKAGE = f"/Game/CyrodiilRP/Fishing/{NAME}"
ENGINE_SETTINGS = "/Engine/Animation/"
# What the bone compression may cost: degrees and centimetres a decoded key may be off the key written, and
# centimetres a hand may be off in the character's space.
NEAR_DEGREES, NEAR_CM, NEAR_SCALE, NEAR_HAND_CM = 1.0, 0.1, 0.01, 0.3
# Helper bones of the legs whose small constant twist (under 6 degrees) the stock cook rounds to none, where the
# game's own cook of the base kept it. The per-track codec weighs a rotation by how far it moves the bones under it,
# and these move none: only the skin of the calf and the thigh turns with them. The legs are outside the UpperBody slot
# the client plays the animation in. The game's sockets on the stand-in skeleton did not change it.
DROPPED = ("calf_twist_02_l", "calf_twist_02_r", "thigh_twist_01_l")
DROPPED_DEGREES = 6.0
failed = False


def report(name, notes, text):
    global failed
    failed |= bool(notes)
    print(name, "OK" if not notes else "FAILED " + str(notes), "|", text)


def content(game_path):
    root, rest = game_path.lstrip("/").split("/", 1)
    return ("OblivionRemastered/Content/" if root == "Game" else f"{root}/Content/") + rest


def exports(game_path):
    path = JSON_DIR / (content(game_path) + ".json")
    if not path.exists():
        raise SystemExit(f"No {path}: UeExport json must cover {game_path}")
    return json.loads(path.read_text())


# Every path to a leaf with the leaf's type: two assets of one structure give the same set.
def shape(value, path=""):
    if isinstance(value, dict):
        out = set()
        for key, item in value.items():
            out |= shape(item, f"{path}/{key}")
        return out or {path + "{}"}
    if isinstance(value, list):
        out = set()
        for index, item in enumerate(value):
            out |= shape(item, f"{path}[{index}]")
        return out or {path + "[]"}
    return {f"{path}:{type(value).__name__ if value is not None else 'null'}"}


def object_paths(value):
    if isinstance(value, dict):
        for key, item in value.items():
            if key == "ObjectPath":
                yield item
            else:
                yield from object_paths(item)
    elif isinstance(value, list):
        for item in value:
            yield from object_paths(item)


# --- the asset as the game's reader sees it --------------------------------------------------------------------------

def check_asset():
    mine_all, base_all = exports(PACKAGE), exports(MADE["base"].split(".")[0])
    skeleton = next(export for export in exports(MADE["skeleton"]["path"]) if export["Type"] == "Skeleton")
    bones = [bone["Name"] for bone in skeleton["ReferenceSkeleton"]["FinalRefBoneInfo"]]
    virtual = [bone["VirtualBoneName"] for bone in skeleton["Properties"].get("VirtualBones", [])]

    packages = sorted(str(path.relative_to(JSON_DIR)).replace("\\", "/") for path in (JSON_DIR / "OblivionRemastered/Content/CyrodiilRP").rglob("*.json"))
    wanted = [content(PACKAGE) + ".json"]
    report("FILES", [] if packages == wanted else [f"the container's packages are {packages}"], f"{len(packages)} package under /Game/CyrodiilRP")

    notes = []
    if [export["Type"] for export in mine_all] != ["AnimSequence"] or [export["Type"] for export in base_all] != ["AnimSequence"]:
        raise SystemExit(f"exports {[export['Type'] for export in mine_all]}, the base's {[export['Type'] for export in base_all]}")
    mine, base = mine_all[0], base_all[0]
    if mine["Name"] != NAME or mine["Package"] != PACKAGE:
        notes.append(f"name {mine['Name']} package {mine['Package']}")
    if mine["Flags"] != base["Flags"] or mine["Class"] != base["Class"]:
        notes.append(f"flags {mine['Flags']} class {mine['Class']}")
    different = shape(mine) ^ shape(base)
    if different:
        notes.append("SHAPE DIFFERS from the base: " + str(sorted(different)))
    properties, base_properties = mine["Properties"], base["Properties"]
    if list(properties) != list(base_properties):
        notes.append(f"properties {list(properties)}, the base's {list(base_properties)}")
    # Everything but the name is the base's: the skeleton and the three settings assets resolve to the very objects
    # the base's do, in the game's own packages.
    for key, value in base_properties.items():
        if properties.get(key) != value:
            notes.append(f"{key} is {properties.get(key)}, the base's {value}")
    report("PROPERTIES", notes, f"{len(properties)} properties, each equal to the base's: {json.dumps(properties)}")

    notes = []
    if mine["SkeletonGuid"] != skeleton["Guid"] or base["SkeletonGuid"] != skeleton["Guid"]:
        notes.append(f"skeleton guid {mine['SkeletonGuid']}, the game skeleton's {skeleton['Guid']}")
    if skeleton["Guid"].replace("-", "") != MADE["skeleton"]["guid"] or [[b["Name"], b["ParentIndex"]] for b in skeleton["ReferenceSkeleton"]["FinalRefBoneInfo"]] != MADE["skeleton"]["bones"]:
        notes.append("the game's skeleton is not the one make_reel.py read")
    skeleton_path = properties["Skeleton"]["ObjectPath"].rsplit(".", 1)[0]
    if skeleton_path != MADE["skeleton"]["path"]:
        notes.append(f"skeleton {skeleton_path}")
    report("SKELETON", notes, f"{properties['Skeleton']['ObjectPath']} with guid {mine['SkeletonGuid']}, {len(bones)} bones and {len(virtual)} virtual bones in the game")

    notes = []
    for key in ("BoneCompressionSettings", "CurveCompressionSettings", "VariableFrameStrippingSettings"):
        path = properties[key]["ObjectPath"].rsplit(".", 1)[0]
        if not path.startswith(ENGINE_SETTINGS) or not (JSON_DIR / (content(path) + ".json")).exists():
            notes.append(f"{key} names {path}, which the game's containers do not hold")
    # UeExport writes the compressed data's structure only when the codec the sequence names is found in the settings
    # asset as the game has it.
    data, base_data = mine.get("CompressedDataStructure"), base.get("CompressedDataStructure")
    if not data:
        notes.append("the bone codec the sequence names is not in the game's settings asset")
    elif {k: v for k, v in data.items()} != {k: v for k, v in base_data.items()} or data["CompressedNumberOfFrames"] != MADE["keys"]:
        notes.append(f"compressed data {data}, the base's {base_data}")
    codecs = [export["Name"] for export in exports(ENGINE_SETTINGS + "DefaultAnimBoneCompressionSettings") if export["Type"].startswith("AnimCompress")]
    report("COMPRESSION", notes, f"{data}; the game's default settings hold {len(codecs)} codecs")

    notes = []
    table, base_table = mine["CompressedTrackToSkeletonMapTable"], base["CompressedTrackToSkeletonMapTable"]
    keyed = sorted(bones.index(track["bone"]) for track in MADE["tracks"])
    baked = list(range(len(bones), len(bones) + len(virtual)))
    if len(table) != len(base_table) or sorted(table) != sorted(base_table):
        notes.append(f"{len(table)} tracks, the base has {len(base_table)}; bones only one has: {sorted(set(table) ^ set(base_table))}")
    if sorted(table) != keyed + baked or len(set(table)) != len(table):
        notes.append("the tracks are not the bones keyed and the virtual bones")
    moved = [(place, bones[bone]) for place, (bone, other) in enumerate(zip(table, base_table)) if bone != other and bone < len(bones)]
    report("TRACKS", notes, f"{len(table)} tracks for the base's {len(base_table)}: {len(keyed)} bones and {len(baked)} virtual bones, "
           f"every index within the game skeleton's {len(bones) + len(virtual)}; {len(moved)} tracks stand at another place than in the base "
           f"(the map names the bone, the place is free)")

    foreign = sorted({path.rsplit(".", 1)[0] for path in object_paths(mine_all)} - {MADE["skeleton"]["path"]}
                     - {ENGINE_SETTINGS + name for name in ("DefaultAnimBoneCompressionSettings", "DefaultAnimCurveCompressionSettings", "DefaultVariableFrameStrippingSettings")})
    report("REFERENCES", ["refers to " + str(foreign)] if foreign else [], "only the game's skeleton and the engine's three settings assets")
    return skeleton


# --- the keys as the game's reader decodes them ----------------------------------------------------------------------

def quat_matrix(q):
    x, y, z, w = q[..., 0], q[..., 1], q[..., 2], q[..., 3]
    m = numpy.empty(q.shape[:-1] + (3, 3))
    m[..., 0, 0] = 1 - 2 * (y * y + z * z)
    m[..., 0, 1] = 2 * (x * y - z * w)
    m[..., 0, 2] = 2 * (x * z + y * w)
    m[..., 1, 0] = 2 * (x * y + z * w)
    m[..., 1, 1] = 1 - 2 * (x * x + z * z)
    m[..., 1, 2] = 2 * (y * z - x * w)
    m[..., 2, 0] = 2 * (x * z - y * w)
    m[..., 2, 1] = 2 * (y * z + x * w)
    m[..., 2, 2] = 1 - 2 * (x * x + y * y)
    return m


def world(local, parents):
    m = numpy.zeros(local.shape[:-1] + (4, 4))
    m[..., :3, :3] = quat_matrix(local[..., :4]) * local[..., None, 7:]
    m[..., :3, 3] = local[..., 4:7]
    m[..., 3, 3] = 1.0
    out = numpy.empty_like(m)
    for bone, parent in enumerate(parents):
        out[:, bone] = m[:, bone] if parent < 0 else out[:, parent] @ m[:, bone]
    return out


def decoded(sequence):
    with open(DECODED_DIR / "sequences.tsv", encoding="utf-8") as listing:
        rows = [row for row in csv.DictReader(listing, delimiter="\t") if row["sequence"] == sequence]
    if not rows or rows[-1]["error"]:
        raise SystemExit(f"{sequence} is not decoded in {DECODED_DIR}: {rows[-1]['error'] if rows else 'no row'}")
    row = rows[-1]
    bones = row["bones"].split(",")
    data = numpy.fromfile(DECODED_DIR / row["anim"], dtype="<f4").reshape(int(row["keys"]), len(bones), 10).astype(numpy.float64)
    # The reader leaves a quaternion up to 0.17% short of unit length at some keys: the rotation is the same.
    data[..., :4] /= numpy.linalg.norm(data[..., :4], axis=-1, keepdims=True)
    return bones, data, row


def apart(found, wanted):
    # Degrees between rotations, centimetres between translations, the largest difference in scale: per key and bone.
    dot = numpy.abs(numpy.sum(found[..., :4] * wanted[..., :4], axis=-1))
    return (numpy.degrees(2 * numpy.arccos(numpy.clip(dot, 0.0, 1.0))), numpy.linalg.norm(found[..., 4:7] - wanted[..., 4:7], axis=-1),
            numpy.abs(found[..., 7:] - wanted[..., 7:]).max(axis=-1))


def check_keys(skeleton):
    names = [bone["Name"] for bone in skeleton["ReferenceSkeleton"]["FinalRefBoneInfo"]]
    parents = [bone["ParentIndex"] for bone in skeleton["ReferenceSkeleton"]["FinalRefBoneInfo"]]
    rest = numpy.array([[pose["Rotation"][k] for k in "XYZW"] + [pose["Translation"][k] for k in "XYZ"] + [pose["Scale3D"][k] for k in "XYZ"]
                        for pose in skeleton["ReferenceSkeleton"]["FinalRefBonePose"]])
    index = {name: i for i, name in enumerate(names)}
    written_bones = [track["bone"] for track in MADE["tracks"]]
    written = numpy.array([[r + t + s for r, t, s in zip(track["rotation"], track["translation"], track["scale"])] for track in MADE["tracks"]]).transpose(1, 0, 2)
    bones, cooked, row = decoded(f"{PACKAGE}.{NAME}")
    base_bones, base, _ = decoded(MADE["base"])

    notes = []
    if int(row["keys"]) != MADE["keys"] or abs(float(row["length"]) - MADE["length"]) > 1e-6 or row["skeleton"].split(".")[0] != MADE["skeleton"]["path"]:
        notes.append(f"{row['keys']} keys over {row['length']} s on {row['skeleton']}")
    if sorted(bones) != sorted(written_bones) or sorted(bones) != sorted(base_bones):
        notes.append(f"decoded bones differ from the bones written: {sorted(set(bones) ^ set(written_bones))}")
        report("DECODED", notes, "")
        return
    cooked = cooked[:, [bones.index(bone) for bone in written_bones]]
    base = base[:, [base_bones.index(bone) for bone in written_bones]]
    degrees, cm, scale = apart(cooked, written)
    # A rotation the cook dropped decodes as none at every key: allowed for the bones listed, where it was small.
    identity = numpy.array([0.0, 0.0, 0.0, 1.0])
    dropped = [i for i, bone in enumerate(written_bones) if degrees[:, i].max() > NEAR_DEGREES
               and numpy.abs(numpy.abs(cooked[:, i, :4]) - identity).max() < 1e-6]
    off_list = [written_bones[i] for i in dropped if written_bones[i] not in DROPPED or degrees[:, i].max() > DROPPED_DEGREES]
    if off_list:
        notes.append(f"the cook dropped the rotation of {off_list}")
    held = [i for i in range(len(written_bones)) if i not in dropped]
    worst = numpy.unravel_index(numpy.argmax(degrees[:, held]), degrees[:, held].shape)
    if degrees[:, held].max() > NEAR_DEGREES or cm.max() > NEAR_CM or scale.max() > NEAR_SCALE:
        notes.append("the decoded keys are not the keys written")
    report("DECODED", notes, f"{len(bones)} bones, {len(cooked)} keys: against the keys written, rotations within {degrees[:, held].max():.3f} degrees "
           f"({written_bones[held[worst[1]]]}, key {worst[0]}), translations within {cm.max():.4f} cm, scales within {scale.max():.5f}; "
           f"rotation dropped by the cook: {', '.join(f'{written_bones[i]} ({degrees[:, i].max():.1f} degrees)' for i in dropped) or 'none'}")

    notes = []
    changed = [written_bones.index(bone) for bone in MADE["changed"]]
    kept = [i for i in range(len(written_bones)) if i not in changed and i not in dropped]
    degrees, cm, scale = apart(cooked[:, kept], base[:, kept])
    if degrees.max() > NEAR_DEGREES or cm.max() > NEAR_CM or scale.max() > NEAR_SCALE:
        notes.append("bones outside the left arm differ from the base")
    arm_degrees, _, _ = apart(cooked[:, changed], base[:, changed])
    report("BASE", notes, f"{len(kept)} bones as the game's decoded base: rotations within {degrees.max():.3f} degrees, translations within {cm.max():.4f} cm, "
           f"scales within {scale.max():.5f}; the {len(changed)} bones of the left arm differ from it by up to {arm_degrees.max():.0f} degrees")

    # The poses in the character's space: the cooked hand on the reel, and every other end of the body where the base has it.
    def posed(tracks):
        local = numpy.repeat(rest[None], len(tracks), axis=0)
        local[:, [index[bone] for bone in written_bones]] = tracks
        return world(local, parents)

    cooked_world, written_world, base_world = posed(cooked), posed(written), posed(base)
    notes = []
    reel = MADE["reel"]
    hang = numpy.eye(4)
    hang[:3, :3] = numpy.diag([-1.0, 1.0, -1.0])
    hang[:3, 3] = reel["offset"]
    rod = cooked_world[:, index[reel["socket"]]] @ hang[None]
    grip = cooked_world[:, index["hand_l"]] @ numpy.array(reel["grip"] + [1.0])
    on_rod = numpy.einsum("kij,kj->ki", numpy.linalg.inv(rod), grip)[:, :3]
    radius = numpy.hypot(on_rod[:, 1] - reel["axle"][1], on_rod[:, 2] - reel["axle"][2])
    angle = numpy.unwrap(numpy.arctan2(on_rod[:, 2] - reel["axle"][2], on_rod[:, 1] - reel["axle"][1]))
    turns = (angle[-1] - angle[0]) / (2 * math.pi)
    hand_off = numpy.linalg.norm(cooked_world[:, index["hand_l"], :3, 3] - written_world[:, index["hand_l"], :3, 3], axis=1).max()
    if abs(radius - reel["radius"]).max() > NEAR_HAND_CM or abs(on_rod[:, 0] - reel["axle"][0]).max() > NEAR_HAND_CM or abs(abs(turns) - reel["turns"]) > 0.02:
        notes.append("the hand does not go round the reel's axle as written")
    if hand_off > NEAR_HAND_CM:
        notes.append(f"the left wrist is {hand_off:.2f} cm off where it was written")
    steps = numpy.diff(angle)
    if (steps * numpy.sign(turns) <= 0).any():
        notes.append("the hand turns back on its way round")
    report("HAND", notes, f"the left hand's grip goes round the axle {abs(turns):.3f} times at {radius.min():.2f} to {radius.max():.2f} cm "
           f"(written {reel['radius']}), {on_rod[:, 0].min():.2f} to {on_rod[:, 0].max():.2f} cm out along it (written {reel['axle'][0]}), "
           f"the wrist within {hand_off:.3f} cm of where it was written")

    notes = []
    ends = ("head", "hand_r", "foot_l", "foot_r", "pelvis", reel["socket"], "clavicle_l")
    body_off = max(numpy.linalg.norm(cooked_world[:, index[bone], :3, 3] - base_world[:, index[bone], :3, 3], axis=1).max() for bone in ends)
    if body_off > NEAR_HAND_CM:
        notes.append(f"the body is {body_off:.2f} cm off the base")
    closes = numpy.abs(cooked[0] - cooked[-1]).max()
    if closes > 0.01:
        notes.append(f"the last key is {closes:.4f} off the first")
    report("BODY", notes, f"{', '.join(ends)} within {body_off:.3f} cm of the game's base in the character's space; "
           f"the loop closes (the last key is the first to {closes:.5f})")


check_keys(check_asset())
print("RESULT", "FAILED" if failed else "ALL OK")
sys.exit(1 if failed else 0)
