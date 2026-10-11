"""Blender regression: stale component IDs must never reach weight writes."""

import importlib.util
import json
from pathlib import Path
from types import SimpleNamespace

import bmesh
import bpy


def main():
    spec = importlib.util.spec_from_file_location("base_repair_test", Path(__file__).with_name("__init__.py"))
    addon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(addon)
    addon.register()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mesh = bpy.data.meshes.new("BodyData")
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0), (2, 2, 2)], [], [(0, 1, 2)])
    obj = bpy.data.objects.new("Body", mesh)
    bpy.context.collection.objects.link(obj)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.vertex_groups.new(name="PreservedWeight").add([0, 1, 2], 0.75, "REPLACE")
    settings = bpy.context.scene.wgame_rigging_assistant
    records = [{"object": obj.name, "vertex_indices": [0, 1, 2, 3], "vertices": 4, "binding_scope": "BASE_SKIN"}]
    settings.unrigged_components_json = json.dumps(records)
    addon._require_current_base_repair_components(bpy.context, settings)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[3]], context="VERTS")
    bmesh.update_edit_mesh(mesh)

    reports = []
    operator = SimpleNamespace(report=lambda levels, message: reports.append(message))
    original_rig_lookup = addon._wgame_rig_object
    addon._wgame_rig_object = lambda context: object()
    # The invalid topology must stop execution before any rig-dependent validation.
    try:
        result = addon.WGAME_RIGGING_OT_repair_base_validation_failures.execute(operator, bpy.context)
    finally:
        addon._wgame_rig_object = original_rig_lookup
    assert result == {"CANCELLED"}, result
    assert obj.mode == "OBJECT"
    assert len(mesh.vertices) == 3
    assert all(text in reports[-1] for text in ("Body", "当前顶点数 3", "[3]", "修复组件记录")), reports
    assert all(abs(vertex.groups[0].weight - 0.75) < 1e-6 for vertex in mesh.vertices)
    assert settings.unrigged_components_json == json.dumps(records)
    assert not settings.wgame_base_skin_complete
    # Direct planner calls receive the same descriptive failure, not IndexError.
    try:
        addon._build_zero_weight_base_sync_plan(bpy.context, settings, None)
    except RuntimeError as error:
        assert "无效编号" in str(error)
    else:
        raise AssertionError("Direct planner accepted stale indices")
    records[0]["vertex_indices"] = [0, 1, 2]
    records[0]["vertices"] = 3
    settings.unrigged_components_json = json.dumps(records)
    addon._require_current_base_repair_components(bpy.context, settings)
    settings.unrigged_component_scan_fingerprint = "stale-topology"
    try:
        addon._require_current_base_repair_components(bpy.context, settings)
    except RuntimeError as error:
        assert "拓扑不一致" in str(error)
    else:
        raise AssertionError("Stale topology fingerprint passed")
    addon.unregister()
    print("WGame base repair stale components: PASS (edit-mode deletion, no weight writes, explicit diagnostic, topology fingerprint)")


if __name__ == "__main__":
    main()
