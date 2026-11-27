"""Writes src/CyrodiilRP.Server/ShippedKits.cs: the game's house kits as the building catalogue lists them, from the list
of the game's assets list_game_assets.py writes.

Usage: python -I tools/building/make_kits.py <paths.txt> src/CyrodiilRP.Server/ShippedKits.cs
"""
import re
import sys

ARCHITECTURE = "../../../OblivionRemastered/Content/Art/Environment/Architecture/"

# key, name, folders under Architecture, use
KITS = [
    ("bruma-lower", "Bruma house, lower", ["bruma/Bruma_HouseLowerExt_Kit"], "Residential"),
    ("bruma-middle", "Bruma house, middle", ["bruma/Bruma_House_MiddleExt_Kit"], "Residential"),
    ("bruma-middle-inside", "Bruma house, middle, inside", ["bruma/Bruma_House_MiddleInt_Kit"], "Residential"),
    ("town-lower", "Town house, lower", ["lowerclass/LowerClass_Exterior_Kit"], "Residential"),
    ("town-lower-inside", "Town house, inside", ["lowerclass/houselowerinterior01_Kit", "lowerclass/houselowerinterior02_Kit",
                                                 "lowerclass/houselowerinterior03_Kit"], "Residential"),
    ("coastal-cottage-1", "Coastal cottage 1", ["anvil/Anvillchouse01"], "Residential"),
    ("coastal-cottage-2", "Coastal cottage 2", ["anvil/Anvillchouse02"], "Residential"),
    ("coastal-cottage-3", "Coastal cottage 3", ["anvil/Anvillchouse03"], "Residential"),
    ("coastal-cottage-inside", "Coastal cottage, inside", ["anvil/anvillchouse_interior"], "Residential"),
    ("coastal-middle", "Coastal town house", ["anvil/anvilhousemc"], "Residential"),
    ("coastal-middle-inside", "Coastal town house, inside", ["anvil/anvilhousemcinterior"], "Residential"),
]

# Collision and stand-in meshes, never drawn on their own.
SKIPPED = re.compile(r"(_COL|_PROXY|PROXY_|_LOD\d*)$", re.IGNORECASE)


def meshes_in(paths, folder):
    prefix = ARCHITECTURE + folder + "/"
    found = set()
    for path in paths:
        if not path.startswith(prefix) or not path.endswith(".uasset"):
            continue
        name = path[len(prefix):-len(".uasset")]
        if "/" not in name and name.startswith("SM_") and not SKIPPED.search(name):
            found.add(name)
    if not found:
        sys.exit(f"{folder}: no meshes")
    return sorted(found, key=str.lower)


def main():
    paths = open(sys.argv[1], encoding="utf-8").read().splitlines()
    lines = []
    total = 0
    longest = 0
    for key, name, folders, use in KITS:
        meshes = []
        for folder in folders:
            found = meshes_in(paths, folder)
            longest = max(longest, max(len(m) for m in found))
            meshes += [f"{folder}/{m}" for m in found]
        total += len(meshes)
        listed = ",\n".join(f'            "{m}"' for m in meshes)
        lines.append(f'        Kit("{key}", "{name}", PieceUse.{use},\n        [\n{listed},\n        ]),')
        print(f"{len(meshes):4} {key}")
    body = "\n".join(lines)
    text = f"""using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Server;

// The game's house kits the building catalogue ships: the meshes of the remaster's architecture its houses are built of,
// each by its folder under /Game/Art/Environment/Architecture. Written by tools/building/make_kits.py from the game's own
// list of assets, so a mesh named here is one the game has.
internal static class ShippedKits
{{
    public static List<KitSettings> All() =>
    [
{body}
    ];

    private static KitSettings Kit(string key, string name, PieceUse use, List<string> meshes) =>
        new() {{ Key = key, Name = name, Use = use, Meshes = meshes }};
}}
"""
    open(sys.argv[2], "w", encoding="utf-8", newline="\n").write(text)
    print(f"{total} meshes, the longest name {longest}")


main()
