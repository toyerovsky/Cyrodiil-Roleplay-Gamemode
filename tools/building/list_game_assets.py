"""Lists every file path in an IoStore .utoc's directory index (UE5 FIoDirectoryIndexResource).

Usage: python -I tools/building/list_game_assets.py <OblivionRemastered-Windows.utoc> <paths.txt>
The game's .utoc (in OblivionRemastered/Content/Paks) is not encrypted.
The index is found by its mount point string, "../../../", then read as:
FString MountPoint, TArray<Dir{Name, FirstChild, NextSibling, FirstFile}>, TArray<File{Name, NextFile, UserData}>,
TArray<FString> StringTable. 0xFFFFFFFF is "none".
"""
import struct
import sys

NONE = 0xFFFFFFFF


def read_fstring(data, at):
    (length,) = struct.unpack_from("<i", data, at)
    at += 4
    if length == 0:
        return "", at
    if length > 0:
        text = data[at:at + length - 1].decode("latin-1")
        return text, at + length
    count = -length
    text = data[at:at + (count - 1) * 2].decode("utf-16-le")
    return text, at + count * 2


def parse(data, start):
    mount, at = read_fstring(data, start)
    (dir_count,) = struct.unpack_from("<i", data, at)
    at += 4
    dirs = [struct.unpack_from("<IIII", data, at + i * 16) for i in range(dir_count)]
    at += dir_count * 16
    (file_count,) = struct.unpack_from("<i", data, at)
    at += 4
    files = [struct.unpack_from("<III", data, at + i * 12) for i in range(file_count)]
    at += file_count * 12
    (string_count,) = struct.unpack_from("<i", data, at)
    at += 4
    strings = []
    for _ in range(string_count):
        text, at = read_fstring(data, at)
        strings.append(text)
    return mount, dirs, files, strings


def walk(dirs, files, strings):
    paths = []
    stack = [(0, "")]
    while stack:
        index, prefix = stack.pop()
        name_index, first_child, next_sibling, first_file = dirs[index]
        here = prefix if name_index == NONE else f"{prefix}{strings[name_index]}/"
        file_index = first_file
        while file_index != NONE:
            file_name, next_file, _ = files[file_index]
            paths.append(here + strings[file_name])
            file_index = next_file
        if first_child != NONE:
            stack.append((first_child, here))
        if next_sibling != NONE:
            stack.append((next_sibling, prefix))
    return paths


def main():
    source, out = sys.argv[1], sys.argv[2]
    data = open(source, "rb").read()
    marker = struct.pack("<i", 10) + b"../../../\x00"
    start = data.find(marker)
    if start < 0:
        sys.exit("no directory index mount point found")
    mount, dirs, files, strings = parse(data, start)
    paths = walk(dirs, files, strings)
    with open(out, "w", encoding="utf-8") as handle:
        for path in sorted(paths):
            handle.write(mount + path + "\n")
    print(f"{len(dirs)} dirs, {len(files)} files, {len(strings)} strings -> {len(paths)} paths")


main()
