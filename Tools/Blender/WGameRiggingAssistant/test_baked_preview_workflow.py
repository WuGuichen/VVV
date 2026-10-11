"""Disposable Blender fixtures for source freshness and candidate isolation."""
import importlib.util
import json
from pathlib import Path
import runpy
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

import bpy
import bmesh

TOOL=Path(__file__).parent
fingerprint=runpy.run_path(str(TOOL/'baked_preview_inputs.py'))['source_fingerprint']
candidate=runpy.run_path(str(TOOL/'baked_preview_candidate.py'))


class BakePreviewTests(unittest.TestCase):
    def setUp(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        self.stage=self.root/'Temp/baked-preview-test';self.stage.mkdir(parents=True)
        self.source=self.root/'work.blend'
        # A real validated catalog keeps feedback gates independent of the production repo.
        validator=TOOL.parents[1]/'RuntimeHash/definition_ids.py'
        ids=runpy.run_path(str(validator))
        catalog=self.root/'Tools/RuntimeHash/character_definition_ids.json';catalog.parent.mkdir(parents=True)
        shutil.copy2(validator,catalog.with_name('definition_ids.py'))
        catalog.write_text(json.dumps(ids['apply'](ids['create']([413]),'allocate',413,'000001')),encoding='utf-8')
        mesh=bpy.data.meshes.new('Fixture');mesh.from_pydata([(0,0,0),(1,0,0),(0,1,0)],[],[(0,1,2)]);mesh.update()
        self.obj=bpy.data.objects.new('Fixture',mesh);bpy.context.collection.objects.link(self.obj)
        self.obj.select_set(True);bpy.context.view_layer.objects.active=self.obj
        self.material=bpy.data.materials.new('Source');mesh.materials.append(self.material)
        self.image=bpy.data.images.new('SourcePaint',width=2,height=2)
        node=self.material.node_tree.nodes.new('ShaderNodeTexImage');node.image=self.image
        bsdf=next(n for n in self.material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        self.material.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
        bpy.ops.wm.save_as_mainfile(filepath=str(self.source))

    def tearDown(self):self.temp.cleanup()

    def hash(self):return fingerprint(bpy.context.scene,self.root)

    def ready(self):
        for name in ('000001_LOD0.fbx','BaseColor.png','SurfaceData.png','character_baked_surface_binding.json'):
            (self.stage/name).write_text('{}' if name.endswith('json') else name)
        (self.stage/'fbx_export_audit.json').write_text('{"status":"Passed"}')
        return candidate['finish_candidate'](self.stage,self.root,'000001',self.hash(),self.source)

    def test_selection_and_node_layout_do_not_expire_preview(self):
        before=self.hash()
        self.obj.data.vertices[0].select=True
        self.material.node_tree.nodes[0].location.x+=50
        bpy.context.scene['wgame_hair_flow_status']='UI only'
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        self.assertEqual(before,self.hash())
        bpy.ops.object.mode_set(mode='OBJECT');self.assertEqual(before,self.hash())

    def test_geometry_profile_root_and_material_changes_expire_preview(self):
        before=self.hash();self.obj.data.vertices[0].co.x=.2;self.assertNotEqual(before,self.hash())
        before=self.hash();a=self.obj.data.attributes.new(name='SurfaceProfile',type='INT',domain='FACE');a.data[0].value=2
        self.assertNotEqual(before,self.hash())
        before=self.hash();a=self.obj.data.attributes.new(name='WGameHairRoot',type='BOOLEAN',domain='POINT');a.data[0].value=True
        self.assertNotEqual(before,self.hash())
        before=self.hash();bsdf=next(n for n in self.material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        bsdf.inputs['Emission Strength'].default_value=.7;self.assertNotEqual(before,self.hash())

    def test_unsaved_paint_and_guide_edits_expire_preview(self):
        before=self.hash();self.image.pixels[0]=.4;self.image.update();self.assertNotEqual(before,self.hash())
        curve=bpy.data.curves.new('Guide','CURVE');spline=curve.splines.new('POLY');spline.points.add(1)
        obj=bpy.data.objects.new('Guide',curve);bpy.context.collection.objects.link(obj);obj['wgame_hair_guide']=True
        before=self.hash();spline.points[1].co.z=1;self.assertNotEqual(before,self.hash())
        rgb=self.material.node_tree.nodes.new('ShaderNodeRGB')
        before=self.hash();rgb.outputs[0].default_value=(.1,.2,.3,1);self.assertNotEqual(before,self.hash())

    def test_ui_requires_viewing_and_fresh_content_before_saving_or_publishing(self):
        folder=TOOL.parent/'WGamePaletteTools'
        spec=importlib.util.spec_from_file_location('candidate_test_palette',folder/'__init__.py',submodule_search_locations=[str(folder)])
        package=importlib.util.module_from_spec(spec);sys.modules[spec.name]=package;spec.loader.exec_module(package)
        workflow=importlib.import_module('candidate_test_palette.baked_workflow')
        receipt=self.ready();key=workflow.key_for(self.root,'000001',self.source)
        item={'stage':str(self.stage),'viewed':False};workflow._candidates[key]=item
        original_run=runpy.run_path
        def load(path,*args,**kwargs):
            if Path(path).name=='baked_preview_candidate.py':return candidate
            return original_run(path,*args,**kwargs)
        with patch.object(workflow,'identity',return_value=(self.root,'000001',self.source,bpy.context.scene)), \
             patch.object(workflow,'fingerprint',return_value=receipt['inputHash']), \
             patch.object(runpy,'run_path',side_effect=load):
            with self.assertRaisesRegex(ValueError,'查看'):workflow.checked_candidate(bpy.context,True)
            item['viewed']=True
            workflow.checked_candidate(bpy.context,True)
            with patch.object(workflow,'fingerprint',return_value='stale'),patch('subprocess.Popen') as process:
                messages=[]
                owner=type('Owner',(),{'report':lambda self,*args:messages.append(args)})()
                self.assertEqual(workflow.WG_OT_BakedPublish.execute(owner,bpy.context),{'CANCELLED'})
                process.assert_not_called();self.assertIn('重新',str(messages))

    def test_snapshot_does_not_change_fingerprint_or_work_bytes(self):
        before=self.hash();work=self.source.read_bytes()
        baker=runpy.run_path(str(TOOL/'baked_surface.py'));baker['snapshot'](self.stage/'source.blend')
        self.assertEqual(before,self.hash());self.assertEqual(work,self.source.read_bytes())

    def test_snapshot_ignores_unused_missing_image_but_rejects_used_image(self):
        missing=bpy.data.images.new('MissingOldPalette',width=1,height=1)
        missing.source='FILE';missing.filepath=str(self.root/'missing.png')
        self.assertEqual(tuple(missing.size),(0,0))
        obsolete=bpy.data.materials.new('ObsoleteSlot')
        node=obsolete.node_tree.nodes.new('ShaderNodeTexImage');node.image=missing
        self.obj.data.materials.append(obsolete)
        baker=runpy.run_path(str(TOOL/'baked_surface.py'))
        before=self.hash();work=self.source.read_bytes()
        baker['snapshot'](self.stage/'source.blend')
        self.assertEqual(before,self.hash());self.assertEqual(work,self.source.read_bytes())
        self.assertEqual(len(self.obj.data.materials),2)
        self.obj.data.polygons[0].material_index=1
        with self.assertRaisesRegex(ValueError,'Missing source image: MissingOldPalette'):
            baker['snapshot'](self.stage/'invalid.blend')
        self.assertIs(node.image,missing)

    def test_saving_reviewed_dirty_paint_preserves_content_fingerprint(self):
        self.image.pixels[0]=.6;self.image.update();before=self.hash()
        saver=runpy.run_path(str(TOOL/'save_character_source.py'))['save_before_publish']
        saver(self.root,'000001')
        self.assertEqual(before,self.hash())

    def test_candidate_rejects_stale_missing_corrupted_and_cross_character_results(self):
        receipt=self.ready()
        candidate['validate_candidate'](self.stage,self.root,'000001',receipt['inputHash'])
        with self.assertRaisesRegex(ValueError,'变化'):candidate['validate_candidate'](self.stage,self.root,'000001','different')
        with self.assertRaises(ValueError):candidate['validate_candidate'](self.stage,self.root,'000002')
        (self.stage/'BaseColor.png').write_bytes(b'changed')
        with self.assertRaises(ValueError):candidate['validate_candidate'](self.stage,self.root,'000001')
        (self.stage/'BaseColor.png').unlink()
        with self.assertRaises(ValueError):candidate['validate_candidate'](self.stage,self.root,'000001')

    def test_preview_worker_never_publishes(self):
        formal=self.root/'ArtSource/Characters/000001/Final_Source';formal.mkdir(parents=True)
        (formal/'keep').write_bytes(b'unchanged')
        (self.stage/'preview_request.json').write_text(json.dumps({'packageId':'000001','sourcePath':str(self.source),'inputHash':self.hash()}))
        bpy.ops.wm.save_as_mainfile(filepath=str(self.stage/'source.blend'),copy=False)
        worker=runpy.run_path(str(TOOL/'bake_character_preview.py'))
        original=runpy.run_path
        def run(path,*args,**kwargs):
            if Path(path).name=='export_unity_fbx.py':self.ready();return {}
            return original(path,*args,**kwargs)
        with patch('runpy.run_path',side_effect=run),patch('subprocess.run') as process,patch.object(sys,'argv',['blender','--','--repo',str(self.root),'--character','000001']):
            worker['main']()
        self.assertEqual(list(formal.iterdir()),[formal/'keep']);self.assertEqual((formal/'keep').read_bytes(),b'unchanged')
        self.assertFalse((self.root/'Authoring').exists());self.assertFalse((self.root/'Temp/MxFrameworkAuthoring').exists())
        self.assertEqual(process.call_count,1)
        self.assertIn('audit_unity_fbx.py',' '.join(process.call_args.args[0]))


if __name__=='__main__':
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(BakePreviewTests))
    if not result.wasSuccessful():raise RuntimeError('Bake preview tests failed')
