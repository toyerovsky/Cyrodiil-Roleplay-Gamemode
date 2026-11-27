"""For each DOOR of a TES4 master, how many placed references teleport (XTEL) and how many do not: a door the game only
ever places without a teleport is one it animates open and shut, which the building catalogue's doors are.

Usage: python -I tools/building/door_uses.py <Oblivion.esm, in OblivionRemastered/Content/Dev/ObvData/Data>
"""
import struct
import sys
import zlib

data = open(sys.argv[1], "rb").read()
doors = {}
uses = {}


def fields_of(body):
    at, out = 0, {}
    while at + 6 <= len(body):
        kind = body[at:at + 4].decode("latin-1")
        (size,) = struct.unpack_from("<H", body, at + 4)
        out.setdefault(kind, body[at + 6:at + 6 + size])
        at += 6 + size
    return out


def walk(start, end):
    at = start
    while at < end:
        kind = data[at:at + 4].decode("latin-1")
        size, flags, form = struct.unpack_from("<III", data, at + 4)
        if kind == "GRUP":
            walk(at + 20, at + size)
            at += size
            continue
        if kind in ("DOOR", "REFR"):
            body = data[at + 20:at + 20 + size]
            if flags & 0x00040000:
                body = zlib.decompress(body[4:])
            f = fields_of(body)
            if kind == "DOOR":
                text = lambda k: f.get(k, b"").split(b"\0", 1)[0].decode("latin-1")
                doors[form] = (text("EDID"), text("FULL"), text("MODL"), f.get("FNAM", b"\0")[0])
            elif "NAME" in f:
                (base,) = struct.unpack_from("<I", f["NAME"], 0)
                swing, load = uses.get(base, (0, 0))
                uses[base] = (swing, load + 1) if "XTEL" in f else (swing + 1, load)
        at += 20 + size


walk(0, len(data))
rows = []
for form, (edid, name, model, flags) in doors.items():
    swing, load = uses.get(form, (0, 0))
    rows.append((swing, load, form, edid, name, model, flags))
for swing, load, form, edid, name, model, flags in sorted(rows, reverse=True):
    print(f"{swing:5} {load:5} {form:08X} {flags:#04x} {edid:40} {name:22} {model}")
