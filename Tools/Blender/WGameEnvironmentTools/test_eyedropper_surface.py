"""Sampling a plain material must not reuse unrelated legacy mesh profiles."""
import sys
from pathlib import Path
import bpy

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import WGameEnvironmentTools as addon
from WGameEnvironmentTools import color_profile, palette_brush
addon.register()
scene=bpy.context.scene;scene['wgame_environment_work']=True
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_plane_add()
obj=bpy.context.object;obj['wgame_environment_asset']='Prop_Eyedropper'
mat=bpy.data.materials.new('Source concrete');mat.use_nodes=True;obj.data.materials.append(mat)
bsdf=mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value=(.24,.25,.26,1)
bsdf.inputs['Roughness'].default_value=.9;bsdf.inputs['Metallic'].default_value=0
module=addon.authoring.palette_module()
color=module.color_index_from_rgb8(134,137,139)
obj.data.attributes.new(name='SurfaceProfile',type='INT',domain='FACE').data[0].value=12
obj.data.attributes.new(name='PaletteToken',type='INT',domain='FACE').data[0].value=color*16+12
obj.data.uv_layers.active.name='PaletteUV'
detail=obj.data.uv_layers.new(name='DetailUV')
for item in detail.data:item.uv=(-1,-1)
scene['wgame_environment_color_profile_source']='materialOrPaletteV2'
scene['wgame_environment_color_profile_initialized']=True
old=color_profile.add(scene,color,12,'Legacy metallic entry')
old.slot_id='legacy';old.category_index=17
before=[tuple(item.uv) for item in detail.data]
assert color_profile.face_pair(obj,0)==(color,10), 'Concrete must sample as matte, not the stale metal profile'
found=palette_brush.select_from_face(scene,obj,0)
assert found>=0 and color_profile.pair(color_profile.active(scene))==(color,10)
assert any(item.slot_id=='legacy' and item.category_index==17 for item in scene.wgame_environment_colors)
assert obj.data.attributes['SurfaceProfile'].data[0].value==12, 'Sampling must not repaint source data'
assert obj.data.materials[0]==mat and obj.data.polygons[0].material_index==0
# Pure materials follow their source BSDF (and explicit material profile overrides).
bsdf.inputs['Roughness'].default_value=.5
assert color_profile.face_pair(obj,0)[1]==11
bsdf.inputs['Metallic'].default_value=.8
assert color_profile.face_pair(obj,0)[1]==12
bsdf.inputs['Roughness'].default_value=.2
assert color_profile.face_pair(obj,0)[1]==13
mat['wgame_surface_profile']=14
assert color_profile.face_pair(obj,0)[1]==14
del mat['wgame_surface_profile']
bsdf.inputs['Roughness'].default_value=.9;bsdf.inputs['Metallic'].default_value=0
bpy.ops.object.mode_set(mode='EDIT')
assert color_profile.face_pair(obj,0)==(color,10)
bpy.ops.object.mode_set(mode='OBJECT')
image=bpy.data.images.new('Empty shared detail',width=16,height=16,alpha=True)
image['wgame_environment_shared_detail']=True;scene.wgame_environment_detail_image=image
item=color_profile.active(scene)
assert bpy.ops.wgame.environment_brush_stroke(object_name=obj.name,indices='0',color_index=item.color_index,
       surface_profile=int(item.surface_profile),category_index=item.category_index)=={'FINISHED'}
assert palette_brush.face_color(obj,0)[:2]==(color,10)
assert before==[tuple(item.uv) for item in obj.data.uv_layers['DetailUV'].data]
# Already painted shared materials retain their authored per-face profile.
assert bpy.ops.wgame.environment_brush_stroke(object_name=obj.name,indices='0',color_index=color,
       surface_profile=12,category_index=17)=={'FINISHED'}
assert color_profile.face_pair(obj,0)==(color,12)
print('PASS eyedropper source surface, legacy profile refresh, explicit overrides, edit mode, paint-back and shared authored profiles')
