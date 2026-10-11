import pathlib
import sys
import bpy
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
obj=bpy.context.object
mesh=obj.data
mesh.uv_layers.active.name='UV0'
for name in ['PaletteUV','FaceDetailUV','DetailUV','DetailUV_Work']:
    layer=mesh.uv_layers.new(name=name)
    for index,loop in enumerate(layer.data):loop.uv=(index*.01,len(mesh.uv_layers)*.1)
mesh.uv_layers.active_index=mesh.uv_layers.find('DetailUV_Work')
before={u.name:[tuple(l.uv) for l in u.data] for u in mesh.uv_layers}
mat=bpy.data.materials.new('Shared');mat.use_nodes=True
n=mat.node_tree.nodes.new('ShaderNodeUVMap');n.name='Detail_UV';n.uv_map='DetailUV'
mesh.materials.clear();mesh.materials.append(mat)
other=obj.copy();other.data=mesh.copy();bpy.context.collection.objects.link(other)
# Low-level legacy promotion still preserves the original UV reference contract.
bpy.context.scene.wgame_uv_tools.old_detail_uv=addon.detail_lifecycle.promote(obj)
assert mesh.uv_layers.active.name=='DetailUV'
assert [tuple(l.uv) for l in mesh.uv_layers['DetailUV'].data]==before['DetailUV_Work']
assert [tuple(l.uv) for l in mesh.uv_layers['DetailUV_Old'].data]==before['DetailUV']
for name in ['UV0','PaletteUV','FaceDetailUV']:
    assert [tuple(l.uv) for l in mesh.uv_layers[name].data]==before[name]
assert obj.active_material != mat and other.active_material==mat
assert mat.node_tree.nodes['Detail_UV'].uv_map=='DetailUV'
assert obj.active_material.node_tree.nodes['Detail_UV'].uv_map=='DetailUV'
# A surviving reference blocks deletion, then removing it permits it.
ref=obj.active_material.node_tree.nodes.new('ShaderNodeUVMap');ref.uv_map='DetailUV_Old'
try:bpy.ops.wgame_uv.detail_lifecycle(action='DELETE')
except RuntimeError:pass
assert mesh.uv_layers.get('DetailUV_Old')
obj.active_material.node_tree.nodes.remove(ref)
assert bpy.ops.wgame_uv.detail_lifecycle(action='DELETE')=={'FINISHED'}
assert not mesh.uv_layers.get('DetailUV_Old')
assert mesh.uv_layers.get('DetailUV')
print('PASS: promotion preserves coordinates and protected UVs; shared material isolated; referenced backup protected; unreferenced backup removed')
