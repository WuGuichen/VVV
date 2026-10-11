"""Run: blender -b --factory-startup --python-exit-code 17 --python <this file>."""
import json
from pathlib import Path
import runpy
import tempfile
import unittest
import bpy
from mathutils import Vector

baker=runpy.run_path(str(Path(__file__).with_name('baked_surface.py')))
hf=baker['hair_module']()


def plane():
    m=bpy.data.meshes.new('HairFixture')
    m.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2),(0,2,3)])
    m.update()
    o=bpy.data.objects.new('HairFixture',m);bpy.context.collection.objects.link(o)
    p=hf.attribute(m,'SurfaceProfile','INT','FACE')
    for item in p.data:item.value=3
    return o


class BakedSurfaceTests(unittest.TestCase):
    def test_control_png_keeps_rgb_independent_of_surface_profile_alpha(self):
        import numpy as np
        image=baker['make_image']('ControlChannelRoundtrip',4,True)
        expected=np.tile([.25,.125,.5,184/255],(16,1)).astype(np.float32)
        image.pixels.foreach_set(expected.ravel())
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'SurfaceData.png';baker['save_png'](image,path)
            loaded=bpy.data.images.load(str(path),check_existing=False)
            loaded.colorspace_settings.name='Non-Color';loaded.alpha_mode='CHANNEL_PACKED'
            self.assertTrue(np.allclose(baker['pixels'](loaded),expected,atol=1/255))

    def detail_fixture(self):
        obj,uv,image,source=self.palette_fixture();mesh=obj.data;tree=mesh.materials[0].node_tree
        detail_uv=mesh.uv_layers.new(name='DetailUV')
        for loop in mesh.loops:detail_uv.data[loop.index].uv=mesh.vertices[loop.vertex_index].co.xy
        detail=tree.nodes.new('ShaderNodeTexImage');detail.image=image.image;detail.interpolation='Linear'
        uv_node=tree.nodes.new('ShaderNodeUVMap');uv_node.uv_map='DetailUV'
        mix=tree.nodes.new('ShaderNodeMixRGB')
        tree.links.new(uv_node.outputs[0],detail.inputs['Vector'])
        tree.links.new(image.outputs['Color'],mix.inputs[1]);tree.links.new(detail.outputs['Color'],mix.inputs[2])
        tree.links.new(mix.outputs[0],next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'])
        return obj,uv,mix

    def test_detail_key_proves_shared_uv_function_and_detects_palette_and_profile_conflicts(self):
        obj,uv,mix=self.detail_fixture()
        keys=baker['detail_reuse_keys'](obj,{0,1})
        self.assertIsNotNone(keys[0]);self.assertEqual(keys[0],keys[1])
        for i in obj.data.polygons[1].loop_indices:uv.data[i].uv=(.2,.2)
        self.assertNotEqual(baker['detail_reuse_keys'](obj,{0,1})[1],keys[0])
        for i in obj.data.polygons[1].loop_indices:uv.data[i].uv=(.51,.51)
        for i in obj.data.polygons[1].loop_indices:obj.data.color_attributes['BakedSurfaceSource'].data[i].color=(1,1,0,24/255)
        self.assertNotEqual(baker['detail_reuse_keys'](obj,{0,1})[1],keys[0])

    def test_hair_shares_compatible_detail_but_position_dependent_albedo_does_not(self):
        obj,uv,mix=self.detail_fixture()
        hair=obj.data.attributes.new(name='WGameBakeHair',type='FLOAT',domain='FACE');hair.data[0].value=1
        hair.data[1].value=1
        keys=baker['detail_reuse_keys'](obj,{0,1})
        self.assertIsNotNone(keys[0]);self.assertEqual(keys[0],keys[1])
        hair.data[1].value=0
        keys=baker['detail_reuse_keys'](obj,{0,1})
        self.assertNotEqual(keys[0],keys[1])
        tree=obj.data.materials[0].node_tree;geometry=tree.nodes.new('ShaderNodeNewGeometry')
        tree.links.new(geometry.outputs['Position'],mix.inputs[1])
        self.assertIsNone(baker['detail_reuse_keys'](obj,{0,1})[1])

    def test_normal_dependent_image_projection_does_not_share_detail(self):
        obj,uv,mix=self.detail_fixture()
        for node in obj.data.materials[0].node_tree.nodes:
            if node.type=='TEX_IMAGE':node.projection='BOX'
        self.assertTrue(all(key is None for key in baker['detail_reuse_keys'](obj,{0,1}).values()))

    def emission_detail_fixture(self):
        obj,uv,mix=self.detail_fixture();tree=obj.data.materials[0].node_tree
        texture=tree.nodes.new('ShaderNodeTexImage')
        texture.image=bpy.data.images.new('EmissionMask',width=16,height=16)
        texture.extension='CLIP';texture.interpolation='Linear'
        source=tree.nodes.new('ShaderNodeUVMap');source.uv_map='DetailUV'
        tree.links.new(source.outputs['UV'],texture.inputs['Vector'])
        bsdf=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
        tree.links.new(texture.outputs['Color'],bsdf.inputs['Emission Strength'])
        for item in obj.data.color_attributes['BakedSurfaceSource'].data:item.color=(1,1,1,8/255)
        return obj,texture,source

    def test_uv_driven_emission_reuses_equal_detail_and_preserves_distinct_controls(self):
        obj,texture,source=self.emission_detail_fixture()
        keys=baker['detail_reuse_keys'](obj,{0,1})
        self.assertIsNotNone(keys[0]);self.assertEqual(keys[0],keys[1])
        for i in obj.data.polygons[1].loop_indices:obj.data.color_attributes['BakedSurfaceSource'].data[i].color=(1,1,.5,8/255)
        self.assertNotEqual(baker['detail_reuse_keys'](obj,{0,1})[1],keys[0])

    def test_context_dependent_emission_still_cannot_share_detail(self):
        obj,texture,source=self.emission_detail_fixture();tree=obj.data.materials[0].node_tree
        geometry=tree.nodes.new('ShaderNodeNewGeometry')
        tree.links.new(geometry.outputs['Position'],texture.inputs['Vector'])
        self.assertTrue(all(key is None for key in baker['detail_reuse_keys'](obj,{0,1}).values()))

    def test_other_varying_emission_uv_still_cannot_share_detail(self):
        obj,texture,source=self.emission_detail_fixture()
        layer=obj.data.uv_layers.new(name='OtherEmissionUV');source.uv_map=layer.name
        for loop in obj.data.loops:layer.data[loop.index].uv=obj.data.vertices[loop.vertex_index].co.xy
        self.assertTrue(all(key is None for key in baker['detail_reuse_keys'](obj,{0,1}).values()))

    def palette_fixture(self):
        obj=plane();mesh=obj.data
        uv=mesh.uv_layers.new(name='PaletteUV')
        for i,item in enumerate(uv.data):item.uv=(.51+(i%3)*1e-7,.51)
        data=mesh.color_attributes.new(name='BakedSurfaceSource',type='FLOAT_COLOR',domain='CORNER')
        for item in data.data:item.color=(1,1,0,8/255)
        mat=bpy.data.materials.new('HairFixturePalette');mesh.materials.append(mat)
        nodes=mat.node_tree.nodes;links=mat.node_tree.links
        image=nodes.new('ShaderNodeTexImage');image.image=bpy.data.images.new('HairFixturePalette',width=16,height=16)
        image.interpolation='Closest'
        source=nodes.new('ShaderNodeUVMap');source.uv_map='PaletteUV'
        links.new(source.outputs['UV'],image.inputs['Vector'])
        links.new(image.outputs['Color'],next(n for n in nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'])
        return obj,uv,image,source

    def test_palette_roundoff_within_same_nearest_texel_is_constant(self):
        obj,uv,image,source=self.palette_fixture()
        key=baker['constant_face_key'](obj,obj.data.polygons[0])
        self.assertIsNotNone(key)
        self.assertEqual(key,baker['constant_face_key'](obj,obj.data.polygons[1]))
        uv.data[0].uv.x=.51-1e-7
        self.assertEqual(key,baker['constant_face_key'](obj,obj.data.polygons[0]))

    def test_nearest_texel_boundary_and_linear_sampling_are_not_rounded_away(self):
        obj,uv,image,source=self.palette_fixture()
        uv.data[0].uv.x=.4999999
        self.assertIsNone(baker['constant_face_key'](obj,obj.data.polygons[0]))
        uv.data[0].uv.x=.51
        image.interpolation='Linear'
        self.assertIsNone(baker['constant_face_key'](obj,obj.data.polygons[0]))

    def test_shared_uv_with_a_linear_consumer_still_requires_exact_coordinates(self):
        obj,uv,image,source=self.palette_fixture();tree=obj.data.materials[0].node_tree
        second=tree.nodes.new('ShaderNodeTexImage');second.image=image.image;second.interpolation='Linear'
        mix=tree.nodes.new('ShaderNodeMixRGB')
        tree.links.new(source.outputs['UV'],second.inputs['Vector'])
        tree.links.new(image.outputs['Color'],mix.inputs[1]);tree.links.new(second.outputs['Color'],mix.inputs[2])
        tree.links.new(mix.outputs[0],next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'])
        self.assertIsNone(baker['constant_face_key'](obj,obj.data.polygons[0]))

    def test_control_roundoff_merges_only_identical_stored_bytes(self):
        obj,uv,image,source=self.palette_fixture()
        data=obj.data.color_attributes['BakedSurfaceSource']
        key=baker['constant_face_key'](obj,obj.data.polygons[0])
        data.data[0].color=(.99999988,1.00000012,0,8/255)
        self.assertEqual(key,baker['constant_face_key'](obj,obj.data.polygons[0]))
        data.data[0].color=(.99,1,0,8/255)
        self.assertIsNone(baker['constant_face_key'](obj,obj.data.polygons[0]))

    def test_spatial_emission_strength_prevents_source_deduplication(self):
        obj,uv,image,source=self.palette_fixture();tree=obj.data.materials[0].node_tree
        noise=tree.nodes.new('ShaderNodeTexNoise')
        tree.links.new(noise.outputs['Fac'],next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Emission Strength'])
        self.assertIsNone(baker['constant_face_key'](obj,obj.data.polygons[0]))

    def test_wrapped_detail_uv_is_preserved_and_protected(self):
        obj=plane();mesh=obj.data;uv=mesh.uv_layers.new(name='DetailUV')
        for loop in mesh.loops:uv.data[loop.index].uv=(mesh.vertices[loop.vertex_index].co.x-.1,mesh.vertices[loop.vertex_index].co.y*.5)
        source=[item.uv[:] for item in uv.data]
        protected=baker['prepare_uv'](obj)
        self.assertEqual(protected,{0,1})
        self.assertEqual([item.uv[:] for item in mesh.uv_layers['BakeUV'].data],source)

    def tearDown(self):
        for o in list(bpy.data.objects):
            if o.name.startswith('HairFixture'):bpy.data.objects.remove(o,do_unlink=True)

    def test_geodesic_flow_follows_root_to_tip_without_touching_weights(self):
        o=plane();m=o.data
        roots=hf.attribute(m,hf.ROOT,'BOOLEAN','POINT');roots.data[0].value=True;roots.data[3].value=True
        group=o.vertex_groups.new(name='head.x');group.add([0,1,2,3],1,'REPLACE')
        result=hf.generate(o)
        self.assertEqual(result['components'],1)
        for item in m.attributes[hf.FLOW].data:self.assertGreater(item.vector.dot(Vector((1,0,0))),.999)
        self.assertEqual([v.groups[0].weight for v in m.vertices],[1]*4)
        self.assertAlmostEqual(m.attributes[hf.PROGRESS].data[1].value,1)

    def test_missing_root_is_rejected_without_guessing_world_up(self):
        with self.assertRaisesRegex(ValueError,'no root'):hf.generate(plane())

    def test_profile_raster_never_invents_intermediate_material(self):
        o=plane();m=o.data
        uv=m.uv_layers.new(name='BakeUV')
        for loop in m.loops:uv.data[loop.index].uv=m.vertices[loop.vertex_index].co.xy
        data=m.color_attributes.new(name='BakedSurfaceSource',type='FLOAT_COLOR',domain='CORNER')
        for face,profile in zip(m.polygons,[0,15]):
            for i in face.loop_indices:data.data[i].color=(1,1,1,(8+16*profile)/255)
        result=baker['raster_profiles']([o],32,2)
        self.assertEqual(set(result.tolist()),{0,15})

    def test_snapshot_preserves_unsaved_image_pixels_and_material_links(self):
        o=plane();mat=bpy.data.materials.new('SnapshotSource');o.data.materials.append(mat)
        im=bpy.data.images.new('UnsavedPaint',width=2,height=2,alpha=True)
        im.pixels[:]=[.2,.3,.4,.5]*4
        node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=im
        original=list(im.pixels);source_path=bpy.data.filepath
        with tempfile.TemporaryDirectory() as temp:
            path=Path(temp)/'snapshot.blend';baker['snapshot'](path)
            self.assertTrue(path.is_file())
            self.assertEqual(bpy.data.filepath,source_path)
            self.assertIs(node.image,im)
            self.assertEqual(list(im.pixels),original)

    def test_small_plain_chart_has_real_512_pixel_coverage(self):
        o=plane();m=o.data;uv=m.uv_layers.new(name='BakeUV')
        for loop in m.loops:uv.data[loop.index].uv=m.vertices[loop.vertex_index].co.xy*1e-6
        baker['ensure_chart_footprints'](o,{},set())
        data=m.color_attributes.new(name='BakedSurfaceSource',type='FLOAT_COLOR',domain='CORNER')
        for item in data.data:item.color=(1,1,1,56/255)
        values=baker['raster_profiles']([o],512,0)
        self.assertGreater(sum(values==3),50)

    def test_explicit_guide_supplies_root_without_rig(self):
        o=plane()
        curve=bpy.data.curves.new('HairFixtureGuide','CURVE');curve.dimensions='3D'
        spline=curve.splines.new('POLY');spline.points.add(1)
        spline.points[0].co=(0,0,0,1);spline.points[1].co=(1,0,0,1)
        guide=bpy.data.objects.new('HairFixtureGuide',curve);guide[hf.GUIDE]=True
        bpy.context.collection.objects.link(guide)
        hf.generate(o,guides=[guide])
        self.assertAlmostEqual(o.data.attributes[hf.PROGRESS].data[0].value,0)
        self.assertGreater(o.data.attributes[hf.PROGRESS].data[1].value,.5)

    def test_disabled_material_emission_does_not_turn_white_vertex_data_into_glow(self):
        o=plane();m=o.data
        uv=m.uv_layers.new(name='BakeUV')
        for loop in m.loops:uv.data[loop.index].uv=m.vertices[loop.vertex_index].co.xy
        data=m.color_attributes.new(name='BakedSurfaceSource',type='FLOAT_COLOR',domain='CORNER')
        for item in data.data:item.color=(1,1,1,8/255)
        mat=bpy.data.materials.new('HairFixtureMaterial');m.materials.append(mat)
        bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        bsdf.inputs['Emission Strength'].default_value=0
        bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=1
        image=baker['make_image']('HairFixtureControl',32,True)
        baker['bake_material'](o,image,'control',1.6,{'aoStrength':0})
        values=baker['pixels'](image)
        self.assertLess(float(values[:,2].max()),1e-5)
        self.assertGreater(float(values[:,0].mean()),.99)

    def test_overlap_neutralization_preserves_unique_hair_ao_and_other_channels(self):
        o=plane();m=o.data
        uv=m.uv_layers.new(name='BakeUV')
        for loop in m.loops:uv.data[loop.index].uv=m.vertices[loop.vertex_index].co.xy
        data=m.color_attributes.new(name='BakedSurfaceSource',type='FLOAT_COLOR',domain='CORNER')
        for item in data.data:item.color=(.25,.6,.4,56/255)
        hair=m.attributes.new(name='WGameBakeHair',type='FLOAT',domain='FACE')
        hair.data[0].value=1;hair.data[1].value=1
        mat=bpy.data.materials.new('HairFixtureNoAo');m.materials.append(mat)
        bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        bsdf.inputs['Emission Strength'].default_value=.5
        bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=1
        image=baker['make_image']('HairFixtureNoAoControl',32,True)
        baker['bake_material'](o,image,'control',1.6,{'aoStrength':1})
        values=baker['pixels'](image).reshape(32,32,4)
        values[:,:,3]=56/255  # Production writes categorical profiles after the control bake.
        overlap=runpy.run_path(str(Path(__file__).with_name('baked_ao_overlap.py')))
        self.assertEqual(overlap['neutralize_hair_overlap']([o],values,32),0)
        self.assertAlmostEqual(float(values[8,24,0]),.25,places=4)
        self.assertAlmostEqual(float(values[24,8,0]),.25,places=4)
        # Give the second triangle the same UVs, leaving the rest of the image intact.
        for target,source in zip(m.polygons[1].loop_indices,m.polygons[0].loop_indices):
            uv.data[target].uv=uv.data[source].uv
        self.assertGreater(overlap['neutralize_hair_overlap']([o],values,32),0)
        self.assertAlmostEqual(float(values[8,24,0]),1.,places=4)
        self.assertAlmostEqual(float(values[24,8,0]),.25,places=4)
        for y,x in ((8,24),(24,8)):
            self.assertAlmostEqual(float(values[y,x,1]),.6,places=4)
            self.assertAlmostEqual(float(values[y,x,2]),.2,places=4)


if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(BakedSurfaceTests)
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():raise RuntimeError('Baked surface tests failed')
