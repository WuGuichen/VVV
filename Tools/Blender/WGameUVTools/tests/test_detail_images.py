"""Native new/save buttons: no preexisting PNG, transparency, rollback, collision and Undo."""
from pathlib import Path
from types import SimpleNamespace
import sys,json,tempfile,unittest,hashlib
import bpy,bmesh,numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
from WGameUVTools import detail_images,detail_binding,decals
addon.register()
bpy.types.Scene.wgame_face_detail_texture_path=bpy.props.StringProperty(subtype='FILE_PATH')
OUT=Path(__file__).resolve().parents[2]


def native_new(**kwargs):
    try:return bpy.ops.wgame_uv.new_detail_canvas(**kwargs)
    except RuntimeError:return {'CANCELLED'}


def native_save(path,overwrite=False):
    try:return bpy.ops.wgame_uv.save_detail_image(filepath=str(path),overwrite_existing=overwrite)
    except RuntimeError:return {'CANCELLED'}


def uvs(obj):
    if obj.mode=='EDIT':
        bm=bmesh.from_edit_mesh(obj.data)
        return {u.name:[tuple(l[u].uv) for f in bm.faces for l in f.loops] for u in bm.loops.layers.uv.values()}
    return {u.name:[tuple(l.uv) for l in u.data] for u in obj.data.uv_layers}


class ImageTests(unittest.TestCase):
    def setUp(self):
        if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        bpy.ops.mesh.primitive_plane_add()
        self.obj=bpy.context.object;mesh=self.obj.data
        mesh.uv_layers.active.name='UV0';mesh.uv_layers.new(name='PaletteUV');mesh.uv_layers.new(name='Work_From_Scratch');mesh.uv_layers.active_index=2
        mat=bpy.data.materials.new('Original material');mat.use_nodes=True
        mesh.materials.clear();mesh.materials.append(mat);self.mat=mat
        self.other=self.obj.copy();self.other.data=mesh.copy();bpy.context.collection.objects.link(self.other);self.other.select_set(False)
        self.before=uvs(self.obj)
        area=next((a for a in bpy.context.screen.areas if a.type=='IMAGE_EDITOR'),bpy.context.screen.areas[0]);area.type='IMAGE_EDITOR';area.ui_type='UV'
        self.space=area.spaces.active
        self.original=bpy.data.images.new('Original image',width=2,height=2,alpha=True)
        self.space.image=self.original;self.space.use_image_pin=True
        bpy.context.scene.wgame_uv_tools.decal_target=None
        bpy.context.scene.wgame_face_detail_texture_path=''
        self.temp=tempfile.TemporaryDirectory();self.output=Path(self.temp.name)

    def tearDown(self):
        if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        self.temp.cleanup()

    def test_native_new_transparent_binds_without_png_and_preserves_uv(self):
        bpy.ops.object.mode_set(mode='EDIT')
        selected=[f.select for f in bmesh.from_edit_mesh(self.obj.data).faces]
        self.assertEqual(native_new(),{'FINISHED'})
        image=bpy.context.scene.wgame_uv_tools.decal_target
        self.assertEqual(tuple(image.size),(2048,2048));self.assertEqual(image.channels,4)
        self.assertTrue(np.all(decals.pixels(image)==0))
        self.assertEqual(image.filepath,'');self.assertTrue(image.use_fake_user)
        self.assertEqual(bpy.context.mode,'EDIT_MESH')
        self.assertEqual(uvs(self.obj)['DetailUV'],self.before['Work_From_Scratch'])
        for n in ('UV0','PaletteUV'):self.assertEqual(uvs(self.obj)[n],self.before[n])
        self.assertEqual(selected,[f.select for f in bmesh.from_edit_mesh(self.obj.data).faces])
        self.assertIs(self.obj.active_material.node_tree.nodes['Detail_Texture'].image,image)
        self.assertIs(self.space.image,image);self.assertTrue(self.space.use_image_pin)
        self.assertIs(self.other.data.materials[0],self.mat)
        self.assertEqual(bpy.context.scene.wgame_face_detail_texture_path,'')
        self.assertEqual(bpy.ops.wgame_uv.enter_paint(),{'FINISHED'})
        self.assertEqual(bpy.context.mode,'PAINT_TEXTURE')
        self.assertIs(bpy.context.scene.tool_settings.image_paint.canvas,image)

    def test_blank_uv_can_create_canvas_then_generate_island(self):
        layer=self.obj.data.uv_layers.active
        for l in layer.data:l.uv=(-1,-1)
        bpy.ops.object.mode_set(mode='EDIT')
        self.assertEqual(native_new(),{'FINISHED'})
        self.assertEqual(self.obj.data.uv_layers.active.name,'DetailUV')
        self.assertTrue(all(v==(-1.,-1.) for v in uvs(self.obj)['DetailUV']))
        self.assertEqual(bpy.ops.wgame_uv.face_authoring(action='CREATE'),{'FINISHED'})
        self.assertTrue(all(0<=x<=1 and 0<=y<=1 for x,y in uvs(self.obj)['DetailUV']))
        self.assertEqual(bpy.ops.wgame_uv.enter_paint(),{'FINISHED'})

    def test_repeated_name_keeps_old_unsaved_pixels_and_image(self):
        self.assertEqual(native_new(image_name='UserDetail'),{'FINISHED'})
        old=bpy.context.scene.wgame_uv_tools.decal_target
        data=decals.pixels(old);data[0,0]=(.2,.3,.4,.7);decals.write_pixels(old,data)
        before=decals.pixels(old).copy();name=old.name
        self.assertEqual(native_new(image_name='UserDetail'),{'FINISHED'})
        new=bpy.context.scene.wgame_uv_tools.decal_target
        self.assertNotEqual(new.name,name)
        self.assertIs(bpy.data.images.get(name),old)
        self.assertTrue(np.array_equal(decals.pixels(old),before))
        self.assertTrue(old.use_fake_user)

    def test_existing_formal_conflict_rejects_without_creating(self):
        self.obj.data.uv_layers.new(name='DetailUV');self.obj.data.uv_layers.active_index=2
        before=uvs(self.obj);images=len(bpy.data.images);materials=len(bpy.data.materials)
        self.assertEqual(native_new(),{'CANCELLED'})
        self.assertEqual(uvs(self.obj),before)
        self.assertEqual(len(bpy.data.images),images);self.assertEqual(len(bpy.data.materials),materials)
        image,_=detail_images.create(bpy.context,allow_replace=True)
        self.assertTrue(self.obj.data.uv_layers.get('DetailUV_Old'))
        self.assertIs(self.space.image,image)

    def test_cancel_confirmation_allocates_nothing(self):
        self.obj.data.uv_layers.new(name='DetailUV');self.obj.data.uv_layers.active_index=2
        before=uvs(self.obj);count=len(bpy.data.images)
        proxy=SimpleNamespace(active_object=self.obj,mode='OBJECT',window_manager=SimpleNamespace(invoke_props_dialog=lambda *a,**k:{'RUNNING_MODAL'}))
        proxy.objects_in_mode=[]
        fake=SimpleNamespace(execute=lambda *a:(_ for _ in ()).throw(AssertionError('execute on invoke')))
        self.assertEqual(detail_images.WGAMEUV_OT_new_detail_image.invoke(fake,proxy,None),{'RUNNING_MODAL'})
        # Cancelling that dialog never calls execute and creates no image or UV changes.
        self.assertEqual(len(bpy.data.images),count);self.assertEqual(uvs(self.obj),before)

    def test_failure_removes_created_image_and_leaves_original_data(self):
        self.obj.data.uv_layers.active_index=1
        count=(len(bpy.data.images),len(bpy.data.materials))
        self.assertEqual(native_new(),{'CANCELLED'})
        self.assertEqual(count,(len(bpy.data.images),len(bpy.data.materials)))
        self.assertEqual(uvs(self.obj),self.before);self.assertIs(self.obj.active_material,self.mat)

    def test_native_save_and_collision_protection(self):
        self.assertEqual(native_new(),{'FINISHED'})
        image=bpy.context.scene.wgame_uv_tools.decal_target
        path=self.output/'FaceDetail/face_detail_rgba.png'
        self.assertEqual(native_save(path),{'FINISHED'})
        self.assertTrue(path.is_file());header=path.read_bytes()[:26]
        self.assertEqual(header[:8],b'\x89PNG\r\n\x1a\n');self.assertEqual(header[25],6)
        self.assertEqual(bpy.context.scene.wgame_face_detail_texture_path,str(path))
        self.assertEqual(detail_binding._image(bpy.context.scene)[0],image)
        previous=path.read_bytes();data=decals.pixels(image);data[0,0]=(.3,.4,.5,1);decals.write_pixels(image,data)
        self.assertEqual(native_save(path),{'CANCELLED'});self.assertEqual(path.read_bytes(),previous)
        self.assertEqual(native_save(path,overwrite=True),{'FINISHED'})
        backups=list(path.parent.glob('*.before-detail-*.png'))
        self.assertEqual(len(backups),1);self.assertEqual(backups[0].read_bytes(),previous)
        self.assertNotEqual(path.read_bytes(),previous)

    def test_cancel_save_dialog_does_not_change_image_or_disk(self):
        self.assertEqual(native_new(),{'FINISHED'})
        image=bpy.context.scene.wgame_uv_tools.decal_target
        captured=[]
        proxy=SimpleNamespace(scene=bpy.context.scene,window_manager=SimpleNamespace(fileselect_add=lambda op:captured.append(op.filepath)))
        fake=SimpleNamespace(report=lambda *a:None)
        self.assertEqual(detail_images.WGAMEUV_OT_save_detail_image.invoke(fake,proxy,None),{'RUNNING_MODAL'})
        self.assertTrue(captured);self.assertEqual(image.filepath,'')
        self.assertEqual(bpy.context.scene.wgame_face_detail_texture_path,'')
        self.assertTrue(np.all(decals.pixels(image)==0))

    def test_native_undo_redo_restores_binding_and_image(self):
        bpy.context.preferences.edit.use_global_undo=True
        obj_name=self.obj.name;old_mat=self.mat.name;old_image=self.original.name
        bpy.ops.ed.undo_push(message='Before new detail image')
        self.assertEqual(native_new(),{'FINISHED'})
        new_name=bpy.context.scene.wgame_uv_tools.decal_target.name
        bpy.ops.ed.undo_push(message='New detail image')
        self.assertEqual(bpy.ops.ed.undo(),{'FINISHED'})
        self.obj=bpy.data.objects[obj_name]
        self.assertEqual(uvs(self.obj),self.before)
        self.assertEqual(self.obj.active_material.name,old_mat)
        self.assertEqual(self.space.image.name,old_image)
        self.assertEqual(bpy.ops.ed.redo(),{'FINISHED'})
        self.obj=bpy.data.objects[obj_name]
        self.assertEqual(self.obj.data.uv_layers.active.name,'DetailUV')
        self.assertEqual(bpy.context.scene.wgame_uv_tools.decal_target.name,new_name)
        self.assertEqual(self.space.image.name,new_name)

    def test_generated_pixels_survive_pack_with_stable_fingerprint(self):
        self.assertEqual(native_new(),{'FINISHED'})
        image=bpy.context.scene.wgame_uv_tools.decal_target
        source=Path(r'E:\Unity\WxFramework\WGameFramework\Tools\Blender\WGameRiggingAssistant\baked_preview_inputs.py')
        import runpy
        fingerprint=runpy.run_path(str(source))['source_fingerprint']
        data=decals.pixels(image);data[0,0]=(.2,.4,.6,.8);decals.write_pixels(image,data)
        before=decals.pixels(image).copy();digest=fingerprint(bpy.context.scene,self.output)
        image.pack()
        self.assertTrue(image.packed_file)
        self.assertTrue(np.array_equal(decals.pixels(image),before))
        self.assertEqual(fingerprint(bpy.context.scene,self.output),digest)


suite=unittest.defaultTestLoader.loadTestsFromTestCase(ImageTests)
result=unittest.TextTestRunner(verbosity=2).run(suite)
(OUT/'detail-images-test-result.json').write_text(json.dumps({'tests':result.testsRun,'failures':len(result.failures),'errors':len(result.errors),'passed':result.wasSuccessful()},indent=2),encoding='utf-8')
if not result.wasSuccessful():raise RuntimeError('Detail image regression failed')
