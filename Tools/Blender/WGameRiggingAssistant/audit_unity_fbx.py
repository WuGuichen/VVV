"""Import a Unity FBX into a clean Blender scene and write a deterministic audit."""

from __future__ import annotations

import json
import hashlib
import pathlib
import runpy
import sys

import bpy

CUSTOM_BIND_MASK_GROUP = "WGame.CustomBindMask"
RIGID_BIND_MASK_GROUP = "WGame.RigidBindMask"
BASE_REGION_GROUP_PREFIX = "WGame.BaseRegion."
CUSTOM_WEIGHT_CHAINS_KEY = "wgame_rigging_assistant.custom_weight_chains.v1"
RIGID_BIND_ATTACHMENTS_KEY = "wgame_rigging_assistant.rigid_bind_attachments.v1"


def _bone_hierarchy_hash(rig: bpy.types.Object) -> str:
    payload = [
        {
            "name": bone.name,
            "parent": bone.parent.name if bone.parent else "",
            "useDeform": bool(bone.use_deform),
        }
        for bone in sorted(rig.data.bones, key=lambda item: item.name)
    ]
    encoded = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return "sha256:" + hashlib.sha256(encoded).hexdigest()


def _arguments() -> tuple[pathlib.Path, pathlib.Path, str]:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = [arg for arg in args if arg not in {"--require-palette", "--baked-surface"}]
    if len(args) not in {2, 3}:
        raise RuntimeError("Expected <input.fbx> <audit.json> [characterId] after --")
    return pathlib.Path(args[0]).resolve(), pathlib.Path(args[1]).resolve(), args[2] if len(args) == 3 else "000001"


def _weight_audit(mesh: bpy.types.Object) -> dict[str, int]:
    zero_weight = 0
    over_four = 0
    unnormalized = 0
    maximum = 0
    for vertex in mesh.data.vertices:
        weights = [float(group.weight) for group in vertex.groups if float(group.weight) > 1e-8]
        maximum = max(maximum, len(weights))
        if not weights:
            zero_weight += 1
            continue
        if len(weights) > 4:
            over_four += 1
        if abs(sum(weights) - 1.0) > 1e-4:
            unnormalized += 1
    return {
        "zeroWeightVertices": zero_weight,
        "verticesOverFourInfluences": over_four,
        "unnormalizedVertices": unnormalized,
        "maximumInfluences": maximum,
    }


def _torso_weight_coverage(mesh: bpy.types.Object) -> dict[str, int]:
    result = {}
    for name in ("pelvis.x", "spine_01.x", "spine_02.x", "upper_chest.x", "neck.x", "head.x"):
        group = mesh.vertex_groups.get(name)
        count = 0
        if group:
            for vertex in mesh.data.vertices:
                if any(item.group == group.index and float(item.weight) > 1e-6 for item in vertex.groups):
                    count += 1
        result[name] = count
    return result


def _audit_mesh(mesh, rig):
    if mesh.vertex_groups.get(CUSTOM_BIND_MASK_GROUP):
        raise RuntimeError(f"Authoring-only vertex group leaked into FBX: {CUSTOM_BIND_MASK_GROUP}")
    if mesh.vertex_groups.get(RIGID_BIND_MASK_GROUP):
        raise RuntimeError(f"Authoring-only vertex group leaked into FBX: {RIGID_BIND_MASK_GROUP}")
    leaked_region_groups = [group.name for group in mesh.vertex_groups if group.name.startswith(BASE_REGION_GROUP_PREFIX)]
    if leaked_region_groups:
        raise RuntimeError(f"Authoring-only base-region groups leaked into FBX: {leaked_region_groups}")
    if CUSTOM_WEIGHT_CHAINS_KEY in rig:
        raise RuntimeError(f"Authoring-only custom bone-chain metadata leaked into FBX: {CUSTOM_WEIGHT_CHAINS_KEY}")
    if RIGID_BIND_ATTACHMENTS_KEY in rig:
        raise RuntimeError(f"Authoring-only rigid-attachment metadata leaked into FBX: {RIGID_BIND_ATTACHMENTS_KEY}")
    mesh.data.calc_loop_triangles()
    weights = _weight_audit(mesh)
    torso_coverage = _torso_weight_coverage(mesh)
    modifiers = [modifier for modifier in mesh.modifiers if modifier.type == "ARMATURE"]
    armature_modifier = modifiers[0].object.name if len(modifiers) == 1 and modifiers[0].object else ""
    zero_area = sum(1 for triangle in mesh.data.loop_triangles if triangle.area <= 1e-12)
    warnings = []
    if zero_area:
        warnings.append(f"The FBX round-trip retains {zero_area} zero-area triangles; geometry cleanup remains non-blocking.")
    if weights["zeroWeightVertices"] or weights["verticesOverFourInfluences"] or weights["unnormalizedVertices"]:
        raise RuntimeError(f"FBX weight audit failed: {weights}")
    if len(modifiers) != 1 or armature_modifier != rig.name:
        raise RuntimeError(f"FBX armature modifier is invalid: modifiers={len(modifiers)} target={armature_modifier!r} rig={rig.name!r}")
    return {
        "mesh": mesh.name,
        "vertices": len(mesh.data.vertices), "polygons": len(mesh.data.polygons),
        "triangles": len(mesh.data.loop_triangles), "zeroAreaTriangles": zero_area,
        "vertexGroups": len(mesh.vertex_groups), "armatureModifier": armature_modifier,
        "dimensionsMeters": [round(float(value), 6) for value in mesh.dimensions],
        "scale": [round(float(value), 6) for value in mesh.scale],
        "rotationEuler": [round(float(value), 6) for value in mesh.rotation_euler],
        "materials": [slot.material.name if slot.material else "" for slot in mesh.material_slots],
        "torsoWeightCoverage": torso_coverage, "warnings": warnings, **weights,
    }


def main() -> None:
    source, target, character_id = _arguments()
    if not source.is_file():
        raise RuntimeError(f"FBX does not exist: {source}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    if "FINISHED" not in result:
        raise RuntimeError(f"FBX import failed: {sorted(result)}")

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if not meshes or len(armatures) != 1:
        raise RuntimeError(f"Expected bound meshes and one armature; meshes={len(meshes)} armatures={len(armatures)}")
    rig = armatures[0]
    palette_required = "--require-palette" in sys.argv or any(
        obj.data.uv_layers.get("PaletteUV") is not None or
        obj.data.color_attributes.get("PaletteData") is not None for obj in meshes)
    preflight = runpy.run_path(str(pathlib.Path(__file__).with_name("palette_export_preflight.py")))
    baked_surface = "--baked-surface" in sys.argv
    if baked_surface:
        palette_required = False
        baked = runpy.run_path(str(pathlib.Path(__file__).with_name("baked_surface.py")))
    mesh_reports = []
    for mesh in sorted(meshes, key=lambda obj: obj.name):
        try:
            palette_report = preflight["validate_mesh"](mesh, mesh.data, "FBX 回读检查（导出后）") if palette_required else None
            report = _audit_mesh(mesh, rig)
            if baked_surface:
                report["bakedSurfaceChannels"] = baked["validate_mesh"](mesh)
            if palette_report is not None:
                report["paletteChannels"] = palette_report
            mesh_reports.append(report)
        except RuntimeError as error:
            raise RuntimeError(f"{mesh.name}: {error}") from error
    torso_coverage = {name: sum(item["torsoWeightCoverage"][name] for item in mesh_reports)
                      for name in mesh_reports[0]["torsoWeightCoverage"]}
    missing_torso = [name for name, count in torso_coverage.items() if count == 0]
    if missing_torso:
        raise RuntimeError("FBX required torso bones have no weights: " + ", ".join(missing_torso))
    weights = {key: sum(item[key] for item in mesh_reports) for key in
               ("zeroWeightVertices", "verticesOverFourInfluences", "unnormalizedVertices")}
    weights["maximumInfluences"] = max(item["maximumInfluences"] for item in mesh_reports)
    zero_area = sum(item["zeroAreaTriangles"] for item in mesh_reports)
    warnings = [item["mesh"] + ": " + warning for item in mesh_reports for warning in item["warnings"]]
    armature_modifier = rig.name

    audit = {
        "schemaVersion": "1.1",
        "characterId": character_id,
        "status": "PassedWithWarnings" if warnings else "Passed",
        "sourceBlend": f"Final_Source/{character_id}_authoring.blend",
        "outputFbx": f"Final_Source/{character_id}_LOD0.fbx",
        "exporter": {
            "blenderVersion": bpy.app.version_string,
            "axisForward": "-Z",
            "axisUp": "Y",
            "applyUnitScale": True,
            "applyScaleOptions": "FBX_SCALE_UNITS",
            "triangulate": True,
            "addLeafBones": False,
            "deformBonesOnly": True,
            "bakeAnimation": False,
            "embedTextures": False,
            "pathMode": "STRIP",
        },
        "source": {
            "mesh": meshes[0].name if len(meshes) == 1 else "",
            "meshes": [item["mesh"] for item in mesh_reports],
            "vertices": sum(item["vertices"] for item in mesh_reports),
            "polygons": sum(item["polygons"] for item in mesh_reports),
            "triangles": sum(item["triangles"] for item in mesh_reports),
            "zeroAreaTriangles": zero_area,
            "armature": rig.name,
            "bones": len(rig.data.bones),
            "deformBones": sum(1 for bone in rig.data.bones if bone.use_deform),
            "vertexGroups": sum(item["vertexGroups"] for item in mesh_reports),
            "boneHierarchyHash": _bone_hierarchy_hash(rig),
        },
        "roundTripImport": {
            "meshes": len(meshes),
            "armatures": len(armatures),
            "vertices": sum(item["vertices"] for item in mesh_reports),
            "polygons": sum(item["polygons"] for item in mesh_reports),
            "triangles": sum(item["triangles"] for item in mesh_reports),
            "zeroAreaTriangles": zero_area,
            "dimensionsMeters": mesh_reports[0]["dimensionsMeters"] if len(meshes) == 1 else None,
            "material": mesh_reports[0]["materials"][0] if len(meshes) == 1 and mesh_reports[0]["materials"] else "",
            "bones": len(rig.data.bones),
            "vertexGroups": sum(item["vertexGroups"] for item in mesh_reports),
            "boneHierarchyHash": _bone_hierarchy_hash(rig),
            **weights,
            "torsoWeightCoverage": torso_coverage,
            "armatureModifier": armature_modifier,
            "scale": mesh_reports[0]["scale"] if len(meshes) == 1 else None,
            "rotationEuler": mesh_reports[0]["rotationEuler"] if len(meshes) == 1 else None,
        },
        "meshReports": mesh_reports,
        "warnings": warnings,
    }
    target.parent.mkdir(parents=True, exist_ok=True)
    if baked_surface:
        receipt_path = source.parent / 'character_baked_surface_binding.json'
        if receipt_path.is_file():
            receipt = json.loads(receipt_path.read_text(encoding='utf-8-sig'))
            audit['sourceBlend'] = receipt.get('authoringSourcePath', '')
            audit['sourceSnapshotHash'] = receipt.get('sourceBlendHash', '')
    target.write_text(json.dumps(audit, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("WGAME_FBX_AUDIT " + json.dumps(audit, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
