"""Plain palette colors are exact data, never a ray-baked appearance guess."""
from pathlib import Path
import runpy
import unittest
import tempfile
import bpy
import numpy as np

tool=Path(__file__).parent
palette=runpy.run_path(str(tool/'baked_palette.py'))
baker=runpy.run_path(str(tool/'baked_surface.py'))
atlas=runpy.run_path(str(tool/'baked_atlas.py'))


class PaletteBakeTests(unittest.TestCase):
    def setUp(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mesh=bpy.data.meshes.new('PaletteFixture');mesh.from_pydata([(0,0,0),(1,0,0),(0,1,0),(0,0,0),(0,1,0),(1,0,0)],[],[(0,1,2),(3,4,5)]);mesh.update()
        self.obj=bpy.data.objects.new('PaletteFixture',mesh);bpy.context.collection.objects.link(self.obj);self.obj.select_set(True);bpy.context.view_layer.objects.active=self.obj
        source=mesh.uv_layers.new(name='PaletteUV');dest=mesh.uv_layers.new(name='BakeUV')
        for i,p in enumerate(mesh.polygons):
            for loop,co in zip(p.loop_indices,((.1,.1),(.4,.1),(.1,.4)) if i==0 else ((.6,.6),(.9,.6),(.6,.9))):
                source.data[loop].uv=(.25 if i==0 else .75,.5);dest.data[loop].uv=co
        data=mesh.color_attributes.new(name='BakedSurfaceSource',type='FLOAT_COLOR',domain='CORNER')
        for value in data.data:value.color=(1,0,0,72/255)
        mat=bpy.data.materials.new('PaletteMaterial');mesh.materials.append(mat);tree=mat.node_tree
        image=bpy.data.images.new('Palette',width=2,height=1);image.colorspace_settings.name='sRGB';image.pixels[:]=(.1,.1,.1,1,.65,.08,.12,1)
        self.texture=tree.nodes.new('ShaderNodeTexImage');self.texture.image=image;self.texture.interpolation='Closest'
        uv=tree.nodes.new('ShaderNodeUVMap');uv.uv_map='PaletteUV';tree.links.new(uv.outputs[0],self.texture.inputs['Vector'])
        tree.links.new(self.texture.outputs['Color'],next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'])

    def test_opposite_coincident_surfaces_keep_their_own_palette_colors(self):
        colors=palette['read_palette_faces'](self.obj)
        self.assertNotEqual(colors[0],colors[1])
        image=baker['make_image']('Result',64)
        baker['write_palette_base']([self.obj],image,{self.obj.name:colors},64)
        pixels=baker['pixels'](image).reshape(64,64,4)
        for face,coord in ((0,(.2,.2)),(1,(.7,.7))):
            actual=pixels[int(coord[1]*64),int(coord[0]*64),:3]
            np.testing.assert_allclose(actual,np.array(colors[face])/255,atol=1e-6)
        self.assertGreater(pixels[44,44,0],pixels[44,44,1]*3)
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'BaseColor.png';baker['save_png'](image,path)
            loaded=bpy.data.images.load(str(path));loaded.colorspace_settings.name='Non-Color'
            saved=baker['pixels'](loaded).reshape(64,64,4)
            for face,coord in ((0,(.2,.2)),(1,(.7,.7))):
                np.testing.assert_allclose(saved[int(coord[1]*64),int(coord[0]*64),:3],np.array(colors[face])/255,atol=1/255)

    def test_real_detail_graph_and_linear_sampler_use_general_bake(self):
        self.texture.interpolation='Linear';self.assertIsNone(palette['read_palette_faces'](self.obj))
        self.texture.interpolation='Closest';tree=self.obj.data.materials[0].node_tree;mix=tree.nodes.new('ShaderNodeMixRGB')
        tree.links.new(self.texture.outputs[0],mix.inputs[1]);tree.links.new(mix.outputs[0],next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'])
        self.assertIsNone(palette['read_palette_faces'](self.obj))

    def test_a_face_crossing_palette_cells_cannot_be_called_plain(self):
        self.obj.data.uv_layers['PaletteUV'].data[0].uv.x=.75
        self.assertIsNone(palette['read_palette_faces'](self.obj))

    def test_multiple_flat_colors_are_not_classified_as_painted_detail(self):
        mesh=self.obj.data;mesh.clear_geometry();mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2),(0,2,3)]);mesh.update()
        uv=mesh.uv_layers.get('BakeUV') or mesh.uv_layers.new(name='BakeUV')
        for loop in mesh.loops:uv.data[loop.index].uv=tuple(v*.6+.2 for v in mesh.vertices[loop.vertex_index].co.xy)
        size=64;color=np.zeros((size,size,4),np.float32);color[:32,:,:]=(.1,.1,.1,1);color[32:,:,:]=(.65,.08,.12,1)
        control=np.tile((1.,0.,0.,72/255),(size*size,1)).astype(np.float32)
        control[:,0]=np.linspace(.5,1.,size*size)
        result=atlas['compact']([self.obj],{self.obj.name:{}},color.reshape(-1,4),control,size,flat_color_faces={self.obj.name:{0,1}})
        self.assertEqual(result['stats']['detailCharts'],0)
        self.assertEqual(result['stats']['surfaceCharts'],1)


result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(PaletteBakeTests))
if not result.wasSuccessful():raise RuntimeError('Palette bake tests failed')
