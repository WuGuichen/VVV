"""Blender smoke test for scene palette strokes in object and edit modes."""
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from WGameEnvironmentTools import authoring, color_profile, palette_brush, register, unregister

bpy.ops.wm.read_factory_settings(use_empty=True)
authoring.palette_module().register()
register()
scene = bpy.context.scene
scene['wgame_environment_work'] = True
color_profile.seed(scene)
mesh = bpy.data.meshes.new('TwoFaces')
mesh.from_pydata(
    [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0),
     (2, 0, 0), (2, 1, 0)], [],
    [(0, 1, 2, 3), (1, 4, 5, 2)])
mesh.update()
obj = bpy.data.objects.new('ScenePart', mesh)
scene.collection.objects.link(obj)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
view = next(area for area in bpy.context.screen.areas if area.type == 'VIEW_3D')
region = next(region for region in view.regions if region.type == 'WINDOW')
with bpy.context.temp_override(area=view, region=region):
    # Registering the scene brush must not replace the character brush RNA poll.
    assert bpy.ops.wgame.color_palette_brush.poll()
    assert not bpy.ops.wgame.environment_brush.poll()
    bpy.ops.object.mode_set(mode='EDIT')
    assert bpy.ops.wgame.color_palette_brush.poll()
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.window_manager.wgame_palette_brush_active = True
    assert not bpy.ops.wgame.color_palette_brush.poll()
    bpy.context.window_manager.wgame_palette_brush_active = False
    obj['wgame_environment_asset'] = obj.name
    assert bpy.ops.wgame.environment_brush.poll()
    assert bpy.ops.wgame.color_palette_brush.poll()
plain = bpy.data.materials.new('OriginalPureColor')
plain.use_nodes = True
mesh.materials.append(plain)
image = bpy.data.images.new('SharedCanvas', width=8, height=8, alpha=True)
image['wgame_environment_shared_detail'] = True
detail = mesh.uv_layers.new(name='DetailUV')
for loop in detail.data:
    loop.uv = (-1, -1)
before = [tuple(loop.uv) for loop in detail.data]
module = authoring.palette_module()
red = module.color_index_from_rgb8(158, 56, 40)
blue = module.color_index_from_rgb8(80, 91, 98)
original = palette_brush.capture(obj, [0])

assert 'FINISHED' in bpy.ops.wgame.environment_brush_stroke(
    object_name=obj.name, indices='0', color_index=red, surface_profile=11)
assert palette_brush.face_color(obj, 0)[:2] == (red, 11)
assert palette_brush.face_colors(obj) == [red, -1]
assert [tuple(loop.uv) for loop in detail.data] == before
assert mesh.attributes.get('ColorProfileSlot') is None
assert len(mesh.materials) == 2
assert mesh.polygons[0].material_index == 1 and mesh.polygons[1].material_index == 0
assert mesh.materials[0] == plain and mesh.materials[1].get('wgame_environment_shared')
palette_brush.restore(obj, original)
assert mesh.polygons[0].material_index == 0
assert palette_brush.face_colors(obj) == [-1, -1]
assert mesh.attributes.get('ColorProfileSlot') is None
assert 'FINISHED' in bpy.ops.wgame.environment_brush_stroke(
    object_name=obj.name, indices='0', color_index=red, surface_profile=11)

bpy.ops.object.mode_set(mode='EDIT')
edit_original = palette_brush.capture(obj, [1])
assert 'FINISHED' in bpy.ops.wgame.environment_brush_stroke(
    object_name=obj.name, indices='1', color_index=blue, surface_profile=12)
assert palette_brush.face_color(obj, 1)[:2] == (blue, 12)
assert palette_brush.face_colors(obj) == [red, blue]
palette_brush.restore(obj, edit_original)
assert palette_brush.face_colors(obj) == [red, -1]
assert 'FINISHED' in bpy.ops.wgame.environment_brush_stroke(
    object_name=obj.name, indices='1', color_index=blue, surface_profile=12)
bpy.ops.object.mode_set(mode='OBJECT')
assert [poly.material_index for poly in mesh.polygons] == [1, 1]
assert [tuple(loop.uv) for loop in mesh.uv_layers['DetailUV'].data] == before
assert mesh.attributes.get('ColorProfileSlot') is None
bpy.ops.object.select_all(action='DESELECT')
selection_mesh = mesh.copy()
for polygon in selection_mesh.polygons:
    polygon.material_index = 0
selection_obj = bpy.data.objects.new('SelectionPart', selection_mesh)
scene.collection.objects.link(selection_obj)
selection_obj['wgame_environment_asset'] = selection_obj.name
selection_obj.location.x = 3
selection_obj.select_set(True)
bpy.context.view_layer.objects.active = selection_obj
scene.wgame_environment_color = tuple(value / 255 for value in (219, 164, 45))
scene.wgame_environment_profile = '11'
assert 'FINISHED' in bpy.ops.wgame.environment_profile_add()
bpy.ops.object.mode_set(mode='EDIT')
import bmesh
bm = bmesh.from_edit_mesh(selection_mesh)
bm.faces.ensure_lookup_table()
for face in bm.faces:
    face.select_set(False)
bm.faces[0].select_set(True)
bmesh.update_edit_mesh(selection_mesh)
assert 'FINISHED' in bpy.ops.wgame.environment_apply_color()
bpy.ops.object.mode_set(mode='OBJECT')
assert selection_mesh.polygons[0].material_index == 1
assert selection_mesh.polygons[1].material_index == 0
assert palette_brush.face_color(selection_obj, 0)[:2] == (module.color_index_from_rgb8(219, 164, 45), 11)
selected_item = color_profile.active(scene)
assert palette_brush.selected_color(scene) == (selected_item.color_index, 11, selected_item.category_index)
assert selection_mesh.attributes['ColorProfileSlot'].data[0].value == selected_item.category_index
bpy.context.view_layer.objects.active = obj
from mathutils import Vector
sampled, face_index = palette_brush.sample_hit(
    scene, bpy.context.evaluated_depsgraph_get(), Vector((3.5, .5, 1)), Vector((0, 0, -1)))
assert sampled == selection_obj and face_index == 0
scene.wgame_environment_color = (0, 0, 0)
assert palette_brush.select_from_face(scene, sampled, face_index) == scene.wgame_environment_color_active
assert module.color_index_from_rgb8(*(round(v * 255) for v in scene.wgame_environment_color)) == selected_item.color_index
stale_mesh = selection_mesh.copy()
stale_mesh.polygons[0].material_index = 0
stale = bpy.data.objects.new('StalePureMaterial', stale_mesh)
scene.collection.objects.link(stale)
stale['wgame_environment_asset'] = stale.name
actual_color, _, _, _ = palette_brush.face_color(stale, 0)
assert actual_color != selected_item.color_index
assert color_profile.used_pairs(scene)[(actual_color, 11)] >= 1
assert palette_brush.select_from_face(scene, stale, 0) == -1
assert module.color_index_from_rgb8(*(round(v * 255) for v in scene.wgame_environment_color)) == actual_color
unregister()
print('PASS scene palette strokes preserve DetailUV and use scene Profile categories')
