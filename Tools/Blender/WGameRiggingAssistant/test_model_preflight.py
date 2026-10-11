"""Run with Blender --background --factory-startup --python-exit-code 17 --python <this file>."""
import json
import ast
import inspect
from pathlib import Path
import sys

import bpy
from mathutils import Matrix

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import WGameRiggingAssistant as addon


def create(name, vertices=None, faces=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices or [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0.5, 0.5, 0)], [],
                     faces if faces is not None else [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)])
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def cancelled(operator, **kwargs):
    try:
        result = operator(**kwargs)
    except RuntimeError:
        result = {"CANCELLED"}
    assert result == {"CANCELLED"}, result


def main():
    addon.register()
    icons = bpy.types.UILayout.bl_rna.functions["operator"].parameters["icon"].enum_items.keys()
    for node in ast.walk(ast.parse(inspect.getsource(addon._draw_model_review))):
        if isinstance(node, ast.keyword) and node.arg == "icon" and isinstance(node.value, ast.Constant):
            assert node.value.value in icons, f"Unsupported Blender UI icon: {node.value.value}"
    repair = bpy.data.meshes.new("LocalRepairFixture")
    repair.from_pydata([(0, 0, 0), (0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 0)],
                      [(0, 1)], [(0, 2, 3), (0, 1, 2)])
    uv = repair.uv_layers.new(name="UV0")
    for index, loop in enumerate(uv.data):
        loop.uv = (index / 10, index / 20)
    expected_uv = [tuple(loop.uv) for loop in uv.data[:3]]
    assert addon._repair_degenerate_mesh(repair) == (1, 1)
    assert len(repair.vertices) == 4, "unconnected coincident vertex was incorrectly merged"
    assert len(repair.polygons) == 1
    assert [tuple(loop.uv) for loop in repair.uv_layers["UV0"].data] == expected_uv
    bpy.data.meshes.remove(repair)
    island = bpy.data.meshes.new("SameIslandFixture")
    island.from_pydata([(0, 0, 0), (0.00000075, 0, 0), (0.0000015, 0, 0),
                        (0, 1, 0), (1, 1, 0), (0, 0, 0)],
                       [(0, 3), (3, 1), (1, 4), (4, 2)], [])
    # A-B and B-C are near; C must not be pulled beyond tolerance into A. Vertex 5 is another island.
    assert addon._merge_same_island_vertices(island, Matrix.Identity(4), 0.000001) == (1, 0, 0)
    assert len(island.vertices) == 5
    assert sum(vertex.co.length == 0 for vertex in island.vertices) == 2, "cross-island merge"
    assert addon._merge_same_island_vertices(island, Matrix.Identity(4), 0.000001)[0] == 0
    bpy.data.meshes.remove(island)
    scaled = bpy.data.meshes.new("WorldDistanceFixture")
    scaled.from_pydata([(0, 0, 0), (0.00000075, 0, 0), (0, 1, 0)], [(0, 2), (2, 1)], [])
    assert addon._merge_same_island_vertices(scaled, Matrix.Scale(2, 4), 0.000001)[0] == 0
    bpy.data.meshes.remove(scaled)
    unsafe = bpy.data.meshes.new("NonmanifoldMergeFixture")
    unsafe.from_pydata([(0, 0, 0), (0, 0, 0), (1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1)],
                      [(0, 1)], [(0, 2, 3), (1, 4, 2), (1, 2, 5)])
    assert addon._merge_same_island_vertices(unsafe, Matrix.Identity(4), 0.000001) == (0, 0, 1)
    assert len(unsafe.vertices) == 6
    bpy.data.meshes.remove(unsafe)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    body = create("Body")
    clothing = create("Clothing")
    backup = create("Body__TOPO_BACKUP", faces=[(0, 1, 2), (0, 1, 2)])
    reference = create("Reference")
    reference["wgame_rig_exclude_from_binding"] = True
    addon._select_only(bpy.context, backup)
    bpy.context.view_layer.update()
    settings = bpy.context.scene.wgame_rigging_assistant
    report = addon._scan_model_preflight(bpy.context)
    assert {item["name"] for item in report["objects"]} == {"Body", "Clothing"}
    assert set(report["excluded"]) == {backup.name, reference.name}
    assert report["blockerCount"] == 0
    assert any(issue["key"] == "boundary" for item in report["objects"] for issue in item["issues"])
    assert {item["object"] for item in addon._scan_unrigged_components(bpy.context)} == {"Body", "Clothing"}
    bpy.ops.wgame_rigging.unrigged_preflight()
    assert settings.unrigged_preflight_passed and not settings.unrigged_pose_locked
    cancelled(bpy.ops.wgame_rigging.lock_bind_pose)
    for key in ("unrigged_topology_reviewed", "unrigged_pose_reviewed", "unrigged_hands_reviewed", "unrigged_regions_reviewed"):
        setattr(settings, key, True)
    cancelled(bpy.ops.wgame_rigging.lock_bind_pose)
    settings.unrigged_review_notes = "测试开放表面；允许边界；UV/材质后续制作"
    assert bpy.ops.wgame_rigging.lock_bind_pose() == {"FINISHED"}
    assert settings.unrigged_pose_locked

    # Backups have no authority over an accepted model fingerprint.
    accepted = addon._unrigged_fingerprint(bpy.context)
    backup.data.vertices[4].co.x += 3
    backup.data.update()
    assert addon._unrigged_fingerprint(bpy.context) == accepted
    # Interior edits keep counts and bounds, but must invalidate acceptance.
    old_dimensions = tuple(body.dimensions)
    body.data.vertices[4].co.x += 0.1
    body.data.update()
    bpy.context.view_layer.update()
    assert tuple(body.dimensions) == old_dimensions
    assert addon._unrigged_fingerprint(bpy.context) != accepted
    assert not addon._current_model_preflight(bpy.context, settings)
    assert not settings.unrigged_pose_locked and not settings.unrigged_pose_reviewed

    # A same-count connectivity change must not reuse component indices.
    topology = addon._unrigged_topology_fingerprint(bpy.context)
    body.data.clear_geometry()
    body.data.from_pydata([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0.6, 0.5, 0)], [],
                          [(0, 4, 1), (1, 2, 4), (2, 3, 4), (3, 0, 4)])
    assert addon._unrigged_topology_fingerprint(bpy.context) != topology

    # Report IDs are usable for selecting actual problem geometry.
    bpy.ops.wgame_rigging.unrigged_preflight()
    bpy.ops.wgame_rigging.focus_preflight_issue(object_name="Body", issue_key="boundary")
    assert bpy.context.mode == "EDIT_MESH"
    bpy.ops.object.mode_set(mode="OBJECT")
    assert sum(edge.select for edge in body.data.edges) == 4

    # Every formal object is checked even when a different object is active.
    bad = create("Bad", vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0), (0.5, 0, 0)],
                 faces=[(0, 1, 2), (2, 1, 0), (0, 1, 3)])
    addon._select_only(bpy.context, body)
    bpy.ops.wgame_rigging.unrigged_preflight()
    assert not settings.unrigged_preflight_passed
    report = json.loads(settings.unrigged_preflight_report)
    keys = {issue["key"] for item in report["objects"] if item["name"] == "Bad" for issue in item["issues"]}
    assert {"duplicate_faces", "degenerate_faces"} <= keys
    addon._select_only(bpy.context, bad)
    bpy.ops.wgame_rigging.toggle_binding_participant()
    assert bad not in addon._formal_binding_meshes(bpy.context)
    assert not settings.unrigged_pose_locked
    bpy.ops.wgame_rigging.unrigged_preflight()
    assert settings.unrigged_preflight_passed
    print("WGame model preflight: PASS")


if __name__ == "__main__":
    main()
