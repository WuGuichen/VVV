"""Sidebar-only file drops, image ownership, cancellation and native Undo."""
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import sys,unittest,tempfile,json,hashlib,struct,zlib
import bpy,numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
from WGameUVTools import decal_drop,decals,canvas_editor
addon.register()
OUT=Path(__file__).resolve().parents[2]

def poll_context(area='IMAGE_EDITOR',region='UI',category='WGame UV',tab='DECAL'):
    return SimpleNamespace(area=SimpleNamespace(type=area),region=SimpleNamespace(type=region,active_panel_category=category),
                           scene=SimpleNamespace(wgame_uv_tools=SimpleNamespace(ui_tab_uv=tab)),space_data=SimpleNamespace(mode='UV'))

class Drops(unittest.TestCase):
    def setUp(self):
        canvas_editor.cancel();decals.cancel()
        if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        for image in list(bpy.data.images):
            if image.name not in {'Render Result','Viewer Node'}:bpy.data.images.remove(image)
        bpy.ops.mesh.primitive_plane_add();self.obj=bpy.context.object;self.obj.data.uv_layers.active.name='DetailUV'
        self.area=next((a for a in bpy.context.screen.areas if a.type=='IMAGE_EDITOR'),bpy.context.screen.areas[0]);self.area.type='IMAGE_EDITOR'
        self.area.spaces.active.mode='UV';self.area.spaces.active.show_region_ui=True
        self.ui=next(r for r in self.area.regions if r.type=='UI')
        settings=bpy.context.scene.wgame_uv_tools;settings.ui_tab_uv='DECAL';settings.decal_workflow='CANVAS';settings.decal_source=None
        self.target=bpy.data.images.new('Protected canvas',width=16,height=16,alpha=True)
        values=np.zeros((16,16,4),np.float32);values[0,0]=(.1,.2,.3,.4);decals.write_pixels(self.target,values)
        self.area.spaces.active.image=self.target;self.area.spaces.active.use_image_pin=True;settings.decal_target=self.target
        mat=bpy.data.materials.new('Original material');mat.use_nodes=True
        tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=self.target
        uv=mat.node_tree.nodes.new('ShaderNodeUVMap');uv.uv_map='DetailUV';mat.node_tree.links.new(uv.outputs['UV'],tex.inputs['Vector'])
        self.obj.data.materials.append(mat)
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        self.path=self.root/'素材.PNG';self.source_file(self.path)

    def tearDown(self):
        canvas_editor.cancel();decals.cancel();self.temp.cleanup()

    def source_file(self,path,rgba=True,size=4):
        path.parent.mkdir(parents=True,exist_ok=True)
        if not rgba:
            def chunk(kind,data):
                return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
            path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',size,size,8,2,0,0,0))
                             +chunk(b'IDAT',zlib.compress((b'\x00'+b'\x80\x40\x20'*size)*size))+chunk(b'IEND',b''))
            return
        image=bpy.data.images.new('Disk source',width=size,height=1 if size>4096 else size,alpha=rgba)
        if rgba:decals.write_pixels(image,np.full((image.size[1],size,4),(.8,.3,.1,.6),np.float32))
        image.file_format='PNG';image.filepath_raw=str(path);image.save();bpy.data.images.remove(image)

    def drop(self,path=None,files=None):
        # Background Blender does not draw/select N-panel categories. Test the actual
        # category poll separately; the GUI integration checks it without this override.
        with patch.object(decal_drop,'drop_target',return_value=True),bpy.context.temp_override(area=self.area,region=self.ui):
            try:
                return bpy.ops.wgame_uv.drop_decal_image(filepath=str(path or ''),directory=str(self.root),files=files or [])
            except RuntimeError:return {'CANCELLED'}

    def protected(self):
        settings=bpy.context.scene.wgame_uv_tools
        return {'pixels':hashlib.sha256(decals.pixels(self.target).tobytes()).hexdigest(),'image':self.area.spaces.active.image.as_pointer(),
                'pin':self.area.spaces.active.use_image_pin,'target':settings.decal_target.as_pointer(),'mode':bpy.context.mode,
                'uvs':[tuple(l.uv) for l in self.obj.data.uv_layers.active.data],
                'materials':[m.as_pointer() for m in self.obj.data.materials],'nodes':[(n.name,n.image.as_pointer()) for n in self.obj.active_material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]}

    def test_poll_is_limited_to_current_decal_sidebar_category(self):
        self.assertTrue(decal_drop.WGAMEUV_FH_decal_source.poll_drop(poll_context()))
        for values in ({'area':'VIEW_3D'},{'area':'NODE_EDITOR'},{'area':'FILE_BROWSER'},{'region':'WINDOW'},{'region':'HEADER'},
                       {'category':'Image'},{'category':'Tool'},{'tab':'CREATE'},{'tab':'CHANNELS'}):
            self.assertFalse(decal_drop.WGAMEUV_FH_decal_source.poll_drop(poll_context(**values)),values)

    def test_native_unicode_png_drop_preserves_canvas_uv_material_mode_and_pin(self):
        before=self.protected();disk=self.path.read_bytes();self.assertEqual(self.drop(self.path),{'FINISHED'})
        image=bpy.context.scene.wgame_uv_tools.decal_source
        self.assertEqual(image.channels,4);self.assertEqual(tuple(image.size),(4,4));self.assertEqual(self.protected(),before)
        self.assertEqual(self.path.read_bytes(),disk);self.assertIn('摆放已加载素材',bpy.context.scene.wgame_uv_tools.decal_message)

    def test_filehandler_directory_files_path_and_multi_file_rejection(self):
        before=self.protected();self.assertEqual(self.drop(files=[{'name':self.path.name}]),{'FINISHED'})
        source=bpy.context.scene.wgame_uv_tools.decal_source;count=len(bpy.data.images)
        self.assertEqual(self.drop(files=[{'name':self.path.name},{'name':'another.png'}]),{'CANCELLED'})
        self.assertIs(bpy.context.scene.wgame_uv_tools.decal_source,source);self.assertEqual(len(bpy.data.images),count);self.assertEqual(self.protected(),before)
        self.assertIn('每次只拖入一张',bpy.context.scene.wgame_uv_tools.decal_message)

    def test_duplicate_import_reuses_dirty_memory_instead_of_reloading(self):
        self.assertEqual(self.drop(self.path),{'FINISHED'});image=bpy.context.scene.wgame_uv_tools.decal_source
        data=decals.pixels(image);data[0,0]=(0,1,0,.2);decals.write_pixels(image,data);before=decals.pixels(image).copy();count=len(bpy.data.images)
        self.assertEqual(self.drop(self.path),{'FINISHED'});self.assertIs(bpy.context.scene.wgame_uv_tools.decal_source,image)
        self.assertEqual(len(bpy.data.images),count);self.assertTrue(np.array_equal(decals.pixels(image),before))
        self.assertIn('保留素材的内存修改',bpy.context.scene.wgame_uv_tools.decal_message)

    def test_same_filename_from_another_directory_keeps_both_images(self):
        self.assertEqual(self.drop(self.path),{'FINISHED'});first=bpy.context.scene.wgame_uv_tools.decal_source;pixels=decals.pixels(first).copy()
        second_path=self.root/'other'/self.path.name;self.source_file(second_path)
        self.assertEqual(self.drop(second_path),{'FINISHED'});second=bpy.context.scene.wgame_uv_tools.decal_source
        self.assertIsNot(first,second);self.assertNotEqual(first.name,second.name);self.assertTrue(np.array_equal(decals.pixels(first),pixels))

    def test_invalid_missing_corrupt_non_rgba_and_large_sources_leave_no_image_leaks(self):
        rgb=self.root/'rgb.png';self.source_file(rgb,rgba=False)
        large=self.root/'large.png';self.source_file(large,size=4097)
        corrupt=self.root/'corrupt.png';corrupt.write_bytes(b'not a png')
        unsupported=self.root/'asset.jpg';unsupported.write_bytes(b'ignored')
        before=self.protected();count=len(bpy.data.images)
        for path in (self.root/'missing.png',corrupt,large,unsupported,self.root):
            self.assertEqual(self.drop(path),{'CANCELLED'},str(path));self.assertEqual(len(bpy.data.images),count)
            self.assertIsNone(bpy.context.scene.wgame_uv_tools.decal_source);self.assertEqual(self.protected(),before)

    def test_rgb_png_follows_existing_blender_opaque_rgba_expansion(self):
        path=self.root/'rgb.png';self.source_file(path,rgba=False);before=self.protected()
        self.assertEqual(self.drop(path),{'FINISHED'})
        image=bpy.context.scene.wgame_uv_tools.decal_source
        self.assertEqual(image.channels,4);self.assertTrue(np.all(decals.pixels(image)[:,:,3]==1))
        self.assertEqual(self.protected(),before)

    def test_current_canvas_cannot_be_used_as_decal_source(self):
        self.target.filepath_raw=str(self.path);before=self.protected()
        self.assertEqual(self.drop(self.path),{'CANCELLED'});self.assertEqual(self.protected(),before)
        self.assertIsNone(bpy.context.scene.wgame_uv_tools.decal_source)

    def test_active_preview_or_floating_selection_is_preserved(self):
        before=self.protected();sentinel={'fixture':'keep'}
        for module in (decals,canvas_editor):
            module._session=sentinel
            try:
                self.assertEqual(self.drop(self.path),{'CANCELLED'});self.assertIs(module._session,sentinel)
                self.assertIsNone(bpy.context.scene.wgame_uv_tools.decal_source);self.assertEqual(self.protected(),before)
            finally:module._session=None

    def test_drop_can_continue_to_existing_placement_and_cancel_without_painting(self):
        before=self.protected();self.assertEqual(self.drop(self.path),{'FINISHED'});image=bpy.context.scene.wgame_uv_tools.decal_source
        with bpy.context.temp_override(area=self.area):
            session=canvas_editor.begin(bpy.context,'IMPORT',image);canvas_editor.render();self.assertEqual(session['source'],image)
            self.assertEqual(hashlib.sha256(decals.pixels(self.target).tobytes()).hexdigest(),before['pixels'])
            canvas_editor.cancel()
        self.assertEqual(self.protected(),before)

    def test_native_undo_redo_restores_source_selection_and_keeps_canvas(self):
        bpy.context.preferences.edit.use_global_undo=True;target_name=self.target.name;obj_name=self.obj.name
        values=decals.pixels(self.target).copy();bpy.ops.ed.undo_push(message='Before source drop')
        self.assertEqual(self.drop(self.path),{'FINISHED'});source_name=bpy.context.scene.wgame_uv_tools.decal_source.name
        bpy.ops.ed.undo_push(message='Source drop');self.assertEqual(bpy.ops.ed.undo(),{'FINISHED'})
        self.target=bpy.data.images[target_name];self.obj=bpy.data.objects[obj_name]
        self.assertIsNone(bpy.context.scene.wgame_uv_tools.decal_source);self.assertTrue(np.array_equal(decals.pixels(self.target),values))
        self.assertEqual(bpy.ops.ed.redo(),{'FINISHED'});self.target=bpy.data.images[target_name];self.obj=bpy.data.objects[obj_name]
        self.assertEqual(bpy.context.scene.wgame_uv_tools.decal_source.name,source_name);self.assertTrue(np.array_equal(decals.pixels(self.target),values))
        self.assertEqual(self.area.spaces.active.image.name,target_name)

result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Drops))
(OUT/'drop-test-result.json').write_text(json.dumps({'tests':result.testsRun,'errors':len(result.errors),'failures':len(result.failures),'passed':result.wasSuccessful()},indent=2),encoding='utf-8')
if not result.wasSuccessful():raise RuntimeError('Decal drop regression failed')
