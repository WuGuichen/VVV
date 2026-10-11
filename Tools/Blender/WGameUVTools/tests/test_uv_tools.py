"""Run in a factory-startup Blender process, never in an artist's open file."""
import importlib.util
import pathlib
import sys
import bpy

root = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('wgame_uv_test', root / '__init__.py')
addon = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = addon
spec.loader.exec_module(addon)
addon.register()
obj = bpy.context.active_object
obj.name = 'UV_Test'
original = bpy.data.materials.new('OriginalShared')
original.use_nodes = True
obj.data.materials.clear()
obj.data.materials.append(original)
obj.data.materials.append(bpy.data.materials.new('SecondMaterial'))
for poly in obj.data.polygons:
    poly.material_index = poly.index % 2
obj.data.uv_layers.new(name='PaletteUV')
obj.data.uv_layers.new(name='DetailUV')
obj.data.uv_layers.active_index = 0
obj.data.uv_layers[0].active_render = True
other = obj.copy()
other.data = obj.data
other.name = 'UnselectedSharedMesh'
bpy.context.collection.objects.link(other)
other.select_set(False)

def snapshot():
    return (
        tuple(tuple(v.co) for v in obj.data.vertices),
        tuple((u.name, tuple(tuple(l.uv) for l in u.data), u.active_render) for u in obj.data.uv_layers),
        tuple(m.as_pointer() for m in obj.data.materials),
        tuple(p.material_index for p in obj.data.polygons),
        tuple((n.name, n.bl_idname) for n in original.node_tree.nodes),
        tuple((l.from_node.name, l.to_node.name) for l in original.node_tree.links),
    )

baseline = snapshot()
settings = bpy.context.scene.wgame_uv_tools
assert bpy.ops.wgame_uv.checker() == {'FINISHED'}
mods = addon.checker_modifiers(obj)
assert len(mods) == 1 and not mods[0].show_render and mods[0].show_in_editmode
assert not addon.checker_modifiers(other)
bpy.context.view_layer.update()
evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
assert any(m and m.get(addon.TAG) for m in evaluated.data.materials)
assert snapshot() == baseline
settings.cells = 12
settings.pattern = 'COLOR'
assert bpy.ops.wgame_uv.checker(action='UPDATE') == {'FINISHED'}
assert len(addon.checker_modifiers(obj)) == 1
assert bpy.ops.wgame_uv.checker() == {'FINISHED'}
assert snapshot() == baseline and not addon.checker_modifiers(obj)
print('PASS: evaluated checker; shared mesh/material isolation; multi-material restoration')

# Edit mode must retain mesh data and selection while previewing.
bpy.ops.object.mode_set(mode='EDIT')
assert bpy.ops.wgame_uv.checker() == {'FINISHED'}
assert bpy.context.mode == 'EDIT_MESH'
assert bpy.ops.wgame_uv.checker() == {'FINISHED'}
bpy.ops.object.mode_set(mode='OBJECT')
assert snapshot() == baseline
print('PASS: edit-mode toggle without UV/geometry/material changes')

# Missing UV and color-address UV are rejected before any modifier is added.
for name in ['PaletteUV', 'MissingUV']:
    try:
        addon.resolve_uv(obj, name)
        raise AssertionError('Invalid UV must be rejected')
    except ValueError:
        pass
    assert not addon.checker_modifiers(obj)
settings.uv_map = ''

# No-material meshes work, and deselection cannot strand a preview.
empty = bpy.data.objects.new('NoMaterial', obj.data.copy())
bpy.context.collection.objects.link(empty)
empty.data.materials.clear()
empty.select_set(True)
assert bpy.ops.wgame_uv.checker() == {'FINISHED'}
assert len(empty.data.materials) == 0
obj.select_set(False)
empty.select_set(False)
assert bpy.ops.wgame_uv.checker(action='CLEAR') == {'FINISHED'}
assert not addon.checker_modifiers(obj) and not addon.checker_modifiers(empty)
assert len(empty.data.materials) == 0
print('PASS: validation; no material slots; clear unselected previews')

area = next(a for a in bpy.context.screen.areas if a.type == 'VIEW_3D')
area.type = 'IMAGE_EDITOR'
area.ui_type = 'UV'
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.mode_set(mode='EDIT')
settings.uv_map = 'DetailUV'
with bpy.context.temp_override(area=area):
    for mode in ['ANGLE', 'AREA', 'OFF']:
        assert bpy.ops.wgame_uv.stretch(mode=mode) == {'FINISHED'}
        assert area.spaces.active.uv_editor.show_stretch == (mode != 'OFF')
        if mode != 'OFF':
            assert area.spaces.active.uv_editor.display_stretch_type == mode
assert obj.data.uv_layers.active.name == 'DetailUV'
bpy.ops.object.mode_set(mode='OBJECT')
assert snapshot() == baseline
print('PASS: native angle/area/off; active UV selection; render UV preserved')

original_image = bpy.data.images.new('OriginalPalette', width=16, height=16)
area.spaces.active.image = original_image
with bpy.context.temp_override(area=area):
    assert bpy.ops.wgame_uv.checker() == {'FINISHED'}
    image = area.spaces.active.image
    group = addon.checker_modifiers(obj)[0].node_group
    material = group.nodes['CheckerMaterial'].inputs['Material'].default_value
    assert next(n.image for n in material.node_tree.nodes if n.type == 'TEX_IMAGE') == image
    assert image.get(addon.TAG)
    assert tuple(image.pixels[0:3]) != tuple(image.pixels[32:35])
    settings.cells = 16
    assert bpy.ops.wgame_uv.checker(action='UPDATE') == {'FINISHED'}
    assert area.spaces.active.image != image
    assert bpy.ops.wgame_uv.checker() == {'FINISHED'}
    assert area.spaces.active.image == original_image
    bpy.ops.wgame_uv.checker()
    manual_image = bpy.data.images.new('UserChosenImage', width=8, height=8)
    area.spaces.active.image = manual_image
    bpy.ops.wgame_uv.checker(action='CLEAR')
    assert area.spaces.active.image == manual_image
print('PASS: shared checker image; pattern update; original image restoration; manual choice preserved')

# Save/reopen keeps enough state to remove the preview without an in-memory backup.
settings.uv_map = ''
assert bpy.ops.wgame_uv.checker() == {'FINISHED'}
temp = pathlib.Path(bpy.app.tempdir) / 'wgame_uv_roundtrip.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(temp))
bpy.ops.wm.open_mainfile(filepath=str(temp))
assert bpy.ops.wgame_uv.checker(action='CLEAR') == {'FINISHED'}
obj = bpy.data.objects['UV_Test']
assert not addon.checker_modifiers(obj)
assert [m.name for m in obj.data.materials] == ['OriginalShared', 'SecondMaterial']
addon.unregister()
addon.register()
addon.unregister()
print('PASS: save/reopen restoration; register/unregister cycle')
print('ALL UV TOOL CHECKS PASSED')
