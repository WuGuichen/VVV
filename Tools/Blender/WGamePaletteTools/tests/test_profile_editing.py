"""Run with Blender --background --factory-startup --python <this file>."""

import os
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace

import bpy


REPOSITORY_ROOT = Path(__file__).resolve().parents[4]
os.environ["WGAME_REPOSITORY_ROOT"] = str(REPOSITORY_ROOT)
sys.path.insert(0, str(REPOSITORY_ROOT / "Tools" / "Blender"))

import WGamePaletteTools as addon


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def make_mesh(scene, name):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0)], [], [(0, 1, 2)])
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj


class OperatorProperties:
    """Check panel assignments against Blender's registered operator RNA."""

    def __init__(self, name):
        namespace, operator = name.split(".")
        object.__setattr__(self, "_properties", getattr(getattr(bpy.ops, namespace), operator).get_rna_type().properties)

    def __setattr__(self, name, value):
        require(name in self._properties, f"Panel assigned undeclared operator property: {name}")


class Layout:
    def __init__(self):
        self.operators = []

    def operator(self, name, **_kwargs):
        self.operators.append(name)
        return OperatorProperties(name)

    def __getattr__(self, _name):
        return lambda *_args, **_kwargs: self


def main():
    with tempfile.TemporaryDirectory(prefix="wgame-profile-test-") as directory:
        original_cache_root = addon._cache_root
        addon._cache_root = lambda: Path(directory)
        addon.register()
        try:
            addon.load_catalog(bpy.context, prefer_server=False)
            scene = bpy.context.scene
            for obj in list(scene.objects):
                bpy.data.objects.remove(obj, do_unlink=True)
            scene.wgame_character_color_profile_colors.clear()
            for index in range(3):
                addon._set_scene_color(scene, 100 + index)
                scene.wgame_character_color_profile_new_label = f"Category {index}"
                require(bpy.ops.wgame.character_color_profile_add() == {"FINISHED"}, "create categories")

            selected = make_mesh(scene, "Selected")
            hidden = make_mesh(scene, "HiddenUnselected")
            unrelated = make_mesh(scene, "Unclassified")
            other_scene = bpy.data.scenes.new("IndependentProfile")
            foreign = make_mesh(other_scene, "OtherScene")
            for obj in (selected, hidden, foreign):
                addon._apply_color_profile_to_polygons(obj, [0], 101, 0, 1)
            selected.select_set(True)
            bpy.context.view_layer.objects.active = selected
            hidden.hide_set(True)
            bpy.ops.object.mode_set(mode="EDIT")
            require(addon.remap_color_profile_category(bpy.context, 1, 400, 5) == (2, 2), "remap hidden and unselected category references")
            require(selected.mode == "EDIT", "remap restores Edit Mode")
            bpy.ops.object.mode_set(mode="OBJECT")
            for obj in (selected, hidden):
                require(obj.data.attributes["PaletteToken"].data[0].value == addon.logical_token(400, 5), "all classified faces receive new color and surface")
            require(foreign.data.attributes["PaletteToken"].data[0].value == addon.logical_token(101, 0), "other Scene remains unchanged")
            require(unrelated.data.attributes.get("PaletteToken") is None, "unclassified object remains unchanged")

            # Selection-only painting must not expand along with category updates.
            addon.apply_color_profile_to_selection(bpy.context, 500, 6, 1)
            require(hidden.data.attributes["PaletteToken"].data[0].value == addon.logical_token(400, 5), "painting still respects selection")
            selected.select_set(False)
            bpy.context.view_layer.objects.active = None
            require(addon.count_color_profile_category_faces(bpy.context, 1) == 2, "reference count does not depend on selection")
            scene.wgame_character_color_profile_active = 1
            try:
                result = bpy.ops.wgame.character_color_profile_remove()
                require(result == {"CANCELLED"}, "referenced deletion must be rejected")
            except RuntimeError as error:
                require("2" in str(error), "deletion reports both remaining references")
            require(len(scene.wgame_character_color_profile_colors) == 3, "failed deletion preserves Profile")

            # Deleting an unused category leaves stable IDs different from list positions.
            scene.wgame_character_color_profile_active = 0
            require(bpy.ops.wgame.character_color_profile_remove() == {"FINISHED"}, "unused category can be deleted")
            require([item.category_index for item in scene.wgame_character_color_profile_colors] == [1, 2], "remaining stable IDs are preserved")
            brush = SimpleNamespace(
                _target=selected,
                _face_under_mouse=lambda *_args: (0, None, None),
                _update_highlight=lambda *_args: None,
                report=lambda *_args: None,
            )
            require(addon.WG_OT_PaletteBrush._sample_at_mouse(brush, bpy.context, None), "sample classified face")
            require(scene.wgame_character_color_profile_active == 0, "eyedropper resolves sparse category ID to list position")
            require(addon._active_profile_item(scene).category_index == brush._category_index == 1, "UI and brush agree on sampled category")
            require(scene.wgame_palette_selected_color == brush._color_index == 500, "sampled color is preserved")
            require(int(scene.wgame_palette_selected_profile) == brush._surface_profile == 6, "sampled surface is preserved")

            scene.wgame_character_color_profile_expanded = True
            layout = Layout()
            addon.WG_PT_ColorPalette.draw(SimpleNamespace(layout=layout), bpy.context)
            require("wgame.character_color_profile_remove" in layout.operators, "expanded panel includes deletion")
            require("wgame.color_palette_brush" in layout.operators and "wgame.color_palette_validate" in layout.operators, "panel reaches paint and validation controls")
            print("WG_PROFILE_EDITING_TEST_OK sceneScope sparseCategoryEyedropper expandedPanel")
        finally:
            addon.unregister()
            addon._cache_root = original_cache_root


if __name__ == "__main__":
    main()
