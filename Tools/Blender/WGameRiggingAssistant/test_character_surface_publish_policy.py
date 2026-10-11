"""Run with Python or Blender; fixtures never touch a real character package."""
import ast
import json
from pathlib import Path
import runpy
import subprocess
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

TOOL = Path(__file__).parent
resolve = runpy.run_path(str(TOOL / 'character_surface_publish_policy.py'))['resolve_surface_publish_mode']


class SurfacePublishPolicyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.package = self.root / 'Authoring/CharacterPackages/character-000001'
        self.config = self.package / 'config'
        self.config.mkdir(parents=True)

    def tearDown(self):
        self.temp.cleanup()

    def binding(self, kind, **values):
        path = self.config / f'character_{kind}_binding.json'
        path.write_text(json.dumps(values), encoding='utf-8')
        return path

    def test_new_character_defaults_to_baked(self):
        self.assertEqual(resolve(self.package), 'BAKED')

    def test_existing_palette_remains_legacy(self):
        self.binding('palette', format='mx.characterPaletteBinding.v5', enabled=True)
        self.assertEqual(resolve(self.package), 'LEGACY')

    def test_explicit_conversion_and_disabled_palette_use_baked(self):
        self.binding('palette', format='mx.characterPaletteBinding.v5', enabled=True)
        self.assertEqual(resolve(self.package, True), 'BAKED')
        self.binding('palette', format='mx.characterPaletteBinding.v5', enabled=False)
        self.assertEqual(resolve(self.package), 'BAKED')

    def test_baked_authority_wins_over_historical_palette(self):
        self.binding('palette', format='mx.characterPaletteBinding.v5', enabled=True)
        self.binding('baked_surface', format='mx.characterBakedSurfaceBinding.v1')
        self.assertEqual(resolve(self.package), 'BAKED')

    def test_broken_baked_authority_never_falls_back(self):
        self.binding('palette', format='mx.characterPaletteBinding.v5', enabled=True)
        path = self.binding('baked_surface', format='unknown')
        for content in ['{', 'null', '{"format":"unknown"}']:
            path.write_text(content)
            for force in [False, True]:
                with self.assertRaises(ValueError): resolve(self.package, force)

    def test_invalid_legacy_requires_repair_or_explicit_conversion(self):
        self.binding('palette', format='mx.characterPaletteBinding.v4', enabled=True)
        with self.assertRaises(ValueError): resolve(self.package)
        self.assertEqual(resolve(self.package, True), 'BAKED')

    def test_unified_operator_routes_new_existing_and_explicit_conversion(self):
        tree = ast.parse((TOOL / '__init__.py').read_text(encoding='utf-8-sig'))
        cls = next(node for node in tree.body if isinstance(node, ast.ClassDef)
                   and node.name == 'WGAME_RIGGING_OT_publish_fbx_and_update_unity')
        execute = next(node for node in cls.body if isinstance(node, ast.FunctionDef) and node.name == 'execute')
        events = []
        settings = types.SimpleNamespace(character_id='000001', wgame_custom_binding_report='', wgame_component_skin_report='')
        scene = types.SimpleNamespace(get=lambda *args: False, wgame_rigging_assistant=settings,
                                      wgame_baked_surface_enabled=False)
        bpy_stub = types.SimpleNamespace(ops=types.SimpleNamespace(wgame=types.SimpleNamespace(
            baked_publish=lambda *args: events.append('baked') or {'RUNNING_MODAL'})))
        def legacy_gate(*args):
            events.append('legacy')
            return False  # Stop before any source saving or legacy export.
        namespace = {'bpy': bpy_stub, 'pathlib': __import__('pathlib'), 'runpy': runpy,
                     '_resolve_repository_root': lambda _: str(self.root), '_wgame_rig_object': lambda _: None,
                     '_validate_complete_binding': legacy_gate}
        exec(compile(ast.Module(body=[execute], type_ignores=[]), '<publish operator>', 'exec'), namespace)
        context = types.SimpleNamespace(scene=scene, object=None)
        owner = types.SimpleNamespace(report=lambda *args: None)
        with patch.object(runpy, 'run_path', return_value={'resolve_surface_publish_mode': resolve}):
            namespace['execute'](owner, context)
            self.assertEqual(events, ['baked'])
            self.binding('palette', format='mx.characterPaletteBinding.v5', enabled=True)
            events.clear(); namespace['execute'](owner, context)
            self.assertEqual(events, ['legacy'])
            scene.wgame_baked_surface_enabled = True
            events.clear(); namespace['execute'](owner, context)
            self.assertEqual(events, ['baked'])
            self.binding('baked_surface', format='broken')
            events.clear(); self.assertEqual(namespace['execute'](owner, context), {'CANCELLED'})
            self.assertEqual(events, [])

    def test_failed_conversion_restores_published_bytes(self):
        self.exercise_reviewed_publish(fail_import=True)

    def test_failed_build_preserves_published_bytes_and_reports_diagnostics(self):
        self.exercise_reviewed_publish(fail_build=True)

    def test_publish_promotes_exact_reviewed_bytes_without_rebaking(self):
        self.exercise_reviewed_publish(fail_import=False)

    def exercise_reviewed_publish(self, fail_import=False, fail_build=False):
        # Exercise the real publisher's rollback after staging/audit succeeds but import fails.
        stage = self.root / 'Temp/stage'
        stage.mkdir(parents=True)
        final = self.root / 'ArtSource/Characters/000001/Final_Source'
        final.mkdir(parents=True)
        (final / 'old.fbx').write_bytes(b'last successful export')
        (self.package / 'manifest.json').write_text('{}')
        model = self.package / 'resources/models/body.fbx'
        model.parent.mkdir(parents=True)
        model.write_bytes(b'last successful model')
        self.binding('palette', format='mx.characterPaletteBinding.v5', enabled=True)
        (self.package / 'resource_catalog.json').write_text(json.dumps({'entries': [
            {'resourceKey': 'char.000001.model.body', 'relativePath': 'resources/models/body.fbx'}]}))
        def snapshot():
            return {str(p.relative_to(self.root)): p.read_bytes()
                    for folder in (self.package, final) for p in folder.rglob('*') if p.is_file()}
        before = snapshot()
        candidate_tools = runpy.run_path(str(TOOL / 'baked_preview_candidate.py'))
        source = stage / 'authoring.blend'
        source.write_bytes(b'reviewed source')
        for filename in ('000001_LOD0.fbx', 'BaseColor.png', 'SurfaceData.png'):
            (stage / filename).write_bytes(b'reviewed baked bytes')
        (stage / 'character_baked_surface_binding.json').write_text('{"textureSize":512}')
        (stage / 'fbx_export_audit.json').write_text('{"status":"Passed"}')
        candidate_tools['finish_candidate'](stage, self.root, '000001', 'snapshot', source)
        original_run = runpy.run_path
        def fake_run(path, *args, **kwargs):
            name = Path(path).name
            if name == 'character_surface_publish_policy.py':
                return {'resolve_surface_publish_mode': resolve}
            if name == 'baked_surface.py':
                def publish(*args):
                    self.binding('baked_surface', format='mx.characterBakedSurfaceBinding.v1')
                return {'BINDING': 'character_baked_surface_binding.json', 'sha': lambda _: 'new-hash', 'publish_package': publish}
            if name == 'export_unity_fbx.py':
                raise AssertionError('Publishing a reviewed candidate must never rebake')
            return original_run(path, *args, **kwargs)
        def fake_process(command, **kwargs):
            if command[0] != 'dotnet':raise AssertionError('Publisher must not run another bake or audit')
            self.assertEqual(kwargs['stderr'],subprocess.STDOUT)
            if command[1]=='build':
                artifacts=Path(command[command.index('--artifacts-path')+1])
                output=Path(command[command.index('--output')+1])
                self.assertIn(stage,artifacts.parents)
                self.assertIn(stage,output.parents)
                if fail_build:
                    kwargs['stdout'].write('test build failure: locked file')
                    raise subprocess.CalledProcessError(1,command)
                output.mkdir(parents=True)
                (output/'authoring.dll').write_bytes(b'test executable')
                return
            self.assertIn(stage,Path(command[1]).parents)
            self.assertEqual(Path(command[1]).name,'authoring.dll')
            if fail_import:
                kwargs['stdout'].write('test import failure')
                raise subprocess.CalledProcessError(1,command)
            report=self.root/'Assets/MxFrameworkGenerated/CharacterPackages/000001/package_cache/import_report.json'
            report.parent.mkdir(parents=True)
            report.write_text(json.dumps({k:'hash' for k in ('sourcePackageHash','generatedConfigHash','geometryBindingHash','resourceMappingHash','writePlanHash')}))
        bpy_stub = types.SimpleNamespace(data=types.SimpleNamespace(filepath=str(stage / 'source.blend')),
                                        app=types.SimpleNamespace(binary_path='blender'))
        with patch.dict(sys.modules, {'bpy': bpy_stub}):
            publisher = original_run(str(TOOL / 'publish_baked_character.py'))
        with patch('runpy.run_path', side_effect=fake_run), patch('subprocess.run', side_effect=fake_process), \
             patch.object(sys, 'argv', ['blender', '--', '--repo', str(self.root), '--character', '000001',
                                      '--candidate', str(stage), '--expected-input-hash', 'snapshot',
                                      '--source-hash', candidate_tools['sha'](source)]):
            if fail_import or fail_build:
                with self.assertRaisesRegex(RuntimeError, 'test build failure' if fail_build else 'test import failure'):publisher['main']()
            else:publisher['main']()
        if fail_import or fail_build:
            self.assertEqual(snapshot(), before)
            self.assertEqual(resolve(self.package), 'LEGACY')
        else:
            for name in ('000001_LOD0.fbx','BaseColor.png','SurfaceData.png'):
                self.assertEqual((final/name).read_bytes(),(stage/name).read_bytes())
            self.assertEqual(model.read_bytes(),(stage/'000001_LOD0.fbx').read_bytes())
            self.assertTrue((stage/'publish_result.json').is_file())
            self.assertEqual(json.loads((final/'character_baked_surface_binding.json').read_text())['authoringSourceHash'],candidate_tools['sha'](source))
            candidate_tools['validate_candidate'](stage,self.root,'000001','snapshot')


if __name__ == '__main__': unittest.main(argv=[sys.argv[0]])
