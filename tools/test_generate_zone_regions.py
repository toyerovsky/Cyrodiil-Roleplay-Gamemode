import copy
import importlib.util
from pathlib import Path
import unittest


SPECIFICATION = importlib.util.spec_from_file_location("zone_regions", Path(__file__).with_name("generate-zone-regions.py"))
zones = importlib.util.module_from_spec(SPECIFICATION)
SPECIFICATION.loader.exec_module(zones)


class ZoneRegionTests(unittest.TestCase):
    def test_conversion_floors_negative_units_and_keeps_vertex_order(self):
        self.assertEqual([[-1, -2], [1, 0], [0, 2]],
            zones.polygon({"tes": [[-1, -4097], [4096, 0], [0, 8192]]}))

    def test_only_consecutive_duplicates_and_the_closing_duplicate_are_removed(self):
        self.assertEqual([[0, 0], [2, 0], [2, 2], [0, 0], [0, 2]],
            zones.polygon({"tes": [[0, 0], [1, 1], [8192, 0], [8192, 8192],
                [0, 0], [0, 8192], [0, 0]]}))

    def test_collapsed_regions_fail_instead_of_disappearing(self):
        with self.assertRaisesRegex(ValueError, "three distinct"):
            zones.polygon({"tes": [[0, 0], [1, 1], [8192, 0]]})

    def test_malformed_and_nonfinite_vertices_fail(self):
        for vertex in [[1, 2, 3], [float("nan"), 0], [0, float("inf")]]:
            with self.subTest(vertex=vertex), self.assertRaisesRegex(ValueError, "Invalid TES"):
                zones.polygon({"tes": [vertex]})

    def test_concave_shapes_and_inclusive_edges_match_the_server(self):
        shape = [[0, 0], [6, 0], [6, 2], [2, 2], [2, 6], [0, 6]]
        for polygon in [shape, list(reversed(shape))]:
            self.assertTrue(zones.contains(polygon, 1, 5))
            self.assertTrue(zones.contains(polygon, 6, 2))
            self.assertTrue(zones.contains(polygon, 0, 0))
            self.assertFalse(zones.contains(polygon, 4, 4))

    def test_gradient_rounds_midpoints_away_from_zero_and_clamps_distance(self):
        zone = dict(minLevel=1, maxLevel=6, mode="Gradient", origin=[0, 0], radius=10)
        self.assertEqual(1, zones.cell_level(zone, [0, 0]))
        self.assertEqual(2, zones.cell_level(zone, [-1, 0]))
        self.assertEqual(6, zones.cell_level(zone, [50, 0]))

    def test_fixed_hash_wraps_unsigned_at_negative_coordinates(self):
        zone = dict(minLevel=1, maxLevel=4, mode="Fixed")
        self.assertEqual(4, zones.cell_level(zone, [-1, 0]))
        self.assertEqual(2, zones.cell_level(zone, [0, -1]))
        self.assertEqual(1, zones.cell_level(zone, [0, 0]))

    def test_both_scaling_modes_report_party_and_unknown_modes_fail(self):
        for mode in ["ScalesAbove", "PartyLowest"]:
            self.assertEqual("party", zones.cell_level(dict(minLevel=35, maxLevel=40, mode=mode), [0, 0]))
        with self.assertRaisesRegex(ValueError, "Unknown zone mode"):
            zones.cell_level(dict(minLevel=1, maxLevel=4, mode="Typo"), [0, 0])

    def test_first_entry_wins_and_coverage_counts_each_cell_once(self):
        entries = [dict(id="region", shapes=[[[0, 0], [1, 0], [1, 1], [0, 1]]]),
            dict(id="region-fallback", shapes=[[[0, 0], [2, 0], [2, 1], [0, 1]]])]
        self.assertEqual("region", zones.find_zone(entries, [1, 1])["id"])
        self.assertEqual("region-fallback", zones.find_zone(entries, [2, 1])["id"])
        self.assertIsNone(zones.find_zone(entries, [3, 1]))
        self.assertEqual({"region": 4, "region-fallback": 2}, zones.coverage(entries))

    def test_sewer_interiors_skip_dummies_tests_and_unrelated_cells(self):
        def cell(form, editor, total=10, outside=True, city=None, interior=True):
            return dict(kind="cell", formId=form, editorId=editor, interior=interior, city=city,
                references=dict(total=total), outside=dict(worldspaces=[zones.TAMRIEL]) if outside else None)
        places = [cell("00096AC5", "ImperialSewerSystemMarketDistrict", city="imperial-city"),
            cell("00093FEF", "Dark04Sewers01"), cell("0000A9C5", "ImperialSewerSystemTG11", total=0, outside=False),
            cell("0004B7B5", "TestSewersXX"), cell("00005E3D", "ICPrisonSewerExit01", interior=False),
            cell("00012345", "SomeCave")]
        self.assertEqual(["00093FEF", "00096AC5"], zones.sewer_interiors(places))
        zone = dict(minLevel=10, maxLevel=15, mode="Fixed")
        self.assertEqual(zones.cell_level(zone, [0x96AC5, 0]), zones.interior_level(zone, "00096AC5"))

    def test_regeneration_keeps_all_areas_fallbacks_settings_and_is_idempotent(self):
        entries = [dict(id=identifier, shapes=[[[0, 0], [1, 0], [0, 1]]], areas=[],
            minLevel=1, maxLevel=4, mode="Fixed") for identifier in zones.REGIONS
            if identifier not in {"heartlands", "colovian-highlands", "imperial-sewers"}]
        entries[-1]["shapes"] = []
        entries[0]["areas"] = ["ICImperialPalaceMQ16"]
        regions = [dict(editorId=source, worldspace=dict(formId=zones.TAMRIEL),
            areas=[dict(tes=[[0, 0], [8192, 0], [0, 8192]]),
                dict(tes=[[-8192, 0], [-4096, 0], [-8192, 8192]])])
            for sources in zones.REGIONS.values() for source in sources]
        places = [dict(kind="worldspace", editorId=name, parent=None) for name in zones.OBLIVION_AREAS]
        places += [dict(kind="worldspace", editorId=name, parent=zones.TAMRIEL)
            for name in ["ICMarketDistrict", "ICImperialPalaceMQ16", "ICTempleDistrictMQ16"]]
        original = copy.deepcopy(entries)
        generated = zones.generate_entries(entries, regions, places)
        self.assertEqual(original, entries)
        self.assertEqual(generated, zones.generate_entries(generated, regions, places))
        self.assertEqual(["ICImperialPalaceMQ16", "ICMarketDistrict"], generated[0]["areas"])
        self.assertEqual(2, len(generated[0]["shapes"]))
        by_id = {zone["id"]: zone for zone in generated}
        for entry in entries[:-1]:
            fallback = copy.deepcopy(entry)
            fallback["id"] += "-fallback"
            fallback["areas"] = []
            self.assertEqual(fallback, by_id[fallback["id"]])
        self.assertEqual([4, 15], by_id["heartlands"]["origin"])
        self.assertEqual(1.4, by_id["colovian-highlands"]["npcDamage"])


if __name__ == "__main__":
    unittest.main()
