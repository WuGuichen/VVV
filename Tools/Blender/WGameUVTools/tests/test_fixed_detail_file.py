"""Native one-click fixed PNG workflow, pixel preservation and transactional failure."""
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import sys,json,tempfile,unittest,struct,hashlib
import bpy,bmesh,numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
from WGameUVTools import detail_images as images,detail_binding,decals
addon.register()
bpy.types.Scene.wgame_face_detail_texture_path=bpy.props.StringProperty(subtype='FILE_PATH')
bpy.types.Scene.wgame_bake_texture_size=bpy.props.StringProperty(default='512')
OUT=Path(__file__).resolve().parents[2]
authority_path=images.fixed_path

def native(action='ensure_detail_image',**kwargs):
    try:return getattr(bpy.ops.wgame_uv,action)(**kwargs)
    except RuntimeError:return {'CANCELLED'}

def uvs(obj):
    if obj.mode=='EDIT':
        bm=bmesh.from_edit_mesh(obj.data)
        return {u.name:[tuple(l[u].uv) for f in bm.faces for l in f.loops] for u in bm.loops.layers.uv.values()}
    return {u.name:[tuple(l.uv) for l in u.data] for u in obj.data.uv_layers}

class FixedFile(unittest.TestCase):
    def setUp(self):
        if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        for image in list(bpy.data.images):
            if image.name not in {'Render Result','Viewer Node'}:bpy.data.images.remove(image)
        bpy.ops.mesh.primitive_plane_add();self.obj=bpy.context.object
        mesh=self.obj.data;mesh.uv_layers.active.name='UV0';mesh.uv_layers.new(name='PaletteUV');mesh.uv_layers.new(name='Work');mesh.uv_layers.active_index=2
        mat=bpy.data.materials.new('Original');mat.use_nodes=True;mesh.materials.clear();mesh.materials.append(mat)
        bpy.context.scene.wgame_uv_tools.decal_target=None;bpy.context.scene.wgame_face_detail_texture_path=''
        bpy.context.scene.wgame_bake_texture_size='512'
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)/'ArtSource/Characters/000001'
        self.source=self.root/'Blender_Work/000001_work.blend';self.source.parent.mkdir(parents=True)
        self.source.write_bytes(b'fixture authority; never saved by detail operator')
        self.manifest=self.root/'authoring_manifest.json'
        self.doc={'characterId':'000001','packageId':'000001','authority':{'blenderPath':''},
                  'items':[{'kind':'BlenderWork','role':'active-work','path':'Blender_Work/000001_work.blend'}]}
        self.manifest.write_text(json.dumps(self.doc),encoding='utf-8')
        self.path=self.root/'FaceDetail/face_detail_rgba.png';self.source_bytes=self.source.read_bytes()
        self.resolver=patch.object(images,'fixed_path',side_effect=lambda source=None:authority_path(source or self.source));self.resolver.start()

    def tearDown(self):
        self.resolver.stop()
        self.assertEqual(self.source.read_bytes(),self.source_bytes)
        self.assertEqual(bpy.context.scene.wgame_bake_texture_size,'512')
        if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        self.temp.cleanup()

    def canvas(self,size=2048,name='Painted current'):
        image=bpy.data.images.new(name,width=size,height=size,alpha=True)
        data=np.zeros((size,size,4),dtype=np.float32);data[0,0]=(.2,.4,.6,.8)
        decals.write_pixels(image,data)
        return image

    def disk_image(self,size=2048):
        image=self.canvas(size,'Existing fixed');self.path.parent.mkdir(exist_ok=True)
        image.file_format='PNG';image.filepath_raw=str(self.path);image.save()
        return image

    def assert_png_pixel(self,image):
        copy=self.root/'readback.png';copy.write_bytes(self.path.read_bytes())
        disk=bpy.data.images.load(str(copy),check_existing=False)
        try:self.assertTrue(np.allclose(decals.pixels(disk)[0,0],decals.pixels(image)[0,0],atol=.005))
        finally:bpy.data.images.remove(disk)

    def state(self):
        return {'uvs':uvs(self.obj),'mode':bpy.context.mode,'materials':[m.as_pointer() for m in self.obj.data.materials],
                'images':[(i.name,i.as_pointer(),i.filepath,i.is_dirty,hashlib.sha256(decals.pixels(i).tobytes()).hexdigest()) for i in bpy.data.images if i.has_data],
                'target':bpy.context.scene.wgame_uv_tools.decal_target,'path':bpy.context.scene.wgame_face_detail_texture_path}

    def test_native_new_immediately_saves_transparent_2048_and_keeps_edit_mode(self):
        bpy.ops.object.mode_set(mode='EDIT');before=uvs(self.obj)
        self.assertEqual(native(),{'FINISHED'})
        image=bpy.context.scene.wgame_uv_tools.decal_target
        self.assertEqual(tuple(image.size),(2048,2048));self.assertTrue(np.all(decals.pixels(image)==0))
        header=self.path.read_bytes()[:26];self.assertEqual(struct.unpack('>II',header[16:24]),(2048,2048));self.assertEqual(header[25],6)
        self.assertEqual(image.filepath,str(self.path));self.assertEqual(bpy.context.scene.wgame_face_detail_texture_path,str(self.path))
        self.assertIs(self.obj.active_material.node_tree.nodes['Detail_Texture'].image,image)
        self.assertEqual(bpy.context.mode,'EDIT_MESH')
        for name in ('UV0','PaletteUV'):self.assertEqual(uvs(self.obj)[name],before[name])
        self.assertEqual([p.name for p in self.path.parent.glob('*.png')],['face_detail_rgba.png'])

    def test_repeat_reuses_same_image_and_then_saves_actual_paint(self):
        self.assertEqual(native(),{'FINISHED'});image=bpy.context.scene.wgame_uv_tools.decal_target
        pointer=image.as_pointer();content=self.path.read_bytes();count=len(bpy.data.images)
        self.assertEqual(native(),{'FINISHED'});self.assertEqual(image.as_pointer(),pointer)
        self.assertEqual(len(bpy.data.images),count);self.assertEqual(self.path.read_bytes(),content)
        data=decals.pixels(image);data[0,0]=(.2,.4,.6,.8);decals.write_pixels(image,data);pixels=decals.pixels(image).copy()
        self.assertEqual(native(),{'FINISHED'});self.assertTrue(np.array_equal(decals.pixels(image),pixels))
        self.assertNotEqual(self.path.read_bytes(),content);self.assert_png_pixel(image)
        self.assertFalse(list(self.path.parent.glob('*.001.png')))

    def test_existing_fixed_png_loads_and_preserves_its_bytes_and_size(self):
        original=self.disk_image(512);content=self.path.read_bytes();bpy.data.images.remove(original)
        self.assertEqual(native(),{'FINISHED'});image=bpy.context.scene.wgame_uv_tools.decal_target
        self.assertEqual(tuple(image.size),(512,512));self.assertEqual(self.path.read_bytes(),content)
        self.assert_png_pixel(image);pointer=image.as_pointer()
        self.assertEqual(native(),{'FINISHED'});self.assertEqual(bpy.context.scene.wgame_uv_tools.decal_target.as_pointer(),pointer)
        self.assertEqual(self.path.read_bytes(),content)

    def test_unsaved_current_paint_is_saved_without_creating_replacement(self):
        image,_=images.create(bpy.context);data=decals.pixels(image);data[0,0]=(.2,.4,.6,.8);decals.write_pixels(image,data)
        pixels=decals.pixels(image).copy();count=len(bpy.data.images)
        self.assertEqual(native(),{'FINISHED'});self.assertIs(bpy.context.scene.wgame_uv_tools.decal_target,image)
        self.assertEqual(len(bpy.data.images),count);self.assertTrue(np.array_equal(decals.pixels(image),pixels));self.assert_png_pixel(image)

    def test_unsaved_current_vs_existing_png_requires_explicit_confirmation(self):
        fixed=self.disk_image();old=self.path.read_bytes()
        current,_=images.create(bpy.context);data=decals.pixels(current);data[0,0]=(.6,.4,.2,.8);decals.write_pixels(current,data)
        before=self.state()
        self.assertEqual(native(),{'CANCELLED'});self.assertEqual(self.state(),before);self.assertEqual(self.path.read_bytes(),old)
        calls=[];fake=SimpleNamespace(report=lambda *a:None,execute=lambda *_:self.fail('invoke wrote before confirmation'))
        proxy=SimpleNamespace(active_object=self.obj,mode=bpy.context.mode,objects_in_mode=[],scene=bpy.context.scene,
                             window_manager=SimpleNamespace(invoke_props_dialog=lambda *a,**k:calls.append('dialog') or {'RUNNING_MODAL'}))
        self.assertEqual(images.invoke_fixed(fake,proxy),{'RUNNING_MODAL'});self.assertEqual(calls,['dialog']);self.assertEqual(self.path.read_bytes(),old)
        self.assertEqual(native(overwrite_existing=True),{'FINISHED'});self.assert_png_pixel(current)
        backups=list((self.path.parent/'.backups').glob('*.png'));self.assertEqual(len(backups),1);self.assertEqual(backups[0].read_bytes(),old)
        self.assertIsNot(fixed,current)
        content=self.path.read_bytes();self.assertEqual(native(),{'FINISHED'});self.assertEqual(self.path.read_bytes(),content)
        self.assertIs(bpy.context.scene.wgame_uv_tools.decal_target,current)

    def test_blank_current_never_overwrites_existing_painted_png_silently(self):
        self.disk_image();old=self.path.read_bytes();images.create(bpy.context)
        self.assertEqual(native(),{'CANCELLED'});self.assertEqual(self.path.read_bytes(),old)

    def test_save_updates_fixed_path_without_file_selector(self):
        self.assertEqual(native(),{'FINISHED'});image=bpy.context.scene.wgame_uv_tools.decal_target
        data=decals.pixels(image);data[0,0]=(.2,.4,.6,.8);decals.write_pixels(image,data)
        fake=SimpleNamespace(report=lambda *a:None,execute=lambda *_:native('save_fixed_detail_image'))
        proxy=SimpleNamespace(scene=bpy.context.scene,window_manager=SimpleNamespace(fileselect_add=lambda *_:self.fail('file selector')))
        self.assertEqual(images.invoke_fixed(fake,proxy,require_canvas=True),{'FINISHED'});self.assert_png_pixel(image)

    def test_external_disk_change_blocks_save_and_preserves_current_pixels(self):
        self.assertEqual(native(),{'FINISHED'});image=bpy.context.scene.wgame_uv_tools.decal_target
        pixels=decals.pixels(image).copy();self.path.write_bytes(self.path.read_bytes()+b'external change');changed=self.path.read_bytes()
        self.assertEqual(native('save_fixed_detail_image'),{'CANCELLED'});self.assertEqual(self.path.read_bytes(),changed)
        self.assertTrue(np.array_equal(decals.pixels(image),pixels))

    def test_failure_staging_rolls_back_new_binding_and_leaves_no_png(self):
        bpy.ops.object.mode_set(mode='EDIT');before=self.state()
        with patch.object(images,'_stage_png',side_effect=PermissionError('fixture write denied')):
            self.assertEqual(native(),{'CANCELLED'})
        self.assertEqual(self.state(),before);self.assertFalse(self.path.exists());self.assertFalse(self.path.parent.exists())

    def test_failure_after_new_file_commit_rolls_back_file_and_binding(self):
        before=self.state()
        with patch.object(images,'_pixel_hash',side_effect=RuntimeError('fixture metadata failure after write')):
            self.assertEqual(native(),{'CANCELLED'})
        self.assertEqual(self.state(),before);self.assertFalse(self.path.exists());self.assertFalse(self.path.parent.exists())

    def test_overwrite_io_failure_keeps_png_memory_and_metadata(self):
        self.assertEqual(native(),{'FINISHED'});image=bpy.context.scene.wgame_uv_tools.decal_target
        data=decals.pixels(image);data[0,0]=(.2,.4,.6,.8);decals.write_pixels(image,data);before=self.state();old=self.path.read_bytes()
        with patch.object(images.os,'replace',side_effect=PermissionError('fixture replace denied')):
            self.assertEqual(native('save_fixed_detail_image'),{'CANCELLED'})
        self.assertEqual(self.state(),before);self.assertEqual(self.path.read_bytes(),old)
        self.assertFalse(list(self.path.parent.glob('.face-detail-*.tmp')))

    def test_metadata_failure_after_existing_file_write_restores_old_bytes(self):
        self.assertEqual(native(),{'FINISHED'});image=bpy.context.scene.wgame_uv_tools.decal_target
        data=decals.pixels(image);data[0,0]=(.2,.4,.6,.8);decals.write_pixels(image,data);before=self.state();old=self.path.read_bytes()
        digest=images._pixel_hash(image)
        with patch.object(images,'_pixel_hash',side_effect=[digest,RuntimeError('fixture failure after replacement')]):
            self.assertEqual(native('save_fixed_detail_image'),{'CANCELLED'})
        self.assertEqual(self.state(),before);self.assertEqual(self.path.read_bytes(),old)

    def test_file_created_concurrently_is_preserved_and_binding_rolls_back(self):
        before=self.state();stage=images._stage_png
        def concurrent(image,path):
            temporary=stage(image,path);path.write_bytes(b'concurrently created bytes');return temporary
        with patch.object(images,'_stage_png',side_effect=concurrent):self.assertEqual(native(),{'CANCELLED'})
        self.assertEqual(self.state(),before);self.assertEqual(self.path.read_bytes(),b'concurrently created bytes')

    def test_authority_rejects_display_id_mismatch_wrong_work_and_traversal(self):
        self.assertEqual(authority_path(self.source),self.path)
        self.doc['authority']['blenderPath']='Blender_Work/other.blend';self.manifest.write_text(json.dumps(self.doc))
        self.assertEqual(native(),{'CANCELLED'});self.assertFalse(self.path.exists())
        self.doc['authority']['blenderPath']='../../outside.blend';self.manifest.write_text(json.dumps(self.doc))
        with self.assertRaises(ValueError):authority_path(self.source)
        self.doc['authority']['blenderPath']='';self.doc['characterId']='999999';self.manifest.write_text(json.dumps(self.doc))
        with self.assertRaises(ValueError):authority_path(self.source)

    def test_native_undo_redo_keeps_one_fixed_png_and_restores_bindings(self):
        bpy.context.preferences.edit.use_global_undo=True;name=self.obj.name
        bpy.ops.ed.undo_push(message='Before fixed detail');self.assertEqual(native(),{'FINISHED'})
        image_name=bpy.context.scene.wgame_uv_tools.decal_target.name;content=self.path.read_bytes()
        bpy.ops.ed.undo_push(message='Created fixed detail');self.assertEqual(bpy.ops.ed.undo(),{'FINISHED'})
        self.obj=bpy.data.objects[name];self.assertEqual(self.obj.data.uv_layers.active.name,'Work');self.assertEqual(self.path.read_bytes(),content)
        self.assertEqual(bpy.ops.ed.redo(),{'FINISHED'});self.obj=bpy.data.objects[name]
        self.assertEqual(self.obj.data.uv_layers.active.name,'DetailUV');self.assertEqual(bpy.context.scene.wgame_uv_tools.decal_target.name,image_name)
        self.assertEqual(self.path.read_bytes(),content)

result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(FixedFile))
(OUT/'fixed-file-test-result.json').write_text(json.dumps({'tests':result.testsRun,'errors':len(result.errors),'failures':len(result.failures),'passed':result.wasSuccessful()},indent=2),encoding='utf-8')
if not result.wasSuccessful():raise RuntimeError('Fixed detail file regression failed')
