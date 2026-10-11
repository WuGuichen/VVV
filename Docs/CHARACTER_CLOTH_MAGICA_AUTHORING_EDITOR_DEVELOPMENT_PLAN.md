# Character Cloth / Magica Collider Authoring Editor 开发计划

> Status: Current
>
> Date: 2026-08-29
>
> Implemented: 2026-08-29
>
> Scope: 为 Character Package 提供可视化、可撤销、可校验、可重建的 Magica Cloth / Collider Authoring 工作区。M0～M4 的当前实现、边界与验收由本文维护。

## 1. 决策摘要

为解决目前只能手改 `config/character_cloth_magica.json`、生成 Prefab 不可作为权威、Collider 难以定位和管理的问题，新增 Unity Editor 工作区：

```text
MxFramework
→ Character
→ Cloth Authoring
```

核心决策如下：

1. `Authoring/CharacterPackages/<package>/config/character_cloth_magica.json` 继续是唯一持久化权威。
2. `Assets/MxFrameworkGenerated/CharacterPackages/...` 继续是可重建投影，不允许作为保存源。
3. Unity Editor 负责真实骨骼解析、Collider Scene Handle、运动预览和 Prefab 重建；CharacterStudio 第一阶段只显示状态和跳转入口，不重复实现 3D Collider 编辑器。
4. Scene Handle 拖拽期间只改内存草稿和瞬态预览；鼠标抬起时形成一次 Undo 事务。禁止每个 Repaint 或 MouseDrag 都写 JSON。
5. 保存必须携带加载时的 expected hash；磁盘内容在编辑期间变化时 fail-closed，禁止覆盖外部修改。
6. 保存成功后显式执行 Unity Import/Rebuild，校验 Preview Prefab、Runtime Resources Prefab 和持久化 Unity receipt。
7. 不修改 `Assets/MagicaCloth2` 供应商源码；所有适配保留在 `MxFramework.Character.Cloth.Magica.Unity.Editor`。
8. Cloth 只属于表现层，不进入 Character / Gameplay / Combat 权威、SaveState、Replay、网络 hash、命中挂点或战斗碰撞。

### 1.1 当前实现状态

当前窗口视觉基线参考：[`Docs/Images/CharacterCloth/cloth-authoring-ui-optimized-concept-v1.png`](Images/CharacterCloth/cloth-authoring-ui-optimized-concept-v1.png)。实际 IMGUI 实现按该图适度简化，保持 Unity 原生控件、现有数据合同与操作语义。

M0～M4 已实现：

- `MxFramework/Character/Cloth Authoring` EditorWindow。
- 共享 typed document、未知字段无损 stable serialization、deep clone。
- expected disk hash 与 clean document hash 分离。
- expected-hash conflict、同目录临时文件、atomic replace、accepted hash。
- Cloth/Collider CRUD、复制、删除、排序、快捷后脑/脖子/上背模板。
- 可搜索 BodyModel Bone Picker，保存完整相对路径。
- Prefab Stage 只读预览、初始化 dirtiness 清理和重入保护。
- Scene View Position/Rotation/Radius/Capsule Length/End Radius Handles。
- pending drag、MouseUp 单 Undo、Escape cancel、selected/all Collider outline。
- Unity 原生 unsaved-change Save/Discard 提示和 SessionState 草稿恢复。
- 合并“保存并重建”主流程、Preview/Runtime parity、acceptance receipt 与待重建状态；常规 Cloth 修改走只同步 CLI 文件、重投影两个现有 Prefab 和刷新 receipt 的快速路径，缺少基础产物或失败时自动回退完整角色导入。
- 隔离 Play Mode 肉眼运动预览：正式动画注册表、7 种可调运动预设、固定世界/跟随角色双镜头、四向观察、米制网格/路径/运动遥测、播放/暂停/循环/倍速、Collider 线框、Cloth Reset，以及内存 Cloth/Collider 草稿实时热应用。
- 运动预览按角色持久化最后选中的稳定 `clipId`；重进 Play Mode、窗口重开和 Domain Reload 后恢复同一动作，只有动作不可加载时才回退并记住默认动作，不依赖列表索引。
- focused PlayMode 自动验收保留在更多菜单，不再占用肉眼预览主入口。
- CharacterStudio readiness Cloth 状态卡及“在 Unity 中编辑”请求桥。
- 空 Cloth 草稿可根据当前 Preview 模型的蒙皮骨骼层级与实际顶点权重初始化：排除 WGame 基础骨、胸部链、单骨链和零权重链，按共同基础身体区域合并 Root，并按角色高度与真实基础骨段拟合最小防穿模 Collider 集；下半身 Cloth 只生成左右 thigh Capsule，每个单侧上半身 Cloth 生成覆盖 `pelvis.x` 到 `neck.x` 的 `torso` Capsule 与同侧 upper-arm Capsule。`000004` 的骨盆与左右上身候选完整初始化为三个 Cloth、六个 Collider，分配为 `2 + 2 + 2`。已有 Collider 全为空时可确认后按同一模型建议整理内存草稿，部分已有时只补空项；完整配置始终保留带当前/建议数量确认、支持 Undo 且保存前不写磁盘的“根据模型重新初始化…”入口。
- 所有模型初始化 Cloth 统一使用 `MC2_Preset_Cape.json`；Cloth 工作区提供标准 Preset 下拉框，切换时原子更新路径与 SHA-256 并保留参数 Override，不要求手填配置字段。
- Preset 下拉同时发现框架自带的 WGame 柔软/中等/硬挺三档完整 Preset；应用快速档位时确认清除旧参数 Override，保证档位基线可复现，但不改变 Root、Collider 或 Cloth 分组。
- 初始化草稿在 Cape 上叠加分区域的折中运动 Override：World Inertia 保持中低以降低急停前冲，Local Inertia 高于 World Inertia 以恢复骨链弯曲；两类均使用适度阻尼与放宽后的移动、旋转和粒子速度限制，避免稳定基线把衣片锁成硬片。
- Character Prefab 构建缓存纳入生成包 `config/**/*.json` 内容聚合 hash，Cloth 配置变化会使缓存失效；Cloth Authoring 的强制重建显式跳过 Prefab 缓存，避免配置已更新但 Preview/Runtime Prefab 仍复用无 Magica 投影的旧结果。

当前保留限制：

- Scene View 只通过窗口列表选择 Collider；尚不从点击任意线框反向选择列表项。
- MeshCloth、自动拟合和任意 AnimationCurve 编辑仍属于非目标/后续项。
- Magica optional integration 对外 UPM 支持等级仍为 O0。
- CharacterStudio 全量旧 smoke 仍被 numeric CharacterActionId 与旧 `startsWith` 断言不兼容阻断；Cloth-focused smoke 独立通过。

## 2. 当前事实基线

### 2.1 已实现

当前仓库已经具备：

- Magica Cloth 2 `2.18.2`，项目 define `MAGICACLOTH2` 已启用。
- `MxFramework.Character.Cloth` noEngine 状态契约。
- `MxFramework.Character.Cloth.Magica.Unity` Runtime adapter。
- `MxFramework.Character.Cloth.Magica.Unity.Editor` Prefab 后处理器。
- `config/character_cloth_magica.json` 兼容 schema `1.0`，typed 参数 Override 使用 schema `1.1`。
- BoneCloth、`Line` connection、Preset path/hash、Sphere/Capsule Collider 投影。
- 完整 BodyModel-relative bone path 唯一解析。
- 导入模型缩放补偿，Magica Collider 最终 world scale 可验证。
- Preview Prefab 与 Runtime Resources Prefab 双写。
- `MagicaCharacterClothController` / `MagicaCharacterClothSystem` 自动生成。
- `wgame_palette_mage_v1` 的 Play Mode 异步 Build、运动响应和四态往返测试。

当前关键代码：

| Path | Responsibility |
|---|---|
| `Packages/com.vincent.wgameframework/Character.Cloth.Magica.Unity/Editor/MagicaCharacterClothPrefabPostprocessor.cs` | 把 package-local Magica JSON 投影到生成 Prefab。 |
| `Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterImportedPackagePrefabPostprocessor.cs` | 可选生成后处理扩展点。 |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/CharacterPackageCommands.cs` | 把声明的 Cloth JSON 写入 Unity 生成目录。 |
| `Docs/Interfaces/CharacterCloth.md` | Character Cloth 当前公共边界。 |

### 2.2 开发前缺口（已由本计划关闭）

实施前不存在：

- Cloth Authoring Unity EditorWindow。
- CharacterStudio Cloth 页面或状态卡。
- Collider 列表的增加、复制、删除、排序入口。
- 骨骼搜索/选择器。
- Scene View Position/Radius/Capsule Length Handle。
- 选中、聚焦、显隐、Solo、Wireframe 辅助。
- JSON expected-hash guarded save service。
- Cloth 草稿 Undo/Redo。
- 保存后的一键 Import/Rebuild/receipt 验收。
- 外部文件变化冲突提示。
- Collider 参数结构化诊断与修复建议。

当前 `wgame_palette_mage_v1` 已保存 `head` Sphere 与 `upper_back` Capsule Collider；Cloth Authoring 仍可通过列表、快捷模板、Bone Picker 和 Scene Handle 增加 `neck` 等 Collider，不要求手工编辑 JSON。

## 3. 目标与非目标

### 3.1 目标

本计划交付后，用户可以：

1. 从 Unity 菜单打开 Cloth Authoring。
2. 选择一个已声明 `config/character_cloth_magica.json` 的 Character Package。
3. 看到 Cloth、Root Bones、Preset、Collider 的结构化列表。
4. 新增 Sphere/Capsule Collider。
5. 从真实 BodyModel 层级选择父骨。
6. 在 Scene View 中移动 Collider 并调整 Radius/Length。
7. 使用 Undo/Redo、Escape 取消拖拽、复制和删除。
8. 看到 dirty、磁盘 hash、导入状态和阻断诊断。
9. 通过“保存并重建”一次操作写入 Authoring JSON 并重建两份 Prefab。
10. 在隔离 Play Mode 中切换正式动作、运动预设、倍速、镜头和 Collider 线框，肉眼检查 Cloth 表现。
11. 需要自动结构验收时，从更多菜单运行 focused PlayMode test。

### 3.2 非目标

第一阶段不实现：

- MeshCloth 顶点 Paint / Texture Paint。
- 自动从渲染网格生成精确身体 Collider。
- 自动判断“艺术效果最好”的碰撞参数。
- 修改 Magica 供应商 Inspector 或源码。
- 把 Magica 类型引入 noEngine Character、Gameplay 或 Combat。
- 把布料骨骼变成命中、Trace、Socket 或网络权威。
- 在 CharacterStudio 浏览器视口中复刻 Magica 求解器。
- UPM 对外 Magica O1/O2/O3 支持声明；来源、许可和分发仍按现有 O0 决策管理。

## 4. 用户工作流

### 4.1 打开与选择角色

菜单：

```text
MxFramework
→ Character
→ Cloth Authoring
```

顶部工作流条：

```text
[角色包 ▼] [重新加载] [校验] [保存] [重建 Prefab] [Play 验证]
```

角色候选直接来自 Character Package 列表，并要求：

- Authoring package 可读取；
- manifest 有有效 `packageId`；
- 不要求预先存在 `config/character_cloth_magica.json`；未配置角色在选中后进入空草稿，首次保存时创建配置并写入 `manifest.json.configFiles[]`。

Unity 生成包不存在或 stale 时，窗口仍允许选择角色、读取或创建 Authoring 草稿，但 Scene 预览和重建入口必须明确显示不可用或 stale 状态。

### 4.2 主界面布局

```text
┌──────────────────────────────────────────────────────────────────────┐
│ 角色: wgame_palette_mage_v1   Draft*   hash: abcd...   Receipt: Ready │
├──────────────────┬────────────────────────┬───────────────────────────┤
│ Cloth 列表        │ Collider 列表           │ 详细属性                   │
│                  │                        │                           │
│ hair.tail        │ head       Sphere      │ Id: head                  │
│  BoneCloth       │ neck       Sphere      │ Bone: head.x [选择]        │
│  Root: c_tail... │ upper_back Capsule     │ Position: 0, .11, 0       │
│                  │                        │ Radius: .13               │
│ [+ Cloth]        │ [+Sphere] [+Capsule]   │ [聚焦] [显示] [复制] [删除] │
├──────────────────┴────────────────────────┴───────────────────────────┤
│ Diagnostics: 0 blocker / 1 warning    [保存] [重建] [进入 Play 验证]   │
└──────────────────────────────────────────────────────────────────────┘
```

Scene View 与窗口选择保持同步：

- 窗口选择 Collider，Scene View 高亮并显示 Handle。
- Scene View 点击 Collider，窗口定位对应条目。
- `F` 或“聚焦”把 Scene View 对准 Collider。
- Solo 只显示当前 Cloth 的 Collider/骨链辅助，不改变模型或 JSON。

### 4.3 新增 Collider

`+ Sphere`：

1. 选择默认父骨；若当前选择的是骨骼则使用该骨，否则打开 Bone Picker。
2. 默认 `radius=0.1`，位置为父骨局部原点。
3. 生成唯一 ID，例如 `head_2`，允许立即重命名。
4. 进入选中状态，Scene View 显示 Position + Radius Handle。
5. 只修改内存草稿，保存前不改 Authoring JSON。

`+ Capsule`：

1. 默认 `radius=endRadius=0.08`、`length=0.2`、`direction=Y`。
2. Scene View 显示 Position、Rotation、两端 Radius 和 Length Handle。
3. Capsule 的 `length` 必须不小于两端直径要求；非法值在草稿中可见但阻断保存。

### 4.4 快捷模板

在普通增加之外提供显式模板，但模板只创建草稿，不自动保存：

```text
Head Sphere
Back Head Sphere
Neck Sphere
Upper Back Capsule
Left/Right Shoulder Capsules
```

模板必须要求用户确认目标骨骼。不能根据名字猜中后无提示写入正式配置。

## 5. 权威与数据流

```text
Authoring package JSON（唯一权威）
  │
  ├─ Load + expected SHA-256
  │
  ▼
Cloth Authoring in-memory draft
  │
  ├─ Inspector edits / list CRUD
  ├─ Scene handles / Undo / Redo
  ├─ validation diagnostics
  │
  ▼ explicit Save
expected-hash guarded atomic write
  │
  ▼ explicit Rebuild
CharacterPackageImportCommand / Authoring CLI
  │
  ├─ generated config mirror
  ├─ Preview Prefab
  ├─ Runtime Resources Prefab
  └─ Unity import receipt
```

禁止的数据流：

```text
Generated Prefab edit → Authoring JSON       禁止隐式反向解析
Scene instance edit → Generated Prefab       禁止保存场景实例为权威
Magica component state → Combat authority    禁止
Personal UI state → Character package JSON   禁止
```

## 6. 数据合同

### 6.1 当前 schema 1.0 / 1.1

当前文档结构保持兼容：

```json
{
  "schemaVersion": "1.1",
  "backend": "MagicaCloth2",
  "enabled": true,
  "cloths": [
    {
      "id": "hair.tail",
      "clothType": "BoneCloth",
      "rootBonePaths": [".../c_tail_00.x"],
      "connectionMode": "Line",
      "presetAssetPath": "Assets/MagicaCloth2/Res/Preset/MC2_Preset_Tail.json",
      "presetSha256": "sha256:...",
      "parameterOverrides": {
        "damping": { "value": 0.08 },
        "worldInertia": { "value": 0.8 },
        "movementSpeedLimit": { "use": true, "value": 5.0 },
        "angleLimit": { "use": true, "value": 80.0 },
        "collisionFriction": { "value": 0.08 },
        "windInfluence": { "value": 0.2 },
        "springPower": { "use": true, "value": 0.01 }
      },
      "colliders": [
        {
          "id": "head",
          "type": "Sphere",
          "parentBonePath": ".../head.x",
          "localPosition": { "x": 0, "y": 0.11, "z": 0 },
          "localRotationEuler": { "x": 0, "y": 0, "z": 0 },
          "radius": 0.13,
          "endRadius": 0,
          "length": 0,
          "direction": "Y"
        }
      ]
    }
  ]
}
```

旧 schema `1.0` 文档继续兼容；首次启用参数 Override 时窗口把内存草稿升级为 `1.1`。窗口必须 round-trip 未识别但合法的未来字段，或在无法无损保存时进入只读阻断状态，禁止反序列化后静默丢字段。

### 6.2 UI 状态不得进入合同

以下内容仅保存在 `SessionState` / `EditorPrefs` 或内存，不写入角色包：

- 当前选中 Cloth/Collider。
- 折叠状态。
- Scene View 相机。
- Solo/显隐。
- Wireframe 开关。
- 列表滚动位置。
- 最近角色包。
- 个人颜色和 Handle 大小。

### 6.3 schema 1.1 typed 参数 Override（Completed）

当前开放 25 项基础运动、惯性、形状保持、碰撞、风和弹簧 Override。每个字段显式选择继承 Preset 或 Override；速度限制、Angle Restoration、Angle Limit、Max Distance 和 Spring 同时保存 `use` 与数值。标量 Override 会关闭该字段的 Preset Curve，未 Override 字段继续继承 hash-frozen Preset。任意 AnimationCurve 编辑、`collisionMode`、particle radius、Collider group/reuse 与左右对称生成 receipt 仍属于后续项。

## 7. Editor 架构

### 7.1 程序集边界

新增代码位于：

```text
Packages/com.vincent.wgameframework/
  Character.Cloth.Magica.Unity/Editor/
```

程序集：

```text
MxFramework.Character.Cloth.Magica.Unity.Editor
```

它保持：

- `includePlatforms = Editor`
- `defineConstraints = MAGICACLOTH2`
- 单向依赖 MagicaClothV2、Runtime adapter 和 `MxFramework.Editor`
- 不被 noEngine / Gameplay / Combat 引用
- 不修改供应商源码

### 7.2 计划文件

| Proposed file | Responsibility |
|---|---|
| `MagicaCharacterClothAuthoringDocument.cs` | 与 schema 1.0/1.1 对应的 typed DTO、parameterOverrides、clone、stable serialization。 |
| `MagicaCharacterClothAuthoringRepository.cs` | load、expected hash、atomic save、conflict result。 |
| `MagicaCharacterClothAuthoringValidator.cs` | 文档、骨路径、Preset/hash、尺寸、重复 ID、缩放诊断。 |
| `MagicaCharacterClothAuthoringWindow.cs` | 主 EditorWindow、列表和属性 UI。 |
| `MagicaCharacterClothBonePicker.cs` | 可搜索骨层级、完整路径存储和重名显示。 |
| `MagicaCharacterClothPreviewSession.cs` | 瞬态角色实例、骨映射、显示和清理。 |
| `MagicaCharacterClothSceneViewEdit.cs` | Scene View Handles、pending drag、mouse-up commit、Escape cancel。 |
| `MagicaCharacterClothRebuildService.cs` | Save 后 import/rebuild、receipt 读取和结果归一化。 |
| `MagicaCharacterClothPrefabPostprocessor.cs` | 继续只负责确定性投影；改为复用 typed document/validator。 |

### 7.3 DTO 抽取

当前 Prefab 后处理器中的私有 nested DTO 必须抽取为 Editor adapter 内共享类型，避免 Authoring Window 和生成器维护两套 JSON 结构。

要求：

- stable property order；
- invariant float formatting；
- trailing newline 固定；
- clone 不共享数组/对象引用；
- 未知字段策略明确；
- JSON load 错误不产生默认空文档覆盖磁盘。

## 8. Preview 与 Scene View

### 8.1 瞬态预览

Authoring Window 不直接修改正式 Scene 或 Prefab。预览会话创建一个瞬态角色实例：

- `HideFlags.HideAndDontSave`；
- 关闭时无条件销毁；
- 不保存 Scene；
- 不把临时 Magica Collider 写入生成 Prefab；
- 打开/关闭前后活动 Scene dirty state必须一致；
- 脚本重编译、Play Mode 切换、窗口关闭和异常路径都必须清理。

若无法证明当前 Scene 不被污染，回退到只读 Prefab Stage + 纯 Handles overlay，不得用“通常不会保存”作为保证。

### 8.2 Scene GUI 模式

复用仓库现有模式：

- `SceneView.duringSceneGui` 注册/注销对称。
- `Handles.PositionHandle` 编辑世界位置。
- `Handles.RadiusHandle` 编辑 Sphere 半径。
- Capsule 使用两个端点 Slider + 起止 Radius Handle。
- 拖拽中只更新 `_pending` draft preview。
- `MouseUp` 提交一个 typed edit。
- `Escape` 恢复拖拽前值。
- `SceneView.RepaintAll()` 只用于 UI 刷新，不写磁盘。

直接参考：

- `Packages/com.vincent.wgameframework/Editor/ProjectileAuthoring/ProjectileSceneViewEdit.cs`
- `Assets/Scripts/WGameFrameworkProject/Editor/Combat/CombatGizmoDrawer.cs`

### 8.3 坐标转换

JSON 保存的是父骨局部值；Handle 显示在世界空间。变换必须使用：

```text
worldPosition = parentBone.TransformPoint(localPosition)
localPosition = parentBone.InverseTransformPoint(worldPosition)
worldRotation = parentBone.rotation * Quaternion.Euler(localRotationEuler)
```

Radius/Length 保存为最终世界米制值。Preview 中的 Handle 不把 BodyModel import scale 重复乘入文档；生成器继续负责实际 Component 的 inverse-lossy-scale 补偿。

非有限、零尺度或非等比角色缩放必须显示阻断诊断。第一阶段不支持在非等比缩放下编辑 Magica Collider。

## 9. Bone Picker

Bone Picker 从 Preview Prefab 的 `BodyModel` 扫描真实 Transform：

- 存储完整 BodyModel-relative path；
- 列表显示短名和路径尾部；
- 同名骨显示完整父链；
- 搜索支持 leaf name、完整路径和常用语义 token；
- 选择时不改场景 Selection authority；
- 未找到当前路径时保留原字符串并进入阻断，不自动替换为名字相似的骨。

Root Bone 与 Collider Parent Bone 使用同一个 picker，但过滤/提示不同：

- Root Bone 提示其子链数量和受影响 SkinnedMesh 权重证据。
- Collider Parent Bone 提示 world scale 与当前角色层级。

## 10. 草稿、Undo 与 Dirty

### 10.1 草稿模型

窗口打开时创建独立 draft：

```text
LoadedDocument
LoadedHash
DraftDocument
DraftRevision
IsDirty
ValidationResult
```

编辑不会立刻写磁盘。`IsDirty` 基于 stable serialized content 比较，不仅依赖按钮事件。

### 10.2 Undo/Redo

建议使用隐藏的 Editor-only `ScriptableObject` 承载草稿并进入 Unity Undo 栈：

- 增加/删除/复制/排序各是一条事务；
- Inspector 数值一次 commit 一条事务；
- Scene drag 从 MouseDown 到 MouseUp 合并为一条事务；
- 显隐、Solo、聚焦、折叠、选择不进入文档 Undo；
- Undo/Redo 后重新验证并刷新 Preview。

### 10.3 关闭窗口

存在 dirty draft 时关闭、切角色或重新加载，必须给出：

```text
保存
放弃
取消
```

禁止自动保存，也禁止因 domain reload 静默丢失 draft。Domain reload 前保存到 `SessionState` 的只能是草稿恢复数据，不得冒充已写入 Authoring package。

## 11. 保存与并发冲突

### 11.1 expected-hash guard

Load 时记录当前 JSON SHA-256。Save 时：

1. 重新计算磁盘 hash。
2. 与 `LoadedHash` 比较。
3. 不相等则返回 `Conflict`，不写文件。
4. UI 显示当前 hash、expected hash 和“重新加载/另存草稿”入口。

第一阶段不做自动三方合并。Collider 数组顺序、ID 重命名和骨路径变化存在语义，自动 merge 风险高。

### 11.2 atomic write

保存采用同目录临时文件：

```text
character_cloth_magica.json.tmp.<transactionId>
```

流程：

1. stable serialize 到临时文件；
2. 重新读取临时文件并执行完整 validation；
3. 若目标存在，使用 atomic replace；否则 move；
4. 清理临时文件；
5. 重新读取正式文件并计算 accepted hash；
6. 更新窗口 `LoadedDocument/LoadedHash`；
7. 生成结构化 Save Result。

IO、权限、路径逃逸和序列化错误必须保留原文件。

## 12. Validation 与诊断

诊断至少包含：

| Code | Severity | Meaning |
|---|---|---|
| `MAGCLOTH001` | Error | JSON/schema/backend 无效。 |
| `MAGCLOTH002` | Error | Cloth ID 缺失或重复。 |
| `MAGCLOTH003` | Error | `clothType` 不受支持。 |
| `MAGCLOTH004` | Error | Root Bone 路径缺失、未找到或歧义。 |
| `MAGCLOTH005` | Error | Collider ID 缺失或同 Cloth 内重复。 |
| `MAGCLOTH006` | Error | Collider Parent Bone 无效。 |
| `MAGCLOTH007` | Error | Sphere/Capsule 尺寸非有限或不为正。 |
| `MAGCLOTH008` | Error | Capsule direction 非 X/Y/Z。 |
| `MAGCLOTH009` | Error | Preset 路径缺失、越界或 hash 不匹配。 |
| `MAGCLOTH016` | Error | schema 1.1 参数 Override 版本、范围、方向或有限值非法。 |
| `MAGCLOTH010` | Error | 角色/父骨 world scale 不可用或非等比。 |
| `MAGCLOTH011` | Error | 保存 expected hash 冲突。 |
| `MAGCLOTH012` | Error | Preview/Runtime Prefab 投影不一致。 |
| `MAGCLOTH013` | Warning | Collider 未覆盖任何模拟粒子邻域。 |
| `MAGCLOTH014` | Warning | BoneCloth 只有一个 Move 粒子，碰撞表达能力有限。 |
| `MAGCLOTH015` | Warning | 只有头部 Collider，脖子/背部无覆盖。 |

诊断需要：

- 稳定 code；
- Cloth/Collider ID；
- 字段路径；
- 当前值；
- 修复建议；
- “定位到骨/Collider”动作；
- Error 阻断 Save 或 Rebuild 的边界明确。

保存前验证不要求 Magica runtime Build；重建后验收必须包含真实组件与 receipt。

## 13. Save、Rebuild 与 Receipt

按钮语义分开：

- `保存`：只写 Authoring JSON，不生成 Unity 资产。
- `重建 Prefab`：要求 draft 已保存且无 blocker，调用现有 Character Import 路径。
- `Play 验证`：要求最新 receipt Ready、Prefab hash/identity 当前。

Rebuild 成功必须验证：

1. generated config mirror 与 Authoring JSON 内容一致。
2. Preview Prefab 的 Cloth/Collider 数量、ID、路径、尺寸正确。
3. Runtime Resources Prefab 与 Preview Prefab 一致。
4. Root Bones 和 Collider List 无 missing reference。
5. Controller/System 各恰好一个。
6. Avatar、Weapon Trace、Socket Bake、Hurtbox Bake 不因 Cloth 重建退化。
7. Unity import receipt `ready=true` 且 identity 对应当前 package hash。

## 14. CharacterStudio 边界

M1 中 CharacterStudio 只增加只读 Cloth 状态卡：

```text
头发/布料物理
状态: 已配置 / 未配置 / 阻断 / Unity 产物过期
Cloth: 1
Collider: 3
Preset: Tail
[在 Unity 中编辑 / 创建]
```

CharacterStudio 不实现：

- Magica Scene Handles；
- Unity bone object reference；
- 实时 Magica simulation；
- 对生成 Prefab 的写入。

如果未来在 CharacterStudio 增加列表编辑，也必须复用 Authoring Service expected-revision/atomic-save 合同，不得与 Unity Window 分裂成两套保存协议。

## 15. 测试计划

### 15.1 EditMode

新增可选测试程序集，受 `UNITY_INCLUDE_TESTS && MAGICACLOTH2` 保护，覆盖：

- schema 1.0/1.1 round-trip 与 25 项 typed Override 映射；
- stable serialization/hash；
- clone 深复制；
- duplicate Cloth/Collider ID；
- invalid bone path；
- Preset hash mismatch；
- Sphere/Capsule 尺寸与 direction；
- parent/local/world 坐标往返；
- uniform scale compensation；
- pending Scene drag commit/cancel；
- Undo/Redo；
- expected-hash conflict；
- atomic save 保留原文件；
- Preview cleanup 不改变 Scene dirty state；
- Prefab postprocessor 复用同一 validator。

### 15.2 PlayMode

扩展现有：

```text
WGamePaletteMageMagicaClothPlayModeTests
```

覆盖：

- 生成 Runtime Prefab 可从 Resources 加载；
- 异步 Build 完成；
- 增加 neck/back Collider 后 Collider List 数量匹配；
- 角色运动时头发骨响应；
- Running / Paused / DisabledKeep / DisabledReset 往返；
- disable/enable 与对象池式复用；
- 快速移动/停止不产生 NaN 或错误日志。

### 15.3 人工视觉验收

至少保存以下视图证据：

- 后视图：后脑、脖子、上背 Collider 覆盖。
- 侧视图：Collider 不把头发顶离身体过远。
- 转身、跑动、急停：无明显穿脖/穿背。
- Pause/Resume：姿态稳定。
- Collider Gizmo 开关、聚焦和 Solo 正确。

人工验收不能被结构化测试替代；结构化测试也不能被单张截图替代。

## 16. 分阶段实施

### M0：共享文档服务与 Validator（Completed）

交付：

- 抽取 typed DTO；
- stable serializer；
- repository/load/hash/atomic save；
- validator/diagnostic codes；
- Prefab postprocessor 改为复用服务；
- focused EditMode tests。

退出条件：同一 JSON 经 Window service 和 Prefab builder 解释结果一致。

### M1：列表 CRUD 与 Bone Picker（Completed）

交付：

- Window 菜单和角色选择；
- Cloth/Collider 列表；
- Sphere/Capsule add/duplicate/delete/reorder；
- 骨选择器；
- dirty、Undo/Redo；
- Save conflict UI。

退出条件：无需手改 JSON 完成多 Collider 保存和重开 round-trip。

### M2：Scene View Handles（Completed）

交付：

- 瞬态 Preview session；
- Position/Rotation/Radius/Length handles；
- MouseUp 单事务；
- Escape cancel；
- focus/show/hide/Solo；
- scene dirty invariants。

退出条件：用户可在 Scene View 完成 head/neck/back Collider 摆放，关闭窗口后 Scene 未脏。

### M3：Rebuild 与验收闭环（Completed）

交付：

- “保存并重建”主按钮与“仅保存草稿/强制重建”更多菜单；
- import progress/result；
- Preview/Runtime parity；
- receipt/status cards；
- PlayMode focused test；
- Console diagnostics。

退出条件：保存后一次操作得到当前两份 Prefab与 Ready receipt。

### M5：肉眼运动预览（Completed）

交付：

- 正式动画注册表与全局资源映射发现；
- 隔离 Play Mode Runtime Prefab、专用相机/灯光/地面；
- 动作、播放、暂停、循环、倍速与进度显示；
- 原地、往返、横移、启停、转身和 Z 字压力运动，以及位移、周期、停顿、转向速度参数；
- 固定世界/跟随角色双镜头与四向观察；
- 米制地面网格、距离刻度、完整运动路径、当前位置/朝向标记；
- 世界位置、线速度、加速度与角速度遥测；
- Runtime Collider 绿色线框、Cloth Reset；
- 运动中保持 Cloth/Collider 编辑器可用，有效草稿去抖后重建运行投影；
- 热应用失败保留最后有效投影，运行修改不写 JSON 或生成 Prefab；
- 自动验收迁入更多菜单，与肉眼预览分离。

退出条件：用户不进入 Test Runner 即可连续肉眼检查当前正式 Prefab 在多种动作和整体运动下的 Cloth 表现。

### M4：CharacterStudio 状态入口（Completed）

交付：

- Cloth 状态卡；
- Cloth/Collider/Preset 摘要；
- stale/blocker 中文问题卡；
- “在 Unity 中编辑”请求/状态链。

退出条件：CharacterStudio 不再让用户猜测 Cloth 是否配置，但仍不复制 Unity Scene authoring。

## 17. 验收标准

当前 UI 已进入 Current；以下条目是持续回归标准：

1. 用户不编辑 JSON 也能增加、复制、删除 Sphere/Capsule。
2. Bone Picker 保存完整 BodyModel-relative path。
3. Scene Handle 修改与数值 Inspector 双向一致。
4. 一次拖拽只产生一条 Undo 事务。
5. Escape 能恢复拖拽前值。
6. 关闭/切角色时 dirty draft 有保存/放弃/取消门禁。
7. 外部修改造成 hash 变化时保存被阻断。
8. 原子写失败不损坏原 JSON。
9. Preview session 不改变 Scene dirty state。
10. 保存不修改生成 Prefab；Rebuild 才更新生成资产。
11. Preview/Runtime 两份 Prefab Cloth/Collider 一致。
12. receipt 对应当前 package identity 且 Ready。
13. `wgame_palette_mage_v1` 至少有 head/neck/upper_back 覆盖并通过实际 Play 验收。
14. Unity refresh/compile、Editor ready、Console error 检查通过。
15. 不修改 `Assets/MagicaCloth2` 供应商源码。

## 18. 风险与对策

| Risk | Mitigation |
|---|---|
| 生成 Prefab 被误当权威 | Window 始终显示 Authoring path；保存按钮只写 JSON；生成资产只读。 |
| Preview 污染正式 Scene | HideAndDontSave + 生命周期清理 + scene dirty 自动测试。 |
| Scene drag 高频写盘 | pending drag，MouseUp 单次 typed commit。 |
| 外部 CharacterStudio 同时保存 | expected-hash conflict，禁止 last-writer-wins。 |
| Bone 重名或模型升级 | 保存完整路径；missing/ambiguous fail-closed。 |
| Magica Preset 更新改变效果 | `presetSha256` 冻结；不匹配阻断。 |
| BodyModel 缩放重复应用 | 文档保存世界米制半径；生成器单点补偿。 |
| Collider 太大导致头发漂浮 | Scene handle + Side/Back manual acceptance。 |
| 两骨发束仍有限 | 显示 `MAGCLOTH014` warning；不把 Collider 调参伪装成骨架质量修复。 |
| Optional adapter 扩大 UPM 声明 | 保持现有 O0；本计划只交付当前项目 Authoring。 |

## 19. 实施顺序与后续演进

本次实现已按 `M0 → M1 → M2 → M3 → M4` 完成。

不建议先做一个直接修改生成 Prefab 的 Custom Inspector。那会快速提供 Scene Handle，但破坏 JSON 权威、重导入可重复性和 Preview/Runtime 镜像一致性，随后仍需返工保存事务和冲突处理。

当前可用闭环是：

```text
打开窗口
→ 选择 wgame_palette_mage_v1
→ 新增 neck Sphere
→ 新增 upper_back Capsule
→ Scene View 调整
→ Undo/Redo
→ 保存 JSON
→ 关闭并重开精确还原
```

Import/receipt/PlayMode 验收和 CharacterStudio 只读状态均已接入同一生产链。schema 1.1 受约束参数 Override 已接入实时热应用；后续演进优先考虑 Scene 线框点击反选、项目自有 Preset 管理和受约束 Curve 编辑，不回退到手工编辑生成 Prefab。
