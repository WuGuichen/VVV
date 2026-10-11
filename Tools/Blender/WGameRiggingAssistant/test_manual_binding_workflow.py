"""Blender 5.2 background validation for the WGame manual binding workflow.

Run with:
  blender --background --factory-startup --python test_manual_binding_workflow.py
"""

import importlib.util
import inspect
import json
import pathlib
import sys
import tempfile

import bpy
from mathutils import Vector


ADDON_PATH = pathlib.Path(__file__).with_name("__init__.py")


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def load_addon():
    spec = importlib.util.spec_from_file_location("wgame_rigging_assistant_test", ADDON_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.register()
    return module


def create_mesh(name, components, x_offset=0.0):
    vertices = []
    faces = []
    for component_index, z in enumerate(components):
        start = len(vertices)
        x = x_offset + component_index * 0.35
        vertices.extend(
            [
                (x - 0.08, -0.08, z),
                (x + 0.08, -0.08, z),
                (x + 0.08, 0.08, z),
                (x - 0.08, 0.08, z),
            ]
        )
        faces.append((start, start + 1, start + 2, start + 3))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def create_rig(addon):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for candidate in bpy.context.view_layer.objects:
        candidate.select_set(False)
    armature = bpy.data.armatures.new("rig")
    rig = bpy.data.objects.new("rig", armature)
    bpy.context.scene.collection.objects.link(rig)
    rig["wgame_rig_kind"] = "deform_skeleton"
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    specifications = (
        ("root.x", (0.0, 0.0, 0.0), (0.0, 0.0, 0.2), None, False),
        ("pelvis.x", (0.0, 0.0, 0.2), (0.0, 0.0, 0.9), "root.x", True),
        ("upper_chest.x", (0.0, 0.0, 0.72), (0.0, 0.0, 0.90), "pelvis.x", True),
        ("shoulder.l", (0.05, 0.0, 0.85), (0.20, 0.0, 0.85), "upper_chest.x", True),
        ("upper_arm.l", (0.20, 0.0, 0.85), (0.50, 0.0, 0.80), "shoulder.l", True),
        ("forearm.l", (0.50, 0.0, 0.80), (0.75, 0.0, 0.76), "upper_arm.l", True),
        ("hand.l", (0.75, 0.0, 0.76), (0.82, 0.0, 0.75), "forearm.l", True),
        ("index_01.l", (0.82, -0.01, 0.75), (0.85, -0.01, 0.745), "hand.l", True),
        ("index_02.l", (0.85, -0.01, 0.745), (0.875, -0.01, 0.74), "index_01.l", True),
        ("index_03.l", (0.875, -0.01, 0.74), (0.90, -0.01, 0.735), "index_02.l", True),
        ("shoulder.r", (-0.05, 0.0, 0.85), (-0.20, 0.0, 0.85), "upper_chest.x", True),
        ("upper_arm.r", (-0.20, 0.0, 0.85), (-0.50, 0.0, 0.80), "shoulder.r", True),
        ("forearm.r", (-0.50, 0.0, 0.80), (-0.75, 0.0, 0.76), "upper_arm.r", True),
        ("hand.r", (-0.75, 0.0, 0.76), (-0.82, 0.0, 0.75), "forearm.r", True),
        ("thigh.l", (0.10, 0.0, 0.55), (0.10, 0.0, 0.30), "pelvis.x", True),
        ("lower_leg.l", (0.10, 0.0, 0.30), (0.10, 0.0, 0.05), "thigh.l", True),
        ("foot.l", (0.10, 0.0, 0.05), (0.10, -0.08, 0.02), "lower_leg.l", True),
        ("toes.l", (0.10, -0.08, 0.02), (0.10, -0.15, 0.015), "foot.l", True),
        ("thigh.r", (-0.10, 0.0, 0.55), (-0.10, 0.0, 0.30), "pelvis.x", True),
        ("lower_leg.r", (-0.10, 0.0, 0.30), (-0.10, 0.0, 0.05), "thigh.r", True),
        ("foot.r", (-0.10, 0.0, 0.05), (-0.10, -0.08, 0.02), "lower_leg.r", True),
        ("toes.r", (-0.10, -0.08, 0.02), (-0.10, -0.15, 0.015), "foot.r", True),
        ("head.x", (0.0, 0.0, 0.9), (0.0, 0.0, 1.6), "upper_chest.x", True),
    )
    for name, head, tail, _parent, use_deform in specifications:
        bone = armature.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.use_deform = use_deform
    for name, _head, _tail, parent, _use_deform in specifications:
        if parent:
            armature.edit_bones[name].parent = armature.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    addon._capture_base_skeleton_snapshot(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for name, x in (
        ("chain_manual", 0.35),
        ("chain_empty", 0.70),
        ("chain_extra_1", 1.05),
        ("chain_extra_2", 1.40),
        ("chain_extra_3", 1.75),
    ):
        bone = armature.edit_bones.new(name)
        bone.head = (x, 0.0, 0.8)
        bone.tail = (x, 0.0, 1.1)
        bone.parent = armature.edit_bones["pelvis.x"]
        bone.use_deform = True
    for side, x in (("l", 0.08), ("r", -0.08)):
        root_name = f"breast_test_01.{side}"
        child_name = f"breast_test_02.{side}"
        root = armature.edit_bones.new(root_name)
        root.head = (x, 0.0, 0.81)
        root.tail = (x, -0.05, 0.79)
        root.parent = armature.edit_bones["upper_chest.x"]
        root.use_deform = True
        child = armature.edit_bones.new(child_name)
        child.head = root.tail
        child.tail = (x, -0.09, 0.77)
        child.parent = root
        child.use_connect = True
        child.use_deform = True
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def weight(mesh_obj, group_name, vertex_id):
    group = mesh_obj.vertex_groups.get(group_name)
    if not group:
        return 0.0
    return next(
        (
            assignment.weight
            for assignment in mesh_obj.data.vertices[vertex_id].groups
            if assignment.group == group.index
        ),
        0.0,
    )


def deform_weights(mesh_obj, rig_obj, vertex_id):
    deform_names = {bone.name for bone in rig_obj.data.bones if bone.use_deform}
    group_names = {group.index: group.name for group in mesh_obj.vertex_groups}
    return {
        group_names[item.group]: round(float(item.weight), 8)
        for item in mesh_obj.data.vertices[vertex_id].groups
        if item.weight > 1e-7 and group_names.get(item.group) in deform_names
    }


def create_custom_auto_weight_mesh():
    vertices = [
        (0.35, -0.03, 0.65), (0.35, 0.03, 0.65),
        (0.35, -0.03, 0.80), (0.35, 0.03, 0.80),
        (0.35, -0.03, 0.95), (0.35, 0.03, 0.95),
        (0.35, -0.03, 1.10), (0.35, 0.03, 1.10),
        (0.50, -0.03, 0.92), (0.55, -0.03, 0.92),
        (0.55, 0.03, 0.98), (0.50, 0.03, 0.98),
        (3.00, -0.03, 0.92), (3.05, -0.03, 0.92),
        (3.05, 0.03, 0.98), (3.00, 0.03, 0.98),
    ]
    faces = [
        (0, 1, 3, 2),
        (2, 3, 5, 4),
        (4, 5, 7, 6),
        (8, 9, 10, 11),
        (12, 13, 14, 15),
    ]
    mesh = bpy.data.meshes.new("CustomAutoWeightMeshData")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("CustomAutoWeightMesh", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def run_custom_auto_weight_tests(addon):
    settings = bpy.context.scene.wgame_rigging_assistant
    mesh_obj = create_custom_auto_weight_mesh()
    rig_obj = create_rig(addon)
    settings.rig_name = rig_obj.name
    settings.mesh_name = mesh_obj.name
    settings.wgame_skeleton_accepted = True
    settings.wgame_base_skin_complete = True
    rig_obj["wgame_skeleton_accepted"] = True
    modifier = mesh_obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig_obj
    components = addon._scan_unrigged_components(bpy.context)
    settings.unrigged_components_json = json.dumps(components, separators=(",", ":"))
    settings.unrigged_component_scan_fingerprint = addon._unrigged_topology_fingerprint(bpy.context)
    settings.unrigged_binding_groups_json = "[]"
    addon._ensure_custom_mask_groups(bpy.context, settings)
    addon._set_custom_mask_vertices(mesh_obj, set(range(2, 16)), True)
    addon._sync_component_scopes_from_mask(bpy.context, settings, finalized=True)

    pelvis = mesh_obj.vertex_groups.new(name="pelvis.x")
    pelvis.add([0, 1], 1.0, "REPLACE")
    unmatched_existing = mesh_obj.vertex_groups.new(name="head.x")
    unmatched_existing.add([12, 13, 14, 15], 0.6, "REPLACE")
    author_mask = mesh_obj.vertex_groups.new(name="author_mask")
    author_mask.add(range(len(mesh_obj.data.vertices)), 0.25, "REPLACE")

    payload = addon._discover_custom_weight_chains(bpy.context, settings, rig_obj)
    roots = {chain["root_bone"]: chain for chain in payload["chains"]}
    require("chain_manual" in roots and "chain_empty" in roots, "custom chain discovery missed single-bone roots")
    require(
        roots["breast_test_01.l"]["bones"] == ["breast_test_01.l", "breast_test_02.l"],
        "custom chain discovery did not preserve parent-before-child order",
    )
    roots["chain_manual"]["enabled"] = False
    roots["chain_manual"]["influence_radius"] = 0.41
    roots["chain_manual"]["blend_steps_override"] = 3
    payload["chains"] = list(roots.values())
    addon._save_custom_weight_chain_payload(rig_obj, payload)
    bpy.context.view_layer.objects.active = rig_obj
    rig_obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    probe = rig_obj.data.edit_bones.new("chain_structure_probe")
    probe.head = (2.0, 0.0, 0.8)
    probe.tail = (2.0, 0.0, 1.0)
    probe.parent = rig_obj.data.edit_bones["pelvis.x"]
    probe.use_deform = True
    bpy.ops.object.mode_set(mode="OBJECT")
    require(addon._custom_weight_chain_status(rig_obj)["stale"], "custom bone edit did not stale the chain record")
    refreshed = addon._discover_custom_weight_chains(bpy.context, settings, rig_obj)
    refreshed_manual = next(chain for chain in refreshed["chains"] if chain["root_bone"] == "chain_manual")
    require(
        not refreshed_manual["enabled"]
        and abs(refreshed_manual["influence_radius"] - 0.41) < 1e-8
        and refreshed_manual["blend_steps_override"] == 3,
        "explicit chain refresh did not preserve same-root settings",
    )
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.object.mode_set(mode="EDIT")
    rig_obj.data.edit_bones.remove(rig_obj.data.edit_bones["chain_structure_probe"])
    bpy.ops.object.mode_set(mode="OBJECT")
    require(addon._custom_weight_chain_status(rig_obj)["stale"], "custom bone removal did not stale the chain record")
    payload = addon._discover_custom_weight_chains(bpy.context, settings, rig_obj)
    for chain in payload["chains"]:
        chain["enabled"] = chain["root_bone"] in {"chain_manual", "chain_empty"}
        if chain["enabled"]:
            chain["influence_radius"] = 0.25
            chain["blend_steps_override"] = 2 if chain["root_bone"] == "chain_manual" else 4
    payload["global_blend_steps"] = 2
    addon._save_custom_weight_chain_payload(rig_obj, payload)
    addon._load_current_custom_chain_settings(settings, bpy.context, payload)

    local_probe_chains = []
    for root_name in ("chain_manual", "chain_empty", "chain_extra_1"):
        source_chain = next(chain for chain in payload["chains"] if chain["root_bone"] == root_name)
        probe_chain = dict(source_chain)
        probe_chain["enabled"] = True
        probe_chain["influence_radius"] = 0.5
        local_probe_chains.append(probe_chain)
    local_chain_ids = set()
    for point in (Vector((0.35, 0.0, 0.90)), Vector((0.70, 0.0, 0.90)), Vector((1.05, 0.0, 0.90))):
        local_matches, _nearest = addon._match_custom_vertex_chains(point, rig_obj, local_probe_chains)
        require(1 <= len(local_matches) <= 2, "per-vertex local chain matching exceeded its two-chain contract")
        local_chain_ids.update(match["chain"]["root_bone"] for match in local_matches)
    require(
        local_chain_ids == {"chain_manual", "chain_empty", "chain_extra_1"},
        f"one connected region could not share more than two chains across local vertices: {sorted(local_chain_ids)}",
    )

    for share, expected in ((0.2, (3, 1)), (0.5, (2, 2)), (0.8, (1, 3))):
        combined = addon._combine_base_custom_distribution(
            {f"base_{index}": 1.0 / index for index in range(1, 5)},
            {f"custom_{index}": 1.0 / index for index in range(1, 5)},
            share,
        )
        actual = (
            sum(name.startswith("base_") for name in combined),
            sum(name.startswith("custom_") for name in combined),
        )
        require(actual == expected and len(combined) == 4, f"dynamic four-bone slots were wrong at share {share}: {combined}")
        require(abs(sum(combined.values()) - 1.0) < 1e-7, "dynamic Base/Custom merge was not normalized")
    settings.wgame_custom_chain_boundary_custom_share = 0.65
    expected_connection_shares = (0.65, 0.7046875, 0.825, 0.9453125, 1.0)
    actual_connection_shares = tuple(
        addon._custom_connection_share(settings, depth, 4)
        for depth in range(5)
    )
    require(
        all(abs(actual - expected) < 1e-7 for actual, expected in zip(actual_connection_shares, expected_connection_shares)),
        f"Custom-dominant boundary curve was wrong: {actual_connection_shares}",
    )

    before_base = [deform_weights(mesh_obj, rig_obj, vertex_id) for vertex_id in (0, 1)]
    before_unmatched = [deform_weights(mesh_obj, rig_obj, vertex_id) for vertex_id in range(12, 16)]
    before_author = [weight(mesh_obj, "author_mask", vertex_id) for vertex_id in range(16)]
    plan, report = addon._build_custom_auto_weight_plan(bpy.context, settings, rig_obj, payload)
    require(report["matched_subislands"] == 2 and report["unmatched_subislands"] == 1, f"unexpected sub-island matching: {report}")
    require(report["attached_subislands"] == 1 and report["detached_subislands"] == 1, "attached/detached classification was wrong")
    require(report["dual_chain_subislands"] == 1, "equidistant detached island did not share two chains")
    require(report["shared_vertices"] > 0, "connected Custom island did not create a Base/Custom sharing band")
    require(not any(vertex_id >= 12 for _name, vertex_id, _weights in plan), "unmatched Custom island entered the rebuild plan")
    addon._apply_deform_weight_plan(bpy.context, rig_obj, plan)

    require(before_base == [deform_weights(mesh_obj, rig_obj, vertex_id) for vertex_id in (0, 1)], "auto Custom generation modified Base weights")
    require(before_unmatched == [deform_weights(mesh_obj, rig_obj, vertex_id) for vertex_id in range(12, 16)], "auto Custom generation modified an unmatched island")
    require(before_author == [weight(mesh_obj, "author_mask", vertex_id) for vertex_id in range(16)], "auto Custom generation modified a non-deform group")
    for vertex_id in range(2, 12):
        assignments = deform_weights(mesh_obj, rig_obj, vertex_id)
        require(assignments and len(assignments) <= 4 and abs(sum(assignments.values()) - 1.0) < 1e-6, f"generated vertex {vertex_id} violated weight integrity: {assignments}")
    for vertex_id in (2, 3):
        assignments = deform_weights(mesh_obj, rig_obj, vertex_id)
        require("pelvis.x" in assignments and "chain_manual" in assignments, "connection boundary did not retain both Base and Custom")
        require(
            assignments["chain_manual"] >= 0.65 - 1e-6
            and assignments["pelvis.x"] <= 0.35 + 1e-6,
            f"connection boundary did not prioritize Custom weight: {assignments}",
        )
    for vertex_id in (6, 7):
        require(weight(mesh_obj, "pelvis.x", vertex_id) == 0.0, "blend propagation exceeded its configured topology width")
    for vertex_id in range(8, 12):
        assignments = deform_weights(mesh_obj, rig_obj, vertex_id)
        require(set(assignments) == {"chain_manual", "chain_empty"}, f"dual-chain detached island used unrelated bones: {assignments}")

    first_result = [deform_weights(mesh_obj, rig_obj, vertex_id) for vertex_id in range(2, 12)]
    repeat_plan, repeat_report = addon._build_custom_auto_weight_plan(bpy.context, settings, rig_obj, payload)
    addon._apply_deform_weight_plan(bpy.context, rig_obj, repeat_plan)
    require(first_result == [deform_weights(mesh_obj, rig_obj, vertex_id) for vertex_id in range(2, 12)], "custom auto-weight rebuild was not deterministic")
    require(report == repeat_report, "custom auto-weight report was not deterministic")
    rigid_island = next(
        island
        for island in addon._custom_weight_subislands(bpy.context, settings)
        if island["vertex_set"] == {8, 9, 10, 11}
    )
    addon._store_rigid_attachment(
        bpy.context,
        settings,
        rig_obj,
        rigid_island,
        "chain_manual",
        "test",
    )
    rigid_plan, rigid_report = addon._build_custom_auto_weight_plan(bpy.context, settings, rig_obj, payload)
    addon._apply_deform_weight_plan(bpy.context, rig_obj, rigid_plan)
    require(
        rigid_report["rigid_subislands"] == 1 and rigid_report["rigid_vertices"] == 4,
        f"rigid attachment was not reported: {rigid_report}",
    )
    for vertex_id in range(8, 12):
        require(
            deform_weights(mesh_obj, rig_obj, vertex_id) == {"chain_manual": 1.0},
            f"rigid attachment vertex was not assigned to one bone at 1.0: {deform_weights(mesh_obj, rig_obj, vertex_id)}",
        )
    rigid_stats = addon._rigid_attachment_validation_stats(bpy.context, rig_obj)
    require(
        rigid_stats == {"records": 1, "vertices": 4, "invalid_records": 0, "mismatches": 0},
        f"rigid attachment validation failed: {rigid_stats}",
    )
    # Rigid failures expose exact vertex, expected bone, actual weights and mask loss.
    mesh_obj.vertex_groups["chain_manual"].add([8], 0.5, "REPLACE")
    mesh_obj.vertex_groups[addon.RIGID_BIND_MASK_GROUP].remove([9])
    details = []
    broken = addon._rigid_attachment_validation_stats(bpy.context, rig_obj, details)
    require(broken["mismatches"] == 2, str(broken))
    require({item["vertex"] for item in details} == {8, 9}, str(details))
    require(all(item["object"] == mesh_obj.name and item["bone"] == "chain_manual" for item in details), str(details))
    require("chain_manual=0.5" in details[0]["actual"] and "不是1" in details[0]["reason"], str(details))
    require("蒙版" in details[1]["reason"], str(details))
    before_focus = [deform_weights(mesh_obj, rig_obj, index) for index in range(len(mesh_obj.data.vertices))]
    result = bpy.ops.wgame_rigging.focus_rigid_weight_errors(object_name=mesh_obj.name, target_bone="chain_manual")
    require(result == {"FINISHED"}, "rigid error focus failed")
    selected_errors = {vertex.index for vertex in __import__("bmesh").from_edit_mesh(mesh_obj.data).verts if vertex.select}
    require(selected_errors == {8, 9}, str(selected_errors))
    bpy.ops.object.mode_set(mode="OBJECT")
    require(before_focus == [deform_weights(mesh_obj, rig_obj, index) for index in range(len(mesh_obj.data.vertices))], "error focus changed weights")
    preserved_group = mesh_obj.vertex_groups.new(name="repair_preserved_non_deform")
    preserved_group.add([8, 9], 0.37, "REPLACE")
    repaired, skipped = addon._repair_rigid_weight_errors(bpy.context, rig_obj)
    require(repaired == 2 and not skipped, (repaired, skipped))
    require(addon._rigid_attachment_validation_stats(bpy.context, rig_obj)["mismatches"] == 0, "repair did not clear errors")
    require(all(abs(preserved_group.weight(index) - 0.37) < 1e-6 for index in (8, 9)), "repair changed non-deform group")
    require(all(before_focus[index] == deform_weights(mesh_obj, rig_obj, index) for index in range(len(mesh_obj.data.vertices)) if index not in (8, 9)), "repair changed healthy vertices")
    repaired, skipped = addon._repair_rigid_weight_errors(bpy.context, rig_obj)
    require(repaired == 0 and not skipped, "repeat repair was not a no-op")
    original_records = rig_obj[addon.RIGID_BIND_ATTACHMENTS_KEY]
    conflicting = json.loads(original_records)
    conflicting["attachments"].append({"object": mesh_obj.name, "vertex_indices": [8], "target_bone": "chain_empty"})
    conflicting["attachments"].append({"object": "missing_mesh", "vertex_indices": [0], "target_bone": "chain_manual"})
    rig_obj[addon.RIGID_BIND_ATTACHMENTS_KEY] = json.dumps(conflicting)
    repaired, skipped = addon._repair_rigid_weight_errors(bpy.context, rig_obj)
    require(repaired == 0 and any("冲突" in item for item in skipped) and any("missing_mesh" in item for item in skipped), (repaired, skipped))
    require(deform_weights(mesh_obj, rig_obj, 8) == {"chain_manual": 1.0}, "conflicting record changed weights")
    rig_obj[addon.RIGID_BIND_ATTACHMENTS_KEY] = original_records
    isolated = addon._set_rigid_vertex_isolation(bpy.context, settings, True)
    require(isolated == 4 and mesh_obj.mode == "EDIT", "rigid-mask isolation did not enter Edit Mode")
    rigid_bmesh = __import__("bmesh").from_edit_mesh(mesh_obj.data)
    require(
        {vertex.index for vertex in rigid_bmesh.verts if not vertex.hide} == {8, 9, 10, 11}
        and {vertex.index for vertex in rigid_bmesh.verts if vertex.select} == {8, 9, 10, 11},
        "rigid-mask isolation did not exclusively show and select every rigid vertex",
    )
    restored = addon._set_rigid_vertex_isolation(bpy.context, settings, False)
    require(restored == 4 and all(not vertex.hide for vertex in mesh_obj.data.vertices), "rigid isolation did not restore mesh visibility")
    settings.wgame_base_skin_complete = True
    result = bpy.ops.wgame_rigging.generate_custom_initial_weights()
    require(result == {"FINISHED"}, "3A custom initial-weight operator did not finish")
    operator_report = json.loads(settings.wgame_custom_auto_weight_report)
    require(
        operator_report["status"] == "GENERATED"
        and operator_report["matched_vertices"] == 10
        and operator_report["unmatched_vertices"] == 4,
        f"3A operator did not persist its structured report: {operator_report}",
    )
    require(not settings.wgame_custom_binding_complete, "3A generation incorrectly declared 3C final acceptance")
    payload = addon._custom_weight_chain_payload(rig_obj)
    manual_index = next(index for index, chain in enumerate(payload["chains"]) if chain["root_bone"] == "chain_manual")
    settings.wgame_custom_chain_index = manual_index
    addon._load_current_custom_chain_settings(settings, bpy.context, payload)
    settings.wgame_weight_browser_bone = "chain_manual"
    result = bpy.ops.wgame_rigging.preview_custom_chain_weight(step=0)
    require(result == {"FINISHED"}, "current custom-chain weight preview did not start")
    require(
        settings.wgame_custom_chain_weight_preview_active
        and bpy.context.object == mesh_obj
        and mesh_obj.mode == "WEIGHT_PAINT"
        and settings.wgame_weight_browser_bone == "chain_manual",
        "custom-chain preview did not activate the expected mesh, mode, and bone",
    )
    require(
        mesh_obj.data.use_paint_mask_vertex
        and not any(vertex.select for vertex in mesh_obj.data.vertices),
        "custom-chain preview did not lock brush edits behind an empty vertex mask",
    )
    require("骨链预览 chain_manual" in settings.wgame_weight_browser_report, "custom-chain preview did not expose its weight summary")
    bpy.ops.wgame_rigging.exit_weight_browser()
    require(not settings.wgame_custom_chain_weight_preview_active, "exiting weight browser left chain auto-preview active")
    payload = addon._custom_weight_chain_payload(rig_obj)
    empty_index = next(index for index, chain in enumerate(payload["chains"]) if chain["root_bone"] == "chain_extra_1")
    settings.wgame_custom_chain_index = empty_index
    addon._load_current_custom_chain_settings(settings, bpy.context, payload)
    require(mesh_obj.vertex_groups.get("chain_extra_1") is None, "zero-weight preview fixture unexpectedly had a vertex group")
    result = bpy.ops.wgame_rigging.preview_custom_chain_weight(step=0)
    require(result == {"FINISHED"}, "zero-weight custom bone could not enter location preview")
    require(
        settings.wgame_custom_chain_weight_preview_active
        and rig_obj.mode == "POSE"
        and rig_obj.data.bones.active == rig_obj.data.bones["chain_extra_1"]
        and rig_obj.pose.bones["chain_extra_1"].select,
        "zero-weight preview did not reveal and select the requested pose bone",
    )
    require(
        mesh_obj.vertex_groups.get("chain_extra_1") is None
        and "当前没有任何非零权重" in settings.wgame_weight_browser_report
        and "未创建顶点组" in settings.wgame_weight_browser_report,
        "zero-weight preview created authoring data or failed to report its state",
    )
    bpy.ops.wgame_rigging.exit_weight_browser()

    payload = addon._custom_weight_chain_payload(rig_obj)
    rename_plan = addon._canonical_custom_chain_plan(rig_obj, payload)
    require(
        rename_plan.get("chain_manual") == "custom_pelvis_01_01.x"
        and rename_plan.get("breast_test_01.l") == "breast_01.l"
        and rename_plan.get("breast_test_02.r") == "breast_02.r",
        f"canonical generic or breast naming plan was wrong: {rename_plan}",
    )
    before_manual_weights = [weight(mesh_obj, "chain_manual", vertex_id) for vertex_id in range(len(mesh_obj.data.vertices))]
    payload, applied_renames = addon._canonicalize_custom_weight_chains(
        bpy.context,
        settings,
        rig_obj,
        payload,
    )
    require(applied_renames == rename_plan, "canonical chain rename did not apply its complete plan")
    require(
        rig_obj.data.bones.get("custom_pelvis_01_01.x") is not None
        and mesh_obj.vertex_groups.get("chain_manual") is None
        and before_manual_weights == [weight(mesh_obj, "custom_pelvis_01_01.x", vertex_id) for vertex_id in range(len(mesh_obj.data.vertices))],
        "canonical bone rename did not synchronize its deform vertex group",
    )
    left_breast = next(chain for chain in payload["chains"] if chain.get("semantic_type") == "BREAST_L")
    original_breast_head = rig_obj.data.bones[left_breast["root_bone"]].head_local.copy()
    original_breast_tail = rig_obj.data.bones[left_breast["bones"][-1]].tail_local.copy()
    settings.wgame_custom_chain_index = payload["chains"].index(left_breast)
    addon._load_current_custom_chain_settings(settings, bpy.context, payload)
    settings.wgame_custom_chain_target_count = 3
    result = bpy.ops.wgame_rigging.resize_custom_weight_chain()
    require(result == {"FINISHED"}, "mirrored breast-chain resize did not finish")
    require(
        all(rig_obj.data.bones.get(f"breast_{segment:02d}.{side}") for side in ("l", "r") for segment in range(1, 4)),
        "breast-chain resize did not synchronize dedicated left/right names",
    )
    require(
        (rig_obj.data.bones["breast_01.l"].head_local - original_breast_head).length < 1e-7
        and (rig_obj.data.bones["breast_03.l"].tail_local - original_breast_tail).length < 1e-7,
        "breast-chain resampling did not preserve the complete chain endpoints",
    )
    selected_edit_bones = {bone.name for bone in rig_obj.data.edit_bones if bone.select}
    require(
        rig_obj.mode == "EDIT"
        and settings.wgame_custom_bone_edit_active
        and selected_edit_bones == {"breast_01.l", "breast_02.l", "breast_03.l"}
        and rig_obj.data.edit_bones.active == rig_obj.data.edit_bones["breast_01.l"],
        f"resize did not return to Edit Mode with the adjusted chain selected: {selected_edit_bones}",
    )
    resized_payload = addon._custom_weight_chain_payload(rig_obj)
    right_breast_index = next(
        index
        for index, chain in enumerate(resized_payload["chains"])
        if chain.get("semantic_type") == "BREAST_R"
    )
    rig_obj.data.edit_bones["breast_02.r"].select = True
    rig_obj.data.edit_bones.active = rig_obj.data.edit_bones["breast_02.r"]
    require(
        addon._sync_custom_chain_from_active_edit_bone(
            bpy.context,
            settings,
            rig_obj,
            resized_payload,
        )
        and settings.wgame_custom_chain_index == right_breast_index,
        "selecting a custom Edit Bone did not synchronize the current 3A chain",
    )


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    addon = load_addon()
    diagnostic = addon._authoring_response_failure(
        "Authoring → Unity 文件同步",
        {
            "success": False,
            "exitCode": 9,
            "stderr": "compiler detail\nERROR: generated resource is invalid",
            "reportOut": "Temp/diagnostic-report",
            "report": {"status": "Blocked", "issues": [{"code": "TEST001", "message": "test blocker"}]},
        },
    )
    require(
        diagnostic.splitlines()[0] == "Authoring → Unity 文件同步失败：test blocker"
        and "进程退出码：9" in diagnostic
        and "问题[TEST001]：test blocker" in diagnostic
        and "stderr：ERROR: generated resource is invalid" in diagnostic,
        "Authoring failure diagnostics did not preserve the actionable server response",
    )
    upper_chest_upgrade_source = inspect.getsource(addon.WGAME_RIGGING_OT_upgrade_upper_chest.execute)
    require("execute_base_skin" not in upper_chest_upgrade_source, "UpperChest creation must not start skinning")
    require("validate_custom_binding" not in upper_chest_upgrade_source, "UpperChest creation must not run binding acceptance")
    require(
        "settings.wgame_skeleton_accepted = False" in upper_chest_upgrade_source,
        "changing the skeleton must invalidate the previous skeleton acceptance",
    )
    publish_source = inspect.getsource(addon.WGAME_RIGGING_OT_publish_fbx_and_update_unity.execute)
    for required_publish_step in (
        "export_unity_fbx.py",
        "audit_unity_fbx.py",
        "write_usage_sidecar",
        "/api/character/import-materials/sync",
        "/api/character/import-unity?package=",
        "_ensure_authoring_server",
        "--python-exit-code",
        "staged_fbx,",
        "staged_audit,",
    ):
        require(required_publish_step in publish_source, f"one-click publish is missing {required_publish_step}")
    exporter_source = (ADDON_PATH.parent / "export_unity_fbx.py").read_text(encoding="utf-8")
    auditor_source = (ADDON_PATH.parent / "audit_unity_fbx.py").read_text(encoding="utf-8")
    require(
        "WGame.CustomBindMask" in exporter_source and "vertex_groups.remove(custom_mask)" in exporter_source,
        "FBX exporter does not exclude the authoring-only custom mask",
    )
    require("CUSTOM_BIND_MASK_AUTHORITY_KEY in mesh" in exporter_source, "FBX exporter does not strip the mask authority marker")
    require(
        "BASE_REGION_GROUP_PREFIX" in exporter_source and "BASE_REGION_MASK_AUTHORITY_KEY in mesh" in exporter_source,
        "FBX exporter does not strip per-vertex base-region authoring metadata",
    )
    require(
        "CUSTOM_WEIGHT_CHAINS_KEY in rig" in exporter_source,
        "FBX exporter does not strip custom bone-chain authoring metadata",
    )
    require(
        "RIGID_BIND_MASK_GROUP" in exporter_source and "RIGID_BIND_ATTACHMENTS_KEY in rig" in exporter_source,
        "FBX exporter does not strip rigid-attachment authoring metadata",
    )
    require(
        "__TOPO_BACKUP" in exporter_source,
        "FBX exporter does not exclude topology-backup meshes",
    )
    require(
        "CUSTOM_WEIGHT_CHAINS_KEY in rig" in auditor_source,
        "FBX round-trip audit does not reject leaked custom bone-chain metadata",
    )
    require(
        "RIGID_BIND_MASK_GROUP" in auditor_source and "RIGID_BIND_ATTACHMENTS_KEY in rig" in auditor_source,
        "FBX round-trip audit does not reject leaked rigid-attachment metadata",
    )
    custom_solver_source = inspect.getsource(addon._build_custom_auto_weight_plan)
    require(
        "_custom_weight_subislands" in custom_solver_source
        and "_match_custom_vertex_chains" in custom_solver_source
        and "_match_custom_subisland_chains" in custom_solver_source
        and "_custom_connection_share" in custom_solver_source
        and "_propagated_boundary_profile" in custom_solver_source,
        "3A auto-weight solver is not using per-vertex chains, topology islands, and boundary propagation",
    )
    custom_generate_source = inspect.getsource(addon.WGAME_RIGGING_OT_generate_custom_initial_weights.execute)
    require(
        "_save_backup_copy" in custom_generate_source
        and "_apply_deform_weight_plan" in custom_generate_source
        and "wgame_custom_binding_complete = False" in custom_generate_source,
        "3A generation lifecycle is missing backup, scoped rebuild, or final-validation invalidation",
    )
    chain_preview_source = inspect.getsource(addon.WGAME_RIGGING_OT_preview_custom_chain_weight.execute)
    chain_navigation_source = inspect.getsource(addon.WGAME_RIGGING_OT_navigate_custom_weight_chain.execute)
    require(
        "_best_weight_preview_mesh" in chain_preview_source
        and "view_weight_browser_bone" in chain_preview_source
        and "preview_custom_chain_weight" in chain_navigation_source,
        "3A chain navigation is not wired to automatic weight heatmap preview",
    )
    chain_refresh_source = inspect.getsource(addon.WGAME_RIGGING_OT_refresh_custom_weight_chains.execute)
    chain_resize_source = inspect.getsource(addon.WGAME_RIGGING_OT_resize_custom_weight_chain.execute)
    require(
        "_canonicalize_custom_weight_chains" in chain_refresh_source
        and "_resize_linear_custom_chain" in chain_resize_source
        and "_select_custom_chain_for_edit" in chain_resize_source
        and "_clear_character_cloth_for_skeleton_change" in chain_resize_source,
        "custom-chain refresh/resize is missing canonical rename, resampling, or stale cloth cleanup",
    )
    breast_weight_source = inspect.getsource(addon._apply_detected_breast_weights)
    require(
        "_limit_base_joint_weights" in breast_weight_source,
        "breast-weight post-processing does not reapply authoritative base joint limits",
    )
    require(
        "Authoring-only vertex group leaked into FBX" in auditor_source,
        "FBX round-trip audit does not reject a leaked custom mask",
    )
    monitor_source = inspect.getsource(addon.WGAME_RIGGING_OT_monitor_unity_publish.modal)
    require("/api/character/import-unity/editor-status?id=" in monitor_source, "Unity publish monitor does not poll Editor status")
    require("time.sleep" not in monitor_source, "Unity publish monitor must not block Blender's main thread")
    brush_modal_source = inspect.getsource(addon.WGAME_RIGGING_OT_brush_custom_vertices.modal)
    brush_invoke_source = inspect.getsource(addon.WGAME_RIGGING_OT_brush_custom_vertices.invoke)
    brush_undo_source = inspect.getsource(addon.WGAME_RIGGING_OT_brush_custom_vertices._undo_history)
    brush_redo_source = inspect.getsource(addon.WGAME_RIGGING_OT_brush_custom_vertices._redo_history_action)
    require(
        'event.type == "Z" and event.ctrl' in brush_modal_source
        and "_undo_history" in brush_modal_source
        and "_redo_history_action" in brush_modal_source
        and "_restore_excluded_island_at_cursor" in brush_modal_source
        and "_toggle_component_context" in brush_modal_source
        and "_restore_mask_snapshot" in brush_undo_source
        and "_restore_mask_snapshot" in brush_redo_source,
        "custom-mask brush does not handle undo/redo inside its modal session",
    )
    require("undo_push" not in brush_modal_source + brush_undo_source + brush_redo_source, "custom-mask brush still relies on ineffective modal undo pushes")
    require("UNDO" in addon.WGAME_RIGGING_OT_brush_custom_vertices.bl_options, "completed brush session is not a Blender Undo step")
    require(
        "_ensure_base_region_mask_groups" in brush_invoke_source
        and "_sync_component_base_region_cache" in brush_invoke_source,
        "shared brush does not support per-vertex base-region semantics",
    )
    component_picker_source = inspect.getsource(addon.WGAME_RIGGING_OT_pick_component_in_view.modal)
    require(
        'event.type == "H"' in component_picker_source
        and "_toggle_picker_custom_visibility" in component_picker_source,
        "component picker does not expose the custom-visibility shortcut",
    )
    require(
        "component_ids.issubset(custom_ids)" in component_picker_source
        and "_toggle_custom_component_from_view" in component_picker_source,
        "ordinary component clicks do not directly fill the custom mask",
    )
    manual_workflow_source = inspect.getsource(addon._draw_wgame_manual_binding_workflow)
    require(
        "继续调整自定义选择" in manual_workflow_source
        and "wgame_rigging.reopen_custom_selection" in manual_workflow_source,
        "existing WGame rig route does not expose scope adjustment after classification",
    )
    require(
        "wgame_manual_binding_ui_tab" in manual_workflow_source
        and 'prop_enum(settings, "wgame_manual_binding_ui_tab", "FINAL")' in manual_workflow_source
        and "验收与发布（4+）" in manual_workflow_source
        and "_sync_custom_chain_from_active_edit_bone" in manual_workflow_source
        and "_NullUILayout" in manual_workflow_source,
        "manual binding workflow is not split into Base, Custom, and Final tabs",
    )
    deformation_source = inspect.getsource(addon.WGAME_RIGGING_OT_preview_deformation.execute)
    deformation_plan_source = inspect.getsource(addon._build_deformation_preview_plan)
    require(
        len(addon.DEFORMATION_PREVIEW_ITEMS) >= 9
        and "CUSTOM_CHAINS" in deformation_plan_source
        and "missing_bones" in deformation_source
        and "applied_operations" in deformation_source,
        "final deformation acceptance does not provide comprehensive data-driven poses and coverage diagnostics",
    )
    require(
        "wgame_deformation_preview_strength" in manual_workflow_source
        and "navigate_deformation_preview" in manual_workflow_source
        and "wgame_show_preview_fine_tune" in manual_workflow_source,
        "Final tab does not expose action strength, navigation, and fine-tune controls",
    )
    region_geometry_draw_source = inspect.getsource(addon._draw_base_region_geometry)
    region_label_draw_source = inspect.getsource(addon._draw_base_region_labels)
    require(
        "_base_region_overlay_data" in region_geometry_draw_source
        and "_base_region_overlay_data" in region_label_draw_source
        and "_base_region_assignments_by_object" not in region_geometry_draw_source + region_label_draw_source,
        "base-region viewport overlay still rescans semantic vertex groups every frame",
    )
    require(
        addon._base_skin_report_segment_value("大臂根部超限 1/1467") == ("大臂根部超限", 1)
        and addon._base_skin_report_segment_value("层级过渡超限 19/9555") == ("层级过渡超限", 19)
        and addon._base_skin_report_segment_value("HEAD覆盖错误 0/5336") == ("HEAD覆盖错误", 0),
        "base-skin report failure extraction is not reliable",
    )
    manual_draw_source = inspect.getsource(addon._draw_wgame_manual_binding_workflow)
    require("_draw_base_skin_report" in manual_draw_source, "manual workflow still renders the base report as one truncated label")
    bootstrap_source = inspect.getsource(addon.WGAME_RIGGING_OT_bootstrap_current_character_source.execute)
    for required_bootstrap_step in (
        "/api/character/id-catalog",
        "/api/character/create",
        "Blender_Work",
        "save_as_mainfile",
        "_adopt_optional_source",
        "wgame_palette_package_root",
        "wgame_palette_exported_model_path",
        "/api/authoring/palette/binding",
        "character_color_profile.json",
        "MaterialClass.",
        "publish_fbx_and_update_unity",
    ):
        require(required_bootstrap_step in bootstrap_source, f"first-source bootstrap is missing {required_bootstrap_step}")
    settings = bpy.context.scene.wgame_rigging_assistant
    body = create_mesh("BodyMesh", (0.35, 0.85, 1.35))
    garment = create_mesh("GarmentMesh", (0.65,), x_offset=-0.35)
    components = addon._scan_unrigged_components(bpy.context)
    settings.unrigged_components_json = json.dumps(components, separators=(",", ":"))
    settings.unrigged_component_scan_fingerprint = addon._unrigged_topology_fingerprint(bpy.context)
    settings.unrigged_binding_groups_json = "[]"

    component_indices = {component["id"]: index for index, component in enumerate(components)}

    body.data.vertices[0].hide = True
    visibility_snapshot = addon._capture_component_brush_visibility(bpy.context, settings)
    isolated_ids = set(components[component_indices["BodyMesh:0"]]["vertex_indices"])
    addon._apply_component_brush_isolation(
        bpy.context,
        settings,
        body,
        isolated_ids,
        visibility_snapshot,
        True,
    )
    require(bpy.context.object == body and body.mode == "EDIT", "component brush isolation did not enter Edit Mode")
    require(garment.hide_get(), "component brush isolation did not hide another participant mesh")
    bpy.ops.object.mode_set(mode="OBJECT")
    require(all(body.data.vertices[index].hide for index in range(4, 12)), "component brush isolation did not hide other islands in the active mesh")
    addon._apply_component_brush_isolation(
        bpy.context,
        settings,
        body,
        isolated_ids,
        visibility_snapshot,
        False,
    )
    require(bpy.context.object == body and body.mode == "EDIT", "component brush visibility restore did not return to Edit Mode")
    require(not garment.hide_get(), "component brush visibility restore left another participant mesh hidden")
    bpy.ops.object.mode_set(mode="OBJECT")
    require(body.data.vertices[0].hide, "component brush visibility restore lost a pre-existing hidden vertex")
    require(all(not body.data.vertices[index].hide for index in range(1, 12)), "component brush visibility restore changed unrelated vertex visibility")
    body.data.vertices[0].hide = False

    legacy_components = addon._unrigged_components(settings)
    legacy_components[component_indices["BodyMesh:1"]]["binding_scope"] = "CUSTOM_BIND"
    settings.unrigged_components_json = json.dumps(legacy_components, separators=(",", ":"))
    body.vertex_groups.new(name=addon.CUSTOM_BIND_MASK_GROUP)
    legacy_ids = addon._scope_vertex_ids_by_object(settings, "CUSTOM_BIND")
    require(legacy_ids[body.name] == set(range(4, 8)), "legacy scope was hidden by an uninitialized empty custom-mask group")
    addon._ensure_custom_mask_groups(bpy.context, settings)
    require(
        addon._mesh_vertex_group_ids(body, addon.CUSTOM_BIND_MASK_GROUP) == set(range(4, 8)),
        "legacy component scope was not materialized into WGame.CustomBindMask",
    )
    require(body.get(addon.CUSTOM_BIND_MASK_AUTHORITY_KEY), "materialized custom mask was not marked authoritative")
    addon._clear_custom_mask_groups(bpy.context, settings)
    for component in legacy_components:
        component["binding_scope"] = ""
    settings.unrigged_components_json = json.dumps(legacy_components, separators=(",", ":"))

    garment_component = legacy_components[component_indices["GarmentMesh:0"]]
    trim_mesh, trim_ids, trim_changed = addon._prepare_component_trim_mask(
        bpy.context,
        settings,
        garment_component,
    )
    require(trim_mesh == garment and trim_ids == set(range(4)) and trim_changed == 4, "island trim did not begin from a full-island mask")
    addon._set_custom_mask_vertices(garment, {0, 1}, False)
    _trim_mesh, _trim_ids, trim_changed = addon._prepare_component_trim_mask(
        bpy.context,
        settings,
        garment_component,
    )
    require(trim_changed == 0, "re-entering a partially trimmed island filled it again")
    require(
        addon._mesh_vertex_group_ids(garment, addon.CUSTOM_BIND_MASK_GROUP) == {2, 3},
        "partially trimmed island mask was not preserved",
    )
    addon._clear_custom_mask_groups(bpy.context, settings)
    addon._set_custom_mask_vertices(garment, {0, 2}, True)
    require(
        addon._connected_custom_mask_region(garment, 0, set(range(4))) == {0}
        and addon._connected_custom_mask_region(garment, 2, set(range(4))) == {2},
        "blue mask gaps did not split pink custom sub-islands",
    )
    require(
        addon._connected_excluded_mask_region(garment, 1, set(range(4))) == {1}
        and addon._connected_excluded_mask_region(garment, 3, set(range(4))) == {3},
        "pink mask gaps did not split blue excluded sub-islands",
    )
    restored_blue_island = addon._connected_excluded_mask_region(garment, 1, set(range(4)))
    addon._set_custom_mask_vertices(garment, restored_blue_island, True)
    require(
        addon._mesh_vertex_group_ids(garment, addon.CUSTOM_BIND_MASK_GROUP) == {0, 1, 2},
        "restoring one blue sub-island changed another blue sub-island",
    )
    addon._set_custom_mask_vertices(garment, restored_blue_island, False)
    excluded_island = addon._connected_custom_mask_region(garment, 0, set(range(4)))
    addon._set_custom_mask_vertices(garment, excluded_island, False)
    require(
        addon._mesh_vertex_group_ids(garment, addon.CUSTOM_BIND_MASK_GROUP) == {2},
        "excluding one pink sub-island changed another pink sub-island",
    )
    addon._clear_custom_mask_groups(bpy.context, settings)

    partial_ids = {0, 1}
    require(addon._set_custom_mask_vertices(garment, partial_ids, True) == 2, "partial custom mask add failed")
    addon._sync_component_scopes_from_mask(bpy.context, settings, finalized=False)
    require(
        addon._scope_vertex_ids_by_object(settings, "CUSTOM_BIND")[garment.name] == partial_ids,
        "partial custom mask did not expose the exact vertices",
    )
    bpy.ops.wgame_rigging.finalize_binding_scopes()
    partial_component = addon._unrigged_components(settings)[component_indices["GarmentMesh:0"]]
    require(partial_component["binding_scope"] == "MIXED", "partial island was not finalized as MIXED")
    require(
        addon._scope_vertex_ids_by_object(settings, "BASE_SKIN")[garment.name] == {2, 3},
        "mixed island base complement was not exact",
    )
    addon._ensure_base_region_mask_groups(bpy.context, settings)
    require(addon._set_base_region_mask_vertices(garment, {2}, "NECK") > 0, "partial base-region mask add failed")
    require(addon._set_base_region_mask_vertices(garment, {2}, "HEAD") > 0, "base-region reassignment failed")
    addon._sync_component_base_region_cache(bpy.context, settings)
    region_assignments = addon._base_region_assignments_by_object(bpy.context, settings)[garment.name]
    require(region_assignments.get(2) == "HEAD" and 3 not in region_assignments, "partial base-region assignment was not exact")
    require(
        not addon._unrigged_components(settings)[component_indices["GarmentMesh:0"]].get("base_region"),
        "partial base-region mask was incorrectly promoted to a whole-component override",
    )
    require(
        2 not in addon._base_region_vertex_ids_by_object(bpy.context, settings, "NECK").get(garment.name, set()),
        "one vertex retained conflicting base-region assignments",
    )
    overlay_geometry = addon._base_region_overlay_data(bpy.context.scene, settings)["geometry"]
    require(
        addon._base_region_overlay_data(bpy.context.scene, settings)["geometry"] is overlay_geometry,
        "unchanged base-region overlay rebuilt its geometry cache",
    )
    addon._set_base_region_mask_vertices(garment, {2}, "AUTO")
    addon._sync_component_base_region_cache(bpy.context, settings)
    require(
        addon._base_region_overlay_data(bpy.context.scene, settings)["geometry"] is not overlay_geometry,
        "base-region edit did not invalidate the overlay cache",
    )
    bpy.ops.wgame_rigging.reopen_custom_selection()
    addon._set_custom_mask_vertices(garment, partial_ids, False)
    addon._sync_component_scopes_from_mask(bpy.context, settings, finalized=False)

    settings.unrigged_component_index = component_indices["BodyMesh:1"]
    changed, _message = addon._toggle_custom_component_from_view(bpy.context, settings, component_indices["BodyMesh:1"])
    require(changed, "3D-picker custom toggle helper failed")
    require(addon._unrigged_components(settings)[component_indices["BodyMesh:1"]]["binding_scope"] == "CUSTOM_BIND", "first custom component was not marked")
    bpy.ops.wgame_rigging.hide_marked_custom()
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    require(all(body.data.vertices[index].hide for index in range(4, 8)), "marked custom component was not hidden")
    require(body.data.polygons[1].hide, "marked custom component face remained visible")
    require(all(edge.hide for edge in body.data.edges if all(index in set(range(4, 8)) for index in edge.vertices)), "marked custom component edges remained visible")
    require(all(not body.data.vertices[index].hide for index in list(range(0, 4)) + list(range(8, 12))), "unmarked component was hidden")
    settings.unrigged_component_index = component_indices["BodyMesh:2"]
    changed, _message = addon._toggle_custom_component_from_view(bpy.context, settings, component_indices["BodyMesh:2"])
    require(changed, "3D-picker second custom toggle failed")
    require(bpy.context.object == body and bpy.context.object.mode == "EDIT", "Shift-picking a hidden custom component did not remain in Edit Mode")
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    require(all(body.data.vertices[index].hide for index in range(4, 8)), "first hidden component was revealed by Shift-picking another component")
    require(body.data.polygons[1].hide, "first hidden component face was revealed by Shift-picking another component")
    require(all(body.data.vertices[index].hide for index in range(8, 12)), "custom component marked while hidden was not hidden")
    settings.unrigged_component_index = component_indices["BodyMesh:0"]
    bpy.ops.wgame_rigging.navigate_component(step=1)
    require(settings.unrigged_component_index == component_indices["GarmentMesh:0"], "navigation did not skip hidden custom components")
    bpy.ops.wgame_rigging.show_marked_custom()
    require(all(not vertex.hide for vertex in body.data.vertices), "marked custom visibility was not restored")
    require(all(not edge.hide for edge in body.data.edges) and all(not polygon.hide for polygon in body.data.polygons), "edge or face visibility was not restored")
    hidden, hidden_count = addon._toggle_picker_custom_visibility(bpy.context, settings)
    require(hidden and hidden_count == 8, "picker H shortcut did not hide the exact custom mask")
    require(bpy.context.object is not None and bpy.context.object.mode == "EDIT", "picker H shortcut left Blender in Object Mode after hiding")
    hidden, shown_count = addon._toggle_picker_custom_visibility(bpy.context, settings)
    require(not hidden and shown_count == 8, "picker H shortcut did not restore the exact custom mask")
    require(bpy.context.object is not None and bpy.context.object.mode == "EDIT", "picker H shortcut left Blender in Object Mode after showing")
    changed, _message = addon._toggle_custom_component_from_view(bpy.context, settings, component_indices["BodyMesh:1"])
    require(changed and not addon._unrigged_components(settings)[component_indices["BodyMesh:1"]]["binding_scope"], "3D-picker custom unmark failed")
    changed, _message = addon._toggle_custom_component_from_view(bpy.context, settings, component_indices["BodyMesh:1"])
    require(changed, "3D-picker custom remark failed")
    bpy.ops.wgame_rigging.finalize_binding_scopes()
    finalized = {component["id"]: component["binding_scope"] for component in addon._unrigged_components(settings)}
    require(finalized["BodyMesh:0"] == "BASE_SKIN" and finalized["GarmentMesh:0"] == "BASE_SKIN", "unmarked components were not finalized as base skin")
    require(finalized["BodyMesh:1"] == "CUSTOM_BIND" and finalized["BodyMesh:2"] == "CUSTOM_BIND", "custom selections changed during finalization")
    bpy.ops.wgame_rigging.reopen_custom_selection()
    reopened = {component["id"]: component["binding_scope"] for component in addon._unrigged_components(settings)}
    require(not reopened["BodyMesh:0"] and not reopened["GarmentMesh:0"], "base candidates were not reopened")
    require(reopened["BodyMesh:1"] == "CUSTOM_BIND" and reopened["BodyMesh:2"] == "CUSTOM_BIND", "custom selections were not preserved when reopening")
    bpy.ops.wgame_rigging.finalize_binding_scopes()
    require(addon._FOCUS_DRAW_HANDLE is not None, "2D viewport overlay draw handler was not registered")
    require(addon._BASE_REGION_DRAW_HANDLE is not None, "3D base-region overlay draw handler was not registered")

    settings.unrigged_component_index = component_indices["GarmentMesh:0"]
    changed, _message = addon._set_component_base_region_override(
        bpy.context,
        settings,
        component_indices["GarmentMesh:0"],
        "HEAD",
    )
    require(changed, "base-region mark helper failed")
    require(
        addon._unrigged_components(settings)[component_indices["GarmentMesh:0"]].get("base_region") == "HEAD",
        "base-region mark was not persisted",
    )
    result = bpy.ops.wgame_rigging.clear_current_base_region()
    require(result == {"FINISHED"}, "current base-region clear operator did not finish")
    require(
        not addon._unrigged_components(settings)[component_indices["GarmentMesh:0"]].get("base_region"),
        "base-region mark was not removed",
    )

    rig = create_rig(addon)
    settings.rig_name = rig.name
    settings.mesh_name = body.name
    bpy.ops.wgame_rigging.reopen_custom_selection()
    require(addon._detect_character_route(bpy.context) == "WGAME_RIG", "existing rig recovery fixture did not enter WGAME_RIG route")
    require(addon._wgame_rig_scope_recovery_required(bpy.context, settings), "existing WGame rig did not expose pending scope recovery")
    bpy.ops.wgame_rigging.finalize_binding_scopes()
    require(not addon._wgame_rig_scope_recovery_required(bpy.context, settings), "scope recovery remained active after finalization")
    settings.wgame_skeleton_accepted = True
    settings.wgame_finger_rig_complete = True
    rig["wgame_skeleton_accepted"] = True

    breast_detection = addon._detect_breast_bone_chains(rig)
    require(breast_detection["valid"], "mirrored two-segment breast chains were not detected")
    require(
        breast_detection["left"] == ["breast_test_01.l", "breast_test_02.l"]
        and breast_detection["right"] == ["breast_test_01.r", "breast_test_02.r"],
        f"breast-chain side or segment ordering was wrong: {breast_detection}",
    )
    breast_items = addon._custom_weight_bone_items(settings, bpy.context)
    breast_labels = {identifier: label for identifier, label, _description in breast_items}
    require(
        breast_labels["breast_test_01.l"].startswith("女性胸部 · 左 1")
        and breast_labels["breast_test_02.r"].startswith("女性胸部 · 右 2"),
        "detected breast roles were not exposed in the custom bone selector",
    )
    body.vertex_groups.new(name="breast_test_01.l")
    settings.wgame_custom_weight_bone = "breast_test_01.l"
    settings.wgame_weight_browser_category = "CUSTOM"
    settings.wgame_weight_browser_bone = "breast_test_02.r"
    result = bpy.ops.wgame_rigging.rename_breast_bones()
    require(result == {"FINISHED"}, "automatic breast-bone rename did not finish")
    require(
        all(rig.data.bones.get(name) for name in ("breast_01.l", "breast_02.l", "breast_01.r", "breast_02.r")),
        "canonical breast bone names were not created",
    )
    require(
        body.vertex_groups.get("breast_01.l") is not None and body.vertex_groups.get("breast_test_01.l") is None,
        "same-named breast vertex group was not synchronized",
    )
    require(
        settings.wgame_custom_weight_bone == "breast_01.l"
        and settings.wgame_weight_browser_bone == "breast_02.r",
        "active breast bone selections were not synchronized",
    )
    breast_detection = addon._detect_breast_bone_chains(rig)
    require(
        breast_detection["left"] == ["breast_01.l", "breast_02.l"]
        and breast_detection["right"] == ["breast_01.r", "breast_02.r"],
        "canonical breast chains were not rediscovered after rename",
    )

    snapshot = addon._base_skeleton_snapshot(rig)
    regions = addon._base_anatomical_regions(rig, snapshot)
    authority_before, regions_before = addon._base_authority_component_ids(
        bpy.context, settings, rig, regions, 1.0,
    )
    cached_components = addon._unrigged_components(settings)
    cached_centers = [component.get("center") for component in cached_components]
    for component in cached_components:
        component["center"] = [99.0, 99.0, -99.0]
    settings.unrigged_components_json = json.dumps(cached_components, separators=(",", ":"))
    authority_after, regions_after = addon._base_authority_component_ids(
        bpy.context, settings, rig, regions, 1.0,
    )
    require(
        authority_after == authority_before and regions_after == regions_before,
        "base authority selection depended on stale cached component centers",
    )
    cached_components = addon._unrigged_components(settings)
    for component, center in zip(cached_components, cached_centers):
        component["center"] = center
    settings.unrigged_components_json = json.dumps(cached_components, separators=(",", ":"))
    inner_point = Vector((0.18, 0.0, 0.82))
    inner_candidates = addon._base_anatomical_candidate_names(
        inner_point,
        rig,
        snapshot,
        regions,
        1.0,
        forced_region="SHOULDER_L",
    )
    require("upper_arm.l" not in inner_candidates, "shoulder-inner point still admitted upper_arm.l")
    near_point = Vector((0.205, 0.0, 0.84))
    near_candidates = addon._base_anatomical_candidate_names(
        near_point,
        rig,
        snapshot,
        regions,
        1.0,
        forced_region="SHOULDER_L",
    )
    near_weights = addon._normalized_anatomical_weights(near_point, rig, near_candidates)
    near_weights = dict(addon._limit_upper_arm_root_weights(near_point, rig, near_weights, 1.0))
    transition = addon._upper_arm_root_transition_length(rig, ".l", 1.0)
    factor = 0.005 / transition
    expected_limit = factor * factor * (3.0 - 2.0 * factor)
    require(
        near_weights.get("upper_arm.l", 0.0) <= expected_limit + 1e-6,
        "upper-arm root blend exceeded its shoulder-plane limit",
    )
    for child_name, parent_name in (("forearm.l", "upper_arm.l"), ("lower_leg.l", "thigh.l")):
        child = rig.data.bones[child_name]
        parent = rig.data.bones[parent_name]
        parent_axis = (parent.tail_local - parent.head_local).normalized()
        transition = addon._child_joint_transition_length(rig, parent_name, 1.0)
        parent_side_point = child.head_local - parent_axis * (transition + 0.001)
        limited = dict(
            addon._limit_limb_child_root_weights(
                parent_side_point,
                rig,
                [(parent_name, 0.5), (child_name, 0.5)],
                1.0,
            )
        )
        require(
            limited.get(child_name, 0.0) == 0.0 and abs(limited.get(parent_name, 0.0) - 1.0) < 1e-6,
            f"{child_name} retained influence on the parent side of its joint",
        )
        child_side_point = child.head_local + parent_axis * 0.005
        limited = dict(
            addon._limit_limb_child_root_weights(
                child_side_point,
                rig,
                [(parent_name, 0.2), (child_name, 0.8)],
                1.0,
            )
        )
        factor = (0.005 + transition) / (transition * 2.0)
        expected_limit = factor * factor * (3.0 - 2.0 * factor)
        require(
            limited.get(child_name, 0.0) <= expected_limit + 1e-6,
            f"{child_name} exceeded its joint-root blend limit",
        )
    distal_cases = (
        ("hand.l", "forearm.l", "PARENT", "HAND"),
        ("foot.l", "lower_leg.l", "PARENT", "FOOT"),
        ("toes.l", "foot.l", "PARENT", "TOES"),
        ("index_01.l", "hand.l", "CHILD", "FINGER"),
        ("index_02.l", "index_01.l", "PARENT", "FINGER"),
        ("index_03.l", "index_02.l", "PARENT", "FINGER"),
    )
    for child_name, parent_name, axis_source, kind in distal_cases:
        child = rig.data.bones[child_name]
        axis_bone = child if axis_source == "CHILD" else rig.data.bones[parent_name]
        axis = (axis_bone.tail_local - axis_bone.head_local).normalized()
        transition = addon._distal_joint_transition_length(rig, child_name, 1.0, kind)
        parent_side_point = child.head_local - axis * (transition + 0.001)
        limited = dict(
            addon._limit_distal_joint_weights(
                parent_side_point,
                rig,
                [(parent_name, 0.5), (child_name, 0.5)],
                1.0,
            )
        )
        require(
            limited.get(child_name, 0.0) == 0.0 and abs(sum(limited.values()) - 1.0) < 1e-6,
            f"{child_name} retained influence outside its parent joint band",
        )
    shoulder = rig.data.bones["shoulder.l"]
    shoulder_axis = (shoulder.tail_local - shoulder.head_local).normalized()
    shoulder_transition = addon._distal_joint_transition_length(
        rig,
        "shoulder.l",
        1.0,
        "SHOULDER",
    )
    chest_point = shoulder.head_local - shoulder_axis * (shoulder_transition + 0.001)
    limited = dict(
        addon._limit_shoulder_root_weights(
            chest_point,
            rig,
            [("upper_chest.x", 0.5), ("shoulder.l", 0.5)],
            1.0,
        )
    )
    require(
        limited.get("shoulder.l", 0.0) == 0.0 and abs(limited.get("upper_chest.x", 0.0) - 1.0) < 1e-6,
        "shoulder.l retained influence in the chest region",
    )
    limited_finger = dict(
        addon._finalize_joint_limited_weights(
            {"pinky_01.l": 0.9923756253, "hand.l": 0.0076243747},
            protected_names={"pinky_01.l"},
            preferred_names=("hand.l",),
        )
    )
    require(
        abs(limited_finger.get("pinky_01.l", 0.0) - 0.9923756253) < 1e-8
        and abs(limited_finger.get("hand.l", 0.0) - 0.0076243747) < 1e-8,
        "joint limiting discarded the required sub-1% parent support weight",
    )

    for component_id in ("BodyMesh:0", "GarmentMesh:0"):
        changed, _message = addon._set_component_base_region_override(
            bpy.context,
            settings,
            component_indices[component_id],
            "HEAD",
        )
        require(changed, f"failed to persist HEAD override for {component_id}")
    authority_ids, _authority_by_region = addon._base_authority_component_ids(
        bpy.context,
        settings,
        rig,
        regions,
        max(float(body.dimensions.z), float(garment.dimensions.z), 1.0),
    )
    marked_base_ids = {"BodyMesh:0", "GarmentMesh:0"}
    require(
        len(authority_ids & marked_base_ids) == 1,
        "HEAD override regression fixture did not contain one authority and one transferred component",
    )
    below_head_point = rig.data.bones["head.x"].head_local.copy()
    below_head_point.z -= 0.01
    require(
        addon._automatic_locked_region_applies(below_head_point, rig, {"base_region": "HEAD"}, "HEAD"),
        "explicit HEAD override must preserve rigid head binding",
    )
    require(
        not addon._automatic_locked_region_applies(below_head_point, rig, {}, "HEAD"),
        "automatic HEAD inference must not lock vertices below the head root",
    )
    result = bpy.ops.wgame_rigging.clear_base_region_overrides(region="HEAD")
    require(result == {"FINISHED"}, "bulk HEAD override removal did not finish")
    require(
        all(not addon._unrigged_components(settings)[component_indices[component_id]].get("base_region") for component_id in marked_base_ids),
        "bulk HEAD override removal left persisted markers behind",
    )
    for component_id in marked_base_ids:
        changed, _message = addon._set_component_base_region_override(
            bpy.context,
            settings,
            component_indices[component_id],
            "HEAD",
        )
        require(changed, f"failed to restore HEAD override fixture for {component_id}")
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    custom_manual = body.vertex_groups.new(name="chain_manual")
    custom_manual.add([4, 5, 6, 7], 1.0, "REPLACE")
    mask = body.vertex_groups.new(name="author_mask")
    mask.add(list(range(len(body.data.vertices))), 0.25, "REPLACE")
    before_custom = [weight(body, "chain_manual", index) for index in range(4, 8)]
    before_mask = [weight(body, "author_mask", index) for index in range(len(body.data.vertices))]

    result = bpy.ops.wgame_rigging.execute_base_skin()
    require(result == {"FINISHED"}, "base skin operator did not finish")
    require(settings.wgame_base_skin_complete, settings.wgame_base_skin_report)
    require(settings.wgame_manual_binding_ui_tab == "CUSTOM", "successful Base Skin did not open the Custom Bind tab")
    require("HEAD覆盖错误 0/8" in settings.wgame_base_skin_report, "explicit HEAD rigid-binding validation did not pass")
    require("自动HEAD越界 0/" in settings.wgame_base_skin_report, "automatic HEAD anatomical boundary validation did not pass")
    require(before_custom == [weight(body, "chain_manual", index) for index in range(4, 8)], "custom deform weights changed")
    require(before_mask == [weight(body, "author_mask", index) for index in range(len(body.data.vertices))], "non-deform group changed")
    require(all(abs(weight(body, "head.x", index) - 1.0) < 1e-6 for index in range(0, 4)), "authority explicit HEAD override was not rigidly assigned to head.x")
    require(all(weight(body, "pelvis.x", index) == 0.0 for index in range(0, 4)), "authority explicit HEAD override retained non-head weights")
    require(all(abs(weight(garment, "head.x", index) - 1.0) < 1e-6 for index in range(0, 4)), "transferred explicit HEAD override was not rigidly assigned to head.x")
    require(all(weight(garment, "pelvis.x", index) == 0.0 for index in range(0, 4)), "transferred explicit HEAD override retained non-head weights")
    body.vertex_groups["head.x"].add([0], 0.4, "REPLACE")
    before_custom_normalize = [weight(body, "chain_manual", index) for index in range(4, 8)]
    result = bpy.ops.wgame_rigging.normalize_all_base_weights()
    require(result == {"FINISHED"}, "base manual-weight normalization did not finish")
    require(settings.wgame_manual_binding_ui_tab == "BASE", "base-weight edit did not return the UI to the preparation tab")
    require(abs(weight(body, "head.x", 0) - 1.0) < 1e-6, "base normalization did not preserve a single-group ratio at sum one")
    require(
        before_custom_normalize == [weight(body, "chain_manual", index) for index in range(4, 8)],
        "base normalization modified Custom Bind weights",
    )
    require("骨间比例保持不变" in settings.wgame_base_skin_report, "base normalization did not report its preservation contract")
    result = bpy.ops.wgame_rigging.validate_existing_base_skin()
    require(result == {"FINISHED"} and settings.wgame_base_skin_complete, settings.wgame_base_skin_report)
    require(settings.wgame_manual_binding_ui_tab == "CUSTOM", "successful no-recalculation validation did not reopen the Custom Bind tab")
    body.vertex_groups["head.x"].remove([0])
    settings.wgame_base_skin_complete = False
    result = bpy.ops.wgame_rigging.repair_base_validation_failures()
    require(result == {"FINISHED"}, "conservative base failure repair did not finish")
    require(abs(weight(body, "head.x", 0) - 1.0) < 1e-6, "base zero-weight repair did not copy safe same-component topology weights")
    require(settings.wgame_base_skin_complete, settings.wgame_base_skin_report)
    changed, _message = addon._set_component_base_region_override(
        bpy.context,
        settings,
        component_indices["BodyMesh:0"],
        "AUTO",
    )
    require(changed, "failed to clear HEAD vertex mask before existing-weight adoption")
    before_existing_adoption = [
        [(assignment.group, round(float(assignment.weight), 8)) for assignment in vertex.groups]
        for vertex in body.data.vertices
    ]
    settings.wgame_base_skin_complete = False
    settings.wgame_base_skin_report = ""
    result = bpy.ops.wgame_rigging.validate_existing_base_skin()
    require(result == {"FINISHED"} and settings.wgame_base_skin_complete, settings.wgame_base_skin_report)
    require(
        addon._unrigged_components(settings)[component_indices["BodyMesh:0"]].get("base_region") == "HEAD",
        "existing rigid head weights were not adopted as explicit HEAD metadata",
    )
    require(
        before_existing_adoption == [
            [(assignment.group, round(float(assignment.weight), 8)) for assignment in vertex.groups]
            for vertex in body.data.vertices
        ],
        "existing base-skin validation modified vertex weights",
    )
    require(addon._armature_modifier(body).object == rig, "body modifier missing")
    require(addon._armature_modifier(garment).object == rig, "second mesh modifier missing")

    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    saved_base_report = settings.wgame_base_skin_report
    addon._set_custom_mask_vertices(body, [11], False)
    addon._sync_component_scopes_from_mask(bpy.context, settings, finalized=True)
    chain_empty = body.vertex_groups.get("chain_empty") or body.vertex_groups.new(name="chain_empty")
    chain_empty.add([11], 1.0, "REPLACE")
    boundary_plan, _boundary_stats = addon._build_zero_weight_neighbor_sync_plan(bpy.context, settings, rig, 2)
    require(
        not any(object_name == body.name and vertex_id in {8, 9, 10} for object_name, vertex_id, _weights in boundary_plan),
        "zero-weight repair crossed from a Base Skin vertex into the Custom Bind mask",
    )
    chain_empty.remove([11])
    addon._set_custom_mask_vertices(body, [11], True)
    addon._sync_component_scopes_from_mask(bpy.context, settings, finalized=True)
    settings.wgame_base_skin_complete = True
    settings.wgame_base_skin_report = saved_base_report

    body.vertex_groups["chain_manual"].remove([7])
    settings.wgame_zero_weight_sync_steps = 2
    result = bpy.ops.wgame_rigging.sync_zero_weights_from_neighbors()
    require(result == {"FINISHED"} and abs(weight(body, "chain_manual", 7) - 1.0) < 1e-6, "scattered zero-weight vertex was not synchronized from topology neighbors")
    require(all(weight(body, "chain_empty", index) == 0.0 for index in range(8, 12)), "all-zero component was incorrectly filled from another island")
    body.vertex_groups["chain_manual"].add([4], 0.6, "REPLACE")
    pelvis_group = body.vertex_groups.get("pelvis.x") or body.vertex_groups.new(name="pelvis.x")
    pelvis_group.add([4], 0.2, "REPLACE")
    result = bpy.ops.wgame_rigging.normalize_all_custom_weights()
    require(result == {"FINISHED"}, "relative custom normalization did not finish")
    require(
        abs(weight(body, "chain_manual", 4) - 0.75) < 1e-6
        and abs(weight(body, "pelvis.x", 4) - 0.25) < 1e-6,
        "relative custom normalization did not preserve existing bone ratios",
    )
    require(all(weight(body, "chain_empty", index) == 0.0 for index in range(8, 12)), "relative custom normalization modified zero-weight vertices")

    bpy.ops.wgame_rigging.isolate_custom_vertices()
    require(all(not body.data.vertices[index].hide for index in range(4, 12)), "custom vertices were hidden")
    require(all(body.data.vertices[index].hide for index in range(0, 4)), "base vertices were not hidden")
    bpy.ops.wgame_rigging.show_all_custom_vertices()
    require(all(not vertex.hide for vertex in body.data.vertices), "visibility was not restored")

    settings.wgame_custom_mesh = body.name
    settings.wgame_custom_weight_bone = "chain_empty"
    hidden_component = addon._unrigged_components(settings)[component_indices["BodyMesh:1"]]
    addon._set_component_vertices_hidden(bpy.context, hidden_component, True)
    original_lock_object_mode = bpy.context.scene.tool_settings.lock_object_mode
    result = bpy.ops.wgame_rigging.edit_custom_weights()
    require(result == {"FINISHED"}, "custom Weight Paint did not start")
    require(not bpy.context.scene.tool_settings.lock_object_mode and rig.mode == "POSE" and body.mode == "WEIGHT_PAINT", "custom Weight Paint did not establish Pose + Paint selection state")
    require(addon._FOCUS_STATE.get("bone_name") == "chain_empty" and addon._FOCUS_STATE.get("kind") == "WEIGHT_BROWSER", "custom bone locator was not shown")
    require(rig.show_in_front and rig.pose.bones["chain_empty"].select, "custom bone was not made visible and selected")
    selected = {vertex.index for vertex in body.data.vertices if vertex.select}
    require(selected == set(range(8, 12)), f"custom mask did not preserve hidden components: {sorted(selected)}")
    require(all(body.data.vertices[index].hide for index in range(4, 8)) and body.data.polygons[1].hide, "entering Weight Paint revealed hidden geometry")
    require(body.vertex_groups.get("chain_empty") is not None, "empty custom bone group was not created")
    require(all(weight(body, "chain_empty", index) == 0.0 for index in range(8, 12)), "empty group received automatic weights")
    require(addon._activate_custom_weight_bone(bpy.context, settings, rig, body, "chain_manual"), "3D bone picker activation helper failed")
    require(settings.wgame_custom_weight_bone == "chain_manual" and body.vertex_groups.active.name == "chain_manual", "3D bone picker did not synchronize current bone and vertex group")
    require(addon._FOCUS_STATE.get("bone_name") == "chain_manual" and rig.pose.bones["chain_manual"].select, "3D bone picker did not update pose selection and marker")
    require(addon._activate_custom_weight_bone(bpy.context, settings, rig, body, "chain_empty"), "failed to restore test weight bone")
    bpy.ops.wgame_rigging.exit_custom_weights()
    require(not addon._FOCUS_STATE, "custom bone locator was not cleared on exit")
    require(bpy.context.scene.tool_settings.lock_object_mode == original_lock_object_mode, "Lock Object Modes was not restored on Weight Paint exit")
    require(all(body.data.vertices[index].hide for index in range(4, 8)) and body.data.polygons[1].hide, "exiting Weight Paint revealed hidden geometry")
    result = bpy.ops.wgame_rigging.isolate_zero_weight_custom()
    require(result == {"FINISHED"} and bpy.context.object.mode == "EDIT", "zero-weight isolation did not enter Edit Mode")
    require(all(not body.data.vertices[index].hide and body.data.vertices[index].select for index in range(8, 12)), "zero-weight vertices were not shown and selected")
    require(all(body.data.vertices[index].hide for index in range(0, 8)), "non-zero or base vertices remained visible during zero isolation")
    bpy.ops.wgame_rigging.isolate_custom_vertices()
    require(all(not body.data.vertices[index].hide for index in range(4, 12)) and all(body.data.vertices[index].hide for index in range(0, 4)), "restoring custom isolation did not show all custom vertices")
    bpy.ops.wgame_rigging.show_all_custom_vertices()
    require(all(not vertex.hide for vertex in body.data.vertices), "restoring all vertices after zero isolation failed")

    bpy.ops.wgame_rigging.validate_custom_binding()
    require(not settings.wgame_custom_binding_complete, "zero-weight custom component unexpectedly passed")
    require(settings.wgame_manual_binding_ui_tab == "CUSTOM", "failed 3C validation did not remain on the Custom tab")
    body.vertex_groups["chain_empty"].add([8, 9, 10, 11], 1.0, "REPLACE")
    bpy.ops.wgame_rigging.validate_custom_binding()
    require(settings.wgame_custom_binding_complete, settings.wgame_custom_binding_report)
    require(settings.wgame_component_skin_complete, "final compatibility state was not set")
    require(settings.wgame_manual_binding_ui_tab == "FINAL", "successful 3C validation did not open the Validation/Publish tab")

    # A normalized weight on the wrong anatomical bone must fail the final gate.
    body.vertex_groups["head.x"].remove([0])
    body.vertex_groups["pelvis.x"].add([0], 1.0, "REPLACE")
    require(settings.wgame_component_skin_complete, "fixture must retain stale acceptance")
    bpy.ops.wgame_rigging.validate_custom_binding()
    require(not settings.wgame_component_skin_complete, "final gate accepted invalid HEAD ownership")
    require("HEAD覆盖错误 1/8" in settings.wgame_base_skin_report, "final gate omitted anatomical diagnostics")
    require(abs(weight(body, "pelvis.x", 0) - 1.0) < 1e-6, "final validation rewrote weights")
    # Publishing must reject current data even if persisted acceptance still says passed.
    settings.wgame_component_skin_complete = True
    settings.character_id = "000004"
    previous_root_resolver = addon._resolve_repository_root
    previous_server = addon._ensure_authoring_server
    addon._resolve_repository_root = lambda _settings: str(ADDON_PATH.parents[3])
    def forbidden_server(*args):
        raise AssertionError("invalid binding reached external publish side effects")
    addon._ensure_authoring_server = forbidden_server
    try:
        try:
            outcome = bpy.ops.wgame_rigging.publish_fbx_and_update_unity()
        except RuntimeError:
            outcome = {"CANCELLED"}
        require(outcome == {"CANCELLED"} and not settings.wgame_component_skin_complete,
                "publish trusted stale acceptance")
    finally:
        addon._resolve_repository_root = previous_root_resolver
        addon._ensure_authoring_server = previous_server
    body.vertex_groups["pelvis.x"].remove([0])
    body.vertex_groups["head.x"].add([0], 1.0, "REPLACE")
    require(addon._validate_complete_binding(bpy.context, settings, rig), "corrected binding did not recover")
    # A second cleanup repairs the old cleanup's invalidated flags without rebinding.
    before_cleanup_weights = [[deform_weights(obj, rig, vertex.index) for vertex in obj.data.vertices] for obj in (body, garment)]
    settings.unrigged_preflight_passed = False
    settings.unrigged_model_fingerprint = ""
    settings.wgame_component_skin_complete = False
    settings.wgame_manual_binding_ui_tab = "BASE"
    result = bpy.ops.wgame_rigging.clean_publish_faceless_vertices()
    require(result == {"FINISHED"} and settings.wgame_component_skin_complete, "repeat cleanup did not restore acceptance")
    require(settings.wgame_manual_binding_ui_tab == "FINAL" and addon._current_model_preflight(bpy.context, settings), "cleanup sent a valid character back to preparation")
    require(before_cleanup_weights == [[deform_weights(obj, rig, vertex.index) for vertex in obj.data.vertices] for obj in (body, garment)], "cleanup acceptance changed weights")
    body.vertex_groups["head.x"].remove([0])
    status = addon._refresh_binding_after_faceless_cleanup(bpy.context, settings, rig)
    require("未通过" in status and not settings.wgame_component_skin_complete, "cleanup accepted a real weight failure")
    body.vertex_groups["head.x"].add([0], 1.0, "REPLACE")
    addon._refresh_binding_after_faceless_cleanup(bpy.context, settings, rig)
    selected = addon._select_publish_meshes(bpy.context, settings, rig)
    require({obj.name for obj in selected} == {body.name, garment.name}, "publish omitted a participant mesh")
    require({obj.name for obj in bpy.context.selected_objects} == {body.name, garment.name}, "Palette scope differs from export scope")

    original_pose = {bone.name: addon._matrix_to_list(bone.matrix_basis) for bone in rig.pose.bones}
    settings.wgame_deformation_preview_strength = 0.75
    settings.wgame_muscle_side = "LEFT"
    settings.wgame_deformation_preview_preset = "SHOULDER_RANGE"
    result = bpy.ops.wgame_rigging.preview_deformation(preset="SELECTED")
    require(result == {"FINISHED"}, "shoulder-range deformation preview did not finish")
    shoulder_report = json.loads(settings.wgame_deformation_preview_report)
    require(
        shoulder_report["preset"] == "SHOULDER_RANGE"
        and shoulder_report["applied_bones"] >= 4
        and shoulder_report["regions"].get("肩臂", 0) >= 2,
        f"shoulder preview coverage report was incomplete: {shoulder_report}",
    )
    require(
        addon._matrix_to_list(rig.pose.bones["upper_arm.l"].matrix_basis) != original_pose["upper_arm.l"]
        and addon._matrix_to_list(rig.pose.bones["upper_arm.r"].matrix_basis) == original_pose["upper_arm.r"],
        "left-only preview changed the wrong side or failed to move the selected side",
    )
    settings.wgame_muscle_side = "BOTH"
    result = bpy.ops.wgame_rigging.navigate_deformation_preview(step=1)
    require(result == {"FINISHED"}, "next deformation acceptance pose did not apply")
    elbow_report = json.loads(settings.wgame_deformation_preview_report)
    require(
        elbow_report["preset"] == "ELBOW_WRIST"
        and elbow_report["regions"].get("肘腕", 0) >= 4,
        f"elbow/wrist preview did not cover both sides: {elbow_report}",
    )
    settings.wgame_deformation_preview_preset = "CUSTOM_CHAINS"
    result = bpy.ops.wgame_rigging.preview_deformation(preset="SELECTED")
    require(result == {"FINISHED"}, "custom-chain deformation preview did not finish")
    custom_pose_report = json.loads(settings.wgame_deformation_preview_report)
    require(
        custom_pose_report["regions"].get("自定义骨链", 0) >= 1,
        f"custom-chain deformation preview did not exercise custom bones: {custom_pose_report}",
    )
    result = bpy.ops.wgame_rigging.restore_deformation_preview()
    require(result == {"FINISHED"}, "deformation preview restore did not finish")
    require(
        all(addon._matrix_to_list(bone.matrix_basis) == original_pose[bone.name] for bone in rig.pose.bones),
        "deformation preview did not restore the exact pre-preview pose",
    )
    require(not settings.wgame_deformation_preview_report, "restoring the bind pose left a stale preview report")

    custom_ids = [8, 9, 10, 11]
    deform_test_groups = ("pelvis.x", "head.x", "chain_manual", "chain_empty", "chain_extra_1")
    for group_name in deform_test_groups:
        group = body.vertex_groups.get(group_name) or body.vertex_groups.new(name=group_name)
        group.add(custom_ids, 0.2, "REPLACE")
    bpy.ops.wgame_rigging.validate_custom_binding()
    require(not settings.wgame_custom_binding_complete and "超4骨 4" in settings.wgame_custom_binding_report, "five-bone influence was not rejected")
    require(settings.wgame_manual_binding_ui_tab == "CUSTOM", "failed custom-weight revalidation did not return to the Custom tab")
    for group_name in deform_test_groups:
        body.vertex_groups[group_name].remove(custom_ids)
    body.vertex_groups["chain_empty"].add(custom_ids, 0.4, "REPLACE")
    bpy.ops.wgame_rigging.validate_custom_binding()
    require(not settings.wgame_custom_binding_complete and "未归一 4" in settings.wgame_custom_binding_report, "unnormalized custom weights were not rejected")
    body.vertex_groups["chain_empty"].add(custom_ids, 1.0, "REPLACE")
    garment_modifier = addon._armature_modifier(garment)
    garment_modifier.object = None
    bpy.ops.wgame_rigging.validate_custom_binding()
    require(not settings.wgame_custom_binding_complete and "Modifier错误 1" in settings.wgame_custom_binding_report, "modifier mismatch was not rejected")
    garment_modifier.object = rig
    bpy.ops.wgame_rigging.validate_custom_binding()
    require(settings.wgame_custom_binding_complete, "valid weights did not recover after failure cases")
    require(settings.wgame_manual_binding_ui_tab == "FINAL", "recovered final validation did not reopen the Final tab")

    unexpected = create_mesh("UnexpectedMesh", (0.2,), x_offset=3.0)
    try:
        result = bpy.ops.wgame_rigging.validate_custom_binding()
    except RuntimeError:
        result = {"CANCELLED"}
    require(result == {"CANCELLED"} and not settings.wgame_base_skin_complete, "topology fingerprint change did not invalidate downstream state")
    bpy.data.objects.remove(unexpected, do_unlink=True)
    settings.unrigged_component_scan_fingerprint = addon._unrigged_topology_fingerprint(bpy.context)
    settings.wgame_base_skin_complete = True

    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    armature_bone = rig.data.edit_bones["head.x"]
    armature_bone.tail.z += 0.1
    bpy.ops.object.mode_set(mode="OBJECT")
    snapshot_ok, _message = addon._base_skeleton_snapshot_status(rig)
    require(not snapshot_ok, "base skeleton change was not detected")

    components[0]["role"] = "HUMAN_BODY"
    settings.unrigged_components_json = json.dumps(components, separators=(",", ":"))
    settings.unrigged_binding_groups_json = json.dumps([{"id": "legacy", "component_ids": [components[0]["id"]]}])
    bone_count = len(rig.data.bones)
    group_count = len(body.vertex_groups)
    bpy.ops.wgame_rigging.reset_binding_scopes()
    require(len(rig.data.bones) == bone_count, "legacy reset deleted bones")
    require(len(body.vertex_groups) == group_count, "legacy reset deleted vertex groups")
    require(not addon._unrigged_binding_groups(settings), "legacy groups were not cleared")
    require(all(not component.get("binding_scope") for component in addon._unrigged_components(settings)), "scopes were not reset")

    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    run_custom_auto_weight_tests(addon)
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    export_mesh = create_mesh("ExportMesh", (0.0,))
    export_armature = bpy.data.armatures.new("ExportRigData")
    export_rig = bpy.data.objects.new("ExportRig", export_armature)
    bpy.context.scene.collection.objects.link(export_rig)
    export_rig["wgame_rig_kind"] = "deform_skeleton"
    export_rig[addon.CUSTOM_WEIGHT_CHAINS_KEY] = json.dumps({"version": 1, "chains": []})
    export_rig[addon.RIGID_BIND_ATTACHMENTS_KEY] = json.dumps({"version": 1, "attachments": []})
    bpy.context.view_layer.objects.active = export_rig
    export_rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    export_bone = export_armature.edit_bones.new("root.x")
    export_bone.head = (0.0, 0.0, 0.0)
    export_bone.tail = (0.0, 0.0, 1.0)
    bpy.ops.object.mode_set(mode="OBJECT")
    export_mesh.vertex_groups.new(name="root.x").add(range(len(export_mesh.data.vertices)), 1.0, "REPLACE")
    export_mesh.vertex_groups.new(name=addon.CUSTOM_BIND_MASK_GROUP).add([0], 1.0, "REPLACE")
    export_mesh.vertex_groups.new(name=addon.RIGID_BIND_MASK_GROUP).add([0], 1.0, "REPLACE")
    export_mesh[addon.CUSTOM_BIND_MASK_AUTHORITY_KEY] = True
    export_mesh.vertex_groups.new(name=addon._base_region_group_name("HEAD")).add([1], 1.0, "REPLACE")
    export_mesh[addon.BASE_REGION_MASK_AUTHORITY_KEY] = True
    export_modifier = export_mesh.modifiers.new("Armature", "ARMATURE")
    export_modifier.object = export_rig
    export_backup = export_mesh.copy()
    export_backup.data = export_mesh.data.copy()
    export_backup.name = "ExportMesh__TOPO_BACKUP"
    export_backup.hide_render = True
    bpy.context.scene.collection.objects.link(export_backup)
    exporter_spec = importlib.util.spec_from_file_location(
        "wgame_rigging_assistant_export_test",
        ADDON_PATH.parent / "export_unity_fbx.py",
    )
    exporter = importlib.util.module_from_spec(exporter_spec)
    exporter_spec.loader.exec_module(exporter)
    resolved_export_mesh, resolved_export_rig = exporter._character_objects()
    require(
        resolved_export_mesh == [export_mesh] and resolved_export_rig == export_rig,
        "FBX exporter did not ignore a bound topology-backup mesh",
    )
    with tempfile.TemporaryDirectory() as temporary_directory:
        output_fbx = pathlib.Path(temporary_directory) / "mask-exclusion.fbx"
        previous_argv = sys.argv
        try:
            sys.argv = ["export_unity_fbx.py", "--", str(output_fbx)]
            exporter.main()
        finally:
            sys.argv = previous_argv
        require(output_fbx.is_file(), "mask-exclusion FBX was not written")
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        require("FINISHED" in bpy.ops.import_scene.fbx(filepath=str(output_fbx), use_anim=False), "mask-exclusion FBX import failed")
        imported_mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
        require(imported_mesh.vertex_groups.get("root.x") is not None, "deform vertex group was lost during FBX export")
        require(
            imported_mesh.vertex_groups.get(addon.CUSTOM_BIND_MASK_GROUP) is None,
            "authoring-only custom mask leaked into FBX",
        )
        require(
            imported_mesh.vertex_groups.get(addon.RIGID_BIND_MASK_GROUP) is None,
            "authoring-only rigid mask leaked into FBX",
        )
        require(addon.CUSTOM_BIND_MASK_AUTHORITY_KEY not in imported_mesh, "custom-mask authority marker leaked into FBX")
        require(
            not any(group.name.startswith(addon.BASE_REGION_GROUP_PREFIX) for group in imported_mesh.vertex_groups),
            "base-region authoring group leaked into FBX",
        )
        require(addon.BASE_REGION_MASK_AUTHORITY_KEY not in imported_mesh, "base-region authority marker leaked into FBX")
        imported_rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
        require(addon.CUSTOM_WEIGHT_CHAINS_KEY not in imported_rig, "custom bone-chain authoring metadata leaked into FBX")
        require(addon.RIGID_BIND_ATTACHMENTS_KEY not in imported_rig, "rigid-attachment authoring metadata leaked into FBX")
    with tempfile.TemporaryDirectory() as temporary_directory:
        temporary_root = pathlib.Path(temporary_directory)
        package_root = temporary_root / "Authoring" / "CharacterPackages" / "character-000004"
        config_root = package_root / "config"
        config_root.mkdir(parents=True)
        cloth_path = config_root / "character_cloth_magica.json"
        original_cloth = '{"schemaVersion":"1.1","backend":"MagicaCloth2","enabled":true,"cloths":[{"id":"old"}]}\n'
        cloth_path.write_text(original_cloth, encoding="utf-8")
        backup_root = temporary_root / "backup"
        require(
            addon._reset_character_cloth_after_skeleton_change(package_root, backup_root),
            "skeleton-change publish did not reset existing Cloth config",
        )
        reset_cloth = json.loads(cloth_path.read_text(encoding="utf-8"))
        require(reset_cloth == {
            "schemaVersion": "1.1",
            "backend": "MagicaCloth2",
            "enabled": False,
            "cloths": [],
        }, "skeleton-change Cloth reset payload is invalid")
        require(
            (backup_root / cloth_path.name).read_text(encoding="utf-8") == original_cloth,
            "skeleton-change Cloth reset did not preserve the previous config backup",
        )
        audit_path = temporary_root / "audit.json"
        audit_path.write_text(
            json.dumps({"source": {"boneHierarchyHash": "sha256:test"}}),
            encoding="utf-8",
        )
        require(
            addon._published_bone_hierarchy_hash(audit_path) == "sha256:test",
            "published bone hierarchy hash was not read from the audit",
        )
    addon.unregister()
    require(addon._FOCUS_DRAW_HANDLE is None, "2D viewport overlay draw handler was not removed")
    require(addon._BASE_REGION_DRAW_HANDLE is None, "3D base-region overlay draw handler was not removed")
    print("WGame manual binding workflow: PASS")


if __name__ == "__main__":
    main()
