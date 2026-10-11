"""Regression: saving a draft is independent of bake readiness; plain colors bake."""
import json
import sys
import tempfile
from pathlib import Path

import bpy
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from WGameEnvironmentTools import authoring,shared_detail,group_bake,workflow

root=Path(tempfile.mkdtemp(prefix='constant-save-',dir=authoring.repository()/'Temp/EnvironmentSurface'))
work=root/'Blender_Work';work.mkdir()
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene['wgame_environment_work']=True;scene['wgame_environment_pipeline']=shared_detail.PIPELINE
scene['wgame_bake_output_size']='512'
palette=authoring.palette_module();objects=[]
for index,color in enumerate(((.55,.04,.025,1),(.02,.06,.6,1))):
    mesh=bpy.data.meshes.new('Mesh'+str(index));mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2,3)]);mesh.update()
    obj=bpy.data.objects.new('Prop_Constant'+str(index),mesh);scene.collection.objects.link(obj);obj['wgame_environment_asset']=obj.name;obj.location.x=index*2
    palette._apply_color_profile_to_polygons(obj,[0],palette.color_index_from_rgb8(120,120,120),11)
    layer=mesh.uv_layers.get('DetailUV') or mesh.uv_layers.new(name='DetailUV')
    for entry in layer.data:entry.uv=(-1,-1)
    mat=bpy.data.materials.new('Solid'+str(index));mat.use_nodes=True;mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color
    mesh.materials.clear();mesh.materials.append(mat);objects.append(obj)
image=bpy.data.images.new('UnboundDraftCanvas',width=8,height=8,alpha=True)
image['wgame_environment_shared_detail']=True
pixels=np.tile([.2,.3,.4,.8],64).astype(np.float32);image.pixels.foreach_set(pixels)
bpy.ops.wm.save_as_mainfile(filepath=str(work/'test.blend'))
before=workflow.content_fingerprint(scene)
workflow.save_source()
assert image.packed_file and Path(image.filepath).is_file()
assert workflow.content_fingerprint(scene)==before
assert len(shared_detail.validate(scene)['objects'])==2
assert group_bake.detail_image(objects[0]) is None and len(group_bake.groups_for(objects))==1
source_hash=authoring.bake_module().sha(bpy.data.filepath)
result=group_bake.run(root/'bake')
assert authoring.bake_module().sha(work/'test.blend')==source_hash
assert len(result['groups'])==1 and len(result['assets'])==2
assert result['groups'][0]['textureSize']==512
assert result['groups'][0]['atlasLayout']['sharedConstantTiles']==2
texture=bpy.data.images.load(str(root/'bake'/result['groups'][0]['baseColor']))
data=np.array(texture.pixels[:]).reshape(texture.size[1],texture.size[0],4)
colors=[]
for obj in objects:
    assert [uv.name for uv in obj.data.uv_layers]==['UV0']
    uv=np.mean([obj.data.uv_layers[0].data[i].uv[:] for i in obj.data.polygons[0].loop_indices],axis=0)
    x,y=(uv*np.array(texture.size)).astype(int);colors.append(data[y,x,:3])
assert colors[0][0]>colors[0][2]*3 and colors[1][2]>colors[1][0]*3,colors
print('PASS constant material save and real shared bake',flush=True)

# A truly multi-image draft must save both images, but remain blocked for baking.
bpy.ops.wm.open_mainfile(filepath=str(work/'test.blend'))
obj=bpy.data.objects['Prop_Constant0'];mat=obj.data.materials[0];tree=mat.node_tree
uv=tree.nodes.new('ShaderNodeUVMap');uv.uv_map='DetailUV'
for index in range(2):
    image=bpy.data.images.new('LinkedDraft'+str(index),width=8,height=8,alpha=True)
    node=tree.nodes.new('ShaderNodeTexImage');node.image=image;tree.links.new(uv.outputs['UV'],node.inputs['Vector'])
workflow.save_source()
assert all(bpy.data.images['LinkedDraft'+str(i)].packed_file for i in range(2))
try:group_bake.detail_image(obj)
except ValueError as error:assert 'LinkedDraft0' in str(error) and 'LinkedDraft1' in str(error)
else:raise AssertionError('Multi-image bake was incorrectly accepted')
print('PASS multi-image draft saves while bake remains blocked',flush=True)
print('CONSTANT_SAVE_TESTS_COMPLETE',root,flush=True)
