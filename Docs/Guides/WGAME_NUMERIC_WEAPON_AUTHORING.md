# WGame 六位数字武器与 DCC 制作资产管线规范

> Status: Current
>
> Implementation: Authoring CLI, Weapon ID Catalog, Blender DCC Pipeline, Unity CommonWeaponWrapperPrefabBuilder

---

## 1. 身份与编目 (Identity & Catalog)

武器作为独立可复用的项目级公共资产（参见 [`Docs/CHARACTER_WEAPON_CONFIGURATION.md`](../CHARACTER_WEAPON_CONFIGURATION.md)），不再依附于单个角色包。新武器采用统一的中心化编号体系进行管理。

### 1.1 编号池与分配权威
- **编目注册表**：固定维护于 [`Authoring/WeaponPackages/weapon_id_catalog.json`](file:///e:/Unity/WxFramework/WGameFramework/Authoring/WeaponPackages/weapon_id_catalog.json)。
- **ID 格式**：固定为 **6 位数字字符串**（`000000` ～ `999999`）；其中 **`000000` 为系统永久保留的内置“空手”（Unarmed）专属 ID**，实体武器编号从 `000001` 开始分配。
- **不可随意手填**：新武器通过编号池的 `nextCandidate` 事务性分配；分配后自动递增编号池版本与游标。
- **退休机制**：归档或弃用的武器 ID 移入 `retiredIds`，**编号永久退休，绝不回收复用**。

### 1.2 全局统一标识映射
一旦确定 6 位数字 ID `<sixDigitId>`（例如 `000001`），系统各层级严格使用确定性命名，禁止字符串猜测：

| 层级 / 资产 | 命名格式 | 示例（以 `000001` 为例） |
| :--- | :--- | :--- |
| **武器唯一 ID** | `<sixDigitId>` | `000001` |
| **美术工程根目录** | `ArtSource/Common/Weapons/<sixDigitId>/` | `ArtSource/Common/Weapons/000001/` |
| **Unity 生成目录** | `Assets/MxFrameworkGenerated/Common/Weapons/<sixDigitId>/` | `Assets/MxFrameworkGenerated/Common/Weapons/000001/` |
| **模型 ResourceKey** | `weapon.<sixDigitId>.model` | `weapon.000001.model` |
| **贴图 ResourceKey** | `weapon.<sixDigitId>.texture.albedo` | `weapon.000001.texture.albedo` |
| **单一武器配置 ID** | `weapon.<sixDigitId>.main` | `weapon.000001.main` |
| **Trace 判定轨迹 ID** | `trace.<sixDigitId>.blade` | `trace.000001.blade` |
| **Unity 包装 Prefab** | `<sixDigitId>_weapon.prefab` | `000001_weapon.prefab` |
| **Unity 包装材质** | `<sixDigitId>_Weapon.mat` | `000001_Weapon.mat` |

### 1.3 存量旧武器兼容
现存的非数字命名武器（如 `common_weapon_1`、`combat_girl_sword_sword`、`common_kite_shield`）保持只读兼容，属于历史豁免（Legacy Exemptions）；所有新建武器必须强制分配 6 位数字 ID。

---

## 2. 美术工程目录规范 (ArtSource Structure)

每个武器的源资产存放在独立的 `ArtSource/Common/Weapons/<sixDigitId>/` 目录下，遵循标准的文件分层：

```text
ArtSource/Common/Weapons/<sixDigitId>/
  ├── Blender_Work/
  │    └── <sixDigitId>_work.blend        # 日常建模、打磨与历史备份工作工程
  ├── Final_Source/
  │    ├── <sixDigitId>_authoring.blend   # 终审通过的 Blender 权威工程
  │    └── <sixDigitId>_LOD0.fbx          # 自动化导出的正式 Unity 模型
  ├── textures/                           # (贴图模式下使用)
  │    └── weapon_albedo.png              # 武器 Albedo 贴图
  ├── README.md                           # 武器制作说明、作者、来源与参数记录
  └── weapon_sources.json                 # 原材料元数据清单
```

### 2.1 版本化约束
- `Final_Source/<sixDigitId>_authoring.blend` 与 `Final_Source/<sixDigitId>_LOD0.fbx` 必须通过 Git LFS 纳入版本管理。
- `.blend1`、autosave、crash 恢复与无意义时间戳备份文件**严禁提交入库**。

---

## 3. Blender DCC 制作规范与检验门禁

为了确保武器能够无缝挂载到角色的骨骼 Socket、正确显示材质并准确产生受击判定，模型在 Blender 中必须遵守以下制作硬门禁：

### 3.1 握持原点（Grip Pivot）对齐
- **世界原点 `(0, 0, 0)` 必须是武器的握持基准点（Grip Point）**。
- 角色的右手或左手插槽挂点（Socket）在实例化武器时，会将武器的 `(0, 0, 0)` 对齐到手掌心中心。
- 如果握持位置偏移，角色持握时会出现悬空或穿透。

### 3.2 坐标系与朝向
- **Unity / CharacterStudio 坐标系约定**：左手系，**+Y 轴向上（武器长度竖直延伸方向）**，**+Z 轴向前（正面朝向）**，**+X 轴向侧面**。
- **Blender 建模朝向**：
  - **武器长度/刀刃朝向**：沿 **+Z** 轴竖直向上延伸（在 Blender 视口中自然直立）。
  - **正面朝向**：沿 **-Y** 轴。
  - **侧向**：沿 **+X** 轴。
- **导出自动烘焙**：导出时通过 `bake_space_transform=True`，将 Blender 的 `+Z` 自然烘焙为 Unity 的 `+Y`（竖直），同时 Transform 保持干净的 `(0, 0, 0)` 无二次旋转。

### 3.3 变换应用（Applied Transforms）
- 武器网格的 `Location` 必须为 `(0, 0, 0)`。
- `Rotation` 必须全部应用为 `(0, 0, 0)`。
- `Scale` 必须全部应用为 `(1.0, 1.0, 1.0)`。
- 单位比例：1 Blender Unit = 1 Meter（米制）。

### 3.4 攻击判定轨迹标定（Trace Locators）
无需在外部配置文件中盲猜长度与坐标，美术可在 Blender 权威工程中直接使用两个 **Empty（空物体）** 标记武器的攻击有效判定线段：
1. **`trace_blade_root`**：位于刀刃/攻击部根部（起始判定点）。
2. **`trace_blade_tip`**：位于刀刃/攻击部末梢尖端（终止判定点）。

> **说明**：导出脚本会自动提取这两个 Empty 相对武器原点的局部坐标，并计算武器攻击距离（`reachMeters = distance(root, tip)`），自动生成 Global Weapon Library 所需的 `traceProfiles`。

### 3.5 材质与着色规范
武器支持以下两种着色路径之一：
1. **传统贴图路径（StylizedLit）**：
   - 展好 UV0，贴图输出至 `textures/weapon_albedo.png`。
   - `weapon_sources.json` 声明 `materialPolicy = "wgame.prop_stylized_lit"`。
   - Unity 端自动生成使用 `WGame/PropStylizedLit` Shader 的材质。
2. **项目统一色板路径（Shared Color Palette）**：
   - 使用 [`Tools/Blender/WGamePaletteTools`](file:///e:/Unity/WxFramework/WGameFramework/Tools/Blender/WGamePaletteTools) 插件着色。
   - 顶点承载 `PaletteUV` 与 `PaletteData`，在 `weapon_sources.json` 根级声明 `"paletteMode": "sharedWGameAtlas"`。
   - Unity 端自动生成使用 `WGame/PropPaletteStylizedLit` Shader 的材质，零贴图开销。

---

## 4. 自动化导出脚本与元数据生成 (`export_weapon_fbx.py`)

仓库在 `Tools/Blender/` 提供专用武器导出脚本，支持在 Blender 中一键导出合规 FBX 并同步元数据。

### 4.1 导出前自动预检 (Preflight)
脚本在导出前执行严格断言：
- [x] 检测网格 `Location` 是否在世界原点。
- [x] 检测网格 `Rotation` 与 `Scale` 是否已完全 Apply。
- [x] 检测场景中是否存在且仅存在一组配对的 `trace_blade_root` 与 `trace_blade_tip`。
- [x] 若使用色板模式，检测 `PaletteUV` 坐标中心与 `PaletteData.A` 表面 Profile 合法性。
- [x] 网格必须三角化（Triangulated），不可存在非流形边或孤立顶点。

### 4.2 FBX 导出参数契约
```python
bpy.ops.export_scene.fbx(
    filepath=str(output_fbx_path),
    check_existing=False,
    use_selection=True,
    object_types={"MESH", "EMPTY"},   # 包含网格与判定定位空物体
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    use_space_transform=True,
    bake_space_transform=True,
    axis_forward="-Z",
    axis_up="Y",
    use_triangles=True,
    mesh_smooth_type="FACE",
    colors_type="SRGB",
    bake_anim=False,                   # 武器网格严禁烘焙动画
    embed_textures=False               # 严禁内嵌贴图
)
```

### 4.3 `weapon_sources.json` 自动更新契约
导出成功后，脚本自动生成或更新同目录下的 `weapon_sources.json`：
```json
{
  "schemaVersion": "1.0",
  "weaponId": "000001",
  "displayName": "DisplayName Here",
  "sourceRoot": "ArtSource/Common",
  "items": [
    {
      "id": "weapon-model",
      "label": "Weapon model",
      "presetId": "weaponModel",
      "required": true,
      "source": {
        "kind": "file",
        "scope": "public",
        "path": "Weapons/000001/Final_Source/000001_LOD0.fbx"
      },
      "target": {
        "resourceKey": "weapon.000001.model",
        "resourceKind": "Model",
        "resourceUsage": "weaponModel",
        "importHints": {
          "scale": 1,
          "modelWrapperPose": {
            "parentKind": "ModelRoot",
            "position": { "x": 0, "y": 0, "z": 0 },
            "rotation": { "x": 0, "y": 0, "z": 0, "w": 1 },
            "scale": { "x": 1, "y": 1, "z": 1 }
          },
          "materialPolicy": "wgame.prop_stylized_lit",
          "collisionPolicy": "authoringTraceOnly"
        }
      }
    }
  ]
}
```

---

## 5. Unity 导入桥与构建闭环 (Build Pipeline)

当导出的 FBX 与 `weapon_sources.json` 就绪后，进入框架的统一生成与装配阶段。

### 5.1 第一方资源同步
在仓库根目录执行第一方资源同步命令：
```powershell
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli -- resource-mapping sync-first-party
```
1. 扫描 `ArtSource/Common/Weapons/<sixDigitId>/weapon_sources.json`。
2. 将 FBX 和贴图拷贝到 `Assets/MxFrameworkGenerated/Common/Weapons/<sixDigitId>/`。
3. 自动向 `Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json` 注册该武器的 ResourceKey 与资产映射。

### 5.2 Unity Prefab 与材质构建
Unity Editor 检测到 profile 变动后，`CommonWeaponWrapperPrefabBuilder` 自动执行构建：
1. 根据 `materialPolicy` 在 `materials/` 下创建材质资产。
2. 在 `prefabs/` 下创建 `<sixDigitId>_weapon.prefab`，挂载 Renderer 并绑定材质。
3. 应用 `modelWrapperPose` 修正，使得该 Prefab 在加载后可直接挂载到手部 Socket。

### 5.3 Global Weapon Library 注册
在 [`WeaponSource/global_weapon_library.json`](file:///e:/Unity/WxFramework/WGameFramework/WeaponSource/global_weapon_library.json) 的 `singleWeaponConfigurations` 中注入条目：
```json
{
  "singleWeaponConfigurationId": "weapon.000001.main",
  "displayName": "武器名称",
  "category": "OneHandMelee",
  "allowedSlots": ["mainHand", "offHand"],
  "weaponRef": {
    "resourceStableId": "global.profile.common.model.weapon.000001.model",
    "sourceProviderId": "resourceMapping",
    "bindingKind": "ResourceManagerAsset",
    "expectedKind": "Model",
    "expectedUsage": "weaponModel",
    "unityAssetPath": "Assets/MxFrameworkGenerated/Common/Weapons/000001/model/000001_LOD0.fbx",
    "runtimeResourceKey": "weapon.000001.model"
  },
  "modelPose": {
    "position": { "x": 0, "y": 0, "z": 0 },
    "rotation": { "x": 0, "y": 0, "z": 0, "w": 1 },
    "scale": { "x": 1, "y": 1, "z": 1 }
  },
  "combatConfiguration": {
    "reachMeters": 1.2,
    "impactScale": 1,
    "combatTags": ["melee", "slash"]
  },
  "traceProfiles": [
    {
      "traceId": "trace.000001.blade",
      "startLocatorPath": "weapon/trace_blade_root",
      "endLocatorPath": "weapon/trace_blade_tip",
      "startPose": { "position": { "x": 0, "y": 0.1, "z": 0 } },
      "endPose": { "position": { "x": 0, "y": 1.2, "z": 0 } },
      "radius": 0.05
    }
  ]
}
```

至此，任何角色即可在其 `character_application.json` 的武器组合中直接引用 `weapon.<sixDigitId>.main`，完成整条从 Blender 建模到游戏内挥舞战斗的闭环。

## 当前原生插件入口（2026-10-08）

Blender 的 `N → WGame Weapon → 仅导出武器源 FBX` 与 `Tools/Blender/export_weapon_fbx.py` 共用导出契约，只生成源 FBX 和清单，不同步 Unity。当前原生插件实际导出权威路径是 `ArtSource/Common/Weapons/<id>/model/<id>.fbx`；上文 `Final_Source/<id>_LOD0.fbx` 是历史目录约定，不是当前按钮写入位置，不应同时维护两份运行时模型权威。终审 Blend 归档仍属于美术验收。

`一步导出并生成 Unity 预制体` 另外执行带 revision 的单一武器注册、当前武器的 Common Resources 同步及 Unity 重建请求；它不替角色选择动作组合。新武器类别取编号池记录，现有拖尾与战斗字段保留。服务无法完整读取当前公共库字段时停止，不能用旧 DTO 重写库。

共享色板的实际 Shader 是 `WGame/PropStylizedLit`，材质启用 `_WG_PALETTE_ON` 并绑定共享 RGB565 图集；并不存在该链所需的独立 `WGame/PropPaletteStylizedLit` Shader。`resource-mapping sync-first-party` 注册已生成的资源，不代替 `POST /api/authoring/common-resources/sync` 从 ArtSource 复制源文件。
