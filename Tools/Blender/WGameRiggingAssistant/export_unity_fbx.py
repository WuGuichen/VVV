"""Export the open WGame character authority Blend as a deterministic Unity FBX."""

from __future__ import annotations

import json
import pathlib
import runpy
import sys

import bpy
from mathutils import Matrix

CUSTOM_BIND_MASK_GROUP = "WGame.CustomBindMask"
CUSTOM_BIND_MASK_AUTHORITY_KEY = "wgame_custom_bind_mask_authority.v1"
RIGID_BIND_MASK_GROUP = "WGame.RigidBindMask"
RIGID_BIND_ATTACHMENTS_KEY = "wgame_rigging_assistant.rigid_bind_attachments.v1"
BASE_REGION_GROUP_PREFIX = "WGame.BaseRegion."
BASE_REGION_MASK_AUTHORITY_KEY = "wgame_base_region_mask_authority.v1"
CUSTOM_WEIGHT_CHAINS_KEY = "wgame_rigging_assistant.custom_weight_chains.v1"


def _output_path() -> pathlib.Path:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = [arg for arg in args if arg not in {"--require-palette", "--baked-surface"} and not arg.startswith("--texture-size=")]
    if len(args) != 1:
        raise RuntimeError("Expected exactly one FBX output path after --")
    return pathlib.Path(args[0]).resolve()


def _character_objects():
    rigs = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "ARMATURE" and obj.get("wgame_rig_kind") == "deform_skeleton"
    ]
    rig = rigs[0] if len(rigs) == 1 else None
    meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and not obj.name.upper().endswith(("__TOPO_BACKUP", "_TOPO_BACKUP", "__BACKUP"))
        and not obj.name.startswith("cs_")
        and not obj.get("wgame_rig_exclude_from_binding", False)
        and any(modifier.type == "ARMATURE" and modifier.object == rig for modifier in obj.modifiers)
    ] if rig else []
    if rig is None or not meshes:
        raise RuntimeError(f"Expected one WGame rig and bound meshes; found rigs={len(rigs)}, meshes={len(meshes)}")
    return sorted(meshes, key=lambda obj: obj.name), rig


def main() -> None:
    output = _output_path()
    output.parent.mkdir(parents=True, exist_ok=True)
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    meshes, rig = _character_objects()
    baked_surface = "--baked-surface" in sys.argv
    for arg in sys.argv:
        if arg.startswith("--texture-size="):bpy.context.scene['wgame_bake_output_size']=arg.split('=',1)[1]
    bake_document = None
    palette_required = "--require-palette" in sys.argv or any(
        obj.data.uv_layers.get("PaletteUV") is not None or
        obj.data.color_attributes.get("PaletteData") is not None for obj in meshes)
    preflight = runpy.run_path(str(pathlib.Path(__file__).with_name("palette_export_preflight.py")))
    if palette_required:
        preflight["validate_sources"](meshes)

    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for pose_bone in rig.pose.bones:
        pose_bone.matrix_basis = Matrix.Identity(4)
    if baked_surface:
        baker = runpy.run_path(str(pathlib.Path(__file__).with_name("baked_surface.py")))
        bake_document = baker["prepare"](meshes, rig, output.parent, output.stem.split("_")[0])
    custom_mask_excluded = False
    rigid_mask_excluded = False
    for mesh in meshes:
        custom_mask = mesh.vertex_groups.get(CUSTOM_BIND_MASK_GROUP)
        if custom_mask:
            mesh.vertex_groups.remove(custom_mask)
        rigid_mask = mesh.vertex_groups.get(RIGID_BIND_MASK_GROUP)
        if rigid_mask:
            mesh.vertex_groups.remove(rigid_mask)
        for region_group in [group for group in mesh.vertex_groups if group.name.startswith(BASE_REGION_GROUP_PREFIX)]:
            mesh.vertex_groups.remove(region_group)
        if CUSTOM_BIND_MASK_AUTHORITY_KEY in mesh:
            del mesh[CUSTOM_BIND_MASK_AUTHORITY_KEY]
        if BASE_REGION_MASK_AUTHORITY_KEY in mesh:
            del mesh[BASE_REGION_MASK_AUTHORITY_KEY]
        custom_mask_excluded |= custom_mask is not None
        rigid_mask_excluded |= rigid_mask is not None
    if CUSTOM_WEIGHT_CHAINS_KEY in rig:
        del rig[CUSTOM_WEIGHT_CHAINS_KEY]
    if RIGID_BIND_ATTACHMENTS_KEY in rig:
        del rig[RIGID_BIND_ATTACHMENTS_KEY]
    bpy.context.view_layer.update()

    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    rig.hide_set(False)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig

    for mesh in meshes:
        mesh.hide_set(False)
        mesh.select_set(True)
        palette_layer = None if baked_surface else mesh.data.uv_layers.get("PaletteUV")
        if palette_layer is not None and mesh.data.uv_layers[0] == palette_layer:
            palette_layer.name = "UV0"
            new_palette = mesh.data.uv_layers.new(name="PaletteUV")
            for i, loop_data in enumerate(mesh.data.uv_layers["UV0"].data):
                new_palette.data[i].uv = loop_data.uv

        mesh.data.calc_loop_triangles()
    if palette_required and not baked_surface:
        preflight["validate_evaluated"](meshes, rig)
    result = bpy.ops.export_scene.fbx(
        filepath=str(output),
        check_existing=False,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        use_mesh_modifiers_render=True,
        mesh_smooth_type="FACE",
        colors_type="SRGB",
        prioritize_active_color=False,
        use_mesh_edges=False,
        use_tspace=True,
        use_triangles=True,
        use_custom_props=True,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=True,
        armature_nodetype="NULL",
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
        use_metadata=True,
    )
    if "FINISHED" not in result or not output.is_file():
        raise RuntimeError(f"FBX export failed: {sorted(result)}")
    if bake_document is not None:
        pruning = runpy.run_path(str(pathlib.Path(__file__).with_name('fbx_tangent_pruning.py')))
        print('BAKED_FBX_TANGENTS', json.dumps(pruning['optimize_file'](output)), flush=True)
        baker["finalize"](bake_document, output, bpy.data.filepath)

    print(
        "WGAME_FBX_EXPORT "
        + json.dumps(
            {
                "blend": bpy.data.filepath,
                "output": str(output),
                "bytes": output.stat().st_size,
                "meshes": [mesh.name for mesh in meshes],
                "vertices": sum(len(mesh.data.vertices) for mesh in meshes),
                "polygons": sum(len(mesh.data.polygons) for mesh in meshes),
                "triangles": sum(len(mesh.data.loop_triangles) for mesh in meshes),
                "armature": rig.name,
                "bones": len(rig.data.bones),
                "deformBones": sum(bone.use_deform for bone in rig.data.bones),
                "vertexGroups": sum(len(mesh.vertex_groups) for mesh in meshes),
                "customBindMaskExcluded": custom_mask_excluded,
                "rigidBindMaskExcluded": rigid_mask_excluded,
                "customWeightChainsExcluded": CUSTOM_WEIGHT_CHAINS_KEY not in rig,
                "rigidBindAttachmentsExcluded": RIGID_BIND_ATTACHMENTS_KEY not in rig,
                "materials": [slot.material.name if slot.material else "" for mesh in meshes for slot in mesh.material_slots],
            },
            ensure_ascii=False,
            sort_keys=True,
        )
    )


if __name__ == "__main__":
    main()
