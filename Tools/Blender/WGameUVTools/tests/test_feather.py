import pathlib
import sys
import bpy
import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
try:
    addon.unregister()
except Exception:
    pass
addon.register()

from WGameUVTools import feather, canvas_editor, decals

# 1. Test algorithmic properties of feather_alpha and bleed_rgb
grid = np.zeros((100, 100), dtype=np.float32)
y, x = np.ogrid[:100, :100]
mask = ((x - 50) ** 2 + (y - 50) ** 2 <= 25 ** 2).astype(np.float32)

# CENTER mode
f_center = feather.feather_alpha(mask, mask > 0.5, radius=6, mode='CENTER')
assert f_center[50, 50] > 0.99, "Center must remain solid"
assert 0.1 < f_center[50, 75] < 0.9, "Boundary must be smooth transition"
assert f_center[50, 95] == 0.0, "Far outside must be 0"

# INSIDE mode (must NOT leak outside original mask)
f_inside = feather.feather_alpha(mask, mask > 0.5, radius=6, mode='INSIDE')
assert np.all(f_inside[mask <= 0.5] == 0.0), "INSIDE mode must strictly never spill outside mask"
assert f_inside[50, 50] > 0.99, "Center must remain solid"
assert f_inside[50, 74] < 0.5, "Inner boundary should fade down"

# OUTSIDE mode (must NOT shrink solid interior)
f_outside = feather.feather_alpha(mask, mask > 0.5, radius=6, mode='OUTSIDE')
assert np.all(f_outside[mask > 0.5] >= 0.99), "OUTSIDE mode must preserve solid interior"
assert np.any(f_outside[mask <= 0.5] > 0.0), "OUTSIDE mode must fade outward"

# 2. Test RGB bleeding (anti-halo)
rgba = np.zeros((64, 64, 4), dtype=np.float32)
rgba[20:44, 20:44, :3] = [1.0, 0.2, 0.4] # pink
rgba[20:44, 20:44, 3] = 1.0

f_rgba = feather.feather_rgba(rgba, radius=4, mode='CENTER')
# Check newly semi-transparent pixels have bled color, not (0,0,0)
fringe = (f_rgba[:, :, 3] > 0.01) & (rgba[:, :, 3] == 0.0)
assert np.any(fringe), "Should have newly feathered fringe pixels"
assert np.all(f_rgba[fringe, 0] > 0.5), "Fringe RGB must bleed pink R channel"
assert np.all(f_rgba[fringe, 1] > 0.1), "Fringe RGB must bleed pink G channel"

# 3. Test operator on Image in scene/editor
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
test_img = bpy.data.images.new("TestFeatherImg", width=32, height=32, alpha=True)
base_pixels = np.zeros((32, 32, 4), dtype=np.float32)
base_pixels[10:22, 10:22] = [1.0, 0.0, 0.0, 1.0] # green box
decals.write_pixels(test_img, base_pixels)

bpy.context.scene.wgame_uv_tools.decal_target = test_img
res = bpy.ops.wgame_uv.feather_islands(radius=3, mode='CENTER')
assert res == {'FINISHED'}, "Operator should succeed on decal_target"

new_pixels = decals.pixels(test_img)
assert np.any((new_pixels[:, :, 3] > 0.01) & (new_pixels[:, :, 3] < 0.99)), "Image should have feathered pixels"

# Test undo via canvas_editor
canvas_editor.undo()
undone_pixels = decals.pixels(test_img)
assert np.array_equal(undone_pixels, base_pixels), "Undo should restore original pixels"

# 3b. Test flood_fill_island and feather_single_island on multiple islands
multi_pixels = np.zeros((64, 64, 4), dtype=np.float32)
multi_pixels[10:30, 10:30] = [1.0, 0.2, 0.2, 1.0] # Island A (left)
multi_pixels[10:30, 40:60] = [0.2, 0.4, 1.0, 1.0] # Island B (right)

island_a_mask = feather.flood_fill_island(multi_pixels[:, :, 3], 20, 20)
assert np.sum(island_a_mask) == 20 * 20, "Should detect Island A"
assert not np.any(island_a_mask[10:30, 40:60]), "Island A mask must not touch Island B"

feathered_single, _ = feather.feather_single_island(multi_pixels, island_a_mask, radius=4, mode='CENTER')
assert np.array_equal(feathered_single[10:30, 40:60], multi_pixels[10:30, 40:60]), "Island B must remain completely untouched"
assert np.any((feathered_single[6:34, 6:34, 3] > 0.01) & (feathered_single[6:34, 6:34, 3] < 0.99)), "Island A must be feathered"

# 4. Test operator on Canvas Editor floating patch
bpy.ops.mesh.primitive_plane_add()
obj = bpy.context.object
obj.data.uv_layers.active.name = 'DetailUV'
mat = bpy.data.materials.new('Mat')
mat.use_nodes = True
tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = test_img
uv_node = mat.node_tree.nodes.new('ShaderNodeUVMap')
uv_node.uv_map = 'DetailUV'
mat.node_tree.links.new(uv_node.outputs['UV'], tex.inputs['Vector'])
obj.data.materials.append(mat)

source_img = bpy.data.images.new("TestSourceImg", width=8, height=8, alpha=True)
src_pixels = np.zeros((8, 8, 4), dtype=np.float32)
src_pixels[2:6, 2:6] = [0.9, 0.1, 0.1, 1.0] # red patch
decals.write_pixels(source_img, src_pixels)

area = next(a for a in bpy.context.screen.areas if a.type == 'VIEW_3D')
area.type = 'IMAGE_EDITOR'
area.spaces.active.image = test_img

with bpy.context.temp_override(area=area):
    session = canvas_editor.begin(bpy.context, 'IMPORT', source_img)
    assert session['phase'] == 'FLOAT'
    before_patch = session['patch'].copy()
    res = bpy.ops.wgame_uv.feather_islands(radius=2, mode='CENTER')
    assert res == {'FINISHED'}, "Operator should succeed on floating patch"
    after_patch = session['patch']
    assert not np.array_equal(before_patch, after_patch), "Floating patch should be feathered"
    canvas_editor.cancel()

    # Test selecting island via PICK action
    session = canvas_editor.begin(bpy.context, 'PICK')
    canvas_editor.select_island(np.array([16.0, 16.0]))
    assert session['phase'] == 'FLOAT'
    assert session['patch'] is not None
    res = bpy.ops.wgame_uv.feather_islands(radius=2, mode='CENTER')
    assert res == {'FINISHED'}, "Operator should succeed on picked island"
    canvas_editor.commit()
area.type = 'VIEW_3D'

print("ALL FEATHER TESTS PASSED SUCCESSFULLY!")
