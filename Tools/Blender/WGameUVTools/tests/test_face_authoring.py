import pathlib
import sys
import bpy
import bmesh
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
obj=bpy.context.active_object
# Cube: one face is an open disk; entire cube needs automatic cuts.
obj.data.uv_layers.active.name='DetailUV'
obj.data.uv_layers.new(name='PaletteUV')
obj.data.uv_layers.active_index=0
editor=next(a for a in bpy.context.screen.areas if a.type=='VIEW_3D')
editor.type='IMAGE_EDITOR';editor.ui_type='UV'
bpy.ops.object.mode_set(mode='EDIT')
def snapshot():
    bm=bmesh.from_edit_mesh(obj.data)
    return {u.name:[[tuple(l[u].uv) for l in f.loops] for f in bm.faces] for u in bm.loops.layers.uv.values()}
before=snapshot()
with bpy.context.temp_override(area=editor):
    assert bpy.ops.wgame_uv.face_authoring(action='NEW')=={'FINISHED'}
    name=obj.data.uv_layers.active.name
    assert name.startswith('DetailUV_Work')
    state=snapshot()
    assert all(co==(-1,-1) for f in state[name] for co in f)
    assert state['DetailUV']==before['DetailUV']
    bm=bmesh.from_edit_mesh(obj.data);bm.faces.ensure_lookup_table()
    for f in bm.faces:f.select_set(f.index==0)
    assert bpy.ops.wgame_uv.face_authoring(action='CREATE')=={'FINISHED'}
    state=snapshot()
    assert addon.islands.area([__import__('mathutils').Vector(p) for p in state[name][0]])>0
    assert all(co==(-1,-1) for f in state[name][1:] for co in f)
    assert state['DetailUV']==before['DetailUV']
    assert state['PaletteUV']==before['PaletteUV']
    assert bpy.ops.wgame_uv.face_authoring(action='CLEAR')=={'FINISHED'}
    assert all(co==(-1,-1) for f in snapshot()[name] for co in f)
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.context.scene.wgame_uv_tools.face_unwrap_mode='SPLIT'
    assert bpy.ops.wgame_uv.face_authoring(action='CREATE')=={'FINISHED'}
    assert all(addon.islands.area([__import__('mathutils').Vector(p) for p in f])>1e-8 for f in snapshot()[name])
    assert snapshot()['DetailUV']==before['DetailUV']
    assert snapshot()['PaletteUV']==before['PaletteUV']
assert bpy.context.mode=='EDIT_MESH'
assert not any(s.name.startswith('WGame UV Temporary') for s in bpy.data.scenes)
print('PASS: blank UV preserves original channels; selected-face unwrap; full-face clear; closed-mesh automatic cuts; cleanup')

# Painting changes the image editor to PAINT. Returning the mesh to Edit mode
# must allow the create button to restore UV editing without discarding paint.
image=bpy.data.images.new('PaintedEmission',width=4,height=4,alpha=True)
pixels=[.2,.4,.6,1.]*16;image.pixels[:]=pixels
pixel_before=list(image.pixels)
editor.spaces.active.image=image;editor.spaces.active.mode='PAINT'
assert editor.ui_type!='UV'
state_before=snapshot()
bm=bmesh.from_edit_mesh(obj.data);bm.faces.ensure_lookup_table()
for face in bm.faces:face.select_set(face.index==0)
bpy.context.scene.wgame_uv_tools.face_unwrap_mode='KEEP'
with bpy.context.temp_override(area=editor):
    assert bpy.ops.wgame_uv.face_authoring(action='CREATE')=={'FINISHED'}
assert editor.ui_type=='UV' and editor.spaces.active.image==image
assert list(image.pixels)==pixel_before
after=snapshot()
assert after[name][1:]==state_before[name][1:]
assert after['DetailUV']==state_before['DetailUV'] and after['PaletteUV']==state_before['PaletteUV']
assert bpy.context.mode=='EDIT_MESH'
assert not any(s.name.startswith('WGame UV Temporary') for s in bpy.data.scenes)
print('PASS: create from image Paint mode restores UV editor; image pixels and unselected UVs preserved')

# A button in the 3D side panel must also find and restore the Paint editor.
editor.spaces.active.mode='PAINT'
view=next(a for a in bpy.context.screen.areas if a.type=='PROPERTIES');view.type='VIEW_3D'
state_before=snapshot()
with bpy.context.temp_override(area=view):
    assert bpy.ops.wgame_uv.face_authoring(action='CREATE')=={'FINISHED'}
assert editor.ui_type=='UV' and editor.spaces.active.image==image
assert list(image.pixels)==pixel_before and snapshot()[name][1:]==state_before[name][1:]
assert bpy.context.mode=='EDIT_MESH'
print('PASS: 3D panel create also restores a Paint image editor')
