"""Uniformly rescale one WGame skinned character authority without leaving object scale.

Run with Blender in background mode after opening the source Blend:

    blender -b source.blend --python scale_character_authority.py -- <factor> <output.blend>

The script scales mesh/shape-key coordinates, deform armature rest bones and WGame
guide locations in data space. Object transforms remain at scale 1, preserving the
Unity import contract and preventing scene-instance compensation.
"""

from __future__ import annotations

import json
import math
import pathlib
import sys

import bpy


SCALE_REVISION = "wgame-height-calibration-v2"


def _arguments() -> tuple[float, pathlib.Path]:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) != 2:
        raise RuntimeError("Expected <uniform-scale-factor> <output.blend> after --")
    factor = float(args[0])
    if not math.isfinite(factor) or factor <= 0.0:
        raise RuntimeError(f"Scale factor must be finite and positive; received {factor!r}")
    return factor, pathlib.Path(args[1]).resolve()


def _character_objects() -> tuple[bpy.types.Object, bpy.types.Object]:
    rigs = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "ARMATURE" and obj.get("wgame_rig_kind") == "deform_skeleton"
    ]
    if len(rigs) != 1:
        raise RuntimeError(f"Expected one WGame deform skeleton; found {len(rigs)}")
    rig = rigs[0]
    meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and any(modifier.type == "ARMATURE" and modifier.object == rig for modifier in obj.modifiers)
    ]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one mesh bound to {rig.name}; found {len(meshes)}")
    return meshes[0], rig


def _require_clean_object_transforms(mesh: bpy.types.Object, rig: bpy.types.Object) -> None:
    for obj in (mesh, rig):
        if any(abs(float(value) - 1.0) > 1e-6 for value in obj.scale):
            raise RuntimeError(f"{obj.name} object scale must be 1 before calibration: {tuple(obj.scale)}")
        if any(abs(float(value)) > 1e-6 for value in obj.rotation_euler):
            raise RuntimeError(f"{obj.name} object rotation must be zero before calibration: {tuple(obj.rotation_euler)}")
    if bpy.data.actions:
        raise RuntimeError("Authority Blend contains animation actions; scale animation translation curves explicitly before calibration")


def _scale_mesh(mesh: bpy.types.Object, factor: float) -> None:
    for vertex in mesh.data.vertices:
        vertex.co *= factor
    if mesh.data.shape_keys:
        for key_block in mesh.data.shape_keys.key_blocks:
            for point in key_block.data:
                point.co *= factor
    mesh.data.update()


def _scale_armature(rig: bpy.types.Object, factor: float) -> dict[str, float | int]:
    previous_active = bpy.context.view_layer.objects.active
    previous_mode = bpy.context.object.mode if bpy.context.object else "OBJECT"
    previous_mirror_x = bool(rig.data.use_mirror_x)
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    rig.hide_set(False)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.data.use_mirror_x = False
    bpy.ops.object.mode_set(mode="EDIT")
    validation: dict[str, float | int] = {}
    try:
        snapshots = {
            bone.name: {
                "head": bone.head.copy(),
                "tail": bone.tail.copy(),
                "head_radius": float(bone.head_radius),
                "tail_radius": float(bone.tail_radius),
                "envelope_distance": float(bone.envelope_distance),
                "roll": float(bone.roll),
                "parent": bone.parent.name if bone.parent else "",
                "use_deform": bool(bone.use_deform),
                "use_connect": bool(bone.use_connect),
            }
            for bone in rig.data.edit_bones
        }
        for bone in rig.data.edit_bones:
            source = snapshots[bone.name]
            bone.head = source["head"] * factor
            bone.tail = source["tail"] * factor
            bone.head_radius = source["head_radius"] * factor
            bone.tail_radius = source["tail_radius"] * factor
            bone.envelope_distance = source["envelope_distance"] * factor
        max_head_error = 0.0
        max_tail_error = 0.0
        max_roll_error = 0.0
        minimum_direction_dot = 1.0
        relationship_mismatches = 0
        for bone in rig.data.edit_bones:
            source = snapshots[bone.name]
            expected_head = source["head"] * factor
            expected_tail = source["tail"] * factor
            max_head_error = max(max_head_error, (bone.head - expected_head).length)
            max_tail_error = max(max_tail_error, (bone.tail - expected_tail).length)
            max_roll_error = max(max_roll_error, abs(float(bone.roll) - source["roll"]))
            expected_vector = expected_tail - expected_head
            if bone.length > 1e-9 and expected_vector.length > 1e-9:
                minimum_direction_dot = min(
                    minimum_direction_dot,
                    (bone.tail - bone.head).normalized().dot(expected_vector.normalized()),
                )
            if (bone.parent.name if bone.parent else "") != source["parent"]:
                relationship_mismatches += 1
            if bool(bone.use_deform) != source["use_deform"] or bool(bone.use_connect) != source["use_connect"]:
                relationship_mismatches += 1
        validation = {
            "boneCount": len(snapshots),
            "maxHeadScaleError": max_head_error,
            "maxTailScaleError": max_tail_error,
            "maxRollErrorRadians": max_roll_error,
            "minimumDirectionDot": minimum_direction_dot,
            "relationshipMismatches": relationship_mismatches,
        }
        if (
            max_head_error > 1e-6
            or max_tail_error > 1e-6
            or max_roll_error > 1e-6
            or minimum_direction_dot < 0.999999
            or relationship_mismatches
        ):
            raise RuntimeError(f"Armature scale validation failed: {validation}")
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")
        rig.data.use_mirror_x = previous_mirror_x
        rig.select_set(False)
        if previous_active and previous_active.name in bpy.context.view_layer.objects:
            previous_active.select_set(True)
            bpy.context.view_layer.objects.active = previous_active
            if previous_mode != "OBJECT":
                bpy.ops.object.mode_set(mode=previous_mode)
    return validation


def _scale_guides(factor: float) -> int:
    count = 0
    for obj in bpy.context.scene.objects:
        if obj.type == "EMPTY" and obj.name.startswith("WGameGuide_"):
            obj.location *= factor
            obj.empty_display_size *= factor
            count += 1
    return count


def main() -> None:
    factor, output = _arguments()
    mesh, rig = _character_objects()
    if rig.get("wgame_scale_revision") == SCALE_REVISION:
        raise RuntimeError(f"Scale calibration {SCALE_REVISION} is already applied")
    _require_clean_object_transforms(mesh, rig)

    before = tuple(float(value) for value in mesh.dimensions)
    _scale_mesh(mesh, factor)
    bone_validation = _scale_armature(rig, factor)
    guide_count = _scale_guides(factor)
    rig["wgame_scale_revision"] = SCALE_REVISION
    rig["wgame_scale_factor"] = factor
    bpy.context.scene["wgame_scale_revision"] = SCALE_REVISION
    bpy.context.scene["wgame_scale_factor"] = factor
    bpy.context.view_layer.update()
    after = tuple(float(value) for value in mesh.dimensions)

    expected = tuple(value * factor for value in before)
    if any(abs(actual - target) > max(1e-5, abs(target) * 1e-5) for actual, target in zip(after, expected)):
        raise RuntimeError(f"Scaled mesh dimensions do not match expectation: actual={after}, expected={expected}")
    for obj in (mesh, rig):
        if any(abs(float(value) - 1.0) > 1e-6 for value in obj.scale):
            raise RuntimeError(f"{obj.name} object scale changed during calibration: {tuple(obj.scale)}")

    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output), check_existing=False)
    print(
        "WGAME_SCALE_AUTHORITY "
        + json.dumps(
            {
                "input": bpy.data.filepath,
                "output": str(output),
                "factor": factor,
                "revision": SCALE_REVISION,
                "mesh": mesh.name,
                "armature": rig.name,
                "guideCount": guide_count,
                "beforeDimensions": before,
                "afterDimensions": after,
                "meshScale": tuple(float(value) for value in mesh.scale),
                "rigScale": tuple(float(value) for value in rig.scale),
                "boneValidation": bone_validation,
            },
            ensure_ascii=False,
            sort_keys=True,
        )
    )


if __name__ == "__main__":
    main()
