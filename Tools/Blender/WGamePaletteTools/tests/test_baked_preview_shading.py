"""Real RNA regression for source studio lights copied into MATERIAL preview."""
import importlib.util
from pathlib import Path
import unittest

import bpy

path=Path(__file__).resolve().parents[1]/'baked_preview.py'
spec=importlib.util.spec_from_file_location('preview_shading_under_test',path)
preview=importlib.util.module_from_spec(spec);spec.loader.exec_module(preview)


class PreviewShadingTests(unittest.TestCase):
    def setUp(self):
        self.window=bpy.context.window
        self.space=next(a.spaces.active for a in self.window.screen.areas if a.type=='VIEW_3D')
        self.shading=self.space.shading
        self.world_lights=[x.name for x in bpy.context.preferences.studio_lights if x.type=='WORLD']
        self.assertTrue(self.world_lights)
        self.shading.type='SOLID';self.shading.studio_light='Default'
        self.state=preview._ui_state(self.window)
        preview._sessions[self.window.as_pointer()]=self.state

    def tearDown(self):
        preview.close_preview(self.window)

    def test_solid_default_opens_material_preview_and_restores_source(self):
        preview.configure_viewport(self.window)
        self.assertEqual(self.shading.type,'MATERIAL')
        self.assertIn(self.shading.studio_light,self.world_lights)
        preview.close_preview(self.window)
        self.assertEqual(self.shading.type,'SOLID')
        self.assertEqual(self.shading.studio_light,'Default')

    def test_material_hdri_and_scalar_settings_are_preserved(self):
        chosen=self.world_lights[-1]
        self.state['sourceShading'].update(studio_light=chosen,studiolight_intensity=0.7)
        preview.configure_viewport(self.window)
        self.assertEqual(self.shading.studio_light,chosen)
        self.assertAlmostEqual(self.shading.studiolight_intensity,0.7,places=5)

    def test_only_incompatible_studio_light_is_filtered(self):
        self.shading.type='MATERIAL'
        chosen=self.shading.studio_light
        preview._copy_material_shading(self.shading,{'studio_light':'basic_bright.exr'})
        self.assertEqual(self.shading.studio_light,chosen)
        with self.assertRaises(TypeError):
            preview._copy_material_shading(self.shading,{'studiolight_intensity':object()})


suite=unittest.defaultTestLoader.loadTestsFromTestCase(PreviewShadingTests)
result=unittest.TextTestRunner(verbosity=2).run(suite)
if not result.wasSuccessful():raise RuntimeError('Preview shading regression failed')
