"""Run in an isolated Blender: --background --factory-startup --python-exit-code 17 --python <this file>."""

import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile

import bpy


ROOT = Path(__file__).resolve().parents[3]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    folder = Path(__file__).resolve().parent
    exporter = load("export_test", folder / "export_unity_fbx.py")
    auditor = load("audit_test", folder / "audit_unity_fbx.py")
    rigging = load("rigging_test", folder / "__init__.py")
    rigging.register()
    os.environ["WGAME_REPOSITORY_ROOT"] = str(ROOT)
    sys.path.insert(0, str(ROOT / "Tools" / "Blender"))
    import WGamePaletteTools as palette
    palette.register()
    palette.load_catalog(bpy.context, prefer_server=False)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)

    rig = bpy.data.objects.new("Rig", bpy.data.armatures.new("RigData"))
    bpy.context.scene.collection.objects.link(rig)
    rig["wgame_rig_kind"] = "deform_skeleton"
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    names = ("pelvis.x", "spine_01.x", "spine_02.x", "upper_chest.x", "neck.x", "head.x")
    parent = None
    for index, name in enumerate(names):
        bone = rig.data.edit_bones.new(name)
        bone.head = (0, 0, index * 0.2)
        bone.tail = (0, 0, (index + 1) * 0.2)
        bone.parent = parent
        parent = bone
    bpy.ops.object.mode_set(mode="OBJECT")

    meshes = []
    for index, name in enumerate(("Body", "Clothes")):
        mesh = bpy.data.meshes.new(name + "Data")
        mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 0, 1)], [], [(0, 1, 2)])
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.location.x = index * 2
        obj.modifiers.new("Armature", "ARMATURE").object = rig
        for vertex_id, bone_name in enumerate(names[index * 3:index * 3 + 3]):
            obj.vertex_groups.new(name=bone_name).add([vertex_id], 1, "REPLACE")
        obj.vertex_groups.new(name=exporter.CUSTOM_BIND_MASK_GROUP).add([0], 1, "REPLACE")
        token, profile, uv, colors = palette._ensure_mesh_attributes(mesh)
        token.data[0].value = 0
        profile.data[0].value = 0
        alpha = palette.STATE.catalog["surfaceProfiles"][0]["alphaByte"] / 255.0
        for loop in mesh.loops:
            uv.data[loop.index].uv = (0.5 / 256, 0.5 / 256)
            colors.data[loop.index].color = (1, 1, 1, alpha)
        meshes.append(obj)
    backup = meshes[1].copy()
    backup.data = meshes[1].data.copy()
    backup.name = "Clothes__TOPO_BACKUP"
    bpy.context.scene.collection.objects.link(backup)
    settings = bpy.context.scene.wgame_rigging_assistant
    settings.unrigged_components_json = json.dumps([
        {"object": obj.name, "id": obj.name + ":0", "vertices": 3} for obj in meshes
    ])
    rigging._select_publish_meshes(bpy.context, settings, rig)
    issues, counts, invalid = palette.validate_meshes(bpy.context)
    assert not issues and invalid == 0 and counts == {0: 2}, (issues, counts, invalid)

    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        fbx = root / "character.fbx"
        audit_path = root / "audit.json"
        sys.argv = ["export", "--", str(fbx)]
        exporter.main()
        # Sidecar generation must use all authoring meshes, excluding the backup and rig.
        rigging._select_publish_meshes(bpy.context, settings, rig)
        (root / "manifest.json").write_text('{"packageId":"000004"}', encoding="utf-8")
        bpy.context.scene.wgame_palette_package_root = str(root)
        bpy.context.scene.wgame_palette_exported_model_path = str(fbx)
        usage_path, issues, invalid = palette.write_usage_sidecar(bpy.context)
        usage = json.loads(usage_path.read_text(encoding="utf-8"))
        assert not issues and not invalid and usage["tokenUsages"][0]["faceCount"] == 2

        sys.argv = ["audit", "--", str(fbx), str(audit_path), "000004", "--require-palette"]
        auditor.main()
        report = json.loads(audit_path.read_text(encoding="utf-8"))
        assert report["status"] == "Passed", report
        assert report["roundTripImport"]["meshes"] == 2
        assert report["roundTripImport"]["vertices"] == 6
        assert report["roundTripImport"]["triangles"] == 2
        assert all(count == 1 for count in report["roundTripImport"]["torsoWeightCoverage"].values())
        assert {item["mesh"] for item in report["meshReports"]} == {"Body", "Clothes"}
        for obj in bpy.context.scene.objects:
            if obj.type == "MESH":
                assert obj.vertex_groups.get(exporter.CUSTOM_BIND_MASK_GROUP) is None
                assert obj.data.uv_layers.get("PaletteUV") is not None

        # Explicit requirement catches total channel loss, even without any Palette markers.
        stripped = root / "stripped.fbx"
        for obj in bpy.context.scene.objects:
            if obj.type == "MESH":
                for layer in list(obj.data.uv_layers):
                    obj.data.uv_layers.remove(layer)
                for layer in list(obj.data.color_attributes):
                    obj.data.color_attributes.remove(layer)
        bpy.ops.export_scene.fbx(filepath=str(stripped), bake_anim=False, add_leaf_bones=False)
        blocked_audit = root / "blocked_audit.json"
        sys.argv = ["audit", "--", str(stripped), str(blocked_audit), "000004", "--require-palette"]
        try:
            auditor.main()
        except RuntimeError as error:
            assert "FBX 回读检查" in str(error) and "缺少 PaletteUV" in str(error) and "缺少 RGBA PaletteData" in str(error), str(error)
        else:
            raise AssertionError("FBX with all Palette channels stripped passed")
        assert not blocked_audit.exists()

        # A failure on the second mesh is not hidden by a valid first mesh.
        clothes = bpy.context.scene.objects["Clothes"]
        imported_rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
        for group in clothes.vertex_groups:
            group.remove([0])
        try:
            auditor._audit_mesh(clothes, imported_rig)
        except RuntimeError as error:
            assert "weight audit failed" in str(error)
        else:
            raise AssertionError("zero weight on the second mesh passed the audit")
    palette.unregister()
    rigging.unregister()
    print("WGame multi-mesh FBX and Palette Usage: PASS")


if __name__ == "__main__":
    main()
