"""Real Blender regression for emission authoring, storage and SurfaceData export."""
import hashlib
import json
import sys
import tempfile
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import WGameEnvironmentTools as addon
from WGameEnvironmentTools import authoring,emission,group_bake,shared_detail,workflow
from WGameUVTools import decals

bpy.ops.wm.read_factory_settings(use_empty=True);addon.register()
scene=bpy.context.scene
scene['wgame_environment_work']=True;scene['wgame_environment_pipeline']=shared_detail.PIPELINE
scene['wgame_bake_output_size']='512'
temp=authoring.repository()/'Temp/EnvironmentSurface';temp.mkdir(parents=True,exist_ok=True)
root=Path(tempfile.mkdtemp(prefix='emission-test-',dir=temp));work=root/'Blender_Work';work.mkdir()
detail=bpy.data.images.new('EmissionTestDetail',width=32,height=32,alpha=True)
detail.generated_color=(0,0,0,0);detail.colorspace_settings.name='sRGB'
detail['wgame_environment_shared_detail']=True;detail.pack()
scene.wgame_environment_detail_image=detail
objects=[];palette=authoring.palette_module()
for index,color in enumerate(((.6,.04,.02,1),(.02,.04,.6,1))):
    mesh=bpy.data.meshes.new('EmissionMesh'+str(index))
    vertices=[(offset+x,y,0) for offset in (0,2,4) for x,y in ((0,0),(1,0),(1,1),(0,1))]
    mesh.from_pydata(vertices,[],[(0,1,2,3),(4,5,6,7),(8,9,10,11)]);mesh.update()
    obj=bpy.data.objects.new('Prop_Emission'+str(index),mesh);scene.collection.objects.link(obj)
    obj['wgame_environment_asset']=obj.name;obj.location.x=index*7
    mat=bpy.data.materials.new('Original'+str(index));mat.use_nodes=True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color;mesh.materials.append(mat)
    palette._apply_color_profile_to_polygons(obj,[0,1,2],palette.color_index_from_rgb8(150,50,30),11)
    layer=mesh.uv_layers.new(name='DetailUV',do_init=False)
    for face,coords in zip(mesh.polygons,(((-.5,-.5),(1.5,-.5),(1.5,1.5),(-.5,1.5)),
                                          ((0,0),(1,0),(1,1),(0,1)),((-1,-1),)*4)):
        for loop,co in zip(face.loop_indices,coords):layer.data[loop].uv=co
    authoring.activate(obj)
    assert bpy.ops.wgame.environment_prepare_emission()=={'FINISHED'}
    # Exercise an empty face that already shares the emissive authoring material.
    bound_color,bound_profile=addon.color_profile.face_pair(obj,0)
    palette._apply_color_profile_to_polygons(obj,[2],bound_color,bound_profile)
    mesh.polygons[2].material_index=mesh.polygons[0].material_index
    objects.append(obj)
copy=objects[0].copy();copy.data=objects[0].data.copy();copy.name='Prop_EmissionSame'
copy['wgame_environment_asset']=copy.name;copy.location.x=14;scene.collection.objects.link(copy);objects.append(copy)
mask=emission.find_mask(detail)
assert mask and mask.colorspace_settings.name=='Non-Color'
assert emission.intensity(mask)==8
del mask[emission.INTENSITY]
assert emission.intensity(mask)==1,'Legacy masks must retain 1x intensity'
emission.set_intensity(mask,8)
signature=workflow.fingerprint(scene)
old_group=next(iter(group_bake.groups_for(objects)))
scene.wgame_environment_emission_intensity=12
assert emission.group_intensity(objects)==12 and workflow.fingerprint(scene)!=signature
assert next(iter(group_bake.groups_for(objects)))!=old_group
scene.wgame_environment_emission_intensity=8
assert emission.group_intensity(objects)==8
assert np.max(np.array(mask.pixels[:]).reshape(-1,4)[:,:3])==0
assert {emission.mask_image(obj.data.materials[obj.data.polygons[0].material_index]) for obj in objects}=={mask}
assert group_bake.detail_image(objects[0])==detail and len(group_bake.groups_for(objects))==1
uv_before={obj.name:[tuple(v.uv) for v in obj.data.uv_layers['DetailUV'].data] for obj in objects}
detail_before=np.array(detail.pixels[:],dtype=np.float32).tobytes()

authoring.activate(objects[0]);scene.wgame_environment_emission_value=.35
assert bpy.ops.wgame.environment_paint(channel='EMISSION')=={'FINISHED'}
paint=scene.tool_settings.image_paint
assert paint.canvas==mask and paint.brush and paint.brush.blend=='MIX'
assert np.allclose(paint.unified_paint_settings.color[:],(.35,)*3)
scene.wgame_environment_color=(.8,.1,.2)
assert np.allclose(paint.unified_paint_settings.color[:],(.35,)*3)
assert decals.target_for_faces(objects[0],objects[0].data.polygons)==mask
assert bpy.ops.wgame.environment_paint(channel='EMISSION',erase='ERASE')=={'FINISHED'}
assert np.allclose(paint.unified_paint_settings.color[:],(0,0,0))
assert bpy.ops.wgame.environment_paint(channel='DETAIL')=={'FINISHED'}
assert paint.canvas==detail and decals.target_for_faces(objects[0],objects[0].data.polygons)==detail
bpy.ops.object.mode_set(mode='OBJECT')
assert all(uv_before[obj.name]==[tuple(v.uv) for v in obj.data.uv_layers['DetailUV'].data] for obj in objects)
print('PASS emission create/reuse, brush/erase/channel switching, UV and base canvas preserved',flush=True)

fingerprint=workflow.fingerprint(scene);group_key=next(iter(group_bake.groups_for(objects)))
values=np.ones((32,32,4),np.float32);values[:,:,:3]=0
values[:,:10,:3]=.25;values[:,10:22,:3]=1
mask.pixels.foreach_set(values.ravel());mask.update()
assert workflow.fingerprint(scene)!=fingerprint
assert next(iter(group_bake.groups_for(objects)))!=group_key
assert np.array(detail.pixels[:],dtype=np.float32).tobytes()==detail_before

def render_emission(obj,name):
    for item in scene.objects:
        if item.type in {'MESH','LIGHT'}:item.hide_render=item!=obj
    if scene.world is None:scene.world=bpy.data.worlds.new('EmissionTestWorld')
    scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=0
    camera_data=bpy.data.cameras.new('EmissionTestCamera');camera_data.type='ORTHO';camera_data.ortho_scale=1
    camera=bpy.data.objects.new('EmissionTestCamera',camera_data);scene.collection.objects.link(camera)
    camera.location=(.5,.5,3);scene.camera=camera
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8
    scene.view_settings.view_transform='Standard';scene.view_settings.exposure=0;scene.view_settings.gamma=1
    scene.render.resolution_x=64;scene.render.resolution_y=64;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='OPEN_EXR';scene.render.image_settings.color_mode='RGBA'
    scene.render.filepath=str(root/(name+'.exr'))
    bpy.ops.render.render(write_still=True)
    image=bpy.data.images.load(scene.render.filepath,check_existing=False)
    rendered=np.array(image.pixels[:]).reshape(64,64,4)
    scene.camera=None;bpy.data.objects.remove(camera,do_unlink=True);bpy.data.cameras.remove(camera_data)
    return rendered

source_render=render_emission(objects[0],'authoring_preview')
assert source_render[35,32,0]>source_render[35,19,0]>.05
assert source_render[35,32,0]>1,'HDR emission must exceed the Bloom threshold'
assert source_render[35,45,0]<.01 and source_render[35,3,0]<.01
source=work/'emission.blend';bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
workflow.save_source()
assert Path(mask.filepath).name.startswith('emission_') and Path(mask.filepath).is_file()
assert mask.packed_file and detail.packed_file
mask_name=mask.name;source_hash=authoring.bake_module().sha(source)
output=root/'build';result=group_bake.run(output,bake_device='CPU')
assert authoring.bake_module().sha(source)==source_hash
assert len(result['groups'])==1 and len(result['assets'])==3
group=result['groups'][0]
assert group['emissionIntensity']==8
assert group['atlasLayout']['reusedDetailCharts']>=2,group['atlasLayout']
assert np.allclose([v.uv[:] for v in objects[0].data.uv_layers['UV0'].data],
                   [v.uv[:] for v in objects[2].data.uv_layers['UV0'].data],atol=1e-6)
assert len([f for f in result['files'] if f['path'].endswith('.png')])==2

def pixels(path,linear=False):
    image=bpy.data.images.load(str(path),check_existing=False)
    if linear:image.colorspace_settings.name='Non-Color';image.alpha_mode='CHANNEL_PACKED'
    return np.array(image.pixels[:]).reshape(image.size[1],image.size[0],4)

control=pixels(output/group['surfaceData'],True);base=pixels(output/group['baseColor'])
def sample(obj,point,data):
    mesh=obj.data;mesh.calc_loop_triangles()
    for triangle in mesh.loop_triangles:
        xy=np.array([mesh.vertices[i].co[:2] for i in triangle.vertices])
        weights=np.linalg.solve(np.vstack((xy.T,np.ones(3))),[*point,1.])
        if weights.min() < -1e-6:continue
        uv=weights @ np.array([mesh.uv_layers['UV0'].data[i].uv[:] for i in triangle.loops])
        x,y=(uv*np.array([data.shape[1],data.shape[0]])).astype(int)
        return data[y,x]
    raise AssertionError('Sample is outside mesh')

for index,obj in enumerate(objects):
    for point,expected in (((.3,.55),.25),((.5,.55),1),((.7,.55),0),((.05,.55),0),((4.5,.55),0)):
        value=sample(obj,point,control)
        assert abs(value[2]-expected)<.04,(point,value)
        assert value[0]>.99 and value[1]<.01 and abs(value[3]-184/255)<.01,value
        color=sample(obj,point,base)
        assert np.argmax(color[:3])==(2 if index==1 else 0),color
    assert len(obj.data.materials)==1 and {face.material_index for face in obj.data.polygons}=={0}
    assert [layer.name for layer in obj.data.uv_layers]==['UV0']
preview=workflow.baked_material(group,output)
bsdf=preview.node_tree.nodes.get('Principled BSDF')
assert bsdf.inputs['Emission Strength'].is_linked and bsdf.inputs['Emission Color'].is_linked
objects[0].data.materials[0]=preview
baked_render=render_emission(objects[0],'baked_preview')
assert np.allclose(source_render[35,[3,19,32,45],:3],baked_render[35,[3,19,32,45],:3],atol=.04)
print('PASS mask gray/white/black/outside/empty -> SurfaceData.B; base and other channels preserved; one shared output group',flush=True)
print('PASS actual authoring and baked preview renders agree on emission without scene lights',flush=True)
print('PASS equal emission surfaces overlap final UV0; different base colors remain separate',json.dumps(group['atlasLayout']),flush=True)

bpy.ops.wm.open_mainfile(filepath=str(source))
detail=bpy.data.images['EmissionTestDetail'];mask=bpy.data.images[mask_name]
assert emission.find_mask(detail)==mask and mask.colorspace_settings.name=='Non-Color'
assert emission.intensity(mask)==8
assert np.allclose(np.array(mask.pixels[:]).reshape(32,32,4),values,atol=1/255)
for name,expected in uv_before.items():
    obj=bpy.data.objects[name]
    assert [tuple(v.uv) for v in obj.data.uv_layers['DetailUV'].data]==expected
    assert all(abs(v.color[2])<1e-6 for v in obj.data.color_attributes['PaletteData'].data)
assert np.array(detail.pixels[:],dtype=np.float32).tobytes()==detail_before
print('PASS save/reopen keeps mask association/pixels/UVs; bake adapter never changes authoring PaletteData',flush=True)
(root/'report.json').write_text(json.dumps({'output':str(output),'group':group,'sourceUnchanged':True},indent=2)+'\n',encoding='utf-8')
print('EMISSION_TESTS_COMPLETE',root,flush=True)
