import pathlib
import sys
import bpy
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
obj=bpy.context.object
mesh=obj.data
mesh.uv_layers.active.name='UV0'
for name in ['FaceDetailUV','PaletteUV','DetailUV','DetailUV_Old']:
    layer=mesh.uv_layers.new(name=name)
    for l in layer.data:l.uv=(-1,-1)
for i,l in enumerate(mesh.uv_layers['PaletteUV'].data):l.uv=(.25,.75)
for i,l in enumerate(mesh.uv_layers['FaceDetailUV'].data):l.uv=(i*.01,.2)
original=[tuple(l.uv) for l in mesh.uv_layers['UV0'].data]
face=[tuple(l.uv) for l in mesh.uv_layers['FaceDetailUV'].data]
mat=bpy.data.materials.new('Face');mat.use_nodes=True
n=mat.node_tree.nodes.new('ShaderNodeUVMap');n.uv_map='FaceDetailUV'
mesh.materials.clear();mesh.materials.append(mat)
assert bpy.ops.wgame_uv.normalize_channels()=={'FINISHED'}
assert list(mesh.uv_layers.keys())==['UV0','PaletteUV','DetailUV']
assert [tuple(l.uv) for l in mesh.uv_layers['UV0'].data]==original
assert [tuple(l.uv) for l in mesh.uv_layers['DetailUV'].data]==face
assert all(tuple(l.uv)==(.25,.75) for l in mesh.uv_layers['PaletteUV'].data)
assert next(n.uv_map for n in obj.active_material.node_tree.nodes if n.type=='UVMAP')=='DetailUV'
assert n.uv_map=='FaceDetailUV'  # original shared material untouched
legacy=mesh.uv_layers.new(name='FaceDetailUV')
for l in legacy.data:l.uv=(.9,.9)
before={u.name:[tuple(l.uv) for l in u.data] for u in mesh.uv_layers}
try:bpy.ops.wgame_uv.normalize_channels()
except RuntimeError:pass
assert before=={u.name:[tuple(l.uv) for l in u.data] for u in mesh.uv_layers}
print('PASS: canonical order; UV0/palette preservation; face merge + material isolation; conflict cancels without data change')
