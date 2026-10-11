"""Real save/reopen checks in temporary files; never saves a production asset."""
import hashlib
import importlib.util
import json
from pathlib import Path
import runpy
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import bpy
import bmesh

tool=Path(__file__).parent
save=runpy.run_path(str(tool/'save_character_source.py'))['save_before_publish']


class SaveSourceTests(unittest.TestCase):
    def setUp(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.temp=tempfile.TemporaryDirectory(prefix='wgame-source-save-')
        self.root=Path(self.temp.name)
        self.source=self.root/'ArtSource/Characters/000009/Blender_Work/000009_work.blend'
        self.source.parent.mkdir(parents=True)
        self.manifest=self.source.parent.parent/'authoring_manifest.json'
        mesh=bpy.data.meshes.new('SourceMesh');mesh.from_pydata([(0,0,0),(1,0,0),(0,1,0)],[],[(0,1,2)]);mesh.update()
        self.obj=bpy.data.objects.new('SourceMesh',mesh);bpy.context.collection.objects.link(self.obj)
        self.obj.select_set(True);bpy.context.view_layer.objects.active=self.obj
        self.material=bpy.data.materials.new('OriginalMaterial');mesh.materials.append(self.material)
        self.image=bpy.data.images.new('Paint',width=2,height=2,alpha=True)
        self.image.colorspace_settings.name='Non-Color';self.image.pixels[:]=[.1,.2,.3,1]*4
        self.external=self.root/'shared.png';self.image.filepath_raw=str(self.external);self.image.file_format='PNG';self.image.save()
        self.disk_pixels=self.external.read_bytes()
        bpy.data.images.remove(self.image)
        self.image=bpy.data.images.load(str(self.external));self.image.name='Paint';self.image.colorspace_settings.name='Non-Color'
        node=self.material.node_tree.nodes.new('ShaderNodeTexImage');node.image=self.image
        bpy.ops.wm.save_as_mainfile(filepath=str(self.source),check_existing=False)
        self.manifest.write_text(json.dumps({'revision':3,'authority':{'blenderPath':'Blender_Work/000009_work.blend'},
            'items':[{'id':'blender-work','path':'Blender_Work/000009_work.blend','sha256':'old'}]}))

    def tearDown(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.temp.cleanup()

    def test_dirty_paint_edit_mesh_and_manifest_survive_reopening(self):
        self.image.pixels[:]=[.8,.2,.4,1]*4
        bpy.ops.object.mode_set(mode='EDIT');bm=bmesh.from_edit_mesh(self.obj.data);bm.verts.ensure_lookup_table();bm.verts[0].co.z=.25
        bmesh.update_edit_mesh(self.obj.data)
        result=save(self.root,'000009')
        self.assertEqual(bpy.context.mode,'EDIT_MESH')
        self.assertIn('Paint',result['packedImages'])
        self.assertEqual(self.external.read_bytes(),self.disk_pixels)
        manifest=json.loads(self.manifest.read_text());self.assertEqual(manifest['revision'],4)
        self.assertEqual(manifest['items'][0]['sha256'],'sha256:'+hashlib.sha256(self.source.read_bytes()).hexdigest())
        bpy.ops.wm.open_mainfile(filepath=str(self.source))
        if bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        self.assertAlmostEqual(bpy.data.objects['SourceMesh'].data.vertices[0].co.z,.25)
        self.assertTrue(bpy.data.images['Paint'].packed_file)
        self.assertAlmostEqual(bpy.data.images['Paint'].pixels[0],.8,delta=1/255)

    def test_repainting_an_already_packed_image_refreshes_saved_pixels(self):
        self.image.pack();self.image.pixels[:]=[.6,.7,.8,1]*4
        save(self.root,'000009')
        bpy.ops.wm.open_mainfile(filepath=str(self.source))
        self.assertAlmostEqual(bpy.data.images['Paint'].pixels[1],.7,delta=1/255)

    def test_generated_paint_is_embedded_without_an_external_file(self):
        image=bpy.data.images.new('GeneratedPaint',width=2,height=2,alpha=True)
        image.colorspace_settings.name='Non-Color';image.pixels[:]=[.25,.5,.75,1]*4
        self.material.node_tree.nodes.new('ShaderNodeTexImage').image=image
        save(self.root,'000009')
        bpy.ops.wm.open_mainfile(filepath=str(self.source))
        self.assertTrue(bpy.data.images['GeneratedPaint'].packed_file)
        self.assertAlmostEqual(bpy.data.images['GeneratedPaint'].pixels[2],.75,delta=1/255)

    def test_authoring_preview_is_restored_in_memory_but_not_saved_as_source(self):
        preview=self.material.copy();preview['wgame_hair_preview_source']=self.material.name
        self.obj.material_slots[0].material=preview
        save(self.root,'000009')
        self.assertEqual(self.obj.material_slots[0].material,preview)
        bpy.ops.wm.open_mainfile(filepath=str(self.source))
        self.assertEqual(bpy.data.objects['SourceMesh'].material_slots[0].material.name,'OriginalMaterial')

    def test_preview_and_wrong_authority_are_rejected_before_writing(self):
        old=self.source.read_bytes();bpy.context.scene['wgame_baked_preview']=True
        with self.assertRaisesRegex(ValueError,'返回制作'):save(self.root,'000009')
        del bpy.context.scene['wgame_baked_preview']
        doc=json.loads(self.manifest.read_text());doc['authority']['blenderPath']='Blender_Work/other.blend';self.manifest.write_text(json.dumps(doc))
        with self.assertRaisesRegex(ValueError,'制作源'):save(self.root,'000009')
        self.assertEqual(self.source.read_bytes(),old)

    def test_cancelled_save_does_not_update_source_manifest(self):
        old=self.source.read_bytes();manifest=self.manifest.read_bytes()
        fake=SimpleNamespace(context=bpy.context,data=bpy.data,ops=SimpleNamespace(wm=SimpleNamespace(save_as_mainfile=lambda **kw:{'CANCELLED'})))
        with patch.dict(save.__globals__,{'bpy':fake}):
            with self.assertRaisesRegex(RuntimeError,'保存失败'):save(self.root,'000009')
        self.assertEqual(self.source.read_bytes(),old);self.assertEqual(self.manifest.read_bytes(),manifest)

    def test_publish_does_not_snapshot_or_start_worker_if_save_fails(self):
        spec=importlib.util.spec_from_file_location('save_test_palette',tool.parent/'WGamePaletteTools/__init__.py',submodule_search_locations=[str(tool.parent/'WGamePaletteTools')])
        package=importlib.util.module_from_spec(spec);sys.modules[spec.name]=package;spec.loader.exec_module(package)
        hair=importlib.import_module('save_test_palette.hair_authoring')
        workflow=importlib.import_module('save_test_palette.baked_workflow')
        package._find_repo_source_file=lambda:str(self.root/'Tools/Blender/WGamePaletteTools/__init__.py')
        (self.root/'Temp').mkdir()
        class Settings(bpy.types.PropertyGroup):
            character_id:bpy.props.StringProperty(default='000009')
        bpy.utils.register_class(Settings);bpy.types.Scene.wgame_rigging_assistant=bpy.props.PointerProperty(type=Settings)
        events=[]
        def fail(*args):events.append('save');raise OSError('disk unavailable')
        def modules(path):
            if Path(path).name=='character_surface_publish_policy.py':
                return {'resolve_surface_publish_mode':lambda *args:'BAKED'}
            if Path(path).name=='save_character_source.py':return {'save_before_publish':fail}
            return {'snapshot':lambda *args:events.append('snapshot')}
        try:
            checked=(self.root,'000009',self.source,bpy.context.scene,{'stage':str(self.root/'Temp')},{'inputHash':'reviewed'})
            with patch.object(workflow,'checked_candidate',return_value=checked),patch.object(runpy,'run_path',side_effect=modules),patch.object(hair.subprocess,'Popen',side_effect=AssertionError('Worker must not start')):
                result=hair.WG_OT_BakedPublish.execute(SimpleNamespace(report=lambda *_:None),bpy.context)
            self.assertEqual(result,{'CANCELLED'});self.assertEqual(events,['save'])
        finally:
            del bpy.types.Scene.wgame_rigging_assistant;bpy.utils.unregister_class(Settings)


result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(SaveSourceTests))
if not result.wasSuccessful():raise RuntimeError('Source save tests failed')
