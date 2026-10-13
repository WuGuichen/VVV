bl_info = {
    "name": "WGame Weapon Tools",
    "author": "Vincent",
    "version": (1, 0, 1),
    "blender": (5, 0, 0),
    "location": "3D View > Sidebar > WGame Weapon",
    "description": "WGame numeric weapon authoring, trace blade setup, FBX export, and 1-step Unity prefab generation",
    "category": "Game Engine",
}

import datetime
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

import bpy
from bpy.app.handlers import persistent
from bpy.props import BoolProperty, FloatVectorProperty, PointerProperty, StringProperty
from bpy.types import Operator, Panel, PropertyGroup
from mathutils import Vector

from . import export_contract


DEPLOYMENT_FILENAME = "deployment.json"
DEFAULT_AUTHORING_SERVER = "http://127.0.0.1:4873"


# ---------------------------------------------------------------------------
# Path & Environment Helpers
# ---------------------------------------------------------------------------

def get_repository_root(context=None) -> pathlib.Path | None:
    """Resolve repository root directory from deployment.json or current blend path."""
    # 1. Check deployment.json next to this addon
    addon_dir = pathlib.Path(__file__).parent
    dep_file = addon_dir / DEPLOYMENT_FILENAME
    if dep_file.is_file():
        try:
            dep_data = json.loads(dep_file.read_text(encoding="utf-8-sig"))
            repo = pathlib.Path(dep_data.get("repositoryRoot", "")).resolve()
            if (repo / "ArtSource").is_dir() and (repo / "Assets").is_dir():
                return repo
        except Exception:
            pass

    # 2. Check current blend file hierarchy
    blend_path = bpy.data.filepath
    if blend_path:
        cand = pathlib.Path(blend_path).resolve()
        for parent in (cand, *cand.parents):
            if (parent / "ArtSource").is_dir() and (parent / "Assets").is_dir():
                return parent

    # 3. Known fallbacks
    known = [
        pathlib.Path(r"e:\Unity\WxFramework\WGameFramework"),
        pathlib.Path(os.getcwd()).resolve(),
    ]
    for k in known:
        if (k / "ArtSource").is_dir() and (k / "Assets").is_dir():
            return k

    return None


def detect_weapon_id(context) -> str:
    """Detect 6-digit weapon ID from current blend path or active object."""
    blend_path = bpy.data.filepath
    if blend_path:
        # Match .../Weapons/(\d{6})/
        m = re.search(r"[Ww]eapons[/\\](\d{6})", blend_path)
        if m:
            return m.group(1)

    obj = context.active_object
    if obj:
        if "wgame_weapon_id" in obj:
            return str(obj["wgame_weapon_id"]).strip()
        m = re.search(r"(\d{6})", obj.name)
        if m:
            return m.group(1)

    return ""


def get_weapon_meshes(context) -> list[bpy.types.Object]:
    return export_contract.collect_weapon_meshes(context)


def find_or_compute_trace_points(weapon_mesh, require_explicit=False):
    return export_contract.trace_points(bpy.context.scene, weapon_mesh, require_explicit)


# ---------------------------------------------------------------------------
# Property Group
# ---------------------------------------------------------------------------

class WGameWeaponSettings(PropertyGroup):
    weapon_id: StringProperty(
        name="武器编号 (ID)",
        description="6 位唯一数字编号，例如 000001",
        default="",
        maxlen=6,
    )
    weapon_name: StringProperty(
        name="武器名称 (Name)",
        description="武器中/英文名称，例如 Fantasy Sword",
        default="",
    )
    authoring_server_url: StringProperty(
        name="Authoring 服务",
        description="本地 Authoring Server 基础 URL",
        default=DEFAULT_AUTHORING_SERVER,
    )
    auto_apply_transforms: BoolProperty(
        name="自动应用旋转和缩放",
        description="导出前自动应用网格的 Rotation & Scale，确保 FBX 变换干净",
        default=True,
    )
    last_status: StringProperty(
        name="状态",
        default="就绪",
    )
    last_export_message: StringProperty(
        name="详情",
        default="",
    )


# ---------------------------------------------------------------------------
# Operators
# ---------------------------------------------------------------------------

class WGAME_WEAPON_OT_detect_id(Operator):
    bl_idname = "wgame_weapon.detect_id"
    bl_label = "自动识别编号"
    bl_description = "从当前保存的 .blend 路径或选中的网格命名中提取 6 位数字编号"

    def execute(self, context):
        detected = detect_weapon_id(context)
        if detected:
            context.scene.wgame_weapon_settings.weapon_id = detected
            self.report({"INFO"}, f"已识别到武器编号：{detected}")
        else:
            self.report({"WARNING"}, "未能在当前工程路径中识别到 6 位武器编号，请手动输入或点击申请。")
        return {"FINISHED"}


class WGAME_WEAPON_OT_allocate_id(Operator):
    bl_idname = "wgame_weapon.allocate_id"
    bl_label = "申请新武器编号"
    bl_description = "通过项目编号池分配下一个可用的 6 位武器 ID 并创建工程脚手架"

    weapon_name_input: StringProperty(
        name="武器名称",
        description="输入新武器的名称（可留空使用默认名称）",
        default="New Weapon",
    )

    def invoke(self, context, event):
        wm = context.window_manager
        return wm.invoke_props_dialog(self)

    def execute(self, context):
        repo_root = get_repository_root(context)
        if not repo_root:
            self.report({"ERROR"}, "无法定位 WGame 仓库根目录！")
            return {"CANCELLED"}

        script_path = repo_root / "Tools" / "MxFramework.Authoring" / "scripts" / "allocate_numeric_weapon.py"
        if not script_path.is_file():
            self.report({"ERROR"}, f"未找到编号分配脚本：{script_path}")
            return {"CANCELLED"}

        name = self.weapon_name_input.strip() or "New Weapon"
        cmd = [sys.executable, str(script_path), "--name", name]

        try:
            res = subprocess.run(cmd, cwd=str(repo_root), capture_output=True, text=True, check=True)
            output = res.stdout
            # Extract allocated ID from output
            m = re.search(r"Allocated Weapon ID:\s*(\d{6})", output)
            if m:
                allocated_id = m.group(1)
                settings = context.scene.wgame_weapon_settings
                settings.weapon_id = allocated_id
                settings.weapon_name = name
                settings.last_status = f"已分配编号 {allocated_id}"
                self.report({"INFO"}, f"成功分配武器编号：{allocated_id} ({name})")
                return {"FINISHED"}
            else:
                self.report({"WARNING"}, f"分配完成，但未能提取到编号：{output}")
                return {"FINISHED"}
        except subprocess.CalledProcessError as err:
            self.report({"ERROR"}, f"编号分配执行失败：{err.stderr or err.stdout}")
            return {"CANCELLED"}


class WGAME_WEAPON_OT_setup_trace_points(Operator):
    bl_idname = "wgame_weapon.setup_trace_points"
    bl_label = "添加/定位刀刃判定点"
    bl_description = "在场景中创建或选中 trace_blade_root 与 trace_blade_tip，用于定义武器攻击判定线段"

    def execute(self, context):
        weapon_meshes = get_weapon_meshes(context)
        weapon_mesh = weapon_meshes[0] if weapon_meshes else None

        root_pos, tip_pos, reach = find_or_compute_trace_points(weapon_mesh)

        root_obj = None
        tip_obj = None
        for obj in context.scene.objects:
            if "blade_root" in obj.name.lower():
                root_obj = obj
            elif "blade_tip" in obj.name.lower():
                tip_obj = obj

        # Create or update root empty
        if not root_obj:
            bpy.ops.object.empty_add(type="SPHERE", radius=0.04)
            root_obj = context.active_object
            root_obj.name = "trace_blade_root"
            if weapon_mesh:
                root_obj.parent = weapon_mesh
                root_obj.matrix_parent_inverse = weapon_mesh.matrix_world.inverted()
                root_obj.location = root_pos
            else:
                root_obj.location = root_pos

        # Create or update tip empty
        if not tip_obj:
            bpy.ops.object.empty_add(type="SPHERE", radius=0.04)
            tip_obj = context.active_object
            tip_obj.name = "trace_blade_tip"
            if weapon_mesh:
                tip_obj.parent = weapon_mesh
                tip_obj.matrix_parent_inverse = weapon_mesh.matrix_world.inverted()
                tip_obj.location = tip_pos
            else:
                tip_obj.location = tip_pos

        # Select both empties for the user to inspect/adjust
        for obj in context.view_layer.objects:
            obj.select_set(False)
        root_obj.select_set(True)
        tip_obj.select_set(True)
        context.view_layer.objects.active = tip_obj

        self.report({"INFO"}, f"判定点就绪：Root={root_pos.z:.3f}m, Tip={tip_pos.z:.3f}m (攻击距离 {reach:.2f}m)")
        return {"FINISHED"}


class WGAME_WEAPON_OT_apply_transforms(Operator):
    bl_idname = "wgame_weapon.apply_transforms"
    bl_label = "应用网格旋转与缩放"
    bl_description = "将所选武器网格的 Rotation 与 Scale 烘焙为 (1,1,1)，保持握持原点 (0,0,0) 不变"

    def execute(self, context):
        meshes = get_weapon_meshes(context)
        if not meshes:
            self.report({"WARNING"}, "未找到武器网格对象！")
            return {"CANCELLED"}

        count = 0
        for m in meshes:
            context.view_layer.objects.active = m
            m.select_set(True)
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
            m.select_set(False)
            count += 1

        self.report({"INFO"}, f"已成功对 {count} 个武器网格应用旋转与缩放。")
        return {"FINISHED"}


class WGAME_WEAPON_OT_export_source(Operator):
    bl_idname = "wgame_weapon.export_source"
    bl_label = "仅导出武器源 FBX"
    bl_description = "导出全部武器分件与判定点，更新源清单；不写 Unity Assets、不生成 Prefab"

    def execute(self, context):
        settings = context.scene.wgame_weapon_settings
        weapon_id = settings.weapon_id.strip() or detect_weapon_id(context)
        repo_root = get_repository_root(context)
        try:
            if not repo_root: raise ValueError("未找到 WGame 仓库")
            identity = export_contract.catalog_identity(repo_root, weapon_id)
            if settings.weapon_name.strip(): identity["displayName"] = settings.weapon_name.strip()
            folder = repo_root / "ArtSource" / "Common" / "Weapons" / weapon_id
            output = folder / "model" / f"{weapon_id}.fbx"
            meshes, start, tip, reach, has_palette = export_contract.export_fbx(context, output, settings.auto_apply_transforms)
            manifest_path = folder / "weapon_sources.json"
            previous = json.loads(manifest_path.read_text(encoding="utf-8-sig")) if manifest_path.is_file() else None
            document = export_contract.weapon_manifest(identity, start, tip, reach, has_palette,
                datetime.datetime.now(datetime.timezone.utc).isoformat(), previous)
            manifest_path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            settings.last_status = "武器源导出完成"
            settings.last_export_message = f"FBX: {output}\n网格: {len(meshes)} 件；Trace: {reach:.4f}m\n未触发 Unity 导入或重建"
            self.report({"INFO"}, settings.last_status)
            return {"FINISHED"}
        except Exception as error:
            self.report({"ERROR"}, f"武器源导出失败：{error}")
            return {"CANCELLED"}


class WGAME_WEAPON_OT_export_and_generate_prefab(Operator):
    bl_idname = "wgame_weapon.export_and_generate_prefab"
    bl_label = "一键导出并生成 Unity 预制体"
    bl_description = "全自动执行：网格规范化 -> 导出 FBX -> 提取判定点 -> 同步资源 -> 驱动 Unity 生成 Prefab"
    bl_options = {"REGISTER"}

    def execute(self, context):
        settings = context.scene.wgame_weapon_settings
        weapon_id = (settings.weapon_id or "").strip()

        if not re.match(r"^\d{6}$", weapon_id):
            # Try auto detect
            detected = detect_weapon_id(context)
            if re.match(r"^\d{6}$", detected):
                weapon_id = detected
                settings.weapon_id = weapon_id
            else:
                self.report({"ERROR"}, "武器编号必须为 6 位数字（如 000001），请先指定编号。")
                return {"CANCELLED"}

        repo_root = get_repository_root(context)
        if not repo_root:
            self.report({"ERROR"}, "未能定位到 WGame 仓库根目录！")
            return {"CANCELLED"}

        meshes = get_weapon_meshes(context)
        if not meshes:
            self.report({"ERROR"}, "当前场景中没有可见的武器网格对象！")
            return {"CANCELLED"}

        art_source_weapon_dir = repo_root / "ArtSource" / "Common" / "Weapons" / weapon_id
        fbx_output = art_source_weapon_dir / "model" / f"{weapon_id}.fbx"
        sources_json_path = art_source_weapon_dir / "weapon_sources.json"
        try:
            identity = export_contract.catalog_identity(repo_root, weapon_id)
            if settings.weapon_name.strip():
                identity["displayName"] = settings.weapon_name.strip()
            meshes, root_pos, tip_pos, reach, has_palette = export_contract.export_fbx(
                context, fbx_output, settings.auto_apply_transforms)
            previous = json.loads(sources_json_path.read_text(encoding="utf-8-sig")) if sources_json_path.is_file() else None
            sources_data = export_contract.weapon_manifest(
                identity, root_pos, tip_pos, reach, has_palette,
                datetime.datetime.now(datetime.timezone.utc).isoformat(), previous)
            sources_json_path.write_text(json.dumps(sources_data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        except Exception as error:
            self.report({"ERROR"}, f"武器源导出失败：{error}")
            return {"CANCELLED"}
        trace_root_str = ",".join(f"{value:.4f}" for value in export_contract.unity_position(root_pos).values())
        trace_tip_str = ",".join(f"{value:.4f}" for value in export_contract.unity_position(tip_pos).values())
        reach_str = f"{reach:.4f}"

        # Revision-protected single-item registration preserves other weapons and VFX.
        authoring_url = settings.authoring_server_url.rstrip("/")
        try:
            export_contract.sync_single_configuration(authoring_url, identity, root_pos, tip_pos, reach, repo_root)
            sync_result = export_contract.api_json(authoring_url, "/api/authoring/common-resources/sync", {
                "itemIds": [f"weapon:{weapon_id}:model"], "force": True})
            if not sync_result.get("success") or not sync_result.get("operations"):
                raise RuntimeError(json.dumps(sync_result, ensure_ascii=False))
        except Exception as error:
            settings.last_status = "源已导出；公共资源同步未完成"
            settings.last_export_message = str(error)
            self.report({"ERROR"}, f"源 FBX 已保留；未触发 Unity 重建：{error}")
            return {"CANCELLED"}

        # 6. Trigger Unity Prefab Rebuild via CommonWeaponImportRequestBridge
        request_id = f"weapon_{weapon_id}_{int(time.time() * 1000)}"
        request_dir = repo_root / "Temp" / "MxFrameworkAuthoring" / "CommonWeapons"
        request_dir.mkdir(parents=True, exist_ok=True)
        request_file = request_dir / "rebuild_request.json"
        status_dir = request_dir / "status"
        status_dir.mkdir(parents=True, exist_ok=True)
        status_file = status_dir / f"{request_id}.json"

        request_payload = {
            "requestId": request_id,
            "weaponId": weapon_id,
            "createdUtc": datetime.datetime.now(datetime.timezone.utc).isoformat()
        }
        request_file.write_text(json.dumps(request_payload, indent=2), encoding="utf-8")

        # Poll status for up to 8 seconds
        unity_rebuilt = False
        rebuild_message = ""
        poll_start = time.time()
        while time.time() - poll_start < 8.0:
            time.sleep(0.3)
            if status_file.is_file():
                try:
                    status_info = json.loads(status_file.read_text(encoding="utf-8"))
                    curr_st = status_info.get("status")
                    if curr_st == "completed":
                        unity_rebuilt = True
                        rebuild_message = status_info.get("message", "Prefab generated successfully.")
                        break
                    elif curr_st == "failed":
                        rebuild_message = "Unity 重建失败: " + status_info.get("message", "")
                        break
                except Exception:
                    pass

        prefab_relative = f"Assets/MxFrameworkGenerated/Common/Weapons/{weapon_id}/prefabs/{weapon_id}_weapon.prefab"
        prefab_full = repo_root / pathlib.Path(*prefab_relative.split("/"))

        if unity_rebuilt and prefab_full.is_file():
            status_msg = f"已一键导出并直接生成 Unity 预制体！"
            detail_msg = (
                f"编号: {weapon_id}\n"
                f"预制体: {prefab_relative}\n"
                f"判定点: Root=({trace_root_str}), Tip=({trace_tip_str})\n"
                f"攻击距离: {reach_str}m\n"
                f"Unity 状态: {rebuild_message}"
            )
            settings.last_status = "导出并生成成功"
            settings.last_export_message = detail_msg
            self.report({"INFO"}, f"成功！Unity 预制体已生成: {prefab_relative}")
        elif prefab_full.is_file():
            status_msg = f"FBX 已导出，Unity 预制体现已存在。"
            detail_msg = f"编号: {weapon_id}\n预制体: {prefab_relative}\nFBX: {fbx_output.name}"
            settings.last_status = "完成 (Prefab 已存在)"
            settings.last_export_message = detail_msg
            self.report({"INFO"}, f"FBX 导出完成；预制体已就绪: {prefab_relative}")
        else:
            status_msg = f"FBX 与清单已同步至 Unity 目录"
            detail_msg = (
                f"FBX 与 weapon_sources.json 已就绪。\n"
                f"若 Unity Editor 此时未在后台运行，请切回 Unity 或在 Unity 菜单点击：\n"
                f"MxFramework -> Weapons -> Synchronize Public Weapon Prefabs"
            )
            settings.last_status = "FBX 已就绪 (等待 Unity)"
            settings.last_export_message = detail_msg
            self.report({"WARNING"}, "FBX 已同步到 Unity 目录。如 Unity 尚未打开，打开后将自动生成 Prefab。")

        return {"FINISHED"}


# ---------------------------------------------------------------------------
# UI Panel
# ---------------------------------------------------------------------------

class VIEW3D_PT_wgame_weapon_tools(Panel):
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "WGame Weapon"
    bl_label = "WGame 武器制作与导出"

    def draw(self, context):
        layout = self.layout
        settings = context.scene.wgame_weapon_settings
        repo_root = get_repository_root(context)

        # Repository Status
        box = layout.box()
        if repo_root:
            row = box.row()
            row.label(text="项目仓库：已连接", icon="CHECKMARK")
        else:
            row = box.row()
            row.label(text="项目仓库：未找到", icon="ERROR")

        # Weapon Identity Section
        box = layout.box()
        box.label(text="1. 武器编号与元数据", icon="OBJECT_DATAMODE")
        col = box.column(align=True)
        row = col.row(align=True)
        row.prop(settings, "weapon_id", text="编号 (ID)")
        row.operator("wgame_weapon.detect_id", text="", icon="FILE_REFRESH")
        col.prop(settings, "weapon_name", text="名称")

        row_alloc = box.row()
        row_alloc.operator("wgame_weapon.allocate_id", text="申请新武器编号...", icon="ADD")

        # Mesh & Trace Section
        box = layout.box()
        box.label(text="2. 攻击判定点 (Trace Blade)", icon="ORIENTATION_GIMBAL")
        
        # Show detected weapon mesh
        meshes = get_weapon_meshes(context)
        if meshes:
            box.label(text=f"武器导出网格: {len(meshes)} 件", icon="MESH_DATA")
        else:
            box.label(text="未找到武器网格对象", icon="ERROR")

        col_tools = box.column(align=True)
        col_tools.operator("wgame_weapon.apply_transforms", text="应用旋转与缩放 (Origin保持原点)", icon="CON_ROTLIMIT")
        col_tools.operator("wgame_weapon.setup_trace_points", text="添加/定位刀刃判定点", icon="EMPTY_AXIS")

        # One-Step Export & Build Button
        box_export = layout.box()
        box_export.label(text="3. 一键发布与生成", icon="EXPORT")
        box_export.prop(settings, "auto_apply_transforms")
        box_export.operator("wgame_weapon.export_source", text="仅导出武器源 FBX", icon="EXPORT")

        # Big Button
        col_btn = box_export.column()
        col_btn.scale_y = 1.8
        col_btn.operator(
            "wgame_weapon.export_and_generate_prefab",
            text="🚀 一步导出并生成 Unity 预制体",
            icon="IMPORT"
        )

        # Status readout
        if settings.last_export_message:
            box_res = layout.box()
            box_res.label(text="最近发布结果：", icon="INFO")
            for line in settings.last_export_message.splitlines():
                box_res.label(text=line)


# ---------------------------------------------------------------------------
# Registration
# ---------------------------------------------------------------------------

classes = (
    WGameWeaponSettings,
    WGAME_WEAPON_OT_detect_id,
    WGAME_WEAPON_OT_allocate_id,
    WGAME_WEAPON_OT_setup_trace_points,
    WGAME_WEAPON_OT_apply_transforms,
    WGAME_WEAPON_OT_export_source,
    WGAME_WEAPON_OT_export_and_generate_prefab,
    VIEW3D_PT_wgame_weapon_tools,
)


@persistent
def auto_detect_on_load(dummy):
    try:
        ctx = bpy.context
        if hasattr(ctx.scene, "wgame_weapon_settings"):
            st = ctx.scene.wgame_weapon_settings
            if not st.weapon_id:
                wid = detect_weapon_id(ctx)
                if wid:
                    st.weapon_id = wid
    except Exception:
        pass


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.wgame_weapon_settings = PointerProperty(type=WGameWeaponSettings)
    if auto_detect_on_load not in bpy.app.handlers.load_post:
        bpy.app.handlers.load_post.append(auto_detect_on_load)


def unregister():
    if auto_detect_on_load in bpy.app.handlers.load_post:
        bpy.app.handlers.load_post.remove(auto_detect_on_load)
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    if hasattr(bpy.types.Scene, "wgame_weapon_settings"):
        del bpy.types.Scene.wgame_weapon_settings


if __name__ == "__main__":
    register()
