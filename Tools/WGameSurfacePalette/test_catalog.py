import json
from pathlib import Path
import unittest

from catalog_core import (
    COLOR_COUNT,
    FORMAT,
    LOGICAL_TOKEN_COUNT,
    SURFACE_PROFILES,
    build_catalog,
    build_surface_profiles_hlsl,
    color_coordinates,
    color_index_from_rgb565,
    color_index_from_rgb8,
    color_index_from_xy,
    decode_rgb8,
    describe_token,
    logical_token,
    rgb565_components,
    rgb8_from_color_index,
    split_logical_token,
    validate_catalog,
)


class ColorPaletteCatalogV3Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = build_catalog()

    def test_catalog_is_deterministic_compact_and_valid(self):
        self.assertEqual(self.catalog, build_catalog())
        self.assertEqual([], validate_catalog(self.catalog))
        self.assertEqual(FORMAT, self.catalog["format"])
        self.assertEqual(65_536, self.catalog["colorCount"])
        self.assertEqual(16, self.catalog["surfaceProfileCount"])
        self.assertEqual(1_048_576, self.catalog["logicalTokenCount"])
        self.assertNotIn("tokens", self.catalog)
        self.assertNotIn("customStartIndex", self.catalog)

    def test_all_color_addresses_round_trip(self):
        for color_index in range(COLOR_COUNT):
            x, y = color_coordinates(color_index)
            self.assertEqual(color_index, color_index_from_xy(x, y))

    def test_rgb565_table_has_65536_unique_formula_colors_and_no_alias_tables(self):
        rgb = decode_rgb8(self.catalog)
        self.assertEqual(COLOR_COUNT * 3, len(rgb))
        colors = {tuple(rgb[index:index + 3]) for index in range(0, len(rgb), 3)}
        self.assertEqual(COLOR_COUNT, len(colors))
        self.assertEqual(COLOR_COUNT, self.catalog["colorData"]["uniqueRgb8Count"])
        self.assertEqual(0, self.catalog["colorData"]["aliasCount"])
        self.assertNotIn("cMaxU16Base64", self.catalog["colorData"])
        self.assertNotIn("canonicalColorIndexU16Base64", self.catalog["colorData"])

    def test_rgb565_bidirectional_formula_round_trips_every_address(self):
        for color_index in range(COLOR_COUNT):
            red5, green6, blue5 = rgb565_components(color_index)
            self.assertEqual(color_index, color_index_from_rgb565(red5, green6, blue5))
            rgb = rgb8_from_color_index(color_index)
            self.assertEqual(color_index, color_index_from_rgb8(*rgb))
        self.assertEqual((0, 0, 0), rgb8_from_color_index(0))
        self.assertEqual((255, 255, 255), rgb8_from_color_index(65_535))

    def test_surface_profile_ids_and_alpha_centers_are_frozen(self):
        self.assertEqual(list(range(16)), [item["index"] for item in SURFACE_PROFILES])
        self.assertEqual([8 + index * 16 for index in range(16)], [item["alphaByte"] for item in SURFACE_PROFILES])
        self.assertEqual(16, len({item["profileId"] for item in SURFACE_PROFILES}))
        self.assertEqual("skin_soft", SURFACE_PROFILES[0]["profileId"])
        self.assertEqual("crystal_frosted", SURFACE_PROFILES[15]["profileId"])

    def test_logical_token_round_trip_including_maximum(self):
        for color_index, profile in ((0, 0), (352, 14), (65_535, 15)):
            value = logical_token(color_index, profile)
            self.assertEqual((color_index, profile), split_logical_token(value))
        self.assertEqual(LOGICAL_TOKEN_COUNT - 1, logical_token(65_535, 15))
        self.assertEqual(float(LOGICAL_TOKEN_COUNT - 1), float(logical_token(65_535, 15)))

    def test_token_description_is_formula_derived(self):
        color_index = color_index_from_rgb565(17, 42, 9)
        token = describe_token(self.catalog, color_index, 14)
        self.assertEqual(color_index * 16 + 14, token["logicalToken"])
        self.assertEqual("crystal_clear", token["surfaceProfileId"])
        self.assertEqual("wgame.surface.v3.r17.g42.b09.crystal_clear", token["tokenId"])
        self.assertEqual((17, 42, 9), (token["red5"], token["green6"], token["blue5"]))
        self.assertEqual(token["colorIndex"], token["canonicalColorIndex"])
        self.assertFalse(token["isAlias"])
        self.assertRegex(token["srgbHex"], r"^#[0-9A-F]{6}$")

    def test_versioned_catalog_and_generated_hlsl_match(self):
        repository_root = Path(__file__).resolve().parents[2]
        versioned = json.loads((repository_root / "ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json").read_text(encoding="utf-8"))
        hlsl = (repository_root / "Assets/Art/WGame/Shaders/ShaderLibrary/WGameSurfaceProfiles.generated.hlsl").read_text(encoding="utf-8")
        self.assertEqual(self.catalog, versioned)
        self.assertEqual(build_surface_profiles_hlsl(self.catalog), hlsl)


if __name__ == "__main__":
    unittest.main()
