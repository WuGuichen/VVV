"""Blender background regression. Pass a reference character .blend after --; never saves it."""

import json
import math
from pathlib import Path
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import WGameRiggingAssistant as addon


def cancelled(operator):
    try:
        result = operator()
    except RuntimeError:
        result = {"CANCELLED"}
    assert result == {"CANCELLED"}, result


def finger_snapshot(rig):
    return [(bone.name, tuple(bone.head_local), tuple(bone.tail_local),
             bone.parent.name if bone.parent else "") for bone in rig.data.bones]


def branch_fixture(count):
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    rig = bpy.data.objects.new("BranchRig", bpy.data.armatures.new("BranchRigData"))
    bpy.context.scene.collection.objects.link(rig)
    addon._select_only(bpy.context, rig)
    bpy.ops.object.mode_set(mode="EDIT")
    bone = rig.data.edit_bones.new("hand.l")
    bone.head = (-0.06, 0, 0)
    bone.tail = (0, 0, 0)
    bpy.ops.object.mode_set(mode="OBJECT")
    vertices, edges = [(-0.01, 0, 0)], []
    for branch in range(count):
        previous = 0
        angle = branch * math.tau / count
        for step in range(1, 21):
            t = step / 20
            vertices.append((-0.01 + 0.19 * t, 0.09 * t * math.cos(angle), 0.09 * t * math.sin(angle)))
            current = len(vertices) - 1
            edges.append((previous, current))
            previous = current
    vertices.extend([(0, 0, -1), (0, 0, 1)])
    mesh = bpy.data.meshes.new("Branches")
    mesh.from_pydata(vertices, edges, [])
    obj = bpy.data.objects.new("Branches", mesh)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.update()
    return obj, rig


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    assert len(args) == 1, "Expected reference .blend path after --"
    addon.register()
    for count in (4, 5, 6):
        mesh, rig = branch_fixture(count)
        try:
            result = addon._hand_geodesic_analysis(mesh, rig, "l")
        except ValueError as error:
            assert count != 5 and f"{count}/5" in str(error), error
        else:
            assert count == 5 and len(result["tips"]) == 5

    bpy.ops.wm.open_mainfile(filepath=str(Path(args[0]).resolve()))
    rig = next(obj for obj in bpy.context.scene.objects
               if obj.type == "ARMATURE" and obj.get("wgame_rig_kind") == "deform_skeleton")
    settings = bpy.context.scene.wgame_rigging_assistant
    assert bpy.ops.wgame_rigging.generate_finger_bones() == {"FINISHED"}
    assert bpy.ops.wgame_rigging.accept_finger_bones() == {"FINISHED"}
    assert not addon._finger_structure_errors(rig, settings.wgame_humanoid_mapping_json)

    # Correct counts alone do not establish a valid hierarchy or mapping.
    mapping = settings.wgame_humanoid_mapping_json
    invalid = json.loads(mapping)
    invalid["LeftIndexProximal"] = "pinky_01.l"
    settings.wgame_humanoid_mapping_json = json.dumps(invalid)
    cancelled(bpy.ops.wgame_rigging.accept_finger_bones)
    assert not settings.wgame_finger_rig_complete
    assert "映射不一致" in settings.wgame_finger_detection_report
    settings.wgame_humanoid_mapping_json = mapping
    assert bpy.ops.wgame_rigging.accept_finger_bones() == {"FINISHED"}
    addon._select_only(bpy.context, rig)
    bpy.ops.object.mode_set(mode="EDIT")
    rig.data.edit_bones["index_01.l"].parent = None
    bpy.ops.object.mode_set(mode="OBJECT")
    cancelled(bpy.ops.wgame_rigging.accept_finger_bones)
    assert "父级必须为 hand.l" in settings.wgame_finger_detection_report
    # A stale confirmation cannot be captured as a new accepted base snapshot.
    settings.wgame_finger_rig_complete = True
    cancelled(bpy.ops.wgame_rigging.accept_wgame_skeleton)
    assert not settings.wgame_skeleton_accepted
    addon._select_only(bpy.context, rig)
    bpy.ops.object.mode_set(mode="EDIT")
    rig.data.edit_bones["index_01.l"].parent = rig.data.edit_bones["hand.l"]
    bpy.ops.object.mode_set(mode="OBJECT")
    assert bpy.ops.wgame_rigging.accept_finger_bones() == {"FINISHED"}

    # Deliberately displaced right-hand geometry must reject the mirror BEFORE deletion.
    mesh = addon._finger_authoring_mesh(bpy.context, rig)
    transform = rig.matrix_world.inverted() @ mesh.matrix_world
    bone = rig.data.bones["hand.r"]
    forward = (bone.tail_local - bone.head_local).normalized()
    height = mesh.dimensions.z
    moved = 0
    for vertex in mesh.data.vertices:
        point = transform @ vertex.co
        along = (point - bone.tail_local).dot(forward)
        radial = (point - (bone.tail_local + forward * along)).length
        if along >= -height * 0.01 and radial <= height * 0.09:
            vertex.co.y += height * 0.3
            moved += 1
    assert moved > 0
    mesh.data.update()
    bpy.context.view_layer.update()
    before = finger_snapshot(rig)
    cancelled(bpy.ops.wgame_rigging.generate_finger_bones)
    if rig.mode == "EDIT":
        bpy.ops.object.mode_set(mode="OBJECT")
    assert finger_snapshot(rig) == before, "failed generation replaced existing bones"
    assert ".r[" in settings.wgame_finger_detection_report
    assert settings.wgame_humanoid_mapping_json == mapping
    print("WGame finger generation, branch ambiguity and structural gates: PASS")


if __name__ == "__main__":
    main()
