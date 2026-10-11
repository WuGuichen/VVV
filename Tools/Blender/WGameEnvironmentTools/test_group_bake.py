"""Real Blender bake tests, isolated from user artwork and character sources."""
import importlib
import json
import sys
import tempfile
from pathlib import Path

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import bpy
import numpy as np
from WGameEnvironmentTools import authoring,shared_detail,group_bake

test_root=authoring.repository()/'Temp/EnvironmentSurface'
test_root.mkdir(parents=True,exist_ok=True)
ROOT=Path(tempfile.mkdtemp(prefix='group-tests-',dir=test_root))


def fixture(different_color=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene['wgame_environment_work']=True
    palette=authoring.palette_module();objects=[]
    image=palette._build_preview_image()
    for index in range(2):
        mesh=bpy.data.meshes.new('TestMesh'+str(index))
        mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2,3)]);mesh.update()
        obj=bpy.data.objects.new('TestObject'+str(index),mesh);bpy.context.collection.objects.link(obj)
        obj['wgame_environment_asset']='Prop_'+obj.name;obj.location.x=index*2
        rgb=(30,60,210) if different_color and index else (210,40,30)
        palette._apply_color_profile_to_polygons(obj,[0],palette.color_index_from_rgb8(*rgb),11)
        material=bpy.data.materials.new('Initial');material.use_nodes=True
        tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;mesh.materials.append(material)
        objects.append(obj)
    tree=bpy.data.node_groups.new('UV Export Passthrough','GeometryNodeTree')
    tree.interface.new_socket(name='Geometry',in_out='INPUT',socket_type='NodeSocketGeometry')
    tree.interface.new_socket(name='Geometry',in_out='OUTPUT',socket_type='NodeSocketGeometry')
    source=tree.nodes.new('NodeGroupInput');target=tree.nodes.new('NodeGroupOutput')
    tree.links.new(source.outputs['Geometry'],target.inputs['Geometry'])
    objects[1].modifiers.new('PassThrough','NODES').node_group=tree
    shared_detail.convert(bpy.context.scene)
    canvas=shared_detail.canvas(obj=objects[0])
    canvas.scale(128,128)
    pixels=np.tile([.35,.5,.3,.5],128*128).astype(np.float32);canvas.pixels.foreach_set(pixels);canvas.pack()
    for obj in objects:
        for entry,value in zip(obj.data.uv_layers['DetailUV'].data,((.1,.1),(.9,.1),(.9,.9),(.1,.9))):entry.uv=value
    bpy.context.scene['wgame_bake_output_size']='512'
    return objects,canvas


def bake_case(name,different=False):
    objects,image=fixture(different)
    source=ROOT/(name+'.blend');bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    before=authoring.bake_module().sha(source)
    result=group_bake.run(ROOT/name)
    assert authoring.bake_module().sha(source)==before,'Source file changed during baking'
    assert len(result['groups'])==1 and len(result['assets'])==2
    assert len([f for f in result['files'] if f['path'].endswith('.png')])==2
    assert all(b'HairFlow' not in (ROOT/name/a['model']).read_bytes() for a in result['assets'])
    assert all([u.name for u in obj.data.uv_layers]==['UV0'] for obj in objects)
    stats=result['groups'][0]['atlasLayout']
    if not different:
        assert stats['reusedDetailCharts']>=1,stats
        uv0=np.array([v.uv[:] for v in objects[0].data.uv_layers[0].data])
        uv1=np.array([v.uv[:] for v in objects[1].data.uv_layers[0].data])
        assert np.allclose(uv0,uv1,atol=1e-6),'Equivalent source regions did not reuse the atlas'
    else:
        texture=bpy.data.images.load(str(ROOT/name/result['groups'][0]['baseColor']))
        pixels=np.empty(len(texture.pixels),dtype=np.float32);texture.pixels.foreach_get(pixels)
        pixels=pixels.reshape(texture.size[1],texture.size[0],4)
        colors=[]
        for obj in objects:
            face=obj.data.polygons[0]
            uv=np.mean([obj.data.uv_layers[0].data[i].uv[:] for i in face.loop_indices],axis=0)
            x,y=np.clip((uv*np.array(texture.size)).astype(int),0,texture.size[0]-1)
            colors.append(pixels[y,x,:3])
        assert colors[0][0]>colors[0][2] and colors[1][2]>colors[1][0],colors
    for obj,asset in zip(objects,result['assets']):
        expected=corner_signature(obj)
        bpy.ops.import_scene.fbx(filepath=str(ROOT/name/asset['model']))
        imported=[item for item in bpy.context.selected_objects if item.type=='MESH']
        assert len(imported)==1
        verify_single_material(imported[0],expected)
    print('PASS',name,json.dumps(stats),flush=True)


def single_material_case():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene;scene['wgame_environment_work']=True
    scene['wgame_bake_output_size']='512'
    mesh=bpy.data.meshes.new('MultiMaterialMesh')
    mesh.from_pydata([(x,y,0) for offset in (0,2,4) for x,y in
                     ((offset,0),(offset+1,0),(offset,1))],[],[(0,1,2),(3,4,5),(6,7,8)])
    mesh.update()
    obj=bpy.data.objects.new('Prop_MultiMaterial',mesh);scene.collection.objects.link(obj)
    obj['wgame_environment_asset']=obj.name
    palette=authoring.palette_module()
    profiles=(5,7,11)
    for index,profile in enumerate(profiles):
        palette._apply_color_profile_to_polygons(obj,[index],palette.color_index_from_rgb8(120,120,120),profile)
    for color in ((.6,.02,.02,1),(.02,.6,.02,1),(.02,.02,.6,1),(.3,.3,.3,1)):
        material=bpy.data.materials.new('AuthoringSurface');material.use_nodes=True
        material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color
        mesh.materials.append(material)
    for face in mesh.polygons:face.material_index=face.index
    detail=mesh.uv_layers.new(name='DetailUV')
    for item in detail.data:item.uv=(-1,-1)
    source_uvs=[uv.name for uv in mesh.uv_layers]
    source=ROOT/'multiple_materials.blend';bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    before=authoring.bake_module().sha(source)
    output=ROOT/'multiple_materials';result=group_bake.run(output)
    assert authoring.bake_module().sha(source)==before,'Source file changed during baking'
    assert len(mesh.materials)==1,'Baked mesh retained authoring material slots'
    assert {face.material_index for face in mesh.polygons}=={0}
    texture=bpy.data.images.load(str(output/result['groups'][0]['baseColor']))
    pixels=np.array(texture.pixels[:]).reshape(texture.size[1],texture.size[0],4)
    surface=bpy.data.images.load(str(output/result['groups'][0]['surfaceData']))
    surface.colorspace_settings.name='Non-Color'
    surface_pixels=np.array(surface.pixels[:]).reshape(surface.size[1],surface.size[0],4)
    for face in mesh.polygons:
        uv=np.mean([mesh.uv_layers['UV0'].data[i].uv[:] for i in face.loop_indices],axis=0)
        x,y=np.clip((uv*np.array(texture.size)).astype(int),0,texture.size[0]-1)
        color=pixels[y,x,:3]
        assert np.argmax(color)==face.index and color[face.index]>.2,color
        assert round(surface_pixels[y,x,3]*255)==8+16*profiles[face.index]
    expected=corner_signature(obj)
    bpy.ops.import_scene.fbx(filepath=str(output/result['assets'][0]['model']))
    imported=[item for item in bpy.context.selected_objects if item.type=='MESH']
    assert len(imported)==1
    verify_single_material(imported[0],expected)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    original=bpy.data.objects['Prop_MultiMaterial'].data
    assert len(original.materials)==4
    assert [face.material_index for face in original.polygons]==[0,1,2]
    assert [uv.name for uv in original.uv_layers]==source_uvs
    print('PASS multiple authored materials preserve baked colors and export one slot',flush=True)


def corner_signature(obj):
    mesh=obj.data;mesh.calc_loop_triangles()
    uv=mesh.uv_layers['UV0']
    return sorted(tuple(round(float(value),5) for value in
                        (*mesh.vertices[mesh.loops[i].vertex_index].co,*uv.data[i].uv,
                         *mesh.corner_normals[i].vector))
                  for triangle in mesh.loop_triangles for i in triangle.loops)


def verify_single_material(obj,expected):
    mesh=obj.data
    assert len(mesh.materials)==1,(obj.name,len(mesh.materials))
    assert {face.material_index for face in mesh.polygons}=={0}
    assert [uv.name for uv in mesh.uv_layers]==['UV0']
    assert np.allclose(corner_signature(obj),expected,atol=2e-5),'FBX geometry, UV or normals changed'


def sample_baked_xy(obj,pixels,point):
    mesh=obj.data;mesh.calc_loop_triangles()
    for triangle in mesh.loop_triangles:
        xy=np.array([mesh.vertices[i].co[:2] for i in triangle.vertices])
        weights=np.linalg.solve(np.vstack((xy.T,np.ones(3))),[*point,1.])
        if weights.min() < -1e-6:continue
        uv=weights @ np.array([mesh.uv_layers['UV0'].data[i].uv[:] for i in triangle.loops])
        x,y=(uv*np.array([pixels.shape[1],pixels.shape[0]])).astype(int)
        return pixels[y,x,:3]
    raise AssertionError('Sample is outside the mesh')


def clipped_detail_case():
    objects,image=fixture()
    image.pixels.foreach_set(np.tile([.02,.8,.02,1.],128*128).astype(np.float32));image.pack()
    layouts=(((-.5,-.5),(1.5,-.5),(1.5,1.5),(-.5,1.5)),
             ((-3,-3),(-2,-3),(-2,-2),(-3,-2)))
    for obj,coords in zip(objects,layouts):
        for item,co in zip(obj.data.uv_layers['DetailUV'].data,coords):item.uv=co
    assert all(len(group_bake.mapped_faces(obj))==1 for obj in objects)
    assert all(item['mappedFaces']==1 for item in shared_detail.validate()['objects'])
    source=ROOT/'clipped_detail.blend';bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    before=authoring.bake_module().sha(source)
    output=ROOT/'clipped_detail';result=group_bake.run(output)
    assert authoring.bake_module().sha(source)==before
    texture=bpy.data.images.load(str(output/result['groups'][0]['baseColor']))
    pixels=np.array(texture.pixels[:]).reshape(texture.size[1],texture.size[0],4)
    center=sample_baked_xy(objects[0],pixels,(.45,.55))
    assert center[1]>center[0]*3,center
    for obj,point in ((objects[0],(.1,.55)),(objects[0],(.9,.55)),
                      (objects[0],(.45,.1)),(objects[0],(.45,.9)),(objects[1],(.45,.55))):
        color=sample_baked_xy(obj,pixels,point)
        assert color[0]>color[1]*3 and color[0]>color[2]*3,color
    for obj,asset in zip(objects,result['assets']):
        assert all(0<=value<=1 for item in obj.data.uv_layers['UV0'].data for value in item.uv)
        expected=corner_signature(obj)
        bpy.ops.import_scene.fbx(filepath=str(output/asset['model']))
        verify_single_material(next(o for o in bpy.context.selected_objects if o.type=='MESH'),expected)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    for index,coords in enumerate(layouts):
        assert [tuple(item.uv) for item in bpy.data.objects['TestObject'+str(index)].data.uv_layers['DetailUV'].data]==list(coords)
    print('PASS crossing and fully outside DetailUV: image inside, base outside, unchanged source, single-slot FBX',flush=True)


clipped_detail_case()
single_material_case()
bake_case('same_image_same_surface')
bake_case('same_image_different_base',True)
objects,image=fixture()
other=image.copy();other.name='DifferentDetailAsset';other['wgame_detail_source_id']='independent-image'
palette=shared_detail.palette_image(objects)
objects[1].data.materials[0]=shared_detail.make_material(palette,other)
assert len(group_bake.groups_for(objects))==2
print('PASS distinct image assets form separate groups',flush=True)
print('GROUP_BAKE_TESTS_COMPLETE',ROOT,flush=True)
