import importlib.util
import pathlib
import sys
import unittest


MODULE_PATH = pathlib.Path(__file__).with_name("physical_matrix_pipeline.py")
SPEC = importlib.util.spec_from_file_location("physical_matrix_pipeline", MODULE_PATH)
PIPELINE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = PIPELINE
SPEC.loader.exec_module(PIPELINE)


class PhysicalMatrixPipelineTests(unittest.TestCase):
    def setUp(self):
        self.materials = [
            {"id": item.material_id, "runtimeMaterialId": 510002 + index}
            for index, item in enumerate(PIPELINE.MATERIALS)
        ] + [{"id": PIPELINE.AIR_ID, "runtimeMaterialId": 510014}]
        self.material_map = {item["id"]: item for item in self.materials}

    def test_inventory_has_seventy_eight_impacts_and_twelve_sweeps(self):
        specs = PIPELINE.desired_pair_specs()
        self.assertEqual(90, len(specs))
        self.assertEqual(78, sum(1 for item in specs if item[2] == "Impact"))
        self.assertEqual(12, sum(1 for item in specs if item[2] == "Sweep"))
        self.assertFalse(any(PIPELINE.AIR_ID == first == second for first, second, _ in specs))

    def test_pair_id_is_unordered_and_stable(self):
        wood = PIPELINE.material_by_token("wood").material_id
        metal = PIPELINE.material_by_token("metal").material_id
        self.assertEqual(
            PIPELINE.pair_id(wood, metal, self.material_map),
            PIPELINE.pair_id(metal, wood, self.material_map),
        )
        self.assertEqual("audio.pair.wood--metal", PIPELINE.pair_id(wood, metal, self.material_map))

    def test_seed_is_reproducible_and_revision_sensitive(self):
        first = PIPELINE.stable_seed("audio.basis.test", "Impact", 1)
        self.assertEqual(first, PIPELINE.stable_seed("audio.basis.test", "Impact", 1))
        self.assertNotEqual(first, PIPELINE.stable_seed("audio.basis.test", "Impact", 2))

    def test_recipe_hash_changes_with_prompt(self):
        recipe = {
            "bindingHash": "sha256:test", "revision": 1, "layerTemplate": "BodyOnly",
            "collisionProfile": None, "targetDurationSeconds": 0.6,
            "positivePrompt": "impact", "negativePrompt": "music",
            "layers": [{"name": "body", "enabled": True, "takes": 3}],
        }
        original = PIPELINE.fnv1a_recipe_hash(recipe)
        recipe["positivePrompt"] = "other impact"
        self.assertNotEqual(original, PIPELINE.fnv1a_recipe_hash(recipe))

    def test_recipe_hash_matches_browser_utf16_fnv(self):
        recipe = {
            "bindingHash": "sha256:test", "revision": 1, "layerTemplate": "BodyOnly",
            "collisionProfile": None, "targetDurationSeconds": 0.6,
            "positivePrompt": "碰撞 impact", "negativePrompt": "music",
            "layers": [{"name": "body", "enabled": True, "takes": 3}],
        }
        self.assertEqual("fnv1a-cb7cbaf2", PIPELINE.fnv1a_recipe_hash(recipe))

    def test_scope_counts_match_production_plan(self):
        self.assertEqual(78, len(PIPELINE.material_targets("impact")))
        self.assertEqual(12, len(PIPELINE.material_targets("sweep")))
        self.assertEqual(90, len(PIPELINE.material_targets("all")))

    def test_noise_and_music_are_excluded(self):
        negative = PIPELINE.NEGATIVE_PROMPT
        self.assertIn("broadband noise", negative)
        self.assertIn("music", negative)
        self.assertIn("baked large-room reverb", negative)
        self.assertIn("room impulse response", negative)
        self.assertIn("wind bed", negative)

    def test_calibration_recipes_use_material_specific_natural_decay_windows(self):
        metal = PIPELINE.material_by_token("metal")
        stone = PIPELINE.material_by_token("stone")
        rubber = PIPELINE.material_by_token("rubber")
        self.assertEqual(0.45, PIPELINE.target_duration("Impact", metal, metal))
        self.assertEqual(0.30, PIPELINE.target_duration("Impact", stone, stone))
        self.assertEqual(0.22, PIPELINE.target_duration("Impact", rubber, stone))
        self.assertEqual("Resonant", PIPELINE.decay_profile("Impact", metal, metal))
        _, layers = PIPELINE.recipe_layers("Hit", "dry hit", 0.45)
        self.assertEqual([0.15, 0.45], [item["maxDurationSeconds"] for item in layers])


if __name__ == "__main__":
    unittest.main()
