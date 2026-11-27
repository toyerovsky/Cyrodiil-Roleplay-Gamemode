"""Regenerate level-zone shapes and the entrance inventory from local CC0 catalog exports."""

import argparse
import copy
import gzip
import hashlib
import json
import math
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / "src/CyrodiilRP.Server/config.json"
PLACES_DOCUMENT = ROOT / "docs/features/zone-places.md"
CELL_UNITS = 4096
TAMRIEL = "0000003C"
FALLBACK_SUFFIX = "-fallback"
MARKER_TYPES = {"cave", "mine", "fort ruin", "elven ruin", "camp", "oblivion gate"}
DUNGEON_TYPES = {"cave", "mine", "fort ruin", "elven ruin"}
STARTER_ZONES = {"imperial-city", "blue-road", "cheydinhal", "heartlands"}
CHEYDINHAL = [27, 22]


def numbered(prefix, first, last):
    return [f"{prefix}{number:02}" for number in range(first, last + 1)]


# Nibenay precedes Heartlands because both include Bravil. Kvatch precedes Gold Coast.
REGIONS = {
    "imperial-city": ["CityIsleRegion"],
    "blue-road": [],
    "imperial-sewers": [],
    "cheydinhal": numbered("NibenayBasinValleySubRegion", 1, 3)
    + numbered("JerallValusMountainsSubRegion", 4, 6),
    # The basin's southern subregion joins the valley, so the second band has room after the start.
    "nibenay": numbered("NibenayBasinValleySubRegion", 4, 6),
    "heartlands": ["Heartlands01"],
    "blackwood": numbered("BlackwoodSubRegion", 1, 4),
    "jerall": numbered("JerallValusMountainsSubRegion", 1, 3),
    "great-forest": numbered("GreatForestSubRegion", 1, 6),
    "colovian-highlands": numbered("ColovianHighlandsSubRegion", 1, 4),
    "west-weald": numbered("WestWealdSubRegion", 1, 3)
    + ["WestWealdSubRegion03A", "WestWealdSubRegion03B"],
    "kvatch": ["KvatchRegion"],
    "gold-coast": numbered("GoldCoastSubRegion", 1, 3),
    "oblivion": [],
}
OBLIVION_AREAS = [f"OblivionRD{number:03}" for number in range(1, 8)] + [
    "MS13CheydinhalOblivionWorld", "MQ10BrumaOblivionGate", "OblivionMQKvatch", "MQ14OblivionWorld"
]


# The Imperial City sewers are one dungeon of interior cells under the safe city (Pete, 27 Nov 2026).
SEWER_LEVELS = (10, 15)


def sewer_interiors(places):
    cells = [place for place in places if place["kind"] == "cell" and place.get("interior")
        and "sewer" in place["editorId"].lower() and not place["editorId"].startswith("Test")
        and place["references"]["total"] > 0 and place.get("outside")
        and (place.get("city") == "imperial-city" or TAMRIEL in place["outside"]["worldspaces"])]
    return sorted(place["formId"] for place in cells)


def read_catalog(path, key):
    with gzip.open(path, "rt", encoding="utf-8") as stream:
        catalog = json.load(stream)
    if catalog["schemaVersion"] != 1:
        raise ValueError(f"Unsupported catalog schema in {path}: {catalog['schemaVersion']}")
    return catalog, catalog[key]


def read_config():
    text = CONFIG.read_bytes().decode("utf-8")
    # Strip JSONC comments without interpreting // inside a JSON string as a comment.
    pattern = r'"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/'
    plain = re.sub(pattern, lambda match: match[0] if match[0].startswith('"') else "", text)
    return text, json.loads(plain)


def polygon(area):
    points = []
    for vertex in area["tes"]:
        if len(vertex) != 2 or not all(math.isfinite(value) for value in vertex):
            raise ValueError(f"Invalid TES vertex: {vertex}")
        point = [math.floor(value / CELL_UNITS) for value in vertex]
        if not points or point != points[-1]:
            points.append(point)
    if len(points) > 1 and points[-1] == points[0]:
        points.pop()
    if len({tuple(point) for point in points}) < 3:
        raise ValueError("Region collapses to fewer than three distinct cell vertices")
    return points


def generate_entries(current, regions, places):
    entries = {zone["id"]: copy.deepcopy(zone) for zone in current}
    for identifier, english, polish, minimum, maximum, origin, radius, strength in [
        ("heartlands", "Heartlands", "Centralne Równiny", 1, 6, [4, 15], 12, 1.0),
        ("colovian-highlands", "Colovian Highlands", "Wyżyna Colovia", 27, 34, [-17, 23], 25, 1.4),
    ]:
        if identifier not in entries:
            entries[identifier] = dict(id=identifier, name=dict(en=english, pl=polish), minLevel=minimum,
                maxLevel=maximum, mode="Gradient", npcHealth=strength, npcDamage=strength,
                origin=origin, radius=radius, shapes=[], areas=[])

    if "imperial-sewers" not in entries:
        entries["imperial-sewers"] = dict(id="imperial-sewers",
            name=dict(en="Imperial City Sewers", pl="Kanały Imperial City"), minLevel=SEWER_LEVELS[0],
            maxLevel=SEWER_LEVELS[1], mode="Fixed", npcHealth=1.0, npcDamage=1.0, shapes=[], areas=[])
    entries["imperial-sewers"]["interiors"] = sewer_interiors(places)

    fallbacks = [zone for zone in entries.values() if zone["id"].endswith(FALLBACK_SUFFIX)]
    if not fallbacks:
        for zone in current:
            if zone["shapes"]:
                fallback = copy.deepcopy(zone)
                fallback.pop("interiors", None)
                fallback["id"] += FALLBACK_SUFFIX
                fallback["areas"] = []
                fallbacks.append(fallback)

    by_editor_id = {region["editorId"]: region for region in regions}
    worldspaces = {place["editorId"]: place for place in places if place["kind"] == "worldspace"}
    for identifier, sources in REGIONS.items():
        zone = entries[identifier]
        if sources:
            zone["shapes"] = []
            for source in sources:
                region = by_editor_id[source]
                if region["worldspace"]["formId"] != TAMRIEL:
                    raise ValueError(f"Region {source} is not in Tamriel")
                if not region["areas"]:
                    raise ValueError(f"Region {source} has no outlines")
                zone["shapes"].extend(polygon(area) for area in region["areas"])
        if identifier == "imperial-city":
            districts = [name for name in worldspaces if name.startswith("IC") and "MQ16" not in name
                and worldspaces[name]["parent"] == TAMRIEL]
            zone["areas"] = list(dict.fromkeys(zone["areas"] + districts))
        if identifier == "oblivion":
            zone["areas"] = OBLIVION_AREAS.copy()
        for area in zone["areas"]:
            if area not in worldspaces:
                raise ValueError(f"Unknown worldspace: {area}")
    return [entries[identifier] for identifier in REGIONS] + fallbacks


def contains(shape, x, y):
    inside = False
    for start, end in zip(shape, shape[1:] + shape[:1]):
        cross = (x - start[0]) * (end[1] - start[1]) - (y - start[1]) * (end[0] - start[0])
        if cross == 0 and min(start[0], end[0]) <= x <= max(start[0], end[0]) \
                and min(start[1], end[1]) <= y <= max(start[1], end[1]):
            return True
        if (start[1] > y) != (end[1] > y) \
                and x < (end[0] - start[0]) * (y - start[1]) / (end[1] - start[1]) + start[0]:
            inside = not inside
    return inside


def find_zone(entries, cell):
    return next((zone for zone in entries if any(contains(shape, *cell) for shape in zone["shapes"])), None)


def cell_level(zone, cell):
    minimum, maximum = zone["minLevel"], zone["maxLevel"]
    match zone["mode"]:
        case "Fixed":
            coordinate_hash = ((cell[0] * 73856093) ^ (cell[1] * 19349663)) & 0xFFFFFFFF
            return minimum + coordinate_hash % (maximum - minimum + 1)
        case "Gradient":
            distance = math.dist(cell, zone["origin"])
            return minimum + math.floor((maximum - minimum) * min(distance / zone["radius"], 1) + 0.5)
        case "ScalesAbove" | "PartyLowest":
            return "party"
        case _:
            raise ValueError(f"Unknown zone mode: {zone['mode']}")


def interior_level(zone, form):
    # The server hashes an interior's form id as X with Y = 0, see ZoneCell.LevelKey.
    form_id = int(form, 16)
    signed = form_id - (1 << 32) if form_id >= 1 << 31 else form_id
    return cell_level(zone, [signed, 0])


def coverage(entries):
    points = [point for zone in entries for shape in zone["shapes"] for point in shape]
    counts = {zone["id"]: 0 for zone in entries}
    for x in range(min(point[0] for point in points), max(point[0] for point in points) + 1):
        for y in range(min(point[1] for point in points), max(point[1] for point in points) + 1):
            zone = find_zone(entries, [x, y])
            if zone:
                counts[zone["id"]] += 1
    return counts


def markdown(value):
    return str(value).replace("|", "\\|").replace("\n", " ")


def places_document(entries, places, weather_path, places_path, weather_catalog, places_catalog):
    rows = {zone["id"]: [] for zone in entries}
    for place in places:
        if place["kind"] != "marker" or place["location"]["worldspace"] != TAMRIEL:
            continue
        zone = find_zone(entries, place["location"]["grid"])
        if zone is None and place["type"] != "none":
            raise ValueError(f"Unzoned Tamriel marker: {place['editorId']} {place['location']['grid']}")
        if place["type"] in MARKER_TYPES:
            cell = place["location"]["grid"]
            rows[zone["id"]].append(dict(place=place, cell=cell, level=cell_level(zone, cell),
                distance=math.dist(cell, CHEYDINHAL)))
    candidates = sorted([row for identifier in STARTER_ZONES for row in rows[identifier]
        if row["place"]["type"] in DUNGEON_TYPES and "sewer" not in (row["place"]["name"] or "").lower()], key=lambda row: (row["level"], row["distance"], row["place"]["editorId"]))
    solo = {row["place"]["formId"] for row in candidates[:3]}
    group = {row["place"]["formId"] for row in candidates[3:6]}
    counts = coverage(entries)
    lines = ["# Zone places / Miejsca w strefach", "",
        "Generated by `tools/generate-zone-regions.py`. Source: [OB:MP asset catalog](https://catalog.obmp.readym.io/), CC0.", "",
        f"Exports: weather `{weather_catalog['generatedAt']}`, places `{places_catalog['generatedAt']}`.",
        f"SHA-256 weather.json.gz: `{hashlib.sha256(weather_path.read_bytes()).hexdigest()}`.",
        f"SHA-256 places.json.gz: `{hashlib.sha256(places_path.read_bytes()).hexdigest()}`.", "",
        "Tamriel entrance markers only, assigned once in config order with inclusive polygon borders.",
        "Levels use the server's coordinate hash or radial gradient. `party` means occupant-dependent scaling.",
        "These are entrance cell levels. Interior enemies and loot are not changed by this inventory,",
        "except zones that list interior cells (the Imperial City sewers), which level those interiors directly.", "",
        "Zone 1 candidates: dungeon types only, sorted by cell level, then distance in cells from Cheydinhal (27,22).",
        "The first three are solo candidates, the next three are 2-3 player candidates. These are planning suggestions,",
        "not verified encounter difficulty or a content change. Camps and gates remain listed, without dungeon recommendations.", "",
        "PL: Tylko znaczniki wejść w Tamriel. Poziom dotyczy komórki wejścia, nie wnętrza lochu.",
        "Kandydaci Strefy 1: najpierw najniższy poziom, potem odległość od Cheydinhal.",
        "Pierwsze trzy miejsca są propozycjami solo, kolejne trzy dla 2-3 graczy. Trudność wymaga testu w grze.",
        "Wyjścia z kanałów Imperial City nie są kandydatami: kanały to osobny dungeon na poziom 10-15.", "",
        "## Coverage / Pokrycie", "",
        "Distinct integer Tamriel cells actually won by each entry, after earlier entries. Worldspaces are excluded.", "",
        "| Zone / Strefa | Cells / Komórki | Markers / Znaczniki |", "|---|---:|---:|"]
    for zone in entries:
        identifier = zone["id"]
        lines.append(f"| `{identifier}` | {counts[identifier]} | {len(rows[identifier])} |")
    for zone in entries:
        identifier = zone["id"]
        lines += ["", f"## {identifier}: {zone['name']['en']} / {zone['name']['pl']}", "",
            "| Name / Nazwa | Type / Typ | Cell / Komórka | Level / Poziom | Zone 1 candidate / Kandydat |",
            "|---|---|---|---:|---|"]
        for row in sorted(rows[identifier], key=lambda row: (str(row["level"]).zfill(3), row["distance"], row["place"]["editorId"])):
            place = row["place"]
            candidate = "Solo / solo" if place["formId"] in solo else "2-3 players / graczy" if place["formId"] in group else ""
            name = place["name"] or place["editorId"]
            lines.append(f"| {markdown(name)} (`{place['formId']}`) | {place['type']} | {row['cell'][0]},{row['cell'][1]} | {row['level']} | {candidate} |")
        if not rows[identifier]:
            lines.append("| No markers / Brak znaczników | | | | |")
        if zone.get("interiors"):
            by_form = {place["formId"]: place for place in places if place["kind"] == "cell"}
            lines += ["", "| Interior cell / Komórka wnętrza | Form id | Level / Poziom |", "|---|---|---:|"]
            for form in zone["interiors"]:
                cell = by_form[form]
                level = interior_level(zone, form)
                lines.append(f"| {markdown(cell['name'] or cell['editorId'])} (`{cell['editorId']}`) | `{form}` | {level} |")
    return "\n".join(lines) + "\n"


def config_with_entries(text, entries):
    # The zones block is last. Preserve everything outside its entries, including JSONC comments.
    start = text.index('    "entries": [', text.index('  "zones": {'))
    encoded = json.dumps(entries, ensure_ascii=False, indent=2)
    encoded = re.sub(r'\[\s*(-?\d+),\s*(-?\d+)\s*\]', r'[\1, \2]', encoded)
    encoded = "\n".join("    " + line for line in encoded.splitlines())
    return text[:start] + '    "entries": ' + encoded.lstrip() + "\n  }\n}\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("weather", type=Path, help="Local weather.json.gz export (read only)")
    parser.add_argument("places", type=Path, help="Local places.json.gz export (read only)")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--write", action="store_true", help="Update config entries and zone-places.md")
    mode.add_argument("--check", action="store_true", help="Fail if config entries or zone-places.md differ")
    arguments = parser.parse_args()
    weather_catalog, regions = read_catalog(arguments.weather, "regions")
    places_catalog, places = read_catalog(arguments.places, "places")
    text, config = read_config()
    entries = generate_entries(config["zones"]["entries"], regions, places)
    document = places_document(entries, places, arguments.weather, arguments.places, weather_catalog, places_catalog)
    if arguments.write:
        CONFIG.write_bytes(config_with_entries(text, entries).encode("utf-8"))
        PLACES_DOCUMENT.write_bytes(document.encode("utf-8"))
        print(f"Wrote {len(entries)} entries and {PLACES_DOCUMENT.relative_to(ROOT)}")
    elif arguments.check:
        if entries != config["zones"]["entries"] or PLACES_DOCUMENT.read_bytes() != document.encode("utf-8"):
            raise ValueError("Generated entries or zone-places.md differ; run with --write")
        print(f"Verified {len(entries)} entries and zone-places.md")
    else:
        print(json.dumps({zone["id"]: {"shapes": zone["shapes"]} for zone in entries}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
