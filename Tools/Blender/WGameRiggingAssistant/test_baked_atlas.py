"""Run with Blender --background --factory-startup --python-exit-code 17."""
from pathlib import Path
import runpy
import unittest
import math
import bpy
import numpy as np

atlas=runpy.run_path(str(Path(__file__).with_name('baked_atlas.py')))


def fixture():
    mesh=bpy.data.meshes.new('AtlasFixture')
    mesh.from_pydata([(0,0,0),(1,0,0),(0,1,0),(2,0,0),(3,0,0),(2,1,0)],[],[(0,1,2),(3,4,5)])
    mesh.update();obj=bpy.data.objects.new('AtlasFixture',mesh);bpy.context.collection.objects.link(obj)
    uv=mesh.uv_layers.new(name='BakeUV')
    for loop,co in zip(uv.data,[(.1,.1),(.3,.1),(.1,.3),(.65,.65),(.85,.65),(.65,.85)]):loop.uv=co
    colors=np.tile(np.array((.5,.4,.3,1),dtype=np.float32),(512*512,1))
    controls=np.tile(np.array((1,.5,0,8/255),dtype=np.float32),(512*512,1))
    return obj,colors,controls


class AtlasTests(unittest.TestCase):
    def detail_fixture(self, second=None):
        obj,color,control=fixture();layer=obj.data.uv_layers.new(name='DetailUV')
        points=[(.1,.1),(.5,.1),(.1,.5)]
        for item,point in zip(layer.data,points+(second or points)):item.uv=point
        return obj,color,control

    def test_repeated_detail_shares_space_and_increases_texel_density(self):
        obj,color,control=self.detail_fixture()
        other,c2,d2=self.detail_fixture()
        plain=atlas['compact']([other],{other.name:{}},c2,d2,512,{other.name:{0,1}})
        result=atlas['compact']([obj],{obj.name:{}},color,control,512,{obj.name:{0,1}},{obj.name:{0:('same',),1:('same',)}})
        uv=obj.data.uv_layers['BakeUV']
        self.assertEqual([v.uv[:] for v in uv.data[:3]],[v.uv[:] for v in uv.data[3:]])
        self.assertEqual(result['stats']['sharedDetailGroups'],1)
        self.assertEqual(result['stats']['detailCharts'],1)
        self.assertGreater(result['stats']['detailScaleFromSource'],plain['stats']['detailScaleFromSource'])

    def test_partial_overlap_uses_one_transform_without_stacking_nonmatching_coordinates(self):
        obj,color,control=self.detail_fixture([(.3,.1),(.7,.1),(.3,.5)])
        result=atlas['compact']([obj],{obj.name:{}},color,control,512,{obj.name:{0,1}},{obj.name:{0:('same',),1:('same',)}})
        target=np.array([v.uv[:] for v in obj.data.uv_layers['BakeUV'].data])
        source=np.array([v.uv[:] for v in obj.data.uv_layers['DetailUV'].data])
        transform=np.linalg.lstsq(np.column_stack((source,np.ones(6))),target,rcond=None)[0]
        self.assertTrue(np.allclose(np.column_stack((source,np.ones(6)))@transform,target,atol=1e-7))
        self.assertEqual(result['stats']['reusedDetailCharts'],1)
        self.assertFalse(np.allclose(target[:3],target[3:]))

    def test_conflicting_or_unknown_inputs_do_not_share_even_if_probe_pixels_match(self):
        for key in (('differentPalette',),('differentProfile',),None):
            obj,color,control=self.detail_fixture()
            result=atlas['compact']([obj],{obj.name:{}},color,control,512,{obj.name:{0,1}},{obj.name:{0:('same',),1:key}})
            self.assertEqual(result['stats']['reusedDetailCharts'],0)
            self.assertEqual(result['stats']['detailCharts'],2)

    def test_touching_or_intersecting_bounds_without_triangle_overlap_are_not_shared(self):
        a=np.array([[(0,0),(1,0),(0,1)]],dtype=float)
        self.assertFalse(atlas['_overlaps'](a,np.array([[(1,1),(1,0),(0,1)]],dtype=float)))
        self.assertFalse(atlas['_overlaps'](a,np.array([[ (.8,.8),(1,.8),(.8,1)]],dtype=float)))
        self.assertTrue(atlas['_overlaps'](a,a))

    def test_mirrored_detail_keeps_source_coordinate_correspondence(self):
        obj,color,control=self.detail_fixture([(.1,.1),(.1,.5),(.5,.1)])
        result=atlas['compact']([obj],{obj.name:{}},color,control,512,{obj.name:{0,1}},{obj.name:{0:('same',),1:('same',)}})
        uv=obj.data.uv_layers['BakeUV']
        self.assertEqual(uv.data[0].uv[:],uv.data[3].uv[:]);self.assertEqual(uv.data[1].uv[:],uv.data[5].uv[:])
        self.assertEqual(uv.data[2].uv[:],uv.data[4].uv[:]);self.assertEqual(result['stats']['reusedDetailCharts'],1)

    def test_detail_can_share_between_meshes_with_the_same_certified_inputs(self):
        a,color,control=self.detail_fixture();b,_,_=self.detail_fixture()
        result=atlas['compact']([a,b],{a.name:{},b.name:{}},color,control,512,
            {a.name:{0,1},b.name:{0,1}},{a.name:{0:('same',),1:('same',)},b.name:{0:('same',),1:('same',)}})
        self.assertEqual(result['stats']['detailCharts'],1)
        self.assertEqual(result['stats']['reusedDetailCharts'],3)
        self.assertEqual([v.uv[:] for v in a.data.uv_layers['BakeUV'].data],[v.uv[:] for v in b.data.uv_layers['BakeUV'].data])

    def test_only_conflicting_faces_split_from_an_otherwise_reusable_island(self):
        obj,color,control=fixture();mesh=obj.data;mesh.clear_geometry()
        mesh.from_pydata([(x+offset,y,0) for offset in (0,2) for x,y in ((0,0),(1,0),(1,1),(0,1))],[],[(0,1,2),(0,2,3),(4,5,6),(4,6,7)]);mesh.update()
        bake=mesh.uv_layers.get('BakeUV') or mesh.uv_layers.new(name='BakeUV');detail=mesh.uv_layers.new(name='DetailUV')
        for loop in mesh.loops:
            vertex=mesh.vertices[loop.vertex_index].co;second=loop.vertex_index>=4
            source=np.array((vertex.x-(2 if second else 0),vertex.y))*.3+.1
            detail.data[loop.index].uv=source;bake.data[loop.index].uv=source+(.5 if second else 0)
        self.assertEqual(len(atlas['charts'](mesh)),2)
        result=atlas['compact']([obj],{obj.name:{}},color,control,512,{obj.name:set(range(4))},
            {obj.name:{0:('same',),1:('same',),2:('same',),3:('conflict',)}})
        self.assertEqual(result['stats']['detailCharts'],2)
        self.assertEqual(result['stats']['sharedDetailFaces'],3)
        self.assertEqual([v.uv[:] for v in bake.data[:3]],[v.uv[:] for v in bake.data[6:9]])
        self.assertNotEqual([v.uv[:] for v in bake.data[3:6]],[v.uv[:] for v in bake.data[9:12]])

    def test_constant_face_can_leave_a_chart_containing_detail_or_control_variation(self):
        for channel in ('color','control'):
            obj,color,control=fixture();mesh=obj.data
            # Two connected triangles in one UV island. Only the second has
            # a varying interior texel; the first should share a constant tile.
            mesh.clear_geometry();mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2),(0,2,3)]);mesh.update()
            uv=mesh.uv_layers.get('BakeUV') or mesh.uv_layers.new(name='BakeUV')
            for loop in mesh.loops:uv.data[loop.index].uv=np.array(mesh.vertices[loop.vertex_index].co.xy)*.4+.1
            pixels=color if channel=='color' else control
            pixels.reshape(512,512,4)[220,80,0]=.1
            self.assertEqual(len(atlas['charts'](mesh)),1)
            result=atlas['compact']([obj],{obj.name:{}},color,control,512)
            self.assertEqual(result['stats']['splitConstantFaces'],1)
            self.assertEqual(result['stats']['sharedConstantTiles'],1)
            self.assertEqual(result['stats']['detailCharts']+result['stats']['surfaceCharts'],1)

    def test_hair_ao_chart_is_not_fragmented_by_locally_flat_probe_faces(self):
        obj,color,control=fixture();mesh=obj.data
        mesh.clear_geometry();mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2),(0,2,3)]);mesh.update()
        uv=mesh.uv_layers.get('BakeUV') or mesh.uv_layers.new(name='BakeUV')
        for loop in mesh.loops:uv.data[loop.index].uv=np.array(mesh.vertices[loop.vertex_index].co.xy)*.4+.1
        hair=mesh.attributes.new(name='WGameBakeHair',type='FLOAT',domain='FACE')
        for item in hair.data:item.value=1
        control.reshape(512,512,4)[220,80,0]=.1
        result=atlas['compact']([obj],{obj.name:{}},color,control,512)
        self.assertEqual(result['stats']['splitConstantFaces'],0)
        self.assertEqual(result['stats']['surfaceCharts'],1)
        self.assertEqual(result['stats']['sharedConstantTiles'],0)

    def tearDown(self):
        for obj in list(bpy.data.objects):
            if obj.name.startswith('AtlasFixture'):bpy.data.objects.remove(obj,do_unlink=True)

    def test_identical_final_outputs_share_one_small_tile(self):
        obj,color,control=fixture();positions=[v.co[:] for v in obj.data.vertices]
        result=atlas['compact']([obj],{obj.name:{}},color,control,512)
        self.assertEqual(result['stats']['sharedConstantTiles'],1)
        self.assertEqual(result['stats']['constantCharts'],2)
        self.assertLess(result['stats']['paletteHeight'],20)
        uv=obj.data.uv_layers['BakeUV']
        self.assertEqual([x.uv[:] for x in uv.data[:3]],[x.uv[:] for x in uv.data[3:]])
        self.assertEqual([v.co[:] for v in obj.data.vertices],positions)
        output=np.zeros_like(color);data=np.zeros_like(control)
        atlas['fill_constants'](result,output,data,512)
        x,y,pitch,key=result['cells'][0]
        self.assertTrue(np.all(atlas['encode_color'](output.reshape(512,512,4)[y:y+pitch,x:x+pitch].reshape(-1,4))==key[:3]))

    def test_same_color_different_material_cannot_merge(self):
        obj,color,control=fixture()
        control.reshape(512,512,4)[256:,:,3]=216/255
        result=atlas['compact']([obj],{obj.name:{}},color,control,512)
        self.assertEqual(result['stats']['sharedConstantTiles'],2)
        cells=result['cells'];self.assertEqual(cells[0][1],cells[1][1])
        self.assertEqual(cells[1][0]-cells[0][0],cells[0][2])

    def test_ao_variation_keeps_unique_surface_charts(self):
        obj,color,control=fixture()
        control.reshape(512,512,4)[:,:,0]=np.arange(512)[None,:]/511
        result=atlas['compact']([obj],{obj.name:{}},color,control,512)
        self.assertEqual(result['stats']['sharedConstantTiles'],0)
        self.assertEqual(result['stats']['surfaceCharts'],2)

    def test_single_detail_pixel_is_not_mistaken_for_flat_color(self):
        obj,color,control=fixture()
        color.reshape(512,512,4)[77,77,:3]=(1,0,0)
        result=atlas['compact']([obj],{obj.name:{}},color,control,512)
        self.assertEqual(result['stats']['detailCharts'],1)
        self.assertEqual(result['stats']['sharedConstantTiles'],1)
        self.assertGreater(result['stats']['detailScaleFromProbe'],1)

    def test_authored_detail_is_not_discarded_when_probe_misses_subpixel_paint(self):
        obj,color,control=fixture()
        result=atlas['compact']([obj],{obj.name:{}},color,control,512,{obj.name:{0}})
        self.assertEqual(result['stats']['detailCharts'],1)
        self.assertEqual(result['stats']['sharedConstantTiles'],1)

    def test_detail_can_use_space_beside_ao_without_overlapping_it(self):
        obj,color,control=fixture()
        control.reshape(512,512,4)[:,:,0]=np.arange(512)[None,:]/511
        result=atlas['compact']([obj],{obj.name:{}},color,control,512,{obj.name:{0}})
        uv=obj.data.uv_layers['BakeUV']
        a=np.array([v.uv[:] for v in uv.data[:3]]);b=np.array([v.uv[:] for v in uv.data[3:]])
        self.assertFalse(atlas['_overlaps'](a[None,:,:],b[None,:,:]))
        self.assertEqual(result['stats']['surfaceBandHeight'],0)
        self.assertGreater(result['stats']['detailScaleFromProbe'],3)

    def test_contours_can_interlock_with_overlapping_bounds_and_safe_gutters(self):
        packer=runpy.run_path(str(Path(__file__).with_name('baked_atlas_packing.py')))
        triangle=np.array(((0.,0.),(.3,0.),(0.,.3)))
        targets=packer['pack_once']([{'id':0,'points':triangle},{'id':1,'points':triangle}],[],128,2,0.,0)
        a,b=targets[0],targets[1]
        self.assertTrue(np.all(a.min(axis=0)<b.max(axis=0)) and np.all(b.min(axis=0)<a.max(axis=0)))
        self.assertFalse(atlas['_overlaps'](a[None,:,:],b[None,:,:]))
        self.assertTrue(packer['validate_gutters'](targets,[],128,2))
        self.assertFalse(packer['validate_gutters']({0:a,1:a},[],128,2))
        self.assertFalse(any(o.name.startswith('WGameAtlasPackingProxy') for o in bpy.data.objects))

    def test_dense_proxy_simplification_preserves_mapping_pinned_cells_and_selection(self):
        packer=runpy.run_path(str(Path(__file__).with_name('baked_atlas_packing.py')))
        triangles=[]
        for y in range(9):
            for x in range(9):
                a,b,c,d=(x,y),(x+1,y),(x+1,y+1),(x,y+1)
                triangles.extend((a,b,c,a,c,d))
        coords=np.array(triangles,dtype=float)/20;before=coords.copy()
        active=bpy.context.view_layer.objects.active;selection=list(bpy.context.selected_objects)
        cells=[(0,0,12,(0,0,0,255,255,0,8))]
        targets=packer['pack_once']([{'id':0,'points':coords}],cells,128,2,0.,0)
        self.assertIsNotNone(targets)
        self.assertTrue(packer['validate_gutters'](targets,cells,128,2))
        matrix=np.linalg.lstsq(np.column_stack((coords,np.ones(len(coords)))),targets[0],rcond=None)[0]
        self.assertTrue(np.allclose(np.column_stack((coords,np.ones(len(coords))))@matrix,targets[0],atol=1e-7))
        self.assertTrue(np.array_equal(coords,before))
        self.assertEqual(active,bpy.context.view_layer.objects.active)
        self.assertEqual(selection,list(bpy.context.selected_objects))

    def test_hair_tube_becomes_a_continuous_chart_even_with_inherited_pins(self):
        import bmesh
        vertices=[(math.cos(i*math.tau/8),math.sin(i*math.tau/8),float(j)) for j in range(4) for i in range(8)]
        vertices.extend([(0,0,0),(0,0,3)])
        faces=[]
        for j in range(3):
            for i in range(8):
                a=j*8+i;b=j*8+(i+1)%8;c=b+8;d=a+8
                faces.extend([(a,b,c),(a,c,d)])
        for i in range(8):faces.extend([(32,(i+1)%8,i),(33,24+i,24+(i+1)%8)])
        mesh=bpy.data.meshes.new('AtlasFixtureTube');mesh.from_pydata(vertices,[],faces);mesh.update()
        obj=bpy.data.objects.new('AtlasFixtureTube',mesh);bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        uv=mesh.uv_layers.new(name='BakeUV');uv.active_render=True
        for face in mesh.polygons:
            for i,p in zip(face.loop_indices,((0,0),(.1,0),(0,.1))):uv.data[i].uv=p
        progress=mesh.attributes.new(name='WGameHairProgress',type='FLOAT',domain='POINT')
        for v in mesh.vertices:progress.data[v.index].value=v.co.z/3
        bpy.ops.object.mode_set(mode='EDIT');bm=bmesh.from_edit_mesh(mesh);layer=bm.loops.layers.uv.get('BakeUV')
        for face in bm.faces:
            for loop in face.loops:loop[layer].pin_uv=True
        bmesh.update_edit_mesh(mesh);bpy.ops.object.mode_set(mode='OBJECT')
        report=atlas['unwrap_hair'](obj,[True]*len(faces),set())
        self.assertEqual(report['fallbackFaces'],0)
        self.assertLessEqual(len(atlas['charts'](mesh)),2)

    def test_hair_handle_gets_disk_cuts_instead_of_fragment_fallback(self):
        vertices=[];faces=[]
        for i in range(16):
            a=i*math.tau/16
            for j in range(8):
                b=j*math.tau/8;r=2+.6*math.cos(b)
                vertices.append((r*math.cos(a),r*math.sin(a),.6*math.sin(b)))
        for i in range(16):
            for j in range(8):
                a=i*8+j;b=((i+1)%16)*8+j;c=((i+1)%16)*8+(j+1)%8;d=i*8+(j+1)%8
                faces.extend(((a,b,c),(a,c,d)))
        mesh=bpy.data.meshes.new('HandleHair');mesh.from_pydata(vertices,[],faces);mesh.update()
        obj=bpy.data.objects.new('HandleHair',mesh);bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        uv=mesh.uv_layers.new(name='BakeUV')
        for face in mesh.polygons:
            for i,p in zip(face.loop_indices,((0,0),(.1,0),(0,.1))):uv.data[i].uv=p
        before_vertices=[v.co[:] for v in mesh.vertices];before_faces=[p.vertices[:] for p in mesh.polygons]
        before_seams=[e.use_seam for e in mesh.edges]
        report=atlas['unwrap_hair'](obj,[True]*len(faces),set())
        self.assertGreater(report['topologyCuts'],0)
        self.assertEqual(report['fallbackFaces'],0)
        self.assertLessEqual(len(atlas['charts'](mesh)),3)
        self.assertEqual(before_vertices,[v.co[:] for v in mesh.vertices])
        self.assertEqual(before_faces,[p.vertices[:] for p in mesh.polygons])
        self.assertEqual(before_seams,[e.use_seam for e in mesh.edges])

    def test_matte_and_sheen_hair_get_separate_categorical_charts(self):
        mesh=bpy.data.meshes.new('ProfileBoundary');mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2),(0,2,3)]);mesh.update()
        obj=bpy.data.objects.new('ProfileBoundary',mesh);bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        mesh.uv_layers.new(name='BakeUV')
        profile=mesh.attributes.new(name='SurfaceProfile',type='INT',domain='FACE');profile.data[0].value=2;profile.data[1].value=3
        report=atlas['unwrap_hair'](obj,[True,True],set())
        self.assertEqual(report['fallbackFaces'],0)
        self.assertEqual(len(atlas['charts'](mesh)),2)
        self.assertEqual([v.value for v in mesh.attributes['SurfaceProfile'].data],[2,3])


result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(AtlasTests))
if not result.wasSuccessful():raise RuntimeError('Atlas tests failed')
