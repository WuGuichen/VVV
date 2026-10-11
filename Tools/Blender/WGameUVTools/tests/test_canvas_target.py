"""Canvas selection stays independent of palette-only faces and paint channels."""
from pathlib import Path
import sys
import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
c=addon.canvas_editor; d=addon.decals
bpy.ops.mesh.primitive_cube_add()
obj=bpy.context.object; mesh=obj.data
mesh.uv_layers.active.name='DetailUV'
detail=bpy.data.images.new('Color detail',width=16,height=16,alpha=True)
mask=bpy.data.images.new('Emission mask',width=16,height=16,alpha=True)
mask.colorspace_settings.name='Non-Color'
source=bpy.data.images.new('Color stamp',width=4,height=4,alpha=True)
d.write_pixels(source,np.full((4,4,4),[0,1,0,1],dtype=np.float32))
mask_source=bpy.data.images.new('Mask stamp',width=4,height=4,alpha=True)
mask_source.colorspace_settings.name='Non-Color'
d.write_pixels(mask_source,np.ones((4,4,4),dtype=np.float32))
mat=bpy.data.materials.new('Shared detail');mat.use_nodes=True;mat['wgame_environment_shared']=True
uv=mat.node_tree.nodes.new('ShaderNodeUVMap');uv.uv_map='DetailUV'
nodes=[]
for name,image in (('Detail_Texture',detail),('Emission_Detail_Texture',mask)):
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.name=name;node.image=image
    mat.node_tree.links.new(uv.outputs['UV'],node.inputs['Vector']);nodes.append(node)
mesh.materials.append(mat);mesh.materials.append(bpy.data.materials.new('Pure body'))
for face in mesh.polygons:
    if face.index:
        face.material_index=1
        for index in face.loop_indices:mesh.uv_layers.active.data[index].uv=(-1,-1)
original_uv=[tuple(v.uv) for v in mesh.uv_layers.active.data]
base=d.pixels(detail).copy(); original_mask=d.pixels(mask).copy()
area=next(a for a in bpy.context.screen.areas if a.type=='VIEW_3D');area.type='IMAGE_EDITOR'
with bpy.context.temp_override(area=area):
    area.spaces.active.image=detail;mat.node_tree.nodes.active=nodes[1]
    assert c.target_image(bpy.context)==detail, 'Displayed color canvas must win over the active emission node'
    session=c.begin(bpy.context,'IMPORT',source)
    assert session['target']==detail and np.array_equal(d.pixels(detail),base)
    c.cancel();assert nodes[0].image==detail and nodes[1].image==mask
    area.spaces.active.image=mask;mat.node_tree.nodes.active=nodes[0]
    assert c.target_image(bpy.context)==mask
    c.begin(bpy.context,'IMPORT',mask_source);c.commit()
    assert not np.array_equal(d.pixels(mask),original_mask)
    assert np.array_equal(d.pixels(detail),base), 'Emission canvas edits must preserve color'
    c.undo();assert np.array_equal(d.pixels(mask),original_mask)
    # No displayed image: infer from only the mapped faces, skipping the pure body.
    area.spaces.active.image=None
    assert c.target_image(bpy.context)==detail
    # A standalone canvas also works before any model UVs have been prepared.
    area.spaces.active.image=detail;bpy.context.view_layer.objects.active=None
    c.begin(bpy.context,'IMPORT',source)
    assert c._session['uv_segments']==[]
    c.cancel();assert np.array_equal(d.pixels(detail),base)
    area.spaces.active.image=None
    try:c.target_image(bpy.context);raise AssertionError('Missing canvas must be rejected')
    except ValueError as exc:assert '图片' in str(exc) or '画布' in str(exc)
    bpy.context.view_layer.objects.active=obj
assert original_uv==[tuple(v.uv) for v in mesh.uv_layers.active.data]
assert not any(i.get(c.TAG) for i in bpy.data.images)
print('PASS canvas: partial material assignments, explicit color/emission selection, standalone editing, missing canvas, cancel/undo and original UV preservation')
