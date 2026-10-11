"""Blender regression: late asset intake preserves art and cannot silently omit meshes."""
import sys
from pathlib import Path

import bpy

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from WGameEnvironmentTools import registration, shared_detail, group_bake, assembly

bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene['wgame_environment_work']=True
scene['wgame_environment_pipeline']=shared_detail.PIPELINE


def make(name):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2),(0,2,3)])
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
    for i,color in enumerate(((.23,.02,.015,1),(.08,.09,.1,1))):
        mat=bpy.data.materials.new(name+str(i));mat.use_nodes=True
        mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color
        mesh.materials.append(mat)
    mesh.polygons[1].material_index=1
    uv=mesh.uv_layers.new(name='OriginalUV')
    for i,loop in enumerate(uv.data):loop.uv=(i*.1,.25)
    return obj


def art(obj):
    return ([tuple(v.co) for v in obj.data.vertices],
            [(tuple(p.vertices),p.material_index) for p in obj.data.polygons],
            [m.as_pointer() for m in obj.data.materials],
            [tuple(v.uv) for v in obj.data.uv_layers['OriginalUV'].data],
            tuple(obj.matrix_world))


def fails(action,text):
    try:action()
    except ValueError as error:assert text in str(error),str(error)
    else:raise AssertionError('Expected rejection: '+text)


first=make('Arch_NewCrane');before=art(first)
# Unselected users of the same mesh must remain untouched.
peer=bpy.data.objects.new('UnlinkedPeer',first.data)
original=first.data
fails(lambda:shared_detail.validate(scene),'未登记模型')
fails(lambda:group_bake.run(Path('/unused-output')),'未登记模型')
result=registration.register_models(scene,[first])
assert result['registered']==1 and first[registration.ASSET_KEY]=='Arch_NewCrane'
assert first.data!=original and peer.data==original and original.uv_layers.get('PaletteUV') is None
assert art(first)==before
assert first.data.uv_layers.active.name=='OriginalUV'
assert all(tuple(v.uv)==(-1,-1) for v in first.data.uv_layers['DetailUV'].data)
assert first.data.attributes['PaletteToken'].domain=='FACE'
assert group_bake.detail_image(first) is None
assert len(shared_detail.validate(scene)['objects'])==1
mesh=first.data;assert registration.register_models(scene,[first])['unchanged']==1 and first.data==mesh
fails(lambda:registration.set_auxiliary([first]),'已登记组件')

# Batch validation is atomic, including ID collisions and unsupported source materials.
good=make('Prop_Good');bad=make('Prop_Bad');good_mesh=good.data;bad_mesh=bad.data
node=bad.data.materials[0].node_tree.nodes.new('ShaderNodeTexImage')
bad.data.materials[0].node_tree.links.new(node.outputs['Color'],bad.data.materials[0].node_tree.nodes['Principled BSDF'].inputs['Base Color'])
fails(lambda:registration.register_models(scene,[good,bad]),'底色是节点图')
assert good.data==good_mesh and bad.data==bad_mesh and not good.get(registration.ASSET_KEY)
registration.set_auxiliary([bad]);assert bad not in registration.pending(scene)
registration.set_auxiliary([bad],False);assert bad in registration.pending(scene)
registration.set_auxiliary([bad])
fails(lambda:registration.register_models(scene,[good],identifier='Arch_NewCrane'),'已被占用')
assert good.data==good_mesh and not good.get(registration.ASSET_KEY)

# A preparation failure after another mesh has been staged must not commit either model.
second=make('Prop_Second');second_mesh=second.data
original_prepare=registration._prepare_mesh
calls=[]
def fail_second(mesh,pairs):
    calls.append(mesh)
    if len(calls)==2:raise ValueError('injected preparation failure')
    original_prepare(mesh,pairs)
registration._prepare_mesh=fail_second
try:fails(lambda:registration.register_models(scene,[good,second]),'injected preparation failure')
finally:registration._prepare_mesh=original_prepare
assert good.data==good_mesh and second.data==second_mesh
assert not good.get(registration.ASSET_KEY) and not second.get(registration.ASSET_KEY)
registration.set_auxiliary([second])

# Preserve authored DetailUV and existing palette data when attaching a missing registration.
uv=good.data.uv_layers.new(name='DetailUV')
for p in good.data.polygons:
    for index,value in zip(p.loop_indices,((.1,.1),(.8,.1),(.1,.8))):uv.data[index].uv=value
detail_before=[tuple(v.uv) for v in uv.data]
registration.register_models(scene,[good]);assert [tuple(v.uv) for v in good.data.uv_layers['DetailUV'].data]==detail_before
# This intentional mapped-without-image draft must still fail the existing material validation.
fails(lambda:shared_detail.validate(scene),'材质未连接细节图')
for value in good.data.uv_layers['DetailUV'].data:value.uv=(-1,-1)
assert len(shared_detail.validate(scene)['objects'])==2
good[registration.ASSET_KEY]=first[registration.ASSET_KEY]
fails(lambda:registration.require_registered(scene),'重复的场景组件 ID')
good[registration.ASSET_KEY]='Prop_Good'
assert len(assembly.audit_scene(scene)['assets'])==2
assert len(group_bake.groups_for([first,good]))==1
print('PASS registration: missing detection, isolated shared meshes, art preservation, idempotence, atomic rejection, ID uniqueness, auxiliary opt-out, UV preservation, bake and assembly preflight')
