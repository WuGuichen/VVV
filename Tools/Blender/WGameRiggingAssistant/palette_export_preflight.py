"""Read-only Palette channel checks shared by publishing and FBX round-trip audit."""

import bpy


def validate_mesh(obj, mesh, stage, allow_single_uv=False):
    uv_names = [layer.name for layer in mesh.uv_layers]
    color_names = [layer.name for layer in mesh.color_attributes]
    prefix = f"{stage}：对象 '{obj.name}' / 网格 '{mesh.name}'"
    issues = []
    uv = mesh.uv_layers.get("PaletteUV")
    if uv is None:
        issues.append(f"缺少 PaletteUV；实际 UV 层={uv_names}")
    elif not (uv_names.index("PaletteUV") == 1 or
              (allow_single_uv and uv_names == ["PaletteUV"])):
        issues.append(f"PaletteUV 位于 UV{uv_names.index('PaletteUV')}，Unity 要求 UV1（第二套）；实际 UV 层={uv_names}")
    if uv is not None and len(uv.data) != len(mesh.loops):
        issues.append(f"PaletteUV 数据数量={len(uv.data)}，要求网格角数量={len(mesh.loops)}")
    color = mesh.color_attributes.get("PaletteData")
    if color is None:
        issues.append(f"缺少 RGBA PaletteData；实际颜色层={color_names}")
    else:
        # FBX import can materialize float colors as byte colors; both carry RGBA.
        if color.domain != "CORNER" or color.data_type not in {"FLOAT_COLOR", "BYTE_COLOR"}:
            issues.append(f"PaletteData 类型={color.domain}/{color.data_type}，要求 CORNER RGBA")
        if len(color.data) != len(mesh.loops):
            issues.append(f"PaletteData 数据数量={len(color.data)}，要求网格角数量={len(mesh.loops)}")
        if color_names[0] != "PaletteData":
            issues.append(f"首个颜色层为 '{color_names[0]}'，Unity 顶点颜色要求 PaletteData；实际颜色层={color_names}")
    covered = {loop.vertex_index for loop in mesh.loops}
    loose = [vertex.index for vertex in mesh.vertices if vertex.index not in covered]
    if loose:
        issues.append(f"{len(loose)} 个顶点不属于任何面，无法携带 CORNER UV/RGBA；顶点索引示例={loose[:8]}；请点击发布按钮下方“自动清理无面顶点（先备份）”，或为需要保留的顶点补全所属面")
    if not mesh.polygons:
        issues.append("网格没有面，无法导出色板表面数据")
    if issues:
        raise RuntimeError(prefix + "；" + "；".join(issues))
    return {"uvLayers": uv_names, "colorLayers": color_names,
            "vertices": len(mesh.vertices), "corners": len(mesh.loops),
            "paletteUvCount": len(uv.data), "paletteDataCount": len(color.data)}


def validate_sources(objects):
    for obj in objects:
        validate_mesh(obj, obj.data, "导出前源网格检查", allow_single_uv=True)


def validate_evaluated(objects, rig):
    """Match the exporter viewport dependency graph with the rig in rest pose."""
    previous_pose = rig.data.pose_position
    try:
        rig.data.pose_position = "REST"
        graph = bpy.context.evaluated_depsgraph_get()
        graph.update()
        for obj in objects:
            evaluated = obj.evaluated_get(graph)
            mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=graph)
            try:
                modifiers = [f"{mod.name} ({mod.type})" for mod in obj.modifiers if mod.show_viewport]
                validate_mesh(obj, mesh, "修改器求值后检查，启用的修改器=" + str(modifiers))
            finally:
                evaluated.to_mesh_clear()
    finally:
        rig.data.pose_position = previous_pose
        bpy.context.view_layer.update()
