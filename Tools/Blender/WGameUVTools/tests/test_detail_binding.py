"""Native Blender regression: explicit image, protected UVs, transactional failure, Undo."""
from pathlib import Path
import sys, json, unittest
import bpy, bmesh
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
from WGameUVTools import detail_binding as binding
addon.register()
bpy.types.Scene.wgame_face_detail_texture_path = bpy.props.StringProperty(subtype='FILE_PATH')
bpy.types.Scene.wgame_face_detail_intensity = bpy.props.FloatProperty(default=1)
ROOT=Path(__file__).resolve().parents[2]
PNG=ROOT/'explicit-detail.png'
if not PNG.exists():
    image=bpy.data.images.new('Fixture PNG',width=512,height=512,alpha=True)
    image.filepath_raw=str(PNG);image.file_format='PNG';image.save();bpy.data.images.remove(image)


def native():
    try:
        return bpy.ops.wgame_uv.detail_lifecycle(action='PROMOTE')
    except RuntimeError:
        return {'CANCELLED'}


def layout(obj):
    if obj.mode=='EDIT':
        bm=bmesh.from_edit_mesh(obj.data)
        return {u.name:[(tuple(l[u].uv),l[u].pin_uv) for f in bm.faces for l in f.loops] for u in bm.loops.layers.uv.values()}
    return {u.name:[(tuple(l.uv),l.pin_uv) for l in u.data] for u in obj.data.uv_layers}


class DetailBindingTests(unittest.TestCase):
    def setUp(self):
        if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        bpy.ops.mesh.primitive_cube_add()
        self.obj=bpy.context.object;mesh=self.obj.data
        mesh.uv_layers.active.name='UV0'
        for name in ('PaletteUV','FaceDetailUV','DetailUV_Work'):
            u=mesh.uv_layers.new(name=name)
            u.data[0].pin_uv=True
            u=mesh.uv_layers[name]
            for n,l in enumerate(u.data):l.uv=(.1+n*.01,.2+len(mesh.uv_layers)*.01)
        mesh.uv_layers.active_index=3;mesh.uv_layers['UV0'].active_render=True
        self.mat=bpy.data.materials.new('Original Palette Preview');self.mat.use_nodes=True
        tree=self.mat.node_tree
        uv=tree.nodes.new('ShaderNodeUVMap');uv.uv_map='PaletteUV'
        self.palette=bpy.data.images.new('OriginalPalette',width=4,height=4)
        tex=tree.nodes.new('ShaderNodeTexImage');tex.image=self.palette
        shader=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
        tree.links.new(uv.outputs[0],tex.inputs['Vector']);tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
        mesh.materials.clear();mesh.materials.append(self.mat)
        self.unused=bpy.data.materials.new('Unused Material');mesh.materials.append(self.unused)
        self.other=self.obj.copy();self.other.data=mesh.copy();bpy.context.collection.objects.link(self.other)
        self.other.select_set(False)
        bpy.context.scene.wgame_face_detail_texture_path=str(PNG)
        self.editor=next((a for a in bpy.context.screen.areas if a.type=='IMAGE_EDITOR'),bpy.context.screen.areas[0])
        self.editor.type='IMAGE_EDITOR';self.editor.ui_type='UV'
        self.editor.spaces.active.image=self.palette;self.editor.spaces.active.use_image_pin=True
        self.before=layout(self.obj)

    def tearDown(self):
        if self.obj.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')

    def assert_bound(self):
        mesh=self.obj.data
        self.assertEqual(mesh.uv_layers.active.name,'DetailUV')
        after=layout(self.obj)
        self.assertEqual(after['DetailUV'],self.before['DetailUV_Work'])
        for n in ('UV0','PaletteUV','FaceDetailUV'):self.assertEqual(after[n],self.before[n])
        self.assertTrue(mesh.uv_layers['UV0'].active_render)
        self.assertIs(self.other.data.materials[0],self.mat)
        self.assertIs(mesh.materials[1],self.unused)
        tree=mesh.materials[0].node_tree
        self.assertEqual(tree.nodes['Detail_UV'].uv_map,'DetailUV')
        self.assertTrue(any(n.type=='UVMAP' and n.uv_map=='PaletteUV' for n in tree.nodes))
        texture=next(n for n in tree.nodes if n.type=='TEX_IMAGE' and n!=tree.nodes.get('OriginalPalette') and n.image!=self.palette)
        self.assertEqual(Path(texture.image.filepath).resolve(),PNG.resolve())
        self.assertEqual(texture.extension,'CLIP')
        self.assertIs(self.editor.spaces.active.image,texture.image)
        self.assertTrue(self.editor.spaces.active.use_image_pin)
        return texture.image

    def test_missing_node_native_edit_preserves_uv_selection_and_pin(self):
        bpy.ops.object.mode_set(mode='EDIT')
        bm=bmesh.from_edit_mesh(self.obj.data)
        selected=[f.select for f in bm.faces]
        uv_selected=[[bool(l[u]) for f in bm.faces for l in f.loops] for u in bm.loops.layers.bool.values()]
        self.before=layout(self.obj)
        self.assertEqual(native(),{'FINISHED'})
        self.assertEqual(bpy.context.mode,'EDIT_MESH')
        self.assertEqual([f.select for f in bmesh.from_edit_mesh(self.obj.data).faces],selected)
        bm=bmesh.from_edit_mesh(self.obj.data)
        self.assertEqual([[bool(l[u]) for f in bm.faces for l in f.loops] for u in bm.loops.layers.bool.values()],uv_selected)
        self.assert_bound()

    def test_repeat_click_has_no_new_images_materials_or_nodes(self):
        self.assertEqual(native(),{'FINISHED'})
        image=self.assert_bound()
        count=(len(bpy.data.images),len(bpy.data.materials),len(self.obj.active_material.node_tree.nodes))
        material=self.obj.active_material
        self.editor.spaces.active.image=self.palette
        self.assertEqual(native(),{'FINISHED'})
        self.assertEqual(count,(len(bpy.data.images),len(bpy.data.materials),len(material.node_tree.nodes)))
        self.assertIs(self.obj.active_material,material)
        self.assertIs(self.editor.spaces.active.image,image)

    def test_missing_png_leaves_everything_intact(self):
        bpy.context.scene.wgame_face_detail_texture_path=''
        before=(len(bpy.data.images),len(bpy.data.materials))
        bpy.ops.object.mode_set(mode='EDIT');self.before=layout(self.obj)
        self.assertEqual(native(),{'CANCELLED'})
        self.assertEqual(bpy.context.mode,'EDIT_MESH')
        self.assertEqual(layout(self.obj),self.before)
        self.assertIs(self.obj.active_material,self.mat)
        self.assertEqual(before,(len(bpy.data.images),len(bpy.data.materials)))

    def test_non_rgba_png_does_not_load_or_change_uv(self):
        bad=ROOT/'wrong-size.png'
        i=bpy.data.images.new('Wrong',width=8,height=8,alpha=False)
        i.filepath_raw=str(bad);i.file_format='PNG';i.save();bpy.data.images.remove(i)
        bpy.context.scene.wgame_face_detail_texture_path=str(bad)
        before=(len(bpy.data.images),len(bpy.data.materials))
        self.assertEqual(native(),{'CANCELLED'})
        self.assertEqual(layout(self.obj),self.before)
        self.assertEqual(before,(len(bpy.data.images),len(bpy.data.materials)))

    def test_conflict_requires_confirmation_and_keeps_backup(self):
        mesh=self.obj.data;old=mesh.uv_layers.new(name='DetailUV')
        for l in old.data:l.uv=(.7,.8)
        original=[tuple(l.uv) for l in old.data]
        mesh.uv_layers.active_index=3
        self.assertEqual(native(),{'CANCELLED'})
        self.assertEqual(mesh.uv_layers['DetailUV'].name,'DetailUV')
        binding.bind(bpy.context,self.obj,allow_replace=True)
        self.assert_bound()
        self.assertEqual([tuple(l.uv) for l in mesh.uv_layers['DetailUV_Old'].data],original)
        self.assertEqual(json.loads(mesh[binding.BACKUPS]),['DetailUV_Old'])

    def test_commit_failure_rolls_back_all_data_and_owned_ids(self):
        image=bpy.data.images.get('explicit-detail.png')
        if image and image.users==0:bpy.data.images.remove(image)
        before=(len(bpy.data.images),len(bpy.data.materials))
        bpy.ops.object.mode_set(mode='EDIT');self.before=layout(self.obj)
        def fail():raise RuntimeError('Injected commit failure')
        with self.assertRaisesRegex(RuntimeError,'Injected'):
            binding.bind(bpy.context,self.obj,commit_hook=fail)
        self.assertEqual(layout(self.obj),self.before)
        self.assertIs(self.obj.active_material,self.mat)
        self.assertEqual(bpy.context.mode,'EDIT_MESH')
        self.assertIs(self.editor.spaces.active.image,self.palette)
        self.assertEqual(before,(len(bpy.data.images),len(bpy.data.materials)))

    def test_ambiguous_material_rolls_back_staged_image(self):
        tree=self.mat.node_tree
        for n in range(2):
            uv=tree.nodes.new('ShaderNodeUVMap');uv.uv_map='DetailUV_Work'
            tex=tree.nodes.new('ShaderNodeTexImage');tree.links.new(uv.outputs[0],tex.inputs['Vector'])
        before=(len(bpy.data.images),len(bpy.data.materials))
        self.assertEqual(native(),{'CANCELLED'})
        self.assertEqual(layout(self.obj),self.before)
        self.assertEqual(before,(len(bpy.data.images),len(bpy.data.materials)))

    def test_dirty_image_pixels_are_not_reloaded_or_saved(self):
        image=bpy.data.images.load(str(PNG),check_existing=True)
        image.pixels[0]=.315;image.update()
        pixel=image.pixels[0]
        file_hash=__import__('hashlib').sha256(PNG.read_bytes()).hexdigest()
        self.assertEqual(native(),{'FINISHED'})
        self.assertEqual(image.pixels[0],pixel)
        self.assertEqual(__import__('hashlib').sha256(PNG.read_bytes()).hexdigest(),file_hash)
        self.assertIs(self.assert_bound(),image)

    def test_native_undo_restores_work_uv_material_and_image(self):
        bpy.context.preferences.edit.use_global_undo=True
        name=self.obj.name;material_name=self.mat.name;palette_name=self.palette.name
        bpy.ops.ed.undo_push(message='Before detail binding')
        self.assertEqual(native(),{'FINISHED'})
        bpy.ops.ed.undo_push(message='Detail binding complete')
        self.assertTrue(bpy.ops.ed.undo.poll())
        self.assertEqual(bpy.ops.ed.undo(),{'FINISHED'})
        self.obj=bpy.data.objects[name]
        self.assertEqual(layout(self.obj),self.before)
        self.assertEqual(self.obj.active_material.name,material_name)
        self.assertEqual(self.editor.spaces.active.image.name,palette_name)
        self.assertEqual(bpy.ops.ed.redo(),{'FINISHED'})
        self.obj=bpy.data.objects[name]
        self.assertEqual(self.obj.data.uv_layers.active.name,'DetailUV')
        self.assertEqual(self.editor.spaces.active.image.name,'explicit-detail.png')

    def test_edit_conflict_commit_failure_keeps_both_layouts(self):
        self.obj.data.uv_layers.new(name='DetailUV')
        self.obj.data.uv_layers.active_index=3
        bpy.ops.object.mode_set(mode='EDIT')
        before=layout(self.obj)
        def fail():raise RuntimeError('Injected replacement failure')
        with self.assertRaisesRegex(RuntimeError,'Injected'):
            binding.bind(bpy.context,self.obj,allow_replace=True,commit_hook=fail)
        self.assertEqual(layout(self.obj),before)
        self.assertEqual(bpy.context.mode,'EDIT_MESH')
        self.assertIs(self.obj.active_material,self.mat)

    def test_protected_uv_never_replaced(self):
        self.obj.data.uv_layers.active_index=1
        self.assertEqual(native(),{'CANCELLED'})
        self.assertEqual(layout(self.obj),self.before)
        self.assertIs(self.obj.active_material,self.mat)


suite=unittest.defaultTestLoader.loadTestsFromTestCase(DetailBindingTests)
result=unittest.TextTestRunner(verbosity=2).run(suite)
(ROOT/'binding-test-result.json').write_text(json.dumps({'tests':result.testsRun,'failures':len(result.failures),'errors':len(result.errors),'passed':result.wasSuccessful()},indent=2),encoding='utf-8')
if not result.wasSuccessful():raise RuntimeError('Detail binding regression failed')
