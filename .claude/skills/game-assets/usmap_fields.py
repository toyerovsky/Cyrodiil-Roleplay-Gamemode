# Lists the fields a game's classes and structs have, from its mappings (.usmap, as UE4SS and CUE4Parse write them),
# each class after its supers: the layout the game reads properties saved by their place with.
# usage: python -I usmap_fields.py MAPPINGS.usmap OODLE_DLL Class [Class...]
#   OODLE_DLL is read only for mappings compressed with Oodle (oo2core_9_win64.dll in the research repo's tools/bin).
import ctypes
import io
import struct
import sys

MAGIC = 0x30C4
LONG_FNAME, LARGE_ENUMS, EXPLICIT_ENUM_VALUES = 2, 3, 4
NO_COMPRESSION, OODLE = 0, 1
TYPES = ["Byte", "Bool", "Int", "Float", "Object", "Name", "Delegate", "Double", "Array", "Struct", "Str", "Text",
         "Interface", "MulticastDelegate", "WeakObject", "LazyObject", "AssetObject", "SoftObject", "UInt64", "UInt32",
         "UInt16", "Int64", "Int16", "Int8", "Map", "Set", "Enum", "FieldPath", "Optional", "Utf8Str", "AnsiStr"]


def read(stream, form):
    return struct.unpack(form, stream.read(struct.calcsize(form)))[0]


def body(path, oodle_dll):
    stream = io.BytesIO(open(path, "rb").read())
    if read(stream, "<H") != MAGIC:
        raise SystemExit(f"{path} is no .usmap")
    version = read(stream, "<B")
    if version >= 1 and read(stream, "<i"):
        read(stream, "<i"), read(stream, "<i")
        for _ in range(read(stream, "<i")):
            stream.read(16), read(stream, "<i")
        read(stream, "<I")
    method = read(stream, "<B")
    compressed, size = read(stream, "<I"), read(stream, "<I")
    payload = stream.read(compressed)
    if method == NO_COMPRESSION:
        return version, payload
    if method == OODLE:
        out = ctypes.create_string_buffer(size)
        done = ctypes.WinDLL(oodle_dll).OodleLZ_Decompress(payload, compressed, out, size, 0, 0, 0, None, 0, None, None, None, 0, 3)
        if done != size:
            raise SystemExit(f"Oodle gave {done} bytes of {size}")
        return version, out.raw
    raise SystemExit(f"compression {method} is not read here")


def parse(version, data):
    stream = io.BytesIO(data)
    names = []
    for _ in range(read(stream, "<I")):
        length = read(stream, "<H") if version >= LONG_FNAME else read(stream, "<B")
        names.append(stream.read(length).decode("utf-8", "replace"))

    def name():
        index = read(stream, "<i")
        return None if index < 0 else names[index]

    for _ in range(read(stream, "<I")):
        name()
        for _ in range(read(stream, "<H") if version >= LARGE_ENUMS else read(stream, "<B")):
            if version >= EXPLICIT_ENUM_VALUES:
                read(stream, "<q")
            name()

    def kind():
        found = TYPES[read(stream, "<B")]
        if found == "Enum":
            inner = kind()
            return f"Enum<{name()}:{inner}>"
        if found == "Struct":
            return f"Struct<{name()}>"
        if found in ("Array", "Set", "Optional"):
            return f"{found}<{kind()}>"
        if found == "Map":
            return f"Map<{kind()},{kind()}>"
        return found

    classes = {}
    for _ in range(read(stream, "<I")):
        own, parent = name(), name()
        count, serialized = read(stream, "<H"), read(stream, "<H")
        fields = []
        for _ in range(serialized):
            index, dimension = read(stream, "<H"), read(stream, "<B")
            fields.append((index, dimension, name(), kind()))
        classes[own] = (parent, count, fields)
    return classes


def main():
    path, oodle_dll, *wanted = sys.argv[1:]
    classes = parse(*body(path, oodle_dll))
    for target in wanted:
        chain, current = [], target
        while current:
            chain.append(current)
            current = classes.get(current, (None,))[0]
        for own in reversed(chain):
            if own not in classes:
                print(f"== {own}: not in the mappings")
                continue
            parent, count, fields = classes[own]
            print(f"== {own} : {parent} ({count} fields)")
            for index, dimension, field, found in fields:
                print(f"   {index:3} {field} {found}" + (f" [{dimension}]" if dimension > 1 else ""))


main()
