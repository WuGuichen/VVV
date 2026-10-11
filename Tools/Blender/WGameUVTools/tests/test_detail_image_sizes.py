"""Native authoring sizes are independent of final bake size and never resize images."""
from pathlib import Path
import sys,unittest,tempfile,json,struct,hashlib
import bpy,numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
from WGameUVTools import detail_images,detail_binding,decals
addon.register()
bpy.types.Scene.wgame_face_detail_texture_path=bpy.props.StringProperty(subtype='FILE_PATH')
bpy.types.Scene.wgame_bake_texture_size=bpy.props.StringProperty(default='512')
OUT=Path(__file__).resolve().parents[2]


class Sizes(unittest.TestCase):
    def setUp(self):
        if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        bpy.ops.mesh.primitive_plane_add()
        self.obj=bpy.context.object
        mesh=self.obj.data;mesh.uv_layers.active.name='UV0';mesh.uv_layers.new(name='PaletteUV');mesh.uv_layers.new(name='Work');mesh.uv_layers.active_index=2
        mat=bpy.data.materials.new('Original');mat.use_nodes=True;mesh.materials.clear();mesh.materials.append(mat)
        bpy.context.scene.wgame_face_detail_texture_path='';bpy.context.scene.wgame_uv_tools.decal_target=None
        bpy.context.scene.wgame_bake_texture_size='512'
        self.temp=tempfile.TemporaryDirectory();self.output=Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def test_default_native_create_bind_save_2048(self):
        self.assertNotIn('resolution',bpy.ops.wgame_uv.new_detail_canvas.get_rna_type().properties)
        self.assertEqual(bpy.ops.wgame_uv.new_detail_canvas(),{'FINISHED'})
        image=bpy.context.scene.wgame_uv_tools.decal_target
        self.assertEqual(tuple(image.size),(2048,2048));self.assertTrue(np.all(decals.pixels(image)==0))
        self.assertIs(self.obj.active_material.node_tree.nodes['Detail_Texture'].image,image)
        self.assertEqual(bpy.context.scene.wgame_bake_texture_size,'512')
        path=self.output/'source-2048.png'
        self.assertEqual(bpy.ops.wgame_uv.save_detail_image(filepath=str(path)),{'FINISHED'})
        header=path.read_bytes()[:26]
        self.assertEqual(struct.unpack('>II',header[16:24]),(2048,2048));self.assertEqual(header[25],6)
        self.assertEqual(tuple(image.size),(2048,2048))

    def test_existing_512_and_1024_rgba_remain_bindable_and_saveable(self):
        for resolution in (512,1024):
            image=bpy.data.images.new('Existing '+str(resolution),width=resolution,height=resolution,alpha=True)
            values=np.zeros((resolution,resolution,4),dtype=np.float32);values[0,0]=(.2,.4,.6,.8);decals.write_pixels(image,values)
            path=self.output/f'existing-{resolution}.png';image.filepath_raw=str(path);image.file_format='PNG';image.save()
            pixels=decals.pixels(image).copy();source_bytes=path.read_bytes()
            bpy.context.scene.wgame_face_detail_texture_path=str(path)
            self.assertEqual(bpy.ops.wgame_uv.detail_lifecycle(action='PROMOTE'),{'FINISHED'})
            self.assertEqual(tuple(image.size),(resolution,resolution))
            self.assertIs(self.obj.active_material.node_tree.nodes['Detail_Texture'].image,image)
            self.assertEqual(path.read_bytes(),source_bytes)
            saved=self.output/f'saved-{resolution}.png'
            self.assertEqual(bpy.ops.wgame_uv.save_detail_image(filepath=str(saved)),{'FINISHED'})
            self.assertEqual(struct.unpack('>II',saved.read_bytes()[16:24]),(resolution,resolution))
            self.assertTrue(np.array_equal(decals.pixels(image),pixels))
            self.assertEqual(bpy.context.scene.wgame_bake_texture_size,'512')

    def test_existing_2048_png_bind_save_preserves_pixels_and_dimensions(self):
        image=bpy.data.images.new('Existing 2048',width=2048,height=2048,alpha=True)
        values=np.zeros((2048,2048,4),dtype=np.float32);values[0,0]=(.2,.4,.6,.8);decals.write_pixels(image,values)
        path=self.output/'existing.png';image.filepath_raw=str(path);image.file_format='PNG';image.save()
        before=decals.pixels(image).copy();source_bytes=path.read_bytes()
        bpy.context.scene.wgame_face_detail_texture_path=str(path)
        self.assertEqual(bpy.ops.wgame_uv.detail_lifecycle(action='PROMOTE'),{'FINISHED'})
        self.assertIs(self.obj.active_material.node_tree.nodes['Detail_Texture'].image,image)
        self.assertEqual(tuple(image.size),(2048,2048));self.assertTrue(np.array_equal(decals.pixels(image),before))
        self.assertEqual(path.read_bytes(),source_bytes)
        saved=self.output/'saved.png'
        self.assertEqual(bpy.ops.wgame_uv.save_detail_image(filepath=str(saved)),{'FINISHED'})
        self.assertTrue(np.array_equal(decals.pixels(image),before))
        self.assertEqual(struct.unpack('>II',saved.read_bytes()[16:24]),(2048,2048))

    def test_existing_non_square_rgba_is_not_resized(self):
        image=bpy.data.images.new('NonSquare',width=1024,height=512,alpha=True)
        path=self.output/'non-square.png';image.filepath_raw=str(path);image.file_format='PNG';image.save()
        bpy.context.scene.wgame_face_detail_texture_path=str(path)
        self.assertEqual(bpy.ops.wgame_uv.detail_lifecycle(action='PROMOTE'),{'FINISHED'})
        self.assertEqual(tuple(image.size),(1024,512))
        self.assertEqual(bpy.ops.wgame_uv.save_detail_image(filepath=str(self.output/'copy.png')),{'FINISHED'})
        self.assertEqual(tuple(image.size),(1024,512))

    def test_native_undo_redo_2048(self):
        bpy.context.preferences.edit.use_global_undo=True
        name=self.obj.name
        bpy.ops.ed.undo_push(message='Before 2048 canvas')
        self.assertEqual(bpy.ops.wgame_uv.new_detail_canvas(),{'FINISHED'})
        image_name=bpy.context.scene.wgame_uv_tools.decal_target.name
        bpy.ops.ed.undo_push(message='2048 canvas')
        self.assertEqual(bpy.ops.ed.undo(),{'FINISHED'})
        self.obj=bpy.data.objects[name]
        self.assertEqual(self.obj.data.uv_layers.active.name,'Work')
        self.assertEqual(bpy.ops.ed.redo(),{'FINISHED'})
        self.obj=bpy.data.objects[name]
        self.assertEqual(self.obj.data.uv_layers.active.name,'DetailUV')
        self.assertEqual(tuple(bpy.data.images[image_name].size),(2048,2048))
        self.assertEqual(bpy.context.scene.wgame_bake_texture_size,'512')


result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Sizes))
(OUT/'sizes-test-result.json').write_text(json.dumps({'tests':result.testsRun,'errors':len(result.errors),'failures':len(result.failures),'passed':result.wasSuccessful()},indent=2),encoding='utf-8')
if not result.wasSuccessful():raise RuntimeError('Authoring size regression failed')
