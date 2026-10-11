import pathlib
import sys
import bpy
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
obj=bpy.context.object
obj.data.uv_layers.active.name='UV0'
obj.data.uv_layers.new(name='DetailUV')
obj.data.uv_layers.new(name='PaletteUV')
obj.data.uv_layers.active_index=0
s=bpy.context.scene.wgame_uv_tools
assert s.uv_map=='UV0'
bpy.ops.wgame_uv.checker()
obj.data.uv_layers.active_index=1
bpy.context.view_layer.update()
assert s.uv_map=='DetailUV'
def material_uv():
    mod=addon.checker_modifiers(obj)[0]
    mat=mod.node_group.nodes['CheckerMaterial'].inputs['Material'].default_value
    return next(n.uv_map for n in mat.node_tree.nodes if n.type=='UVMAP')
assert material_uv()=='DetailUV'
s.uv_map='UV0'
assert obj.data.uv_layers.active.name=='UV0'
assert material_uv()=='UV0'
obj.data.uv_layers.active_index=2
bpy.context.view_layer.update()
assert s.uv_map=='PaletteUV'
assert not addon.checker_modifiers(obj)
addon.unregister()
assert addon.sync_checker_uv not in bpy.app.handlers.depsgraph_update_post
print('PASS: native UV selection -> panel + checker; panel -> native selection + checker; palette protection; handler cleanup')
