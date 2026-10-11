import pathlib
import sys
import bpy
import bmesh

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
mesh = bpy.data.meshes.new('Islands')
verts = [(i*3+x,y,0) for i in range(4) for x,y in [(0,0),(1,0),(1,1),(0,1)]]
mesh.from_pydata(verts, [], [(i*4,i*4+1,i*4+2,i*4+3) for i in range(4)])
uv = mesh.uv_layers.new(name='DetailUV')
mesh.uv_layers.new(name='PaletteUV')
mesh.uv_layers.active_index=0
for face in mesh.polygons:
    coords = [(0,0),(.4,0),(.4,.4),(0,.4)] if face.index<2 else [(0.6,0.6),(.8,.6),(.8,.8),(.6,.8)]
    if face.index==3: coords=[(-1,-1)]*4
    for i,co in zip(face.loop_indices,coords):uv.data[i].uv=co
obj=bpy.data.objects.new('Islands',mesh)
bpy.context.collection.objects.link(obj)
obj.select_set(True)
bpy.context.view_layer.objects.active=obj
area=next(a for a in bpy.context.screen.areas if a.type=='VIEW_3D')
area.type='IMAGE_EDITOR'
area.ui_type='UV'
bpy.ops.object.mode_set(mode='EDIT')
bpy.context.scene.tool_settings.use_uv_select_sync=False
def coords():
    bm=bmesh.from_edit_mesh(mesh);bm.faces.ensure_lookup_table()
    layer=bm.loops.layers.uv.active
    return [[tuple(l[layer].uv) for l in f.loops] for f in bm.faces]
before=coords()
with bpy.context.temp_override(area=area):
    assert bpy.ops.wgame_uv.island_tools(action='OVERLAP')=={'FINISHED'}
    bm=bmesh.from_edit_mesh(mesh);bm.faces.ensure_lookup_table()
    assert [f.index for f in bm.faces if f.select]==[0,1], [f.index for f in bm.faces if f.select]
    assert coords()==before
    assert bpy.ops.wgame_uv.island_tools(action='SEPARATE')=={'FINISHED'}
    after=coords()
    assert after[2:]==before[2:]
    assert min(p[0] for p in after[1])>max(p[0] for p in after[0])
    for i in (0,1):
        assert abs((after[i][1][0]-after[i][0][0])-.4)<1e-5
    assert bpy.ops.wgame_uv.island_tools(action='RELAX')=={'FINISHED'}
    assert coords()[2:]==before[2:]
    assert bpy.ops.wgame_uv.island_tools(action='PACK')=={'FINISHED'}
    after=coords()
    assert after[2:]==before[2:]
    assert all(-1e-5<=v<=1.00001 for i in (0,1) for co in after[i] for v in co),after
assert len([s for s in bpy.data.scenes if s.name.startswith('WGame UV Temporary')])==0
assert bpy.context.mode=='EDIT_MESH'
print('PASS: exact overlap selection, separate preserving dimensions, relax, 0-1 pack, untouched islands and sentinel, scene cleanup')

# Protected pinned islands cancel without partial writes; sync mode uses mesh selection.
bpy.context.scene.tool_settings.use_uv_select_sync=True
bm=bmesh.from_edit_mesh(mesh);bm.faces.ensure_lookup_table()
layer=bm.loops.layers.uv.active
bm.faces[0].loops[0][layer].pin_uv=True
baseline=coords()
with bpy.context.temp_override(area=area):
    try:
        bpy.ops.wgame_uv.island_tools(action='PACK')
    except RuntimeError:
        pass
assert coords()==baseline
bm.faces[0].loops[0][layer].pin_uv=False
with bpy.context.temp_override(area=area):
    assert bpy.ops.wgame_uv.island_tools(action='SEPARATE')=={'FINISHED'}
assert coords()[2:]==before[2:]
print('PASS: pinned cancellation and synchronized selection')

bpy.context.scene.tool_settings.use_uv_select_sync=False
bm=bmesh.from_edit_mesh(mesh);bm.faces.ensure_lookup_table()
uv=bm.loops.layers.uv.active
for f in bm.faces:
    f.select_set(True)
    for l in f.loops:l.uv_select_vert=False
bm.faces[0].loops[0].uv_select_vert=True
before_clear=coords()
with bpy.context.temp_override(area=area):
    assert bpy.ops.wgame_uv.clear_selected()=={'FINISHED'}
after_clear=coords()
assert after_clear[0][0]==(-1.0,-1.0)
assert after_clear[0][1:]==before_clear[0][1:]
assert after_clear[1:]==before_clear[1:]
print('PASS: clear only selected UV point, preserve other points and islands')
