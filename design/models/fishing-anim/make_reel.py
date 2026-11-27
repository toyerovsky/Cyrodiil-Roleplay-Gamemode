# Makes the angler's reeling animation from code, for Blender 4.2 (its Python brings numpy, and it writes the FBX):
#   blender -b --factory-startup -P make_reel.py -- SKELETON_JSON UE_DIR OUT_DIR [--no-skeleton] [--comfort]
#   SKELETON_JSON  the game's SKEL_HumanoidSkeleton as UeExport's `json` command writes it (research repo)
#   UE_DIR         what UeExport's `anims` command wrote for the base sequence (sequences.tsv and its .anim.bin)
# Writes into OUT_DIR:
#   authored/             the same keys as UeExport decodes a cooked sequence (sequences.tsv, A_Fishing_Reel.anim.bin)
#   A_Fishing_Reel.json   every key of every bone track, in Unreal's space (centimetres, quaternions x y z w), with the
#                         game skeleton's bones, virtual bones, sockets and guid, for the scripts in unreal/
#   SKEL_HumanoidSkeleton_<n>.fbx  the game's skeleton as meshes of one triangle, for the stand-in skeleton the Unreal
#                         project keeps at the game skeleton's path (never shipped): imported one after another they
#                         give Unreal's skeleton the game's bone order (see BATCHES)
#
# The animation is the game's A_Humanoid_Staff_Block_Idle, the pose the client holds through a fight, with the left arm
# taken off the rod's butt and put on the reel's handle: the hand goes round the reel's axle TURNS times, the upper arm
# and the forearm follow by two-bone IK, and the arm's helper bones are keyed by the rules the game's own animations
# show (fitted over 300 of them: the twist bones take 0.8 and 0.2 of a twist, the corrective roots turn half way back).
# Every other bone keeps the base's keys, so only the left arm differs and the pose layers over the base cleanly. That
# goes for the bulge bones under the corrective roots and the wrist's two helpers too: they keep the keys of the base's
# arm (its elbow bent 105 degrees, here 64 to 84), which the game's rig would key anew.
import csv
import json
import math
import os
import sys

import numpy

ARGS = sys.argv[sys.argv.index("--") + 1:]
SKELETON_JSON, UE_DIR, OUT = (os.path.abspath(path) for path in ARGS[:3])
os.makedirs(OUT, exist_ok=True)

NAME = "A_Fishing_Reel"
BASE = "/Game/Art/Animation/Humanoid/ThirdPerson/Staff/Combat/Block/A_Humanoid_Staff_Block_Idle.A_Humanoid_Staff_Block_Idle"
SKELETON = "/Game/Art/Character/Humanoid/SKEL_HumanoidSkeleton"
TARGET = f"/Game/CyrodiilRP/Fishing/{NAME}.{NAME}"
RATE = 30
# Whole turns of the handle over the loop, so its last key is its first: 6 in 3 s, two a second.
TURNS = 6

# The rod on the hand, as the client hangs it (AnglerLooks.HandSocket, RodOffset, RodRotation): on Weapon_Socket, which
# is the bone Weapon_Anchor with no offset of its own, half a turn about the rod's length (pitch) and 10 cm toward the tip.
SOCKET_BONE = "Weapon_Anchor"
ROD_OFFSET = (0.0, -10.0, 0.0)
# The reel on a rod's mesh, in Unreal's centimetres (make_rods.py, centrepin: Blender's x, y, z in metres are Unreal's
# x, -y, z in centimetres). The three rods differ by a centimetre, these are their means: the axle crosses the rod
# along X, 24 cm behind the mesh's origin and 7 cm under the rod, the handle's knob stands on the +X face and goes
# round 2.75 cm from the axle.
AXLE_Y, AXLE_Z = 24.0, -7.0
CRANK_RADIUS = 2.75
# How far out along the axle the index finger's knuckle is held: the knob spans 1.85 to 4.35 cm, the reel's face ends
# at 1.85, and a finger is about 1 cm thick either way.
KNUCKLE_X = 3.3
# The hand's turn about the axle, in degrees, and where on the circle the loop starts. `--comfort` lists every turn with
# how far it bends the wrist: 210 bends it least, and reads well on the previews.
HAND_TURN = 210
START_ANGLE = 90.0
# The elbow's side: the IK bends the arm toward this point of the character's space (left, a little forward, low).
ELBOW_HINT = (45.0, 15.0, 95.0)
# The arm never straightens further than this share of its length.
LONGEST_REACH = 0.985

ARM = ("upperarm_l", "lowerarm_l", "hand_l")
# The game's rig keys these from the arm's three bones. Fitted over 900 poses of 300 of the game's sequences:
# a twist bone turns about its length by a share of its driver's twist from the reference pose (rms error under half a
# degree), a corrective root turns back by a share of each part of its driver's rotation vector.
TWISTS = {"lowerarm_twist_01_l": ("hand_l", 0.8), "lowerarm_twist_02_l": ("hand_l", 0.2),
          "upperarm_twist_01_l": ("upperarm_l", -0.8), "upperarm_twist_02_l": ("upperarm_l", -0.2)}
CORRECTIVE_ROOTS = {"upperarm_correctiveRoot_l": ("upperarm_l", (-0.98, -0.5, -0.5)),
                    "lowerarm_correctiveRoot_l": ("lowerarm_l", (-0.71, -0.5, -0.5))}

# The game's skeleton grew in five steps: a first mesh's bones in the order a depth-first walk gives, then four times
# the bones another mesh added, each time at the end. Unreal's FBX import orders a mesh's bones by such a walk and
# adds what a later mesh brings at the end too, so importing meshes with the first 371, 380, 389, 394 and all 401
# bones, in this order, gives the stand-in the game's order. unreal/import_reel.py refuses any other.
BATCHES = (371, 380, 389, 394, 401)


# --- quaternions (x y z w) and transforms as Unreal has them ---------------------------------------------------------

def quat_multiply(a, b):
    # b turned first, then a.
    ax, ay, az, aw = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    bx, by, bz, bw = b[..., 0], b[..., 1], b[..., 2], b[..., 3]
    return numpy.stack([aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
                        aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz], axis=-1)


def quat_inverse(q):
    return q * numpy.array([-1.0, -1.0, -1.0, 1.0])


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


def matrix_quat(m):
    # One rotation matrix to a quaternion.
    trace = m[0, 0] + m[1, 1] + m[2, 2]
    if trace > 0:
        s = math.sqrt(trace + 1.0) * 2
        q = [(m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, 0.25 * s]
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        q = [0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s]
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        q = [(m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s]
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        q = [(m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s, (m[1, 0] - m[0, 1]) / s]
    q = numpy.array(q)
    return q / numpy.linalg.norm(q)


def rotation_vector(q):
    # The axis times the angle in radians, the short way round.
    q = q if q[3] >= 0 else -q
    half = math.acos(min(1.0, q[3]))
    s = math.sin(half)
    return numpy.zeros(3) if s < 1e-9 else q[:3] / s * 2 * half


def vector_quat(v):
    angle = numpy.linalg.norm(v)
    if angle < 1e-12:
        return numpy.array([0.0, 0.0, 0.0, 1.0])
    return numpy.concatenate([v / angle * math.sin(angle / 2), [math.cos(angle / 2)]])


def twist_about_x(q):
    # Radians of the part of q that turns about X.
    q = q if q[3] >= 0 else -q
    return 2 * math.atan2(q[0], q[3])


def about_x(angle):
    c, s = math.cos(angle), math.sin(angle)
    return numpy.array([[1.0, 0.0, 0.0], [0.0, c, -s], [0.0, s, c]])


def compose(local):
    m = numpy.zeros(local.shape[:-1] + (4, 4))
    m[..., :3, :3] = quat_matrix(local[..., :4]) * local[..., None, 7:]
    m[..., :3, 3] = local[..., 4:7]
    m[..., 3, 3] = 1.0
    return m


def unit(v):
    return v / numpy.linalg.norm(v)


# --- the game's skeleton and the base animation ----------------------------------------------------------------------

class Skeleton:
    def __init__(self, path):
        with open(path, encoding="utf-8") as source:
            exports = json.load(source)
        asset = next(export for export in exports if export["Type"] == "Skeleton")
        reference = asset["ReferenceSkeleton"]
        self.names = [bone["Name"] for bone in reference["FinalRefBoneInfo"]]
        self.parents = [bone["ParentIndex"] for bone in reference["FinalRefBoneInfo"]]
        self.index = {name: i for i, name in enumerate(self.names)}
        self.rest = numpy.array([[pose["Rotation"][k] for k in "XYZW"] + [pose["Translation"][k] for k in "XYZ"]
                                 + [pose["Scale3D"][k] for k in "XYZ"] for pose in reference["FinalRefBonePose"]])
        self.guid = asset["Guid"].replace("-", "")
        self.virtual = [[bone["SourceBoneName"], bone["TargetBoneName"], bone["VirtualBoneName"]]
                        for bone in asset["Properties"].get("VirtualBones", [])]
        # Each socket's name and bone, in the skeleton's order: a reference's path ends in its export's place in the file.
        self.sockets = [[exports[int(socket["ObjectPath"].rsplit(".", 1)[1])]["Properties"][key] for key in ("SocketName", "BoneName")]
                        for socket in asset["Properties"].get("Sockets", [])]
        if len(self.names) != BATCHES[-1]:
            raise SystemExit(f"The skeleton has {len(self.names)} bones, BATCHES ends at {BATCHES[-1]}: the game's skeleton changed.")

    def world(self, local):
        # local: (keys, bones, 10). A parent always comes before its children.
        m = compose(local)
        out = numpy.empty_like(m)
        for bone, parent in enumerate(self.parents):
            out[:, bone] = m[:, bone] if parent < 0 else out[:, parent] @ m[:, bone]
        return out


def read_base(skeleton):
    with open(os.path.join(UE_DIR, "sequences.tsv"), encoding="utf-8") as listing:
        rows = [row for row in csv.DictReader(listing, delimiter="\t") if row["sequence"] == BASE and not row["error"]]
    if not rows:
        raise SystemExit(f"{BASE} is not decoded in {UE_DIR}: run UeExport anims on it.")
    row = rows[-1]
    bones = row["bones"].split(",")
    keys = int(row["keys"])
    data = numpy.fromfile(os.path.join(UE_DIR, row["anim"]), dtype="<f4").reshape(keys, len(bones), 10).astype(numpy.float64)
    # UeExport's reader leaves a quaternion up to 0.17% short of unit length at some keys (the same keys for every
    # bone). Unreal takes a key's quaternion as it is given, and turns one that is not of unit length into another
    # rotation, 7 degrees off: every key is brought to unit length here.
    data[..., :4] /= numpy.linalg.norm(data[..., :4], axis=-1, keepdims=True)
    # One hemisphere from key to key, as the game's own keys are once decoded.
    for key in range(1, keys):
        flip = numpy.sum(data[key, :, :4] * data[key - 1, :, :4], axis=1) < 0
        data[key, flip, :4] *= -1
    return bones, data, float(row["length"])


# --- the left arm ----------------------------------------------------------------------------------------------------

class Reel:
    def __init__(self, skeleton, bones, data):
        self.skeleton, self.bones = skeleton, bones
        self.track = {bone: i for i, bone in enumerate(bones)}
        self.keys = len(data)
        self.base = data
        # Every bone's local transform at every key: the base's keys, the reference pose where it has none.
        self.local = numpy.repeat(skeleton.rest[None], self.keys, axis=0)
        self.local[:, [skeleton.index[bone] for bone in bones]] = data
        self.world = skeleton.world(self.local)
        index = skeleton.index
        # The rod's mesh in the character's space: the socket, the offset, half a turn of pitch (x and z turned over).
        hang = numpy.eye(4)
        hang[:3, :3] = numpy.diag([-1.0, 1.0, -1.0])
        hang[:3, 3] = ROD_OFFSET
        self.rod = self.world[:, index[SOCKET_BONE]] @ hang[None]
        self.shoulder = self.world[:, index["upperarm_l"], :3, 3]
        self.upper = float(numpy.linalg.norm(data[0, self.track["lowerarm_l"], 4:7]))
        self.fore = float(numpy.linalg.norm(data[0, self.track["hand_l"], 4:7]))
        self.grip, self.grip_axis = self.base_grip()

    def base_grip(self):
        # How the base pose's left hand holds the staff, in the hand's own space: the point of the staff's axis beside
        # the index finger's knuckle, and the axis from the little finger's side to the index finger's.
        index, world = self.skeleton.index, self.world[0]
        origin, along = self.rod[0, :3, 3], self.rod[0, :3, 1]
        knuckle = world[index["index_01_l"], :3, 3]
        little = world[index["pinky_01_l"], :3, 3]
        point = origin + along * ((knuckle - origin) @ along)
        if (knuckle - little) @ along < 0:
            along = -along
        hand = world[index["hand_l"]]
        inverse = numpy.linalg.inv(hand)
        return (inverse @ numpy.append(point, 1.0))[:3], unit(inverse[:3, :3] @ along)

    def hand_on_rod(self, turn):
        # The hand's axes on the rod's mesh: its grip along the axle with the index finger toward the reel (the mesh's
        # -X from the knob), turned about the axle by `turn` degrees.
        a = self.grip_axis
        target = numpy.array([-1.0, 0.0, 0.0])
        side = unit(numpy.cross(a, [0.0, 0.0, 1.0]) if abs(a[2]) < 0.9 else numpy.cross(a, [0.0, 1.0, 0.0]))
        source = numpy.stack([a, side, numpy.cross(a, side)], axis=1)
        aimed = numpy.stack([target, [0.0, 1.0, 0.0], numpy.cross(target, [0.0, 1.0, 0.0])], axis=1)
        return about_x(math.radians(turn)) @ aimed @ source.T

    def crank(self, key):
        # Where the index finger's knuckle is at a key, on the rod's mesh.
        angle = math.radians(START_ANGLE) + 2 * math.pi * TURNS * key / (self.keys - 1)
        return numpy.array([KNUCKLE_X, AXLE_Y + CRANK_RADIUS * math.cos(angle), AXLE_Z + CRANK_RADIUS * math.sin(angle), 1.0])

    def solve(self, key, turn):
        # The arm's three bones in the character's space at a key: rotation matrices of the upper arm, the forearm and
        # the hand, and how far the wrist is from the shoulder as a share of the arm's length.
        rod = self.rod[key]
        hand = rod[:3, :3] @ self.hand_on_rod(turn)
        wrist = (rod @ self.crank(key))[:3] - hand @ self.grip
        shoulder = self.shoulder[key]
        reach = numpy.linalg.norm(wrist - shoulder)
        longest = (self.upper + self.fore) * LONGEST_REACH
        share = reach / (self.upper + self.fore)
        if reach > longest:
            # Out of reach: the wrist comes back toward the shoulder, and the hand with it.
            wrist = shoulder + (wrist - shoulder) * (longest / reach)
            reach = longest
        to_wrist = (wrist - shoulder) / reach
        # The elbow lies in the plane the hint gives, where the two bones' lengths put it.
        hint = numpy.array(ELBOW_HINT) - shoulder
        out = unit(hint - to_wrist * (hint @ to_wrist))
        near = (self.upper ** 2 - self.fore ** 2 + reach ** 2) / (2 * reach)
        elbow = shoulder + to_wrist * near + out * math.sqrt(max(self.upper ** 2 - near ** 2, 0.0))
        # Both bones run along their X, and the elbow is a hinge about Z (the reference pose and the game's keys turn
        # the forearm about Z alone): Y is the side the forearm folds to.
        upper_x = (elbow - shoulder) / self.upper
        fore_x = (wrist - elbow) / self.fore
        upper_y = unit(fore_x - upper_x * (fore_x @ upper_x))
        hinge = numpy.cross(upper_x, upper_y)
        upper = numpy.stack([upper_x, upper_y, hinge], axis=1)
        fore = numpy.stack([fore_x, numpy.cross(hinge, fore_x), hinge], axis=1)
        return upper, fore, hand, share

    def comfort(self):
        # The hand's turn about the axle that bends the wrist least over the loop, from the reference pose's straight
        # wrist. A turn of the forearm (the twist about its length) costs a third of a bend.
        rest = self.skeleton.rest[self.skeleton.index["hand_l"], :4]
        best = None
        for turn in range(0, 360, 5):
            worst, reach = 0.0, 0.0
            for key in range(0, self.keys - 1, 3):
                upper, fore, hand, share = self.solve(key, turn)
                local = quat_multiply(quat_inverse(rest), matrix_quat(fore.T @ hand))
                twist = twist_about_x(local)
                swing = quat_multiply(local, quat_inverse(vector_quat(numpy.array([twist, 0.0, 0.0]))))
                bend = numpy.linalg.norm(rotation_vector(swing))
                worst = max(worst, math.degrees(bend) + abs(math.degrees(twist)) / 3)
                reach = max(reach, share)
            print(f"  hand turn {turn:3d}: cost {worst:6.1f}, reach {reach:.3f}")
            if reach < LONGEST_REACH and (best is None or worst < best[0]):
                best = (worst, turn)
        if best is None:
            raise SystemExit("The left hand reaches the reel at no turn of the hand.")
        return best[1]

    def keyed(self, turn):
        # The base's keys with the left arm's replaced. Returns them track by track, as the base orders them.
        skeleton, index = self.skeleton, self.skeleton.index
        local = self.local.copy()
        parent_world = self.world[:, skeleton.parents[index["upperarm_l"]], :3, :3]
        for key in range(self.keys):
            upper, fore, hand, _ = self.solve(key, turn)
            # The parent's axes carry the clavicle's scale: taken out, a rotation is left.
            parent = parent_world[key] / numpy.linalg.norm(parent_world[key], axis=0, keepdims=True)
            local[key, index["upperarm_l"], :4] = matrix_quat(parent.T @ upper)
            local[key, index["lowerarm_l"], :4] = matrix_quat(upper.T @ fore)
            local[key, index["hand_l"], :4] = matrix_quat(fore.T @ hand)
            for bone, (driver, share) in TWISTS.items():
                rest = skeleton.rest[index[bone], :4]
                moved = quat_multiply(quat_inverse(skeleton.rest[index[driver], :4]), local[key, index[driver], :4])
                turned = vector_quat(numpy.array([twist_about_x(moved) * share, 0.0, 0.0]))
                local[key, index[bone], :4] = quat_multiply(rest, turned)
            for bone, (driver, shares) in CORRECTIVE_ROOTS.items():
                rest = skeleton.rest[index[bone], :4]
                moved = quat_multiply(quat_inverse(skeleton.rest[index[driver], :4]), local[key, index[driver], :4])
                local[key, index[bone], :4] = quat_multiply(rest, vector_quat(rotation_vector(moved) * numpy.array(shares)))
        changed = list(ARM) + list(TWISTS) + list(CORRECTIVE_ROOTS)
        missing = [bone for bone in changed if bone not in self.track]
        if missing:
            raise SystemExit(f"The base has no track for {missing}.")
        for bone in changed:
            keys = local[:, index[bone], :4]
            for key in range(1, self.keys):
                if keys[key] @ keys[key - 1] < 0:
                    keys[key] *= -1
        # The loop closes: the last key is the first, as the base's is to a ten-thousandth.
        tracks = local[:, [index[bone] for bone in self.bones]].copy()
        tracks[-1] = tracks[0]
        return tracks, changed, local

    def report(self, tracks, changed, local, turn):
        skeleton, index = self.skeleton, self.skeleton.index
        world = skeleton.world(local)
        held = world[:, index["hand_l"]] @ numpy.append(self.grip, 1.0)
        on_rod = numpy.einsum("kij,kj->ki", numpy.linalg.inv(self.rod), held)[:, :3]
        radius = numpy.hypot(on_rod[:, 1] - AXLE_Y, on_rod[:, 2] - AXLE_Z)
        elbow = numpy.degrees(2 * numpy.arctan2(local[:, index["lowerarm_l"], 2], local[:, index["lowerarm_l"], 3]))
        reach = numpy.linalg.norm(world[:, index["hand_l"], :3, 3] - self.shoulder, axis=1) / (self.upper + self.fore)
        print(f"hand turn {turn} degrees, {TURNS} turns over {self.keys} keys")
        print(f"the hand's grip goes round the axle at {radius.min():.2f} to {radius.max():.2f} cm, {on_rod[:, 0].min():.2f} to {on_rod[:, 0].max():.2f} cm out")
        print(f"elbow bend {elbow.min():.0f} to {elbow.max():.0f} degrees (the base: "
              f"{math.degrees(2 * math.atan2(self.base[0, self.track['lowerarm_l'], 2], self.base[0, self.track['lowerarm_l'], 3])):.0f}), "
              f"reach {reach.min():.3f} to {reach.max():.3f} of the arm")
        same = [bone for bone in self.bones if bone not in changed]
        off = numpy.abs(tracks[:-1, [self.track[bone] for bone in same]] - self.base[:-1, [self.track[bone] for bone in same]]).max()
        print(f"{len(changed)} tracks changed ({', '.join(changed)}), {len(same)} as the base (largest difference {off:.2g})")
        # The rules for the helper bones, held to the base's own keys.
        for bone, (driver, share) in TWISTS.items():
            rest = skeleton.rest[index[bone], :4]
            moved = quat_multiply(quat_inverse(skeleton.rest[index[driver], :4]), self.base[0, self.track[driver], :4])
            ruled = quat_multiply(rest, vector_quat(numpy.array([twist_about_x(moved) * share, 0.0, 0.0])))
            error = numpy.linalg.norm(rotation_vector(quat_multiply(quat_inverse(ruled), self.base[0, self.track[bone], :4])))
            print(f"  rule for {bone}: {math.degrees(error):.1f} degrees off the base's own key")
        for bone, (driver, shares) in CORRECTIVE_ROOTS.items():
            rest = skeleton.rest[index[bone], :4]
            moved = quat_multiply(quat_inverse(skeleton.rest[index[driver], :4]), self.base[0, self.track[driver], :4])
            ruled = quat_multiply(rest, vector_quat(rotation_vector(moved) * numpy.array(shares)))
            error = numpy.linalg.norm(rotation_vector(quat_multiply(quat_inverse(ruled), self.base[0, self.track[bone], :4])))
            print(f"  rule for {bone}: {math.degrees(error):.1f} degrees off the base's own key")


def write_keys(skeleton, bones, tracks, changed, length, reel):
    keys = len(tracks)
    made = {
        "name": NAME,
        "base": BASE,
        "rate": RATE,
        "keys": keys,
        "length": length,
        "changed": changed,
        # What check_reel.py holds the cooked keys to: the rod on its socket and the hand's way round the axle.
        "reel": {"socket": SOCKET_BONE, "offset": list(ROD_OFFSET), "axle": [KNUCKLE_X, AXLE_Y, AXLE_Z], "radius": CRANK_RADIUS,
                 "turns": TURNS, "grip": reel.grip.tolist()},
        "skeleton": {"path": SKELETON, "guid": skeleton.guid, "bones": [[name, parent] for name, parent in zip(skeleton.names, skeleton.parents)],
                     "virtual": skeleton.virtual, "sockets": skeleton.sockets},
        "tracks": [{"bone": bone,
                    "rotation": numpy.round(tracks[:, i, :4], 7).tolist(),
                    "translation": numpy.round(tracks[:, i, 4:7], 5).tolist(),
                    "scale": numpy.round(tracks[:, i, 7:], 6).tolist()} for i, bone in enumerate(bones)],
    }
    if abs((keys - 1) / RATE - length) > 1e-6:
        raise SystemExit(f"{keys} keys at {RATE} a second are not {length} s.")
    path = os.path.join(OUT, f"{NAME}.json")
    with open(path, "w", encoding="utf-8", newline="\n") as target:
        json.dump(made, target, separators=(",", ":"))
    print(f"{path}: {len(bones)} tracks of {keys} keys")
    # The same keys as UeExport's `anims` command writes a decoded sequence, so preview_reel.py and check_reel.py take
    # the authored animation and the cooked one alike.
    authored = os.path.join(OUT, "authored")
    os.makedirs(authored, exist_ok=True)
    tracks.astype("<f4").tofile(os.path.join(authored, f"{NAME}.anim.bin"))
    row = [TARGET, f"{SKELETON}.{SKELETON.split('/')[-1]}", keys, RATE, length, "", f"{NAME}.anim.bin", ",".join(bones), ""]
    with open(os.path.join(authored, "sequences.tsv"), "w", encoding="utf-8", newline="\n") as listing:
        listing.write("\t".join(("sequence", "skeleton", "keys", "fps", "length", "additive", "anim", "bones", "error")) + "\n")
        listing.write("\t".join(str(value) for value in row) + "\n")


# --- the stand-in skeleton -------------------------------------------------------------------------------------------

def write_skeletons(skeleton):
    import bpy
    from mathutils import Matrix

    world = skeleton.world(skeleton.rest[None])[0]
    # Unreal's (x, y, z) is Blender's (x, -y, z). The scene's unit is the centimetre, so the FBX carries no scale.
    mirror = numpy.diag([1.0, -1.0, 1.0, 1.0])
    for number, count in enumerate(BATCHES, start=1):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = bpy.context.scene
        scene.unit_settings.system = "METRIC"
        scene.unit_settings.scale_length = 0.01
        # Unreal's importer drops a root node of this name, which Blender's exporter puts over the bones.
        armature = bpy.data.objects.new("Armature", bpy.data.armatures.new("Armature"))
        scene.collection.objects.link(armature)
        bpy.context.view_layer.objects.active = armature
        bpy.ops.object.mode_set(mode="EDIT")
        made = []
        # In the skeleton's order: a parent's children then lie in it too, which is the order the walk takes them in.
        for bone in range(count):
            edit = armature.data.edit_bones.new(skeleton.names[bone])
            edit.head, edit.tail = (0.0, 0.0, 0.0), (0.0, 1.0, 0.0)
            matrix = mirror @ world[bone] @ mirror
            # Scale is left out of a bone's rest: the reference pose has it on a few helper bones, the keys bring it.
            matrix[:3, :3] /= numpy.linalg.norm(matrix[:3, :3], axis=0, keepdims=True)
            edit.matrix = Matrix(matrix.tolist())
            edit.length = 1.0
            if skeleton.parents[bone] >= 0:
                edit.parent = made[skeleton.parents[bone]]
            edit.use_connect = False
            made.append(edit)
        bpy.ops.object.mode_set(mode="OBJECT")
        mesh = bpy.data.meshes.new("Stand")
        mesh.from_pydata([(-5.0, 0.0, 0.0), (5.0, 0.0, 0.0), (0.0, 0.0, 10.0)], [], [(0, 1, 2)])
        stand = bpy.data.objects.new("Stand", mesh)
        scene.collection.objects.link(stand)
        stand.vertex_groups.new(name=skeleton.names[0]).add([0, 1, 2], 1.0, "REPLACE")
        stand.parent = armature
        stand.modifiers.new("Armature", "ARMATURE").object = armature
        path = os.path.join(OUT, f"SKEL_HumanoidSkeleton_{number}.fbx")
        bpy.ops.export_scene.fbx(filepath=path, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, use_armature_deform_only=False,
                                 primary_bone_axis="Y", secondary_bone_axis="X", bake_anim=False, apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE")
        print(f"{path}: {count} bones")


skeleton = Skeleton(SKELETON_JSON)
bones, data, length = read_base(skeleton)
reel = Reel(skeleton, bones, data)
turn = reel.comfort() if "--comfort" in ARGS else HAND_TURN
tracks, changed, local = reel.keyed(turn)
reel.report(tracks, changed, local, turn)
write_keys(skeleton, bones, tracks, changed, length, reel)
if "--no-skeleton" not in ARGS:
    write_skeletons(skeleton)
