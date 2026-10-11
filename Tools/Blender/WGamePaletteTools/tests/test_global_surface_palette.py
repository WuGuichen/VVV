import json
import os
from pathlib import Path
import sys

import bpy


REPOSITORY_ROOT = Path(__file__).resolve().parents[4]
os.environ["WGAME_REPOSITORY_ROOT"] = str(REPOSITORY_ROOT)
sys.path.insert(0, str(REPOSITORY_ROOT / "Tools" / "Blender"))

import WGamePaletteTools as addon


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    addon.register()
    try:
        catalog = addon.load_catalog(bpy.context, prefer_server=False)
        require(catalog["format"] == "mx.wgameSurfacePaletteCatalog.v3", "RGB565 catalog v3")
        require(catalog["width"] == 256 and catalog["height"] == 256 and catalog["colorCount"] == 65536, "complete color atlas")
        require(len(catalog["surfaceProfiles"]) == 16, "16 surface profiles")
        require(addon.WG_PT_ColorPalette.bl_label == "WGame 角色画板", "Blender panel is Chinese-first")
        require(addon.bl_info["version"] >= (3, 8, 0), "palette tools expose the current per-stroke undo version")
        require(hasattr(addon, "WG_OT_PaletteBrush"), "viewport modal palette brush is registered")
        require(all(any("\u4e00" <= char <= "\u9fff" for char in item[1]) for item in addon.PROFILE_ITEMS), "all Surface Profile names have Chinese labels")
        require(addon.DETAIL_MODE_LABELS["woven"] == "编织纹理" and addon.DETAIL_MODE_LABELS["frost"] == "磨砂细节", "procedural detail summaries are localized")
        require(catalog["colorData"]["uniqueRgb8Count"] == 65536 and catalog["colorData"]["aliasCount"] == 0, "all addresses unique")
        require(len({addon.rgb8_from_color_index(index) for index in range(65536)}) == 65536, "RGB565 expansion has no duplicates")
        require(not bpy.context.scene.wgame_palette_browser_expanded, "technical RGB565 browser is collapsed by default")
        require(not bpy.context.scene.wgame_palette_usage_expanded, "usage paths are collapsed by default")
        require(not bpy.context.scene.wgame_palette_migration_expanded, "dangerous migration is collapsed by default")
        for index in (0, 1, 352, 54321, 65535):
            require(addon.color_index_from_rgb565(*addon.rgb565_components(index)) == index, "RGB565 bidirectional formula")
        require(len(bpy.context.window_manager.wgame_palette_colors) <= addon.PAGE_SIZE, "only visible page has UI items")
        bpy.context.scene.wgame_palette_color_preview = (0.91, 0.75, 0.66)
        picked = bpy.context.scene.wgame_palette_selected_color
        actual_picker = bpy.context.scene.wgame_palette_color_preview
        expected_from_input = addon.color_index_from_rgb8(round(0.91 * 255.0), round(0.75 * 255.0), round(0.66 * 255.0))
        expected_picker_index = addon.color_index_from_rgb8(*[round(float(value) * 255.0) for value in actual_picker])
        require(picked == expected_from_input, "technical list rebuild must not replace the color-picker selection")
        require(picked == expected_picker_index, "color picker maps Blender display color to nearest RGB565 address")
        require(addon.color_hex(picked) == "#{:02X}{:02X}{:02X}".format(*addon.rgb8_from_color_index(picked)), "picker exposes final exact RGB565-expanded Hex")
        bpy.context.scene.wgame_character_color_profile_new_label = "Skin Base"
        require(bpy.ops.wgame.character_color_profile_add() == {"FINISHED"}, "add current picker color to Character Color Profile")
        require(len(bpy.context.scene.wgame_character_color_profile_colors) == 1, "profile owns a curated color list")
        profile_item = bpy.context.scene.wgame_character_color_profile_colors[0]
        require(profile_item.slot_id == "skin_base" and profile_item.category_index == 0 and profile_item.color_index == picked, "profile color has stable category and global color index")

        temp_root = REPOSITORY_ROOT / "Temp" / "Codex" / "SurfacePaletteBlenderTest"
        profile_path = temp_root / "character_color_profile.json"
        bpy.context.scene.wgame_character_color_profile_id = "test-character"
        bpy.context.scene.wgame_character_color_profile_name = "Test Character Colors"
        bpy.context.scene.wgame_character_color_profile_path = str(profile_path)
        saved_profile = addon.save_character_color_profile(bpy.context.scene)
        saved_document = json.loads(saved_profile.read_text(encoding="utf-8"))
        require(saved_document["format"] == "mx.characterColorProfile.v1", "versioned Character Color Profile format")
        require(saved_document["catalogHash"] == catalog["catalogHash"], "Character Color Profile binds the current global catalog")
        require(saved_document["colors"][0]["categoryIndex"] == 0 and saved_document["colors"][0]["surfaceProfile"] == 0, "Profile serializes authoring category and Surface Profile")
        bpy.context.scene.wgame_character_color_profile_colors.clear()
        addon.load_character_color_profile(bpy.context.scene)
        require(len(bpy.context.scene.wgame_character_color_profile_colors) == 1, "Character Color Profile round trip")

        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        mesh = bpy.data.meshes.new("ColorPaletteProfileContractMesh")
        mesh.from_pydata(
            [(0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (1.0, 0.0, 1.0), (0.0, 0.0, 1.0)],
            [],
            [(0, 1, 2), (0, 2, 3)],
        )
        mesh.update()
        obj = bpy.data.objects.new("ColorPaletteProfileContractObject", mesh)
        bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action="DESELECT")
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.context.view_layer.update()
        for polygon in mesh.polygons:
            polygon.select = True

        color_index = 54321
        surface_profile = 15
        token = addon.logical_token(color_index, surface_profile)
        addon._set_scene_color(bpy.context.scene, color_index)
        require(bpy.ops.wgame.character_color_profile_update() == {"FINISHED"}, "update selected Character Color Profile entry from picker")
        require(bpy.context.scene.wgame_character_color_profile_colors[0].color_index == color_index, "Profile update keeps exact global color index")
        bpy.context.scene.wgame_palette_selected_profile = str(surface_profile)
        require(bpy.ops.wgame.color_palette_apply() == {"FINISHED"}, "paint operator uses the selected Character Color Profile entry")
        objects, faces = addon.apply_color_profile_to_selection(bpy.context, color_index, surface_profile, 0)
        require(objects == 1 and faces == 2, "paint selected faces")
        token_attr = mesh.attributes["PaletteToken"]
        profile_attr = mesh.attributes["SurfaceProfile"]
        category_attr = mesh.attributes["ColorProfileSlot"]
        palette_uv = mesh.uv_layers["PaletteUV"]
        palette_data = mesh.color_attributes["PaletteData"]
        require(all(item.value == token for item in token_attr.data), "PaletteToken stores logical token above 4095")
        require(all(item.value == surface_profile for item in profile_attr.data), "SurfaceProfile stores 0..15")
        require(all(item.value == 0 for item in category_attr.data), "ColorProfileSlot preserves authoring category")
        require(mesh.attributes.get("MaterialClass") is None, "legacy MaterialClass is absent")
        x, y = addon.color_coordinates(color_index)
        expected_uv = ((x + 0.5) / 256.0, (y + 0.5) / 256.0)
        require(
            all(abs(item.uv.x - expected_uv[0]) < 1.0e-7 and abs(item.uv.y - expected_uv[1]) < 1.0e-7 for item in palette_uv.data),
            "full PaletteUV.xy pixel centers",
        )
        expected_alpha = catalog["surfaceProfiles"][surface_profile]["alphaByte"] / 255.0
        require(all(abs(item.color[3] - expected_alpha) < 1.0e-7 for item in palette_data.data), "PaletteData.A profile center")
        brush_color = 12345
        brush_profile = 6
        require(
            addon.apply_color_profile_to_face(obj, 0, brush_color, brush_profile, 0) == 1,
            "viewport brush writes exactly one raycast face",
        )
        require(
            token_attr.data[0].value == addon.logical_token(brush_color, brush_profile)
            and token_attr.data[1].value == token,
            "single-face brush does not modify neighboring faces",
        )
        sampled_color, sampled_profile, sampled_category, sampled_in_profile = addon.color_profile_from_face(
            bpy.context,
            obj,
            0,
        )
        require(
            sampled_color == brush_color
            and sampled_profile == brush_profile
            and sampled_category == 0
            and sampled_in_profile,
            "viewport eyedropper restores Color, Surface Profile, and category from the highlighted face",
        )
        stroke_one_color = 23456
        stroke_two_color = 34567
        if not bpy.app.background:
            require(bpy.ops.ed.undo_push(message="WGame brush undo baseline") == {"FINISHED"}, "initialize UI undo history")
        require(
            bpy.ops.wgame.color_palette_brush_stroke(
                object_name=obj.name,
                polygon_indices="0",
                color_index=stroke_one_color,
                surface_profile=4,
                category_index=0,
            ) == {"FINISHED"},
            "first brush stroke commits as an undoable operator",
        )
        if not bpy.app.background:
            require(bpy.ops.ed.undo_push(message="WGame brush stroke 1") == {"FINISHED"}, "push first brush stroke")
        require(
            bpy.ops.wgame.color_palette_brush_stroke(
                object_name=obj.name,
                polygon_indices="1",
                color_index=stroke_two_color,
                surface_profile=6,
                category_index=0,
            ) == {"FINISHED"},
            "second brush stroke commits independently",
        )
        if not bpy.app.background:
            require(bpy.ops.ed.undo_push(message="WGame brush stroke 2") == {"FINISHED"}, "push second brush stroke")
        if not bpy.app.background:
            require(bpy.ops.ed.undo() == {"FINISHED"}, "undo latest brush stroke")
            obj = bpy.data.objects["ColorPaletteProfileContractObject"]
            mesh = obj.data
            token_attr = mesh.attributes["PaletteToken"]
            require(
                token_attr.data[0].value == addon.logical_token(stroke_one_color, 4)
                and token_attr.data[1].value == token,
                "one undo removes only the latest stroke",
            )
            require(bpy.ops.ed.redo() == {"FINISHED"}, "redo latest brush stroke")
        obj = bpy.data.objects["ColorPaletteProfileContractObject"]
        mesh = obj.data
        token_attr = mesh.attributes["PaletteToken"]
        require(
            token_attr.data[0].value == addon.logical_token(stroke_one_color, 4)
            and token_attr.data[1].value == addon.logical_token(stroke_two_color, 6),
            "redo restores the latest stroke",
        )
        for polygon in mesh.polygons:
            polygon.select = True
        objects, faces = addon.apply_color_profile_to_selection(bpy.context, color_index, surface_profile, 0)
        require(objects == 1 and faces == 2, "bulk paint restores both selected faces after brush test")
        remap_objects, remap_faces = addon.remap_color_profile_category(bpy.context, 0, color_index, surface_profile)
        require(remap_objects == 1 and remap_faces == 2, "editing a Profile category remaps every classified face")
        try:
            bpy.ops.wgame.character_color_profile_remove()
            raise AssertionError("referenced Profile category removal should fail")
        except RuntimeError as error:
            require("仍被 2 个面引用" in str(error), "referenced Profile categories cannot be removed")
        bpy.context.scene.wgame_character_color_profile_colors.clear()
        bpy.context.scene.wgame_character_color_profile_active = -1
        require(bpy.ops.wgame.character_color_profile_import_used() == {"FINISHED"}, "seed Character Color Profile from colors already painted on the model")
        require(len(bpy.context.scene.wgame_character_color_profile_colors) == 1 and bpy.context.scene.wgame_character_color_profile_colors[0].color_index == color_index, "used-color import preserves the global color token")
        require(bpy.ops.wgame.character_color_profile_read_face() == {"FINISHED"}, "read Color and Surface Profile from selected face")
        require(bpy.context.scene.wgame_palette_selected_color == color_index and bpy.context.scene.wgame_palette_selected_profile == str(surface_profile), "selected face restores paint controls")
        issues, counts, invalid = addon.validate_meshes(bpy.context)
        require(not issues and invalid == 0 and counts[token] == 2, "mesh RGB565 validation")
        bpy.ops.object.mode_set(mode="EDIT")
        edit_issues, edit_counts, edit_invalid = addon.validate_meshes(bpy.context)
        require(
            not edit_issues
            and edit_invalid == 0
            and edit_counts[token] == 2
            and obj.mode == "EDIT",
            "validation synchronizes Edit Mode mesh attributes and restores the user's mode",
        )
        bpy.ops.object.mode_set(mode="OBJECT")

        material = addon.build_preview_material(bpy.context)
        require(material.get("surfaceProfileSource") == "PaletteData.A", "preview profile authority")
        preview_image = bpy.data.images["WGameRgb565LookupAtlas_Technical"]
        for preview_index in (0, color_index, 65535):
            expected_rgb = addon.rgb8_from_color_index(preview_index)
            offset = preview_index * 4
            actual_rgb = [float(preview_image.pixels[offset + channel]) for channel in range(3)]
            require(all(abs(actual_rgb[channel] - expected_rgb[channel] / 255.0) <= 1.0 / 255.0 for channel in range(3)), "preview Image contains current RGB565 pixels")
        roughness = material.node_tree.nodes.get("Surface Profile Roughness")
        metallic = material.node_tree.nodes.get("Surface Profile Metallic")
        require(roughness and metallic and len(roughness.color_ramp.elements) == 16, "preview has generated 16-profile ramps")

        bpy.context.scene.wgame_face_detail_texture_path = str(REPOSITORY_ROOT / "ArtSource" / "Common" / "Textures" / "wgame_character_face_detail" / "FacePartAtlas" / "Output" / "Tiles" / "Slot_00.png")
        face_material = addon.build_preview_material(bpy.context)
        require(face_material.node_tree.nodes.get("Character FaceDetail RGBA") is not None, "Blender preview directly binds one character-owned 512 RGBA FaceDetail texture")
        require(tuple(face_material.node_tree.nodes.get("Character FaceDetail RGBA").image.size[:]) == (512, 512), "Blender FaceDetail preview requires 512x512")

        package_root = temp_root / "package"
        model_path = package_root / "resources" / "models" / "test.fbx"
        model_path.parent.mkdir(parents=True, exist_ok=True)
        (package_root / "manifest.json").write_text('{"packageId":"palette-v2-test"}\n', encoding="utf-8")
        model_path.write_bytes(b"palette-v2-model-hash-fixture")
        bpy.context.scene.wgame_palette_package_root = str(package_root)
        bpy.context.scene.wgame_palette_exported_model_path = str(model_path)
        usage_path, usage_issues, usage_invalid = addon.write_usage_sidecar(bpy.context)
        usage = json.loads(usage_path.read_text(encoding="utf-8"))
        require(not usage_issues and usage_invalid == 0, "usage validation")
        require(usage["format"] == "mx.characterPaletteUsage.v2" and usage["tokenUsages"][0]["logicalToken"] == token, "usage v2 logical token")
        require(usage["tokenUsages"][0]["colorIndex"] == color_index and usage["tokenUsages"][0]["surfaceProfileId"] == "crystal_frosted", "usage v2 composite fields")
        manifest_after = json.loads((package_root / "manifest.json").read_text(encoding="utf-8"))
        require("config/character_palette_usage.json" in manifest_after.get("configFiles", []), "usage sidecar registered in manifest configFiles")

        blend_path = temp_root / "PaletteMigrationInput.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
        backup, report, invalid_faces = addon.migrate_selected_to_global(bpy.context)
        require(backup.is_file() and report.is_file(), "timestamped migration backup and report")
        require(invalid_faces == 2, "migration invalidates every triangle")
        require(all(item.value == -1 for item in mesh.attributes["PaletteToken"].data), "migration writes PaletteToken -1")
        require(all(item.value == -1 for item in mesh.attributes["SurfaceProfile"].data), "migration writes SurfaceProfile -1")
        require(all(item.value == -1 for item in mesh.attributes["ColorProfileSlot"].data), "migration writes ColorProfileSlot -1")
        require(all(abs(item.uv.x + 1.0) < 1.0e-7 and abs(item.uv.y + 1.0) < 1.0e-7 for item in mesh.uv_layers["PaletteUV"].data), "migration writes invalid PaletteUV")
        require(all(abs(item.color[3]) < 1.0e-7 for item in mesh.color_attributes["PaletteData"].data), "migration writes temporary alpha zero")
        require(bool(obj.get("paletteRepaintRequired")), "migration marks repaint required")
        print(
            "WG_RGB565_PALETTE_V3_TEST_OK",
            f"uniqueColors={catalog['colorData']['uniqueRgb8Count']}",
            f"profiles={catalog['surfaceProfileCount']}",
            f"logicalToken={token}",
            f"invalidAfterMigration={invalid_faces}",
        )
    finally:
        addon.unregister()


if __name__ == "__main__":
    main()
